using Microsoft.Extensions.Options;

namespace Recipes.Pages.Recipes;

public class EditModel(RecipesContext db, ReportService reports, IOptions<CompanyOptions> options) : PageBase
{
    [FromRoute] public int? Id { get; set; }
    [BindProperty] public RecipeInput Input { get; set; } = new();
    [BindProperty] public List<IngredientInput> Products { get; set; } = [];
    [BindProperty] public NewIngredientRow NewRow { get; set; } = new();

    public Dictionary<int, ProductInfo> Details { get; private set; } = [];
    public string? LastRecipeCode { get; private set; }

    public record ProductInfo(string Code, string Name, string UnitOfMeasure, decimal PurchasePrice);

    public class RecipeInput
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal VatPercent { get; set; }
        public int Multiplier { get; set; } = 10;
        public decimal Markup { get; set; } = 3; // only used to work out the price, not saved
        public decimal PackagingPrice { get; set; } = 0.36m;
    }

    public class IngredientInput
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class NewIngredientRow
    {
        public string? Search { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    // ---- computed values shown under the lines
    public decimal Cost(int i) => Products[i].Quantity * Details[Products[i].ProductId].PurchasePrice;
    public decimal Portion(int i) => Input.Multiplier > 0 ? Cost(i) / Input.Multiplier : 0;
    public decimal TotalValue => Enumerable.Range(0, Products.Count).Sum(Cost);
    public decimal TotalPerPortion => Enumerable.Range(0, Products.Count).Sum(Portion);
    public decimal TotalWithMarkupAndVat => TotalWithMarkup * (1 + Input.VatPercent / 100);
    public decimal TotalWithMarkup => TotalPerPortion * Input.Markup + Input.PackagingPrice;

    public IActionResult OnGet()
    {
        if (Id is null) Input.VatPercent = options.Value.StandardVat * 100;
        else
        {
            var recipe = db.Recipes.AsNoTracking().Include(r => r.Products).FirstOrDefault(r => r.ID == Id);
            if (recipe is null) return RedirectToPage("Index");

            Input = new RecipeInput { Code = recipe.Code, Name = recipe.Name, Category = recipe.Category, Quantity = recipe.Quantity, Price = recipe.Price, VatPercent = recipe.Vat * 100, Multiplier = recipe.QuantityMultiplier };
            Products = recipe.Products.Select(p => new IngredientInput { ProductId = p.ProductId, Quantity = p.Quantity }).ToList();
        }

        Prepare();
        return Page();
    }

    public IActionResult OnPostAdd()
    {
        var product = Resolve();
        ModelState.ErrorIf(NewRow.Quantity <= 0 && !string.IsNullOrWhiteSpace(NewRow.Search), "NewRow.Quantity", "Quantity must be greater than zero");
        ModelState.ErrorIf(product is null && !string.IsNullOrWhiteSpace(NewRow.Search), "NewRow.Search", "Product not found. Pick it from the list or type its exact code or name.");

        if (ModelState.IsValid && product is not null)
        {
            Products.Add(new IngredientInput { ProductId = product.ID, Quantity = NewRow.Quantity });
            NewRow = new();
            ModelState.Clear();
        }

        Prepare();
        return Page();
    }

    public IActionResult OnPostRemove(int index)
    {
        if (index >= 0 && index < Products.Count) Products.RemoveAt(index);
        ModelState.Clear();
        Prepare();
        return Page();
    }

    public IActionResult OnPostRecalculate()
    {
        ModelState.Clear();
        Prepare();
        return Page();
    }

    public IActionResult OnPost()
    {
        var recipe = Validate();
        if (recipe is null)
        {
            Prepare();
            return Page();
        }

        db.SaveChanges();
        Message($"Recipe {recipe.Name} was saved.");
        return RedirectToPage("Index");
    }

    public IActionResult OnPostClone()
    {
        if (Id is null) return RedirectToPage("Index");
        var selection = db.Recipes.AsNoTracking().Include(r => r.Products).FirstOrDefault(r => r.ID == Id);
        if (selection is null) return RedirectToPage("Index");

        db.Recipes.Add(new Recipe
        {
            Quantity = selection.Quantity, Category = selection.Category, QuantityMultiplier = selection.QuantityMultiplier, Price = selection.Price, Vat = selection.Vat,
            Code = $"{selection.Code} ({selection.ID}!)", Name = $"{selection.Name} ({selection.ID}!)",
            Products = selection.Products.Select(p => new RecipeIngredient { Quantity = p.Quantity, ProductId = p.ProductId }).ToList()
        });
        db.SaveChanges();

        Message("The recipe was cloned.");
        return RedirectToPage("Index");
    }

    /// <summary>Validates the posted recipe and applies it to the context; null (with errors in ModelState) when invalid.</summary>
    private Recipe? Validate()
    {
        ModelState.Required(Input.Name, "Input.Name", "Recipe name is required");
        ModelState.Required(Input.Category, "Input.Category", "Recipe category is required");
        ModelState.Required(Input.Code, "Input.Code", "Recipe code is required");
        ModelState.Required(Input.Quantity, "Input.Quantity", "Recipe quantity is required");
        ModelState.ErrorIf(Input.VatPercent is < 0 or > 100, "Input.VatPercent", "VAT rate is invalid");
        ModelState.ErrorIf(Input.Multiplier <= 0, "Input.Multiplier", "Portions yielded must be greater than zero");
        ModelState.ErrorIf(!string.IsNullOrWhiteSpace(NewRow.Search), "NewRow.Search", "The product in the add row has not been added (press +).");

        for (int i = 0; i < Products.Count; i++)
        {
            ModelState.ErrorIf(Products[i].Quantity <= 0, $"Products[{i}].Quantity", "Quantity must be greater than zero");
            ModelState.ErrorIf(!db.Products.Any(p => p.ID == Products[i].ProductId), $"Products[{i}].ProductId", "The product no longer exists");
        }

        if (ModelState.IsValid)
        {
            ModelState.ErrorIf(db.Recipes.Any(r => r.ID != Id && r.Name == Input.Name), "Input.Name", "Another recipe has the same name");
            ModelState.ErrorIf(db.Recipes.Any(r => r.ID != Id && r.Code == Input.Code), "Input.Code", "Another recipe has the same code");
        }
        if (!ModelState.IsValid) return null;

        Recipe recipe;
        if (Id is null) db.Recipes.Add(recipe = new Recipe { Code = Input.Code!, Name = Input.Name!, Category = Input.Category!, Quantity = Input.Quantity! });
        else
        {
            recipe = db.Recipes.Include(r => r.Products).First(r => r.ID == Id);
            db.RecipeIngredients.RemoveRange(recipe.Products);
            recipe.Code = Input.Code!;
            recipe.Name = Input.Name!;
            recipe.Category = Input.Category!;
            recipe.Quantity = Input.Quantity!;
        }

        recipe.Price = Input.Price;
        recipe.Vat = Input.VatPercent / 100;
        recipe.QuantityMultiplier = Input.Multiplier;
        recipe.Products = Products.Select(p => new RecipeIngredient { ProductId = p.ProductId, Quantity = p.Quantity }).ToList();
        return recipe;
    }

    /// <summary>The product picked from the suggestions, or the one whose code or name was typed exactly.</summary>
    private Product? Resolve()
    {
        if (NewRow.ProductId > 0) return db.Products.Find(NewRow.ProductId);
        var text = NewRow.Search?.Trim();
        return string.IsNullOrEmpty(text) ? null : db.Products.FirstOrDefault(p => p.Code == text || p.Name == text);
    }

    /// <summary>Product details and the latest purchase price (per unit) of each ingredient come from the database.</summary>
    private void Prepare()
    {
        var ids = Products.Select(p => p.ProductId).Distinct().ToList();
        var products = db.Products.AsNoTracking().Where(p => ids.Contains(p.ID)).ToDictionary(p => p.ID);
        var prices = reports.LatestPurchasePrices(ids);

        Products = Products.Where(p => products.ContainsKey(p.ProductId)).ToList();
        Details = products.ToDictionary(p => p.Key, p => new ProductInfo(p.Value.Code, p.Value.Name, p.Value.UnitOfMeasure, prices.GetValueOrDefault(p.Key, p.Value.PurchasePrice)));
        LastRecipeCode = db.Recipes.OrderByDescending(r => r.Code).Select(r => r.Code).FirstOrDefault();
    }
}

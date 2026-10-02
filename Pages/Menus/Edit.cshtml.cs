using Microsoft.Extensions.Options;

namespace Recipes.Pages.Menus;

public class EditModel(RecipesContext db, IOptions<CompanyOptions> options) : PageBase
{
    [FromRoute] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; } // the validity proposed for a new menu
    [BindProperty] public MenuInput Input { get; set; } = new();
    [BindProperty] public List<MenuRecipeInput> Recipes { get; set; } = [];
    [BindProperty] public NewRecipeRow NewRow { get; set; } = new();

    public Dictionary<int, Recipe> Details { get; private set; } = [];
    public string? LastMenuCode { get; private set; }

    public class MenuInput
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public decimal VatPercent { get; set; }
        public DateOnly? ValidOn { get; set; }
    }

    public class MenuRecipeInput
    {
        public int RecipeId { get; set; }
        public decimal Portions { get; set; }
    }

    public class NewRecipeRow
    {
        public string? Search { get; set; }
        public int RecipeId { get; set; }
        public decimal Portions { get; set; } = 1;
    }

    public IActionResult OnGet()
    {
        if (Id is null)
        {
            Input.ValidOn = Date;
            Input.VatPercent = options.Value.StandardVat * 100;
        }
        else
        {
            var menu = db.Menus.AsNoTracking().Include(m => m.Recipes).FirstOrDefault(m => m.ID == Id);
            if (menu is null) return RedirectToPage("Index");

            Input = new MenuInput { Code = menu.Code, Name = menu.Name, Price = menu.Price, VatPercent = menu.Vat * 100, ValidOn = menu.ValidOn };
            Recipes = menu.Recipes.Select(r => new MenuRecipeInput { RecipeId = r.RecipeId, Portions = r.Portions }).ToList();
        }

        Prepare();
        return Page();
    }

    public IActionResult OnPostAdd()
    {
        var recipe = Resolve();
        if (!string.IsNullOrWhiteSpace(NewRow.Search))
        {
            ModelState.ErrorIf(recipe is null, "NewRow.Search", "Recipe not found. Pick it from the list or type its exact code or name.");
            ModelState.ErrorIf(NewRow.Portions <= 0, "NewRow.Portions", "Portions must be greater than zero");
        }

        if (ModelState.IsValid && recipe is not null)
        {
            Recipes.Add(new MenuRecipeInput { RecipeId = recipe.ID, Portions = NewRow.Portions });
            NewRow = new();
            ModelState.Clear();
        }

        Prepare();
        return Page();
    }

    public IActionResult OnPostRemove(int index)
    {
        if (index >= 0 && index < Recipes.Count) Recipes.RemoveAt(index);
        ModelState.Clear();
        Prepare();
        return Page();
    }

    public IActionResult OnPost()
    {
        ModelState.Required(Input.Code, "Input.Code", "Menu code is required");
        ModelState.Required(Input.Name, "Input.Name", "Menu name is required");
        ModelState.ErrorIf(Input.VatPercent is < 0 or > 100, "Input.VatPercent", "VAT rate is invalid");
        ModelState.ErrorIf(!string.IsNullOrWhiteSpace(NewRow.Search), "NewRow.Search", "The recipe in the add row has not been added (press +).");
        ModelState.ErrorIf(!string.IsNullOrWhiteSpace(Input.Code) && db.Menus.Any(m => m.ID != Id && m.Code == Input.Code), "Input.Code", "Another menu has the same code");
        for (int i = 0; i < Recipes.Count; i++)
        {
            ModelState.ErrorIf(Recipes[i].Portions <= 0, $"Recipes[{i}].Portions", "Portions must be greater than zero");
            ModelState.ErrorIf(!db.Recipes.Any(r => r.ID == Recipes[i].RecipeId), $"Recipes[{i}].RecipeId", "The recipe no longer exists");
        }

        if (!ModelState.IsValid)
        {
            Prepare();
            return Page();
        }

        Menu menu;
        if (Id is null) db.Menus.Add(menu = new Menu { Code = Input.Code!, Name = Input.Name! });
        else
        {
            menu = db.Menus.Include(m => m.Recipes).First(m => m.ID == Id);
            db.MenuRecipes.RemoveRange(menu.Recipes);
            menu.Code = Input.Code!;
            menu.Name = Input.Name!;
        }

        menu.Price = Input.Price;
        menu.Vat = Input.VatPercent / 100;
        menu.ValidOn = Input.ValidOn;
        menu.Recipes = Recipes.Select(r => new MenuRecipe { RecipeId = r.RecipeId, Portions = r.Portions }).ToList();
        db.SaveChanges();

        Message($"Menu {menu.Name} was saved.");
        return RedirectToPage("Index", new { date = Input.ValidOn?.ToString("yyyy-MM-dd") });
    }

    /// <summary>The copy takes over the code, the original gets an archive code.</summary>
    public IActionResult OnPostClone()
    {
        var selection = db.Menus.Include(m => m.Recipes).FirstOrDefault(m => m.ID == Id);
        if (selection is null) return RedirectToPage("Index");

        var clone = new Menu
        {
            Code = selection.Code, Name = selection.Name, Price = selection.Price, Vat = selection.Vat, ValidOn = selection.ValidOn,
            Recipes = selection.Recipes.Select(r => new MenuRecipe { RecipeId = r.RecipeId, Portions = r.Portions }).ToList()
        };
        selection.Code = $"{selection.Code} ({selection.ID}!)";
        db.Menus.Add(clone);
        db.SaveChanges();

        Message("The menu was cloned.");
        return RedirectToPage("Index");
    }

    private Recipe? Resolve()
    {
        if (NewRow.RecipeId > 0) return db.Recipes.Find(NewRow.RecipeId);
        var text = NewRow.Search?.Trim();
        return string.IsNullOrEmpty(text) ? null : db.Recipes.FirstOrDefault(r => r.Code == text || r.Name == text);
    }

    private void Prepare()
    {
        var ids = Recipes.Select(r => r.RecipeId).Distinct().ToList();
        Details = db.Recipes.AsNoTracking().Where(r => ids.Contains(r.ID)).ToDictionary(r => r.ID);
        Recipes = Recipes.Where(r => Details.ContainsKey(r.RecipeId)).ToList();
        LastMenuCode = db.Menus.OrderByDescending(m => m.Code).Select(m => m.Code).FirstOrDefault();
    }
}

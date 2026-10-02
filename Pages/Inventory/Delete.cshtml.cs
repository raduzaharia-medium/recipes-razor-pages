namespace Recipes.Pages.Inventory;

public class DeleteModel(RecipesContext db) : PageBase
{
    [FromRoute] public int Id { get; set; }
    [BindProperty] public string? Replacement { get; set; }
    [BindProperty] public int ReplacementId { get; set; }

    public Product Product { get; private set; } = null!; // set by Load() before the page renders
    public List<RecipeIngredient> Recipes { get; private set; } = [];
    public bool HasGoodsReceipts { get; private set; }
    public bool HasSales { get; private set; }

    public IActionResult OnGet() => Load() ? Page() : RedirectToPage("Index");

    public IActionResult OnPost()
    {
        if (!Load()) return RedirectToPage("Index");

        // the replacement is the chosen suggestion, or whatever product the typed code/name matches exactly
        Product? replacement = null;
        if (ReplacementId > 0) replacement = db.Products.Find(ReplacementId);
        else if (!string.IsNullOrWhiteSpace(Replacement))
        {
            var text = Replacement.Trim();
            replacement = db.Products.FirstOrDefault(p => p.Code == text || p.Name == text);
        }

        if (!string.IsNullOrWhiteSpace(Replacement) && (replacement is null || replacement.ID == Id)) ModelState.AddModelError(nameof(Replacement), "The replacement product was not selected correctly");
        else if (replacement is null && (HasGoodsReceipts || HasSales)) ModelState.AddModelError(nameof(Replacement), "This product has goods receipts or sales, so it can only be replaced.");
        if (!ModelState.IsValid) return Page();

        using var transaction = db.Database.BeginTransaction();
        if (replacement is not null)
        {
            int newRow = replacement.ID;
            db.RecipeIngredients.Where(e => e.ProductId == Id).ExecuteUpdate(set => set.SetProperty(e => e.ProductId, newRow));
            db.GoodsReceiptLines.Where(e => e.ProductId == Id).ExecuteUpdate(set => set.SetProperty(e => e.ProductId, newRow));
            db.SaleLines.Where(e => e.ProductId == Id).ExecuteUpdate(set => set.SetProperty(e => e.ProductId, newRow));
        }
        db.Stock.Where(e => e.ProductId == Id).ExecuteDelete();
        db.Products.Where(e => e.ID == Id).ExecuteDelete();
        transaction.Commit();

        Message($"Product {Product.Name} was deleted.");
        return RedirectToPage("Index");
    }

    private bool Load()
    {
        var product = db.Products.AsNoTracking().FirstOrDefault(p => p.ID == Id);
        if (product is null) return false;

        Product = product;
        Recipes = db.RecipeIngredients.AsNoTracking().Include(e => e.Recipe).Where(e => e.ProductId == Id).ToList();
        HasGoodsReceipts = db.GoodsReceiptLines.Any(e => e.ProductId == Id);
        HasSales = db.SaleLines.Any(e => e.ProductId == Id)
            || db.SaleLines.Any(e => e.Recipe != null && e.Recipe.Products.Any(f => f.ProductId == Id));
        return true;
    }
}

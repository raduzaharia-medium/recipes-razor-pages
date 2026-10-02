namespace Recipes.Pages.Recipes;

public class IndexModel(RecipesContext db) : PageBase
{
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty] public List<RecipePrice> Prices { get; set; } = [];

    public List<Recipe> Recipes { get; private set; } = [];
    public List<string> Categories { get; private set; } = [];

    public class RecipePrice
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
    }

    public void OnGet() => Load();

    public IActionResult OnPostSavePrices()
    {
        var ids = Prices.Select(p => p.Id).ToList();
        var recipes = db.Recipes.Where(r => ids.Contains(r.ID)).ToDictionary(r => r.ID);
        int changed = 0;
        foreach (var price in Prices)
        {
            if (recipes.TryGetValue(price.Id, out var recipe) && recipe.Price != price.Price) { recipe.Price = price.Price; changed++; }
        }

        db.SaveChanges();
        Message(changed == 0 ? "No prices were changed." : $"Prices saved: {changed}.");
        return RedirectToPage(new { category = Category, q = Q });
    }

    public IActionResult OnPostDelete(int id)
    {
        var recipe = db.Recipes.Include(r => r.Products).FirstOrDefault(r => r.ID == id);
        if (recipe is null) return RedirectToPage(new { category = Category, q = Q });

        if (db.MenuRecipes.Any(m => m.RecipeId == id)) Error("The recipe is used in a menu and cannot be deleted.");
        else if (db.SaleLines.Any(e => e.RecipeId == id)) Error("The recipe appears in sales and cannot be deleted.");
        else
        {
            db.Recipes.Remove(recipe);
            db.SaveChanges();
            Message($"Recipe {recipe.Name} was deleted.");
        }

        return RedirectToPage(new { category = Category, q = Q });
    }

    private void Load()
    {
        Categories = db.Recipes.Select(r => r.Category).Distinct().OrderBy(c => c).ToList();
        Recipes = db.Recipes.AsNoTracking()
            .Where(r => string.IsNullOrEmpty(Category) || r.Category == Category)
            .Where(r => string.IsNullOrWhiteSpace(Q) || EF.Functions.Like(r.Code + " " + r.Name, Q.ContainsPattern(), "\\"))
            .OrderBy(r => r.Name)
            .ToList();
    }
}

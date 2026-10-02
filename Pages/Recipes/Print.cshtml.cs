namespace Recipes.Pages.Recipes;

public class PrintModel(RecipesContext db) : PageModel
{
    public Recipe Recipe { get; private set; } = null!; // set by OnGet before the page renders

    public IActionResult OnGet(int id)
    {
        var recipe = db.Recipes.AsNoTracking().WithIngredients().FirstOrDefault(r => r.ID == id);
        if (recipe is null) return NotFound();

        Recipe = recipe;
        return Page();
    }
}

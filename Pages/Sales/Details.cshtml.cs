namespace Recipes.Pages.Sales;

public class DetailsModel(RecipesContext db) : PageModel
{
    public Sale Sale { get; private set; } = new();

    public IActionResult OnGet(int id)
    {
        var sale = db.Sales.AsNoTracking()
            .Include(v => v.Vouchers)
            .WithNamedItems()
            .FirstOrDefault(v => v.ID == id);
        if (sale is null) return RedirectToPage("Index");

        Sale = sale;
        return Page();
    }
}

namespace Recipes.Pages.Sales;

public class IndexModel(RecipesContext db, TimeProvider time) : PageBase
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public List<Sale> Sales { get; private set; } = [];
    public decimal Total => Sales.Sum(v => v.Value);
    public decimal RegisterTotal => Sales.Where(v => SaleType.IsRegisterSale(v.Type)).Sum(v => v.Value);

    public void OnGet()
    {
        Sales = db.Sales.AsNoTracking().Include(v => v.SaleLines).Where(v => v.Date == Day).OrderBy(v => v.ID).ToList();
    }

    public IActionResult OnPostDelete(int id)
    {
        var sale = db.Sales.FirstOrDefault(v => v.ID == id);
        if (sale is not null)
        {
            db.Sales.Remove(sale); // lines and vouchers are deleted in cascade
            db.SaveChanges();
            Message("The sale was deleted.");
        }

        return RedirectToPage(new { date = Date?.ToString("yyyy-MM-dd") });
    }
}

namespace Recipes.Pages.Inventory;

public class PrintModel(ReportService reports, TimeProvider time) : PageModel
{
    public List<StockLine> Stock { get; private set; } = [];

    public void OnGet() => Stock = reports.CalculateStock(time.Today);
}

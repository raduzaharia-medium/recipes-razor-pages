namespace Recipes.Pages.Inventory;

public class IndexModel(ReportService reports, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public List<StockLine> Stock { get; private set; } = [];
    public string Title => Day == time.Today ? "Current theoretical stock" : $"Stock on {Day:dd/MM/yyyy}";

    public void OnGet()
    {
        Stock = reports.CalculateStock(Day)
            .Where(item => string.IsNullOrWhiteSpace(Q) || $"{item.Code} {item.Name}".Contains(Q.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

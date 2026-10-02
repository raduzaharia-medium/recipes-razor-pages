using Microsoft.Extensions.Options;

namespace Recipes.Pages.Sales;

public class SummaryModel(ReportService reports, IOptions<CompanyOptions> options, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public bool Print { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public string Storekeeper => options.Value.Company.Storekeeper;
    public List<SummaryLine> Items { get; private set; } = [];

    public void OnGet() => Items = reports.SalesSummary(Day);
}

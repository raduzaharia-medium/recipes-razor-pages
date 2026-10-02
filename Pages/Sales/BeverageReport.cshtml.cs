using Microsoft.Extensions.Options;

namespace Recipes.Pages.Sales;

public class BeverageReportModel(ReportService reports, IOptions<CompanyOptions> options, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public bool Print { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public string Storekeeper => options.Value.Company.Storekeeper;
    public List<BalantaLinie> Items { get; private set; } = [];
    public List<StockLine> RawMaterials { get; private set; } = [];
    public List<StockLine> Produced { get; private set; } = [];

    public void OnGet()
    {
        Items = ReportService.StockBalance(
            reports.CalculateStock(Day.AddDays(-1)), reports.CalculateStock(Day), reports.ConsumptionByProduct(Day, string.Empty), reports.ReceivedQuantities(Day))
            .Where(e => e.Category == "Beverages").ToList();

        if (Print)
        {
            RawMaterials = reports.ConsumptionByProduct(Day, null);
            Produced = reports.RecipesSold(Day);
        }
    }
}

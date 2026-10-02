using Microsoft.Extensions.Options;

namespace Recipes.Pages.Sales;

public class ProductionModel(ReportService reports, IOptions<CompanyOptions> options, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public bool Print { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public string Storekeeper => options.Value.Company.Storekeeper;
    public List<StockLine> RawMaterials { get; private set; } = [];
    public List<StockLine> Produced { get; private set; } = [];
    public List<string> Categories { get; private set; } = [];

    public void OnGet()
    {
        RawMaterials = reports.ConsumptionByProduct(Day, Category);
        Produced = reports.ItemsSold(Day);
        Categories = reports.ConsumptionByProduct(Day, string.Empty).Select(e => e.Category).Distinct().Order().ToList();
    }
}

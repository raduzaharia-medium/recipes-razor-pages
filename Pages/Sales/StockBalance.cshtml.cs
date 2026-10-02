using Microsoft.Extensions.Options;

namespace Recipes.Pages.Sales;

/// <summary>Stock movements for one day (?date=) or for a whole month (?month=yyyy-MM, the current month by default).</summary>
public class BalantaModel(ReportService reports, IOptions<CompanyOptions> options, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public bool Print { get; set; }

    public bool PeZi => Date is not null;
    public DateOnly Day => Date ?? time.Today;
    public DateOnly Month { get; private set; }
    public string Storekeeper => options.Value.Company.Storekeeper;
    public string Title => PeZi ? $"{Day:dd/MM/yyyy}" : $"{Month:MM/yyyy}";
    public List<BalantaLinie> Items { get; private set; } = [];

    public void OnGet([FromQuery(Name = "month")] string? month)
    {
        Month = DateOnly.TryParseExact(month, "yyyy-MM", out var requested) ? requested : new DateOnly(time.Today.Year, time.Today.Month, 1);

        if (PeZi)
        {
            Items = ReportService.StockBalance(
                reports.CalculateStock(Day.AddDays(-1)), reports.CalculateStock(Day), reports.ConsumptionByProduct(Day, string.Empty), reports.ReceivedQuantities(Day));
        }
        else
        {
            var prima = Month;
            Items = ReportService.StockBalance(
                reports.CalculateStock(prima), reports.CalculateStock(prima.AddMonths(1).AddDays(-1)),
                reports.MonthlyConsumption(Month.Month, Month.Year, string.Empty), reports.MonthlyReceivedQuantities(Month.Month, Month.Year));
        }
    }
}

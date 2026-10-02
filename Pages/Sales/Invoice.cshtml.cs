using Microsoft.Extensions.Options;

namespace Recipes.Pages.Sales;

public record InvoiceLine(string Name, decimal Price, decimal Portions, decimal Vat)
{
    public decimal Net => Price / (1 + Vat);
    public decimal NetValue => Net * Portions;
    public decimal VatValue => NetValue * Vat;
}

public class InvoiceModel(RecipesContext db, IOptions<CompanyOptions> options) : PageModel
{
    public CompanyOptions Options => options.Value;
    public Sale Sale { get; private set; } = new();

    /// <summary>The same item sold on several lines appears once, with the summed portions.</summary>
    public List<InvoiceLine> Items { get; private set; } = [];

    [BindProperty(SupportsGet = true, Name = "products")] public bool PrintLineItems { get; set; } = true;

    public IActionResult OnGet(int id)
    {
        var sale = db.Sales.AsNoTracking()
            .WithNamedItems()
            .FirstOrDefault(v => v.ID == id);
        if (sale is null) return NotFound();

        Sale = sale;
        Items = sale.SaleLines
            .GroupBy(e => (Type: e.MenuId is not null ? 'M' : e.RecipeId is not null ? 'R' : 'P', Id: e.MenuId ?? e.RecipeId ?? e.ProductId, e.Price, e.Vat, Name: e.DisplayName ?? ""))
            .Select(g => new InvoiceLine(g.Key.Name, g.Key.Price, g.Sum(e => e.Portions), g.Key.Vat))
            .ToList();
        return Page();
    }
}

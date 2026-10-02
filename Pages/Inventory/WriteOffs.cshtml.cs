using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Recipes.Pages.Inventory;

public record WrittenOffProduct(string Code, string Name, decimal Quantity);

/// <summary>The list of written-off products left by the stock-take, printed once.</summary>
public class WriteOffsModel(IOptions<CompanyOptions> options) : PageModel
{
    public List<WrittenOffProduct> WriteOffs { get; private set; } = [];
    public string Storekeeper => options.Value.Company.Storekeeper;

    public IActionResult OnGet()
    {
        if (TempData["WriteOffs"] is not string json) return RedirectToPage("Index");

        WriteOffs = JsonSerializer.Deserialize(json, WriteOffsJson.Default.ListWrittenOffProduct) ?? [];
        return Page();
    }
}

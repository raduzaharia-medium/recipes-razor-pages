using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Recipes.Pages.Inventory;

/// <summary>The quantities counted today; unknown products can be added on the spot.</summary>
public class StockCountModel(RecipesContext db, ReportService reports, TimeProvider time, IOptions<CompanyOptions> options) : PageBase
{
    [BindProperty] public List<StockCountLine> Lines { get; set; } = [];
    [BindProperty] public List<NewStockProduct> NewProducts { get; set; } = [];
    [BindProperty] public NewStockProduct NewRow { get; set; } = new();

    public Dictionary<int, StockLine> Details { get; private set; } = [];
    public string? LastProductCode { get; private set; }

    public class StockCountLine
    {
        public int Id { get; set; }
        public decimal Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal Price { get; set; }
    }

    public class NewStockProduct
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? UnitOfMeasure { get; set; }
        public decimal Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal Price { get; set; }
    }

    public void OnGet()
    {
        var stock = reports.CalculateStock(time.Today);
        Details = stock.ToDictionary(item => item.Id);
        Lines = stock.Select(item => new StockCountLine { Id = item.Id, Quantity = item.Quantity, PurchasePrice = item.PurchasePrice, Price = item.Price }).ToList();
        LastProductCode = Last();
    }

    public IActionResult OnPostAdd()
    {
        Rebuild();
        if (string.IsNullOrWhiteSpace(NewRow.Name) && string.IsNullOrWhiteSpace(NewRow.Code)) return Page();

        ModelState.Required(NewRow.Name, "NewRow.Name", "Product name is required");
        ModelState.Required(NewRow.Code, "NewRow.Code", "Code is required");
        ModelState.Required(NewRow.Category, "NewRow.Category", "Category is required");
        ModelState.Required(NewRow.UnitOfMeasure, "NewRow.UnitOfMeasure", "Unit of measure is required");
        ModelState.ErrorIf(Existent(NewRow.Code, NewRow.Name, out var reason), reason, reason.EndsWith("Code") ? "Another product has this code" : "Another product has this name");
        if (!ModelState.IsValid) return Page();

        NewProducts.Add(NewRow);
        NewRow = new();
        ModelState.Clear();
        return Page();
    }

    public IActionResult OnPostRemove(int index)
    {
        Rebuild();
        if (index >= 0 && index < NewProducts.Count) NewProducts.RemoveAt(index);
        ModelState.Clear();
        return Page();
    }

    public IActionResult OnPost()
    {
        Rebuild();
        if (!string.IsNullOrWhiteSpace(NewRow.Name))
            ModelState.AddModelError("NewRow.Name", "The new product row is filled in but has not been added (press +).");

        for (int i = 0; i < NewProducts.Count; i++)
        {
            var p = NewProducts[i];
            if (NewProducts.Take(i).Any(other => string.Equals(other.Code, p.Code, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError($"NewProducts[{i}].Code", $"Code {p.Code} appears twice");
            else if (db.Products.Any(x => x.Code == p.Code) || db.Products.Any(x => x.Name == p.Name))
                ModelState.AddModelError($"NewProducts[{i}].Code", $"Product {p.Name} already exists");
        }
        if (!ModelState.IsValid) return Page();

        var today = time.Today;
        var products = db.Products.ToDictionary(p => p.ID);
        var todayStock = db.Stock.Where(s => s.Date == today).ToDictionary(s => s.ProductId);
        var writeOffs = new List<WrittenOffProduct>();

        foreach (var linie in Lines)
        {
            if (!products.TryGetValue(linie.Id, out var product)) continue;

            // the difference from the theoretical stock is written off
            if (Details.TryGetValue(linie.Id, out var theoretical) && theoretical.TheoreticalQuantity > linie.Quantity)
                writeOffs.Add(new(product.Code, product.Name, theoretical.TheoreticalQuantity - linie.Quantity));

            if (todayStock.TryGetValue(linie.Id, out var todayRow)) todayRow.Quantity = linie.Quantity;
            else db.Stock.Add(new StockEntry { ProductId = linie.Id, Quantity = linie.Quantity, Date = today });

            product.Price = linie.Price;
            product.PurchasePrice = linie.PurchasePrice;
        }

        foreach (var p in NewProducts)
        {
            var product = new Product { Code = p.Code!, Name = p.Name!, Category = p.Category!, UnitOfMeasure = p.UnitOfMeasure!, Price = p.Price, PurchasePrice = p.PurchasePrice, Vat = options.Value.StandardVat };
            db.Stock.Add(new StockEntry { Product = product, Quantity = p.Quantity, Date = today });
        }

        db.SaveChanges();
        Message("Stock count saved.");

        if (writeOffs.Count == 0) return RedirectToPage("Index");
        TempData["WriteOffs"] = JsonSerializer.Serialize(writeOffs, WriteOffsJson.Default.ListWrittenOffProduct);
        return RedirectToPage("WriteOffs");
    }

    /// <summary>Product names and theoretical quantities are shown from the database, not posted back.</summary>
    private void Rebuild()
    {
        Details = reports.CalculateStock(time.Today).ToDictionary(item => item.Id);
        Lines = Lines.Where(linie => Details.ContainsKey(linie.Id)).ToList();
        LastProductCode = Last();
    }

    private bool Existent(string? code, string? name, out string field)
    {
        field = "";
        if (db.Products.Any(p => p.Code == code) || NewProducts.Any(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))) { field = "NewRow.Code"; return true; }
        if (db.Products.Any(p => p.Name == name) || NewProducts.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) { field = "NewRow.Name"; return true; }
        return false;
    }

    private string? Last() => db.Products.OrderByDescending(p => p.Code).Select(p => p.Code).FirstOrDefault();
}

namespace Recipes.Pages;

/// <summary>JSON lookups for the autocomplete fields (see wwwroot/js/site.js).</summary>
public class LookupModel(RecipesContext db) : PageModel
{
    private const int Limit = 8;
    private static bool IsMatch(string? value, string? q) => EF.Functions.Like(value, q.ContainsPattern(), "\\");

    public IActionResult OnGetProducts(string? q) => new JsonResult(db.Products.AsNoTracking()
        .Where(p => EF.Functions.Like(p.Code + " " + p.Name, q.ContainsPattern(), "\\"))
        .OrderBy(p => p.Code).Take(Limit).ToList()
        .Select(p => new { p.ID, Label = p.Name, Detail = $"{p.Code} · {p.UnitOfMeasure}", p.Code, p.Name, p.UnitOfMeasure }));

    public IActionResult OnGetRecipes(string? q) => new JsonResult(db.Recipes.AsNoTracking()
        .Where(r => EF.Functions.Like(r.Code + " " + r.Name, q.ContainsPattern(), "\\") && !(r.Code + r.Name).Contains("!)"))
        .OrderBy(r => r.Name).Take(Limit).ToList()
        .Select(r => new { r.ID, Label = r.Name, Detail = $"{r.Code} · {r.Price:0.00}", r.Code, r.Name }));

    /// <summary>Menus, recipes and priced products that can be sold.</summary>
    public IActionResult OnGetItems(string? q)
    {
        var model = q.ContainsPattern();
        var menus = db.Menus.AsNoTracking()
            .Where(m => EF.Functions.Like(m.Code + " " + m.Name, model, "\\") && !(m.Code + m.Name).Contains("!)"))
            .OrderBy(m => m.Name).Take(Limit).ToList()
            .Select(m => new { ID = m.ID, Type = ItemType.Menu, Label = m.Name, Detail = $"{m.Code} · {ItemType.Menu} · {m.Price:0.00}", m.Code, Name = m.Name, m.Price });
        var recipes = db.Recipes.AsNoTracking()
            .Where(r => EF.Functions.Like(r.Code + " " + r.Name, model, "\\") && !(r.Code + r.Name).Contains("!)"))
            .OrderBy(r => r.Name).Take(Limit).ToList()
            .Select(r => new { ID = r.ID, Type = ItemType.Recipe, Label = r.Name, Detail = $"{r.Code} · {ItemType.Recipe} · {r.Price:0.00}", r.Code, Name = r.Name, r.Price });
        var products = db.Products.AsNoTracking()
            .Where(p => p.Price > 0 && EF.Functions.Like(p.Code + " " + p.Name, model, "\\"))
            .OrderBy(p => p.Name).Take(Limit).ToList()
            .Select(p => new { ID = p.ID, Type = ItemType.Product, Label = p.Name, Detail = $"{p.Code} · {ItemType.Product} · {p.Price:0.00}", p.Code, Name = p.Name, p.Price });

        return new JsonResult(menus.Concat(recipes).Concat(products).OrderBy(e => e.Label).Take(Limit));
    }

    public IActionResult OnGetCustomers(string? q) => new JsonResult(db.Sales.AsNoTracking()
        .Where(v => v.CustomerName != null && EF.Functions.Like(v.CustomerName, q.ContainsPattern(), "\\"))
        .Select(v => new { v.CustomerName, v.CustomerAddress, v.CustomerCounty, v.CustomerTaxId, v.CustomerTradeRegisterNo, v.CustomerAccount, v.CustomerBank })
        .Distinct().OrderBy(v => v.CustomerName).Take(Limit).ToList()
        .Select(v => new { Label = v.CustomerName, Address = v.CustomerAddress, County = v.CustomerCounty, TaxId = v.CustomerTaxId, TradeRegisterNo = v.CustomerTradeRegisterNo, Account = v.CustomerAccount, Bank = v.CustomerBank }));

    public IActionResult OnGetSuppliers(string? q) => new JsonResult(db.GoodsReceipts.AsNoTracking()
        .Where(r => r.SupplierName != null && EF.Functions.Like(r.SupplierName, q.ContainsPattern(), "\\"))
        .Select(r => new { r.SupplierName, r.SupplierAddress, r.SupplierCounty, r.SupplierTaxId, r.SupplierTradeRegisterNo, r.SupplierAccount, r.SupplierBank })
        .Distinct().OrderBy(r => r.SupplierName).Take(Limit).ToList()
        .Select(r => new { Label = r.SupplierName, Address = r.SupplierAddress, County = r.SupplierCounty, TaxId = r.SupplierTaxId, TradeRegisterNo = r.SupplierTradeRegisterNo, Account = r.SupplierAccount, Bank = r.SupplierBank }));

    public IActionResult OnGetCategories(string? q) => new JsonResult(db.Products
        .Where(p => EF.Functions.Like(p.Category, q.ContainsPattern(), "\\"))
        .Select(p => p.Category).Distinct().OrderBy(c => c).Take(Limit).ToList()
        .Select(c => new { Label = c }));
}

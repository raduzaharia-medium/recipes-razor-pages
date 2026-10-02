using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace Recipes.Pages.GoodsReceipts;

public class EditModel(RecipesContext db, TimeProvider time, IOptions<CompanyOptions> options) : PageBase
{
    private static readonly string[] Types = ["Invoice", "Delivery note"];

    [FromRoute] public int? Id { get; set; }
    [BindProperty] public GoodsReceiptInput Input { get; set; } = new();
    [BindProperty] public List<LinieInput> Lines { get; set; } = [];
    [BindProperty] public NewLineRow NewRow { get; set; } = new();
    [BindProperty] public ProductInput NewProduct { get; set; } = new();

    public Dictionary<int, ProductInfo> Details { get; private set; } = [];
    /// <summary>VAT rates (in percent) already used, offered while typing a line.</summary>
    public List<decimal> VatRatesUsed { get; private set; } = [];
    public string? LastProductCode { get; private set; }
    public bool ShowNewProduct { get; private set; }
    public SelectList DocumentTypes => new(Types);

    public record ProductInfo(string Code, string Name, string UnitOfMeasure, string Category);

    public class GoodsReceiptInput
    {
        public int Number { get; set; }
        public DateOnly Date { get; set; }
        public string? DeliveryDocumentType { get; set; } = Types[0];
        public string? DeliveryDocumentNumber { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierCounty { get; set; }
        public string? SupplierTaxId { get; set; }
        public string? SupplierTradeRegisterNo { get; set; }
        public string? SupplierAccount { get; set; }
        public string? SupplierBank { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalWithVat { get; set; }
    }

    public class LinieInput
    {
        public int ProductId { get; set; }
        public decimal PackSize { get; set; } = 1;
        public decimal Quantity { get; set; }
        public decimal VatPercent { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
    }

    public class NewLineRow : LinieInput
    {
        public string? Search { get; set; }
    }

    public class ProductInput
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? UnitOfMeasure { get; set; }
        public string? Category { get; set; }
    }

    // ---- computed values shown in the lines
    public decimal Value(int i) => Lines[i].Quantity * Lines[i].PurchasePrice;
    public decimal VatValue(int i) => Value(i) * Lines[i].VatPercent / 100;

    public IActionResult OnGet()
    {
        if (Id is null)
        {
            // numbering restarts every month
            var ultima = db.GoodsReceipts.OrderByDescending(r => r.Date).ThenByDescending(r => r.Number).FirstOrDefault();
            var today = time.Today;
            Input.Date = today;
            Input.Number = ultima is null ? 1 : ultima.Date.Month == today.Month && ultima.Date.Year == today.Year ? ultima.Number + 1 : 1;
            NewRow.VatPercent = LastVatPercent();
        }
        else
        {
            var r = db.GoodsReceipts.AsNoTracking().Include(x => x.Products).FirstOrDefault(x => x.ID == Id);
            if (r is null) return RedirectToPage("Index");

            Input = new GoodsReceiptInput
            {
                Number = r.Number, Date = r.Date, DeliveryDocumentType = r.DeliveryDocumentType, DeliveryDocumentNumber = r.DeliveryDocumentNumber,
                SupplierName = r.SupplierName, SupplierAddress = r.SupplierAddress, SupplierCounty = r.SupplierCounty, SupplierTaxId = r.SupplierTaxId,
                SupplierTradeRegisterNo = r.SupplierTradeRegisterNo, SupplierAccount = r.SupplierAccount, SupplierBank = r.SupplierBank,
                TotalValue = r.TotalValue, TotalVat = r.TotalVat, TotalWithVat = r.TotalWithVat
            };
            Lines = r.Products.Select(l => new LinieInput { ProductId = l.ProductId, PackSize = l.PackSize, Quantity = l.Quantity, VatPercent = l.Vat * 100, PurchasePrice = l.PurchasePrice, SellingPrice = l.SellingPrice }).ToList();
            NewRow.VatPercent = Lines.Count > 0 ? Lines[^1].VatPercent : LastVatPercent();
        }

        Prepare();
        return Page();
    }

    public IActionResult OnPostAdd()
    {
        var product = Resolve();
        bool filled = !string.IsNullOrWhiteSpace(NewRow.Search);

        if (filled && product is null)
        {
            // the product does not exist: offer to add it to the inventory
            ModelState.Clear();
            ModelState.AddModelError("NewRow.Search", "The product is not in the inventory (or you did not pick it from the list).");
            NewProduct = new ProductInput { Name = NewRow.Search!.Trim() };
            ShowNewProduct = true;
        }
        else if (filled)
        {
            ModelState.ErrorIf(NewRow.Quantity <= 0, "NewRow.Quantity", "Quantity must be greater than zero");
            ModelState.ErrorIf(NewRow.PackSize <= 0, "NewRow.PackSize", "Pack size must be greater than zero");
            ModelState.ErrorIf(NewRow.VatPercent is < 0 or > 100, "NewRow.VatPercent", "VAT rate is invalid");
        }

        if (filled && ModelState.IsValid && product is not null)
        {
            Lines.Add(new LinieInput { ProductId = product.ID, PackSize = NewRow.PackSize, Quantity = NewRow.Quantity, VatPercent = NewRow.VatPercent, PurchasePrice = NewRow.PurchasePrice, SellingPrice = NewRow.SellingPrice });
            NewRow = new() { VatPercent = NewRow.VatPercent };   // the next line most likely has the same rate
            ModelState.Clear();
            Prepare();
            RecalculateTotals();
            return Page();
        }

        Prepare();
        return Page();
    }

    public IActionResult OnPostCreateProduct()
    {
        ModelState.Required(NewProduct.Code, "NewProduct.Code", "Code is required");
        ModelState.Required(NewProduct.Name, "NewProduct.Name", "Product name is required");
        ModelState.Required(NewProduct.UnitOfMeasure, "NewProduct.UnitOfMeasure", "Unit of measure is required");
        ModelState.Required(NewProduct.Category, "NewProduct.Category", "Product category is required");
        if (ModelState.IsValid)
        {
            ModelState.ErrorIf(db.Products.Any(p => p.Code == NewProduct.Code), "NewProduct.Code", "Another product has this code");
            ModelState.ErrorIf(db.Products.Any(p => p.Name == NewProduct.Name), "NewProduct.Name", "Another product has this name");
        }

        if (!ModelState.IsValid)
        {
            ShowNewProduct = true;
            Prepare();
            return Page();
        }

        var product = new Product { Code = NewProduct.Code!, Name = NewProduct.Name!, UnitOfMeasure = NewProduct.UnitOfMeasure!, Category = NewProduct.Category!, Vat = NewRow.VatPercent / 100 };
        db.Products.Add(product);
        db.SaveChanges();

        // the new product is ready in the "add a line" row
        ModelState.Clear();
        NewRow.ProductId = product.ID;
        NewRow.Search = product.Name;
        NewProduct = new();
        Prepare();
        return Page();
    }

    public IActionResult OnPostRemove(int index)
    {
        if (index >= 0 && index < Lines.Count) Lines.RemoveAt(index);
        ModelState.Clear();
        Prepare();
        RecalculateTotals();
        return Page();
    }

    public IActionResult OnPostRecalculate()
    {
        ModelState.Clear();
        Prepare();
        RecalculateTotals();
        return Page();
    }

    public IActionResult OnPost()
    {
        ModelState.ErrorIf(Input.Number <= 0, "Input.Number", "Goods receipt number is required");
        ModelState.ErrorIf(Input.Date == default, "Input.Date", "Date is required");
        ModelState.ErrorIf(!string.IsNullOrWhiteSpace(NewRow.Search), "NewRow.Search", "The product in the add row has not been added (press +).");
        for (int i = 0; i < Lines.Count; i++)
        {
            ModelState.ErrorIf(Lines[i].Quantity <= 0, $"Lines[{i}].Quantity", "Quantity must be greater than zero");
            ModelState.ErrorIf(Lines[i].PackSize <= 0, $"Lines[{i}].PackSize", "Pack size must be greater than zero");
            ModelState.ErrorIf(Lines[i].VatPercent is < 0 or > 100, $"Lines[{i}].VatPercent", "VAT rate is invalid");
            ModelState.ErrorIf(!db.Products.Any(p => p.ID == Lines[i].ProductId), $"Lines[{i}].ProductId", "The product no longer exists");
        }

        if (!ModelState.IsValid)
        {
            Prepare();
            return Page();
        }

        GoodsReceipt goodsReceipt;
        if (Id is null) db.GoodsReceipts.Add(goodsReceipt = new GoodsReceipt());
        else
        {
            goodsReceipt = db.GoodsReceipts.Include(r => r.Products).First(r => r.ID == Id);
            db.GoodsReceiptLines.RemoveRange(goodsReceipt.Products);
        }

        goodsReceipt.Number = Input.Number;
        goodsReceipt.Date = Input.Date;
        goodsReceipt.DeliveryDocumentType = Input.DeliveryDocumentType;
        goodsReceipt.DeliveryDocumentNumber = Input.DeliveryDocumentNumber;
        goodsReceipt.SupplierName = Input.SupplierName;
        goodsReceipt.SupplierAddress = Input.SupplierAddress;
        goodsReceipt.SupplierCounty = Input.SupplierCounty;
        goodsReceipt.SupplierTaxId = Input.SupplierTaxId;
        goodsReceipt.SupplierTradeRegisterNo = Input.SupplierTradeRegisterNo;
        goodsReceipt.SupplierAccount = Input.SupplierAccount;
        goodsReceipt.SupplierBank = Input.SupplierBank;
        goodsReceipt.TotalValue = Input.TotalValue;
        goodsReceipt.TotalVat = Input.TotalVat;
        goodsReceipt.TotalWithVat = Input.TotalWithVat;
        goodsReceipt.Products = Lines.Select(l => new GoodsReceiptLine
        {
            ProductId = l.ProductId, PackSize = l.PackSize, Quantity = l.Quantity, Vat = l.VatPercent / 100, PurchasePrice = l.PurchasePrice, SellingPrice = l.SellingPrice
        }).ToList();
        // what is sold as bought (a drink, say) is sold with the VAT rate it was last received with
        foreach (var line in Lines)
            if (db.Products.Find(line.ProductId) is { } product) product.Vat = line.VatPercent / 100;
        db.SaveChanges();

        Message($"Goods receipt {goodsReceipt.Number} was saved.");
        return RedirectToPage("Index", new { date = Input.Date.ToString("yyyy-MM-dd") });
    }

    /// <summary>Totals follow the lines; they can be corrected by hand before saving.</summary>
    private void RecalculateTotals()
    {
        decimal value = 0, vat = 0;
        for (int i = 0; i < Lines.Count; i++) { value += Value(i); vat += VatValue(i); }

        Input.TotalValue = Math.Round(value, 2);
        Input.TotalVat = Math.Round(vat, 2);
        Input.TotalWithVat = Math.Round(value + vat, 2);
    }

    /// <summary>The rate of the most recent receipt line, or the standard rate when there is none yet.</summary>
    private decimal LastVatPercent() =>
        (db.GoodsReceiptLines.OrderByDescending(l => l.GoodsReceipt.Date).ThenByDescending(l => l.ID).Select(l => (decimal?)l.Vat).FirstOrDefault() ?? options.Value.StandardVat) * 100;

    /// <summary>The product picked from the suggestions, or the one whose code or name was typed exactly.</summary>
    private Product? Resolve()
    {
        if (NewRow.ProductId > 0) return db.Products.Find(NewRow.ProductId);
        var text = NewRow.Search?.Trim();
        return string.IsNullOrEmpty(text) ? null : db.Products.FirstOrDefault(p => p.Code == text || p.Name == text);
    }

    private void Prepare()
    {
        var ids = Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = db.Products.AsNoTracking().Where(p => ids.Contains(p.ID)).ToDictionary(p => p.ID);
        Lines = Lines.Where(l => products.ContainsKey(l.ProductId)).ToList();
        Details = products.ToDictionary(p => p.Key, p => new ProductInfo(p.Value.Code, p.Value.Name, p.Value.UnitOfMeasure, p.Value.Category));
        VatRatesUsed = db.GoodsReceiptLines.Select(l => l.Vat).ToList().Select(v => v * 100).Append(options.Value.StandardVat * 100).Distinct().Order().ToList();
        LastProductCode = db.Products.OrderByDescending(p => p.Code).Select(p => p.Code).FirstOrDefault();
    }
}

using Microsoft.AspNetCore.Mvc.Rendering;

namespace Recipes.Pages.Sales;

public class NewModel(RecipesContext db, CashRegister register, TimeProvider time) : PageBase
{
    [BindProperty] public SaleInput Input { get; set; } = new();
    [BindProperty] public List<ElementInput> Items { get; set; } = [];
    [BindProperty] public List<VoucherInput> Vouchers { get; set; } = [];
    [BindProperty] public NewItemRow NewRow { get; set; } = new();
    [BindProperty] public VoucherInput NewVoucher { get; set; } = new();

    public SelectList SaleTypes => new(SaleType.All);
    public decimal Value => Items.Sum(e => e.Price * e.Portions);
    public decimal VouchersValue => Vouchers.Sum(b => b.VoucherValue * b.Number);

    public class SaleInput
    {
        public string? Type { get; set; } = SaleType.Cash;
        public DateOnly Date { get; set; }
        public bool PrintFiscalReceipt { get; set; } = true;
        public bool PrintInvoice { get; set; }
        public bool PrintLineItems { get; set; } = true;
        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerCounty { get; set; }
        public string? CustomerTaxId { get; set; }
        public string? CustomerTradeRegisterNo { get; set; }
        public string? CustomerAccount { get; set; }
        public string? CustomerBank { get; set; }
        public string? InvoiceSeries { get; set; }
        public int InvoiceNumber { get; set; }
    }

    public class ElementInput
    {
        public string Type { get; set; } = "";
        public int ItemId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Portions { get; set; }
    }

    public class NewItemRow
    {
        public string? Search { get; set; }
        public string? Type { get; set; }
        public int ItemId { get; set; }
        public decimal Portions { get; set; } = 1;
    }

    public class VoucherInput
    {
        public decimal VoucherValue { get; set; }
        public int Number { get; set; }
    }

    public void OnGet()
    {
        Input.Date = time.Today;

        // the meal voucher values used so far, ready to fill in with the voucher count
        Vouchers = db.SaleVouchers.Select(b => b.VoucherValue).ToList().Distinct().Order().Select(value => new VoucherInput { VoucherValue = value }).ToList();
    }

    public IActionResult OnPostAdd()
    {
        if (string.IsNullOrWhiteSpace(NewRow.Search)) return Page();

        var item = Resolve();
        ModelState.ErrorIf(item is null, "NewRow.Search", "No menu, recipe or product has this name. Pick it from the list or type its exact code or name.");
        ModelState.ErrorIf(NewRow.Portions <= 0, "NewRow.Portions", "Portions must be greater than zero");
        if (!ModelState.IsValid) return Page();

        item!.Portions = NewRow.Portions;
        Items.Add(item);
        NewRow = new();
        ModelState.Clear();
        return Page();
    }

    public IActionResult OnPostRemove(int index)
    {
        if (index >= 0 && index < Items.Count) Items.RemoveAt(index);
        ModelState.Clear();
        return Page();
    }

    public IActionResult OnPostAddVoucher()
    {
        if (NewVoucher.Number > 0 && NewVoucher.VoucherValue > 0)
        {
            Vouchers.Add(NewVoucher);
            NewVoucher = new();
        }

        ModelState.Clear();
        return Page();
    }

    public IActionResult OnPostRefresh()
    {
        ModelState.Clear();
        return Page();
    }

    public IActionResult OnPost()
    {
        ModelState.ErrorIf(Items.Count == 0, "", "No products have been sold");
        ModelState.ErrorIf(Input.Date == default, "Input.Date", "Date is required");
        ModelState.ErrorIf(!string.IsNullOrWhiteSpace(NewRow.Search), "NewRow.Search", "The item in the add row has not been added (press +).");
        ModelState.ErrorIf(!string.IsNullOrEmpty(Input.Type) && !SaleType.All.Contains(Input.Type), "Input.Type", "Unknown sale type");

        for (int i = 0; i < Items.Count; i++)
        {
            var e = Items[i];
            ModelState.ErrorIf(e.Portions <= 0, $"Items[{i}].Portions", "Portions must be greater than zero");
            ModelState.ErrorIf(!ItemType.All.Contains(e.Type) || !Exists(e.Type, e.ItemId), $"Items[{i}].Name", $"{e.Name} no longer exists");
        }
        ModelState.ErrorIf(Vouchers.Any(b => b.Number < 0 || b.VoucherValue < 0), "", "Invalid meal voucher");
        if (!ModelState.IsValid) return Page();

        var vouchers = Vouchers.Where(b => b.Number > 0).ToList();
        var sale = new Sale
        {
            Date = Input.Date, Type = Input.Type, CustomerName = NullIfBlank(Input.CustomerName), CustomerAddress = NullIfBlank(Input.CustomerAddress), CustomerCounty = NullIfBlank(Input.CustomerCounty),
            CustomerTaxId = NullIfBlank(Input.CustomerTaxId), CustomerTradeRegisterNo = NullIfBlank(Input.CustomerTradeRegisterNo), CustomerAccount = NullIfBlank(Input.CustomerAccount), CustomerBank = NullIfBlank(Input.CustomerBank),
            InvoiceSeries = NullIfBlank(Input.InvoiceSeries), InvoiceNumber = Input.PrintInvoice && Input.InvoiceNumber > 0 ? Input.InvoiceNumber : null,
            Vouchers = vouchers.Select(b => new SaleVoucher { VoucherCount = b.Number, VoucherValue = b.VoucherValue }).ToList(),
            SaleLines = Items.Select(e => new SaleLine
            {
                Portions = e.Portions, Price = e.Price, Vat = Vat(e.Type, e.ItemId),
                MenuId = e.Type == ItemType.Menu ? e.ItemId : null,
                RecipeId = e.Type == ItemType.Recipe ? e.ItemId : null,
                ProductId = e.Type == ItemType.Product ? e.ItemId : null
            }).ToList()
        };
        db.Sales.Add(sale);
        db.SaveChanges();

        if (Input.PrintFiscalReceipt)
        {
            // the same goods sold several times appear once on the receipt
            var grouped = Items.GroupBy(e => (e.Type, e.ItemId, e.Price, e.Name))
                .Select(g => new ReceiptLine(g.Key.Name, g.Key.Price, g.Sum(e => e.Portions))).ToList();
            var receipt = register.PrintFiscalReceipt(Input.Type, grouped, vouchers.Select(b => new VoucherValue(b.VoucherValue, b.Number)).ToList());

            if (receipt.State == "error") Error(receipt.Message!);
            else Message(receipt.State == "printed" ? "Sale recorded. The fiscal receipt was sent to the cash register." : $"Sale recorded. {receipt.Message}.");
        }
        else Message("Sale recorded.");

        return Input.PrintInvoice
            ? RedirectToPage("Invoice", new { id = sale.ID, products = Input.PrintLineItems })
            : RedirectToPage("New");
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The VAT rate the item is sold with now, kept on the sale line.</summary>
    private decimal Vat(string type, int id) => type switch
    {
        ItemType.Menu => db.Menus.Where(m => m.ID == id).Select(m => m.Vat).FirstOrDefault(),
        ItemType.Recipe => db.Recipes.Where(r => r.ID == id).Select(r => r.Vat).FirstOrDefault(),
        _ => db.Products.Where(p => p.ID == id).Select(p => p.Vat).FirstOrDefault()
    };

    private bool Exists(string type, int id) => type switch
    {
        ItemType.Menu => db.Menus.Any(m => m.ID == id),
        ItemType.Recipe => db.Recipes.Any(r => r.ID == id),
        _ => db.Products.Any(p => p.ID == id)
    };

    /// <summary>The item picked from the suggestions, or the menu/recipe/product whose code or name was typed exactly.</summary>
    private ElementInput? Resolve()
    {
        string? type = NewRow.ItemId > 0 && ItemType.All.Contains(NewRow.Type ?? "") ? NewRow.Type : null;
        var text = NewRow.Search!.Trim();

        if (type is null)
        {
            if (db.Menus.Any(m => m.Code == text || m.Name == text)) type = ItemType.Menu;
            else if (db.Recipes.Any(r => r.Code == text || r.Name == text)) type = ItemType.Recipe;
            else if (db.Products.Any(p => p.Code == text || p.Name == text)) type = ItemType.Product;
            else return null;
        }

        int id = NewRow.ItemId;
        return type switch
        {
            ItemType.Menu => db.Menus.AsNoTracking().Where(m => id > 0 ? m.ID == id : m.Code == text || m.Name == text)
                .Select(m => new ElementInput { Type = ItemType.Menu, ItemId = m.ID, Code = m.Code, Name = m.Name, Price = m.Price }).FirstOrDefault(),
            ItemType.Recipe => db.Recipes.AsNoTracking().Where(r => id > 0 ? r.ID == id : r.Code == text || r.Name == text)
                .Select(r => new ElementInput { Type = ItemType.Recipe, ItemId = r.ID, Code = r.Code, Name = r.Name, Price = r.Price }).FirstOrDefault(),
            _ => db.Products.AsNoTracking().Where(p => id > 0 ? p.ID == id : p.Code == text || p.Name == text)
                .Select(p => new ElementInput { Type = ItemType.Product, ItemId = p.ID, Code = p.Code, Name = p.Name, Price = p.Price }).FirstOrDefault()
        };
    }
}

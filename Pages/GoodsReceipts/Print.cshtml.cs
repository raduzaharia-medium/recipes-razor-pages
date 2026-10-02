using Microsoft.Extensions.Options;

namespace Recipes.Pages.GoodsReceipts;

public class PrintModel(RecipesContext db, IOptions<CompanyOptions> options) : PageModel
{
    public GoodsReceipt GoodsReceipt { get; private set; } = new();
    public List<ChangedPrice> ChangedPrices { get; private set; } = [];
    public string Storekeeper => options.Value.Company.Storekeeper;

    public record ChangedPrice(Product Product, decimal OldPrice, decimal NewPrice);

    public IActionResult OnGet(int id)
    {
        var goodsReceipt = db.GoodsReceipts.AsNoTracking().WithLines().FirstOrDefault(r => r.ID == id);
        if (goodsReceipt is null) return NotFound();
        GoodsReceipt = goodsReceipt;
        foreach (var linie in goodsReceipt.Products) linie.GoodsReceipt = goodsReceipt;

        // the selling price against the previous goods receipt of the same product
        foreach (var linie in goodsReceipt.Products)
        {
            var previous = db.GoodsReceiptLines.AsNoTracking()
                .Where(l => l.ProductId == linie.ProductId && l.GoodsReceiptId != id && (l.GoodsReceipt.Date < goodsReceipt.Date || l.GoodsReceipt.Date == goodsReceipt.Date && l.GoodsReceiptId < id))
                .OrderByDescending(l => l.GoodsReceipt.Date).ThenByDescending(l => l.ID)
                .FirstOrDefault();

            if (previous is not null && previous.SellingPrice != linie.SellingPrice)
                ChangedPrices.Add(new(linie.Product, previous.SellingPrice, linie.SellingPrice));
        }

        return Page();
    }
}

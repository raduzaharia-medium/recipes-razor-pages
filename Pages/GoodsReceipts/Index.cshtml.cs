namespace Recipes.Pages.GoodsReceipts;

public class IndexModel(RecipesContext db, TimeProvider time) : PageBase
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public List<GoodsReceipt> GoodsReceipts { get; private set; } = [];

    public void OnGet()
    {
        GoodsReceipts = db.GoodsReceipts.AsNoTracking().Include(r => r.Products).Where(r => r.Date == Day).OrderByDescending(r => r.Number).ToList();
    }

    public IActionResult OnPostDelete(int id)
    {
        var goodsReceipt = db.GoodsReceipts.FirstOrDefault(r => r.ID == id);
        if (goodsReceipt is not null)
        {
            db.GoodsReceipts.Remove(goodsReceipt); // lines are deleted in cascade
            db.SaveChanges();
            Message($"Goods receipt {goodsReceipt.Number} was deleted.");
        }

        return RedirectToPage(new { date = Date?.ToString("yyyy-MM-dd") });
    }
}

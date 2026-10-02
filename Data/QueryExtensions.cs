namespace Recipes.Data;

/// <summary>Reusable eager-loading shapes for the queries.</summary>
public static class QueryExtensions
{
    extension(IQueryable<Recipe> recipes)
    {
        public IQueryable<Recipe> WithIngredients() => recipes.Include(r => r.Products).ThenInclude(p => p.Product);
    }

    extension(IQueryable<Menu> menus)
    {
        public IQueryable<Menu> WithRecipes() => menus.Include(m => m.Recipes).ThenInclude(r => r.Recipe);
    }

    extension(IQueryable<GoodsReceipt> goodsReceipts)
    {
        public IQueryable<GoodsReceipt> WithLines() => goodsReceipts.Include(r => r.Products).ThenInclude(l => l.Product);
    }

    extension(IQueryable<Sale> sales)
    {
        /// <summary>The sale with its lines, each with the menu, recipe or product it sells (for names and codes).</summary>
        public IQueryable<Sale> WithNamedItems() => sales
            .Include(v => v.SaleLines).ThenInclude(e => e.Menu)
            .Include(v => v.SaleLines).ThenInclude(e => e.Recipe)
            .Include(v => v.SaleLines).ThenInclude(e => e.Product);
    }

    extension(IQueryable<SaleLine> items)
    {
        /// <summary>Sale lines with everything needed to work out which products they consume (menus -> recipes -> ingredients).</summary>
        public IQueryable<SaleLine> WithConsumedProducts() => items
            .Include(e => e.Sale)
            .Include(e => e.Product)
            .Include(e => e.Recipe).ThenInclude(r => r!.Products).ThenInclude(i => i.Product)
            .Include(e => e.Menu).ThenInclude(m => m!.Recipes).ThenInclude(mr => mr.Recipe).ThenInclude(r => r.Products).ThenInclude(i => i.Product);
    }
}

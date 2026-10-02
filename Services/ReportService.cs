namespace Recipes.Services;

/// <summary>Stock and sales calculations behind the stock list, the stock-take and the reports.</summary>
public class ReportService(RecipesContext db)
{
    private record ConsumedProduct(Product Product, decimal Quantity);
    private record SoldLine(int Id, string Name, string Code, string Category, decimal Portions, decimal Price);

    /// <summary>Products consumed by a sale line: the product itself, the recipe ingredients or the ingredients of every recipe in the menu.</summary>
    private static IEnumerable<ConsumedProduct> Consumption(SaleLine item)
    {
        if (item.Product is not null)
        {
            yield return new(item.Product, item.Portions);
        }
        else if (item.Recipe is not null)
        {
            foreach (var ingredient in item.Recipe.Products)
                yield return new(ingredient.Product, item.Portions / item.Recipe.QuantityMultiplier * ingredient.Quantity);
        }
        else if (item.Menu is not null)
        {
            foreach (var menuRecipe in item.Menu.Recipes)
                foreach (var ingredient in menuRecipe.Recipe.Products)
                    yield return new(ingredient.Product, item.Portions * ingredient.Quantity * menuRecipe.Portions / menuRecipe.Recipe.QuantityMultiplier);
        }
    }

    private IQueryable<SaleLine> LinesWithProducts() => db.SaleLines.AsNoTracking().WithConsumedProducts();

    private StockEntry? LastStockCount(DateOnly date) => db.Stock
        .Where(item => item.Date <= date)
        .OrderByDescending(item => item.Date)
        .FirstOrDefault();

    /// <summary>Receipt lines between the last stock-take (exclusive) and <paramref name="date"/> (inclusive), newest first.</summary>
    private List<GoodsReceiptLine> GoodsReceiptLinesInRange(StockEntry? lastStockCount, DateOnly date)
    {
        DateOnly? by = lastStockCount?.Date;
        return db.GoodsReceiptLines
            .AsNoTracking()
            .Include(item => item.GoodsReceipt)
            .Where(item => item.GoodsReceipt.Date <= date && (by == null || item.GoodsReceipt.Date > by))
            .ToList()
            .OrderByDescending(item => item.GoodsReceipt.Date)
            .ThenByDescending(item => item.ID)
            .ToList();
    }

    /// <summary>Purchase price per unit (price / packSize) from the latest receipt of each product.</summary>
    public Dictionary<int, decimal> LatestPurchasePrices(IReadOnlyCollection<int> productIds) => db.GoodsReceiptLines
        .AsNoTracking()
        .Include(item => item.GoodsReceipt)
        .Where(item => productIds.Contains(item.ProductId))
        .ToList()
        .GroupBy(item => item.ProductId)
        .ToDictionary(
            group => group.Key,
            group =>
            {
                var ultima = group.OrderByDescending(item => item.GoodsReceipt.Date).ThenByDescending(item => item.ID).First();
                return ultima.PackSize == 0 ? ultima.PurchasePrice : ultima.PurchasePrice / ultima.PackSize;
            });

    /// <summary>Takes the purchase and selling price from the latest receipt of each product.</summary>
    private void UpdatePrices(List<StockLine> products, DateOnly date)
    {
        var latestGoodsReceipts = GoodsReceiptLinesInRange(LastStockCount(date), date)
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var product in products)
        {
            if (!latestGoodsReceipts.TryGetValue(product.Id, out var goodsReceipt)) continue;
            product.PurchasePrice = goodsReceipt.PurchasePrice / goodsReceipt.PackSize;
            product.Price = goodsReceipt.SellingPrice;
        }
    }

    public List<StockLine> CalculateStock(DateOnly date)
    {
        var lastStockCount = LastStockCount(date);
        var stock = db.Products.AsNoTracking().ToList()
            .Select(product => new StockLine
            {
                Id = product.ID, Name = product.Name, Code = product.Code, Quantity = 0, Price = product.Price,
                PurchasePrice = product.PurchasePrice, UnitOfMeasure = product.UnitOfMeasure, Category = product.Category
            })
            .ToDictionary(item => item.Id);

        // the latest counted quantities
        if (lastStockCount is not null)
        {
            foreach (var item in db.Stock.AsNoTracking().Where(row => row.Date == lastStockCount.Date))
                if (stock.TryGetValue(item.ProductId, out var selection)) selection.Quantity = item.Quantity;
        }

        // add the goods receipts and take the latest purchase price
        foreach (var group in GoodsReceiptLinesInRange(lastStockCount, date).GroupBy(item => item.ProductId))
        {
            if (!stock.TryGetValue(group.Key, out var selection)) continue;
            selection.Quantity += group.Sum(item => item.Quantity * item.PackSize);

            var ultima = group.First();
            selection.PurchasePrice = ultima.PurchasePrice / ultima.PackSize;
            selection.Price = ultima.SellingPrice;
        }

        // subtract the sales
        DateOnly? by = lastStockCount?.Date;
        var sales = LinesWithProducts()
            .Where(item => item.Sale.Date <= date && (by == null || item.Sale.Date > by))
            .ToList();
        foreach (var consumption in sales.SelectMany(Consumption).GroupBy(consumption => consumption.Product.ID))
        {
            if (stock.TryGetValue(consumption.Key, out var selection)) selection.Quantity -= consumption.Sum(item => item.Quantity);
        }

        foreach (var item in stock.Values) item.TheoreticalQuantity = item.Quantity;
        return stock.Values.OrderBy(item => item.Name).ToList();
    }

    public List<StockLine> ConsumptionByProduct(DateOnly date, string? category)
    {
        var items = LinesWithProducts()
            .Where(item => item.Sale.Date == date && SaleType.Register.Contains(item.Sale.Type!))
            .ToList();

        var result = GroupConsumption(items, category);
        UpdatePrices(result, date);
        return result;
    }

    public List<StockLine> MonthlyConsumption(int month, int an, string? category)
    {
        var prima = new DateOnly(an, month, 1);
        var items = LinesWithProducts()
            .Where(item => item.Sale.Date >= prima && item.Sale.Date < prima.AddMonths(1) && SaleType.Register.Contains(item.Sale.Type!))
            .ToList();

        var result = GroupConsumption(items, category);
        UpdatePrices(result, prima.AddMonths(1).AddDays(-1));
        return result;
    }

    private static List<StockLine> GroupConsumption(List<SaleLine> items, string? category) => items
        .SelectMany(Consumption)
        .Where(consumption => string.IsNullOrEmpty(category) || consumption.Product.Category.Contains(category, StringComparison.OrdinalIgnoreCase))
        .GroupBy(consumption => consumption.Product.ID)
        .Select(group =>
        {
            var product = group.First().Product;
            return new StockLine
            {
                Id = product.ID, Name = product.Name, Code = product.Code, Category = product.Category, UnitOfMeasure = product.UnitOfMeasure,
                Quantity = group.Sum(item => item.Quantity), Price = product.PurchasePrice, PurchasePrice = product.PurchasePrice
            };
        })
        .OrderBy(item => item.Name)
        .ToList();

    public List<StockLine> ReceivedQuantities(DateOnly date) => GoodsReceipts(
        db.GoodsReceiptLines.AsNoTracking().Where(item => item.GoodsReceipt.Date == date));

    public List<StockLine> MonthlyReceivedQuantities(int month, int an)
    {
        var prima = new DateOnly(an, month, 1);
        return GoodsReceipts(db.GoodsReceiptLines.AsNoTracking().Where(item => item.GoodsReceipt.Date >= prima && item.GoodsReceipt.Date < prima.AddMonths(1)));
    }

    private static List<StockLine> GoodsReceipts(IQueryable<GoodsReceiptLine> goodsReceipts) => goodsReceipts
        .ToList()
        .GroupBy(item => item.ProductId)
        .Select(group => new StockLine { Id = group.Key, Quantity = group.Sum(item => item.Quantity * item.PackSize) })
        .ToList();

    /// <summary>Recipes obtained from the sales of a day (a menu counts every recipe it contains).</summary>
    public List<StockLine> RecipesSold(DateOnly date)
    {
        var items = LinesWithProducts()
            .Where(item => item.Sale.Date == date && SaleType.Register.Contains(item.Sale.Type!))
            .ToList();

        var recipes = items.SelectMany(item =>
            item.Recipe is not null ? [(Recipe: item.Recipe, Quantity: item.Portions)]
            : item.Menu is not null ? item.Menu.Recipes.Select(menuRecipe => (menuRecipe.Recipe, Quantity: item.Portions * menuRecipe.Portions))
            : []);

        return recipes
            .GroupBy(item => item.Recipe.ID)
            .Select(group =>
            {
                var recipe = group.First().Recipe;
                return new StockLine
                {
                    Id = recipe.ID, Name = recipe.Name, Code = recipe.Code, Category = recipe.Category, UnitOfMeasure = "pcs",
                    Quantity = group.Sum(item => item.Quantity), Price = recipe.Price, PurchasePrice = recipe.Price
                };
            })
            .OrderBy(item => item.Name)
            .ToList();
    }

    /// <summary>Recipes and menus sold during a day, as sold (without expanding menus; products sold directly are not "produced").</summary>
    public List<StockLine> ItemsSold(DateOnly date) => db.SaleLines
        .AsNoTracking()
        .Include(item => item.Recipe)
        .Include(item => item.Menu)
        .Where(item => item.Sale.Date == date && SaleType.Register.Contains(item.Sale.Type!) && item.ProductId == null)
        .ToList()
        .Select(item => item.Recipe is not null
            ? new SoldLine(item.Recipe.ID, item.Recipe.Name, item.Recipe.Code, item.Recipe.Category, item.Portions, item.Price)
            : new SoldLine(item.Menu!.ID, item.Menu.Name, item.Menu.Code, "Menu", item.Portions, item.Price))
        .GroupBy(item => (item.Id, item.Name, item.Code, item.Category))
        .Select(group => new StockLine
        {
            Id = group.Key.Id, Name = group.Key.Name, Code = group.Key.Code, Category = group.Key.Category, UnitOfMeasure = "pcs",
            Quantity = group.Sum(item => item.Portions), Price = group.Average(item => item.Price), PurchasePrice = group.Average(item => item.Price)
        })
        .ToList();

    public List<SummaryLine> SalesSummary(DateOnly date) => db.SaleLines
        .AsNoTracking()
        .Include(item => item.Product)
        .Include(item => item.Recipe)
        .Include(item => item.Menu)
        .Where(item => item.Sale.Date == date)
        .ToList()
        .GroupBy(item => (Type: item.Product is not null ? 'P' : item.Recipe is not null ? 'R' : 'M', item.ProductId ?? item.RecipeId ?? item.MenuId, Name: item.DisplayName, Code: item.DisplayCode))
        .Select(group =>
        {
            decimal portions = group.Sum(item => item.Portions), price = group.Average(item => item.Price);
            return new SummaryLine(group.Key.Code ?? "", group.Key.Name ?? "", portions, price, portions * price);
        })
        .OrderBy(item => item.Product)
        .ToList();

    /// <summary>Stock movements of every product that was bought or sold between the two stock snapshots.</summary>
    public static List<BalantaLinie> StockBalance(List<StockLine> openingStock, List<StockLine> closingStock, List<StockLine> sales, List<StockLine> goodsReceipts)
    {
        var closings = closingStock.ToDictionary(item => item.Id);
        var sold = sales.ToLookup(item => item.Id);
        var purchased = goodsReceipts.ToLookup(item => item.Id);

        return openingStock
            .Where(opening => closings.ContainsKey(opening.Id))
            .Select(opening => new BalantaLinie(
                opening.Code, opening.Name, opening.Category, opening.Quantity, closings[opening.Id].Quantity,
                purchased[opening.Id].Sum(item => item.Quantity), sold[opening.Id].Sum(item => item.Quantity)))
            .Where(item => item.PurchasedQuantity > 0 || item.SoldQuantity > 0)
            .OrderBy(item => item.Product)
            .ToList();
    }
}

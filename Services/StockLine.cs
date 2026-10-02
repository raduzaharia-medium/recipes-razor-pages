namespace Recipes.Services;

/// <summary>A product (or recipe) with a quantity, used by the stock and sales reports.</summary>
public class StockLine
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal TheoreticalQuantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Price { get; set; }
    public decimal Value => Price * Quantity;
    public decimal PurchaseValue => PurchasePrice * Quantity;
}

public record SummaryLine(string Code, string Product, decimal Portions, decimal Price, decimal Value);
public record BalantaLinie(string Code, string Product, string Category, decimal OpeningStock, decimal ClosingStock, decimal PurchasedQuantity, decimal SoldQuantity);

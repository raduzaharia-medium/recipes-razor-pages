namespace Recipes.Models;

public class Product
{
    public int ID { get; set; }
    public required string Name { get; set => field = value.Trim(); }
    public decimal PurchasePrice { get; set; }
    public required string UnitOfMeasure { get; set => field = value.Trim(); }
    public required string Category { get; set => field = value.Trim(); }
    public required string Code { get; set => field = value.Trim(); }
    public decimal Price { get; set; }
    /// <summary>VAT rate of what is sold, as a fraction (0.24 = 24%); 0 means exempt.</summary>
    public decimal Vat { get; set; }
}

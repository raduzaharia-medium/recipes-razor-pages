namespace Recipes.Models;

public class StockEntry
{
    public int ID { get; set; }
    public int ProductId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Quantity { get; set; }
    public Product Product { get; set; } = null!;
}

namespace Recipes.Models;

public class RecipeIngredient
{
    public int ID { get; set; }
    public int RecipeId { get; set; }
    public decimal Quantity { get; set; }
    public int ProductId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Product Product { get; set; } = null!;

    public decimal PurchaseValue => Quantity * Product.PurchasePrice;
    public decimal PortionCost => Recipe is { QuantityMultiplier: > 0 } ? PurchaseValue / Recipe.QuantityMultiplier : 0m;
}

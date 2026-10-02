namespace Recipes.Models;

public class SaleLine
{
    public int ID { get; set; }
    public int SaleId { get; set; }
    public int? MenuId { get; set; }
    public int? RecipeId { get; set; }
    public decimal Portions { get; set; }
    public decimal Price { get; set; }
    /// <summary>VAT rate that applied when it was sold (a fraction); later changes of the item's rate do not alter past sales.</summary>
    public decimal Vat { get; set; }
    public int? ProductId { get; set; }
    public Sale Sale { get; set; } = null!;
    public Menu? Menu { get; set; }
    public Recipe? Recipe { get; set; }
    public Product? Product { get; set; }

    public decimal Value => Price * Portions;
    public string? DisplayName => Recipe?.Name ?? Menu?.Name ?? Product?.Name;
    public string? DisplayCode => Recipe?.Code ?? Menu?.Code ?? Product?.Code;
}

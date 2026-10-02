namespace Recipes.Models;

public class Menu
{
    public int ID { get; set; }
    public required string Name { get; set => field = value.Trim(); }
    public DateOnly? ValidOn { get; set; }
    public decimal Price { get; set; }
    /// <summary>VAT rate of what is sold, as a fraction (0.24 = 24%); 0 means exempt.</summary>
    public decimal Vat { get; set; }
    public required string Code { get; set => field = value.Trim(); }
    public List<MenuRecipe> Recipes { get; set; } = [];
    public List<SaleLine> SaleLines { get; set; } = [];
}

namespace Recipes.Models;

public class Recipe
{
    public int ID { get; set; }
    public required string Name { get; set => field = value.Trim(); }
    public decimal Price { get; set; }
    /// <summary>VAT rate of what is sold, as a fraction (0.24 = 24%); 0 means exempt.</summary>
    public decimal Vat { get; set; }
    public required string Code { get; set => field = value.Trim(); }
    public int QuantityMultiplier { get; set; }
    public required string Category { get; set => field = value.Trim(); }
    public required string Quantity { get; set => field = value.Trim(); }
    public List<RecipeIngredient> Products { get; set; } = [];
    public List<MenuRecipe> MenuRecipes { get; set; } = [];
    public List<SaleLine> SaleLines { get; set; } = [];
}

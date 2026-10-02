namespace Recipes.Models;

public class MenuRecipe
{
    public int ID { get; set; }
    public int RecipeId { get; set; }
    public decimal Portions { get; set; }
    public int MenuId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Menu Menu { get; set; } = null!;
}

using System.Globalization;

namespace Recipes.Pages.Menus;

public class WeekModel(RecipesContext db, TimeProvider time) : PageModel
{
    private static readonly CultureInfo English = new("en-US");

    public List<IGrouping<DateOnly?, Menu>> Days { get; private set; } = [];

    public void OnGet()
    {
        var today = time.Today;
        var firstDay = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // the week starts on Monday
        var lastDay = firstDay.AddDays(7);

        Days = db.Menus.AsNoTracking()
            .WithRecipes()
            .Where(m => m.ValidOn != null && m.ValidOn >= firstDay && m.ValidOn <= lastDay)
            .OrderBy(m => m.ValidOn).ThenBy(m => m.Code)
            .ToList()
            .GroupBy(m => m.ValidOn)
            .ToList();
    }

    public string DayName(DateOnly? day) => day?.ToString("dddd, MMMM d, yyyy", English) ?? "";
}

namespace Recipes.Pages.Menus;

public class IndexModel(RecipesContext db, TimeProvider time) : PageBase
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }

    public DateOnly Day => Date ?? time.Today;
    public List<Menu> Menus { get; private set; } = [];

    public string Title => (Day.DayNumber - time.Today.DayNumber) switch
    {
        0 => "Today's menus",
        1 => "Tomorrow's menus",
        2 => "Menus for the day after tomorrow",
        _ => $"Menus on {Day:dd/MM/yyyy}"
    };

    public void OnGet()
    {
        Menus = db.Menus.AsNoTracking().Where(m => m.ValidOn == Day || m.ValidOn == null).OrderBy(m => m.Code).ToList();
    }

    public IActionResult OnPostDelete(int id)
    {
        var menu = db.Menus.FirstOrDefault(m => m.ID == id);
        if (menu is not null)
        {
            if (db.SaleLines.Any(e => e.MenuId == id)) Error("The menu appears in sales and cannot be deleted.");
            else
            {
                db.Menus.Remove(menu);
                db.SaveChanges();
                Message($"Menu {menu.Name} was deleted.");
            }
        }

        return RedirectToPage(new { date = Date?.ToString("yyyy-MM-dd") });
    }
}

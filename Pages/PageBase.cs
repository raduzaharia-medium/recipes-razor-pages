namespace Recipes.Pages;

/// <summary>Shared helpers of the page models.</summary>
public abstract class PageBase : PageModel
{
    /// <summary>Where the main form posts: this page without the handler of the last postback, so Save never repeats "Add".</summary>
    public string FormAction => Request.Path + QueryString.Create(Request.Query.Where(q => q.Key != "handler"));

    protected void Message(string text) => TempData["Message"] = text;
    protected void Error(string text) => TempData["Error"] = text;
}

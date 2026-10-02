using System.Net;

namespace Recipes.Tests;

public class PageTests
{
    [Theory]
    [InlineData("/sales", "Sales", "sales")]
    [InlineData("/sales/new", "New sale", "sales")]
    [InlineData("/sales/1", "Sale", "sales")]
    [InlineData("/sales/summary", "Sales summary", "sales")]
    [InlineData("/sales/consumption", "Consumption report", "sales")]
    [InlineData("/sales/production", "Production report", "sales")]
    [InlineData("/sales/stock-balance", "Stock balance", "sales")]
    [InlineData("/sales/beverage-report", "Beverage report", "sales")]
    [InlineData("/menus", "Menus", "menus")]
    [InlineData("/menus/edit", "New menu", "menus")]
    [InlineData("/menus/edit/1", "Menu", "menus")]
    [InlineData("/menus/week", "Weekly menu", "menus")]
    [InlineData("/recipes", "Recipes", "recipes")]
    [InlineData("/recipes/edit", "New recipe", "recipes")]
    [InlineData("/recipes/edit/1", "Recipe", "recipes")]
    [InlineData("/goods-receipts", "Goods receipts", "goods-receipts")]
    [InlineData("/goods-receipts/edit", "New goods receipt", "goods-receipts")]
    [InlineData("/goods-receipts/edit/1", "Goods receipt", "goods-receipts")]
    [InlineData("/inventory", "Inventory", "inventory")]
    [InlineData("/inventory/stock-count", "Stock count", "inventory")]
    [InlineData("/inventory/new-product", "New product", "inventory")]
    [InlineData("/inventory/delete/1", "Delete product", "inventory")]
    public async Task Page_renders_in_layout_with_correct_menu(string url, string title, string menuActive)
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var html = await app.Get(url);

        Assert.Contains($"<title>{title} - Recipes</title>", html);
        Assert.Contains($"href=\"/{menuActive}\" class=\"active\"", html);
        Assert.Equal(1, TestApp.CountOccurrences(html, "class=\"active\""));
    }

    [Theory]
    [InlineData("/sales/1/invoice")]
    [InlineData("/sales/summary?print=true")]
    [InlineData("/sales/consumption?print=true")]
    [InlineData("/sales/production?print=true")]
    [InlineData("/sales/stock-balance?print=true")]
    [InlineData("/sales/beverage-report?print=true")]
    [InlineData("/menus/week/print")]
    [InlineData("/recipes/1/print")]
    [InlineData("/goods-receipts/1/print")]
    [InlineData("/inventory/print")]
    public async Task Printable_documents_use_the_print_layout_without_menu(string url)
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var html = await app.Get(url);

        Assert.Contains("data-autoprint", html);
        Assert.DoesNotContain("id=\"menu\"", html);
    }

    [Theory]
    [InlineData("/does-not-exist")]
    [InlineData("/recipes/abc")]
    [InlineData("/sales/999/invoice")]
    [InlineData("/recipes/999/print")]
    [InlineData("/Shared/_Layout")]
    [InlineData("/Pages/Index.cshtml")]
    public async Task Unknown_routes_and_internal_files_give_404(string url)
    {
        using var app = new TestApp();
        var response = await app.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Error_page_shows_the_message_for_404()
    {
        using var app = new TestApp();
        var response = await app.Client.GetAsync("/does-not-exist");

        Assert.Contains("Page not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Root_redirects_to_sales_list_and_static_assets_are_served()
    {
        using var app = new TestApp();

        var root = await app.Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/sales", root.Headers.Location?.OriginalString);

        foreach (var resource in new[] { "/css/app.css", "/css/print.css", "/js/site.js" })
            Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync(resource)).StatusCode);
    }

    [Fact]
    public async Task Forms_are_rejected_without_an_antiforgery_token()
    {
        using var app = new TestApp();
        var response = await app.Client.PostAsync("/inventory/new-product", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Code"] = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_can_be_browsed_by_month()
    {
        using var app = new TestApp();

        var html = await app.Get("/sales?date=2026-02-14&month=2026-03");

        Assert.Contains("March 2026", html);
        Assert.Contains("href=\"/sales?date=2026-03-01&amp;month=2026-03\"", html);
    }

    [Fact]
    public async Task Calendar_has_no_previous_today_next_day_links_but_marks_today()
    {
        using var app = new TestApp();

        var html = await app.Get("/sales?date=2026-03-02&month=2026-03");

        Assert.DoesNotContain("Day previous", html);
        Assert.DoesNotContain("Next day", html);
        Assert.Contains("class=\"day today\"", html);                    // today (18 March, the tests' fixed clock) has its own marking
        Assert.Contains("class=\"day selected\" href=\"/sales?date=2026-03-02", html); // the chosen day
        Assert.Contains("class=\"month\" href=\"/sales\"", html);      // the month name leads back to today
    }
}

public class SidebarTests
{
    // every page that has the menu column
    private static readonly string[] PaginiCuMenuLateral =
    [
        "/sales", "/sales/new", "/sales/1", "/sales/summary", "/sales/consumption", "/sales/production", "/sales/stock-balance", "/sales/beverage-report",
        "/menus", "/menus/edit", "/menus/edit/1", "/menus/week", "/recipes", "/recipes/edit", "/recipes/edit/1",
        "/goods-receipts", "/goods-receipts/edit", "/goods-receipts/edit/1", "/inventory", "/inventory/stock-count", "/inventory/new-product", "/inventory/delete/1"
    ];

    private static string OtherPlaces(string html)
    {
        int start = html.IndexOf("<legend>Other places</legend>", StringComparison.Ordinal);
        Assert.True(start >= 0, "the page has no 'Other places' panel");
        return html[start..html.IndexOf("</fieldset>", start, StringComparison.Ordinal)];
    }

    [Theory]
    [InlineData("/sales", new[] { "/menus", "/recipes", "/inventory" })]
    [InlineData("/sales/new", new[] { "/menus", "/recipes", "/inventory" })]
    [InlineData("/sales/consumption", new[] { "/menus", "/recipes", "/inventory" })]
    [InlineData("/menus", new[] { "/recipes", "/sales/new", "/sales" })]
    [InlineData("/recipes/edit/1", new[] { "/menus", "/inventory", "/goods-receipts" })]
    [InlineData("/goods-receipts", new[] { "/inventory", "/recipes", "/sales" })]
    [InlineData("/inventory/stock-count", new[] { "/goods-receipts", "/recipes", "/sales" })]
    public async Task Other_places_links_to_related_places_and_never_to_the_current_page(string page, string[] expected)
    {
        using var app = new TestApp();
        Restaurant.Create(app).GoodsReceipt();

        var panel = OtherPlaces(await app.Get(page));
        var links = System.Text.RegularExpressions.Regex.Matches(panel, "href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();

        Assert.Equal(expected, links);
    }

    [Fact]
    public async Task Every_page_with_a_sidebar_has_other_places_and_action_icons()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        foreach (var page in PaginiCuMenuLateral)
        {
            var html = await app.Get(page);
            OtherPlaces(html);
            Assert.Contains("class=\"toolbox\" data-icon=", html);
        }
    }

    [Fact]
    public async Task Every_icon_used_exists_as_a_file_and_has_a_style_rule()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var icons = new SortedSet<string>();
        foreach (var page in PaginiCuMenuLateral)
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(await app.Get(page), "data-icon=\"([^\"]+)\""))
                icons.Add(m.Groups[1].Value);

        Assert.True(icons.Count >= 20, $"expected the full icon set, found {icons.Count}");

        var style = await app.Get("/css/app.css");
        foreach (var icon in icons)
        {
            var response = await app.Client.GetAsync($"/icons/{icon}.svg");
            Assert.True(response.IsSuccessStatusCode, $"missing icons/{icon}.svg");
            Assert.Contains("<svg", await response.Content.ReadAsStringAsync());
            Assert.Contains($".toolbox[data-icon=\"{icon}\"]", style);
        }
    }

    [Fact]
    public async Task Sale_print_options_sit_in_their_own_row_and_sidebar_actions_are_regular_size()
    {
        using var app = new TestApp();

        var html = await app.Get("/sales/new");
        Assert.DoesNotContain("toolbox big", html);
        Assert.Equal(1, TestApp.CountOccurrences(html, "class=\"checks\""));
        Assert.DoesNotContain("Print invoice", html);
    }
}

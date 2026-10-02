using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recipes.Data;
using Recipes.Models;
using Recipes.Services;

namespace Recipes.Tests;

public class InventoryTests
{
    [Fact]
    public async Task New_product_is_validated_and_name_and_code_must_be_unique_ignoring_case()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var empty = await app.PostExpectingErrors("/inventory/new-product", ("Input.Code", ""), ("Input.Name", " "));
        Assert.Contains("Product name is required", empty);
        Assert.Contains("Product code is required", empty);
        Assert.Contains("Unit of measure is required", empty);
        Assert.Contains("Category is required", empty);

        var duplicate = await app.PostExpectingErrors("/inventory/new-product", ("Input.Code", "p001"), ("Input.Name", "FLOUR"), ("Input.UnitOfMeasure", "g"), ("Input.Category", "Groceries"));
        Assert.Contains("The product already exists", duplicate);
        Assert.Contains("Another product has this code", duplicate);

        var unde = await app.PostRedirect("/inventory/new-product", ("Input.Code", "P009"), ("Input.Name", "Salt"), ("Input.UnitOfMeasure", "kg"), ("Input.Category", "Groceries"));
        Assert.Equal("/inventory", unde);
        Assert.Equal("Salt", app.Db(db => db.Products.Single(p => p.Code == "P009").Name));
        Assert.Contains("Product Salt was added", await app.Get("/inventory")); // the message from TempData
    }

    [Fact]
    public async Task Stock_count_saves_quantities_and_write_off_is_printed()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale(); // cola theoretical 21, flour 9250, cheese -300

        var unde = await app.PostRedirect("/inventory/stock-count",
            ("Lines[0].Id", restaurant.Flour.ToString()), ("Lines[0].Quantity", "9250"), ("Lines[0].PurchasePrice", "0,005"), ("Lines[0].Price", "0"),
            ("Lines[1].Id", restaurant.Cola.ToString()), ("Lines[1].Quantity", "18"), ("Lines[1].PurchasePrice", "2.5"), ("Lines[1].Price", "7"),
            ("NewProducts[0].Code", "P004"), ("NewProducts[0].Name", "Salt"), ("NewProducts[0].Category", "Groceries"), ("NewProducts[0].UnitOfMeasure", "kg"), ("NewProducts[0].Quantity", "3"), ("NewProducts[0].PurchasePrice", "2"), ("NewProducts[0].Price", "0"));

        Assert.Equal("/inventory/write-offs", unde);
        var list = await app.Get(unde);
        Assert.Contains("Cola", list);
        Assert.Contains("3.00", list);
        Assert.Equal(HttpStatusCode.Redirect, (await app.Client.GetAsync("/inventory/write-offs")).StatusCode); // it is printed only once

        var stock = app.Db(db => new ReportService(db).CalculateStock(TestApp.TodayDate)).ToDictionary(s => s.Name);
        Assert.Equal(18m, stock["Cola"].Quantity);
        Assert.Equal(7m, stock["Cola"].Price);
        Assert.Equal(3m, stock["Salt"].Quantity);
        Assert.Equal(0.005m, stock["Flour"].PurchasePrice); // decimal comma

        // a second count on the same day replaces the first
        await app.PostRedirect("/inventory/stock-count", ("Lines[0].Id", restaurant.Cola.ToString()), ("Lines[0].Quantity", "20"), ("Lines[0].PurchasePrice", "2.5"), ("Lines[0].Price", "7"));
        Assert.Equal(20m, app.Db(db => new ReportService(db).CalculateStock(TestApp.TodayDate)).Single(s => s.Name == "Cola").Quantity);
    }

    [Fact]
    public async Task New_product_row_in_stock_count_is_validated_and_added_on_request()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var incomplete = await app.PostExpectingErrors("/inventory/stock-count?handler=Add", ("NewRow.Name", "Salt"));
        Assert.Contains("Code is required", incomplete);
        Assert.Contains("Category is required", incomplete);

        var duplicate = await app.PostExpectingErrors("/inventory/stock-count?handler=Add", ("NewRow.Code", "P001"), ("NewRow.Name", "Salt"), ("NewRow.Category", "x"), ("NewRow.UnitOfMeasure", "kg"));
        Assert.Contains("Another product has this code", duplicate);

        var response = await app.Post("/inventory/stock-count?handler=Add", ("NewRow.Code", "P004"), ("NewRow.Name", "Salt"), ("NewRow.Category", "Groceries"), ("NewRow.UnitOfMeasure", "kg"), ("NewRow.Quantity", "2"));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"NewProducts[0].Code\"", html);
        Assert.Contains("value=\"P004\"", html);

        // a filled-in but unadded row is not silently lost on save
        var notAdded = await app.PostExpectingErrors("/inventory/stock-count", ("NewRow.Name", "Salt"));
        Assert.Contains("has not been added", notAdded);
    }

    [Fact]
    public async Task Product_with_goods_receipts_is_deleted_only_by_replacement()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();

        var refused = await app.PostExpectingErrors($"/inventory/delete/{restaurant.Flour}");
        Assert.Contains("can only be replaced", refused);

        var wrong = await app.PostExpectingErrors($"/inventory/delete/{restaurant.Flour}", ("Replacement", "does not exist"));
        Assert.Contains("The replacement product was not selected correctly", wrong);

        var unde = await app.PostRedirect($"/inventory/delete/{restaurant.Flour}", ("Replacement", "P002")); // typed code, no autocomplete
        Assert.Equal("/inventory", unde);

        app.Db(db =>
        {
            Assert.False(db.Products.Any(p => p.ID == restaurant.Flour));
            Assert.All(db.RecipeIngredients.Where(r => r.RecipeId == restaurant.Pizza), r => Assert.Equal(restaurant.Cheese, r.ProductId));
            Assert.All(db.GoodsReceiptLines.Where(r => r.PackSize == 1000), r => Assert.Equal(restaurant.Cheese, r.ProductId));
        });
    }

    [Fact]
    public async Task Unused_product_is_deleted_directly_and_page_lists_recipes_using_it()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var page = await app.Get($"/inventory/delete/{restaurant.Cheese}");
        Assert.Contains("Pizza", page);

        await app.PostRedirect($"/inventory/delete/{restaurant.Cola}");
        Assert.False(app.Db(db => db.Products.Any(p => p.ID == restaurant.Cola)));
        Assert.Equal(HttpStatusCode.Redirect, (await app.Client.GetAsync($"/inventory/delete/{restaurant.Cola}")).StatusCode);
    }

    [Fact]
    public async Task Theoretical_stock_adds_goods_receipts_and_subtracts_sales_consumption()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var stock = app.Db(db => new ReportService(db).CalculateStock(TestApp.TodayDate)).ToDictionary(s => s.Name);

        // 10 x 1000 g received; sold 2 pizzas (2/2 x 500) + a menu with a pizza (1 x 500 / 2)
        Assert.Equal(10000m - 750m, stock["Flour"].Quantity);
        Assert.Equal(-300m, stock["Cheese"].Quantity); // 200 + 100
        Assert.Equal(24m - 3m, stock["Cola"].Quantity);
        Assert.Equal(4m / 1000m, stock["Flour"].PurchasePrice);
        Assert.Equal(6m, stock["Cola"].Price);

        var page = await app.Get("/inventory?q=chee");
        Assert.Contains("-300.00", page);
        Assert.DoesNotContain("Flour", page); // the filter
    }
}

public class RecipeTests
{
    private static (string, string)[] Recipe(string code, string name, params (string, string)[] extra) =>
        [("Input.Code", code), ("Input.Name", name), ("Input.Category", "Dishes"), ("Input.Quantity", "300g"), ("Input.Price", "20"), ("Input.Multiplier", "4"), .. extra];

    [Fact]
    public async Task Recipe_is_validated_and_name_and_code_are_unique()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var empty = await app.PostExpectingErrors("/recipes/edit", ("Input.Multiplier", "0"));
        foreach (var message in new[] { "Recipe name is required", "Recipe category is required", "Recipe code is required", "Recipe quantity is required", "Portions yielded must be greater than zero" })
            Assert.Contains(message, empty);

        var duplicate = await app.PostExpectingErrors("/recipes/edit", Recipe("r001", "PIZZA"));
        Assert.Contains("Another recipe has the same name", duplicate);
        Assert.Contains("Another recipe has the same code", duplicate);
    }

    [Fact]
    public async Task Recipe_is_created_with_ingredients_and_edited()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var unde = await app.PostRedirect("/recipes/edit", Recipe("R002", "Pizza mica",
            ("Products[0].ProductId", restaurant.Flour.ToString()), ("Products[0].Quantity", "300"), ("Products[1].ProductId", restaurant.Cheese.ToString()), ("Products[1].Quantity", "100,5")));
        Assert.Equal("/recipes", unde);

        var id = app.Db(db => db.Recipes.Single(r => r.Code == "R002").ID);
        app.Db(db => Assert.Equal([300m, 100.5m], db.RecipeIngredients.Where(r => r.RecipeId == id).OrderBy(r => r.ID).Select(r => r.Quantity).ToList()));

        await app.PostRedirect($"/recipes/edit/{id}", Recipe("R002", "Pizza mica 2", ("Products[0].ProductId", restaurant.Flour.ToString()), ("Products[0].Quantity", "50")));
        app.Db(db =>
        {
            var recipe = db.Recipes.Include(r => r.Products).Single(r => r.ID == id);
            Assert.Equal("Pizza mica 2", recipe.Name);
            Assert.Equal(50m, Assert.Single(recipe.Products).Quantity);
        });
    }

    [Fact]
    public async Task Save_after_adding_an_ingredient_saves_the_recipe()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var added = await app.Post("/recipes/edit?handler=Add", [.. Recipe("R9", "Soup"), ("NewRow.Search", "P001"), ("NewRow.Quantity", "100")]);
        var html = await added.Content.ReadAsStringAsync();
        var action = System.Text.RegularExpressions.Regex.Match(html, "<form method=\"post\" action=\"([^\"]*)\"").Groups[1].Value;

        var saved = await app.Post(action, [.. Recipe("R9", "Soup"), ("Products[0].ProductId", restaurant.Flour.ToString()), ("Products[0].Quantity", "100")]);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(1, app.Db(db => db.Recipes.Count(r => r.Code == "R9")));
    }

    [Fact]
    public async Task Ingredient_is_added_by_exact_code_and_price_comes_from_latest_goods_receipt()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt(); // flour: 4 RON / 1000 g

        var response = await app.Post("/recipes/edit?handler=Add", ("Input.Multiplier", "10"), ("NewRow.Search", "P001"), ("NewRow.Quantity", "1000"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains($"name=\"Products[0].ProductId\"", html);
        Assert.Contains("0.00", html);   // price per gram, rounded
        Assert.Contains(">4.00<", html); // value: 1000 g x 0.004

        var unknown = await app.PostExpectingErrors("/recipes/edit?handler=Add", ("NewRow.Search", "does not exist"), ("NewRow.Quantity", "1"));
        Assert.Contains("Product not found", unknown);
        var without = await app.PostExpectingErrors("/recipes/edit?handler=Add", ("NewRow.Search", "P001"), ("NewRow.Quantity", "0"));
        Assert.Contains("Quantity must be greater than zero", without);
    }

    [Fact]
    public async Task Recipe_totals_are_calculated_with_markup_and_VAT()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();

        var html = await app.Get($"/recipes/edit/{restaurant.Pizza}");

        // flour 500 x 0.004 = 2.00; cheese 200 x 0.02 = 4.00; multiplier 2 => 3.00 / portion; x3 + 0.36 = 9.36; +24% = 11.61
        Assert.Contains(">6.00<", html);
        Assert.Contains(">3.00<", html);
        Assert.Contains(">9.36<", html);
        Assert.Contains(">11.61<", html);
    }

    [Fact]
    public async Task Removing_an_ingredient_and_cloning_the_recipe()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var without = await app.Post($"/recipes/edit/{restaurant.Pizza}?handler=Remove&index=0", Recipe("R001", "Pizza",
            ("Products[0].ProductId", restaurant.Flour.ToString()), ("Products[0].Quantity", "500"), ("Products[1].ProductId", restaurant.Cheese.ToString()), ("Products[1].Quantity", "200")).Concat([("x", "")]).ToArray());
        var html = await without.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{restaurant.Cheese}\"", html);
        Assert.DoesNotContain("Products[1]", html);

        await app.PostRedirect($"/recipes/edit/{restaurant.Pizza}?handler=Clone");
        app.Db(db =>
        {
            var clone = db.Recipes.Include(r => r.Products).Single(r => r.ID != restaurant.Pizza);
            Assert.Equal($"R001 ({restaurant.Pizza}!)", clone.Code);
            Assert.Equal(2, clone.Products.Count);
        });
    }

    [Fact]
    public async Task Recipe_used_in_a_menu_cannot_be_deleted_and_prices_are_saved_in_bulk()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        await app.PostRedirect($"/recipes?handler=Delete&id={restaurant.Pizza}");
        Assert.Contains("used in a menu", await app.Get("/recipes"));
        Assert.True(app.Db(db => db.Recipes.Any(r => r.ID == restaurant.Pizza)));

        await app.PostRedirect("/recipes?handler=SavePrices", ("Prices[0].Id", restaurant.Pizza.ToString()), ("Prices[0].Price", "30,5"));
        Assert.Equal(30.5m, app.Db(db => db.Recipes.Single(r => r.ID == restaurant.Pizza).Price));
        Assert.Contains("Prices saved: 1", await app.Get("/recipes"));
    }

    [Fact]
    public async Task List_filters_by_category_and_name_ignoring_case()
    {
        using var app = new TestApp();
        Restaurant.Create(app);
        app.Db(db => { db.Recipes.Add(new Recipe { Code = "R002", Name = "Salad", Category = "Salads", Quantity = "200g", Price = 10, QuantityMultiplier = 1 }); db.SaveChanges(); });

        var all = await app.Get("/recipes");
        Assert.Contains("Pizza", all); Assert.Contains("Salad", all);

        var byName = await app.Get("/recipes?q=PIZ");
        Assert.Contains("Pizza", byName); Assert.DoesNotContain(">Salad<", byName);

        var byCategory = await app.Get("/recipes?category=Salads");
        Assert.Contains("Salad", byCategory); Assert.DoesNotContain(">Pizza<", byCategory);
    }
}

public class MenuTests
{
    [Fact]
    public async Task Menu_is_created_with_recipes_and_appears_only_on_its_day()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        var maine = TestApp.TodayDate.AddDays(1).ToString("yyyy-MM-dd");

        var page = await app.Post("/menus/edit?handler=Add", ("Input.Code", "M002"), ("NewRow.Search", "Pizza"), ("NewRow.Portions", "2"));
        Assert.Contains("name=\"Recipes[0].RecipeId\"", await page.Content.ReadAsStringAsync());

        var unde = await app.PostRedirect("/menus/edit", ("Input.Code", "M002"), ("Input.Name", "Menu for tomorrow"), ("Input.Price", "20"), ("Input.ValidOn", maine),
            ("Recipes[0].RecipeId", restaurant.Pizza.ToString()), ("Recipes[0].Portions", "2"));
        Assert.Equal($"/menus?date={maine}", unde);

        Assert.Contains("was saved", await app.Get($"/menus?date={TestApp.Today}")); // the message from TempData (it also contains the menu name)
        var today = await app.Get($"/menus?date={TestApp.Today}");
        Assert.DoesNotContain("Menu for tomorrow", today);
        Assert.Contains("Menu of the day", today); // no date = valid any day
        var day = await app.Get($"/menus?date={maine}");
        Assert.Contains("Menu for tomorrow", day); Assert.Contains("Menu of the day", day);
    }

    [Fact]
    public async Task Menu_is_validated_and_code_is_unique()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var empty = await app.PostExpectingErrors("/menus/edit", ("Input.Code", ""));
        Assert.Contains("Menu code is required", empty);
        Assert.Contains("Menu name is required", empty);

        var duplicate = await app.PostExpectingErrors("/menus/edit", ("Input.Code", "m001"), ("Input.Name", "Another menu"));
        Assert.Contains("Another menu has the same code", duplicate);

        var recipeNecunoscuta = await app.PostExpectingErrors("/menus/edit?handler=Add", ("NewRow.Search", "does not exist"), ("NewRow.Portions", "1"));
        Assert.Contains("Recipe not found", recipeNecunoscuta);
    }

    [Fact]
    public async Task Cloning_moves_the_code_to_the_copy()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        await app.PostRedirect($"/menus/edit/{restaurant.Menu}?handler=Clone");

        app.Db(db =>
        {
            Assert.Equal($"M001 ({restaurant.Menu}!)", db.Menus.Single(m => m.ID == restaurant.Menu).Code);
            var copy = db.Menus.Include(m => m.Recipes).Single(m => m.ID != restaurant.Menu);
            Assert.Equal("M001", copy.Code);
            Assert.Equal(restaurant.Pizza, Assert.Single(copy.Recipes).RecipeId);
        });
    }

    [Fact]
    public async Task Sold_menu_cannot_be_deleted()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.Sale();

        await app.PostRedirect($"/menus?handler=Delete&id={restaurant.Menu}");

        Assert.Contains("cannot be deleted", await app.Get("/menus"));
        Assert.True(app.Db(db => db.Menus.Any()));
    }

    [Fact]
    public async Task Weekly_menu_groups_menus_by_day()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        app.Db(db => { db.Menus.Add(new Menu { Code = "M002", Name = "Menu for today", Price = 10, ValidOn = TestApp.TodayDate, Recipes = [new() { RecipeId = restaurant.Pizza, Portions = 2 }] }); db.SaveChanges(); });

        var html = await app.Get("/menus/week");

        Assert.Contains("Menu for today", html);
        Assert.Contains("Pizza - 2 portions", html);
        Assert.DoesNotContain("Menu of the day", html); // without a date it belongs to no day
    }
}

public class GoodsReceiptTests
{
    [Fact]
    public async Task Added_line_calculates_totals_with_VAT()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var response = await app.Post("/goods-receipts/edit?handler=Add", ("Input.Number", "1"), ("Input.Date", TestApp.Today),
            ("NewRow.Search", "Flour"), ("NewRow.PackSize", "1000"), ("NewRow.Quantity", "10"), ("NewRow.VatPercent", "24"), ("NewRow.PurchasePrice", "4"), ("NewRow.SellingPrice", "0"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"Lines[0].ProductId\"", html);
        Assert.Contains("value=\"40.00\"", html);   // value
        Assert.Contains("value=\"9.60\"", html);    // Vat 24%
        Assert.Contains("value=\"49.60\"", html);   // incl. VAT
    }

    [Fact]
    public async Task Each_line_has_its_own_VAT_rate_which_the_next_line_and_the_next_receipt_start_from()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        // the first line of a brand-new database starts from the standard rate
        Assert.Contains("name=\"NewRow.VatPercent\" value=\"24\"", await app.Get("/goods-receipts/edit"));

        var response = await app.Post("/goods-receipts/edit?handler=Add", ("Input.Number", "1"), ("Input.Date", TestApp.Today),
            ("NewRow.Search", "Flour"), ("NewRow.PackSize", "1000"), ("NewRow.Quantity", "10"), ("NewRow.VatPercent", "11"), ("NewRow.PurchasePrice", "10"), ("NewRow.SellingPrice", "0"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("value=\"11\"", html);       // the line keeps its rate
        Assert.Contains("value=\"11.00\"", html);    // VAT of 100 at 11%
        Assert.Contains("value=\"111.00\"", html);   // incl. VAT
        Assert.Contains("name=\"NewRow.VatPercent\" value=\"11\"", html);   // the next line starts from the same rate

        await app.PostRedirect("/goods-receipts/edit", ("Input.Number", "1"), ("Input.Date", TestApp.Today),
            ("Lines[0].ProductId", restaurant.Flour.ToString()), ("Lines[0].PackSize", "1000"), ("Lines[0].Quantity", "10"), ("Lines[0].VatPercent", "11"), ("Lines[0].PurchasePrice", "10"), ("Lines[0].SellingPrice", "0"),
            ("Lines[1].ProductId", restaurant.Cola.ToString()), ("Lines[1].PackSize", "1"), ("Lines[1].Quantity", "4"), ("Lines[1].VatPercent", "0"), ("Lines[1].PurchasePrice", "2"), ("Lines[1].SellingPrice", "6"));

        app.Db(db =>
        {
            var lines = db.GoodsReceiptLines.OrderBy(l => l.ID).ToList();
            Assert.Equal([0.11m, 0m], lines.Select(l => l.Vat));
        });
        Assert.Contains("name=\"NewRow.VatPercent\" value=\"0\"", await app.Get("/goods-receipts/edit"));   // the next receipt starts from the last line used

        var id = app.Db(db => db.GoodsReceipts.Single().ID);
        var print = await app.Get($"/goods-receipts/{id}/print");
        Assert.Contains(">11%<", print);   // one row per VAT group
        Assert.Contains(">0%<", print);
        Assert.Contains("11%", await app.Get("/goods-receipts?date=" + TestApp.Today));

        var invalid = await app.PostExpectingErrors("/goods-receipts/edit", ("Input.Number", "2"), ("Input.Date", TestApp.Today),
            ("Lines[0].ProductId", restaurant.Flour.ToString()), ("Lines[0].PackSize", "1"), ("Lines[0].Quantity", "1"), ("Lines[0].VatPercent", "150"));
        Assert.Contains("VAT rate is invalid", invalid);
    }

    [Fact]
    public async Task Goods_receipt_is_saved_with_manually_corrected_totals_and_edited()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var unde = await app.PostRedirect("/goods-receipts/edit", ("Input.Number", "7"), ("Input.Date", TestApp.Today), ("Input.SupplierName", "Supplier Ltd"),
            ("Input.TotalValue", "40"), ("Input.TotalVat", "9,6"), ("Input.TotalWithVat", "50"), // 49.60 corrected to 50
            ("Lines[0].ProductId", restaurant.Flour.ToString()), ("Lines[0].PackSize", "1000"), ("Lines[0].Quantity", "10"), ("Lines[0].VatPercent", "24"), ("Lines[0].PurchasePrice", "4"), ("Lines[0].SellingPrice", "0"));
        Assert.Equal($"/goods-receipts?date={TestApp.Today}", unde);

        var id = app.Db(db => db.GoodsReceipts.Single(r => r.Number == 7).ID);
        app.Db(db =>
        {
            var r = db.GoodsReceipts.Include(x => x.Products).Single(x => x.ID == id);
            Assert.Equal((40m, 9.6m, 50m), (r.TotalValue, r.TotalVat, r.TotalWithVat));
            Assert.Equal(1000m, Assert.Single(r.Products).PackSize);
        });

        await app.PostRedirect($"/goods-receipts/edit/{id}", ("Input.Number", "7"), ("Input.Date", TestApp.Today),
            ("Lines[0].ProductId", restaurant.Cola.ToString()), ("Lines[0].PackSize", "1"), ("Lines[0].Quantity", "4"), ("Lines[0].PurchasePrice", "2.5"), ("Lines[0].SellingPrice", "6"));
        app.Db(db => Assert.Equal(restaurant.Cola, Assert.Single(db.GoodsReceiptLines.Where(l => l.GoodsReceiptId == id)).ProductId));
    }

    [Fact]
    public async Task Goods_receipt_is_validated()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var errors = await app.PostExpectingErrors("/goods-receipts/edit", ("Input.Number", "0"), ("Input.Date", TestApp.Today),
            ("Lines[0].ProductId", "9999"), ("Lines[0].PackSize", "0"), ("Lines[0].Quantity", "0"));

        Assert.Contains("Goods receipt number is required", errors);
        Assert.Contains("Quantity must be greater than zero", errors);
        Assert.Contains("Pack size must be greater than zero", errors);
    }

    [Fact]
    public async Task Unknown_product_can_be_added_to_inventory_from_the_goods_receipt()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var suggestion = await app.Post("/goods-receipts/edit?handler=Add", ("Input.Date", TestApp.Today), ("NewRow.Search", "Sugar"), ("NewRow.Quantity", "5"));
        var html = await suggestion.Content.ReadAsStringAsync();
        Assert.Contains("The product is not in the inventory", html);
        Assert.Contains("name=\"NewProduct.Code\"", html);

        var invalid = await app.PostExpectingErrors("/goods-receipts/edit?handler=CreateProduct", ("Input.Date", TestApp.Today), ("NewProduct.Name", "Sugar"));
        Assert.Contains("Code is required", invalid);

        var creat = await app.Post("/goods-receipts/edit?handler=CreateProduct", ("Input.Date", TestApp.Today), ("NewProduct.Code", "P010"), ("NewProduct.Name", "Sugar"), ("NewProduct.UnitOfMeasure", "kg"), ("NewProduct.Category", "Groceries"));
        var by = await creat.Content.ReadAsStringAsync();
        Assert.Equal("Sugar", app.Db(db => db.Products.Single(p => p.Code == "P010").Name));
        Assert.Contains("value=\"Sugar\"", by); // the new product is already in the add row
    }

    [Fact]
    public async Task Goods_receipt_is_deleted_with_its_lines_and_numbering_continues_in_same_month()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        var id = restaurant.GoodsReceipt(number: 1);

        Assert.Matches("name=\"Input.Number\"[^>]*value=\"2\"", await app.Get("/goods-receipts/edit"));

        await app.PostRedirect($"/goods-receipts?handler=Delete&id={id}");
        Assert.Empty(app.Db(db => db.GoodsReceipts.ToList()));
        Assert.Empty(app.Db(db => db.GoodsReceiptLines.ToList()));
    }

    [Fact]
    public async Task Price_change_report_shows_only_changed_selling_prices_versus_previous_receipt()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt(); // cola sells for 6
        var secondReceipt = app.Db(db =>
        {
            var r = new GoodsReceipt
            {
                Number = 2, Date = TestApp.TodayDate.AddDays(1),
                Products = [new() { ProductId = restaurant.Cola, PackSize = 1, Quantity = 1, PurchasePrice = 2.5m, SellingPrice = 7 }, new() { ProductId = restaurant.Flour, PackSize = 1000, Quantity = 1, PurchasePrice = 4, SellingPrice = 0 }]
            };
            db.GoodsReceipts.Add(r); db.SaveChanges(); return r.ID;
        });

        var html = await app.Get($"/goods-receipts/{secondReceipt}/print");

        Assert.Contains("Price change report", html);
        Assert.Contains("Cola", html);
        var priceReport = html[html.IndexOf("Price change report")..];
        Assert.DoesNotContain("Flour", priceReport);
        Assert.Contains(">6.00<", priceReport);
        Assert.Contains(">7.00<", priceReport);
    }
}

public class SalesTests
{
    private static (string, string)[] Items(Restaurant r) =>
    [
        ("Items[0].Type", "recipe"), ("Items[0].ItemId", r.Pizza.ToString()), ("Items[0].Code", "R001"), ("Items[0].Name", "Pizza"), ("Items[0].Price", "25"), ("Items[0].Portions", "2"),
        ("Items[1].Type", "product"), ("Items[1].ItemId", r.Cola.ToString()), ("Items[1].Code", "P003"), ("Items[1].Name", "Cola"), ("Items[1].Price", "6"), ("Items[1].Portions", "3"),
    ];

    [Fact]
    public async Task Item_is_added_by_exact_name_and_total_is_calculated()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var response = await app.Post("/sales/new?handler=Add", ("NewRow.Search", "pizza"), ("NewRow.Portions", "2"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"Items[0].ItemId\"", html);
        Assert.Contains("value=\"recipe\"", html);
        Assert.Contains("50.00", html);

        var unknown = await app.PostExpectingErrors("/sales/new?handler=Add", ("NewRow.Search", "unknown"), ("NewRow.Portions", "1"));
        Assert.Contains("No menu, recipe or product has this name", unknown);
        var without = await app.PostExpectingErrors("/sales/new?handler=Add", ("NewRow.Search", "Pizza"), ("NewRow.Portions", "0"));
        Assert.Contains("Portions must be greater than zero", without);
    }

    [Fact]
    public async Task Sale_is_saved_with_total_value_and_only_vouchers_with_a_count()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var unde = await app.PostRedirect("/sales/new", [("Input.Type", "Cash payment"), ("Input.Date", TestApp.Today), ("Input.PrintFiscalReceipt", "false"),
            ("Vouchers[0].VoucherValue", "10"), ("Vouchers[0].Number", "2"), ("Vouchers[1].VoucherValue", "15"), ("Vouchers[1].Number", "0"), .. Items(restaurant)]);

        Assert.Equal("/sales/new", unde);
        Assert.Contains("Sale recorded", await app.Get(unde));
        app.Db(db =>
        {
            var v = db.Sales.Include(x => x.SaleLines).Include(x => x.Vouchers).Single();
            Assert.Equal(2 * 25m + 3 * 6m, v.Value);
            Assert.Equal((10m, 2), (v.Vouchers.Single().VoucherValue, v.Vouchers.Single().VoucherCount));
            Assert.Equal([restaurant.Pizza], v.SaleLines.Where(e => e.RecipeId != null).Select(e => e.RecipeId!.Value));
        });

        // the voucher values used appear on the next sale, ready to fill in
        var page = await app.Get("/sales/new");
        Assert.Contains("name=\"Vouchers[0].VoucherValue\"", page);
        Assert.DoesNotContain("Vouchers[1]", page);
    }

    [Theory]
    [InlineData("/recipes/edit?handler=Add", "/recipes/edit")]
    [InlineData("/menus/edit?handler=Add&date=2026-03-18", "/menus/edit?date=2026-03-18")]
    [InlineData("/goods-receipts/edit?handler=Add", "/goods-receipts/edit")]
    [InlineData("/inventory/stock-count?handler=Add", "/inventory/stock-count")]
    [InlineData("/sales/new?handler=Add", "/sales/new")]
    public async Task Main_form_posts_without_the_handler_of_the_previous_action_so_Save_does_not_repeat_it(string url, string expectedAction)
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var response = await app.Post(url, ("NewRow.Search", "Pizza"), ("NewRow.Quantity", "1"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains($"<form method=\"post\" action=\"{expectedAction}\">", html);
    }

    [Fact]
    public async Task Sale_without_items_or_with_nonexistent_items_is_rejected()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var empty = await app.PostExpectingErrors("/sales/new", ("Input.Type", "Cash payment"), ("Input.Date", TestApp.Today));
        Assert.Contains("No products have been sold", empty);

        var missing = await app.PostExpectingErrors("/sales/new", ("Input.Type", "Cash payment"), ("Input.Date", TestApp.Today),
            ("Items[0].Type", "recipe"), ("Items[0].ItemId", "4242"), ("Items[0].Name", "Ghost"), ("Items[0].Price", "1"), ("Items[0].Portions", "0"),
            ("Items[1].Type", "unknown"), ("Items[1].ItemId", "1"), ("Items[1].Name", "Something else"), ("Items[1].Price", "1"), ("Items[1].Portions", "1"));
        Assert.Contains("Ghost no longer exists", missing);
        Assert.Contains("Something else no longer exists", missing);
        Assert.Contains("Portions must be greater than zero", missing);
        Assert.Empty(app.Db(db => db.Sales.ToList()));
    }

    [Fact]
    public async Task Meal_voucher_is_added_only_with_value_and_count()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var without = await (await app.Post("/sales/new?handler=AddVoucher", ("NewVoucher.VoucherValue", "10"), ("NewVoucher.Number", "0"))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Vouchers[0]", without);

        var withVoucher = await (await app.Post("/sales/new?handler=AddVoucher", ("NewVoucher.VoucherValue", "12,5"), ("NewVoucher.Number", "3"))).Content.ReadAsStringAsync();
        Assert.Contains("name=\"Vouchers[0].Number\"", withVoucher);
        Assert.Contains("37.50", withVoucher); // 3 x 12.5
    }

    [Fact]
    public async Task Invoice_opens_after_saving_and_fiscal_receipt_is_skipped_without_cash_register()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        var unde = await app.PostRedirect("/sales/new", [("Input.Type", "Bank transfer"), ("Input.Date", TestApp.Today), ("Input.PrintFiscalReceipt", "true"), ("Input.PrintInvoice", "true"), ("Input.PrintLineItems", "true"),
            ("Input.CustomerName", "Customer Ltd"), ("Input.CustomerTaxId", "RO123"), ("Input.InvoiceSeries", "AB"), ("Input.InvoiceNumber", "17"), .. Items(restaurant)]);

        var id = app.Db(db => db.Sales.Single().ID);
        Assert.Equal($"/sales/{id}/invoice?products=True", unde);

        var invoice = await app.Get(unde);
        Assert.Contains("AB 000017", invoice);
        Assert.Contains("Customer Ltd", invoice);
        Assert.Contains("RO123", invoice);
        Assert.Contains("data-autoprint", invoice);
        Assert.Contains("The cash register is not available", await app.Get("/sales/new")); // fiscal receipt skipped, and announced
    }

    [Fact]
    public async Task Invoice_calculates_VAT_from_price_and_groups_same_item_sold_several_times()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        var id = app.Db(db =>
        {
            var v = new Sale { Date = TestApp.TodayDate, Type = "Bank transfer", InvoiceSeries = "AB", InvoiceNumber = 1, SaleLines = [new() { RecipeId = restaurant.Pizza, Portions = 1, Price = 124, Vat = 0.24m }, new() { RecipeId = restaurant.Pizza, Portions = 2, Price = 124, Vat = 0.24m }] };
            db.Sales.Add(v); db.SaveChanges(); return v.ID;
        });

        var html = await app.Get($"/sales/{id}/invoice");

        Assert.Equal(1, TestApp.CountOccurrences(html, ">Pizza<"));
        Assert.Contains(">100.00<", html);  // price excl. VAT
        Assert.Contains(">300.00<", html);  // 3 portions
        Assert.Contains(">72.00<", html);   // 24% VAT of 300
        Assert.Contains(">372.00<", html);  // total to pay

        var without = await app.Get($"/sales/{id}/invoice?products=false");
        Assert.Contains("Products as per fiscal receipt", without);
    }

    [Fact]
    public async Task Invoice_has_one_VAT_row_per_rate_and_sale_lines_keep_the_rate_they_were_sold_with()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        app.Db(db => { db.Products.Single(p => p.ID == restaurant.Cola).Vat = 0.11m; db.SaveChanges(); });   // drinks at a reduced rate, the pizza stays at 24%

        await app.PostRedirect("/sales/new", [("Input.Type", "Bank transfer"), ("Input.Date", TestApp.Today), ("Input.PrintFiscalReceipt", "false"), ("Input.PrintInvoice", "true"), ("Input.PrintLineItems", "true"),
            ("Input.CustomerName", "Customer Ltd"),
            ("Items[0].Type", "recipe"), ("Items[0].ItemId", restaurant.Pizza.ToString()), ("Items[0].Name", "Pizza"), ("Items[0].Price", "124"), ("Items[0].Portions", "1"),
            ("Items[1].Type", "product"), ("Items[1].ItemId", restaurant.Cola.ToString()), ("Items[1].Name", "Cola"), ("Items[1].Price", "111"), ("Items[1].Portions", "1")]);
        app.Db(db => Assert.Equal([0.24m, 0.11m], db.SaleLines.OrderBy(l => l.ID).Select(l => l.Vat).ToList()));

        // changing the rate afterwards does not alter the sale that was made
        app.Db(db => { db.Products.Single(p => p.ID == restaurant.Cola).Vat = 0.05m; db.SaveChanges(); });
        var id = app.Db(db => db.Sales.Single().ID);
        var invoice = await app.Get($"/sales/{id}/invoice");

        Assert.Contains(">24%<", invoice);
        Assert.Contains(">11%<", invoice);
        Assert.DoesNotContain(">5%<", invoice);
        Assert.Contains(">100.00<", invoice);   // pizza excl. VAT
        Assert.Contains(">24.00<", invoice);    // its VAT
        Assert.Contains(">100.00<", invoice);   // cola excl. VAT: 111 / 1.11
        Assert.Contains(">11.00<", invoice);    // its VAT
        Assert.Contains(">235.00<", invoice);   // total to pay

        var summary = await app.Get($"/sales/{id}/invoice?products=false");
        Assert.Contains("(VAT 24%)", summary);
        Assert.Contains("(VAT 11%)", summary);
    }

    [Fact]
    public async Task Recipe_menu_and_product_keep_their_own_VAT_rate()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);

        // new ones start from the standard rate
        Assert.Contains("name=\"Input.VatPercent\" value=\"24\"", await app.Get("/recipes/edit"));
        Assert.Contains("name=\"Input.VatPercent\" value=\"24\"", await app.Get("/menus/edit"));
        Assert.Contains("name=\"Input.VatPercent\" value=\"24\"", await app.Get("/inventory/new-product"));

        await app.PostRedirect("/recipes/edit", ("Input.Code", "R7"), ("Input.Name", "Soup"), ("Input.Category", "Dishes"), ("Input.Quantity", "300g"), ("Input.Price", "10"), ("Input.VatPercent", "11"), ("Input.Multiplier", "1"));
        await app.PostRedirect("/menus/edit", ("Input.Code", "M7"), ("Input.Name", "Lunch"), ("Input.Price", "20"), ("Input.VatPercent", "9"));
        await app.PostRedirect("/inventory/new-product", ("Input.Code", "P9"), ("Input.Name", "Water"), ("Input.UnitOfMeasure", "pcs"), ("Input.Category", "Beverages"), ("Input.VatPercent", "5"));

        app.Db(db =>
        {
            Assert.Equal(0.11m, db.Recipes.Single(r => r.Code == "R7").Vat);
            Assert.Equal(0.09m, db.Menus.Single(m => m.Code == "M7").Vat);
            Assert.Equal(0.05m, db.Products.Single(p => p.Code == "P9").Vat);
        });

        var recipeId = app.Db(db => db.Recipes.Single(r => r.Code == "R7").ID);
        Assert.Contains("name=\"Input.VatPercent\" value=\"11\"", await app.Get($"/recipes/edit/{recipeId}"));

        var invalid = await app.PostExpectingErrors("/menus/edit", ("Input.Code", "M8"), ("Input.Name", "Other"), ("Input.VatPercent", "101"));
        Assert.Contains("VAT rate is invalid", invalid);
    }

    [Fact]
    public async Task List_shows_totals_and_sale_can_be_deleted_with_all_its_lines()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        var id = restaurant.Sale();
        restaurant.Sale("Staff meal");

        var list = await app.Get($"/sales?date={TestApp.Today}");
        Assert.Contains("196.00", list); // 2 x (2*25 + 30 + 3*6)
        Assert.Contains("98.00", list);  // only the paid sale

        await app.PostRedirect($"/sales?handler=Delete&id={id}");
        app.Db(db =>
        {
            Assert.Single(db.Sales.ToList());
            Assert.Equal(3, db.SaleLines.Count());
            Assert.Empty(db.SaleVouchers.Where(b => b.SaleId == id));
        });
        Assert.Contains("Sale #", await app.Get($"/sales?date={TestApp.Today}"));
    }

    [Fact]
    public async Task Sale_details_show_items_and_vouchers()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        var id = restaurant.Sale();

        var html = await app.Get($"/sales/{id}");

        Assert.Contains("Pizza", html);
        Assert.Contains("Menu of the day", html);
        Assert.Contains("Cola", html);
        Assert.Contains("2 x 10.00", html);
        Assert.Contains("98.00", html);
        Assert.Equal(HttpStatusCode.Redirect, (await app.Client.GetAsync("/sales/999")).StatusCode);
    }
}

public class ReportTests
{
    [Fact]
    public async Task Summary_groups_sales_by_item()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.Sale();
        restaurant.Sale();

        var html = await app.Get($"/sales/summary?date={TestApp.Today}");

        Assert.Contains(">196.00<", html);
        Assert.Contains(">4.00<", html);  // 4 portions of pizza
        Assert.True(html.IndexOf("Cola") < html.IndexOf("Menu of the day") && html.IndexOf("Menu of the day") < html.IndexOf(">Pizza<")); // ordered by name
    }

    [Fact]
    public async Task Consumption_note_sums_raw_materials_at_latest_receipt_price_and_filters_by_category()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var all = await app.Get($"/sales/consumption?date={TestApp.Today}");
        Assert.Contains(">750.00<", all);  // flour
        Assert.Contains(">3.00<", all);    // 750 x 0.004
        Assert.Contains(">7.50<", all);    // cola
        Assert.Contains("value=\"Beverages\"", all); // the categories in the filter list

        var beverages = await app.Get($"/sales/consumption?date={TestApp.Today}&category=Beverages");
        Assert.Contains("Cola", beverages.Replace("<option", "")[beverages.IndexOf("<tbody>")..]);
        Assert.DoesNotContain("Flour", beverages[beverages.IndexOf("<tbody>")..]);

        var print = await app.Get($"/sales/consumption?date={TestApp.Today}&category=Beverages&print=true");
        Assert.Contains("Category: </strong>Beverages", print);
        Assert.Contains("Test Storekeeper", print);
    }

    [Fact]
    public async Task Stock_balance_by_day_and_month_shows_stock_movements()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var balante = app.Db(db =>
        {
            var reports = new ReportService(db);
            var today = TestApp.TodayDate;
            return ReportService.StockBalance(reports.CalculateStock(today.AddDays(-1)), reports.CalculateStock(today), reports.ConsumptionByProduct(today, ""), reports.ReceivedQuantities(today));
        });
        var cola = balante.Single(b => b.Product == "Cola");
        Assert.Equal((0m, 21m, 24m, 3m), (cola.OpeningStock, cola.ClosingStock, cola.PurchasedQuantity, cola.SoldQuantity));

        var day = await app.Get($"/sales/stock-balance?date={TestApp.Today}");
        var month = await app.Get("/sales/stock-balance");
        Assert.Contains("Cola", day);
        Assert.Contains("Cola", month);
        Assert.Contains($"Stock balance - {TestApp.TodayDate:MM/yyyy}", month);
        Assert.Contains($"Stock balance - {TestApp.TodayDate:dd/MM/yyyy}", day);
        Assert.Contains("Cola", await app.Get($"/sales/stock-balance?month={TestApp.TodayDate:yyyy-MM}"));
    }

    [Fact]
    public async Task Beverage_report_shows_only_beverages_and_print_lists_raw_materials_and_what_was_produced()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var screen = await app.Get($"/sales/beverage-report?date={TestApp.Today}");
        var corp = screen[screen.IndexOf("<tbody>")..];
        Assert.Contains("Cola", corp);
        Assert.DoesNotContain("Flour", corp);

        var print = await app.Get($"/sales/beverage-report?date={TestApp.Today}&print=true");
        Assert.Contains("Raw materials consumed", print);
        Assert.Contains("Flour", print);
        Assert.Contains("Pizza", print); // 2 from the direct sale + 1 from the menu = 3.00
        Assert.Contains(">3.00<", print);
    }

    [Fact]
    public async Task Production_report_does_not_list_directly_sold_products_as_produced()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        restaurant.Sale();

        var produced = app.Db(db => new ReportService(db).ItemsSold(TestApp.TodayDate));

        Assert.Equal(["Menu of the day", "Pizza"], produced.Select(o => o.Name).Order());
        Assert.Contains("Menu of the day", await app.Get($"/sales/production?date={TestApp.Today}"));
    }
}

public class LookupTests
{
    [Fact]
    public async Task Lookup_ignores_case_and_searches_code_and_name()
    {
        using var app = new TestApp();
        Restaurant.Create(app);

        var byName = await app.Get("/lookup?handler=Products&q=FLOU");
        Assert.Contains("\"code\":\"P001\"", byName);
        Assert.DoesNotContain("Cheese", byName);
        Assert.Contains("Cheese", await app.Get("/lookup?handler=Products&q=p002"));
        Assert.Equal("[]", await app.Get("/lookup?handler=Products&q=100%25")); // % is not a wildcard
        Assert.Contains("\"label\":\"Groceries\"", await app.Get("/lookup?handler=Categories&q=GROC"));
    }

    [Fact]
    public async Task Sellable_items_include_menus_recipes_and_priced_products_but_not_archived_ones()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        await app.PostRedirect($"/recipes/edit/{restaurant.Pizza}?handler=Clone"); // "R001 (1!)" is not for sale
        await app.PostRedirect($"/menus/edit/{restaurant.Menu}?handler=Clone");

        var json = await app.Get("/lookup?handler=Items&q=");

        Assert.Contains("\"type\":\"product\"", json); // cola has a price
        Assert.DoesNotContain("Flour", json);        // no price
        Assert.Equal(1, TestApp.CountOccurrences(json, "\"label\":\"Pizza\""));
        Assert.DoesNotContain("!)", json);
        Assert.Equal(1, TestApp.CountOccurrences(json, "\"type\":\"menu\""));
    }

    [Fact]
    public async Task Customers_and_suppliers_are_found_by_name_with_their_details()
    {
        using var app = new TestApp();
        var restaurant = Restaurant.Create(app);
        restaurant.GoodsReceipt();
        app.Db(db => { db.Sales.Add(new Sale { Date = TestApp.TodayDate, Type = "Bank transfer", CustomerName = "Customer Ltd", CustomerTaxId = "RO123" }); db.SaveChanges(); });

        var client = await app.Get("/lookup?handler=Customers&q=ustomer");
        Assert.Contains("\"label\":\"Customer Ltd\"", client);
        Assert.Contains("\"taxId\":\"RO123\"", client);
        Assert.Contains("\"label\":\"Supplier Ltd\"", await app.Get("/lookup?handler=Suppliers&q=upplier"));
        Assert.Equal("[]", await app.Get("/lookup?handler=Customers&q=zzz"));
    }

    [Fact]
    public async Task Recipes_and_products_are_limited_to_a_few_suggestions()
    {
        using var app = new TestApp();
        app.Db(db =>
        {
            for (int i = 0; i < 20; i++) db.Products.Add(new Product { Code = $"X{i:00}", Name = $"Product {i}", Category = "c", UnitOfMeasure = "g" });
            db.SaveChanges();
        });

        var json = await app.Get("/lookup?handler=Products&q=Product");

        Assert.Equal(8, TestApp.CountOccurrences(json, "\"code\""));
    }
}

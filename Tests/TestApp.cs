using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Recipes.Data;
using Recipes.Models;

namespace Recipes.Tests;

/// <summary>A running copy of the app on its own empty SQLite database.</summary>
public sealed partial class TestApp : WebApplicationFactory<Program>
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"recipes-test-{Guid.NewGuid():N}.db");

    /// <summary>Does not follow redirects, so tests can see where a post goes.</summary>
    public HttpClient Client { get; }

    public TestApp()
    {
        Client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // The tests live in the app's own project, so there is no solution to find the app's content root from:
    // WebApplicationFactory also accepts it as the setting TEST_CONTENTROOT_<ASSEMBLY> (the folder with Pages, wwwroot, appsettings.json).
    static TestApp() => Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_Recipes", ProjectRoot());

    private static string ProjectRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "Recipes.csproj"))) return folder.FullName;

        throw new DirectoryNotFoundException("Recipes.csproj was not found above " + AppContext.BaseDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Recipes", $"Data Source={file}");
        builder.UseSetting("Company:Storekeeper", "Test Storekeeper");

        // "today" is fixed (a Wednesday), so the tests don't depend on the date they run on
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(TodayDate.ToDateTime(new TimeOnly(10, 30))));
        });
    }

    /// <summary>The date the app thinks it is.</summary>
    public static readonly DateOnly TodayDate = new(2026, 3, 18);

    public static string Today => TodayDate.ToString("yyyy-MM-dd");

    /// <summary>Runs code against the database (for arranging and checking data).</summary>
    public T Db<T>(Func<RecipesContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<RecipesContext>());
    }

    public void Db(Action<RecipesContext> action) => Db(db => { action(db); return 0; });

    public async Task<string> Get(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Posts a form like the browser does: fetches the page first for the antiforgery token and cookie.</summary>
    public async Task<HttpResponseMessage> Post(string url, params (string Name, string Value)[] fields)
    {
        var page = await Client.GetStringAsync(url.Split('?')[0]);
        var token = TokenAntiforgery().Match(page).Groups[1].Value;
        Assert.NotEmpty(token);

        var date = fields.Select(c => new KeyValuePair<string, string>(c.Name, c.Value)).Append(new("__RequestVerificationToken", token));
        return await Client.PostAsync(url, new FormUrlEncodedContent(date));
    }

    /// <summary>Posts and expects a redirect; returns where to.</summary>
    public async Task<string> PostRedirect(string url, params (string Name, string Value)[] fields)
    {
        var response = await Post(url, fields);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"POST {url} -> {(int)response.StatusCode}: {Messages(await response.Content.ReadAsStringAsync())}");
        return response.Headers.Location!.OriginalString;
    }

    /// <summary>Posts and expects the page to be shown again; returns the validation messages.</summary>
    public async Task<string> PostExpectingErrors(string url, params (string Name, string Value)[] fields)
    {
        var response = await Post(url, fields);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Messages(await response.Content.ReadAsStringAsync());
    }

    /// <summary>The text of the validation summary and field errors of a page.</summary>
    public static string Messages(string html) => string.Join(" | ", ErrorMessages().Matches(html)
        .Select(m => WebUtility.HtmlDecode(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)));

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    [GeneratedRegex("<li>([^<]*)</li>|field-validation-error[^>]*>([^<]*)<")]
    private static partial Regex ErrorMessages();

    /// <summary>How many times the text appears.</summary>
    public static int CountOccurrences(string text, string part) => text.Split(part).Length - 1;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        Client.Dispose();
        foreach (var f in new[] { file, file + "-wal", file + "-shm" }) File.Delete(f);
    }
}

/// <summary>The small restaurant used by most tests: flour, cheese and cola, a pizza recipe and a menu.</summary>
public sealed class Restaurant
{
    public required TestApp App { get; init; }
    public int Flour { get; private init; }
    public int Cheese { get; private init; }
    public int Cola { get; private init; }
    public int Pizza { get; private init; }
    public int Menu { get; private init; }

    public static Restaurant Create(TestApp app)
    {
        Product flour = new() { Code = "P001", Name = "Flour", Category = "Groceries", UnitOfMeasure = "g" },
               cheese = new() { Code = "P002", Name = "Cheese", Category = "Groceries", UnitOfMeasure = "g", PurchasePrice = 0.02m },
               cola = new() { Code = "P003", Name = "Cola", Category = "Beverages", UnitOfMeasure = "pcs", Price = 6, Vat = 0.24m };
        Recipe pizza = new()
        {
            Code = "R001", Name = "Pizza", Category = "Dishes", Quantity = "400g", Price = 25, Vat = 0.24m, QuantityMultiplier = 2,
            Products = [new() { Product = flour, Quantity = 500 }, new() { Product = cheese, Quantity = 200 }]
        };
        Menu menu = new() { Code = "M001", Name = "Menu of the day", Price = 30, Vat = 0.24m, Recipes = [new() { Recipe = pizza, Portions = 1 }] };

        app.Db(db => { db.AddRange(flour, cheese, cola, pizza, menu); db.SaveChanges(); });
        return new Restaurant { App = app, Flour = flour.ID, Cheese = cheese.ID, Cola = cola.ID, Pizza = pizza.ID, Menu = menu.ID };
    }

    /// <summary>10 kg of flour (10 x 1000 g at 4 RON) and 24 colas (2.5 RON, sold at 6), received today.</summary>
    public int GoodsReceipt(int number = 1) => App.Db(db =>
    {
        var goodsReceipt = new GoodsReceipt
        {
            Number = number, Date = TestApp.TodayDate, DeliveryDocumentType = "Invoice", SupplierName = "Supplier Ltd",
            TotalValue = 100, TotalVat = 24, TotalWithVat = 124,
            Products =
            [
                new() { ProductId = Flour, PackSize = 1000, Quantity = 10, Vat = 0.24m, PurchasePrice = 4, SellingPrice = 0 },
                new() { ProductId = Cola, PackSize = 1, Quantity = 24, Vat = 0.24m, PurchasePrice = 2.5m, SellingPrice = 6 }
            ]
        };
        db.GoodsReceipts.Add(goodsReceipt);
        db.SaveChanges();
        return goodsReceipt.ID;
    });

    /// <summary>2 pizzas + 1 menu (with a pizza) + 3 colas, paid in cash today.</summary>
    public int Sale(string type = "Cash payment") => App.Db(db =>
    {
        var sale = new Sale
        {
            Date = TestApp.TodayDate, Type = type,
            SaleLines =
            [
                new() { RecipeId = Pizza, Portions = 2, Price = 25 },
                new() { MenuId = Menu, Portions = 1, Price = 30 },
                new() { ProductId = Cola, Portions = 3, Price = 6 }
            ],
            Vouchers = [new() { VoucherValue = 10, VoucherCount = 2 }]
        };
        db.Sales.Add(sale);
        db.SaveChanges();
        return sale.ID;
    });
}

/// <summary>A clock that always shows the same moment (UTC is the local time zone).</summary>
public sealed class FixedTimeProvider(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

using System.Globalization;
using Recipes.Infrastructure;

// numbers and dates are exchanged in invariant format (1234.5, 2026-10-01)
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        options.ModelBinderProviders.Insert(0, new DecimalCommaBinderProvider());
        // inputs are validated by the handlers, not by implied [Required] on non-nullable references
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        options.MaxModelBindingCollectionSize = 100_000;
    });

// big forms (stock-take) post many fields
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.ValueCountLimit = 100_000);

builder.Services.AddDbContext<RecipesContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Recipes"), sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

builder.Services.Configure<CompanyOptions>(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<CashRegister>();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<RecipesContext>().Database.Migrate();
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

public partial class Program;

using Microsoft.Extensions.Options;

namespace Recipes.Pages.Inventory;

public class NewProductModel(RecipesContext db, IOptions<CompanyOptions> options) : PageBase
{
    [BindProperty] public ProductInput Input { get; set; } = new();
    public string? LastProductCode { get; private set; }

    public class ProductInput
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? UnitOfMeasure { get; set; }
        public string? Category { get; set; }
        public decimal VatPercent { get; set; }
    }

    public void OnGet()
    {
        LastProductCode = Last();
        Input.VatPercent = options.Value.StandardVat * 100;
    }

    public IActionResult OnPost()
    {
        ModelState.Required(Input.Name, "Input.Name", "Product name is required");
        ModelState.Required(Input.Code, "Input.Code", "Product code is required");
        ModelState.Required(Input.UnitOfMeasure, "Input.UnitOfMeasure", "Unit of measure is required");
        ModelState.Required(Input.Category, "Input.Category", "Category is required");
        ModelState.ErrorIf(Input.VatPercent is < 0 or > 100, "Input.VatPercent", "VAT rate is invalid");
        if (ModelState.IsValid)
        {
            ModelState.ErrorIf(db.Products.Any(p => p.Name == Input.Name), "Input.Name", "The product already exists");
            ModelState.ErrorIf(db.Products.Any(p => p.Code == Input.Code), "Input.Code", "Another product has this code");
        }

        if (!ModelState.IsValid)
        {
            LastProductCode = Last();
            return Page();
        }

        db.Products.Add(new Product { Code = Input.Code!, Name = Input.Name!, UnitOfMeasure = Input.UnitOfMeasure!, Category = Input.Category!, Vat = Input.VatPercent / 100 });
        db.SaveChanges();
        Message($"Product {Input.Name} was added.");
        return RedirectToPage("Index");
    }

    private string? Last() => db.Products.OrderByDescending(p => p.Code).Select(p => p.Code).FirstOrDefault();
}

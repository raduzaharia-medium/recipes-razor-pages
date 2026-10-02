namespace Recipes.Services;

public class CompanyOptions
{
    /// <summary>Standard VAT rate (0.24 = 24%): the starting rate offered for new products, recipes, menus and goods receipt lines. Every one of them can be given its own rate.</summary>
    public decimal StandardVat { get; set; } = 0.24m;
    public FPrintOptions FPrint { get; set; } = new();
    public CompanySection Company { get; set; } = new();
    public DelegateSection Delegate { get; set; } = new();
    public IssuerSection InvoiceIssuer { get; set; } = new();

    public class FPrintOptions
    {
        public string Path { get; set; } = "";
        public string Exe { get; set; } = "";
        public string Input { get; set; } = "";
    }

    public class CompanySection
    {
        public string Name { get; set; } = "";
        public string TradeRegisterNo { get; set; } = "";
        public string TaxId { get; set; } = "";
        public string HeadOffice { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Account { get; set; } = "";
        public string Storekeeper { get; set; } = "";
    }

    public class DelegateSection
    {
        public string Name { get; set; } = "";
        public string IdCardSeries { get; set; } = "";
        public string IdCardNumber { get; set; } = "";
        public string IdCardIssuer { get; set; } = "";
        public string Transport { get; set; } = "";
    }

    public class IssuerSection
    {
        public string Name { get; set; } = "";
        public string IdCardSeries { get; set; } = "";
        public string IdCardNumber { get; set; } = "";
        public string PersonalId { get; set; } = "";
    }
}

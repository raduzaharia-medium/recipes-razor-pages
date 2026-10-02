namespace Recipes.Models;

public class Sale
{
    public int ID { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerTaxId { get; set; }
    public string? CustomerTradeRegisterNo { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerCounty { get; set; }
    public string? CustomerAccount { get; set; }
    public string? CustomerBank { get; set; }
    public DateOnly? Date { get; set; }
    public string? Type { get; set; }
    public int? InvoiceNumber { get; set; }
    public string? InvoiceSeries { get; set; }
    public List<SaleLine> SaleLines { get; set; } = [];
    public List<SaleVoucher> Vouchers { get; set; } = [];

    public decimal Value => SaleLines.Sum(item => item.Value);
}

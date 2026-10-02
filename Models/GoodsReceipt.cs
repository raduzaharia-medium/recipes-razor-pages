namespace Recipes.Models;

public class GoodsReceipt
{
    public int ID { get; set; }

    public int Number { get; set; }

    public DateOnly Date { get; set; }

    public string? SupplierName { get; set; }
    public string? SupplierTaxId { get; set; }
    public string? SupplierTradeRegisterNo { get; set; }
    public string? SupplierAddress { get; set; }
    public string? SupplierCounty { get; set; }
    public string? SupplierAccount { get; set; }
    public string? SupplierBank { get; set; }
    public string? DeliveryDocumentType { get; set; }
    public string? DeliveryDocumentNumber { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalWithVat { get; set; }
    public List<GoodsReceiptLine> Products { get; set; } = [];

    public decimal Value => Products.Sum(item => item.Value);
}

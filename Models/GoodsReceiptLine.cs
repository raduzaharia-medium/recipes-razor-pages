namespace Recipes.Models;

public class GoodsReceiptLine
{
    public int ID { get; set; }
    public int GoodsReceiptId { get; set; }
    public decimal Quantity { get; set; }
    public int ProductId { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PackSize { get; set; }
    /// <summary>VAT rate of this line as a fraction (0.24 = 24%); 0 means exempt.</summary>
    public decimal Vat { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public Product Product { get; set; } = null!;

    public decimal Value => Quantity * PurchasePrice;
    public decimal PurchasePriceWithVat => PurchasePrice * (1 + Vat);
    public decimal VatValue => Value * Vat;
    public decimal ValueWithVat => Value + VatValue;
    public decimal MarkupPercent => PurchasePriceWithVat > 0 ? SellingPrice / PurchasePriceWithVat : 0m;
}

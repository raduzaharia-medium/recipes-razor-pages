namespace Recipes.Models;

public class SaleVoucher
{
    public int ID { get; set; }
    public int SaleId { get; set; }
    public decimal VoucherValue { get; set; }
    public int VoucherCount { get; set; }
    public Sale Sale { get; set; } = null!;

    public decimal Value => VoucherValue * VoucherCount;
}

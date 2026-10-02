namespace Recipes.Models;

/// <summary>How a sale was paid or accounted for (stored as text).</summary>
public static class SaleType
{
    public const string Cash = "Cash payment";
    public const string BankTransfer = "Bank transfer";
    public const string StaffMeal = "Staff meal";
    public const string DamagedGoods = "Damaged goods";

    public static readonly string[] All = [Cash, BankTransfer, StaffMeal, DamagedGoods];

    /// <summary>The sales that go through the cash register.</summary>
    public static readonly string[] Register = [Cash, BankTransfer];

    public static bool IsRegisterSale(string? type) => type is Cash or BankTransfer;
}

/// <summary>What a sale line sells.</summary>
public static class ItemType
{
    public const string Menu = "menu";
    public const string Recipe = "recipe";
    public const string Product = "product";

    public static readonly string[] All = [Menu, Recipe, Product];
}

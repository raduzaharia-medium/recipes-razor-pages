using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace Recipes.Services;

public record ReceiptLine(string Name, decimal Price, decimal Portions);
public record VoucherValue(decimal Value, int Number);

/// <param name="State">printed, skipped or error</param>
public record FiscalReceipt(string State, string? Message);

/// <summary>Writes the fiscal receipt file and launches the Datecs FPrint application (Windows only).</summary>
public class CashRegister(IOptions<CompanyOptions> options, ILogger<CashRegister> logger)
{
    private readonly CompanyOptions.FPrintOptions settings = options.Value.FPrint;

    public bool Available => OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(settings.Input) && !string.IsNullOrWhiteSpace(settings.Exe);

    public FiscalReceipt PrintFiscalReceipt(string? saleType, IReadOnlyList<ReceiptLine> items, IReadOnlyList<VoucherValue> vouchers)
    {
        if (!Available)
        {
            logger.LogWarning("Fiscal receipt skipped: FPrint is only available on Windows with FPrint settings configured");
            return new("skipped", "The cash register is not available on this system");
        }

        try
        {
            var inv = CultureInfo.InvariantCulture;
            decimal vouchersValue = vouchers.Sum(voucher => voucher.Value * voucher.Number);
            decimal saleValue = items.Sum(item => item.Price * item.Portions);

            using (var writer = new StreamWriter(settings.Input))
            {
                foreach (var item in items)
                {
                    string name = item.Name.Length > 19 ? item.Name[..19] : item.Name;
                    writer.WriteLine($"S,1,______,_,__;{name};{item.Price.ToString("0.00", inv)};{item.Portions};1;1;1;0;0;");
                }

                if (vouchersValue > 0)
                {
                    writer.WriteLine($"T,1,______,_,__;3;{vouchersValue.ToString("0.00", inv)};;;;");
                    if (vouchersValue < saleValue) writer.WriteLine("T,1,______,_,__;0;;;;;");
                }
                else if (saleType == SaleType.BankTransfer) writer.WriteLine($"T,1,______,_,__;2;{saleValue.ToString("0.00", inv)};;;;");
                else writer.WriteLine("T,1,______,_,__;0;;;;;"); // cash payment
            }

            Process.Start(new ProcessStartInfo { WorkingDirectory = settings.Path, FileName = settings.Exe, Arguments = settings.Input });
            return new("printed", null);
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Fiscal receipt could not be printed");
            return new("error", "The sale was recorded, but the fiscal receipt could not be printed.");
        }
    }
}

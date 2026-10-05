using System.Globalization;

namespace HeladeriaPOS.Services;

public static class CashPaymentCalculator
{
    public static decimal AmountDue(decimal total, decimal card, decimal transfer, bool mixed) =>
        Math.Max(0m, total - (mixed ? card + transfer : 0m));

    public static decimal ApplyQuickCash(decimal received, decimal due, string action)
    {
        if (string.Equals(action, "Exacto", StringComparison.OrdinalIgnoreCase)) return due;
        if (string.Equals(action, "Limpiar", StringComparison.OrdinalIgnoreCase)) return 0m;
        return decimal.TryParse(action, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) && amount > 0m
            ? received + amount : received;
    }
}

using System.Globalization;

namespace CinemaWebsite.Extensions;

public static class MoneyExtensions
{
    public const string CurrencyCode = "EUR";
    public const string CultureName = "de-DE";
    private static readonly CultureInfo EuroCulture = CultureInfo.GetCultureInfo(CultureName);

    // Format existing EUR amounts without performing currency conversion.
    public static string ToMoney(this decimal amount) => amount.ToString("C2", EuroCulture);
}

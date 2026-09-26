using System.Globalization;

namespace MyBudget.UI.Services;

/// <summary>Display helpers. Money is always shown with two decimals and grouping; never rounded here.</summary>
public static class Fmt
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public static string Money(decimal? d) => d is { } v ? v.ToString("C2", Us) : "—";
    public static string Money(decimal d) => d.ToString("C2", Us);
    public static string Pct(decimal? d) => d is { } v ? v.ToString("P1", Us) : "—";
    public static string Date(DateOnly? d) => d is { } v ? v.ToString("MMM d, yyyy", Us) : "—";
    public static string Month(DateOnly d) => d.ToString("MMM", Us);
    public static string Words(Enum e) => System.Text.RegularExpressions.Regex.Replace(e.ToString(), "(?<=[a-z0-9])([A-Z])", " $1");
}

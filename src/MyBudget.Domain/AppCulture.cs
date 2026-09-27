using System.Globalization;

namespace MyBudget.Domain;

/// <summary>
/// Pins formatting to en-US. Every engine writes its audit formulas with "C" and "N", so on a machine
/// with no LANG set — a bare GitHub runner, a minimal container, some desktop sessions — .NET falls
/// back to the invariant culture and every dollar figure renders as "¤210.00" instead of "$210.00".
///
/// This app is US-only by construction: federal and Missouri withholding, US contribution limits, US
/// card programs. There is no case where another currency is correct, so the entry points call this
/// once at startup rather than leaving the output at the mercy of an environment variable.
/// </summary>
public static class AppCulture
{
    public static readonly CultureInfo Default = CultureInfo.GetCultureInfo("en-US");

    public static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = Default;
        CultureInfo.DefaultThreadCurrentUICulture = Default;
        CultureInfo.CurrentCulture = Default;
        CultureInfo.CurrentUICulture = Default;
    }
}

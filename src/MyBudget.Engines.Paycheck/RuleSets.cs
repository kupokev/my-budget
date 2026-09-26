using MyBudget.Domain;

namespace MyBudget.Engines.Paycheck;

/// <summary>
/// Built-in reference values used to seed the year-keyed tables. The database copy is what the app
/// uses; this is the starting point you confirm each January. Rates are fractions.
/// </summary>
public static class RuleSets
{
    public static FederalRules Federal(int year) => year switch
    {
        2025 => new(2025,
            Std(15_000m, 30_000m, 22_500m),
            Fed(
                single: [0m, 11_925m, 48_475m, 103_350m, 197_300m, 250_525m, 626_350m],
                joint: [0m, 23_850m, 96_950m, 206_700m, 394_600m, 501_050m, 751_600m],
                hoh: [0m, 17_000m, 64_850m, 103_350m, 197_300m, 250_525m, 626_350m]),
            0.062m, 176_100m, 0.0145m, 0.009m, 200_000m, 0.22m, 0.37m, 1_000_000m),
        2026 => new(2026,
            Std(16_100m, 32_200m, 24_150m),
            Fed(
                single: [0m, 12_400m, 50_400m, 105_700m, 201_775m, 256_225m, 640_600m],
                joint: [0m, 24_800m, 100_800m, 211_400m, 403_550m, 512_450m, 768_700m],
                hoh: [0m, 17_700m, 67_450m, 105_700m, 201_775m, 256_225m, 640_600m]),
            0.062m, 184_500m, 0.0145m, 0.009m, 200_000m, 0.22m, 0.37m, 1_000_000m),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No built-in federal rules for {year}; enter them in Tax tables."),
    };

    public static MissouriRules Missouri(int year) => year switch
    {
        // 2025: 0% to 1,313 then 2% steps of 1,313 up to 4.5%, 4.7% over 9,191. Standard deduction = federal.
        2025 => new(2025, MoStd(15_000m, 30_000m, 22_500m), MoBrackets(1_313m, 0.047m), 0.047m),
        // 2026: PROVISIONAL — 2025 bracket edges and the 4.7% top rate carried forward until confirmed
        // against the 2026 Missouri Employer's Tax Guide / withholding formula.
        2026 => new(2026, MoStd(16_100m, 32_200m, 24_150m), MoBrackets(1_313m, 0.047m), 0.047m),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No built-in Missouri rules for {year}; enter them in Tax tables."),
    };

    /// <summary>Which built-in years are marked verified against the published tables.</summary>
    public static bool IsProvisional(int year, Jurisdiction j) => j == Jurisdiction.Missouri && year == 2026;

    public static readonly decimal[] FederalRates = [0.10m, 0.12m, 0.22m, 0.24m, 0.32m, 0.35m, 0.37m];

    private static Dictionary<FederalFilingStatus, decimal> Std(decimal single, decimal joint, decimal hoh) => new()
    {
        [FederalFilingStatus.SingleOrMarriedFilingSeparately] = single,
        [FederalFilingStatus.MarriedFilingJointly] = joint,
        [FederalFilingStatus.HeadOfHousehold] = hoh,
    };

    private static Dictionary<MissouriFilingStatus, decimal> MoStd(decimal single, decimal combined, decimal hoh) => new()
    {
        [MissouriFilingStatus.Single] = single,
        [MissouriFilingStatus.MarriedSpouseWorks] = single,
        [MissouriFilingStatus.MarriedOneIncome] = combined,
        [MissouriFilingStatus.HeadOfHousehold] = hoh,
    };

    private static Dictionary<FederalFilingStatus, IReadOnlyList<Bracket>> Fed(decimal[] single, decimal[] joint, decimal[] hoh) => new()
    {
        [FederalFilingStatus.SingleOrMarriedFilingSeparately] = Zip(single),
        [FederalFilingStatus.MarriedFilingJointly] = Zip(joint),
        [FederalFilingStatus.HeadOfHousehold] = Zip(hoh),
    };

    private static IReadOnlyList<Bracket> Zip(decimal[] overs) => overs.Select((o, i) => new Bracket(o, FederalRates[i])).ToList();

    /// <summary>Missouri's table: 0% for the first step, then 2.0% rising 0.5% per step to 4.5%, then the top rate.</summary>
    public static IReadOnlyList<Bracket> MoBrackets(decimal step, decimal topRate)
    {
        var list = new List<Bracket> { new(0m, 0m) };
        decimal[] rates = [0.02m, 0.025m, 0.03m, 0.035m, 0.04m, 0.045m];
        for (var i = 0; i < rates.Length; i++) list.Add(new Bracket(step * (i + 1), rates[i]));
        list.Add(new Bracket(step * 7, topRate));
        return list;
    }
}

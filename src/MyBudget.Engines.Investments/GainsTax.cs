namespace MyBudget.Engines.Investments;

public sealed record GainsTaxInput(
    decimal ShortTermGain, decimal LongTermGain,
    /// <summary>Ordinary marginal federal rate on the next dollar (from the paycheck estimate), e.g. 0.24.</summary>
    decimal OrdinaryMarginalRate,
    /// <summary>Ordinary taxable income before these gains, to place long-term gains in the 0/15/20% brackets.</summary>
    decimal OrdinaryTaxableIncome,
    decimal Ltcg15Threshold, decimal Ltcg20Threshold,
    /// <summary>Missouri taxes gains as ordinary income at its top rate.</summary>
    decimal MissouriRate);

public sealed record GainsTaxEstimate(decimal ShortTermTax, decimal LongTermTax, decimal MissouriTax, decimal Total, IReadOnlyList<string> Steps);

/// <summary>INV-4: short-term at the ordinary marginal rate; long-term stacked on ordinary income across 0/15/20%; Missouri at its rate. Net losses reduce to zero (no carryforward modelling).</summary>
public static class GainsTax
{
    public static GainsTaxEstimate Estimate(GainsTaxInput i)
    {
        var steps = new List<string>();
        var st = Math.Max(0, i.ShortTermGain);
        var stTax = R(st * i.OrdinaryMarginalRate);
        steps.Add($"Short-term {i.ShortTermGain:C}{(i.ShortTermGain < 0 ? " (loss, taxed as 0)" : "")} × ordinary {i.OrdinaryMarginalRate:P0} = {stTax:C}");

        var lt = Math.Max(0, i.LongTermGain);
        var stack = i.OrdinaryTaxableIncome + st;
        var at0 = Math.Max(0, Math.Min(lt, i.Ltcg15Threshold - stack));
        var at15 = Math.Max(0, Math.Min(lt - at0, i.Ltcg20Threshold - Math.Max(stack, i.Ltcg15Threshold)));
        var at20 = Math.Max(0, lt - at0 - at15);
        var ltTax = R(at15 * 0.15m + at20 * 0.20m);
        steps.Add($"Long-term {i.LongTermGain:C} stacked on {stack:C} ordinary: {at0:C} at 0% + {at15:C} at 15% + {at20:C} at 20% = {ltTax:C}");

        var moTax = R((st + lt) * i.MissouriRate);
        steps.Add($"Missouri: ({st:C} + {lt:C}) × {i.MissouriRate:P1} = {moTax:C}");
        return new GainsTaxEstimate(stTax, ltTax, moTax, R(stTax + ltTax + moTax), steps);
    }

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

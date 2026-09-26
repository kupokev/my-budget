using MyBudget.Domain;

namespace MyBudget.Engines.Hsa;

public sealed record HsaLimits(decimal SelfOnly, decimal Family, decimal CatchUp);

public sealed record HsaContributionInput(DateOnly Date, decimal Amount, HsaContributionSource Source);

public sealed record HsaPlanInput(
    int Year,
    /// <summary>Tier per month, index 0 = January. Missing months count as not eligible.</summary>
    IReadOnlyList<HsaTier> Months,
    HsaLimits Limits,
    IReadOnlyList<HsaContributionInput> Contributions,
    DateOnly AsOf,
    /// <summary>Checks remaining between AsOf and the target date, for the per-paycheck figure.</summary>
    int PaychecksLeft,
    bool CatchUpEligible = false,
    decimal? TargetAmount = null,
    DateOnly? TargetDate = null);

public sealed record HsaPlan(
    int EligibleMonths,
    decimal AnnualLimit, string LimitFormula,
    decimal Employer, decimal Payroll, decimal Direct, decimal Contributed,
    decimal Room,
    decimal Target, string TargetSource,
    decimal RemainingToTarget,
    int MonthsLeft, decimal RecommendedMonthly,
    int PaychecksLeft, decimal RecommendedPerPaycheck,
    bool OverContributed, decimal OverBy,
    IReadOnlyList<string> Steps);

/// <summary>
/// HSA-1..4: limit = Σ over eligible months of (that month's tier limit ÷ 12) [+ catch-up prorated the same way];
/// room = limit − all contributions from every source; recommended monthly = remaining ÷ months left to the target date.
/// Reproduces the sheet: $8,750 family × 11/12 = $8,020.83, less Inspira $1,288 = $6,732.83 to contribute.
/// </summary>
public static class HsaPlanner
{
    public static HsaPlan Plan(HsaPlanInput input)
    {
        var steps = new List<string>();
        var months = Enumerable.Range(0, 12).Select(i => i < input.Months.Count ? input.Months[i] : HsaTier.NotEligible).ToList();
        var selfMonths = months.Count(t => t == HsaTier.SelfOnly);
        var familyMonths = months.Count(t => t == HsaTier.Family);
        var eligible = selfMonths + familyMonths;

        var limit = input.Limits.Family * familyMonths / 12m + input.Limits.SelfOnly * selfMonths / 12m;
        var parts = new List<string>();
        if (familyMonths > 0) parts.Add($"{input.Limits.Family:C} family × {familyMonths}/12");
        if (selfMonths > 0) parts.Add($"{input.Limits.SelfOnly:C} self-only × {selfMonths}/12");
        if (input.CatchUpEligible && eligible > 0)
        {
            limit += input.Limits.CatchUp * eligible / 12m;
            parts.Add($"{input.Limits.CatchUp:C} catch-up × {eligible}/12");
        }
        limit = Round(limit);
        var limitFormula = parts.Count == 0 ? "no eligible months → $0" : string.Join(" + ", parts) + $" = {limit:C}";
        steps.Add($"Annual limit: {limitFormula}");

        var employer = Round(input.Contributions.Where(c => c.Source == HsaContributionSource.Employer).Sum(c => c.Amount));
        var payroll = Round(input.Contributions.Where(c => c.Source == HsaContributionSource.Payroll).Sum(c => c.Amount));
        var direct = Round(input.Contributions.Where(c => c.Source == HsaContributionSource.Direct).Sum(c => c.Amount));
        var contributed = employer + payroll + direct;
        var room = Round(limit - contributed);
        steps.Add($"Contributed so far: employer {employer:C} + payroll {payroll:C} + direct {direct:C} = {contributed:C}; room = {limit:C} − {contributed:C} = {room:C}");

        var target = input.TargetAmount is { } t ? Math.Min(t, limit) : limit;
        var targetSource = input.TargetAmount is { } tt ? (tt > limit ? $"your target {tt:C} capped at the limit" : "your target") : "the annual limit";
        var remaining = Round(target - contributed);
        var targetDate = input.TargetDate ?? new DateOnly(input.Year, 12, 31);
        var monthsLeft = Math.Max(0, (targetDate.Year - input.AsOf.Year) * 12 + targetDate.Month - input.AsOf.Month + 1);
        var monthly = monthsLeft > 0 && remaining > 0 ? Round(remaining / monthsLeft) : 0m;
        var perCheck = input.PaychecksLeft > 0 && remaining > 0 ? Round(remaining / input.PaychecksLeft) : 0m;
        steps.Add($"Target {target:C} ({targetSource}); remaining {remaining:C}; {monthsLeft} months through {targetDate:MMM yyyy} → {monthly:C}/month; {input.PaychecksLeft} checks left → {perCheck:C}/check");

        var over = contributed > limit;
        if (over) steps.Add($"Over-contributed by {contributed - limit:C}: withdraw the excess before the filing deadline to avoid the 6% excise tax.");

        return new HsaPlan(eligible, limit, limitFormula, employer, payroll, direct, contributed, room,
            target, targetSource, remaining, monthsLeft, monthly, input.PaychecksLeft, perCheck, over, over ? Round(contributed - limit) : 0m, steps);
    }

    private static decimal Round(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

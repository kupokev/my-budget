namespace MyBudget.Engines.Paycheck;

public sealed record SelfEmploymentInput(
    int Year,
    /// <summary>Net 1099 profit for the year (received or projected).</summary>
    decimal NetProfit,
    /// <summary>W-2 Social Security wages for the year, which use up the wage base first.</summary>
    decimal W2SocialSecurityWages,
    decimal W2MedicareWages,
    /// <summary>Federal ordinary marginal rate the extra income lands in.</summary>
    decimal FederalMarginalRate,
    decimal MissouriRate,
    FederalRules Federal,
    /// <summary>Estimated payments already made this year (federal, Missouri).</summary>
    decimal FederalPaid = 0, decimal MissouriPaid = 0,
    DateOnly? AsOf = null);

public sealed record QuarterlyPayment(int Quarter, DateOnly DueDate, decimal Federal, decimal Missouri, bool Past);

public sealed record SelfEmploymentEstimate(
    decimal NetProfit, decimal SeEarnings, decimal SocialSecurityTax, decimal MedicareTax, decimal SelfEmploymentTax, decimal HalfSeDeduction,
    decimal FederalIncomeTax, decimal MissouriIncomeTax, decimal TotalTax, decimal SetAsideFraction,
    decimal FederalRemaining, decimal MissouriRemaining, IReadOnlyList<QuarterlyPayment> Schedule, IReadOnlyList<string> Steps);

/// <summary>
/// INC-8: SE tax = 92.35% of net profit × (12.4% Social Security up to the wage base left after W-2 wages + 2.9% Medicare
/// + 0.9% additional over the threshold); half of it is deductible; income tax at the marginal rates. The remaining
/// balance is spread over the quarterly due dates still ahead.
/// </summary>
public static class SelfEmployment
{
    public static SelfEmploymentEstimate Estimate(SelfEmploymentInput i)
    {
        var steps = new List<string>();
        var f = i.Federal;
        var net = Math.Max(0, i.NetProfit);
        var seEarnings = R(net * 0.9235m);
        var ssRoom = Math.Max(0, f.SocialSecurityWageBase - i.W2SocialSecurityWages);
        var ssSubject = Math.Min(seEarnings, ssRoom);
        var ssTax = R(ssSubject * f.SocialSecurityRate * 2);
        var medTax = R(seEarnings * f.MedicareRate * 2);
        var addlOver = Math.Max(0, i.W2MedicareWages + seEarnings - Math.Max(i.W2MedicareWages, f.AdditionalMedicareThreshold));
        var addl = R(addlOver * f.AdditionalMedicareRate);
        var seTax = ssTax + medTax + addl;
        var half = R(seTax / 2);
        steps.Add($"SE earnings = {net:C} × 92.35% = {seEarnings:C}");
        steps.Add($"Social Security: min({seEarnings:C}, wage base {f.SocialSecurityWageBase:N0} − W-2 wages {i.W2SocialSecurityWages:N0} = {ssRoom:N0}) × 12.4% = {ssTax:C}");
        steps.Add($"Medicare: {seEarnings:C} × 2.9% = {medTax:C}" + (addl > 0 ? $" + {addlOver:C} over {f.AdditionalMedicareThreshold:N0} × 0.9% = {addl:C}" : ""));
        steps.Add($"Self-employment tax = {seTax:C}; half ({half:C}) is deductible");

        var fedIncome = R((net - half) * i.FederalMarginalRate);
        var moIncome = R((net - half) * i.MissouriRate);
        var total = seTax + fedIncome + moIncome;
        var fraction = net == 0 ? 0 : Math.Round(total / net, 4);
        steps.Add($"Federal income tax on ({net:C} − {half:C}) at marginal {i.FederalMarginalRate:P0} = {fedIncome:C}; Missouri at {i.MissouriRate:P1} = {moIncome:C}");
        steps.Add($"Total {total:C} = {fraction:P1} of net profit → set aside {fraction:P0} of every 1099 payment");

        var fedRemaining = Math.Max(0, seTax + fedIncome - i.FederalPaid);
        var moRemaining = Math.Max(0, moIncome - i.MissouriPaid);
        var asOf = i.AsOf ?? DateOnly.FromDateTime(DateTime.Today);
        var dues = new[] { new DateOnly(i.Year, 4, 15), new DateOnly(i.Year, 6, 15), new DateOnly(i.Year, 9, 15), new DateOnly(i.Year + 1, 1, 15) };
        var ahead = dues.Count(d => d >= asOf);
        var schedule = dues.Select((d, idx) => new QuarterlyPayment(idx + 1, d,
            d >= asOf && ahead > 0 ? R(fedRemaining / ahead) : 0, d >= asOf && ahead > 0 ? R(moRemaining / ahead) : 0, d < asOf)).ToList();
        steps.Add($"Remaining: federal {fedRemaining:C} (paid {i.FederalPaid:C}), Missouri {moRemaining:C} (paid {i.MissouriPaid:C}) over {ahead} due date(s) left");

        return new SelfEmploymentEstimate(net, seEarnings, ssTax, medTax + addl, seTax, half, fedIncome, moIncome, total, fraction, fedRemaining, moRemaining, schedule, steps);
    }

    /// <summary>Which federal bracket rate the next dollar of ordinary income lands in.</summary>
    public static decimal MarginalRate(decimal taxableIncome, IReadOnlyList<Bracket> brackets)
        => brackets.OrderBy(b => b.Over).LastOrDefault(b => taxableIncome >= b.Over)?.Rate ?? brackets[0].Rate;

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

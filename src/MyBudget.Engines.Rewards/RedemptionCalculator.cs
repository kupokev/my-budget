namespace MyBudget.Engines.Rewards;

/// <summary>How good a redemption is, on bands that hold across every program.</summary>
public enum RedemptionVerdict
{
    /// <summary>Under 0.45¢ a point. Pay cash and keep the points.</summary>
    Poor,
    /// <summary>0.45¢ to 0.55¢. A wash; decide on something other than the points.</summary>
    Marginal,
    /// <summary>0.55¢ and up. Spend the points.</summary>
    Good,
}

/// <summary>
/// One booking, priced both ways. Totals for the whole stay rather than per night, so a fourth- or
/// fifth-night-free benefit is already baked into whatever the booking page quoted.
/// </summary>
/// <param name="CashTotal">All-in cash price: room plus taxes and fees. An award stay wipes the taxes out, so they belong in what redeeming avoids.</param>
/// <param name="PointsTotal">Points for the same stay, same room, same dates.</param>
/// <param name="PointsEarnedIfPaidCash">Points the cash stay would have earned: program base, elite bonus and the card's rate. Booking with points forfeits these.</param>
/// <param name="EarnedPointValueCents">What a forfeited point is worth, for costing the line above. Zero skips the correction.</param>
public sealed record RedemptionInput(
    decimal CashTotal,
    decimal PointsTotal,
    decimal PointsEarnedIfPaidCash = 0m,
    decimal EarnedPointValueCents = 0m);

/// <param name="CentsPerPoint">The headline: value per point after subtracting what paying cash would have earned.</param>
/// <param name="GrossCentsPerPoint">Before that subtraction, which is what a booking page comparison alone would tell you.</param>
/// <param name="ForgoneValue">Dollar value of the points a cash stay would have earned.</param>
/// <param name="NetCashAvoided">What redeeming actually saves you: the cash price less the earnings you gave up.</param>
public sealed record RedemptionResult(
    decimal CentsPerPoint,
    decimal GrossCentsPerPoint,
    decimal ForgoneValue,
    decimal NetCashAvoided,
    RedemptionVerdict Verdict,
    string Formula);

/// <summary>
/// Points or cash for one hotel stay (RWD-7).
///
/// Two corrections separate this from dividing the cash price by the points. An award stay pays no
/// taxes, so the full all-in cash price is what redeeming avoids. And a cash stay earns points, which
/// booking with points forfeits, so that value comes back off the top. On a real booking the second
/// correction moved the answer by a tenth of a cent, which is a whole band.
///
/// The bands are deliberately program-agnostic. A single "value per point" per program can't describe
/// currencies whose worth swings more than two to one with the cash price, so the judgement belongs
/// on the individual booking rather than on the program.
/// </summary>
public static class RedemptionCalculator
{
    /// <summary>Below this, paying cash beats redeeming.</summary>
    public const decimal PoorBelow = 0.45m;

    /// <summary>At or above this, redeeming is clearly worth it.</summary>
    public const decimal GoodAtOrAbove = 0.55m;

    public static RedemptionVerdict Band(decimal centsPerPoint)
        => centsPerPoint < PoorBelow ? RedemptionVerdict.Poor
            : centsPerPoint < GoodAtOrAbove ? RedemptionVerdict.Marginal
            : RedemptionVerdict.Good;

    public static RedemptionResult Evaluate(RedemptionInput input)
    {
        if (input.PointsTotal <= 0)
            return new RedemptionResult(0m, 0m, 0m, 0m, RedemptionVerdict.Poor, "No points entered, so there is nothing to compare.");

        var gross = Round4(input.CashTotal / input.PointsTotal * 100m);
        var forgone = Round(input.PointsEarnedIfPaidCash * input.EarnedPointValueCents / 100m);
        var netCash = Round(input.CashTotal - forgone);
        var cents = Round4(netCash / input.PointsTotal * 100m);
        var verdict = Band(cents);

        var formula = forgone > 0
            ? $"{input.CashTotal:C} all-in − {input.PointsEarnedIfPaidCash:N0} pts earned by paying cash × {input.EarnedPointValueCents:0.##}¢ ({forgone:C}) = {netCash:C} avoided ÷ {input.PointsTotal:N0} pts = {cents:0.###}¢/pt"
            : $"{input.CashTotal:C} all-in ÷ {input.PointsTotal:N0} pts = {cents:0.###}¢/pt";

        return new RedemptionResult(cents, gross, forgone, netCash, verdict, formula);
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
}

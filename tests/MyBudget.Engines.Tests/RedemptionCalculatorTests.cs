using MyBudget.Engines.Rewards;

namespace MyBudget.Engines.Tests;

/// <summary>
/// Real bookings, not invented ones: IHG Midtown and Hilton Downtown Atlanta, both currencies quoted
/// for the same night off each brand's own booking page.
/// </summary>
public class RedemptionCalculatorTests
{
    private static decimal Cents(decimal cash, decimal points) => RedemptionCalculator.Evaluate(new(cash, points)).CentsPerPoint;

    [Theory]
    // IHG prices its awards against the cash price, so the value barely moves and sags at the top end.
    [InlineData("IHG Hotel Indigo Midtown", 199, 30_500, 0.6525, RedemptionVerdict.Good)]
    [InlineData("IHG voco The Darwin", 213, 38_500, 0.5532, RedemptionVerdict.Good)]
    [InlineData("IHG Kimpton Shane", 296, 63_500, 0.4661, RedemptionVerdict.Marginal)]
    // Hilton holds its award price nearly flat, so the value swings more than two to one with the cash price.
    [InlineData("Hilton Atlanta", 219, 60_000, 0.3650, RedemptionVerdict.Poor)]
    [InlineData("Hilton Candler Curio", 269, 70_000, 0.3843, RedemptionVerdict.Poor)]
    [InlineData("Hilton American Tapestry", 239, 60_000, 0.3983, RedemptionVerdict.Poor)]
    [InlineData("Hilton Home2 Suites", 262, 60_000, 0.4367, RedemptionVerdict.Poor)]
    [InlineData("Hilton Hampton Inn", 292, 60_000, 0.4867, RedemptionVerdict.Marginal)]
    [InlineData("Hilton Garden Inn Downtown", 476, 70_000, 0.6800, RedemptionVerdict.Good)]
    [InlineData("Hilton Embassy Suites Centennial", 574, 75_000, 0.7653, RedemptionVerdict.Good)]
    public void Real_atlanta_bookings_price_out_and_land_in_the_right_band(string _, decimal cash, decimal points, decimal expected, RedemptionVerdict verdict)
    {
        var r = RedemptionCalculator.Evaluate(new(cash, points));
        Assert.Equal(expected, r.CentsPerPoint);
        Assert.Equal(verdict, r.Verdict);
        Assert.Contains($"{points:N0} pts", r.Formula);   // the formula names its own inputs (auditability)
    }

    [Fact]
    public void The_cheapest_hilton_and_the_most_expensive_one_are_the_same_currency_doing_opposite_work()
    {
        // Same chain, same city, same night: an unremarkable hotel on a demand spike beats the big one.
        Assert.True(Cents(476, 70_000) > Cents(219, 60_000) * 1.8m);
    }

    [Fact]
    public void Taxes_count_because_an_award_stay_does_not_pay_them()
    {
        // The Indigo quoted $179 room + $20 fees. Leaving the fees out understates the redemption.
        var roomOnly = Cents(179, 30_500);
        var allIn = Cents(199, 30_500);
        Assert.Equal(0.5869m, roomOnly);
        Assert.Equal(0.6525m, allIn);
        Assert.True(allIn > roomOnly);
    }

    [Fact]
    public void Forfeiting_the_points_a_cash_stay_would_earn_moves_the_answer_by_most_of_a_band()
    {
        // Paying cash for the Indigo earns roughly 5,000 points as Diamond with the card's 10x.
        var r = RedemptionCalculator.Evaluate(new(CashTotal: 199m, PointsTotal: 30_500m,
            PointsEarnedIfPaidCash: 5_000m, EarnedPointValueCents: 0.55m));

        Assert.Equal(0.6525m, r.GrossCentsPerPoint);   // what the booking pages alone would suggest
        Assert.Equal(27.50m, r.ForgoneValue);
        Assert.Equal(171.50m, r.NetCashAvoided);
        Assert.Equal(0.5623m, r.CentsPerPoint);
        Assert.Contains("earned by paying cash", r.Formula);
    }

    [Fact]
    public void The_correction_can_push_a_marginal_redemption_into_paying_cash()
    {
        var before = RedemptionCalculator.Evaluate(new(292m, 60_000m));
        var after = RedemptionCalculator.Evaluate(new(292m, 60_000m, PointsEarnedIfPaidCash: 9_000m, EarnedPointValueCents: 0.5m));
        Assert.Equal(RedemptionVerdict.Marginal, before.Verdict);
        Assert.Equal(RedemptionVerdict.Poor, after.Verdict);
    }

    [Theory]
    [InlineData(0.4400, RedemptionVerdict.Poor)]
    [InlineData(0.4499, RedemptionVerdict.Poor)]
    [InlineData(0.4500, RedemptionVerdict.Marginal)]   // the band edges meet, no gap between them
    [InlineData(0.5499, RedemptionVerdict.Marginal)]
    [InlineData(0.5500, RedemptionVerdict.Good)]
    public void Bands_are_contiguous(decimal cents, RedemptionVerdict expected)
        => Assert.Equal(expected, RedemptionCalculator.Band(cents));

    [Fact]
    public void A_stay_with_no_points_price_says_so_instead_of_dividing_by_zero()
    {
        var r = RedemptionCalculator.Evaluate(new(199m, 0m));
        Assert.Equal(0m, r.CentsPerPoint);
        Assert.Equal(RedemptionVerdict.Poor, r.Verdict);
        Assert.Contains("nothing to compare", r.Formula);
    }

    [Fact]
    public void Totals_for_the_whole_stay_absorb_a_free_night_benefit()
    {
        // Four nights at the Indigo with the card's fourth night free: pay for three, stay four.
        var perNight = Cents(199, 30_500);
        var fourNights = Cents(199 * 4, 30_500 * 3);
        Assert.Equal(0.6525m, perNight);
        Assert.Equal(0.8699m, fourNights);
    }
}

using MyBudget.Domain;

namespace MyBudget.Engines.Tests;

/// <summary>
/// A benefit you have to use is worth nothing until you use it. Counting it regardless is what makes
/// a card look worth its fee when it is not — Kevin's three cases are a Delta bag allowance worth $90
/// a round trip, a Hilton credit of $50 that expires every quarter, and an IHG reward night worth
/// about $120.
/// </summary>
public class EarnedPerkTests
{
    private static CardPerk Perk(decimal perUse, PerkPeriod period, int? cap, params DateOnly[] uses)
    {
        var perk = new CardPerk { Description = "Benefit", ValuePerUse = perUse, Period = period, MaxUsesPerPeriod = cap };
        foreach (var d in uses) perk.Uses.Add(new CardPerkUse { Date = d });
        return perk;
    }

    [Fact]
    public void An_unused_benefit_is_worth_nothing()
    {
        var delta = Perk(90m, PerkPeriod.Year, cap: null);

        Assert.Equal(0m, delta.ValueIn(2026));
    }

    [Fact]
    public void Two_round_trips_beat_the_delta_fee_and_one_does_not()
    {
        var one = Perk(90m, PerkPeriod.Year, null, new DateOnly(2026, 3, 4));
        var two = Perk(90m, PerkPeriod.Year, null, new DateOnly(2026, 3, 4), new DateOnly(2026, 8, 19));

        Assert.Equal(90m, one.ValueIn(2026));       // under the $150 fee
        Assert.Equal(180m, two.ValueIn(2026));      // over it
    }

    [Fact]
    public void A_quarterly_credit_cannot_be_claimed_twice_in_one_quarter()
    {
        // Two Hilton stays in Q1 still only release Q1's single $50 credit.
        var hilton = Perk(50m, PerkPeriod.Quarter, cap: 1, new DateOnly(2026, 1, 8), new DateOnly(2026, 2, 20));

        Assert.Equal(50m, hilton.ValueIn(2026));
    }

    [Fact]
    public void A_quarterly_credit_earns_once_in_each_quarter_it_is_used()
    {
        var hilton = Perk(50m, PerkPeriod.Quarter, 1,
            new DateOnly(2026, 2, 3), new DateOnly(2026, 5, 9), new DateOnly(2026, 8, 14), new DateOnly(2026, 11, 2));

        Assert.Equal(200m, hilton.ValueIn(2026));   // against a $150 fee
    }

    [Fact]
    public void A_missed_quarter_is_simply_lost()
    {
        // Nothing in Q3: it does not roll into Q4.
        var hilton = Perk(50m, PerkPeriod.Quarter, 1,
            new DateOnly(2026, 2, 3), new DateOnly(2026, 5, 9), new DateOnly(2026, 11, 2), new DateOnly(2026, 12, 20));

        Assert.Equal(150m, hilton.ValueIn(2026));
    }

    [Fact]
    public void The_current_period_reports_what_is_still_claimable()
    {
        var hilton = Perk(50m, PerkPeriod.Quarter, 1, new DateOnly(2026, 2, 3));

        Assert.Equal((1, 1), hilton.UsesIn(new DateOnly(2026, 3, 30)));   // Q1 claimed
        Assert.Equal((0, 1), hilton.UsesIn(new DateOnly(2026, 4, 1)));    // Q2 open again
    }

    [Fact]
    public void Uses_in_another_year_do_not_count()
    {
        var ihg = Perk(120m, PerkPeriod.Year, null, new DateOnly(2025, 6, 1), new DateOnly(2026, 6, 1));

        Assert.Equal(120m, ihg.ValueIn(2026));
    }

    [Fact]
    public void A_benefit_that_simply_arrives_still_counts_without_being_logged()
    {
        // The original behaviour: a credit posted whether or not you do anything.
        var automatic = new CardPerk { Description = "Global Entry credit", AnnualValue = 100m };

        Assert.Equal(100m, automatic.ValueIn(2026));
        Assert.False(automatic.IsEarned);
    }
}

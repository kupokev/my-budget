using MyBudget.Domain;

namespace MyBudget.Engines.Tests;

/// <summary>
/// A card's fee changes: waived for a first year, raised later. Keeping one number would mean editing
/// it rewrites what the card cost in a year that has already happened — the Hilton Surpass fee was
/// waived in its first year, and charging it retroactively made the card look worse than it was.
/// </summary>
public class CardFeeHistoryTests
{
    private static Card Surpass(params (int Year, decimal Amount)[] rows)
    {
        var card = new Card { Name = "Hilton Surpass", AnnualFee = 150m };
        foreach (var (year, amount) in rows) card.Fees.Add(new CardFee { FromYear = year, Amount = amount });
        return card;
    }

    [Fact]
    public void With_no_history_every_year_uses_the_current_fee()
    {
        var card = Surpass();

        Assert.Equal(150m, card.FeeFor(2025));
        Assert.Equal(150m, card.FeeFor(2026));
    }

    [Fact]
    public void A_waived_first_year_costs_nothing_and_later_years_still_do()
    {
        var card = Surpass((2026, 0m), (2027, 150m));

        Assert.Equal(0m, card.FeeFor(2026));     // waived
        Assert.Equal(150m, card.FeeFor(2027));
        Assert.Equal(150m, card.FeeFor(2030));   // carries forward until something supersedes it
    }

    [Fact]
    public void A_rise_applies_from_its_year_onward_and_leaves_earlier_years_alone()
    {
        var card = Surpass((2026, 95m), (2029, 150m));

        Assert.Equal(95m, card.FeeFor(2026));
        Assert.Equal(95m, card.FeeFor(2028));
        Assert.Equal(150m, card.FeeFor(2029));
    }

    [Fact]
    public void A_year_before_the_earliest_row_falls_back_to_the_current_fee()
    {
        var card = Surpass((2026, 0m));

        Assert.Equal(150m, card.FeeFor(2025));
    }

    [Fact]
    public void Rows_are_read_by_the_latest_start_not_the_order_they_were_entered()
    {
        var card = Surpass((2029, 150m), (2026, 0m));

        Assert.Equal(0m, card.FeeFor(2027));
    }
}

using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Engines.Tests;

/// <summary>
/// A budget line's amount changes: a gym membership at $50 goes to $51 in October. Keeping one number
/// would restate the nine months already budgeted and reconciled at the old price, so the amount is
/// effective-dated the same way a card's fee and a pay schedule are.
/// </summary>
public class BudgetLineAmountTests
{
    private static BudgetLine Ymca(params (int Year, int Month, decimal Amount)[] changes)
    {
        var line = new BudgetLine { Name = "YMCA", Frequency = BudgetFrequency.Monthly, ProjectedAmount = 50m, DueDay = 1 };
        foreach (var (y, m, a) in changes) line.Amounts.Add(new BudgetLineAmount { FromPeriod = new DateOnly(y, m, 1), Amount = a });
        return line;
    }

    [Fact]
    public void With_no_changes_every_month_uses_the_usual_amount()
    {
        var line = Ymca();

        Assert.Equal(50m, line.AmountFor(new DateOnly(2026, 3, 1)));
        Assert.Equal(50m, line.AmountFor(new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void A_rise_applies_from_its_month_and_leaves_the_earlier_months_alone()
    {
        var line = Ymca((2026, 10, 51m));

        Assert.Equal(50m, line.AmountFor(new DateOnly(2026, 9, 30)));   // the nine months already budgeted
        Assert.Equal(51m, line.AmountFor(new DateOnly(2026, 10, 1)));
        Assert.Equal(51m, line.AmountFor(new DateOnly(2027, 5, 1)));    // carries forward
    }

    [Fact]
    public void Any_day_in_a_month_resolves_to_that_month()
    {
        var line = Ymca((2026, 10, 51m));

        Assert.Equal(51m, line.AmountFor(new DateOnly(2026, 10, 27)));
    }

    [Fact]
    public void The_accrual_uses_the_amount_in_force_that_month()
    {
        var line = Ymca((2026, 10, 51m));

        Assert.Equal(50m, SinkingFund.MonthlyAccrual(line, new DateOnly(2026, 9, 1)).Monthly);
        Assert.Equal(51m, SinkingFund.MonthlyAccrual(line, new DateOnly(2026, 10, 1)).Monthly);
    }

    [Fact]
    public void A_single_months_override_still_beats_the_schedule()
    {
        // An odd month — a double charge, a credit — is not a price change and stays an override.
        var line = Ymca((2026, 10, 51m));

        var odd = SinkingFund.MonthlyAccrual(line, new DateOnly(2026, 10, 1), projectedThisMonth: 102m);

        Assert.Equal(102m, odd.Monthly);
        Assert.Contains("51.00", odd.Formula);   // and it says what the usual amount was
    }

    [Fact]
    public void Successive_rises_each_apply_from_their_own_month()
    {
        var line = Ymca((2026, 10, 51m), (2027, 4, 55m));

        Assert.Equal(51m, line.AmountFor(new DateOnly(2027, 3, 1)));
        Assert.Equal(55m, line.AmountFor(new DateOnly(2027, 4, 1)));
    }
}

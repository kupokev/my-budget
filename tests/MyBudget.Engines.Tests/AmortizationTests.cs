using MyBudget.Engines.Amortization;
using Amort = MyBudget.Engines.Amortization.Amortization;

namespace MyBudget.Engines.Tests;

public class AmortizationTests
{
    [Fact]
    public void Thirty_year_mortgage_payment_matches_the_textbook_figure()
    {
        // $300,000 at 6.5% for 360 months: the widely published payment is $1,896.20.
        var (payment, formula) = Amort.Payment(300_000m, 0.065m, 360);
        Assert.Equal(1_896.20m, payment);
        Assert.Contains("n = 360", formula);
    }

    [Fact]
    public void Schedule_pays_off_on_the_last_scheduled_month_with_expected_total_interest()
    {
        var s = Amort.Project(300_000m, 0.065m, 1_896.20m, new(2026, 1, 1));
        Assert.Equal(360, s.Rows.Count);
        Assert.Equal(new DateOnly(2055, 12, 1), s.PayoffDate);
        Assert.Equal(0m, s.Rows[^1].Balance);
        Assert.Equal(1_625.00m, s.Rows[0].Interest);   // 300,000 × 6.5% / 12
        Assert.Equal(271.20m, s.Rows[0].Principal);
        Assert.InRange(s.TotalInterest, 382_000m, 383_000m); // ≈ $382,633
    }

    [Fact]
    public void Extra_principal_shortens_the_loan_and_reports_the_savings()
    {
        var s = Amort.Project(300_000m, 0.065m, 1_896.20m, new(2026, 1, 1), extraMonthly: 200m);
        Assert.True(s.Rows.Count < 360);
        Assert.NotNull(s.MonthsSavedByExtra);
        Assert.InRange(s.MonthsSavedByExtra!.Value, 80, 90);       // ≈ 7 years
        Assert.InRange(s.InterestSavedByExtra!.Value, 95_000m, 110_000m);
        Assert.Equal(200m, s.Rows[0].Extra);
    }

    [Fact]
    public void Zero_rate_loan_is_a_straight_split()
    {
        var (payment, _) = Amort.Payment(12_000m, 0m, 24);
        Assert.Equal(500m, payment);
        var s = Amort.Project(12_000m, 0m, 500m, new(2026, 1, 1));
        Assert.Equal(24, s.Rows.Count);
        Assert.Equal(0m, s.TotalInterest);
    }

    [Fact]
    public void Payment_that_does_not_cover_interest_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => Amort.Project(50_000m, 0.08m, 100m, new(2026, 1, 1)));
    }

    [Fact]
    public void Scheduled_balance_after_n_payments_matches_the_schedule_row()
    {
        var s = Amort.Project(300_000m, 0.065m, 1_896.20m, new(2026, 1, 1));
        Assert.Equal(s.Rows[11].Balance, Amort.ScheduledBalanceAfter(300_000m, 0.065m, 1_896.20m, 12));
    }
}

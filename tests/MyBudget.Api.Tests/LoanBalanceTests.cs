using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A loan's balance is the last one recorded against it, because the lender is the authority and extra
/// payments move the real number off any schedule. One rule, so the loans list, the Wealth panels and
/// the net-worth report cannot disagree — which they did: the report fell back to the original
/// principal while the loans table showed a dash for the same loan.
/// </summary>
public class LoanBalanceTests
{
    private static Loan Mortgage(params (int Year, int Month, decimal Balance)[] recorded)
    {
        var loan = new Loan { Id = 1, Name = "Mortgage", OriginalPrincipal = 178_700m, StartDate = new(2024, 1, 1) };
        foreach (var (y, m, b) in recorded)
            loan.Balances.Add(new LoanBalance { LoanId = 1, AsOf = new DateOnly(y, m, 1), Balance = b });
        return loan;
    }

    [Fact]
    public void With_nothing_recorded_the_original_principal_stands_in_and_is_flagged()
    {
        var current = LoanBalanceMath.Of(Mortgage(), new DateOnly(2026, 9, 27));

        Assert.Equal(178_700m, current.Balance);
        Assert.Null(current.AsOf);
        Assert.True(current.IsEstimate);
    }

    [Fact]
    public void The_most_recent_recorded_balance_wins()
    {
        var current = LoanBalanceMath.Of(Mortgage((2026, 1, 175_000m), (2026, 6, 170_000m)), new DateOnly(2026, 9, 27));

        Assert.Equal(170_000m, current.Balance);
        Assert.Equal(new DateOnly(2026, 6, 1), current.AsOf);
        Assert.False(current.IsEstimate);
    }

    [Fact]
    public void A_balance_recorded_after_the_date_asked_about_is_ignored()
    {
        // The net-worth chart walks backwards through months, so it must not see the future.
        var current = LoanBalanceMath.Of(Mortgage((2026, 1, 175_000m), (2026, 6, 170_000m)), new DateOnly(2026, 3, 1));

        Assert.Equal(175_000m, current.Balance);
        Assert.Equal(new DateOnly(2026, 1, 1), current.AsOf);
        Assert.False(current.IsEstimate);
    }
}

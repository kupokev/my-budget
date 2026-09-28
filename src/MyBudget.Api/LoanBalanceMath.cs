using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>
/// What a loan is worth owing right now. A loan's balance is whatever was last recorded against it —
/// the lender is the authority, not a schedule, because extra payments and timing move the real
/// number. Until a balance has been recorded there is nothing better than the original principal.
///
/// One place computes it so the Wealth panels, the loan list and the net-worth report cannot disagree
/// — which they did: the report fell back to the principal while the loans table showed a dash.
/// </summary>
public static class LoanBalanceMath
{
    public readonly record struct Current(decimal Balance, DateOnly? AsOf, bool IsEstimate);

    public static Current Of(Loan loan, DateOnly asOf)
    {
        var recorded = loan.Balances
            .Where(b => b.AsOf <= asOf)
            .OrderByDescending(b => b.AsOf)
            .FirstOrDefault();

        return recorded is null
            ? new Current(loan.OriginalPrincipal, null, IsEstimate: true)
            : new Current(recorded.Balance, recorded.AsOf, IsEstimate: false);
    }
}

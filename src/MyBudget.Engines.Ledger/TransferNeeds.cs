using MyBudget.Domain;

namespace MyBudget.Engines.Ledger;

/// <summary>
/// Required transfer per funding account, monthly and per paycheck (ACC-2). A card-paid line counts
/// toward its funding account, never the card or the card's paying account (ACC-2a).
/// </summary>
public static class TransferNeeds
{
    public sealed record Line(int BudgetLineId, string LineName, BudgetFrequency Frequency, decimal ProjectedAmount, decimal MonthlyAccrual, string Formula, bool PaidByCard);

    public sealed record AccountNeed(int AccountId, decimal Monthly, decimal PerPaycheck, string PerPaycheckFormula, IReadOnlyList<Line> Lines);

    /// <param name="projectedThisMonth">Per-line projected overrides for asOf's month, keyed by line id.</param>
    public static IReadOnlyList<AccountNeed> Compute(IEnumerable<BudgetLine> lines, int paychecksPerYear, DateOnly asOf,
        IReadOnlyDictionary<int, decimal>? projectedThisMonth = null)
    {
        if (paychecksPerYear <= 0) throw new ArgumentOutOfRangeException(nameof(paychecksPerYear));

        return lines
            .Where(b => b.IsActive && (b.StartDate is null || b.StartDate <= asOf) && (b.EndDate is null || b.EndDate >= asOf))
            .GroupBy(b => b.FundingAccountId)
            .Select(g =>
            {
                var lines = g.Select(b =>
                {
                    var a = SinkingFund.MonthlyAccrual(b, asOf, projectedThisMonth is not null && projectedThisMonth.TryGetValue(b.Id, out var o) ? o : null);
                    return new Line(b.Id, b.Name, b.Frequency, b.ProjectedAmount, a.Monthly, a.Formula, b.PaymentMethod == PaymentMethodKind.Card);
                }).OrderBy(l => l.LineName).ToList();
                var monthly = SinkingFund.Round(lines.Sum(l => l.MonthlyAccrual));
                var perCheck = SinkingFund.Round(monthly * 12m / paychecksPerYear);
                return new AccountNeed(g.Key, monthly, perCheck, $"{monthly:C} × 12 ÷ {paychecksPerYear} checks", lines);
            })
            .OrderBy(n => n.AccountId)
            .ToList();
    }
}

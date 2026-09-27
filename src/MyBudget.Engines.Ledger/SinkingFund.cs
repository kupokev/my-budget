using MyBudget.Domain;

namespace MyBudget.Engines.Ledger;

/// <summary>Monthly set-aside for a non-monthly line (BIL-4). Auditable: returns the formula alongside the number.</summary>
public static class SinkingFund
{
    public sealed record Accrual(decimal Monthly, string Formula);

    /// <param name="projectedThisMonth">A per-month projected override for asOf's month; applies to monthly lines only.</param>
    public static Accrual MonthlyAccrual(BudgetLine line, DateOnly asOf, decimal? projectedThisMonth = null)
    {
        var amount = line.ProjectedAmount;
        switch (line.Frequency)
        {
            case BudgetFrequency.Monthly:
                if (projectedThisMonth is { } o)
                    return new(Round(o), $"{o:C} projected for {asOf:MMMM yyyy} (default {amount:C})");
                return new(Round(amount), $"{amount:C} monthly");
            case BudgetFrequency.Variable:
                if (projectedThisMonth is { } v)
                    return new(Round(v), $"{v:C} planned for {asOf:MMMM yyyy} (default {amount:C})");
                return new(Round(amount), $"{amount:C} planned per month");
            case BudgetFrequency.OneOff:
            {
                if (line.AnchorDueDate is not { } due || due <= asOf)
                    return new(0m, "one-off, already due or no due date: nothing to accrue");
                var months = Math.Max(1, MonthsUntil(asOf, due));
                return new(Round(amount / months), $"{amount:C} ÷ {months} months until {due:yyyy-MM-dd}");
            }
            default:
            {
                var perYear = BudgetDueDates.OccurrencesPerYear(line.Frequency);
                return new(Round(amount * perYear / 12m), $"{amount:C} × {perYear}/yr ÷ 12");
            }
        }
    }

    /// <summary>Whole months from the first of asOf's month to the first of due's month.</summary>
    public static int MonthsUntil(DateOnly asOf, DateOnly due)
        => (due.Year - asOf.Year) * 12 + (due.Month - asOf.Month);

    internal static decimal Round(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

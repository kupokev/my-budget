using MyBudget.Domain;

namespace MyBudget.Engines.Ledger;

/// <summary>When a line falls due within a window, honoring frequency, anchor date, start/end and active flag.</summary>
public static class BudgetDueDates
{
    public static int OccurrencesPerYear(BudgetFrequency f) => f switch
    {
        BudgetFrequency.Monthly => 12,
        BudgetFrequency.Quarterly => 4,
        BudgetFrequency.SemiAnnual => 2,
        BudgetFrequency.Annual => 1,
        BudgetFrequency.OneOff => 0,
        BudgetFrequency.Variable => 0,   // no due date at all; the amount is spread across the month
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <param name="dueOverrides">Per-month due-date overrides keyed by period (first of month). A month's
    /// generated date is replaced by its override, which may move it into or out of the window.</param>
    public static IReadOnlyList<DateOnly> Between(BudgetLine line, DateOnly from, DateOnly to, IReadOnlyDictionary<DateOnly, DateOnly>? dueOverrides = null)
    {
        if (dueOverrides is { Count: > 0 })
        {
            // Generate a month wider each side so an override that shifts a date across the window edge is seen.
            var wide = Between(line, Dates.FirstOfMonth(from).AddMonths(-1), to.AddMonths(1));
            return wide
                .Select(d => dueOverrides.TryGetValue(Dates.FirstOfMonth(d), out var o) ? o : d)
                .Where(d => d >= from && d <= to)
                .Distinct().OrderBy(d => d).ToList();
        }

        if (!line.IsActive || to < from) return [];
        if (line.Frequency == BudgetFrequency.Variable) return [];   // planned spend, not a dated obligation
        var lo = line.StartDate is { } s && s > from ? s : from;
        var hi = line.EndDate is { } e && e < to ? e : to;
        if (hi < lo) return [];

        var dates = new List<DateOnly>();
        switch (line.Frequency)
        {
            case BudgetFrequency.Monthly:
                for (var m = Dates.FirstOfMonth(lo); m <= hi; m = m.AddMonths(1))
                    dates.Add(Dates.OnDay(m.Year, m.Month, line.DueDay));
                break;
            case BudgetFrequency.OneOff:
                if (line.AnchorDueDate is { } once) dates.Add(once);
                break;
            default:
            {
                if (line.AnchorDueDate is not { } anchor)
                    throw new InvalidOperationException($"Budget line '{line.Name}' is {line.Frequency} and needs an AnchorDueDate.");
                var step = 12 / OccurrencesPerYear(line.Frequency);
                var d = anchor;
                while (d > lo) d = d.AddMonths(-step);
                for (; d <= hi; d = d.AddMonths(step))
                    dates.Add(Dates.OnDay(d.Year, d.Month, line.DueDay > 0 ? line.DueDay : anchor.Day));
                break;
            }
        }
        return dates.Where(d => d >= lo && d <= hi).OrderBy(d => d).ToList();
    }
}

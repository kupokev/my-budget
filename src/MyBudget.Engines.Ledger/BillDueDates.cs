using MyBudget.Domain;

namespace MyBudget.Engines.Ledger;

/// <summary>When a bill falls due within a window, honoring frequency, anchor date, start/end and active flag.</summary>
public static class BillDueDates
{
    public static int OccurrencesPerYear(BillFrequency f) => f switch
    {
        BillFrequency.Monthly => 12,
        BillFrequency.Quarterly => 4,
        BillFrequency.SemiAnnual => 2,
        BillFrequency.Annual => 1,
        BillFrequency.OneOff => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <param name="dueOverrides">Per-month due-date overrides keyed by period (first of month). A month's
    /// generated date is replaced by its override, which may move it into or out of the window.</param>
    public static IReadOnlyList<DateOnly> Between(Bill bill, DateOnly from, DateOnly to, IReadOnlyDictionary<DateOnly, DateOnly>? dueOverrides = null)
    {
        if (dueOverrides is { Count: > 0 })
        {
            // Generate a month wider each side so an override that shifts a date across the window edge is seen.
            var wide = Between(bill, Dates.FirstOfMonth(from).AddMonths(-1), to.AddMonths(1));
            return wide
                .Select(d => dueOverrides.TryGetValue(Dates.FirstOfMonth(d), out var o) ? o : d)
                .Where(d => d >= from && d <= to)
                .Distinct().OrderBy(d => d).ToList();
        }

        if (!bill.IsActive || to < from) return [];
        var lo = bill.StartDate is { } s && s > from ? s : from;
        var hi = bill.EndDate is { } e && e < to ? e : to;
        if (hi < lo) return [];

        var dates = new List<DateOnly>();
        switch (bill.Frequency)
        {
            case BillFrequency.Monthly:
                for (var m = Dates.FirstOfMonth(lo); m <= hi; m = m.AddMonths(1))
                    dates.Add(Dates.OnDay(m.Year, m.Month, bill.DueDay));
                break;
            case BillFrequency.OneOff:
                if (bill.AnchorDueDate is { } once) dates.Add(once);
                break;
            default:
            {
                if (bill.AnchorDueDate is not { } anchor)
                    throw new InvalidOperationException($"Bill '{bill.Name}' is {bill.Frequency} and needs an AnchorDueDate.");
                var step = 12 / OccurrencesPerYear(bill.Frequency);
                var d = anchor;
                while (d > lo) d = d.AddMonths(-step);
                for (; d <= hi; d = d.AddMonths(step))
                    dates.Add(Dates.OnDay(d.Year, d.Month, bill.DueDay > 0 ? bill.DueDay : anchor.Day));
                break;
            }
        }
        return dates.Where(d => d >= lo && d <= hi).OrderBy(d => d).ToList();
    }
}

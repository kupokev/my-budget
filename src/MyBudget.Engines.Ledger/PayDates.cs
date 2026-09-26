using MyBudget.Domain;

namespace MyBudget.Engines.Ledger;

/// <summary>Generates actual pay dates from effective-dated schedules (INC-3) and finds 3-paycheck months (INC-4).</summary>
public static class PayDates
{
    public static int PaychecksPerYear(PayFrequency f) => f switch
    {
        PayFrequency.BiWeekly => 26,
        PayFrequency.SemiMonthly => 24,
        PayFrequency.Monthly => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <summary>
    /// Pay dates in [from, to], inclusive. On any given date the schedule in effect is the one with
    /// the latest EffectiveDate on or before it; a schedule contributes only dates it is in effect for.
    /// </summary>
    public static IReadOnlyList<DateOnly> Generate(IEnumerable<PaySchedule> schedules, DateOnly from, DateOnly to)
    {
        var ordered = schedules.OrderBy(s => s.EffectiveDate).ToList();
        if (ordered.Count == 0 || to < from) return [];

        var result = new SortedSet<DateOnly>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var start = s.EffectiveDate > from ? s.EffectiveDate : from;
            var end = i + 1 < ordered.Count ? ordered[i + 1].EffectiveDate.AddDays(-1) : to;
            if (end > to) end = to;
            if (end < start) continue;

            foreach (var d in Raw(s, start, end))
            {
                var adjusted = s.PayOnPriorBusinessDay ? Dates.PriorBusinessDay(d) : d;
                if (adjusted >= start && adjusted <= end) result.Add(adjusted);
            }
        }
        return result.ToList();
    }

    /// <summary>Months in which three checks land, with the dates, for a bi-weekly schedule (INC-4).</summary>
    public static IReadOnlyList<(int Year, int Month, IReadOnlyList<DateOnly> Dates)> ThreePaycheckMonths(IEnumerable<DateOnly> payDates)
        => payDates
            .GroupBy(d => (d.Year, d.Month))
            .Where(g => g.Count() >= 3)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key.Year, g.Key.Month, (IReadOnlyList<DateOnly>)g.OrderBy(d => d).ToList()))
            .ToList();

    private static IEnumerable<DateOnly> Raw(PaySchedule s, DateOnly start, DateOnly end)
    {
        switch (s.Frequency)
        {
            case PayFrequency.BiWeekly:
            {
                // Walk from the anchor in 14-day steps to just before the window, then forward through it.
                // Widen by a week each side so weekend shifts can't drop an edge date.
                var d = s.AnchorPayDate;
                while (d > start.AddDays(-7)) d = d.AddDays(-14);
                while (d < start.AddDays(-7)) d = d.AddDays(14);
                for (; d <= end.AddDays(7); d = d.AddDays(14)) yield return d;
                break;
            }
            case PayFrequency.Monthly:
            {
                for (var m = Dates.FirstOfMonth(start).AddMonths(-1); m <= end.AddMonths(1); m = m.AddMonths(1))
                    yield return Dates.OnDay(m.Year, m.Month, s.AnchorPayDate.Day);
                break;
            }
            case PayFrequency.SemiMonthly:
            {
                var first = s.FirstPayDay ?? throw new InvalidOperationException("Semi-monthly schedule needs FirstPayDay.");
                var second = s.SecondPayDay ?? throw new InvalidOperationException("Semi-monthly schedule needs SecondPayDay.");
                for (var m = Dates.FirstOfMonth(start).AddMonths(-1); m <= end.AddMonths(1); m = m.AddMonths(1))
                {
                    yield return Dates.OnDay(m.Year, m.Month, first);
                    yield return Dates.OnDay(m.Year, m.Month, second);
                }
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(s.Frequency));
        }
    }
}

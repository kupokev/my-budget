using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>
/// "What changed this month" on the dashboard: a few sentences the app writes itself from numbers it
/// has already worked out, each carrying the working behind it. The same lines are the fact sheet the
/// optional AI note is written from, so the model never does the arithmetic and never sees raw tables.
///
/// Rewards are deliberately absent: Home is a financial-health snapshot, and rewards live on Cards.
/// </summary>
public static class MonthHighlights
{
    /// <summary>Lines over plan worth listing individually; the rest are summed into one line.</summary>
    private const int MaxOverLines = 3;

    /// <param name="lines">Active budget lines, for when they start and end and what they cost a month.</param>
    /// <param name="timeOff">Goals needing time off, each checked against its bucket on the day it starts.</param>
    public static List<HighlightDto> Build(DateOnly today, IReadOnlyList<BudgetHistoryDto> grid, IReadOnlyList<BudgetLineDto> lines,
        HsaPlanDto? hsa, IReadOnlyList<GoalProgressDto> goals, decimal netWorth, decimal? netWorthChange,
        IReadOnlyList<TimeOffGoalCheckDto>? timeOff = null)
    {
        var month = today.ToString("MMMM");
        var rows = grid.Select(l => (l.LineName, Month: l.Months.Single(m => m.Period.Month == today.Month))).ToList();
        var list = new List<HighlightDto>();

        // Lines over plan, biggest first. A line paid with nothing planned this month is over by all of it.
        var over = rows.Where(r => r.Month.Actual is { } a && a > r.Month.Projected)
            .Select(r => (r.LineName, Actual: r.Month.Actual!.Value, r.Month.Projected, Over: r.Month.Actual!.Value - r.Month.Projected))
            .OrderByDescending(r => r.Over).ToList();
        foreach (var r in over.Take(MaxOverLines))
            list.Add(new(r.Projected == 0
                    ? $"{r.LineName}: {Money(r.Actual)} paid with nothing planned for {month}."
                    : $"{r.LineName} came to {Money(r.Actual)} against {Money(r.Projected)} planned, {Money(r.Over)} over.",
                $"{Money(r.Actual)} actual − {Money(r.Projected)} projected for {month} = {Money(r.Over)}", "bad", "budget"));
        if (over.Count > MaxOverLines)
        {
            var rest = over.Skip(MaxOverLines).ToList();
            list.Add(new($"{rest.Count} more {(rest.Count == 1 ? "line is" : "lines are")} over plan by {Money(rest.Sum(r => r.Over))} between them.",
                string.Join("; ", rest.Select(r => $"{r.LineName} {Money(r.Over)}")), "bad", "budget"));
        }

        // Where the month stands: paid against planned, and what is still to go out.
        var planned = rows.Sum(r => r.Month.Projected);
        var paid = rows.Sum(r => r.Month.Actual ?? 0);
        var toGo = rows.Where(r => r.Month.Actual is null && r.Month.PaidOn is null).Sum(r => r.Month.Projected);
        if (planned > 0 || paid > 0)
            list.Add(new($"Paid so far in {month}: {Money(paid)} of {Money(planned)} planned, with {Money(toGo)} still to go.",
                $"Paid = sum of actuals entered for {month}; planned = sum of every line's projected amount; " +
                "still to go = projected amount of lines with no actual and no paid-on date", "neutral", "budget"));

        // A goal counting HSA contributions is the HSA line said again; its shortfall joins that line
        // instead of being a second one, which read as two problems where there is one.
        var hsaGoalBehind = hsa is { Target: > 0 }
            ? goals.FirstOrDefault(g => g.StatusText == "Not On Track" && g.Goal.Metric == GoalMetric.AccountTypeContributions && g.Goal.AccountType == AccountType.Hsa)
            : null;

        // Lines making their last payment this month free that money from next month; lines starting
        // next month take some. Only a line actually due this month counts as ending here, so one
        // whose end date passed without a payment isn't announced as good news.
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var nextStart = monthStart.AddMonths(1);
        var next = nextStart.ToString("MMMM");
        var dueThisMonth = grid.Where(l => l.Months.Single(m => m.Period.Month == today.Month) is var m && (m.Projected > 0 || m.Actual > 0))
            .Select(l => l.BudgetLineId).ToHashSet();
        var ending = lines.Where(l => l.EndDate is { } e && e >= monthStart && e < nextStart && dueThisMonth.Contains(l.Id))
            .OrderByDescending(l => l.MonthlyAccrual).ToList();
        foreach (var l in ending)
            list.Add(new($"{l.Name} ends with this month's payment, freeing {Money(l.MonthlyAccrual)} a month from {next}.",
                $"Last month owed {l.EndDate:MMM d, yyyy}; {l.MonthlyAccrualFormula ?? Money(l.MonthlyAccrual) + " a month"}", "good", "budget"));
        foreach (var l in lines.Where(l => l.StartDate is { } s && s >= nextStart && s < nextStart.AddMonths(1)).OrderByDescending(l => l.MonthlyAccrual))
            list.Add(new($"{l.Name} starts in {next}: {Money(l.ProjectedAmount)}{(l.Frequency == BudgetFrequency.Monthly ? " a month" : "")}.",
                $"Starts {l.StartDate:MMM d, yyyy}; {l.MonthlyAccrualFormula ?? Money(l.MonthlyAccrual) + " a month"}", "neutral", "budget"));

        // HSA pace toward its target for the year.
        if (hsa is { Target: > 0 })
        {
            if (hsa.OverContributed)
                list.Add(new($"HSA is {Money(hsa.OverBy)} over this year's limit.", string.Join(" ", hsa.Steps), "bad", "hsa"));
            else if (hsa.RemainingToTarget <= 0)
                list.Add(new($"HSA has reached this year's target of {Money(hsa.Target)}.", string.Join(" ", hsa.Steps), "good", "hsa"));
            else if (hsa.MonthsLeft > 0)
                list.Add(new($"HSA: {Money(hsa.Contributed)} of {Money(hsa.Target)} in" +
                             (hsaGoalBehind is { } hg ? $", {Money(hg.MissingAmount)} behind pace" : "") +
                             $". {Money(hsa.RecommendedMonthly)} a month reaches it by December.",
                    $"({Money(hsa.Target)} − {Money(hsa.Contributed)}) ÷ {hsa.MonthsLeft} months left = {Money(hsa.RecommendedMonthly)}" +
                    (hsaGoalBehind is { } g2 ? $". Behind pace: {g2.Formula}" : ""),
                    hsaGoalBehind is null ? "neutral" : "bad", "hsa"));
        }

        // Money freed from next month set against what the HSA needs from next month. Stated here
        // rather than left to the AI note, which reasoned it backwards. The freed money only starts
        // next month, so the HSA's need is re-spread over the months from then — not the monthly
        // figure above, which counts this month too.
        var freed = ending.Sum(l => l.MonthlyAccrual);
        var monthsFromNext = hsa is null ? 0 : hsa.MonthsLeft - 1;
        if (freed > 0 && hsa is { Target: > 0, OverContributed: false, RemainingToTarget: > 0 } && monthsFromNext > 0)
        {
            var need = Math.Round(hsa.RemainingToTarget / monthsFromNext, 2);
            var by = string.Join(" and ", ending.Select(l => l.Name));
            var last = nextStart.AddMonths(monthsFromNext - 1).ToString("MMMM");
            var months = monthsFromNext switch { 1 => next, 2 => $"{next} and {last}", _ => $"{next} through {last}" };
            var spare = freed - need;
            list.Add(new(spare >= 0
                    ? $"From {next}, the {Money(freed)} a month freed by {by} covers the HSA: its {Money(hsa.RemainingToTarget)} still to go is {Money(need)} a month over {months}, leaving {Money(spare)} a month to spare."
                    : $"From {next}, the {Money(freed)} a month freed by {by} covers most of the HSA's {Money(need)} a month over {months}, leaving {Money(-spare)} a month to find.",
                $"{Money(hsa.RemainingToTarget)} ÷ {monthsFromNext} = {Money(need)} a month; {Money(freed)} freed − {Money(need)} = {Money(spare)}",
                spare >= 0 ? "good" : "neutral", "hsa"));
        }

        // Goals behind where they should be by now.
        foreach (var g in goals.Where(g => g.StatusText == "Not On Track" && g != hsaGoalBehind).OrderByDescending(g => g.MissingAmount))
            list.Add(new($"{g.Goal.Name} is behind: {Money(g.MissingAmount)} short of where it should be by now.", g.Formula, "bad", "goals"));

        // Time off a goal needs, against what its bucket will hold by then. Enough means it can be booked
        // now; short says by how much, while there's still time to plan around it.
        foreach (var t in timeOff ?? [])
        {
            string Days(decimal h) => $"{h:0.##}h ({h / t.HoursPerDay:0.#} days)";
            list.Add(t.ProjectedHours switch
            {
                null => new($"{t.Goal} needs {Days(t.HoursNeeded)} of {t.Bucket}, but no pay stub has recorded that balance yet.", t.Formula, "neutral", "paycheck"),
                var h when t.Enough => new($"{t.Goal}: {t.Bucket} will be {Days(h.Value)} by {t.Starts:MMM d}, enough for the {Days(t.HoursNeeded)} it needs. You can book the time off.",
                    t.Formula, "good", "goals"),
                var h => new($"{t.Goal}: {t.Bucket} will be {Days(h.Value)} by {t.Starts:MMM d}, {Days(t.HoursNeeded - h.Value)} short of the {Days(t.HoursNeeded)} it needs.",
                    t.Formula, "bad", "goals"),
            });
        }

        // Net worth against the previous month's reading.
        list.Add(netWorthChange switch
        {
            null => new($"Net worth is {Money(netWorth)}.", "Owned − owed", "neutral", "wealth"),
            0 => new($"Net worth is {Money(netWorth)}, unchanged since last month.", "Owned − owed, against last month's reading", "neutral", "wealth"),
            var c => new($"Net worth is {Money(netWorth)}, {(c > 0 ? "up" : "down")} {Money(Math.Abs(c.Value))} since last month.",
                "Owned − owed, against last month's reading", c > 0 ? "good" : "bad", "wealth"),
        });

        return list;
    }

    private static string Money(decimal d) => d.ToString("C");
}

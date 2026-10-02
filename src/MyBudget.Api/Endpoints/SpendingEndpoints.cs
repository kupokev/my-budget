using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

/// <summary>RPT-0 dashboard, BIL-8 month-over-month, BIL-9 drill-down. Spending = money out, excluding transfers and card payments.</summary>
public static class SpendingEndpoints
{
    public static RouteGroupBuilder MapSpending(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/spending");

        g.MapGet("/summary", async (int? year, int? month, BudgetDbContext db, TimeProvider clock) =>
        {
            var today = clock.GetLocalNow();
            return await Summary(db, year ?? today.Year, month ?? today.Month);
        });

        // The dashboard's "spending vs last month" curve.
        g.MapGet("/cumulative", async (int? year, int? month, BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var y = year ?? today.Year;
            var m = month ?? today.Month;
            return await Cumulative(db, y, m, today);
        });

        g.MapGet("/matrix", async (int? year, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            var lines = await db.Transactions.Include(t => t.Category).Where(t => !t.IsTransfer && t.Amount < 0 && t.Date.Year == y).ToListAsync();
            var rows = lines.GroupBy(t => (t.CategoryId, Name: t.Category?.Name ?? "Uncategorized")).Select(g =>
            {
                var months = Enumerable.Range(1, 12).Select(m => R(-g.Where(t => t.Date.Month == m).Sum(t => t.Amount))).ToList();
                return new SpendingRowDto(g.Key.CategoryId, g.Key.Name, months, months.Sum());
            }).OrderByDescending(r => r.Total).ToList();
            var monthTotals = Enumerable.Range(0, 12).Select(i => rows.Sum(r => r.Months[i])).ToList();
            return new SpendingMatrixDto(y, rows, monthTotals, monthTotals.Sum());
        });

        // categoryId 0 = uncategorized
        g.MapGet("/category/{categoryId:int}", async (int categoryId, int? year, int? month, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            int? cat = categoryId == 0 ? null : categoryId;
            var q = TransactionEndpoints.Query(db).Where(t => !t.IsTransfer && t.Amount < 0 && t.Date.Year == y && t.CategoryId == cat);
            if (month is { } m) q = q.Where(t => t.Date.Month == m);
            var lines = await q.OrderByDescending(t => t.Date).ToListAsync();
            var name = cat is null ? "Uncategorized" : (await db.Categories.FindAsync(cat))?.Name ?? "?";
            var merchants = lines.GroupBy(t => t.Merchant ?? t.Description).Select(g => new MerchantTotalDto(g.Key, g.Count(), R(-g.Sum(t => t.Amount)))).OrderByDescending(x => x.Total).ToList();
            return new CategoryDrilldownDto(cat, name, y, month, R(-lines.Sum(t => t.Amount)), merchants, lines.Select(TransactionEndpoints.ToDto).ToList());
        });

        return api;
    }


    internal static async Task<SpendingSummaryDto> Summary(BudgetDbContext db, int y, int m)
    {
        var thisStart = new DateOnly(y, m, 1); var thisEnd = thisStart.AddMonths(1);
        var lastStart = thisStart.AddMonths(-1);
        var yearStart = new DateOnly(y, 1, 1);
        var lines = await db.Transactions.Include(t => t.Category).Where(t => !t.IsTransfer && t.Date >= yearStart.AddMonths(-1) && t.Date < thisEnd).ToListAsync();
        var spend = lines.Where(t => t.Amount < 0).ToList();
        var monthsElapsed = Math.Max(1, m);

        var cats = spend.GroupBy(t => (t.CategoryId, Name: t.Category?.Name ?? "Uncategorized")).Select(g =>
        {
            var thisM = -g.Where(t => t.Date >= thisStart && t.Date < thisEnd).Sum(t => t.Amount);
            var lastM = -g.Where(t => t.Date >= lastStart && t.Date < thisStart).Sum(t => t.Amount);
            var ytd = -g.Where(t => t.Date >= yearStart && t.Date < thisEnd).Sum(t => t.Amount);
            return new SpendingCategoryDto(g.Key.CategoryId, g.Key.Name, R(thisM), R(lastM), R(thisM - lastM), R(ytd), R(ytd / monthsElapsed), g.Count(t => t.Date >= thisStart && t.Date < thisEnd));
        }).OrderByDescending(c => c.ThisMonth).ThenByDescending(c => c.YearToDate).ToList();

        var thisTotal = cats.Sum(c => c.ThisMonth); var lastTotal = cats.Sum(c => c.LastMonth);
        var uncategorized = spend.Where(t => t.CategoryId == null && t.Date >= thisStart && t.Date < thisEnd).ToList();
        var income = lines.Where(t => t.Amount > 0 && t.Date >= thisStart && t.Date < thisEnd).Sum(t => t.Amount);
        return new SpendingSummaryDto(y, m, R(thisTotal), R(lastTotal), R(thisTotal - lastTotal), R(cats.Sum(c => c.YearToDate)), cats, uncategorized.Count, R(-uncategorized.Sum(t => t.Amount)), R(income));
    }

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// What the Budget grid records as paid, run up day by day, for the named month and the one before
    /// it. It reads the grid rather than imported transactions: the grid is where a bill is marked paid,
    /// and transactions only cover the accounts that have had a statement imported.
    ///
    /// Each month's actual lands on the day it was paid: its paid-on date, else the day it was due that
    /// month, else (a Variable line, which has no due date) the last day so far — today in the current
    /// month, month end in a past one. A date outside its month is pulled to the month's edge, so
    /// September's bill paid on August 30 still counts in September. The current month stops at today.
    /// </summary>
    internal static async Task<CumulativeSpendDto> Cumulative(BudgetDbContext db, int year, int month, DateOnly today)
    {
        var start = new DateOnly(year, month, 1);
        var priorStart = start.AddMonths(-1);
        var end = start.AddMonths(1);
        var isCurrent = today.Year == year && today.Month == month;

        var periods = await db.BudgetPeriods.Include(p => p.BudgetLine).ThenInclude(b => b!.Periods)
            .Where(p => p.ActualAmount != null && p.Period >= priorStart && p.Period < end)
            .ToListAsync();

        var rows = periods.Select(p =>
        {
            var monthEnd = p.Period.AddMonths(1).AddDays(-1);
            var lastDay = isCurrent && p.Period == start ? today : monthEnd;
            var due = BudgetDueDates.Between(p.BudgetLine!, p.Period, monthEnd, BudgetEndpoints.DueOverrides(p.BudgetLine!)).Cast<DateOnly?>().FirstOrDefault();
            var day = p.PaidOn ?? due ?? lastDay;
            day = day < p.Period ? p.Period : day > lastDay ? lastDay : day;
            return new { Date = day, Amount = -p.ActualAmount!.Value };
        }).ToList();

        static decimal[] Daily(IEnumerable<(DateOnly Date, decimal Amount)> src, DateOnly monthStart)
        {
            var days = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var running = new decimal[days];
            var byDay = new decimal[days];
            foreach (var r in src) byDay[r.Date.Day - 1] += -r.Amount;
            decimal total = 0;
            for (var i = 0; i < days; i++) { total += byDay[i]; running[i] = Math.Round(total, 2); }
            return running;
        }

        var thisSeries = Daily(rows.Where(r => r.Date >= start).Select(r => (r.Date, r.Amount)), start);
        var lastSeries = Daily(rows.Where(r => r.Date < start).Select(r => (r.Date, r.Amount)), priorStart);

        // The axis is as long as the longer of the two months.
        var span = Math.Max(thisSeries.Length, lastSeries.Length);
        var labels = Enumerable.Range(1, span).Select(d => d.ToString()).ToList();

        // Today caps the current month; a past month is shown whole.
        var upTo = isCurrent ? today.Day : thisSeries.Length;
        var thisOut = thisSeries.Take(upTo).ToList();
        var lastOut = lastSeries.ToList();

        var toDate = thisOut.Count > 0 ? thisOut[^1] : 0m;
        var lastSameDay = lastSeries.Length == 0 ? 0m : lastSeries[Math.Min(upTo, lastSeries.Length) - 1];
        var difference = Math.Round(toDate - lastSameDay, 2);

        var weekStart = (isCurrent ? today : start.AddMonths(1).AddDays(-1)).AddDays(-6);
        var thisWeek = Math.Round(rows.Where(r => r.Date >= weekStart && r.Date >= start).Sum(r => -r.Amount), 2);

        // Two separate facts. Joining them with "bringing" implies the week caused the gap, which
        // reads oddly when the week is empty and isn't true even when it isn't.
        var week = thisWeek == 0
            ? "Nothing marked paid in the last seven days."
            : $"{thisWeek:C} marked paid in the last seven days.";
        var against = difference == 0
            ? "This month is level with the same point last month."
            : $"This month is {Math.Abs(difference):C} {(difference < 0 ? "below" : "above")} where it stood at this point last month.";
        var summary = $"{week} {against}";

        return new CumulativeSpendDto(
            year, month, today, upTo, labels, thisOut, lastOut,
            start.ToString("MMMM"), priorStart.ToString("MMMM"),
            thisWeek, toDate, lastSameDay, difference, summary);
    }
}

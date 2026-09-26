using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

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
            var y = year ?? today.Year; var m = month ?? today.Month;
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

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

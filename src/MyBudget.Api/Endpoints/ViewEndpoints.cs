using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

/// <summary>Computed views: transfer needs (ACC-2/2a/3) and the home screen (HOME-1, Phase 1 slice).</summary>
public static class ViewEndpoints
{
    public static RouteGroupBuilder MapViews(this RouteGroupBuilder api)
    {
        api.MapGet("/transfer-needs", async (DateOnly? asOf, BudgetDbContext db, TimeProvider clock) =>
            await Needs(db, asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));

        api.MapGet("/home", async (BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var upcoming = await BillEndpoints.Upcoming(db, today, 14);
            var calendar = await IncomeEndpoints.PayCalendar(db, today.Year);
            var next = calendar.PayDates.FirstOrDefault(d => d.Date >= today);
            var needs = await Needs(db, today);
            return new HomeDto(today, upcoming, next, needs.Accounts);
        });

        return api;
    }

    internal static async Task<TransferNeedsDto> Needs(BudgetDbContext db, DateOnly asOf)
    {
        var (perYear, source) = await IncomeEndpoints.PaychecksPerYear(db, asOf);
        var bills = await db.Bills.ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);
        var monthStart = new DateOnly(asOf.Year, asOf.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var transferred = await db.Transfers
            .Where(t => t.Date >= monthStart && t.Date <= monthEnd)
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Sum);

        var needs = TransferNeeds.Compute(bills, perYear, asOf);
        var dtos = needs.Select(n =>
        {
            var acct = accounts.GetValueOrDefault(n.AccountId);
            var moved = transferred.GetValueOrDefault(n.AccountId);
            return new AccountNeedDto(
                n.AccountId, acct?.Name ?? $"Account {n.AccountId}", acct?.TransferCadence ?? Domain.TransferCadence.Monthly,
                n.Monthly, n.PerPaycheck, n.PerPaycheckFormula, moved, Math.Round(moved - n.Monthly, 2),
                n.Lines.Select(l => new NeedLineDto(l.BillId, l.BillName, l.Frequency, l.ProjectedAmount, l.MonthlyAccrual, l.Formula, l.PaidByCard)).ToList());
        }).OrderBy(d => d.AccountName).ToList();

        return new TransferNeedsDto(asOf, perYear, source, dtos);
    }
}

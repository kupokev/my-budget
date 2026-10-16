using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

/// <summary>Computed views: transfer needs (ACC-2/2a/3) and the home screen (HOME-1, Phase 1 slice).</summary>
public static class ViewEndpoints
{
    public static RouteGroupBuilder MapViews(this RouteGroupBuilder api)
    {
        api.MapGet("/transfer-needs", async (DateOnly? asOf, BudgetDbContext db, TimeProvider clock) =>
            await Needs(db, asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));

        // Per "Funded from" account, what one month's lines add up to (HOME-1 funding panel).
        api.MapGet("/funding-needs", async (int? year, int? month, BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            if (month is < 1 or > 12) return Results.BadRequest("month must be 1–12");
            return Results.Ok(await FundingMonth(db, new DateOnly(year ?? today.Year, month ?? today.Month, 1)));
        });

        api.MapGet("/home", async (BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var upcoming = await BudgetEndpoints.Upcoming(db, today, 14);
            var calendar = await IncomeEndpoints.PayCalendar(db, today.Year);
            var next = calendar.PayDates.FirstOrDefault(d => d.Date >= today);
            var needs = await Needs(db, today);
            return new HomeDto(today, upcoming, next, needs.Accounts);
        });

        return api;
    }

    /// <summary>
    /// The Budget grid's month, grouped by funding account. A line counts when it falls due in the month
    /// (or is a Variable allowance in range), at its month override or the amount in force; a line with
    /// nothing due still counts when the grid records a payment for it that month.
    /// </summary>
    internal static async Task<FundingMonthDto> FundingMonth(BudgetDbContext db, DateOnly period)
    {
        var start = new DateOnly(period.Year, period.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var lines = await db.BudgetLines.Include(b => b.Periods).Include(b => b.Amounts).Include(b => b.PaymentAccount).Include(b => b.PaymentCard)
            .Where(b => b.IsActive).ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);
        var steady = TransferNeeds.Compute(lines, 12, start, BudgetEndpoints.ProjectedOverrides(lines, start)).ToDictionary(n => n.AccountId, n => n.Monthly);

        var byAccount = lines.Select(b =>
            {
                var generated = BudgetDueDates.Between(b, start, end, BudgetEndpoints.DueOverrides(b));
                var row = b.Periods.FirstOrDefault(p => p.Period == start);
                var applies = generated.Count > 0
                              || (b.Frequency == BudgetFrequency.Variable && (b.StartDate is null || b.StartDate <= end) && (b.EndDate is null || b.EndDate >= start));
                var budgeted = row?.ProjectedAmount ?? (applies ? b.AmountFor(start) : 0m);
                // Paid on with no amount means it went out at the planned amount.
                var paid = row?.ActualAmount ?? (row?.PaidOn is not null ? budgeted : (decimal?)null);
                var via = b.PaymentMethod switch
                {
                    PaymentMethodKind.Card => $"Card: {b.PaymentCard?.Name}",
                    PaymentMethodKind.Cash => "Cash",
                    _ => b.PaymentAccount?.Name ?? "—",
                };
                return (b.FundingAccountId, line: new FundingLineDto(b.Id, b.Name, row?.DueDate ?? generated.Cast<DateOnly?>().FirstOrDefault(), budgeted, paid, via));
            })
            .Where(x => x.line.Budgeted > 0 || x.line.Paid is > 0)
            .GroupBy(x => x.FundingAccountId)
            .Select(g =>
            {
                var items = g.Select(x => x.line).OrderBy(l => l.DueDate ?? DateOnly.MaxValue).ThenBy(l => l.LineName).ToList();
                var budgeted = Math.Round(items.Sum(l => l.Budgeted), 2);
                var paid = Math.Round(items.Sum(l => l.Paid ?? 0m), 2);
                var name = accounts.GetValueOrDefault(g.Key)?.Name ?? $"Account {g.Key}";
                return new FundingAccountMonthDto(g.Key, name, budgeted, paid, Math.Max(0m, Math.Round(budgeted - paid, 2)), steady.GetValueOrDefault(g.Key),
                    string.Join(" + ", items.Select(l => $"{l.LineName} {l.Budgeted:C}")) + $" = {budgeted:C}", items);
            })
            .OrderByDescending(a => a.Budgeted).ThenBy(a => a.AccountName)
            .ToList();

        return new FundingMonthDto(start, Math.Round(byAccount.Sum(a => a.Budgeted), 2), Math.Round(byAccount.Sum(a => a.Paid), 2), byAccount);
    }

    internal static async Task<TransferNeedsDto> Needs(BudgetDbContext db, DateOnly asOf)
    {
        var (perYear, source) = await IncomeEndpoints.PaychecksPerYear(db, asOf);
        var lines = await db.BudgetLines.Include(b => b.Periods).Include(b => b.Amounts).ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);
        var monthStart = new DateOnly(asOf.Year, asOf.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var transferred = await db.Transactions
            .Where(t => t.AccountId != null && t.IsTransfer && t.Date >= monthStart && t.Date <= monthEnd && !(t.Origin == TransactionOrigin.Manual && t.ReconciledWithId != null))
            .GroupBy(t => t.AccountId!.Value)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Sum);

        var needs = TransferNeeds.Compute(lines, perYear, asOf, BudgetEndpoints.ProjectedOverrides(lines, asOf));
        var dtos = needs.Select(n =>
        {
            var acct = accounts.GetValueOrDefault(n.AccountId);
            var moved = transferred.GetValueOrDefault(n.AccountId);
            return new AccountNeedDto(
                n.AccountId, acct?.Name ?? $"Account {n.AccountId}", acct?.TransferCadence ?? Domain.TransferCadence.Monthly,
                n.Monthly, n.PerPaycheck, n.PerPaycheckFormula, moved, Math.Round(moved - n.Monthly, 2),
                n.Lines.Select(l => new NeedLineDto(l.BudgetLineId, l.LineName, l.Frequency, l.ProjectedAmount, l.MonthlyAccrual, l.Formula, l.PaidByCard)).ToList());
        }).OrderBy(d => d.AccountName).ToList();

        return new TransferNeedsDto(asOf, perYear, source, dtos);
    }
}

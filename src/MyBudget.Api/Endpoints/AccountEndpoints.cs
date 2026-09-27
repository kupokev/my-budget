using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccounts(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/accounts");

        g.MapGet("/", async (BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var accounts = (await db.Accounts.Include(a => a.Balances).OrderBy(a => a.Name).ToListAsync()).Select(a => a.ToDto()).ToList();
            var outflow = await MonthlyOutflow(db, today);
            foreach (var a in accounts)
                if (outflow.TryGetValue(a.Id, out var o)) { a.ThisMonthOutflow = o.Total; a.ThisMonthOutflowDetail = o.Detail; }
            return accounts;
        });

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await db.Accounts.Include(a => a.Balances).FirstOrDefaultAsync(a => a.Id == id) is { } a ? Results.Ok(a.ToDto()) : Results.NotFound());

        g.MapPost("/", async (AccountDto dto, BudgetDbContext db) =>
        {
            var a = new Account { Name = dto.Name };
            a.Apply(dto);
            db.Accounts.Add(a);
            await db.SaveChangesAsync();
            return Results.Created($"/api/accounts/{a.Id}", a.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, AccountDto dto, BudgetDbContext db) =>
        {
            var a = await db.Accounts.Include(x => x.Balances).FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            a.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(a.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var a = await db.Accounts.FindAsync(id);
            if (a is null) return Results.NotFound();
            if (await db.Bills.AnyAsync(b => b.FundingAccountId == id || b.PaymentAccountId == id) || await db.Cards.AnyAsync(c => c.PayingAccountId == id))
                return Results.Conflict("Account is referenced by a bill or card. Mark it inactive instead.");
            db.Accounts.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/{id:int}/balances", async (int id, BudgetDbContext db) =>
            (await db.AccountBalances.Where(b => b.AccountId == id).OrderByDescending(b => b.AsOf).ToListAsync()).Select(b => b.ToDto()));

        // Upsert by (account, asOf): re-entering the same date overwrites.
        g.MapPost("/{id:int}/balances", async (int id, AccountBalanceDto dto, BudgetDbContext db) =>
        {
            if (await db.Accounts.FindAsync(id) is null) return Results.NotFound();
            var b = await db.AccountBalances.FirstOrDefaultAsync(x => x.AccountId == id && x.AsOf == dto.AsOf)
                    ?? db.AccountBalances.Add(new AccountBalance { AccountId = id, AsOf = dto.AsOf }).Entity;
            b.Balance = dto.Balance;
            await db.SaveChangesAsync();
            return Results.Ok(b.ToDto());
        });

        g.MapGet("/{id:int}/transfers", async (int id, int? year, int? month, BudgetDbContext db) =>
        {
            var q = db.Transfers.Where(t => t.AccountId == id);
            if (year is { } y) q = q.Where(t => t.Date.Year == y);
            if (month is { } m) q = q.Where(t => t.Date.Month == m);
            return (await q.OrderByDescending(t => t.Date).ToListAsync()).Select(t => t.ToDto());
        });

        g.MapPost("/{id:int}/transfers", async (int id, TransferDto dto, BudgetDbContext db) =>
        {
            if (await db.Accounts.FindAsync(id) is null) return Results.NotFound();
            var t = new Transfer { AccountId = id, Date = dto.Date, Amount = dto.Amount, Notes = dto.Notes };
            db.Transfers.Add(t);
            await db.SaveChangesAsync();
            return Results.Created($"/api/accounts/{id}/transfers/{t.Id}", t.ToDto());
        });

        g.MapDelete("/{id:int}/transfers/{transferId:int}", async (int id, int transferId, BudgetDbContext db) =>
        {
            var t = await db.Transfers.FirstOrDefaultAsync(x => x.Id == transferId && x.AccountId == id);
            if (t is null) return Results.NotFound();
            db.Transfers.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }

    /// <summary>
    /// Cash actually leaving each account in the month: bills paid from the account directly, plus bills charged to a
    /// card that this account pays. Uses each bill's projected amount for the month (with per-month overrides), only
    /// for months the bill is due. This is what a direct deposit into the account has to cover.
    /// </summary>
    internal static async Task<Dictionary<int, (decimal Total, string Detail)>> MonthlyOutflow(BudgetDbContext db, DateOnly month)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var bills = await db.Bills.Include(b => b.Periods).Include(b => b.PaymentCard).Where(b => b.IsActive).ToListAsync();
        var lines = new Dictionary<int, List<(string Name, decimal Amount)>>();
        foreach (var b in bills)
        {
            var due = MyBudget.Engines.Ledger.BillDueDates.Between(b, start, end, BillEndpoints.DueOverrides(b));
            var period = b.Periods.FirstOrDefault(p => p.Period == start);
            var amount = period?.ProjectedAmount ?? (due.Count > 0 ? b.ProjectedAmount * due.Count : 0m);
            if (amount <= 0) continue;
            int? accountId = b.PaymentMethod == PaymentMethodKind.Card ? b.PaymentCard?.PayingAccountId : b.PaymentAccountId;
            if (accountId is null) continue;
            if (!lines.TryGetValue(accountId.Value, out var list)) lines[accountId.Value] = list = [];
            list.Add((b.PaymentMethod == PaymentMethodKind.Card ? $"{b.Name} (via {b.PaymentCard!.Name})" : b.Name, amount));
        }
        return lines.ToDictionary(kv => kv.Key, kv => (Math.Round(kv.Value.Sum(x => x.Amount), 2), string.Join(", ", kv.Value.OrderByDescending(x => x.Amount).Select(x => $"{x.Name} {x.Amount:C}"))));
    }
}

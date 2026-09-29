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
            var accounts = (await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).OrderBy(a => a.Name).ToListAsync()).Select(a => a.ToDto(today)).ToList();
            var outflow = await MonthlyOutflow(db, today);
            foreach (var a in accounts)
                if (outflow.TryGetValue(a.Id, out var o)) { a.ThisMonthOutflow = o.Total; a.ThisMonthOutflowDetail = o.Detail; }

            // An investment account holds securities rather than a typed balance, so it read as "—"
            // here while the Wealth screens showed it in full. Same rule as the net-worth report.
            var byHoldings = await HoldingValues.ByAccountAsync(db, today);
            foreach (var a in accounts)
                if (byHoldings.TryGetValue(a.Id, out var valued))
                {
                    a.LatestBalance = valued.Value;
                    a.LatestBalanceAsOf = valued.PricedAsOf;
                    a.BalanceDetail = $"holdings valued {(valued.PricedAsOf is { } d ? $"at prices to {d:yyyy-MM-dd}" : "at the latest prices")}";
                }

            return accounts;
        });

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).FirstOrDefaultAsync(a => a.Id == id) is { } a ? Results.Ok(a.ToDto()) : Results.NotFound());

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
            var a = await db.Accounts.Include(x => x.Balances).Include(x => x.Transactions).FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            a.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(a.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var a = await db.Accounts.FindAsync(id);
            if (a is null) return Results.NotFound();
            if (await db.BudgetLines.AnyAsync(b => b.FundingAccountId == id || b.PaymentAccountId == id) || await db.Cards.AnyAsync(c => c.PayingAccountId == id))
                return Results.Conflict("Account is referenced by a budget line or card. Mark it inactive instead.");
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

        // Manual transfers live in the transactions table (Origin = Manual, IsTransfer = true) so they show on Transactions too.
        g.MapGet("/{id:int}/transfers", async (int id, int? year, int? month, BudgetDbContext db) =>
        {
            var q = db.Transactions.Include(t => t.CounterpartyAccount).Where(t => t.AccountId == id && t.Origin == TransactionOrigin.Manual);
            if (year is { } y) q = q.Where(t => t.Date.Year == y);
            if (month is { } m) q = q.Where(t => t.Date.Month == m);
            return (await q.OrderByDescending(t => t.Date).ToListAsync()).Select(t => t.ToTransferDto());
        });

        // A move between two of your accounts writes both sides, linked, in one call.
        g.MapPost("/{id:int}/transfers", async (int id, TransferDto dto, BudgetDbContext db) =>
        {
            var account = await db.Accounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            if (dto.Amount == 0) return Results.Problem("Amount can't be zero.", statusCode: 400);
            if (dto.CounterpartyAccountId == id) return Results.Problem("The other account must be a different account.", statusCode: 400);
            var other = dto.CounterpartyAccountId is { } oid ? await db.Accounts.FindAsync(oid) : null;
            var direction = dto.Amount > 0 ? "Transfer in" : "Transfer out";
            var t = ManualTransfer(id, dto.Date, dto.Amount, $"{direction}{(other is not null ? (dto.Amount > 0 ? " from " : " to ") + other.Name : "")}", dto.Notes, other?.Id);
            db.Transactions.Add(t);
            if (other is not null)
            {
                var mirror = ManualTransfer(other.Id, dto.Date, -dto.Amount, $"{(dto.Amount > 0 ? "Transfer out to " : "Transfer in from ")}{account.Name}", dto.Notes, id);
                db.Transactions.Add(mirror);
                await db.SaveChangesAsync();
                t.LinkedTransactionId = mirror.Id; mirror.LinkedTransactionId = t.Id;
            }
            await db.SaveChangesAsync();
            await db.Entry(t).Reference(x => x.CounterpartyAccount).LoadAsync();
            return Results.Created($"/api/accounts/{id}/transfers/{t.Id}", t.ToTransferDto());
        });

        g.MapDelete("/{id:int}/transfers/{transferId:int}", async (int id, int transferId, BudgetDbContext db) =>
        {
            var t = await db.Transactions.FirstOrDefaultAsync(x => x.Id == transferId && x.AccountId == id);
            if (t is null) return Results.NotFound();
            db.Transactions.Remove(t);
            if (t.LinkedTransactionId is { } linked && await db.Transactions.FindAsync(linked) is { } mirror)
            {
                db.Transactions.Remove(mirror);
                if (mirror.ReconciledWithId is { } mr && await db.Transactions.FindAsync(mr) is { } mp) mp.ReconciledWithId = null;
            }
            if (t.ReconciledWithId is { } rec && await db.Transactions.FindAsync(rec) is { } partner) partner.ReconciledWithId = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }

    internal static Transaction ManualTransfer(int accountId, DateOnly date, decimal amount, string description, string? notes, int? counterpartyId) => new()
    {
        AccountId = accountId, Date = date, Amount = amount, Description = description, Merchant = description, Notes = notes,
        IsTransfer = true, Origin = TransactionOrigin.Manual, ExternalId = "manual:" + Guid.NewGuid().ToString("N"), CounterpartyAccountId = counterpartyId, IsManuallyCategorized = true,
    };

    /// <summary>
    /// Cash actually leaving each account in the month: lines paid from the account directly, plus lines charged to a
    /// card that this account pays. Uses each line's projected amount for the month (with per-month overrides), only
    /// for months the line is due. This is what a direct deposit into the account has to cover.
    /// </summary>
    internal static async Task<Dictionary<int, (decimal Total, string Detail)>> MonthlyOutflow(BudgetDbContext db, DateOnly month)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var budgetLines = await db.BudgetLines.Include(b => b.Periods).Include(b => b.Amounts).Include(b => b.PaymentCard).Where(b => b.IsActive).ToListAsync();
        var byAccount = new Dictionary<int, List<(string Name, decimal Amount)>>();
        foreach (var b in budgetLines)
        {
            var due = MyBudget.Engines.Ledger.BudgetDueDates.Between(b, start, end, BudgetEndpoints.DueOverrides(b));
            var period = b.Periods.FirstOrDefault(p => p.Period == start);
            var amount = period?.ProjectedAmount ?? (due.Count > 0 ? b.ProjectedAmount * due.Count : 0m);
            if (amount <= 0) continue;
            int? accountId = b.PaymentMethod switch
            {
                PaymentMethodKind.Card => b.PaymentCard?.PayingAccountId,
                // Cash names no paying account, but it is withdrawn from the one funding the line.
                PaymentMethodKind.Cash => b.FundingAccountId,
                _ => b.PaymentAccountId,
            };
            if (accountId is null) continue;
            if (!byAccount.TryGetValue(accountId.Value, out var list)) byAccount[accountId.Value] = list = [];
            list.Add((b.PaymentMethod switch
            {
                PaymentMethodKind.Card => $"{b.Name} (via {b.PaymentCard!.Name})",
                PaymentMethodKind.Cash => $"{b.Name} (cash)",
                _ => b.Name,
            }, amount));
        }
        return byAccount.ToDictionary(kv => kv.Key, kv => (Math.Round(kv.Value.Sum(x => x.Amount), 2), string.Join(", ", kv.Value.OrderByDescending(x => x.Amount).Select(x => $"{x.Name} {x.Amount:C}"))));
    }
}

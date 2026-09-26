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

        g.MapGet("/", async (BudgetDbContext db) =>
            (await db.Accounts.Include(a => a.Balances).OrderBy(a => a.Name).ToListAsync()).Select(a => a.ToDto()));

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
}

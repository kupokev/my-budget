using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

public static class CardEndpoints
{
    public static RouteGroupBuilder MapCards(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/cards");

        g.MapGet("/", async (BudgetDbContext db) =>
            (await db.Cards.Include(c => c.Balances).Include(c => c.Fees).OrderBy(c => c.Name).ToListAsync()).Select(c => c.ToDto()));

        // CC-2: lines charged to each card, monthly spend from those lines, balance, utilization, paying account.
        g.MapGet("/summary", async (BudgetDbContext db, TimeProvider clock) =>
        {
            var asOf = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var cards = await db.Cards.Include(c => c.Balances).Include(c => c.PayingAccount).Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            var lines = await db.BudgetLines.Where(b => b.IsActive && b.PaymentMethod == PaymentMethodKind.Card).ToListAsync();

            // Actual spend on each card, this month and last, straight from its transactions.
            var thisMonth = new DateOnly(asOf.Year, asOf.Month, 1);
            var lastMonth = thisMonth.AddMonths(-1);
            var spend = (await db.Transactions
                    .Where(t => t.CardId != null && !t.IsTransfer && t.Amount < 0 && t.Date >= lastMonth && t.Date < thisMonth.AddMonths(1))
                    .Where(t => t.Origin != TransactionOrigin.Manual || t.ReconciledWithId == null)
                    .Select(t => new { t.CardId, t.Date, t.Amount }).ToListAsync())
                .GroupBy(t => (t.CardId!.Value, new DateOnly(t.Date.Year, t.Date.Month, 1)))
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => -x.Amount), 2));
            return cards.Select(c =>
            {
                var mine = lines.Where(b => b.PaymentCardId == c.Id).ToList();
                var latest = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
                return new CardSummaryDto(
                    c.Id, c.Name, c.PayingAccount?.Name, c.StatementDay, c.DueDay,
                    latest?.Balance, latest?.AsOf, c.CreditLimit,
                    latest is not null && c.CreditLimit > 0 ? Math.Round(latest.Balance / c.CreditLimit, 4) : null,
                    mine.Select(b => b.Name).OrderBy(n => n).ToList(),
                    Math.Round(mine.Sum(b => SinkingFund.MonthlyAccrual(b, asOf).Monthly), 2),
                    spend.GetValueOrDefault((c.Id, thisMonth)),
                    spend.GetValueOrDefault((c.Id, lastMonth)));
            });
        });

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await db.Cards.Include(c => c.Balances).Include(c => c.Fees).FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(c.ToDto()) : Results.NotFound());

        g.MapPost("/", async (CardDto dto, BudgetDbContext db) =>
        {
            var c = new Card { Name = dto.Name };
            c.Apply(dto);
            db.Cards.Add(c);
            await db.SaveChangesAsync();
            try { await CardFeeBudget.SyncAsync(db, c); }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            await db.SaveChangesAsync();
            return Results.Created($"/api/cards/{c.Id}", c.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, CardDto dto, BudgetDbContext db) =>
        {
            var c = await db.Cards.Include(x => x.Balances).Include(x => x.Fees).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            c.Apply(dto);
            try { await CardFeeBudget.SyncAsync(db, c); }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            await db.SaveChangesAsync();
            return Results.Ok(c.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var c = await db.Cards.FindAsync(id);
            if (c is null) return Results.NotFound();
            if (await db.BudgetLines.AnyAsync(b => b.PaymentCardId == id))
                return Results.Conflict("Card is the payment method for a budget line. Mark it inactive instead.");
            db.Cards.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/{id:int}/balances", async (int id, BudgetDbContext db) =>
            (await db.CardBalances.Where(b => b.CardId == id).OrderByDescending(b => b.AsOf).ToListAsync()).Select(b => b.ToDto()));

        g.MapPost("/{id:int}/balances", async (int id, CardBalanceDto dto, BudgetDbContext db) =>
        {
            if (await db.Cards.FindAsync(id) is null) return Results.NotFound();
            var b = await db.CardBalances.FirstOrDefaultAsync(x => x.CardId == id && x.AsOf == dto.AsOf)
                    ?? db.CardBalances.Add(new CardBalance { CardId = id, AsOf = dto.AsOf }).Entity;
            b.Balance = dto.Balance;
            await db.SaveChangesAsync();
            return Results.Ok(b.ToDto());
        });

        return api;
    }
}

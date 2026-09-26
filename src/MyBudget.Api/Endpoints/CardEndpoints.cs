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
            (await db.Cards.Include(c => c.Balances).OrderBy(c => c.Name).ToListAsync()).Select(c => c.ToDto()));

        // CC-2: bills charged to each card, monthly spend from those bills, balance, utilization, paying account.
        g.MapGet("/summary", async (BudgetDbContext db, TimeProvider clock) =>
        {
            var asOf = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var cards = await db.Cards.Include(c => c.Balances).Include(c => c.PayingAccount).Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            var bills = await db.Bills.Where(b => b.IsActive && b.PaymentMethod == PaymentMethodKind.Card).ToListAsync();
            return cards.Select(c =>
            {
                var mine = bills.Where(b => b.PaymentCardId == c.Id).ToList();
                var latest = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
                return new CardSummaryDto(
                    c.Id, c.Name, c.PayingAccount?.Name, c.StatementDay, c.DueDay,
                    latest?.Balance, latest?.AsOf, c.CreditLimit,
                    latest is not null && c.CreditLimit > 0 ? Math.Round(latest.Balance / c.CreditLimit, 4) : null,
                    mine.Select(b => b.Name).OrderBy(n => n).ToList(),
                    Math.Round(mine.Sum(b => SinkingFund.MonthlyAccrual(b, asOf).Monthly), 2));
            });
        });

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await db.Cards.Include(c => c.Balances).FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(c.ToDto()) : Results.NotFound());

        g.MapPost("/", async (CardDto dto, BudgetDbContext db) =>
        {
            var c = new Card { Name = dto.Name };
            c.Apply(dto);
            db.Cards.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/cards/{c.Id}", c.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, CardDto dto, BudgetDbContext db) =>
        {
            var c = await db.Cards.Include(x => x.Balances).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            c.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(c.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var c = await db.Cards.FindAsync(id);
            if (c is null) return Results.NotFound();
            if (await db.Bills.AnyAsync(b => b.PaymentCardId == id))
                return Results.Conflict("Card is the payment method for a bill. Mark it inactive instead.");
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

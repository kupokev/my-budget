using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactions(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/transactions");

        g.MapGet("/", async (int? year, int? month, int? categoryId, bool? uncategorized, string? search, int? accountId, int? cardId, int? billId, int? limit, BudgetDbContext db) =>
        {
            var q = Query(db);
            if (year is { } y) q = q.Where(t => t.Date.Year == y);
            if (month is { } m) q = q.Where(t => t.Date.Month == m);
            if (categoryId is { } c) q = q.Where(t => t.CategoryId == c);
            if (uncategorized == true) q = q.Where(t => t.CategoryId == null && !t.IsTransfer);
            if (accountId is { } a) q = q.Where(t => t.AccountId == a);
            if (cardId is { } cd) q = q.Where(t => t.CardId == cd);
            if (billId is { } b) q = q.Where(t => t.BillId == b);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(t => t.Description.Contains(search) || (t.Merchant != null && t.Merchant.Contains(search)));
            return (await q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).Take(limit ?? 500).ToListAsync()).Select(ToDto);
        });

        g.MapPut("/{id:int}", async (int id, TransactionUpdateDto dto, BudgetDbContext db, ImportService svc) =>
        {
            var t = await Query(db).FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            t.CategoryId = dto.CategoryId; t.BillId = dto.BillId; t.IsTransfer = dto.IsTransfer; t.Notes = dto.Notes; t.IsManuallyCategorized = true;
            if (t.BillId is { } b && t.CategoryId is null) t.CategoryId = (await db.Bills.FindAsync(b))?.CategoryId;
            CategoryRule? rule = null;
            if (dto.CreateRule)
            {
                var pattern = string.IsNullOrWhiteSpace(dto.RulePattern) ? t.Merchant ?? t.Description : dto.RulePattern.Trim();
                rule = new CategoryRule { Pattern = pattern, Match = RuleMatch.Contains, CategoryId = dto.CategoryId, BillId = dto.BillId, MarkAsTransfer = dto.IsTransfer };
                db.CategoryRules.Add(rule);
            }
            await db.SaveChangesAsync();
            await svc.SyncAsync([t]);
            if (rule is not null && dto.ApplyRuleToExisting) await svc.ApplyRulesAsync(rule);
            return Results.Ok(ToDto((await Query(db).FirstAsync(x => x.Id == id))));
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db, ImportService svc) =>
        {
            var t = await db.Transactions.FindAsync(id);
            if (t is null) return Results.NotFound();
            db.Transactions.Remove(t);
            await db.SaveChangesAsync();
            await svc.SyncAsync([t]);
            return Results.NoContent();
        });

        var r = api.MapGroup("/category-rules");
        r.MapGet("/", async (BudgetDbContext db) => (await db.CategoryRules.OrderBy(x => x.Priority).ThenBy(x => x.Pattern).ToListAsync()).Select(ToDto));
        r.MapPost("/", async (CategoryRuleDto dto, BudgetDbContext db) =>
        {
            var e = new CategoryRule { Pattern = dto.Pattern.Trim(), Match = dto.Match, CategoryId = dto.CategoryId, BillId = dto.BillId, MarkAsTransfer = dto.MarkAsTransfer, Priority = dto.Priority, IsActive = dto.IsActive };
            db.CategoryRules.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/category-rules/{e.Id}", ToDto(e));
        });
        r.MapPut("/{id:int}", async (int id, CategoryRuleDto dto, BudgetDbContext db) =>
        {
            var e = await db.CategoryRules.FindAsync(id);
            if (e is null) return Results.NotFound();
            e.Pattern = dto.Pattern.Trim(); e.Match = dto.Match; e.CategoryId = dto.CategoryId; e.BillId = dto.BillId; e.MarkAsTransfer = dto.MarkAsTransfer; e.Priority = dto.Priority; e.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        r.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var e = await db.CategoryRules.FindAsync(id);
            if (e is null) return Results.NotFound();
            db.CategoryRules.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        r.MapPost("/apply", async (ImportService svc) => new { Changed = await svc.ApplyRulesAsync() });

        return api;
    }

    internal static IQueryable<Transaction> Query(BudgetDbContext db)
        => db.Transactions.Include(t => t.Account).Include(t => t.Card).Include(t => t.Category).Include(t => t.Bill);

    internal static TransactionDto ToDto(Transaction t) => new(t.Id, t.AccountId, t.CardId, t.Account?.Name ?? t.Card?.Name ?? "—", t.Date, t.PostedDate, t.Amount,
        t.Description, t.Merchant, t.CategoryId, t.Category?.Name, t.BillId, t.Bill?.Name, t.IsTransfer, t.Notes, t.IsManuallyCategorized);

    private static CategoryRuleDto ToDto(CategoryRule r) => new() { Id = r.Id, Pattern = r.Pattern, Match = r.Match, CategoryId = r.CategoryId, BillId = r.BillId, MarkAsTransfer = r.MarkAsTransfer, Priority = r.Priority, IsActive = r.IsActive };
}

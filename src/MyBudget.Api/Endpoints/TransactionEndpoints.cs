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

        g.MapGet("/", async (int? year, int? month, int? categoryId, bool? uncategorized, string? search, int? accountId, int? cardId, int? billId, int? limit, bool? unreconciled, BudgetDbContext db) =>
        {
            var q = Query(db);
            if (unreconciled == true) q = q.Where(t => t.Origin == TransactionOrigin.Manual && t.ReconciledWithId == null);
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
            t.CategoryId = dto.CategoryId; t.LabelId = dto.LabelId; t.BillId = dto.BillId; t.IsTransfer = dto.IsTransfer; t.Notes = dto.Notes; t.IsManuallyCategorized = true;
            if (t.BillId is { } b && t.CategoryId is null) t.CategoryId = (await db.Bills.FindAsync(b))?.CategoryId;
            // Repayment from a person: create/replace/remove the receivable payment this line represents.
            var existingPayment = t.ReceivablePaymentId is { } rp ? await db.ReceivablePayments.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.Id == rp) : null;
            if (dto.RepaymentFromPersonId is { } personId && t.Amount > 0)
            {
                if (existingPayment is not null && existingPayment.PersonId != personId) { db.ReceivablePayments.Remove(existingPayment); existingPayment = null; }
                if (existingPayment is null)
                {
                    var person = await db.People.Include(p => p.Obligations).ThenInclude(o => o.Bill).Include(p => p.Charges).Include(p => p.Payments).ThenInclude(x => x.Allocations).FirstOrDefaultAsync(p => p.Id == personId);
                    if (person is null) return Results.NotFound("Person not found.");
                    var payment = new ReceivablePayment { PersonId = personId, Date = t.Date, Amount = t.Amount, Notes = $"from bank line: {t.Merchant ?? t.Description}" };
                    payment.Allocations.AddRange(ReceivableEndpoints.AutoAllocate(person, t.Amount, t.Date));
                    db.ReceivablePayments.Add(payment);
                    await db.SaveChangesAsync();
                    t.ReceivablePaymentId = payment.Id;
                }
                t.IsTransfer = true; // not income, not spending: it's money coming back
            }
            else if (existingPayment is not null)
            {
                db.ReceivablePayments.Remove(existingPayment); t.ReceivablePaymentId = null;
            }
            CategoryRule? rule = null;
            if (dto.CreateRule)
            {
                var pattern = string.IsNullOrWhiteSpace(dto.RulePattern) ? t.Merchant ?? t.Description : dto.RulePattern.Trim();
                rule = new CategoryRule { Pattern = pattern, Match = RuleMatch.Contains, CategoryId = dto.CategoryId, LabelId = dto.LabelId, BillId = dto.BillId, MarkAsTransfer = dto.IsTransfer };
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
            if (t.LinkedTransactionId is { } linked && await db.Transactions.FindAsync(linked) is { } mirror) db.Transactions.Remove(mirror);
            if (t.ReconciledWithId is { } rec && await db.Transactions.FindAsync(rec) is { } partner) partner.ReconciledWithId = null;
            if (t.ReceivablePaymentId is { } rpId && await db.ReceivablePayments.FindAsync(rpId) is { } rpay) db.ReceivablePayments.Remove(rpay);
            await db.SaveChangesAsync();
            await svc.SyncAsync([t]);
            return Results.NoContent();
        });

        // Reconciliation: a manual row ↔ the imported line that is the same movement.
        // Close matches only (within 20% or $10 of the amount, ±45 days) unless all=true.
        g.MapGet("/{id:int}/reconcile-candidates", async (int id, bool? all, BudgetDbContext db) =>
        {
            var t = await db.Transactions.FindAsync(id);
            if (t is null) return Results.NotFound();
            var lo = t.Date.AddDays(-45); var hi = t.Date.AddDays(45);
            var tolerance = Math.Max(10m, Math.Abs(t.Amount) * 0.2m);
            var otherOrigin = t.Origin == TransactionOrigin.Manual ? TransactionOrigin.Imported : TransactionOrigin.Manual;
            var list = await Query(db).Where(c => c.Origin == otherOrigin && c.ReconciledWithId == null && c.AccountId == t.AccountId && c.CardId == t.CardId && c.Date >= lo && c.Date <= hi && Math.Sign(c.Amount) == Math.Sign(t.Amount)).ToListAsync();
            return Results.Ok(list.Select(c => new ReconcileCandidateDto(ToDto(c), Math.Abs(c.Date.DayNumber - t.Date.DayNumber), Math.Round(Math.Abs(c.Amount - t.Amount), 2)))
                .Where(c => all == true || c.AmountDifference <= tolerance)
                .OrderBy(c => c.AmountDifference).ThenBy(c => c.DaysApart).Take(25));
        });

        g.MapPost("/{id:int}/reconcile/{otherId:int}", async (int id, int otherId, BudgetDbContext db, ImportService svc) =>
        {
            var a = await db.Transactions.FindAsync(id); var b = await db.Transactions.FindAsync(otherId);
            if (a is null || b is null) return Results.NotFound();
            if (a.Origin == b.Origin) return Results.Problem("Reconcile a manual entry with an imported line, not two of the same kind.", statusCode: 400);
            if (a.ReconciledWithId != null || b.ReconciledWithId != null) return Results.Problem("One of them is already reconciled; unlink it first.", statusCode: 400);
            var (imported, manual) = a.Origin == TransactionOrigin.Imported ? (a, b) : (b, a);
            ImportService.Link(imported, manual);
            await db.SaveChangesAsync();
            await svc.SyncAsync([imported]);
            return Results.Ok(ToDto(await Query(db).FirstAsync(x => x.Id == id)));
        });

        g.MapDelete("/{id:int}/reconcile", async (int id, BudgetDbContext db) =>
        {
            var t = await db.Transactions.FindAsync(id);
            if (t is null) return Results.NotFound();
            if (t.ReconciledWithId is { } otherId && await db.Transactions.FindAsync(otherId) is { } other) other.ReconciledWithId = null;
            t.ReconciledWithId = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var r = api.MapGroup("/category-rules");
        r.MapGet("/", async (BudgetDbContext db) => (await db.CategoryRules.OrderBy(x => x.Priority).ThenBy(x => x.Pattern).ToListAsync()).Select(ToDto));
        r.MapPost("/", async (CategoryRuleDto dto, BudgetDbContext db) =>
        {
            var e = new CategoryRule { Pattern = dto.Pattern.Trim(), Match = dto.Match, CategoryId = dto.CategoryId, LabelId = dto.LabelId, BillId = dto.BillId, MarkAsTransfer = dto.MarkAsTransfer, Priority = dto.Priority, IsActive = dto.IsActive };
            db.CategoryRules.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/category-rules/{e.Id}", ToDto(e));
        });
        r.MapPut("/{id:int}", async (int id, CategoryRuleDto dto, BudgetDbContext db) =>
        {
            var e = await db.CategoryRules.FindAsync(id);
            if (e is null) return Results.NotFound();
            e.Pattern = dto.Pattern.Trim(); e.Match = dto.Match; e.CategoryId = dto.CategoryId; e.LabelId = dto.LabelId; e.BillId = dto.BillId; e.MarkAsTransfer = dto.MarkAsTransfer; e.Priority = dto.Priority; e.IsActive = dto.IsActive;
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
        => db.Transactions.Include(t => t.Account).Include(t => t.Card).Include(t => t.Category).Include(t => t.Label).Include(t => t.Bill).Include(t => t.CounterpartyAccount).Include(t => t.ReconciledWith).Include(t => t.ReceivablePayment).ThenInclude(p => p!.Person);

    internal static TransactionDto ToDto(Transaction t) => new(t.Id, t.AccountId, t.CardId, t.Account?.Name ?? t.Card?.Name ?? "—", t.Date, t.PostedDate, t.Amount,
        t.Description, t.Merchant, t.CategoryId, t.Category?.Name, t.BillId, t.Bill?.Name, t.IsTransfer, t.Notes, t.IsManuallyCategorized, t.Origin, t.CounterpartyAccount?.Name,
        t.ReconciledWithId, t.ReconciledWith is { } r ? $"{r.Date:MMM d} {r.Amount:C} {(r.Merchant ?? r.Description)}" : null,
        t.ReceivablePayment?.PersonId, t.ReceivablePayment?.Person?.Name, t.LabelId, t.Label?.Name);

    private static CategoryRuleDto ToDto(CategoryRule r) => new() { Id = r.Id, Pattern = r.Pattern, Match = r.Match, CategoryId = r.CategoryId, LabelId = r.LabelId, BillId = r.BillId, MarkAsTransfer = r.MarkAsTransfer, Priority = r.Priority, IsActive = r.IsActive };
}

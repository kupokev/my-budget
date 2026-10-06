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

        g.MapGet("/", async (int? year, int? month, int? categoryId, bool? uncategorized, string? search, int? accountId, int? cardId, int? lineId, int? limit, bool? unreconciled, BudgetDbContext db) =>
        {
            var q = Query(db);
            if (unreconciled == true) q = q.Where(t => t.Origin == TransactionOrigin.Manual && t.ReconciledWithId == null);
            if (year is { } y) q = q.Where(t => t.Date.Year == y);
            if (month is { } m) q = q.Where(t => t.Date.Month == m);
            if (categoryId is { } c) q = q.Where(t => t.CategoryId == c);
            if (uncategorized == true) q = q.Where(t => t.CategoryId == null && !t.IsTransfer);
            if (accountId is { } a) q = q.Where(t => t.AccountId == a);
            if (cardId is { } cd) q = q.Where(t => t.CardId == cd);
            if (lineId is { } b) q = q.Where(t => t.BudgetLineId == b);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(t => t.Description.Contains(search) || (t.Merchant != null && t.Merchant.Contains(search)));
            var list = (await q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).Take(limit ?? 500).ToListAsync()).Select(ToDto).ToList();
            if (accountId is not { } acct) return list;

            // One account: each row also carries the balance just after it, worked out from the whole
            // account rather than the filtered rows, so it matches the Accounts page.
            var running = BalanceMath.Running(
                await db.AccountBalances.Where(b => b.AccountId == acct).ToListAsync(),
                await db.Transactions.Where(t => t.AccountId == acct).ToListAsync());
            return list.Select(t => running.TryGetValue(t.Id, out var r) ? t with { Balance = r.Balance, BalanceDetail = r.Detail } : t).ToList();
        });

        // Entering a transaction by hand. This is the only place transactions are created outside an
        // import: the card-spend grid and the account transfer form both used to do their own version.
        g.MapPost("/", async (TransactionCreateDto dto, BudgetDbContext db, ImportService svc) =>
        {
            if ((dto.AccountId is null) == (dto.CardId is null))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["AccountId"] = ["Pick either an account or a card, not both."] });
            if (dto.Amount == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = ["Amount can't be zero."] });
            if (dto.CounterpartyAccountId is { } cp && cp == dto.AccountId)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["CounterpartyAccountId"] = ["The other account must be a different account."] });
            if (dto.AccountId is { } aid && await db.Accounts.FindAsync(aid) is null) return Results.NotFound();
            if (dto.CardId is { } cid && await db.Cards.FindAsync(cid) is null) return Results.NotFound();

            var other = dto.CounterpartyAccountId is { } oid ? await db.Accounts.FindAsync(oid) : null;
            var isTransfer = dto.IsTransfer || other is not null;

            var t = new Transaction
            {
                AccountId = dto.AccountId, CardId = dto.CardId, Date = dto.Date, Amount = dto.Amount,
                Description = dto.Description.Trim(), Merchant = dto.Description.Trim(),
                CategoryId = isTransfer ? null : dto.CategoryId, LabelId = isTransfer ? null : dto.LabelId, IncomeSourceId = dto.IncomeSourceId,
                BudgetLineId = isTransfer ? null : dto.BudgetLineId,
                IsTransfer = isTransfer, Notes = Mapping.Clean(dto.Notes),
                Origin = TransactionOrigin.Manual, IsManuallyCategorized = true,
                CounterpartyAccountId = other?.Id, ExternalId = "manual:" + Guid.NewGuid().ToString("N"),
            };
            db.Transactions.Add(t);

            // A transfer has two sides, so the other account gets the mirror row and the pair is linked.
            if (other is not null)
            {
                var source = dto.AccountId is { } sid ? (await db.Accounts.FindAsync(sid))!.Name : (await db.Cards.FindAsync(dto.CardId!.Value))!.Name;
                var mirror = new Transaction
                {
                    AccountId = other.Id, Date = dto.Date, Amount = -dto.Amount,
                    Description = $"{(dto.Amount > 0 ? "Transfer out to " : "Transfer in from ")}{source}",
                    Merchant = $"{(dto.Amount > 0 ? "Transfer out to " : "Transfer in from ")}{source}",
                    IsTransfer = true, Notes = Mapping.Clean(dto.Notes), Origin = TransactionOrigin.Manual,
                    IsManuallyCategorized = true, CounterpartyAccountId = dto.AccountId,
                    ExternalId = "manual:" + Guid.NewGuid().ToString("N"),
                };
                db.Transactions.Add(mirror);
                await db.SaveChangesAsync();
                t.LinkedTransactionId = mirror.Id; mirror.LinkedTransactionId = t.Id;
            }

            await db.SaveChangesAsync();
            await svc.SyncAsync([t]);   // keeps budget-line actuals in step
            return Results.Created($"/api/transactions/{t.Id}", ToDto(await Query(db).FirstAsync(x => x.Id == t.Id)));
        });

        g.MapPut("/{id:int}", async (int id, TransactionUpdateDto dto, BudgetDbContext db, ImportService svc) =>
        {
            var t = await Query(db).FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            t.CategoryId = dto.CategoryId; t.LabelId = dto.LabelId; t.BudgetLineId = dto.BudgetLineId; t.IsTransfer = dto.IsTransfer; t.IncomeSourceId = dto.IncomeSourceId; t.Notes = dto.Notes; t.IsManuallyCategorized = true;
            if (t.BudgetLineId is { } b && t.CategoryId is null) t.CategoryId = (await db.BudgetLines.FindAsync(b))?.CategoryId;
            // Repayment from a person: create/replace/remove the receivable payment this line represents.
            var existingPayment = t.ReceivablePaymentId is { } rp ? await db.ReceivablePayments.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.Id == rp) : null;
            if (dto.RepaymentFromPersonId is { } personId && t.Amount > 0)
            {
                if (existingPayment is not null && existingPayment.PersonId != personId) { db.ReceivablePayments.Remove(existingPayment); existingPayment = null; }
                if (existingPayment is null)
                {
                    var person = await db.People.Include(p => p.Obligations).ThenInclude(o => o.BudgetLine).Include(p => p.Charges).Include(p => p.Payments).ThenInclude(x => x.Allocations).FirstOrDefaultAsync(p => p.Id == personId);
                    if (person is null) return Results.NotFound("Person not found.");
                    var payment = new ReceivablePayment { PersonId = personId, Date = t.Date, Amount = t.Amount, Notes = $"from bank feed: {t.Merchant ?? t.Description}" };
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
                rule = new CategoryRule { Pattern = pattern, Match = RuleMatch.Contains, CategoryId = dto.CategoryId, LabelId = dto.LabelId, BudgetLineId = dto.BudgetLineId, MarkAsTransfer = dto.IsTransfer, IncomeSourceId = dto.IncomeSourceId };
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
            var list = await Query(db).Where(c => c.Origin == otherOrigin && c.ReconciledWithId == null && c.AccountId == t.AccountId && c.CardId == t.CardId && c.Date >= lo && c.Date <= hi).ToListAsync();
            // Same direction of money, checked here: SQLite can't translate Math.Sign on a decimal.
            return Results.Ok(list.Where(c => Math.Sign(c.Amount) == Math.Sign(t.Amount)).Select(c => new ReconcileCandidateDto(ToDto(c), Math.Abs(c.Date.DayNumber - t.Date.DayNumber), Math.Round(Math.Abs(c.Amount - t.Amount), 2)))
                .Where(c => all == true || c.AmountDifference <= tolerance)
                .OrderBy(c => c.AmountDifference).ThenBy(c => c.DaysApart).Take(25));
        });

        g.MapPost("/{id:int}/reconcile/{otherId:int}", async (int id, int otherId, BudgetDbContext db, ImportService svc) =>
        {
            var a = await db.Transactions.FindAsync(id); var b = await db.Transactions.FindAsync(otherId);
            if (a is null || b is null) return Results.NotFound();
            if (a.Origin == b.Origin) return Results.Problem("Reconcile a manual entry with an imported transaction, not two of the same kind.", statusCode: 400);
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
            var e = new CategoryRule { Pattern = dto.Pattern.Trim(), Match = dto.Match, CategoryId = dto.CategoryId, LabelId = dto.LabelId, BudgetLineId = dto.BudgetLineId, MarkAsTransfer = dto.MarkAsTransfer, IncomeSourceId = dto.IncomeSourceId, Priority = dto.Priority, IsActive = dto.IsActive };
            db.CategoryRules.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/category-rules/{e.Id}", ToDto(e));
        });
        r.MapPut("/{id:int}", async (int id, CategoryRuleDto dto, BudgetDbContext db) =>
        {
            var e = await db.CategoryRules.FindAsync(id);
            if (e is null) return Results.NotFound();
            e.Pattern = dto.Pattern.Trim(); e.Match = dto.Match; e.CategoryId = dto.CategoryId; e.LabelId = dto.LabelId; e.BudgetLineId = dto.BudgetLineId; e.MarkAsTransfer = dto.MarkAsTransfer; e.IncomeSourceId = dto.IncomeSourceId; e.Priority = dto.Priority; e.IsActive = dto.IsActive;
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
        => db.Transactions.Include(t => t.Account).Include(t => t.Card).Include(t => t.Category).Include(t => t.Label).Include(t => t.IncomeSource).Include(t => t.BudgetLine).Include(t => t.CounterpartyAccount).Include(t => t.ReconciledWith).Include(t => t.ReceivablePayment).ThenInclude(p => p!.Person);

    internal static TransactionDto ToDto(Transaction t) => new(t.Id, t.AccountId, t.CardId, t.Account?.Name ?? t.Card?.Name ?? "—", t.Date, t.PostedDate, t.Amount,
        t.Description, t.Merchant, t.CategoryId, t.Category?.Name, t.BudgetLineId, t.BudgetLine?.Name, t.IsTransfer, t.Notes, t.IsManuallyCategorized, t.Origin, t.CounterpartyAccount?.Name,
        t.ReconciledWithId, t.ReconciledWith is { } r ? $"{r.Date:MMM d} {r.Amount:C} {(r.Merchant ?? r.Description)}" : null,
        t.ReceivablePayment?.PersonId, t.ReceivablePayment?.Person?.Name, t.LabelId, t.Label?.Name, t.IncomeSourceId, t.IncomeSource?.Name);

    private static CategoryRuleDto ToDto(CategoryRule r) => new() { Id = r.Id, Pattern = r.Pattern, Match = r.Match, CategoryId = r.CategoryId, LabelId = r.LabelId, BudgetLineId = r.BudgetLineId, MarkAsTransfer = r.MarkAsTransfer, IncomeSourceId = r.IncomeSourceId, Priority = r.Priority, IsActive = r.IsActive };
}

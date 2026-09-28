using Microsoft.EntityFrameworkCore;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>
/// Keeps a card's annual fee in the budget when asked to.
///
/// A fee is money that will be spent, so it belongs in the budget rather than only in the card's
/// rewards maths — but a row nobody asked for appearing in someone's budget is worse than the gap.
/// Hence the tick on the card: on means there is a line, off means there is not, and deleting the
/// line turns the tick off so the two can never disagree.
/// </summary>
public static class CardFeeBudget
{
    public const string CategoryName = "Fees";
    public const string LabelName = "Credit Card";

    /// <summary>Creates, updates or removes the fee's budget line to match the card. Caller saves.</summary>
    public static async Task SyncAsync(BudgetDbContext db, Card card, CancellationToken ct = default)
    {
        var existing = card.FeeBudgetLineId is { } id
            ? await db.BudgetLines.FirstOrDefaultAsync(b => b.Id == id, ct)
            : null;

        // No fee, or not wanted: take the line away rather than leaving a stale one behind.
        var wanted = card.BudgetAnnualFee && card.AnnualFee > 0 && card.IsActive;
        if (!wanted)
        {
            if (existing is not null) db.BudgetLines.Remove(existing);
            card.FeeBudgetLineId = null;
            card.BudgetAnnualFee = false;
            return;
        }

        // A budget line has to say where the money comes from. A card with no paying account cannot
        // answer that, so it says so rather than inventing one.
        if (card.PayingAccountId is not { } funding)
            throw new InvalidOperationException($"Set the account that pays {card.Name} before budgeting its annual fee.");

        var category = await FindOrCreate(db, ct);
        var label = await FindOrCreateLabel(db, category, ct);

        var line = existing ?? new BudgetLine { Name = card.Name };
        line.Name = card.Name;                      // the card's name is the line's name
        line.CategoryId = category.Id;
        line.LabelId = label.Id;
        line.Frequency = BudgetFrequency.Annual;
        line.ProjectedAmount = card.AnnualFee;
        line.AnchorDueDate = NextFeeDate(card);
        line.DueDay = line.AnchorDueDate?.Day ?? 1;
        line.PaymentMethod = PaymentMethodKind.Card;
        line.PaymentCardId = card.Id;
        line.PaymentAccountId = null;
        line.FundingAccountId = funding;
        line.IsCardEligible = false;                // it is already on the card; it cannot be moved to another
        line.IsActive = true;
        line.Notes = "Annual fee, kept in step with the card.";

        if (existing is null)
        {
            db.BudgetLines.Add(line);
            await db.SaveChangesAsync(ct);          // needs an id to point the card at
        }

        card.FeeBudgetLineId = line.Id;
    }

    /// <summary>Turns the tick off when the line it points at has been deleted from the budget.</summary>
    public static async Task ForgetLineAsync(BudgetDbContext db, int budgetLineId, CancellationToken ct = default)
    {
        foreach (var card in await db.Cards.Where(c => c.FeeBudgetLineId == budgetLineId).ToListAsync(ct))
        {
            card.FeeBudgetLineId = null;
            card.BudgetAnnualFee = false;
        }
    }

    /// <summary>The next time the fee posts: this year if still to come, otherwise next year.</summary>
    private static DateOnly? NextFeeDate(Card card)
    {
        if (card.AnnualFeeMonth is not { } month) return null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var thisYear = new DateOnly(today.Year, month, 1);
        return thisYear >= new DateOnly(today.Year, today.Month, 1) ? thisYear : thisYear.AddYears(1);
    }

    private static async Task<Category> FindOrCreate(BudgetDbContext db, CancellationToken ct)
    {
        var found = await db.Categories.FirstOrDefaultAsync(c => c.Name == CategoryName, ct);
        if (found is not null) return found;

        var created = new Category { Name = CategoryName, IsCardEligible = false };
        db.Categories.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static async Task<Label> FindOrCreateLabel(BudgetDbContext db, Category category, CancellationToken ct)
    {
        var found = await db.Labels.FirstOrDefaultAsync(l => l.Name == LabelName, ct);
        if (found is not null) return found;

        var created = new Label { Name = LabelName, CategoryId = category.Id };
        db.Labels.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}

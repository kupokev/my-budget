using Microsoft.EntityFrameworkCore;
using MyBudget.Domain;

namespace MyBudget.Data;

/// <summary>Creates a Card (with earn rules, thresholds and status paths) from a catalog entry. Categories and programs are created if missing.</summary>
public static class CatalogService
{
    public static async Task<Card> AddCardAsync(BudgetDbContext db, string catalogKey, Account? payingAccount, CancellationToken ct = default)
    {
        var entry = CardCatalog.Entries.FirstOrDefault(e => e.Key == catalogKey)
            ?? throw new KeyNotFoundException($"No catalog entry '{catalogKey}'.");

        LoyaltyProgram? program = null;
        if (entry.Program is { } programName)
        {
            program = await db.LoyaltyPrograms.Include(p => p.Paths).FirstOrDefaultAsync(p => p.Name == programName, ct);
            if (program is null)
            {
                program = CardCatalog.Programs().FirstOrDefault(p => p.Name == programName) ?? new LoyaltyProgram { Name = programName, PointValueCents = entry.PointValueCents };
                db.LoyaltyPrograms.Add(program);
            }
        }

        var card = new Card
        {
            Name = entry.Name, Issuer = entry.Issuer, Network = entry.Network, AnnualFee = entry.AnnualFee, AnnualFeeMonth = entry.AnnualFee > 0 ? 1 : null,
            StatementDay = 1, DueDay = 25, Apr = 24.99m, CreditLimit = 10_000m, PayingAccount = payingAccount,
            LoyaltyProgram = program, PointValueCents = program is null ? entry.PointValueCents : null, CatalogKey = entry.Key, Notes = entry.Notes,
        };
        foreach (var e in entry.EarnRules)
        {
            Category? cat = null;
            if (e.Category is { } name)
            {
                cat = await db.Categories.FirstOrDefaultAsync(c => c.Name == name, ct) ?? db.Categories.Local.FirstOrDefault(c => c.Name == name);
                if (cat is null) { cat = new Category { Name = name }; db.Categories.Add(cat); }
            }
            card.EarnRules.Add(new EarnRule { Category = cat, PointsPerDollar = e.PointsPerDollar, AnnualSpendCap = e.Cap, Notes = e.Notes });
        }
        foreach (var t in entry.Thresholds)
            card.Thresholds.Add(new SpendThreshold { Amount = t.Amount, RewardKind = t.Kind, Description = t.Description, ValueDollars = t.ValueDollars, TierName = t.Tier });
        db.Cards.Add(card);

        if (program is not null)
        {
            if (entry.HoldTier is { } hold)
                program.Paths.Add(new StatusPath { TierName = hold, Kind = StatusPathKind.HoldCard, Threshold = 0, Card = card, Notes = $"for holding the {entry.Name}" });
            foreach (var t in entry.Thresholds.Where(t => t.Kind == ThresholdRewardKind.Status && t.Tier is not null))
                program.Paths.Add(new StatusPath { TierName = t.Tier!, Kind = StatusPathKind.CardSpend, Threshold = t.Amount, Card = card, Notes = t.Description });
        }
        await db.SaveChangesAsync(ct);
        return card;
    }
}

using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;
using MyBudget.Engines.Rewards;

namespace MyBudget.Api.Endpoints;

public static class RewardsEndpoints
{
    public static RouteGroupBuilder MapRewards(this RouteGroupBuilder api)
    {
        // ---- Card rewards setup (RWD-1) ----
        api.MapGet("/cards/{id:int}/rewards", async (int id, BudgetDbContext db) =>
            await db.Cards.Include(c => c.EarnRules).Include(c => c.Thresholds).Include(c => c.Perks).FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(ToRewardsDto(c)) : Results.NotFound());

        api.MapPut("/cards/{id:int}/rewards", async (int id, CardRewardsDto dto, BudgetDbContext db) =>
        {
            var c = await db.Cards.Include(x => x.EarnRules).Include(x => x.Thresholds).Include(x => x.Perks).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            c.LoyaltyProgramId = dto.LoyaltyProgramId; c.PointValueCents = dto.PointValueCents;
            c.EarnRules.Clear();
            c.EarnRules.AddRange(dto.EarnRules.Select(r => new EarnRule { CategoryId = r.CategoryId, LabelId = r.LabelId, PointsPerDollar = r.PointsPerDollar, AnnualSpendCap = r.AnnualSpendCap, StartYear = r.StartYear, EndYear = r.EndYear, Notes = r.Notes }));
            c.Perks.Clear();
            c.Perks.AddRange(dto.Perks.Where(x => !string.IsNullOrWhiteSpace(x.Description)).Select(x => new CardPerk { Description = x.Description.Trim(), AnnualValue = x.AnnualValue, StartYear = x.StartYear, EndYear = x.EndYear, Notes = x.Notes }));
            c.Thresholds.Clear();
            c.Thresholds.AddRange(dto.Thresholds.Where(t => !string.IsNullOrWhiteSpace(t.Description)).Select(t => new SpendThreshold { Amount = t.Amount, RewardKind = t.RewardKind, Description = t.Description.Trim(), ValueDollars = t.ValueDollars, TierName = t.TierName, StartYear = t.StartYear, EndYear = t.EndYear }));
            await db.SaveChangesAsync();
            return Results.Ok(ToRewardsDto(c));
        });

        // ---- Programs (RWD-2) ----
        var p = api.MapGroup("/loyalty-programs");
        p.MapGet("/", async (BudgetDbContext db) => (await Programs(db).OrderBy(x => x.Priority).ToListAsync()).Select(ToDto));
        p.MapPost("/", async (LoyaltyProgramDto dto, BudgetDbContext db) =>
        {
            var e = new LoyaltyProgram { Name = dto.Name };
            Apply(e, dto);
            db.LoyaltyPrograms.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/loyalty-programs/{e.Id}", ToDto(e));
        });
        p.MapPut("/{id:int}", async (int id, LoyaltyProgramDto dto, BudgetDbContext db) =>
        {
            var e = await Programs(db).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            Apply(e, dto);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        p.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var e = await db.LoyaltyPrograms.FindAsync(id);
            if (e is null) return Results.NotFound();
            db.LoyaltyPrograms.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---- The report (RWD-3..6) ----
        api.MapGet("/rewards/report", async (int? year, DateOnly? asOf, BudgetDbContext db, TimeProvider clock) =>
        {
            var today = asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var y = year ?? today.Year;
            // Planning a future year (the "2027 plan by January") starts from January 1 with zero YTD.
            var effectiveAsOf = y > today.Year ? new DateOnly(y, 1, 1) : y < today.Year ? new DateOnly(y, 12, 31) : today;
            return await Report(db, y, effectiveAsOf, y <= today.Year);
        });

        return api;
    }

    internal static async Task<RewardsReportDto> Report(BudgetDbContext db, int year, DateOnly asOf, bool carryCurrentTier = true)
    {
        var cards = await db.Cards.Include(c => c.EarnRules).ThenInclude(r => r.Category).Include(c => c.EarnRules).ThenInclude(r => r.Label).Include(c => c.Thresholds).Include(c => c.Perks).Include(c => c.LoyaltyProgram).Where(c => c.IsActive).ToListAsync();
        var programs = await Programs(db).Where(p => p.IsActive).ToListAsync();
        var spend = await CardSpendFor(db, year);
        var categories = await db.Categories.ToListAsync();
        var labels = await db.Labels.ToListAsync();
        var lines = await db.BudgetLines.Include(b => b.Category).Include(b => b.Periods).Where(b => b.IsActive).ToListAsync();
        var accrual = lines.ToDictionary(b => b.Id, b => SinkingFund.MonthlyAccrual(b, asOf).Monthly);
        return RewardsOptimizer.Run(new RewardsInput(year, asOf, cards, programs, spend, categories, labels, lines, accrual, carryCurrentTier));
    }

    /// <summary>
    /// A card's spend by month, category and label, summed from its transactions. This used to be a
    /// stored table that only an import ever refreshed, which left anything entered by hand invisible
    /// to the thresholds. Deriving it means the figures are always current.
    /// </summary>
    internal static async Task<List<CardSpend>> CardSpendFor(BudgetDbContext db, int year)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year + 1, 1, 1);
        var rows = await db.Transactions
            .Where(t => t.CardId != null && !t.IsTransfer && t.Amount < 0 && t.Date >= from && t.Date < to)
            .Where(t => t.Origin != TransactionOrigin.Manual || t.ReconciledWithId == null)   // a reconciled pair counts once
            .GroupBy(t => new { t.CardId, t.Date.Year, t.Date.Month, t.CategoryId, t.LabelId })
            .Select(g => new { g.Key.CardId, g.Key.Year, g.Key.Month, g.Key.CategoryId, g.Key.LabelId, Sum = g.Sum(t => -t.Amount) })
            .ToListAsync();
        return rows.Select(r => new CardSpend
        {
            CardId = r.CardId!.Value, Period = new DateOnly(r.Year, r.Month, 1),
            CategoryId = r.CategoryId, LabelId = r.LabelId, Amount = Math.Round(r.Sum, 2),
        }).ToList();
    }

    private static IQueryable<LoyaltyProgram> Programs(BudgetDbContext db)
        => db.LoyaltyPrograms.Include(p => p.Tiers).Include(p => p.Paths).ThenInclude(x => x.Card).Include(p => p.Progress);

    private static CardRewardsDto ToRewardsDto(Card c) => new()
    {
        CardId = c.Id, LoyaltyProgramId = c.LoyaltyProgramId, PointValueCents = c.PointValueCents,
        EarnRules = c.EarnRules.OrderBy(r => r.CategoryId is null).ThenByDescending(r => r.PointsPerDollar).Select(r => new EarnRuleDto { Id = r.Id, CategoryId = r.CategoryId, LabelId = r.LabelId, PointsPerDollar = r.PointsPerDollar, AnnualSpendCap = r.AnnualSpendCap, StartYear = r.StartYear, EndYear = r.EndYear, Notes = r.Notes }).ToList(),
        Thresholds = c.Thresholds.OrderBy(t => t.Amount).Select(t => new SpendThresholdDto { Id = t.Id, Amount = t.Amount, RewardKind = t.RewardKind, Description = t.Description, ValueDollars = t.ValueDollars, TierName = t.TierName, StartYear = t.StartYear, EndYear = t.EndYear }).ToList(),
        Perks = c.Perks.OrderByDescending(x => x.AnnualValue).Select(x => new CardPerkDto { Id = x.Id, Description = x.Description, AnnualValue = x.AnnualValue, StartYear = x.StartYear, EndYear = x.EndYear, Notes = x.Notes }).ToList(),
    };

    private static LoyaltyProgramDto ToDto(LoyaltyProgram p) => new()
    {
        Id = p.Id, Name = p.Name, PointValueCents = p.PointValueCents, PointsBalance = p.PointsBalance, CurrentTier = p.CurrentTier, TargetTier = p.TargetTier,
        Priority = p.Priority, IsActive = p.IsActive, Notes = p.Notes,
        Tiers = p.Tiers.OrderBy(t => t.Rank).Select(t => new LoyaltyTierDto { Name = t.Name, Benefits = t.Benefits }).ToList(),
        Paths = p.Paths.OrderBy(x => x.TierName).ThenBy(x => x.Kind).Select(x => new StatusPathDto { Id = x.Id, TierName = x.TierName, Kind = x.Kind, Threshold = x.Threshold, CardId = x.CardId, StartYear = x.StartYear, EndYear = x.EndYear, Notes = x.Notes }).ToList(),
        Progress = p.Progress.OrderByDescending(x => x.Year).Select(x => new LoyaltyProgressDto { Year = x.Year, Nights = x.Nights, Stays = x.Stays, ProgramSpend = x.ProgramSpend, QualifyingPoints = x.QualifyingPoints }).ToList(),
    };

    private static void Apply(LoyaltyProgram e, LoyaltyProgramDto d)
    {
        e.Name = d.Name.Trim(); e.PointValueCents = d.PointValueCents; e.PointsBalance = d.PointsBalance; e.CurrentTier = Mapping.Clean(d.CurrentTier);
        e.TargetTier = Mapping.Clean(d.TargetTier); e.Priority = d.Priority; e.IsActive = d.IsActive; e.Notes = d.Notes;
        e.Tiers.Clear();
        e.Tiers.AddRange(d.Tiers.Where(t => !string.IsNullOrWhiteSpace(t.Name)).Select((t, i) => new LoyaltyTier { Name = t.Name.Trim(), Rank = i, Benefits = Mapping.Clean(t.Benefits) }));
        e.Paths.Clear();
        e.Paths.AddRange(d.Paths.Where(x => !string.IsNullOrWhiteSpace(x.TierName)).Select(x => new StatusPath { TierName = x.TierName.Trim(), Kind = x.Kind, Threshold = x.Threshold, CardId = x.Kind is StatusPathKind.CardSpend or StatusPathKind.HoldCard ? x.CardId : null, StartYear = x.StartYear, EndYear = x.EndYear, Notes = x.Notes }));
        e.Progress.Clear();
        e.Progress.AddRange(d.Progress.GroupBy(x => x.Year).Select(g => g.First()).Select(x => new LoyaltyProgress { Year = x.Year, Nights = x.Nights, Stays = x.Stays, ProgramSpend = x.ProgramSpend, QualifyingPoints = x.QualifyingPoints }));
    }
}

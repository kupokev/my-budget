using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Engines.Rewards;

/// <summary>Everything the optimizer looks at for one year. Navigation properties must be loaded (earn rules with category, thresholds, program; program paths with card, tiers, progress).</summary>
public sealed record RewardsInput(
    int Year,
    DateOnly AsOf,
    IReadOnlyList<Card> Cards,
    IReadOnlyList<LoyaltyProgram> Programs,
    IReadOnlyList<CardSpend> Spend,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<Bill> Bills,
    /// <summary>Monthly accrual per bill id (from the ledger engine), for card-eligible bill spend.</summary>
    IReadOnlyDictionary<int, decimal> BillMonthlyAccrual);

/// <summary>
/// RWD-3..6 and RWD-4a. Works in priority order: status goals first (assign projected card-eligible spend to
/// the co-branded card each goal needs), then route what's left to the best value per category, and say
/// plainly when the spend can't cover every goal and what the other paths are.
/// </summary>
public static class RewardsOptimizer
{
    public static RewardsReportDto Run(RewardsInput input)
    {
        var warnings = new List<string>();
        var monthsLeft = Math.Max(1, 12 - input.AsOf.Month + 1);
        var activeCards = input.Cards.Where(c => c.IsActive).ToList();
        var ytd = activeCards.ToDictionary(c => c.Id, c => input.Spend.Where(s => s.CardId == c.Id && s.Period.Year == input.Year).Sum(s => s.Amount));

        foreach (var c in activeCards.Where(c => c.EarnRules.Count == 0))
            warnings.Add($"{c.Name} has no earn rules; it earns nothing in this plan.");

        // Projected monthly card-eligible spend by category: card-eligible bills' accruals + planned variable spend.
        var projected = new Dictionary<int?, decimal>();
        var sourceParts = new List<string>();
        var eligibleBills = input.Bills.Where(b => b.IsActive && b.IsCardEligible && (b.Category?.IsCardEligible ?? true)).ToList();
        decimal billsTotal = 0, plannedTotal = 0;
        foreach (var b in eligibleBills)
        {
            var amt = input.BillMonthlyAccrual.GetValueOrDefault(b.Id);
            projected[b.CategoryId] = projected.GetValueOrDefault(b.CategoryId) + amt;
            billsTotal += amt;
        }
        foreach (var cat in input.Categories.Where(c => c.IsActive && c.IsCardEligible && c.PlannedMonthly is > 0))
        {
            projected[cat.Id] = projected.GetValueOrDefault(cat.Id) + cat.PlannedMonthly!.Value;
            plannedTotal += cat.PlannedMonthly.Value;
        }
        var projectedMonthly = Round(projected.Values.Sum());
        var projectedSource = $"card-eligible bills {billsTotal:C}/mo + planned variable spend {plannedTotal:C}/mo";
        if (projectedMonthly == 0) warnings.Add("No projected card-eligible spend: set planned monthly amounts on categories or mark bills card-eligible.");

        var thresholds = Thresholds(activeCards, ytd, projectedMonthly, monthsLeft);
        var (programs, plan) = Plan(input, activeCards, ytd, projected, projectedMonthly, monthsLeft, warnings);
        var bills = BillRecommendations(eligibleBills, input, activeCards, plan);
        var earnings = Earnings(activeCards, input.Spend.Where(s => s.Period.Year == input.Year).ToList(), thresholds);
        return new RewardsReportDto(input.Year, input.AsOf, thresholds, programs, plan, bills, earnings, warnings);
    }

    // ---- Thresholds (RWD-3) ----------------------------------------------------------------

    private static List<ThresholdProgressDto> Thresholds(List<Card> cards, Dictionary<int, decimal> ytd, decimal projectedMonthly, int monthsLeft)
    {
        var list = new List<ThresholdProgressDto>();
        foreach (var c in cards)
        foreach (var t in c.Thresholds.OrderBy(t => t.Amount))
        {
            var spent = ytd[c.Id];
            var remaining = Math.Max(0, t.Amount - spent);
            var required = Round(remaining / monthsLeft);
            var projectedYearEnd = spent; // pace is judged in the plan; here the raw "if you keep this card's YTD pace" figure
            var monthsElapsed = Math.Max(1, 12 - monthsLeft);
            projectedYearEnd = Round(spent + spent / monthsElapsed * monthsLeft);
            list.Add(new ThresholdProgressDto(c.Id, c.Name, t.Id, t.Amount, t.RewardKind, t.Description, spent, remaining, required, projectedYearEnd,
                remaining == 0, projectedYearEnd >= t.Amount,
                remaining == 0 ? $"{spent:C} spent ≥ {t.Amount:C}" : $"({t.Amount:C} − {spent:C} YTD) ÷ {monthsLeft} months left = {required:C}/mo; at the YTD pace ({spent:C} ÷ {monthsElapsed} mo) year-end ≈ {projectedYearEnd:C}"));
        }
        return list;
    }

    // ---- Status + spend plan (RWD-2, RWD-4, RWD-6) -------------------------------------------

    private static (List<ProgramStatusDto>, SpendPlanDto) Plan(RewardsInput input, List<Card> cards, Dictionary<int, decimal> ytd,
        Dictionary<int?, decimal> projected, decimal projectedMonthly, int monthsLeft, List<string> warnings)
    {
        var steps = new List<string> { $"{monthsLeft} months left in {input.Year}; projected card-eligible spend {projectedMonthly:C}/mo" };
        var pool = projectedMonthly;
        var allocations = new List<AllocationDto>();
        var gaps = new List<GapDto>();
        var statuses = new List<ProgramStatusDto>();

        foreach (var p in input.Programs.Where(p => p.IsActive).OrderBy(p => p.Priority).ThenBy(p => p.Name))
        {
            var progress = p.Progress.FirstOrDefault(x => x.Year == input.Year);
            var rank = p.Tiers.ToDictionary(t => t.Name, t => t.Rank, StringComparer.OrdinalIgnoreCase);
            int RankOf(string? tier) => tier is not null && rank.TryGetValue(tier, out var r) ? r : -1;

            var paths = p.Paths.Select(path => PathProgress(path, progress, ytd, cards, monthsLeft)).ToList();
            var held = p.Paths.Where(x => x.Kind == StatusPathKind.HoldCard && x.CardId is { } cid && cards.Any(c => c.Id == cid))
                .Select(x => x.TierName).OrderByDescending(RankOf).FirstOrDefault();
            var pointsValue = Round(p.PointsBalance * p.PointValueCents / 100m);

            if (p.TargetTier is null)
            {
                statuses.Add(new ProgramStatusDto(p.Id, p.Name, p.Priority, p.CurrentTier, null, held, false, "no target tier set", null, paths, p.PointsBalance, pointsValue));
                continue;
            }

            var targetPaths = paths.Where(x => string.Equals(x.TierName, p.TargetTier, StringComparison.OrdinalIgnoreCase)).ToList();
            if (targetPaths.Count == 0) warnings.Add($"{p.Name}: no path defined for target tier {p.TargetTier}.");

            if (RankOf(held) >= RankOf(p.TargetTier) && held is not null)
            {
                steps.Add($"{p.Name}: {p.TargetTier} is held for holding a card; no spend needed");
                statuses.Add(new ProgramStatusDto(p.Id, p.Name, p.Priority, p.CurrentTier, p.TargetTier, held, true, $"granted for holding a card ({held})", null, paths, p.PointsBalance, pointsValue));
                continue;
            }
            var reached = targetPaths.FirstOrDefault(x => x.Reached);
            if (reached is not null)
            {
                steps.Add($"{p.Name}: {p.TargetTier} already reached via {Describe(reached)}");
                statuses.Add(new ProgramStatusDto(p.Id, p.Name, p.Priority, p.CurrentTier, p.TargetTier, held, true, $"reached via {Describe(reached)}", reached, paths, p.PointsBalance, pointsValue));
                continue;
            }

            // Plan: the card-spend path gets projected spend, in priority order, until its monthly requirement is met.
            var cardPath = targetPaths.Where(x => x.Kind == StatusPathKind.CardSpend && x.RequiredMonthly is not null).OrderBy(x => x.RequiredMonthly).FirstOrDefault();
            var alternatives = targetPaths.Where(x => x != cardPath && x.Kind != StatusPathKind.HoldCard).Select(Describe).ToList();
            foreach (var hold in targetPaths.Where(x => x.Kind == StatusPathKind.HoldCard))
                alternatives.Add($"hold the {hold.CardName} ({p.TargetTier} for holding it)");

            if (cardPath is null)
            {
                steps.Add($"{p.Name}: no card-spend path to {p.TargetTier}; other paths: {string.Join("; ", alternatives)}");
                statuses.Add(new ProgramStatusDto(p.Id, p.Name, p.Priority, p.CurrentTier, p.TargetTier, held, false, "no card-spend path; see other paths", null, paths, p.PointsBalance, pointsValue));
                continue;
            }

            var card = cards.First(c => c.Name == cardPath.CardName);
            var required = cardPath.RequiredMonthly!.Value;
            var alloc = Round(Math.Min(required, pool));
            pool = Round(pool - alloc);
            if (alloc > 0) allocations.Add(new AllocationDto(card.Id, card.Name, alloc, $"{p.Name} {p.TargetTier}: {cardPath.Formula}"));
            steps.Add($"{p.Name} (priority {p.Priority}): {p.TargetTier} needs {required:C}/mo on {card.Name}; allocated {alloc:C}/mo, {pool:C}/mo left");
            if (alloc < required)
            {
                gaps.Add(new GapDto(p.Name, p.TargetTier, required, alloc, Round(required - alloc), alternatives));
                steps.Add($"{p.Name}: short {required - alloc:C}/mo — {(alternatives.Count > 0 ? "alternatives: " + string.Join("; ", alternatives) : "no other path")}");
            }
            statuses.Add(new ProgramStatusDto(p.Id, p.Name, p.Priority, p.CurrentTier, p.TargetTier, held, false,
                alloc >= required ? $"on plan: {alloc:C}/mo on {card.Name}" : $"short {required - alloc:C}/mo on {card.Name}", cardPath, paths, p.PointsBalance, pointsValue));
        }

        // Route categories: goal cards first (categories they earn most on), remainder to the best value card.
        var routing = new List<CategoryRouteDto>();
        var remainingByCat = projected.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        string CatName(int? id) => id is null ? "Uncategorized" : input.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? $"Category {id}";
        foreach (var a in allocations)
        {
            var card = cards.First(c => c.Id == a.CardId);
            var left = a.Monthly;
            foreach (var catId in remainingByCat.Keys.OrderByDescending(k => CentsPerDollar(card, k)).ThenByDescending(k => remainingByCat[k]).ToList())
            {
                if (left <= 0) break;
                var take = Round(Math.Min(left, remainingByCat[catId]));
                if (take <= 0) continue;
                routing.Add(new CategoryRouteDto(catId, CatName(catId), take, card.Id, card.Name, EarnRate(card, catId), CentsPerDollar(card, catId), $"toward {a.Reason.Split(':')[0]}"));
                remainingByCat[catId] = Round(remainingByCat[catId] - take);
                left = Round(left - take);
            }
        }
        foreach (var (catId, amount) in remainingByCat.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value))
        {
            var best = cards.Where(c => c.EarnRules.Count > 0).OrderByDescending(c => CentsPerDollar(c, catId)).ThenBy(c => c.AnnualFee).FirstOrDefault();
            if (best is null) { routing.Add(new CategoryRouteDto(catId, CatName(catId), amount, null, "—", 0, 0, "no card with earn rules")); continue; }
            routing.Add(new CategoryRouteDto(catId, CatName(catId), amount, best.Id, best.Name, EarnRate(best, catId), CentsPerDollar(best, catId),
                $"best value: {EarnRate(best, catId):0.##}× at {PointValue(best):0.##}¢ = {CentsPerDollar(best, catId):0.##}¢/$"));
        }
        if (routing.Count > 0) steps.Add("Remaining spend routed by earn rate × point value per category.");

        return (statuses, new SpendPlanDto(input.Year, input.AsOf, monthsLeft, projectedMonthly,
            $"card-eligible bills + planned variable spend by category", allocations, routing, gaps, steps));
    }

    private static PathProgressDto PathProgress(StatusPath path, LoyaltyProgress? progress, Dictionary<int, decimal> ytd, List<Card> cards, int monthsLeft)
    {
        var card = path.CardId is { } cid ? cards.FirstOrDefault(c => c.Id == cid) : null;
        decimal current = path.Kind switch
        {
            StatusPathKind.CardSpend => card is null ? 0 : ytd.GetValueOrDefault(card.Id),
            StatusPathKind.HoldCard => card is null ? 0 : 1,
            StatusPathKind.Nights => progress?.Nights ?? 0,
            StatusPathKind.Stays => progress?.Stays ?? 0,
            StatusPathKind.ProgramSpend => progress?.ProgramSpend ?? 0,
            StatusPathKind.QualifyingPoints => progress?.QualifyingPoints ?? 0,
            _ => 0,
        };
        var threshold = path.Kind == StatusPathKind.HoldCard ? 1 : path.Threshold;
        var remaining = Math.Max(0, threshold - current);
        decimal? requiredMonthly = path.Kind == StatusPathKind.CardSpend && card is not null ? Round(remaining / monthsLeft) : null;
        var formula = path.Kind switch
        {
            StatusPathKind.CardSpend when card is null => "card not held",
            StatusPathKind.CardSpend => remaining == 0 ? $"{current:C} spent on {card!.Name} ≥ {threshold:C}" : $"({threshold:C} − {current:C} YTD on {card!.Name}) ÷ {monthsLeft} = {requiredMonthly:C}/mo",
            StatusPathKind.HoldCard => card is null ? "card not held" : $"holding the {card.Name}",
            StatusPathKind.ProgramSpend => $"{current:C} of {threshold:C} eligible spend",
            _ => $"{current:0.#} of {threshold:0.#} {path.Kind.ToString().ToLowerInvariant()}",
        };
        return new PathProgressDto(path.Id, path.TierName, path.Kind, threshold, current, remaining, requiredMonthly, remaining == 0 && (path.Kind != StatusPathKind.HoldCard || card is not null), card?.Name, formula);
    }

    private static string Describe(PathProgressDto p) => p.Kind switch
    {
        StatusPathKind.CardSpend => $"{p.Remaining:C} more on {p.CardName}",
        StatusPathKind.HoldCard => $"holding the {p.CardName}",
        StatusPathKind.ProgramSpend => $"{p.Remaining:C} more eligible program spend",
        StatusPathKind.Nights => $"{p.Remaining:0} more nights",
        StatusPathKind.Stays => $"{p.Remaining:0} more stays",
        StatusPathKind.QualifyingPoints => $"{p.Remaining:N0} more qualifying points",
        _ => p.Formula,
    };

    // ---- Per-bill (RWD-4, RWD-4a) ---------------------------------------------------------------

    private static List<BillRecommendationDto> BillRecommendations(List<Bill> bills, RewardsInput input, List<Card> cards, SpendPlanDto plan)
    {
        var list = new List<BillRecommendationDto>();
        var shortGoalCards = plan.Gaps.Count > 0 ? plan.Allocations.Select(a => a.CardId).ToHashSet() : [];
        foreach (var b in bills.OrderBy(b => b.Name))
        {
            var monthly = input.BillMonthlyAccrual.GetValueOrDefault(b.Id);
            var route = plan.Routing.Where(r => r.CategoryId == b.CategoryId && r.CardId is not null).OrderByDescending(r => r.Monthly).FirstOrDefault();
            var card = route is null ? cards.Where(c => c.EarnRules.Count > 0).OrderByDescending(c => CentsPerDollar(c, b.CategoryId)).FirstOrDefault() : cards.First(c => c.Id == route.CardId);
            if (card is null) { list.Add(new BillRecommendationDto(b.Id, b.Name, monthly, null, "no card with earn rules", 0, b.BankAutopayDiscount ?? 0, "")); continue; }
            var cents = CentsPerDollar(card, b.CategoryId);
            var cardValue = Round(monthly * cents / 100m);
            var discount = b.BankAutopayDiscount ?? 0;
            var goalShort = shortGoalCards.Contains(card.Id);
            var useBank = discount > 0 && discount >= cardValue && !goalShort;
            var rec = useBank ? $"Pay from bank: {discount:C} discount beats {cardValue:C} in rewards"
                : goalShort ? $"{card.Name}: every dollar counts toward a status goal that is short" + (discount > 0 ? $" (forgoing the {discount:C} bank discount)" : "")
                : $"{card.Name}: {cardValue:C}/mo in rewards" + (discount > 0 ? $" beats the {discount:C} bank discount" : "");
            list.Add(new BillRecommendationDto(b.Id, b.Name, monthly, useBank ? null : card.Id, rec, cardValue, discount,
                $"{monthly:C} × {EarnRate(card, b.CategoryId):0.##} pts/$ × {PointValue(card):0.##}¢ = {cardValue:C}" + (discount > 0 ? $" vs bank discount {discount:C}" : "")));
        }
        return list;
    }

    // ---- Earnings (RWD-5) -------------------------------------------------------------------------

    private static List<CardEarningsDto> Earnings(List<Card> cards, List<CardSpend> spend, List<ThresholdProgressDto> thresholds)
    {
        var list = new List<CardEarningsDto>();
        foreach (var c in cards)
        {
            var mine = spend.Where(s => s.CardId == c.Id).ToList();
            var months = mine.GroupBy(s => s.Period).OrderBy(g => g.Key).Select(g =>
            {
                var pts = g.Sum(s => s.Amount * EarnRate(c, s.CategoryId));
                return new MonthEarningsDto(g.Key, Round(g.Sum(s => s.Amount)), Round(pts), Round(pts * PointValue(c) / 100m));
            }).ToList();
            var ytdSpend = months.Sum(m => m.Spend);
            var ytdPoints = months.Sum(m => m.Points);
            var ytdDollars = Round(ytdPoints * PointValue(c) / 100m);
            var rewardsValue = thresholds.Where(t => t.CardId == c.Id && t.Reached).Sum(t => c.Thresholds.First(x => x.Id == t.ThresholdId).ValueDollars ?? 0);
            var net = Round(ytdDollars + rewardsValue - c.AnnualFee);
            list.Add(new CardEarningsDto(c.Id, c.Name, PointValue(c), months, ytdSpend, ytdPoints, ytdDollars, c.AnnualFee, rewardsValue, net,
                $"{ytdPoints:N0} pts × {PointValue(c):0.##}¢ = {ytdDollars:C}" + (rewardsValue > 0 ? $" + reached thresholds {rewardsValue:C}" : "") + $" − annual fee {c.AnnualFee:C} = {net:C}"));
        }
        return list.OrderByDescending(e => e.NetValue).ToList();
    }

    // ---- helpers ----------------------------------------------------------------------------------

    public static decimal EarnRate(Card c, int? categoryId)
        => (categoryId is not null ? c.EarnRules.FirstOrDefault(r => r.CategoryId == categoryId) : null)?.PointsPerDollar
           ?? c.EarnRules.FirstOrDefault(r => r.CategoryId is null)?.PointsPerDollar ?? 0m;

    public static decimal PointValue(Card c) => c.PointValueCents ?? c.LoyaltyProgram?.PointValueCents ?? 1.0m;

    public static decimal CentsPerDollar(Card c, int? categoryId) => Round4(EarnRate(c, categoryId) * PointValue(c));

    private static decimal Round(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    private static decimal Round4(decimal d) => Math.Round(d, 4, MidpointRounding.AwayFromZero);
}

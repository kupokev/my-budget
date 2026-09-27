using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

public class RewardsEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public RewardsEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_card_carries_its_own_earn_rules_thresholds_and_program_link()
    {
        var cards = await _api.Get<List<CardDto>>("api/cards");
        var ihg = cards.Single(c => c.Name == "Chase IHG One Rewards Premier");
        var rewards = await _api.Get<CardRewardsDto>($"api/cards/{ihg.Id}/rewards");
        Assert.Contains(rewards.EarnRules, r => r.CategoryId is null && r.PointsPerDollar == 3m);
        Assert.Contains(rewards.Thresholds, t => t.Amount == 40_000m && t.TierName == "Diamond");
        Assert.NotNull(rewards.LoyaltyProgramId);

        var programs = await _api.Get<List<LoyaltyProgramDto>>("api/loyalty-programs");
        var ihgProg = programs.Single(p => p.Name == "IHG One Rewards");
        Assert.Contains(ihgProg.Paths, x => x.Kind == StatusPathKind.CardSpend && x.CardId == ihg.Id && x.Threshold == 40_000m);
        Assert.Contains(ihgProg.Paths, x => x.Kind == StatusPathKind.HoldCard && x.TierName == "Platinum");

        // The tier ladder carries what each tier actually gets you, and keeps its order.
        Assert.Equal(["Club", "Silver", "Gold", "Platinum", "Diamond"], ihgProg.Tiers.Select(t => t.Name));
        Assert.Contains("breakfast", ihgProg.Tiers.Single(t => t.Name == "Diamond").Benefits!);

        // A cash-back card is the same machinery: no program, a point worth 1¢.
        var citi = cards.Single(c => c.Name == "Citi Double Cash");
        var citiRewards = await _api.Get<CardRewardsDto>($"api/cards/{citi.Id}/rewards");
        Assert.Null(citiRewards.LoyaltyProgramId);
        Assert.Equal(1.0m, citiRewards.PointValueCents);
        Assert.Equal(2m, Assert.Single(citiRewards.EarnRules).PointsPerDollar);
    }

    [Fact]
    public async Task An_earn_rule_carries_the_years_it_covers()
    {
        var cards = await _api.Get<List<CardDto>>("api/cards");
        var freedom = cards.Single(c => c.Name == "Chase Freedom Unlimited");
        var rewards = await _api.Get<CardRewardsDto>($"api/cards/{freedom.Id}/rewards");
        var promo = rewards.EarnRules.Single(r => r.StartYear == 2026 && r.EndYear == 2026);
        Assert.Equal(5m, promo.PointsPerDollar);
        Assert.Contains(rewards.EarnRules, r => r.CategoryId is null && r.StartYear is null && r.PointsPerDollar == 1.5m);

        // Editing keeps the years.
        rewards.EarnRules.Add(new EarnRuleDto { PointsPerDollar = 4, StartYear = 2027 });
        var saved = await _api.Put($"api/cards/{freedom.Id}/rewards", rewards);
        Assert.Contains(saved.EarnRules, r => r.PointsPerDollar == 4m && r.StartYear == 2027 && r.EndYear is null);
        saved.EarnRules.RemoveAll(r => r.PointsPerDollar == 4m);
        await _api.Put($"api/cards/{freedom.Id}/rewards", saved);
    }

    [Fact]
    public async Task Report_shows_2026_progress_and_a_2027_plan_from_zero()
    {
        var r = await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26");
        Assert.Equal(4, r.Plan.MonthsLeft);
        var ihg = r.Programs.Single(p => p.Name == "IHG One Rewards");
        Assert.Equal("Platinum", ihg.HeldTier);                         // Platinum comes free with the card
        Assert.True(ihg.TargetReached);                                 // Diamond was bought with 2025's $40,000 and is held all of 2026
        Assert.Contains("already Diamond", ihg.HowReached);
        Assert.Equal(9 * 1_390m, ihg.Paths.Single(x => x.Kind == StatusPathKind.CardSpend).Current);   // seeded YTD spend
        // Status from a card that belongs to another brand entirely.
        var hertz = r.Programs.Single(p => p.Name == "Hertz Gold Plus Rewards");
        Assert.True(hertz.TargetReached);
        Assert.Equal("Five Star", hertz.HeldTier);
        // A benefit with no spend threshold behind it still offsets the fee.
        var premier = r.Earnings.Single(e => e.CardName == "Chase IHG One Rewards Premier");
        Assert.Equal(170m, premier.PerksValue);
        Assert.Contains(r.BudgetLines, b => b.LineName == "AT&T" && b.BankDiscount == 5m);
        Assert.DoesNotContain(r.BudgetLines, b => b.LineName == "Mortgage"); // not card-eligible
        Assert.Contains(r.Earnings, e => e.YtdPoints > 0);

        var next = await _api.Get<RewardsReportDto>("api/rewards/report?year=2027&asOf=2026-09-26");
        Assert.Equal(12, next.Plan.MonthsLeft);
        Assert.All(next.Thresholds, t => Assert.Equal(0m, t.YtdSpend));
        Assert.Equal(Math.Round(40_000m / 12, 2), next.Programs.Single(p => p.Name == "IHG One Rewards").PlannedPath!.RequiredMonthly);
    }

    [Fact]
    public async Task A_card_transaction_entered_by_hand_moves_the_threshold_straight_away()
    {
        var cards = await _api.Get<List<CardDto>>("api/cards");
        var surpass = cards.Single(c => c.Name == "Amex Hilton Honors Surpass");
        Threshold Before() => new(_api, surpass.Id);
        var before = await Before().Read();

        // There is no card-spend table any more: the figures are summed from transactions, so a row
        // typed on the Transactions page counts immediately.
        var created = await _api.Post<TransactionCreateDto, TransactionDto>("api/transactions", new TransactionCreateDto
        {
            CardId = surpass.Id, Date = new(2026, 9, 15), Amount = -1_000m, Description = "Hotel stay",
        });
        var after = await Before().Read();
        Assert.Equal(before + 1_000m, after);

        // And removing it takes the spend back off.
        (await _api.Client.DeleteAsync($"api/transactions/{created.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(before, await Before().Read());
    }

    private sealed record Threshold(ApiFixture Api, int CardId)
    {
        public async Task<decimal> Read() =>
            (await Api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26"))
            .Thresholds.Single(t => t.CardId == CardId && t.Amount == 15_000m).YtdSpend;
    }

    [Fact]
    public async Task Adding_a_card_by_hand_and_giving_it_a_hold_the_card_path_grants_the_tier()
    {
        // Exactly the flow on the page: New card, then a status path on the program that says holding it grants Diamond.
        var created = await _api.Post("api/cards", new CardDto { Name = "Amex Hilton Honors Aspire", Issuer = "American Express", Network = "Amex", AnnualFee = 550m, AnnualFeeMonth = 1, StatementDay = 5, DueDay = 2 });
        var hiltonProgram = (await _api.Get<List<LoyaltyProgramDto>>("api/loyalty-programs")).Single(p => p.Name == "Hilton Honors");
        await _api.Put($"api/cards/{created.Id}/rewards", new CardRewardsDto
        {
            CardId = created.Id, LoyaltyProgramId = hiltonProgram.Id,
            EarnRules = [new EarnRuleDto { PointsPerDollar = 3 }],
        });
        hiltonProgram.Paths.Add(new StatusPathDto { TierName = "Diamond", Kind = StatusPathKind.HoldCard, CardId = created.Id, Notes = "for holding the Aspire" });
        await _api.Put($"api/loyalty-programs/{hiltonProgram.Id}", hiltonProgram);

        var r = await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26");
        var hilton = r.Programs.Single(p => p.Name == "Hilton Honors");
        Assert.True(hilton.TargetReached);
        Assert.Equal("Diamond", hilton.HeldTier);

        hiltonProgram.Paths.RemoveAll(x => x.CardId == created.Id);
        await _api.Put($"api/loyalty-programs/{hiltonProgram.Id}", hiltonProgram);
        await _api.Client.DeleteAsync($"api/cards/{created.Id}");
    }

    [Fact]
    public async Task Tier_benefits_round_trip_and_reach_the_status_report()
    {
        var program = (await _api.Get<List<LoyaltyProgramDto>>("api/loyalty-programs")).Single(p => p.Name == "Hertz Gold Plus Rewards");
        program.Tiers.Single(t => t.Name == "Five Star").Benefits = "Free single upgrade and a wider aisle";
        var saved = await _api.Put($"api/loyalty-programs/{program.Id}", program);
        Assert.Equal("Free single upgrade and a wider aisle", saved.Tiers.Single(t => t.Name == "Five Star").Benefits);

        // The Rewards status page reads them off the report, not off the program list.
        var report = await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26");
        var hertz = report.Programs.Single(p => p.Name == "Hertz Gold Plus Rewards");
        Assert.Equal(["Gold", "Five Star", "President's Circle"], hertz.Tiers.Select(t => t.Name));
        Assert.Equal("Free single upgrade and a wider aisle", hertz.Tiers.Single(t => t.Name == "Five Star").Benefits);
    }
}

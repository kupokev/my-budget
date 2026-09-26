using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

public class RewardsEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public RewardsEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Catalog_cards_come_with_earn_rules_thresholds_and_program_paths()
    {
        var catalog = await _api.Get<List<CatalogEntryDto>>("api/card-catalog");
        Assert.Contains(catalog, c => c.Key == "chase-ihg-premier" && c.Program == "IHG One Rewards");

        var cards = await _api.Get<List<CardDto>>("api/cards");
        var ihg = cards.Single(c => c.CatalogKey == "chase-ihg-premier");
        var rewards = await _api.Get<CardRewardsDto>($"api/cards/{ihg.Id}/rewards");
        Assert.Contains(rewards.EarnRules, r => r.CategoryId is null && r.PointsPerDollar == 3m);
        Assert.Contains(rewards.Thresholds, t => t.Amount == 40_000m && t.TierName == "Diamond");

        var programs = await _api.Get<List<LoyaltyProgramDto>>("api/loyalty-programs");
        var ihgProg = programs.Single(p => p.Name == "IHG One Rewards");
        Assert.Contains(ihgProg.Paths, x => x.Kind == StatusPathKind.CardSpend && x.CardId == ihg.Id && x.Threshold == 40_000m);
        Assert.Contains(ihgProg.Paths, x => x.Kind == StatusPathKind.HoldCard && x.TierName == "Platinum");
    }

    [Fact]
    public async Task Report_shows_2026_progress_and_a_2027_plan_from_zero()
    {
        var r = await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26");
        Assert.Equal(4, r.Plan.MonthsLeft);
        var ihg = r.Programs.Single(p => p.Name == "IHG One Rewards");
        Assert.Equal("Platinum", ihg.HeldTier);
        Assert.NotNull(ihg.PlannedPath);
        Assert.Equal(9 * 1_390m, ihg.PlannedPath!.Current);           // seeded YTD spend
        Assert.NotEmpty(r.Plan.Gaps);                                   // both Diamonds can't be hit on the seed's planned spend
        Assert.Contains(r.Bills, b => b.BillName == "AT&T" && b.BankDiscount == 5m);
        Assert.DoesNotContain(r.Bills, b => b.BillName == "Mortgage"); // not card-eligible
        Assert.Contains(r.Earnings, e => e.YtdPoints > 0);

        var next = await _api.Get<RewardsReportDto>("api/rewards/report?year=2027&asOf=2026-09-26");
        Assert.Equal(12, next.Plan.MonthsLeft);
        Assert.All(next.Thresholds, t => Assert.Equal(0m, t.YtdSpend));
        Assert.Equal(Math.Round(40_000m / 12, 2), next.Programs.Single(p => p.Name == "IHG One Rewards").PlannedPath!.RequiredMonthly);
    }

    [Fact]
    public async Task Card_spend_upsert_changes_threshold_progress()
    {
        var cards = await _api.Get<List<CardDto>>("api/cards");
        var surpass = cards.Single(c => c.CatalogKey == "amex-hilton-surpass");
        var before = (await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26")).Thresholds.Single(t => t.CardId == surpass.Id && t.Amount == 15_000m);

        var r = await _api.Client.PutAsJsonAsync("api/card-spend", new CardSpendDto { CardId = surpass.Id, Period = new(2026, 9, 15), CategoryId = null, Amount = 1_000m }, ApiFixture.Json);
        r.EnsureSuccessStatusCode();
        var after = (await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26")).Thresholds.Single(t => t.CardId == surpass.Id && t.Amount == 15_000m);
        Assert.Equal(before.YtdSpend + 1_000m, after.YtdSpend);

        await _api.Client.PutAsJsonAsync("api/card-spend", new CardSpendDto { CardId = surpass.Id, Period = new(2026, 9, 1), CategoryId = null, Amount = 0m }, ApiFixture.Json);
    }

    [Fact]
    public async Task Adding_the_Aspire_from_the_catalog_makes_Hilton_Diamond_held()
    {
        var created = await _api.Post<object, CardDto>("api/cards/from-catalog/amex-hilton-aspire", new { });
        Assert.Equal("Amex Hilton Honors Aspire", created.Name);
        var r = await _api.Get<RewardsReportDto>("api/rewards/report?year=2026&asOf=2026-09-26");
        var hilton = r.Programs.Single(p => p.Name == "Hilton Honors");
        Assert.True(hilton.TargetReached);
        Assert.Equal("Diamond", hilton.HeldTier);
        await _api.Client.DeleteAsync($"api/cards/{created.Id}");
    }
}

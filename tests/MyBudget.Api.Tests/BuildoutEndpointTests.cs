using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

public class BuildoutEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public BuildoutEndpointTests(ApiFixture api) => _api = api;

    private async Task<HoldingDto> NewHolding(string ticker, bool drip)
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var brokerage = accounts.Single(a => a.Name == "Fidelity Brokerage");
        var h = await _api.Post("api/investments/holdings", new HoldingDto { Ticker = ticker, AccountId = brokerage.Id, Drip = drip });
        await _api.Post("api/investments/trades", new TradeDto { HoldingId = h.Id, Date = new(2025, 3, 3), Kind = TradeKind.Buy, Shares = 10, Price = 280m });
        return h;
    }

    [Fact]
    public async Task Portfolio_values_holdings_realizes_gains_and_flags_wash_sales()
    {
        var vti = await NewHolding("TST1", drip: false);
        await _api.Client.PostAsync($"api/investments/prices?ticker=TST1&date=2026-09-25&price=300", null);
        var before = await _api.Get<PortfolioDto>("api/investments/portfolio?year=2026&asOf=2026-09-26");
        var pos = before.Positions.Single(p => p.Holding.Ticker == "TST1");
        Assert.Equal(10m, pos.Shares);
        Assert.Equal(2_800m, pos.CostBasis);
        Assert.Equal(3_000m, pos.MarketValue);
        Assert.Equal(200m, pos.UnrealizedGain);

        // Sell 4 at a loss then buy back inside the window → wash sale; sale after 1 year is long-term.
        await _api.Post("api/investments/trades", new TradeDto { HoldingId = vti.Id, Date = new(2026, 9, 1), Kind = TradeKind.Sell, Shares = 4, Price = 270m });
        await _api.Post("api/investments/trades", new TradeDto { HoldingId = vti.Id, Date = new(2026, 9, 10), Kind = TradeKind.Buy, Shares = 2, Price = 275m });
        var after = await _api.Get<PortfolioDto>("api/investments/portfolio?year=2026&asOf=2026-09-26");
        var p2 = after.Positions.Single(p => p.Holding.Ticker == "TST1");
        var gain = Assert.Single(p2.Realized);
        Assert.Equal("Long", gain.Term);
        Assert.True(gain.WashSale);
        Assert.Equal(20m, gain.DisallowedLoss);                          // 2 of 4 shares replaced → half of the $40 loss
        Assert.Equal(-20m, gain.Gain);
        Assert.Single(p2.WashSales);
        Assert.Equal(8m, p2.Shares);
        Assert.NotNull(after.Tax);
        Assert.Equal(0m, after.Tax!.Total);                              // a net loss owes nothing
        await _api.Client.DeleteAsync($"api/investments/holdings/{vti.Id}");
    }

    [Fact]
    public async Task Manual_dividend_on_a_drip_holding_creates_a_reinvest_trade()
    {
        var vti = await NewHolding("TST2", drip: true);
        await _api.Client.PostAsync($"api/investments/prices?ticker=TST2&date=2026-06-30&price=280", null);
        var d = await _api.Post("api/investments/dividends", new DividendDto { HoldingId = vti.Id, ExDate = new(2026, 6, 26), PerShare = 0.9m });
        Assert.Equal(10m, d.SharesHeld);
        Assert.Equal(9m, d.Amount);
        var pf = await _api.Get<PortfolioDto>("api/investments/portfolio?year=2026&asOf=2026-07-01");
        var pos = pf.Positions.Single(p => p.Holding.Ticker == "TST2");
        var reinvest = pos.Trades.Single(t => t.Kind == TradeKind.Reinvest);
        Assert.Equal(d.Id, reinvest.DividendPaymentId);
        Assert.Equal(Math.Round(9m / 280m, 6), reinvest.Shares);
        Assert.Equal(9m, pos.DividendsThisYear);
        await _api.Client.DeleteAsync($"api/investments/holdings/{vti.Id}");
    }

    [Fact]
    public async Task Receivable_ledger_shows_prepaid_missed_and_one_off_balances()
    {
        var ledgers = await _api.Get<List<PersonLedgerDto>>("api/people/ledgers");
        var robin = ledgers.Single(l => l.Person.Name == "Robin");
        Assert.Equal("Paid", robin.Periods.Single(r => r.Period == new DateOnly(2026, 3, 1)).Status);   // prepaid in January
        Assert.Equal("Missed", robin.Periods.Single(r => r.Period == new DateOnly(2026, 5, 1)).Status);
        Assert.Equal(120m, robin.OneOffCharged);
        Assert.Equal(0m, robin.OneOffBalance);
        Assert.True(robin.TotalOwed > 0);
        var sam = ledgers.Single(l => l.Person.Name == "Sam");
        Assert.Equal(250m, sam.Periods.First().Expected);                 // 100% of the Amex loan line
        Assert.Equal("Due", sam.Periods.Single(r => r.Period == new DateOnly(2026, 9, 1)).Status);
    }

    [Fact]
    public async Task A_quarterly_obligation_is_only_expected_every_third_month()
    {
        var people = await _api.Get<List<PersonDto>>("api/people");
        var robin = people.Single(p => p.Name == "Robin");
        robin.Obligations.Add(new ObligationDto { Description = "Blueland", MonthlyAmount = 11m, EveryMonths = 3, StartPeriod = new(2026, 2, 1) });
        await _api.Put($"api/people/{robin.Id}", robin);

        var ledger = (await _api.Get<List<PersonLedgerDto>>("api/people/ledgers")).Single(l => l.Person.Id == robin.Id);
        decimal Expected(int month) => ledger.Periods.Single(r => r.Period == new DateOnly(2026, month, 1)).Expected;
        Assert.Equal(45m, Expected(1));                       // phone only, before it starts
        Assert.Equal(45m + 11m, Expected(2));                 // first due
        Assert.Equal(45m, Expected(3));
        Assert.Equal(45m, Expected(4));
        Assert.Equal(45m + 11m, Expected(5));                 // every third month
        Assert.Equal(45m + 11m, Expected(8));
        Assert.Contains("Blueland (quarterly)", ledger.Periods.Single(r => r.Period == new DateOnly(2026, 8, 1)).Detail);

        robin.Obligations.RemoveAll(o => o.Description == "Blueland");
        await _api.Put($"api/people/{robin.Id}", robin);
    }

    [Fact]
    public async Task Self_employment_estimate_annualizes_receipts_and_schedules_the_remaining_quarters()
    {
        var se = await _api.Get<SelfEmploymentDto>("api/side-income/estimate?year=2026");
        Assert.Equal(10_500m, se.NetProfitYtd);                           // 4,000 + 6,500 seeded
        Assert.Equal(2, se.Sources.Count);
        Assert.True(se.SetAsideFraction > 0.2m);
        Assert.Equal(4, se.Schedule.Count);
        Assert.Contains(se.Steps, s => s.StartsWith("SE earnings"));

        var fixedProjection = await _api.Get<SelfEmploymentDto>("api/side-income/estimate?year=2026&projected=20000");
        Assert.Equal(20_000m, fixedProjection.ProjectedAnnual);
        Assert.Equal("your projection", fixedProjection.ProjectionMethod);
    }

    [Fact]
    public async Task Dashboard_and_alerts_and_rainy_day_come_back_together()
    {
        var d = await _api.Get<HomeDashboardDto>("api/home/dashboard");
        Assert.NotEmpty(d.Needs);
        Assert.NotEmpty(d.Programs);
        Assert.NotEmpty(d.Goals);
        Assert.Contains(d.Alerts, a => a.Kind == "tax");                  // 2026 tables unverified
        Assert.DoesNotContain(d.Alerts, a => a.Kind == "rewards");        // rewards stay off Home; they live on the Cards page
        var rewardsAlerts = await _api.Get<List<AlertDto>>("api/alerts?kind=rewards");
        Assert.NotEmpty(rewardsAlerts);                                    // both Diamonds short on the seed
        Assert.All(rewardsAlerts, a => Assert.Equal("rewards", a.Kind));
        Assert.Equal(2, d.RainyDay.Accounts.Count);                       // T-BudgetLine + Wealthfront marked
        Assert.False(d.AiEnabled);

        await _api.Client.PostAsync("api/investments/prices?ticker=VTI&date=2026-09-25&price=300", null); // seeded holding gets a price
        var nw = await _api.Get<NetWorthDto>("api/reports/net-worth");
        Assert.Contains(nw.Lines, l => l.Kind == "home" && l.Balance == 371_200m);   // latest of the seeded monthly valuations
        Assert.Contains(nw.Lines, l => l.Kind == "investments");
    }

    [Fact]
    public async Task Ai_is_off_by_default_and_says_so()
    {
        var s = await _api.Get<AiStatusDto>("api/ai/status");
        Assert.False(s.Enabled);
        Assert.Contains("spend_by_category", s.Tools);
        var r = await _api.Client.PostAsJsonAsync("api/ai/chat", new ChatRequest { Messages = [new() { Role = "user", Content = "hi" }] }, ApiFixture.Json);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task An_asset_carries_its_valuation_history_and_the_loans_secured_against_it()
    {
        var assets = await _api.Get<List<AssetDto>>("api/assets");
        var house = assets.Single(a => a.Name == "House");

        // History is newest first, and each record knows the move from the one before it.
        Assert.Equal(21, house.History.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), house.History[0].AsOf);
        Assert.Equal(371_200m, house.History[0].Value);
        Assert.Equal(2_100m, house.History[0].Change);
        Assert.Null(house.History[^1].Change);                       // the first record has nothing to compare to
        Assert.Contains(house.History, p => p.Change < 0);           // the market dips as well as rises

        // Both loans are secured against it, so equity is value less what's owed.
        Assert.Equal(2, house.Loans.Count);
        Assert.Equal(314_400m, house.LoanBalance);                   // 283,000 mortgage + 31,400 equity loan
        Assert.Equal(56_800m, house.Equity);
        Assert.Contains("Home equity loan", house.EquityFormula);

        // The car has no loan against it, so its equity is simply its value.
        var car = assets.Single(a => a.Name == "Car");
        Assert.Empty(car.Loans);
        Assert.Equal(18_500m, car.Equity);
        Assert.Contains("nothing secured against it", car.EquityFormula!);
        Assert.True(car.ChangeSincePrior < 0);                        // it depreciates
    }

    [Fact]
    public async Task Recording_and_removing_a_valuation_reshapes_the_history()
    {
        var car = (await _api.Get<List<AssetDto>>("api/assets")).Single(a => a.Name == "Car");
        var before = car.History.Count;

        var added = await _api.Post<AssetValueDto, AssetDto>($"api/assets/{car.Id}/values",
            new AssetValueDto { AssetId = car.Id, AsOf = new(2026, 10, 1), Value = 18_100m });
        Assert.Equal(before + 1, added.History.Count);
        Assert.Equal(18_100m, added.History[0].Value);
        Assert.Equal(-400m, added.History[0].Change);

        // Recording the same date again replaces rather than duplicates.
        var replaced = await _api.Post<AssetValueDto, AssetDto>($"api/assets/{car.Id}/values",
            new AssetValueDto { AssetId = car.Id, AsOf = new(2026, 10, 1), Value = 18_000m });
        Assert.Equal(before + 1, replaced.History.Count);
        Assert.Equal(18_000m, replaced.History[0].Value);

        var removed = await _api.Client.DeleteAsync($"api/assets/{car.Id}/values/{replaced.History[0].Id}");
        removed.EnsureSuccessStatusCode();
        Assert.Equal(before, (await _api.Get<List<AssetDto>>("api/assets")).Single(a => a.Name == "Car").History.Count);
    }

    [Fact]
    public async Task Ai_connection_details_are_edited_in_the_app_not_a_config_file()
    {
        // A settings row exists from startup, so there is always something to edit.
        var before = await _api.Get<AppSettingsDto>("api/settings");
        Assert.False(before.AiEnabled);
        Assert.False(string.IsNullOrWhiteSpace(before.AiBaseUrl));

        var saved = await _api.Put<AppSettingsDto, AppSettingsDto>("api/settings", new AppSettingsDto
        {
            AiEnabled = true, AiBaseUrl = "http://nas.local:11434/", AiModel = "  qwen2.5:14b  ", AiMaxToolRounds = 99,
        });

        Assert.True(saved.AiEnabled);
        Assert.Equal("http://nas.local:11434", saved.AiBaseUrl);   // trailing slash trimmed
        Assert.Equal("qwen2.5:14b", saved.AiModel);                     // trimmed
        Assert.Equal(20, saved.AiMaxToolRounds);                        // clamped

        // The assistant reads those settings rather than configuration.
        var status = await _api.Get<AiStatusDto>("api/ai/status");
        Assert.True(status.Enabled);
        Assert.Equal("http://nas.local:11434", status.BaseUrl);
        Assert.Equal("qwen2.5:14b", status.Model);
        Assert.NotEmpty(status.Tools);

        // And the dashboard's AI flag follows the same row.
        Assert.True((await _api.Get<HomeDashboardDto>("api/home/dashboard")).AiEnabled);

        await _api.Put<AppSettingsDto, AppSettingsDto>("api/settings", before);
    }

    [Fact]
    public async Task Probing_an_address_that_is_not_there_reports_it_rather_than_throwing()
    {
        var probe = await _api.Get<AiProbeDto>("api/settings/ai/probe?baseUrl=http://127.0.0.1:1");
        Assert.False(probe.Reachable);
        Assert.Empty(probe.Models);
        Assert.False(string.IsNullOrWhiteSpace(probe.Error));
    }
}

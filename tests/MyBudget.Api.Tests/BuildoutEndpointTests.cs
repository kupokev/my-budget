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
        var brokerage = accounts.Single(a => a.Type == AccountType.Brokerage);
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
        Assert.Equal(250m, sam.Periods.First().Expected);                 // 100% of the Amex loan bill
        Assert.Equal("Due", sam.Periods.Single(r => r.Period == new DateOnly(2026, 9, 1)).Status);
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
        Assert.Equal(2, d.RainyDay.Accounts.Count);                       // T-Bill + Wealthfront marked
        Assert.False(d.AiEnabled);

        await _api.Client.PostAsync("api/investments/prices?ticker=VTI&date=2026-09-25&price=300", null); // seeded holding gets a price
        var nw = await _api.Get<NetWorthDto>("api/reports/net-worth");
        Assert.Contains(nw.Lines, l => l.Kind == "home" && l.Balance == 385_000m);
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
}

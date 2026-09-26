using System.Net;
using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

public class LedgerEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public LedgerEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Health_is_anonymous_but_everything_else_needs_the_key()
    {
        var health = await _api.Anonymous.GetFromJsonAsync<HealthDto>("health", ApiFixture.Json);
        Assert.Equal("InMemory", health!.Provider);

        var denied = await _api.Anonymous.GetAsync("api/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        var ok = await _api.Client.GetAsync("api/accounts");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Seeded_transfer_needs_roll_card_paid_bills_into_their_funding_account()
    {
        var needs = await _api.Get<TransferNeedsDto>("api/transfer-needs?asOf=2026-09-26");

        Assert.Equal(26, needs.PaychecksPerYear); // bi-weekly since 2026-02-01
        var automated = Assert.Single(needs.Accounts, a => a.AccountName == "Chase Automated Bills");
        var hulu = Assert.Single(automated.Lines, l => l.BillName == "Hulu");
        Assert.True(hulu.PaidByCard);
        Assert.Equal(18.99m, hulu.MonthlyAccrual);
        Assert.DoesNotContain(needs.Accounts, a => a.AccountName.Contains("IHG"));

        var savings = Assert.Single(needs.Accounts, a => a.AccountName == "Chase Premier Savings");
        Assert.Equal(102m + 10m + 5.42m, savings.MonthlyNeed); // car insurance 612/6, AAA 120/12, Costco 65/12
        Assert.Equal(Math.Round(savings.MonthlyNeed * 12 / 26, 2), savings.PerPaycheckNeed);
        Assert.Contains("612", savings.Lines.Single(l => l.BillName == "Car insurance").Formula);
    }

    [Fact]
    public async Task Bill_actual_upsert_shows_variance_in_history()
    {
        var bills = await _api.Get<List<BillDto>>("api/bills");
        var electric = bills.Single(b => b.Name == "Electric");

        await _api.Put($"api/bills/{electric.Id}/actuals/2026-09-15", new BillActualDto { BillId = electric.Id, Period = new(2026, 9, 1), Amount = 151.20m });
        await _api.Put($"api/bills/{electric.Id}/actuals/2026-09-01", new BillActualDto { BillId = electric.Id, Period = new(2026, 9, 1), Amount = 149.50m }); // same month → overwrite

        var history = await _api.Get<List<BillHistoryDto>>("api/bills/history?year=2026");
        var row = history.Single(h => h.BillId == electric.Id);
        var sep = row.Months.Single(m => m.Period == new DateOnly(2026, 9, 1));
        Assert.Equal(149.50m, sep.Actual);
        Assert.Equal(140m, sep.Projected);
        Assert.Equal(9.50m, sep.Variance);
        Assert.Equal(149.50m, row.AverageActual);
    }

    [Fact]
    public async Task Bill_validation_requires_a_card_when_paid_by_card()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var dto = new BillDto { Name = "Bad bill", ProjectedAmount = 1m, PaymentMethod = PaymentMethodKind.Card, FundingAccountId = accounts[0].Id, DueDay = 1 };
        var r = await _api.Client.PostAsJsonAsync("api/bills", dto, ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("PaymentCardId", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Pay_calendar_marks_2026_three_check_months()
    {
        var cal = await _api.Get<PayCalendarDto>("api/income-sources/pay-calendar?year=2026");
        Assert.Contains(cal.ThreePaycheckMonths, m => m.StartsWith("May 2026"));
        Assert.Contains(cal.ThreePaycheckMonths, m => m.StartsWith("October 2026"));
        Assert.Equal(26, cal.PayDates.Count);
        Assert.Equal(new DateOnly(2026, 5, 29), cal.PayDates.Single(d => d.ThirdCheckOfMonth && d.Date.Month == 5).Date);
    }

    [Fact]
    public async Task Home_lists_upcoming_bills_and_next_pay_date()
    {
        var home = await _api.Get<HomeDto>("api/home");
        Assert.NotNull(home.NextPayDate);
        Assert.NotEmpty(home.NeedsThisPayPeriod);
        Assert.All(home.UpcomingBills, b => Assert.InRange(b.DueDate.DayNumber - home.AsOf.DayNumber, 0, 14));
    }

    [Fact]
    public async Task Account_referenced_by_a_bill_cannot_be_deleted()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var main = accounts.Single(a => a.Name == "Chase Main");
        var r = await _api.Client.DeleteAsync($"api/accounts/{main.Id}");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }
}

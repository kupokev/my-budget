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
        var hulu = Assert.Single(automated.Lines, l => l.LineName == "Hulu");
        Assert.True(hulu.PaidByCard);
        Assert.Equal(18.99m, hulu.MonthlyAccrual);
        Assert.DoesNotContain(needs.Accounts, a => a.AccountName.Contains("IHG"));

        var savings = Assert.Single(needs.Accounts, a => a.AccountName == "Chase Premier Savings");
        Assert.Equal(102m + 10m + 5.42m, savings.MonthlyNeed); // car insurance 612/6, AAA 120/12, Costco 65/12
        Assert.Equal(Math.Round(savings.MonthlyNeed * 12 / 26, 2), savings.PerPaycheckNeed);
        Assert.Contains("612", savings.Lines.Single(l => l.LineName == "Car insurance").Formula);
    }

    [Fact]
    public async Task Budget_month_upsert_shows_variance_in_history()
    {
        var lines = await _api.Get<List<BudgetLineDto>>("api/budget");
        var electric = lines.Single(b => b.Name == "Electric");

        await _api.Put($"api/budget/{electric.Id}/periods/2026-09-15", new BudgetPeriodDto { ActualAmount = 151.20m });
        await _api.Put($"api/budget/{electric.Id}/periods/2026-09-01", new BudgetPeriodDto { ActualAmount = 149.50m }); // same month → overwrite

        var history = await _api.Get<List<BudgetHistoryDto>>("api/budget/history?year=2026");
        var row = history.Single(h => h.BudgetLineId == electric.Id);
        var sep = row.Months.Single(m => m.Period == new DateOnly(2026, 9, 1));
        Assert.Equal(149.50m, sep.Actual);
        Assert.Equal(140m, sep.Projected);
        Assert.False(sep.ProjectedIsOverride);
        Assert.Equal(new DateOnly(2026, 9, 18), sep.DueDate);
        Assert.Equal(9.50m, sep.Variance);
        Assert.Equal(149.50m, row.AverageActual);
    }

    [Fact]
    public async Task Confirmation_number_is_kept_with_the_month_and_alone_keeps_the_row()
    {
        var lines = await _api.Get<List<BudgetLineDto>>("api/budget");
        var water = lines.Single(b => b.Name == "Water");

        await _api.Put($"api/budget/{water.Id}/periods/2026-08-01", new BudgetPeriodDto { ConfirmationNumber = "  WTR-88213  " });

        var history = await _api.Get<List<BudgetHistoryDto>>("api/budget/history?year=2026");
        var aug = history.Single(h => h.BudgetLineId == water.Id).Months.Single(m => m.Period == new DateOnly(2026, 8, 1));
        Assert.Equal("WTR-88213", aug.ConfirmationNumber);

        await _api.Client.DeleteAsync($"api/budget/{water.Id}/periods/2026-08-01");
    }

    [Fact]
    public async Task Per_month_due_date_and_projected_overrides_flow_to_history_upcoming_and_needs()
    {
        var lines = await _api.Get<List<BudgetLineDto>>("api/budget");
        var water = lines.Single(b => b.Name == "Water");

        // October: water is due on the 3rd this time and expected to be $72, not the usual 20th / $60.
        await _api.Put($"api/budget/{water.Id}/periods/2026-10-01", new BudgetPeriodDto { DueDate = new(2026, 10, 3), ProjectedAmount = 72m });
        // December: no water line (say the account closes) → projected 0 for that month only.
        await _api.Put($"api/budget/{water.Id}/periods/2026-12-01", new BudgetPeriodDto { ProjectedAmount = 0m });

        var history = await _api.Get<List<BudgetHistoryDto>>("api/budget/history?year=2026");
        var months = history.Single(h => h.BudgetLineId == water.Id).Months;
        var oct = months.Single(m => m.Period == new DateOnly(2026, 10, 1));
        Assert.Equal(new DateOnly(2026, 10, 3), oct.DueDate);
        Assert.True(oct.DueDateIsOverride);
        Assert.Equal(72m, oct.Projected);
        Assert.True(oct.ProjectedIsOverride);
        Assert.Equal(0m, months.Single(m => m.Period == new DateOnly(2026, 12, 1)).Projected);
        Assert.Equal(60m, months.Single(m => m.Period == new DateOnly(2026, 11, 1)).Projected);

        var needs = await _api.Get<TransferNeedsDto>("api/transfer-needs?asOf=2026-10-15");
        var line = needs.Accounts.Single(a => a.AccountName == "Chase Automated Bills").Lines.Single(l => l.LineName == "Water");
        Assert.Equal(72m, line.MonthlyAccrual);
        Assert.Contains("October 2026", line.Formula);

        // Clean up so other tests see the seed as-is.
        await _api.Client.DeleteAsync($"api/budget/{water.Id}/periods/2026-10-01");
        await _api.Client.DeleteAsync($"api/budget/{water.Id}/periods/2026-12-01");
    }

    [Fact]
    public async Task Budget_line_validation_requires_a_card_when_paid_by_card()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var dto = new BudgetLineDto { Name = "Bad line", ProjectedAmount = 1m, PaymentMethod = PaymentMethodKind.Card, FundingAccountId = accounts[0].Id, DueDay = 1 };
        var r = await _api.Client.PostAsJsonAsync("api/budget", dto, ApiFixture.Json);
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
        Assert.All(home.UpcomingLines, b => Assert.InRange(b.DueDate.DayNumber - home.AsOf.DayNumber, 0, 14));
    }

    [Fact]
    public async Task Upcoming_line_marked_paid_on_the_budget_grid_says_so()
    {
        var upcoming = await _api.Get<List<UpcomingLineDto>>("api/budget/upcoming?days=14");
        var history = await _api.Get<List<BudgetHistoryDto>>($"api/budget/history?year={upcoming[0].DueDate.Year}");
        // One with nothing recorded for its month, so deleting the row afterwards loses nothing.
        var line = upcoming.First(u => history.Single(h => h.BudgetLineId == u.BudgetLineId).Months
            .Single(m => m.Period.Month == u.DueDate.Month) is { Actual: null, PaidOn: null, ProjectedIsOverride: false, DueDateIsOverride: false, Notes: null });
        Assert.False(line.IsPaid);

        var period = $"{line.DueDate:yyyy-MM}-01";
        await _api.Put($"api/budget/{line.BudgetLineId}/periods/{period}", new BudgetPeriodDto { ActualAmount = 12.34m, PaidOn = line.DueDate });
        var after = (await _api.Get<List<UpcomingLineDto>>("api/budget/upcoming?days=14")).Single(u => u.BudgetLineId == line.BudgetLineId && u.DueDate == line.DueDate);
        Assert.True(after.IsPaid);
        Assert.Equal(line.DueDate, after.PaidOn);
        Assert.Equal(12.34m, after.PaidAmount);

        await _api.Client.DeleteAsync($"api/budget/{line.BudgetLineId}/periods/{period}");
    }

    [Fact]
    public async Task Transfer_between_two_accounts_writes_both_sides_and_deletes_together()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var main = accounts.Single(a => a.Name == "Chase Main");
        var auto = accounts.Single(a => a.Name == "Chase Automated Bills");
        var t = await _api.Post($"api/accounts/{main.Id}/transfers", new TransferDto { AccountId = main.Id, Date = new(2026, 9, 20), Amount = -330m, CounterpartyAccountId = auto.Id, Notes = "monthly budget lines" });
        Assert.Equal(-330m, t.Amount);
        Assert.Equal("Chase Automated Bills", t.CounterpartyName);
        Assert.NotNull(t.LinkedTransferId);
        var mirror = (await _api.Get<List<TransferDto>>($"api/accounts/{auto.Id}/transfers?year=2026&month=9")).Single(x => x.Id == t.LinkedTransferId);
        Assert.Equal(330m, mirror.Amount);
        Assert.Equal(main.Id, mirror.CounterpartyAccountId);
        var lines = await _api.Get<List<TransactionDto>>($"api/transactions?year=2026&month=9&accountId={main.Id}");
        var line = lines.Single(x => x.Id == t.Id);
        Assert.True(line.IsTransfer);
        Assert.Equal(TransactionOrigin.Manual, line.Origin);
        Assert.Equal("Chase Automated Bills", line.CounterpartyName);
        var after = await _api.Get<List<AccountDto>>("api/accounts");
        Assert.Equal(main.LatestBalance - 330m, after.Single(a => a.Id == main.Id).LatestBalance);   // snapshot Sep 1 + transfer Sep 20
        Assert.Equal(auto.LatestBalance + 330m, after.Single(a => a.Id == auto.Id).LatestBalance);
        await _api.Client.DeleteAsync($"api/accounts/{main.Id}/transfers/{t.Id}");
        Assert.DoesNotContain(await _api.Get<List<TransferDto>>($"api/accounts/{auto.Id}/transfers?year=2026&month=9"), x => x.Id == mirror.Id);
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

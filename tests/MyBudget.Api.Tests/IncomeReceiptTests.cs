using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>1099 work: a source with no salary or pay schedule, paid when it's paid, each payment correctable.</summary>
public class IncomeReceiptTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public IncomeReceiptTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_1099_source_needs_no_schedule_and_its_payments_can_be_corrected()
    {
        var source = await _api.Post("api/income-sources", new IncomeSourceDto { Name = "Consulting (receipts)", Type = IncomeSourceType.Contract1099, IsActive = true });
        Assert.Empty(source.PaySchedules);

        var r = await _api.Post("api/side-income/receipts", new IncomeReceiptDto { IncomeSourceId = source.Id, Date = new(2026, 3, 14), Amount = 1_200m });
        r.Amount = 1_250m; r.Notes = "  invoice 1042  ";
        var fixedUp = await _api.Put($"api/side-income/receipts/{r.Id}", r);
        Assert.Equal(1_250m, fixedUp.Amount);
        Assert.Equal("invoice 1042", fixedUp.Notes);

        var listed = (await _api.Get<List<IncomeReceiptDto>>("api/side-income/receipts?year=2026")).Single(x => x.Id == r.Id);
        Assert.Equal(1_250m, listed.Amount);

        r.Amount = 0;
        var refused = await _api.Client.PutAsJsonAsync($"api/side-income/receipts/{r.Id}", r);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refused.StatusCode);

        await _api.Client.DeleteAsync($"api/side-income/receipts/{r.Id}");
        await _api.Client.DeleteAsync($"api/income-sources/{source.Id}");
    }

    [Fact]
    public async Task Income_this_month_uses_a_stub_over_the_estimate_and_counts_1099_payments()
    {
        var before = (await _api.Get<HomeDashboardDto>("api/home/dashboard")).Income;
        var check = before.Items.First(i => i.Basis == "estimated take-home");
        var source = (await _api.Get<List<IncomeSourceDto>>("api/income-sources")).Single(s => s.Name == check.Source);

        // A stub for that pay date replaces the estimate.
        var stub = await _api.Post("api/paychecks", new PaycheckDto { IncomeSourceId = source.Id, PayDate = check.Date, Gross = 5_000m, Net = 3_210.98m });
        // A 1099 payment today counts as received, at its full amount.
        var side = await _api.Post("api/income-sources", new IncomeSourceDto { Name = "Consulting (month income)", Type = IncomeSourceType.Contract1099, IsActive = true });
        var today = (await _api.Get<HomeDashboardDto>("api/home/dashboard")).AsOf;
        await _api.Post("api/side-income/receipts", new IncomeReceiptDto { IncomeSourceId = side.Id, Date = today, Amount = 800m });

        var after = (await _api.Get<HomeDashboardDto>("api/home/dashboard")).Income;
        var replaced = Assert.Single(after.Items, i => i.Source == check.Source && i.Date == check.Date);
        Assert.Equal(3_210.98m, replaced.Amount);
        Assert.Equal("take-home from the stub", replaced.Basis);
        Assert.Contains(after.Items, i => i.Source == "Consulting (month income)" && i.Amount == 800m && i.Basis == "1099, nothing withheld");
        Assert.Equal(before.Expected - check.Amount + 3_210.98m + 800m, after.Expected);
        Assert.Equal(after.Items.Where(i => i.Date <= today).Sum(i => i.Amount), after.Received);

        await _api.Client.DeleteAsync($"api/paychecks/{stub.Id}");
        await _api.Client.DeleteAsync($"api/income-sources/{side.Id}");
    }
}

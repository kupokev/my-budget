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
}

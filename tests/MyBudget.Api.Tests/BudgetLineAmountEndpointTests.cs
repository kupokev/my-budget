using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A line's price changes come back with it, so the editor shows them, and a save sends the full
/// list back: rows can be corrected and removed, not only added. Reading lines without them showed
/// none in the editor, and correcting an existing month hit the unique index.
/// </summary>
public class BudgetLineAmountEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public BudgetLineAmountEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Price_changes_load_with_the_line_and_can_be_corrected_and_removed()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Gym (amounts)", FundingAccountId = account.Id, PaymentAccountId = account.Id, DueDay = 1, ProjectedAmount = 51m, IsActive = true,
            Amounts = [new() { FromPeriod = new(2025, 10, 1), Amount = 51m }, new() { FromPeriod = new(2026, 10, 1), Amount = 52m }],
        });

        var listed = (await _api.Get<List<BudgetLineDto>>("api/budget")).Single(b => b.Id == line.Id);
        Assert.Equal([52m, 51m], listed.Amounts.Select(a => a.Amount));            // newest first
        Assert.Equal(2, (await _api.Get<BudgetLineDto>($"api/budget/{line.Id}")).Amounts.Count);

        // What the editor sends after fixing October's typo and dropping the 2025 row.
        listed.Amounts = [new() { FromPeriod = new(2026, 10, 1), Amount = 53m }];
        var saved = await _api.Put($"api/budget/{line.Id}", listed);
        var only = Assert.Single(saved.Amounts);
        Assert.Equal(53m, only.Amount);
        Assert.Equal(new DateOnly(2026, 10, 1), only.FromPeriod);

        await _api.Client.DeleteAsync($"api/budget/{line.Id}");
    }
}

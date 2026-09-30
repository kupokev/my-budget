using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A budget line like "General Merchandise" covers several labels. The breakdown says which of them
/// the money actually went to, month by month, which the line's own total cannot.
/// </summary>
public class BudgetLabelBreakdownTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public BudgetLabelBreakdownTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Spending_splits_by_label_across_the_months_it_happened_in()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var category = await _api.Post("api/categories", new CategoryDto { Name = "Merch for breakdown" });
        var amazon = await _api.Post("api/labels", new LabelDto { Name = "Amazon (breakdown)", CategoryId = category.Id, IsActive = true });
        var costco = await _api.Post("api/labels", new LabelDto { Name = "Costco (breakdown)", CategoryId = category.Id, IsActive = true });

        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Merch line", CategoryId = category.Id, FundingAccountId = account.Id, PaymentAccountId = account.Id,
            Frequency = BudgetFrequency.Variable, ProjectedAmount = 500m, IsActive = true,
        });

        await Spend(account.Id, line.Id, category.Id, amazon.Id, new DateOnly(2026, 3, 4), -120.00m);
        await Spend(account.Id, line.Id, category.Id, amazon.Id, new DateOnly(2026, 3, 19), -30.00m);
        await Spend(account.Id, line.Id, category.Id, costco.Id, new DateOnly(2026, 5, 2), -240.00m);
        await Spend(account.Id, line.Id, category.Id, null, new DateOnly(2026, 5, 9), -10.00m);

        var breakdown = await _api.Get<BudgetLabelBreakdownDto>($"api/budget/{line.Id}/labels?year=2026");

        // Ordered by what each label came to, biggest first.
        Assert.Equal(3, breakdown.Rows.Count);
        Assert.Equal("Costco (breakdown)", breakdown.Rows[0].LabelName);
        Assert.Equal(240m, breakdown.Rows[0].Total);

        var amazonRow = breakdown.Rows.Single(r => r.LabelId == amazon.Id);
        Assert.Equal(150m, amazonRow.Total);            // both March purchases
        Assert.Equal(150m, amazonRow.Months[2]);        // March
        Assert.Equal(0m, amazonRow.Months[4]);          // nothing in May

        // Spending on the line with no label of its own is still shown rather than dropped.
        Assert.Contains(breakdown.Rows, r => r.LabelId is null && r.Total == 10m);
    }

    [Fact]
    public async Task An_actual_with_no_transactions_behind_it_shows_as_not_itemised()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Typed actual line", FundingAccountId = account.Id, PaymentAccountId = account.Id,
            Frequency = BudgetFrequency.Monthly, ProjectedAmount = 100m, IsActive = true,
        });

        // A month reconciled by hand, before any statement arrived.
        var period = new DateOnly(2026, 7, 1);
        var saved = await _api.Client.PutAsJsonAsync($"api/budget/{line.Id}/periods/{period:yyyy-MM-dd}",
            new BudgetPeriodDto { BudgetLineId = line.Id, Period = period, ActualAmount = 85m }, ApiFixture.Json);
        saved.EnsureSuccessStatusCode();

        var breakdown = await _api.Get<BudgetLabelBreakdownDto>($"api/budget/{line.Id}/labels?year=2026");

        // The rows have to add up to the line, or the money looks like it went missing.
        var row = Assert.Single(breakdown.Rows);
        Assert.Equal("Not itemised", row.LabelName);
        Assert.Equal(85m, row.Total);
        Assert.Equal(85m, row.Months[6]);
    }

    [Fact]
    public async Task A_refund_reduces_a_label_rather_than_inflating_it()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var category = await _api.Post("api/categories", new CategoryDto { Name = "Refund category" });
        var label = await _api.Post("api/labels", new LabelDto { Name = "Refund label", CategoryId = category.Id, IsActive = true });
        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Refund line", CategoryId = category.Id, FundingAccountId = account.Id, PaymentAccountId = account.Id,
            Frequency = BudgetFrequency.Variable, ProjectedAmount = 200m, IsActive = true,
        });

        await Spend(account.Id, line.Id, category.Id, label.Id, new DateOnly(2026, 4, 3), -100m);
        await Spend(account.Id, line.Id, category.Id, label.Id, new DateOnly(2026, 4, 20), 25m);   // returned an item

        var breakdown = await _api.Get<BudgetLabelBreakdownDto>($"api/budget/{line.Id}/labels?year=2026");

        // The line's own actual counts spending only, so the label has to as well or the two disagree.
        Assert.Equal(100m, breakdown.Rows.Single(r => r.LabelId == label.Id).Months[3]);
    }

    [Fact]
    public async Task A_line_with_no_transactions_returns_nothing_rather_than_failing()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Empty line", FundingAccountId = account.Id, PaymentAccountId = account.Id,
            Frequency = BudgetFrequency.Monthly, ProjectedAmount = 10m, IsActive = true,
        });

        var breakdown = await _api.Get<BudgetLabelBreakdownDto>($"api/budget/{line.Id}/labels?year=2026");

        Assert.Empty(breakdown.Rows);
    }

    private async Task Spend(int accountId, int lineId, int categoryId, int? labelId, DateOnly date, decimal amount)
    {
        var created = await _api.Client.PostAsJsonAsync("api/transactions", new TransactionCreateDto
        {
            AccountId = accountId, Date = date, Amount = amount, Description = "Purchase",
            CategoryId = categoryId, LabelId = labelId, BudgetLineId = lineId,
        }, ApiFixture.Json);
        created.EnsureSuccessStatusCode();
    }
}

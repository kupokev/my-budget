using System.Net;
using System.Net.Http.Json;
using MyBudget.Contracts;

namespace MyBudget.Api.Tests;

/// <summary>
/// Deleting a category is only safe while nothing points at it. Nulling the category out of
/// transactions or budget lines that already reference it would rewrite recorded history, so the
/// endpoint refuses instead, and retiring stays the way to take one out of circulation.
/// </summary>
public class CategoryDeleteTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public CategoryDeleteTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task An_unused_category_can_be_deleted()
    {
        var created = await _api.Post("api/categories", new CategoryDto { Name = "Scratch bucket" });

        var deleted = await _api.Client.DeleteAsync($"api/categories/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.DoesNotContain(await Categories(), c => c.Id == created.Id);
    }

    [Fact]
    public async Task A_category_a_budget_line_points_at_is_refused_and_says_what_is_using_it()
    {
        var category = await _api.Post("api/categories", new CategoryDto { Name = "Spoken for" });
        var funding = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Line that claims it",
            CategoryId = category.Id,
            FundingAccountId = funding.Id,
            PaymentAccountId = funding.Id,
            Frequency = MyBudget.Domain.BudgetFrequency.Variable,
            ProjectedAmount = 25m,
            IsActive = true,
        });

        var refused = await _api.Client.DeleteAsync($"api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        // The message is what the page shows, so it has to name the category and the blocker.
        var problem = await refused.Content.ReadAsStringAsync();
        Assert.Contains("Spoken for", problem);
        Assert.Contains("1 budget lines", problem);

        // And it is still there.
        Assert.Contains(await Categories(), c => c.Id == category.Id);
    }

    [Fact]
    public async Task A_budget_line_reports_what_it_costs_per_month_not_its_raw_amount()
    {
        var funding = (await _api.Get<List<AccountDto>>("api/accounts")).First();

        var annual = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Annual thing",
            FundingAccountId = funding.Id,
            PaymentAccountId = funding.Id,
            Frequency = MyBudget.Domain.BudgetFrequency.Annual,
            AnchorDueDate = new DateOnly(2026, 6, 1),
            ProjectedAmount = 1200m,
            IsActive = true,
        });

        // A twelfth of the year's bill, worked out by the ledger engine rather than by a screen.
        Assert.Equal(100m, annual.MonthlyAccrual);
        Assert.False(string.IsNullOrWhiteSpace(annual.MonthlyAccrualFormula));

        var monthly = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Monthly thing",
            FundingAccountId = funding.Id,
            PaymentAccountId = funding.Id,
            Frequency = MyBudget.Domain.BudgetFrequency.Monthly,
            ProjectedAmount = 40m,
            IsActive = true,
        });

        Assert.Equal(40m, monthly.MonthlyAccrual);
    }

    [Fact]
    public async Task Deleting_a_category_that_is_not_there_is_a_not_found()
    {
        var missing = await _api.Client.DeleteAsync("api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task<List<CategoryDto>> Categories() =>
        (await _api.Client.GetFromJsonAsync<List<CategoryDto>>("api/categories", ApiFixture.Json))!;
}

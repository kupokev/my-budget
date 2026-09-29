using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A variable line is a monthly allowance, not a bill: it has no due dates, so the grid used to find
/// none and fall through to an expected amount of zero. Every month then read as over budget the
/// moment a dollar was spent, when the point of the line is "up to $300 is fine".
/// </summary>
public class VariableLineProjectionTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public VariableLineProjectionTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_variable_line_expects_its_amount_in_every_month()
    {
        var funding = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Fuel allowance",
            FundingAccountId = funding.Id,
            PaymentAccountId = funding.Id,
            Frequency = BudgetFrequency.Variable,
            ProjectedAmount = 300m,
            IsActive = true,
        });

        var history = (await _api.Get<List<BudgetHistoryDto>>($"api/budget/history?year={DateTime.Today.Year}"))
            .Single(h => h.BudgetLineId == line.Id);

        Assert.All(history.Months, m => Assert.Equal(300m, m.Projected));
    }

    [Fact]
    public async Task Spending_under_the_allowance_is_not_over_budget()
    {
        var funding = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var line = await _api.Post("api/budget", new BudgetLineDto
        {
            Name = "Fuel under",
            FundingAccountId = funding.Id,
            PaymentAccountId = funding.Id,
            Frequency = BudgetFrequency.Variable,
            ProjectedAmount = 300m,
            IsActive = true,
        });

        var period = new DateOnly(DateTime.Today.Year, 5, 1);
        var saved = await _api.Client.PutAsJsonAsync($"api/budget/{line.Id}/periods/{period:yyyy-MM-dd}",
            new BudgetPeriodDto { BudgetLineId = line.Id, Period = period, ActualAmount = 70.56m }, ApiFixture.Json);
        saved.EnsureSuccessStatusCode();

        var month = (await _api.Get<List<BudgetHistoryDto>>($"api/budget/history?year={period.Year}"))
            .Single(h => h.BudgetLineId == line.Id)
            .Months.Single(m => m.Period == period);

        Assert.Equal(300m, month.Projected);
        Assert.Equal(-229.44m, month.Variance);      // under, not over
    }
}

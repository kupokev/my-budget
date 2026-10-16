using MyBudget.Contracts;

namespace MyBudget.Api.Tests;

/// <summary>The dashboard's "Needed in each account" panel: one month of the Budget grid grouped by Funded from.</summary>
public class FundingNeedsTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public FundingNeedsTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Groups_the_month_by_funding_account_and_counts_card_paid_lines_there()
    {
        // March 2026: Car insurance ($612, semi-annual, anchored Mar 15) is charged to the Surpass card
        // but funded from Premier Savings, so it lands there and not on the card's paying account.
        var march = await _api.Get<FundingMonthDto>("api/funding-needs?year=2026&month=3");
        Assert.Equal(new DateOnly(2026, 3, 1), march.Month);
        var savings = march.Accounts.Single(a => a.AccountName == "Chase Premier Savings");
        var insurance = Assert.Single(savings.Lines, l => l.LineName == "Car insurance");
        Assert.Equal(612m, insurance.Budgeted);
        Assert.Equal(new DateOnly(2026, 3, 15), insurance.DueDate);
        Assert.StartsWith("Card:", insurance.PaidVia);
        Assert.Equal(savings.Lines.Sum(l => l.Budgeted), savings.Budgeted);
        Assert.Contains("Car insurance", savings.Formula);

        // Automated Bills carries the monthly utilities plus Hulu (card-paid, funded from here).
        var bills = march.Accounts.Single(a => a.AccountName == "Chase Automated Bills");
        Assert.Contains(bills.Lines, l => l.LineName == "Hulu");
        Assert.Equal(60m + 45m + 140m + 85m + 18.99m, bills.Lines.Where(l => l.LineName is "Water" or "Sewer" or "Electric" or "AT&T" or "Hulu").Sum(l => l.Budgeted));
        Assert.Equal(march.Accounts.Sum(a => a.Budgeted), march.Budgeted);

        // April has no insurance payment, so Premier Savings drops it; the even pace still spreads it.
        var april = await _api.Get<FundingMonthDto>("api/funding-needs?year=2026&month=4");
        var savingsApril = april.Accounts.SingleOrDefault(a => a.AccountName == "Chase Premier Savings");
        Assert.DoesNotContain(savingsApril?.Lines ?? [], l => l.LineName == "Car insurance");
        Assert.True(savings.SteadyMonthly > 0 && savings.SteadyMonthly < savings.Budgeted);
    }

    [Fact]
    public async Task Rejects_an_impossible_month()
    {
        var r = await _api.Client.GetAsync("api/funding-needs?year=2026&month=13");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }
}

using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// An investment account has no typed balance — it is worth what it holds. The accounts list used to
/// show a dash for exactly the accounts holding the most money, while the Wealth screens valued them
/// in full from the same data.
/// </summary>
public class AccountHoldingValueTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public AccountHoldingValueTests(ApiFixture api) => _api = api;

    private const string Header =
        "Account name,Account number,Asset Class,Description,Ticker,Quantity,Price,Pricing Date,Acquisition Date,Unit Cost";

    [Fact]
    public async Task An_account_with_holdings_reports_what_they_are_worth()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "Brokerage with holdings", Institution = "JPM", Type = AccountType.Brokerage, IsActive = true,
        });

        // 2 shares at 333.37, plus a 500.00 cash sweep. The ticker is deliberately one no price
        // provider knows: a real one would be repriced by the fetch that follows every import, and the
        // test would then depend on today's market rather than on the arithmetic being checked.
        await ImportAsync(account.Id,
            Header + "\n\"Brokerage\",\"...1\",\"Equity\",\"NOT A REAL SECURITY\",ZZQQ,2,333.37,09/25/2026 08:00:00,08/06/2024,265\n" +
            "\"Brokerage\",\"...1\",\"Cash & Money Market Funds\",\"SWEEP\",QSWEEP,500,1,09/25/2026 08:00:00,,1\n");

        var listed = (await _api.Get<List<AccountDto>>("api/accounts")).Single(a => a.Id == account.Id);

        Assert.Equal(1_166.74m, listed.LatestBalance);          // 666.74 + 500.00
        Assert.Equal(new DateOnly(2026, 9, 25), listed.LatestBalanceAsOf);
        Assert.Contains("holdings valued", listed.BalanceDetail);
    }

    [Fact]
    public async Task An_ordinary_account_is_unaffected_and_still_uses_its_own_balance()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "Plain checking", Institution = "Chase", Type = AccountType.Checking, IsActive = true,
        });

        var listed = (await _api.Get<List<AccountDto>>("api/accounts")).Single(a => a.Id == account.Id);

        Assert.DoesNotContain("holdings", listed.BalanceDetail ?? "");
    }

    [Fact]
    public async Task A_brokerage_marked_as_the_rainy_day_fund_counts_towards_it()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "Brokerage as rainy-day", Institution = "JPM", Type = AccountType.Brokerage,
            IsRainyDayFund = true, IsActive = true,
        });

        await ImportAsync(account.Id,
            Header + "\n\"Brokerage\",\"...2\",\"Cash & Money Market Funds\",\"SWEEP\",ZZSWEEP,28605.06,1,09/25/2026 08:00:00,,1\n");

        var fund = await _api.Get<RainyDayDto>("api/rainy-day");

        // The fund used to read zero while holding this, because it only looked at typed balances.
        Assert.Contains("Brokerage as rainy-day", fund.Accounts);
        Assert.True(fund.Balance >= 28_605.06m, $"rainy-day balance was {fund.Balance}");
    }

    private async Task ImportAsync(int accountId, string csv)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        form.Add(content, "file", "lots.csv");
        form.Add(new StringContent(accountId.ToString()), "accountId");

        var r = await _api.Client.PostAsync("api/investments/import-lots", form);
        r.EnsureSuccessStatusCode();
    }
}

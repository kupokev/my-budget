using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A brokerage's cash sweep is a balance that every statement restates, not a lot that accumulates.
/// Importing successive statements must leave the position at the latest stated balance; appending
/// each one would have stacked September's cash on top of October's and inflated the account.
/// </summary>
public class CashSweepImportTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public CashSweepImportTests(ApiFixture api) => _api = api;

    private const string Header =
        "Account name,Account number,Asset Class,Description,Ticker,Quantity,Price,Pricing Date,Acquisition Date,Unit Cost";

    private static string Sweep(decimal balance, string pricingDate) =>
        $"{Header}\n\"Traditional IRA\",\"...4077\",\"Cash & Money Market Funds\",\"CHASE IRA DEPOSIT SWEEP\",QDERQ,\"{balance:N2}\",1,{pricingDate} 08:00:00,,1\n";

    [Fact]
    public async Task Successive_statements_leave_the_sweep_at_the_latest_balance()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "IRA for sweep test", Institution = "JPM", Type = AccountType.TraditionalIra, IsActive = true,
        });

        var first = await ImportAsync(account.Id, Sweep(11_012.76m, "09/25/2026"));
        Assert.Equal(1, first.HoldingsCreated);

        // The balance dropped: money was moved out, not a second pot of cash acquired.
        var second = await ImportAsync(account.Id, Sweep(5_512.38m, "09/28/2026"));

        var sweep = await SweepPosition(account.Id);

        // 11,012.76 in, then 5,500.38 out — the position is the stated balance, and the move is visible.
        Assert.Equal(5_512.38m, sweep.Shares);
        Assert.Contains(sweep.Trades, t => t.Kind == TradeKind.Sell && t.Shares == 5_500.38m);
        Assert.Equal(1, second.LotsImported);
    }

    [Fact]
    public async Task Re_importing_the_same_statement_changes_nothing()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "IRA for repeat test", Institution = "JPM", Type = AccountType.TraditionalIra, IsActive = true,
        });

        await ImportAsync(account.Id, Sweep(11_012.76m, "09/25/2026"));
        var again = await ImportAsync(account.Id, Sweep(11_012.76m, "09/25/2026"));

        var sweep = await SweepPosition(account.Id);

        Assert.Equal(11_012.76m, sweep.Shares);
        Assert.Equal(0, again.LotsImported);
    }

    private async Task<PositionDto> SweepPosition(int accountId)
    {
        var portfolio = await _api.Get<PortfolioDto>("api/investments/portfolio");
        return Assert.Single(portfolio.Positions, p => p.Holding.Ticker == "QDERQ" && p.Holding.AccountId == accountId);
    }

    private async Task<LotImportResultDto> ImportAsync(int accountId, string csv)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        form.Add(content, "file", "lots.csv");
        form.Add(new StringContent(accountId.ToString()), "accountId");

        var r = await _api.Client.PostAsync("api/investments/import-lots", form);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<LotImportResultDto>(ApiFixture.Json))!;
    }
}

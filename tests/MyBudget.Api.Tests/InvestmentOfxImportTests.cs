using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A plan statement reports fees in dollars with no share count, so the purchases alone always
/// overstate the position. The imported holding has to land on what the statement says is held.
/// </summary>
public class InvestmentOfxImportTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public InvestmentOfxImportTests(ApiFixture api) => _api = api;

    // Two purchases totalling 7.604 shares, one fee, and a position of 7.500.
    private const string Qfx =
        "<OFX><INVSTMTMSGSRSV1><INVSTMTTRNRS><INVSTMTRS><INVACCTFROM><ACCTID>93793</INVACCTFROM><INVTRANLIST>" +
        "<BUYMF><INVBUY><INVTRAN><FITID>A1<DTTRADE>20260402160000.000[-5:EST]</INVTRAN>" +
        "<SECID><UNIQUEID>92202V641<UNIQUEIDTYPE>CUSIP</SECID><UNITS>4.032<UNITPRICE>74.41<TOTAL>300.0</INVBUY><BUYTYPE>BUY</BUYMF>" +
        "<BUYMF><INVBUY><INVTRAN><FITID>A2<DTTRADE>20260917160000.000[-5:EST]</INVTRAN>" +
        "<SECID><UNIQUEID>92202V641<UNIQUEIDTYPE>CUSIP</SECID><UNITS>3.572<UNITPRICE>83.99<TOTAL>300.0</INVBUY><BUYTYPE>BUY</BUYMF>" +
        "<INVEXPENSE><INVTRAN><FITID>F1<DTTRADE>20260904160000.000[-5:EST]</INVTRAN>" +
        "<SECID><UNIQUEID>92202V641<UNIQUEIDTYPE>CUSIP</SECID><TOTAL>-10.5</INVEXPENSE>" +
        "</INVTRANLIST><INVPOSLIST><POSMF><INVPOS><SECID><UNIQUEID>VGI001480<UNIQUEIDTYPE>CUSIP</SECID>" +
        "<UNITS>7.500<UNITPRICE>84.44<DTPRICEASOF>20260928160000.000[-5:EST]</INVPOS></POSMF></INVPOSLIST>" +
        "</INVSTMTRS></INVSTMTTRNRS></INVSTMTMSGSRSV1><SECLISTMSGSRSV1><SECLIST><MFINFO><SECINFO>" +
        "<SECID><UNIQUEID>VGI001480<UNIQUEIDTYPE>CUSIP</SECID><SECNAME>Target Retire 2050 Tr II" +
        "<TICKER>VGI001480<UNITPRICE>84.44</SECINFO></MFINFO></SECLIST></SECLISTMSGSRSV1></OFX>";

    [Fact]
    public async Task The_holding_settles_on_the_shares_the_statement_reports()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "401k for ofx import", Institution = "Vanguard", Type = AccountType.Retirement401k, IsActive = true,
        });

        await ImportAsync(account.Id, Qfx);

        var position = await Position(account.Id);

        Assert.Equal(7.500m, position.Shares);                                  // not the 7.604 bought
        Assert.Contains(position.Trades, t => t.Kind == TradeKind.Sell && t.Notes!.Contains("adjusted to statement"));
        Assert.Equal(84.44m, position.Price);
    }

    [Fact]
    public async Task Importing_the_same_statement_twice_does_not_drift()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "401k for repeat ofx", Institution = "Vanguard", Type = AccountType.Retirement401k, IsActive = true,
        });

        await ImportAsync(account.Id, Qfx);
        await ImportAsync(account.Id, Qfx);

        // The lots dedupe and the position already matches, so nothing is added the second time.
        Assert.Equal(7.500m, (await Position(account.Id)).Shares);
    }

    [Fact]
    public async Task Plan_fees_are_recorded_so_the_cost_of_the_plan_can_be_added_up()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "401k for fees", Institution = "Vanguard", Type = AccountType.Retirement401k, IsActive = true,
        });

        await ImportAsync(account.Id, Qfx);

        var position = await Position(account.Id);

        var fee = Assert.Single(position.Fees!);
        Assert.Equal(10.50m, fee.Amount);                        // recorded as a positive cost
        Assert.Equal(new DateOnly(2026, 9, 4), fee.Date);
        Assert.Equal(10.50m, position.FeesThisYear);
    }

    [Fact]
    public async Task Re_importing_does_not_double_count_the_fees()
    {
        var account = await _api.Post("api/accounts", new AccountDto
        {
            Name = "401k for repeat fees", Institution = "Vanguard", Type = AccountType.Retirement401k, IsActive = true,
        });

        await ImportAsync(account.Id, Qfx);
        await ImportAsync(account.Id, Qfx);

        var position = await Position(account.Id);

        Assert.Single(position.Fees!);
        Assert.Equal(10.50m, position.FeesThisYear);
    }

    private async Task<PositionDto> Position(int accountId)
    {
        var portfolio = await _api.Get<PortfolioDto>("api/investments/portfolio");
        return Assert.Single(portfolio.Positions, p => p.Holding.AccountId == accountId);
    }

    private async Task ImportAsync(int accountId, string qfx)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(qfx));
        form.Add(content, "file", "vanguard.qfx");
        form.Add(new StringContent(accountId.ToString()), "accountId");

        var r = await _api.Client.PostAsync("api/investments/import-lots", form);
        r.EnsureSuccessStatusCode();
    }
}

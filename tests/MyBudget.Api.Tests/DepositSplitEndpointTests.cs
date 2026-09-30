using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A cheque lands in several accounts — fixed amounts first, one account taking the balance — so a
/// deposit can't be matched one-to-one with a cheque. Each account's tagged total is compared to its
/// expected share, and an account with nothing tagged reads as "not imported", not as a shortfall.
/// </summary>
public class DepositSplitEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public DepositSplitEndpointTests(ApiFixture api) => _api = api;

    private const string PayDate = "2026-09-18";

    private async Task<IncomeSourceDto> Employer()
        => (await _api.Get<List<IncomeSourceDto>>("api/income-sources")).Single(s => s.Name == "Ridgeline Partners");

    [Fact]
    public async Task The_split_divides_the_net_and_the_last_account_takes_the_balance()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var (a, b, c) = (accounts[0], accounts[1], accounts[2]);
        var source = await Employer();
        source.DepositSplits =
        [
            new DepositSplitDto { AccountId = a.Id, Amount = 500m },
            new DepositSplitDto { AccountId = b.Id, Amount = 250m },
            new DepositSplitDto { AccountId = c.Id, IsRemainder = true },
        ];
        await _api.Put($"api/income-sources/{source.Id}", source);

        var e = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={source.Id}&date={PayDate}");
        var deposits = e.Deposits!;

        Assert.Equal(3, deposits.Count);
        Assert.Equal(500m, deposits[0].Amount);
        Assert.Equal(250m, deposits[1].Amount);
        Assert.Equal(e.Net - 750m, deposits[2].Amount);
        Assert.Equal(e.Net, deposits.Sum(d => d.Amount));
        Assert.Contains("balance", deposits[2].Formula);
    }

    [Fact]
    public async Task A_deposit_tagged_to_the_income_source_shows_against_its_account()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var (a, b) = (accounts[0], accounts[1]);
        var source = await Employer();
        source.DepositSplits =
        [
            new DepositSplitDto { AccountId = a.Id, Amount = 500m },
            new DepositSplitDto { AccountId = b.Id, IsRemainder = true },
        ];
        await _api.Put($"api/income-sources/{source.Id}", source);

        var tx = await _api.Post<TransactionCreateDto, TransactionDto>("api/transactions", new TransactionCreateDto
        {
            AccountId = a.Id, Date = DateOnly.Parse(PayDate), Amount = 500m,
            Description = "DIRECT DEP PAYROLL", IncomeSourceId = source.Id,
        });
        Assert.Equal(source.Id, tx.IncomeSourceId);

        var e = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={source.Id}&date={PayDate}");
        var first = e.Deposits!.Single(d => d.AccountId == a.Id);
        var second = e.Deposits!.Single(d => d.AccountId == b.Id);

        Assert.Equal(500m, first.Received);
        Assert.Equal(1, first.MatchedCount);
        Assert.Equal(0, second.MatchedCount);   // that account's statement isn't imported yet
    }

    [Fact]
    public async Task Only_one_row_can_take_the_balance()
    {
        var accounts = await _api.Get<List<AccountDto>>("api/accounts");
        var source = await Employer();
        source.DepositSplits =
        [
            new DepositSplitDto { AccountId = accounts[0].Id, IsRemainder = true },
            new DepositSplitDto { AccountId = accounts[1].Id, IsRemainder = true },
        ];
        await _api.Put($"api/income-sources/{source.Id}", source);

        var saved = await Employer();
        Assert.Equal(1, saved.DepositSplits.Count(d => d.IsRemainder));
    }
}

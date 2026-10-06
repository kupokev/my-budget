using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>The Transactions page's running balance, checked against Wealthfront's September 2026.</summary>
public class RunningBalanceTests
{
    private static Transaction Tx(int id, int month, int day, decimal amount, TransactionOrigin origin = TransactionOrigin.Imported, int? reconciledWith = null)
        => new() { Id = id, Date = new DateOnly(2026, month, day), Amount = amount, Description = "x", ExternalId = $"e{id}", Origin = origin, ReconciledWithId = reconciledWith };

    [Fact]
    public void Runs_back_to_rows_the_statement_includes_and_forward_from_it_and_ends_on_the_account_balance()
    {
        var account = new Account { Name = "Wealthfront" };
        account.Balances.Add(new AccountBalance { AsOf = new DateOnly(2026, 9, 1), Balance = 2259.22m });
        account.Transactions.AddRange([
            Tx(141, 8, 30, -550m),
            Tx(142, 9, 1, 5.81m),
            Tx(428, 9, 1, 5.81m, TransactionOrigin.Manual, reconciledWith: 142),
            Tx(143, 9, 2, 1000m),
            Tx(144, 9, 5, -600m),
            Tx(145, 9, 5, -1122m),
            Tx(146, 9, 16, 1000m),
        ]);

        var r = BalanceMath.Running(account.Balances, account.Transactions);

        // On or before the statement date the statement already counts them, so they run backwards from it.
        Assert.Equal(2253.41m, r[141].Balance);
        Assert.Equal(2259.22m, r[142].Balance);
        Assert.Contains("includes this transaction", r[142].Detail);
        // A hand entry reconciled with a bank line is counted through that line.
        Assert.False(r.ContainsKey(428));
        Assert.Equal(3259.22m, r[143].Balance);
        Assert.Equal(2659.22m, r[144].Balance);
        Assert.Equal(1537.22m, r[145].Balance);
        Assert.Equal(2537.22m, r[146].Balance);
        Assert.Equal(BalanceMath.Of(account, new DateOnly(2026, 10, 5)).Balance, r[146].Balance);
    }
}

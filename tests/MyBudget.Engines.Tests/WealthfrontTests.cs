using MyBudget.Engines.Import;

namespace MyBudget.Engines.Tests;

/// <summary>Wealthfront's cash-account export: Transaction date, Description, Type, Amount.</summary>
public class WealthfrontTests
{
    private const string Csv =
        "Transaction date,Description,Type,Amount\n" +
        "9/16/2026,Fox Rothschild - Payroll,Deposit,1000.00\n" +
        "9/5/2026,Chase (Account ****5868),Withdrawal,-600.00\n" +
        "9/1/2026,August interest,Interest payment,5.81\n";

    [Fact]
    public void The_layout_is_recognised_and_amounts_keep_their_sign()
    {
        var r = CsvStatementParser.Parse(Csv);

        Assert.Equal("wealthfront", r.Profile);
        Assert.Equal(3, r.Transactions.Count);
        Assert.Empty(r.Warnings);

        Assert.Equal(new DateOnly(2026, 9, 16), r.Transactions[0].Date);
        Assert.Equal(1000m, r.Transactions[0].Amount);          // deposits positive
        Assert.Equal(-600m, r.Transactions[1].Amount);          // withdrawals already negative
        Assert.Equal(5.81m, r.Transactions[2].Amount);

        // The Type column is kept as the memo, so "Interest payment" survives into the ledger.
        Assert.Equal("Interest payment", r.Transactions[2].Memo);
    }

    [Fact]
    public void A_chase_card_export_still_wins_even_though_wealthfront_is_a_subset_of_it()
    {
        // Chase's card export also has Transaction Date, Description, Type and Amount. It is only told
        // apart by Post Date, so this is the regression that adding Wealthfront could have caused.
        const string chase =
            "Transaction Date,Post Date,Description,Category,Type,Amount,Memo\n" +
            "09/24/2026,09/25/2026,AMAZON.COM,Shopping,Sale,-42.10,\n";

        var r = CsvStatementParser.Parse(chase);

        Assert.Equal("chase-card", r.Profile);
        Assert.Equal(-42.10m, r.Transactions[0].Amount);
    }

    [Fact]
    public void A_statement_is_not_mistaken_for_a_tax_lot_export()
        => Assert.Equal(ImportKind.Statement, ImportFileKind.Detect("Wealthfront - Individual Cash Account.csv", Csv));
}

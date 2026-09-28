using MyBudget.Engines.Import;

namespace MyBudget.Engines.Tests;

/// <summary>
/// One upload box serves statements and tax-lot exports, so the file has to say which it is. The
/// account cannot: a brokerage account issues both a cash statement and a lot export.
/// </summary>
public class ImportFileKindTests
{
    // Trimmed from a real J.P. Morgan positions export, which carries some 70 columns.
    private const string TaxLots =
        "Account name,Account number,Description,Ticker,Quantity,Price,Cost,Acquisition Date,Unit Cost,Tax term\n" +
        "\"Traditional IRA\",\"...4077\",\"NUSCALE POWER\",SMR,75,8.42,637.50,09/25/2026,8.50,Short\n";

    private const string BankStatement =
        "Details,Posting Date,Description,Amount,Type,Balance,Check or Slip #\n" +
        "DEBIT,09/24/2026,\"Payment to Chase card ending in 9039\",-314.00,LOAN_PMT,552.79,\n";

    [Fact]
    public void A_brokerage_lot_export_is_recognised_by_its_columns()
        => Assert.Equal(ImportKind.TaxLots, ImportFileKind.Detect("taxlots.csv", TaxLots));

    [Fact]
    public void A_bank_statement_is_a_statement()
        => Assert.Equal(ImportKind.Statement, ImportFileKind.Detect("activity.csv", BankStatement));

    [Fact]
    public void Ofx_is_always_a_statement()
        => Assert.Equal(ImportKind.Statement, ImportFileKind.Detect("export.qfx", "<OFX><BANKMSGSRSV1>"));

    [Fact]
    public void A_near_miss_goes_to_the_statement_importer_so_it_can_report_its_own_missing_columns()
    {
        // Ticker and Quantity but no unit cost or acquisition date: not enough to import lots from.
        var partial = "Description,Ticker,Quantity,Price\n\"NUSCALE\",SMR,75,8.42\n";

        Assert.Equal(ImportKind.Statement, ImportFileKind.Detect("holdings.csv", partial));
    }

    [Fact]
    public void An_empty_file_does_not_throw()
        => Assert.Equal(ImportKind.Statement, ImportFileKind.Detect("empty.csv", ""));
}

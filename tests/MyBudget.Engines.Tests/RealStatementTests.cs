using MyBudget.Engines.Import;

namespace MyBudget.Engines.Tests;

/// <summary>Lines shaped like Kevin's real Chase checking export and J.P. Morgan tax-lot export.</summary>
public class RealStatementTests
{
    [Fact]
    public void Chase_checking_export_with_trailing_comma_and_bank_tails()
    {
        const string csv = "Details,Posting Date,Description,Amount,Type,Balance,Check or Slip #\n" +
            "DEBIT,09/24/2026,\"Payment to Chase card ending in 9039 09/24\",-314.00,LOAN_PMT,552.79,,\n" +
            "CREDIT,09/21/2026,\"PAYPAL           TRANSFER                   PPD ID: PAYPALSD11\",165.89,MISC_CREDIT,807.63,,\n" +
            "DEBIT,09/09/2026,\"WELLS FARGO CARD CCPYMT     90154358591204  WEB ID: 3411650794\",-90.00,ACH_DEBIT,304.20,,\n" +
            "DEBIT,09/08/2026,\"Online Transfer to CHK ...0778 transaction#: 30710838767 09/08\",-600.00,ACCT_XFER,1558.37,,\n" +
            "DEBIT,09/04/2026,\"Speedpay         AmerenMO   0421704132      WEB ID: 9212021104\",-290.28,ACH_DEBIT,506.72,,\n";
        var r = CsvStatementParser.Parse(csv);
        Assert.Equal("chase-checking", r.Profile);
        Assert.Equal(5, r.Transactions.Count);
        Assert.Equal(-314m, r.Transactions[0].Amount);
        Assert.Equal(new DateOnly(2026, 9, 24), r.Transactions[0].Date);
        Assert.Equal("LOAN_PMT", r.Transactions[0].Memo);
        Assert.Equal("Paypal Transfer", Merchants.Normalize(r.Transactions[1].Description));
        Assert.Equal("Wells Fargo Card Ccpymt", Merchants.Normalize(r.Transactions[2].Description));
        Assert.Equal("Online Transfer To Chk", Merchants.Normalize(r.Transactions[3].Description));
        Assert.Equal("Speedpay Amerenmo", Merchants.Normalize(r.Transactions[4].Description));
    }

    [Fact]
    public void Tax_lot_export_becomes_one_lot_per_row_and_skips_cash()
    {
        const string csv = "Account name,Account number,Account type,Sub account,Asset Class,Asset Strategy,Asset Strategy Detail,Description,Ticker,CUSIP,Quantity,Base CCY,Local CCY,Price,PriceInd,Local Price,Today's Price Change,Price Change %,Pricing Date,Value,Today's Value Change,Value Change %,Local Value,Cost,Orig Cost (Base),Orig Cost (Local),Cost Source,Local Cost,Unrealized G/L Amt.,Orig. $ Gain/Loss (Base),Orig. $ Gain/Loss (Local),Local Unrealized G/L Amt.,Unrealized Gain/Loss (%),Orig. % Gain/Loss (Base),Orig. % Gain/Loss (Local),Local Unrealized Gain/Loss (%),Disallowed Loss (Base),Disallowed Loss (Local),Acquisition Date,Adj Date,Acquisition exchange,Unit Cost,Local Unit Cost,Tax term\n" +
            "\"Traditional IRA\",\"...4077\",\"Brokerage\",\"\",\"Equity\",\"US Small Cap\",\"\",\"INSTALLED BUILDING PRODUCTS INC\",\"IBP\",\"45780R101\",\"25\",\"USD\",\"\",\"200.23\",\"false\",\"\",\"3.94\",\"2.01\",\"09/25/2026 08:00:00\",\"5,005.75\",\"98.5\",\"2.01\",\"\",\"4,862.88\",\"4,862.88\",\"\",\"\",\"\",\"142.87\",\"142.87\",\"\",\"\",\"2.94\",\"2.94\",\"\",\"\",\"\",\"\",\"09/25/2026\",\"\",\"0\",\"194.52\",\"\",\"Short\"\n" +
            "\"Traditional IRA\",\"...4077\",\"Brokerage\",\"\",\"Equity\",\"US Large Cap\",\"\",\"GLOBAL X FDS GLOBAL X NASDAQ 100 COVERED CALL ETF\",\"QYLD\",\"37954Y483\",\"1,145\",\"USD\",\"\",\"18.55\",\"false\",\"\",\"0.02\",\"0.11\",\"09/25/2026 08:00:00\",\"21,239.75\",\"22.9\",\"0.11\",\"\",\"20,438.25\",\"20,438.25\",\"\",\"\",\"\",\"801.5\",\"801.5\",\"\",\"\",\"3.92\",\"3.92\",\"\",\"\",\"\",\"\",\"01/16/2026\",\"\",\"0\",\"17.85\",\"\",\"Short\"\n" +
            "\"Traditional IRA\",\"...4077\",\"Brokerage\",\"\",\"Cash & Money Market Funds\",\"Money Market Funds\",\"\",\"CHASE IRA DEPOSIT SWEEP JPMORGAN CHASE BANK NA\",\"QDERQ\",\"\",\"11,012.76\",\"USD\",\"\",\"1\",\"false\",\"\",\"0\",\"0\",\"09/25/2026 08:00:00\",\"11,012.76\",\"0\",\"0\",\"\",\"11,012.76\",\"11,012.76\",\"\",\"\",\"\",\"0\",\"0\",\"\",\"\",\"0\",\"0\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"1\",\"\",\"Short\"\n" +
            "\nFOOTNOTES\nP,\"This order is pending settlement.\"\n";
        var r = TaxLotParser.Parse(csv);
        Assert.Equal(2, r.Lots.Count);
        var ibp = r.Lots.Single(l => l.Ticker == "IBP");
        Assert.Equal(25m, ibp.Quantity);
        Assert.Equal(194.52m, ibp.UnitCost);
        Assert.Equal(new DateOnly(2026, 9, 25), ibp.Acquired);
        Assert.Equal(200.23m, ibp.Price);
        Assert.Equal(new DateOnly(2026, 9, 25), ibp.PriceDate);
        var qyld = r.Lots.Single(l => l.Ticker == "QYLD");
        Assert.Equal(1_145m, qyld.Quantity);                   // "1,145" with the thousands separator
        Assert.Equal(new DateOnly(2026, 1, 16), qyld.Acquired);
        Assert.Single(r.Skipped);
        Assert.Contains("SWEEP", r.Skipped[0]);
        Assert.Empty(r.Warnings);
    }
}

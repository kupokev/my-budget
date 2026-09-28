using MyBudget.Engines.Import;

namespace MyBudget.Engines.Tests;

/// <summary>
/// Money-market funds and the cash sweep carry no acquisition date, so they are not tax lots in the
/// usual sense — but they are real money. Dropping them made the imported account total read short by
/// exactly their value, which is the kind of gap that looks like a broken import.
/// </summary>
public class TaxLotCashTests
{
    private const string Header =
        "Account name,Account number,Asset Class,Description,Ticker,Quantity,Price,Pricing Date,Acquisition Date,Unit Cost";

    private const string Rows =
        // A normal equity lot.
        "\"Self-Directed\",\"...8197\",\"Equity\",\"CHUBB LTD COM\",CB,2,333.37,09/25/2026 08:00:00,08/06/2024,265\n" +
        // A money-market fund: no acquisition date, holds at $1.
        "\"Self-Directed\",\"...8197\",\"Cash & Money Market Funds\",\"VANGUARD TREASURY MONEY MARKET FUND\",VUSXX,\"10,669.58\",1,09/25/2026 08:00:00,,1\n" +
        // The bank's cash sweep, same shape.
        "\"Self-Directed\",\"...8197\",\"Cash & Money Market Funds\",\"CHASE DEPOSIT SWEEP\",QACDS,96.69,1,09/25/2026 08:00:00,,1\n";

    [Fact]
    public void Cash_and_money_market_rows_are_imported_not_skipped()
    {
        var result = TaxLotParser.Parse($"{Header}\n{Rows}");

        Assert.Empty(result.Warnings);
        Assert.Empty(result.Skipped);
        Assert.Equal(3, result.Lots.Count);

        var vusxx = Assert.Single(result.Lots, l => l.Ticker == "VUSXX");
        Assert.Equal(10_669.58m, vusxx.Quantity);
        Assert.Equal(1m, vusxx.UnitCost);
        // Dated from the statement, since the brokerage gives no acquisition date for a $1 NAV fund.
        Assert.Equal(new DateOnly(2026, 9, 25), vusxx.Acquired);

        Assert.Contains(result.Lots, l => l.Ticker == "QACDS" && l.Quantity == 96.69m);
    }

    [Fact]
    public void The_account_total_includes_the_cash_positions()
    {
        var result = TaxLotParser.Parse($"{Header}\n{Rows}");

        var total = result.Lots.Sum(l => l.Quantity * (l.Price ?? 0));

        // 2 × 333.37 + 10,669.58 + 96.69
        Assert.Equal(11_433.01m, Math.Round(total, 2));
    }

    [Fact]
    public void Cash_positions_are_flagged_so_nothing_asks_a_price_provider_about_them()
    {
        var result = TaxLotParser.Parse($"{Header}\n{Rows}");

        // QACDS is an internal Chase sweep code, not a listed security: Yahoo returns 404 for it.
        Assert.True(result.Lots.Single(l => l.Ticker == "QACDS").IsCashEquivalent);
        Assert.True(result.Lots.Single(l => l.Ticker == "VUSXX").IsCashEquivalent);
        Assert.False(result.Lots.Single(l => l.Ticker == "CB").IsCashEquivalent);
    }

    [Fact]
    public void A_row_with_no_ticker_at_all_is_still_skipped()
    {
        var result = TaxLotParser.Parse($"{Header}\n\"Self-Directed\",\"...8197\",\"Equity\",\"SOMETHING ODD\",,5,10,09/25/2026 08:00:00,01/01/2026,10\n");

        Assert.Empty(result.Lots);
        Assert.Contains(result.Skipped, s => s.Contains("SOMETHING ODD"));
    }
}

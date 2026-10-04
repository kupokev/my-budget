using MyBudget.Engines.Import;

namespace MyBudget.Engines.Tests;

/// <summary>
/// Vanguard's 401(k) QFX. An investment OFX has purchases and positions where a bank OFX has
/// &lt;STMTTRN&gt; lines, so it has to reach the lot importer rather than the statement one — before
/// this it parsed to nothing and reported "No &lt;STMTTRN&gt; blocks found".
/// </summary>
public class InvestmentOfxTests
{
    // Trimmed from the real download: two purchases, one fee, the position and the securities list.
    // OFX is SGML — leaf tags are never closed, and the whole file is one line.
    private const string Qfx =
        "<OFX><INVSTMTMSGSRSV1><INVSTMTTRNRS><INVSTMTRS><CURDEF>USD" +
        "<INVACCTFROM><BROKERID>vanguard.com<ACCTID>93793</INVACCTFROM><INVTRANLIST>" +
        "<BUYMF><INVBUY><INVTRAN><FITID>A1<DTTRADE>20260402160000.000[-5:EST]</INVTRAN>" +
        "<SECID><UNIQUEID>92202V641<UNIQUEIDTYPE>CUSIP</SECID><UNITS>4.032<UNITPRICE>74.41<TOTAL>300.0" +
        "<INV401KSOURCE>PRETAX</INVBUY><BUYTYPE>BUY</BUYMF>" +
        "<BUYMF><INVBUY><INVTRAN><FITID>A2<DTTRADE>20260917160000.000[-5:EST]</INVTRAN>" +
        "<SECID><UNIQUEID>92202V641<UNIQUEIDTYPE>CUSIP</SECID><UNITS>3.572<UNITPRICE>83.99<TOTAL>300.0" +
        "<INV401KSOURCE>PRETAX</INVBUY><BUYTYPE>BUY</BUYMF>" +
        "<INVEXPENSE><INVTRAN><FITID>F1<DTTRADE>20260904160000.000[-5:EST]</INVTRAN>" +
        "<SECID><UNIQUEID>92202V641<UNIQUEIDTYPE>CUSIP</SECID><TOTAL>-10.5</INVEXPENSE>" +
        "</INVTRANLIST><INVPOSLIST><POSMF><INVPOS><SECID><UNIQUEID>VGI001480<UNIQUEIDTYPE>CUSIP</SECID>" +
        "<UNITS>7.500<UNITPRICE>84.44<MKTVAL>633.30<DTPRICEASOF>20260928160000.000[-5:EST]</INVPOS></POSMF></INVPOSLIST>" +
        "</INVSTMTRS></INVSTMTTRNRS></INVSTMTMSGSRSV1><SECLISTMSGSRSV1><SECLIST><MFINFO><SECINFO>" +
        "<SECID><UNIQUEID>VGI001480<UNIQUEIDTYPE>CUSIP</SECID><SECNAME>Target Retire 2050 Tr II" +
        "<TICKER>VGI001480<UNITPRICE>84.44</SECINFO></MFINFO></SECLIST></SECLISTMSGSRSV1></OFX>";

    [Fact]
    public void It_is_routed_to_the_lot_importer_not_the_statement_one()
        => Assert.Equal(ImportKind.TaxLots, ImportFileKind.Detect("Vanguard - OfxDownload.qfx", Qfx));

    [Fact]
    public void Purchases_become_lots_priced_from_the_position()
    {
        var r = InvestmentOfxParser.Parse(Qfx);

        Assert.Equal(2, r.Lots.Count);

        var first = r.Lots[0];
        Assert.Equal(new DateOnly(2026, 4, 2), first.Acquired);
        Assert.Equal(4.032m, first.Quantity);
        Assert.Equal(74.41m, first.UnitCost);
        Assert.Equal(84.44m, first.Price);                              // today's price, from the position
        Assert.Equal(new DateOnly(2026, 9, 28), first.PriceDate);
        Assert.Equal("Target Retire 2050 Tr II", first.Description);
    }

    [Fact]
    public void A_purchase_labelled_with_the_cusip_still_finds_the_fund_named_elsewhere()
    {
        // Vanguard tags purchases with the fund's CUSIP but the position with its own internal id.
        // With one fund in the file there is no ambiguity, so both must land on the same holding.
        var r = InvestmentOfxParser.Parse(Qfx);

        Assert.All(r.Lots, l => Assert.Equal("VGI001480", l.Ticker));
    }

    [Fact]
    public void Fees_are_recorded_as_data_because_plan_costs_are_worth_totalling()
    {
        var r = InvestmentOfxParser.Parse(Qfx);

        // A fee carries no share count, so it cannot be a lot — but it is a real cost taken straight
        // out of the balance, and the point of keeping it is to be able to add it up.
        var fee = Assert.Single(r.Fees!);
        Assert.Equal(10.50m, fee.Amount);          // positive: a cost, not a negative holding
        Assert.Equal(new DateOnly(2026, 9, 4), fee.Date);
        Assert.Equal("VGI001480", fee.Ticker);
    }

    [Fact]
    public void A_mismatch_against_the_held_position_is_stated_not_hidden()
    {
        var r = InvestmentOfxParser.Parse(Qfx);

        // 4.032 + 3.572 bought against 7.500 held: the difference went in fees.
        var warning = Assert.Single(r.Warnings);
        Assert.Contains("7.5", warning);
        Assert.Contains("fee", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_funds_are_marked_as_priced_by_the_statement()
    {
        var r = InvestmentOfxParser.Parse(Qfx);

        // "VGI001480" is Vanguard's own identifier for a collective trust sold only inside the plan.
        // It is a real position, not cash — it just cannot be looked up, so nothing should try.
        Assert.All(r.Lots, l => Assert.True(l.PricedFromStatement));
        Assert.All(r.Lots, l => Assert.False(l.IsCashEquivalent));
    }

    [Fact]
    public void The_position_the_statement_reports_is_handed_to_the_importer()
    {
        var r = InvestmentOfxParser.Parse(Qfx);

        // Purchases add to 7.604 but 7.500 is held: fees were taken in shares. Reporting that is not
        // enough — the importer needs the real figure or the holding stays overstated.
        var position = Assert.Single(r.Positions!);
        Assert.Equal("VGI001480", position.Ticker);
        Assert.Equal(7.500m, position.Units);
        Assert.Equal(new DateOnly(2026, 9, 28), position.AsOf);
        Assert.Equal(7.604m, r.Lots.Sum(l => l.Quantity));
    }

    [Fact]
    public void A_bank_ofx_is_still_a_statement()
    {
        const string bank = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260924<TRNAMT>-314.00<FITID>X1<NAME>PAYMENT</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";

        Assert.Equal(ImportKind.Statement, ImportFileKind.Detect("chase.qfx", bank));
    }

    [Fact]
    public void Plan_purchases_tagged_with_a_source_are_contributions()
    {
        var c = InvestmentOfxParser.Parse(Qfx).Contributions!;
        Assert.Equal(2, c.Count);
        Assert.All(c, x => Assert.Equal(MyBudget.Domain.ContributionKind.Personal, x.Kind));
        Assert.Equal(600m, c.Sum(x => x.Amount));
        Assert.Equal("401(k) pre-tax", c[0].Description);
    }

    [Fact]
    public void An_exchange_between_funds_is_not_a_contribution_but_a_match_is()
    {
        const string exchange =
            "<OFX><INVSTMTMSGSRSV1><INVSTMTTRNRS><INVSTMTRS><INVTRANLIST>" +
            "<SELLMF><INVSELL><INVTRAN><FITID>S1<DTTRADE>20260501</INVTRAN><SECID><UNIQUEID>AAA</SECID><UNITS>-10<UNITPRICE>50<TOTAL>500<INV401KSOURCE>PRETAX</INVSELL><SELLTYPE>SELL</SELLMF>" +
            "<BUYMF><INVBUY><INVTRAN><FITID>B1<DTTRADE>20260501</INVTRAN><SECID><UNIQUEID>BBB</SECID><UNITS>5<UNITPRICE>100<TOTAL>-500<INV401KSOURCE>PRETAX</INVBUY><BUYTYPE>BUY</BUYMF>" +
            "<BUYMF><INVBUY><INVTRAN><FITID>B2<DTTRADE>20260501</INVTRAN><SECID><UNIQUEID>BBB</SECID><UNITS>1.5<UNITPRICE>100<TOTAL>-150<INV401KSOURCE>MATCH</INVBUY><BUYTYPE>BUY</BUYMF>" +
            "</INVTRANLIST></INVSTMTRS></INVSTMTTRNRS></INVSTMTMSGSRSV1></OFX>";
        var c = Assert.Single(InvestmentOfxParser.Parse(exchange).Contributions!);
        Assert.Equal(MyBudget.Domain.ContributionKind.Employer, c.Kind);
        Assert.Equal(150m, c.Amount);
    }

    [Fact]
    public void Brokerage_deposits_and_withdrawals_are_contributions_but_interest_is_not()
    {
        const string brokerage =
            "<OFX><INVSTMTMSGSRSV1><INVSTMTTRNRS><INVSTMTRS><INVTRANLIST>" +
            "<INVBANKTRAN><STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260105<TRNAMT>7000.00<FITID>D1<NAME>ACH DEPOSIT</STMTTRN><SUBACCTFUND>CASH</INVBANKTRAN>" +
            "<INVBANKTRAN><STMTTRN><TRNTYPE>INT<DTPOSTED>20260131<TRNAMT>12.34<FITID>I1<NAME>INTEREST</STMTTRN><SUBACCTFUND>CASH</INVBANKTRAN>" +
            "<INVBANKTRAN><STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260301<TRNAMT>-250.00<FITID>W1<NAME>TRANSFER OUT</STMTTRN><SUBACCTFUND>CASH</INVBANKTRAN>" +
            "</INVTRANLIST></INVSTMTRS></INVSTMTTRNRS></INVSTMTMSGSRSV1></OFX>";
        var c = InvestmentOfxParser.Parse(brokerage).Contributions!;
        Assert.Equal(2, c.Count);
        Assert.Equal((7_000m, MyBudget.Domain.ContributionKind.Personal, "D1"), (c[0].Amount, c[0].Kind, c[0].ExternalId));
        Assert.Equal((250m, MyBudget.Domain.ContributionKind.Withdrawal), (c[1].Amount, c[1].Kind));
    }

    // The shape of Chase's investment activity export, trimmed: a bank-link deposit, the sweep moving it
    // inside the account, a sale, a dividend and interest.
    private const string ChaseActivity =
        "\uFEFFTrade Date,Post Date,Settlement Date,Account Name,Account Number,Account Type,Type,Description,Cusip,Ticker,Security Type,Local Currency,Price USD,Price Local,Quantity,G/L Short USD,G/L Short Local,G/L Long USDs,G/L Long Local,Amount USD,Amount Local,Income USD,Income Local,Balance,Commissions USD,Commissions Local,Tran Code,Tran Code Description,Broker,Check Number,Tax Withheld\n" +
        "\"3/2/2026\",\"3/2/2026\",\"3/2/2026\",\"Roth IRA\",\"...0000\",\"Brokerage\",\"Interest\",\"CHASE IRA DEPOSIT SWEEP MONTHLY INTEREST\",\"\",\"\",\"Other\",\"USD\",\"\",\"\",\"0\",\"\",\"\",\"\",\"\",\"0.01\",\"0.01\",\"\",\"\",\"0\",\"\",\"\",\"0\",\"Interest\",\"\",\"0\",\"0\"\n" +
        "\"2/18/2026\",\"2/18/2026\",\"2/18/2026\",\"Roth IRA\",\"...0000\",\"Brokerage\",\"BNK\",\"BANKLINK ACH PULL IRA:C2025RTHB 70658546\",\"\",\"\",\"Other\",\"USD\",\"\",\"\",\"0\",\"\",\"\",\"\",\"\",\"1400\",\"1400\",\"\",\"\",\"0\",\"\",\"\",\"0\",\"BNK\",\"\",\"0\",\"0\"\n" +
        "\"2/18/2026\",\"2/18/2026\",\"2/18/2026\",\"Roth IRA\",\"...0000\",\"Brokerage\",\"DBS\",\"CHASE IRA DEPOSIT SWEEP INTRA-DAY DEPOSIT\",\"\",\"QDERQ\",\"Money Market\",\"USD\",\"1\",\"1\",\"1400\",\"\",\"\",\"\",\"\",\"-1400\",\"-1400\",\"\",\"\",\"0\",\"\",\"\",\"0\",\"DBS\",\"\",\"0\",\"0\"\n" +
        "\"12/29/2025\",\"12/29/2025\",\"12/30/2025\",\"Roth IRA\",\"...0000\",\"Brokerage\",\"Sell\",\"PIMCO HIGH INCOME FUND\",\"722014107\",\"PHK\",\"Stock\",\"USD\",\"4.85\",\"4.85\",\"-223\",\"\",\"\",\"\",\"\",\"1081.55\",\"1081.55\",\"\",\"\",\"0\",\"\",\"\",\"0\",\"Sell\",\"\",\"0\",\"0\"\n" +
        "\"10/1/2025\",\"10/1/2025\",\"10/1/2025\",\"Roth IRA\",\"...0000\",\"Brokerage\",\"Dividend\",\"PIMCO HIGH INCOME FUND\",\"722014107\",\"PHK\",\"Stock\",\"USD\",\"\",\"\",\"0\",\"\",\"\",\"\",\"\",\"0.11\",\"0.11\",\"\",\"\",\"0\",\"\",\"\",\"0\",\"Dividend\",\"\",\"0\",\"0\"\n";

    [Fact]
    public void A_chase_activity_export_goes_to_the_investment_importer()
        => Assert.Equal(ImportKind.TaxLots, ImportFileKind.Detect("chase roth ira transactions.csv", ChaseActivity));

    [Fact]
    public void Only_the_bank_link_deposit_is_a_contribution_and_the_file_covers_from_its_first_day()
    {
        var r = BrokerageActivityParser.Parse(ChaseActivity);
        var c = Assert.Single(r.Contributions!);
        Assert.Equal((new DateOnly(2026, 2, 18), 1_400m, MyBudget.Domain.ContributionKind.Personal), (c.Date, c.Amount, c.Kind));
        Assert.Contains("C2025RTHB", c.Description);
        Assert.Equal(new DateOnly(2025, 10, 1), r.ContributionsCoverFrom);
        Assert.Empty(r.Warnings);
    }
}

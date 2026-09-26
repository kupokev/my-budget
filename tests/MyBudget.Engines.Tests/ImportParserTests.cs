using MyBudget.Engines.Import;

namespace MyBudget.Engines.Tests;

public class ImportParserTests
{
    [Fact]
    public void Chase_card_csv_is_detected_and_charges_are_negative()
    {
        const string csv = "Transaction Date,Post Date,Description,Category,Type,Amount,Memo\n" +
                           "09/18/2026,09/19/2026,HULU 877-8244858 CA,Entertainment,Sale,-18.99,\n" +
                           "09/20/2026,09/21/2026,Payment Thank You-Mobile,,Payment,500.00,\n" +
                           "09/22/2026,09/23/2026,\"AMAZON.COM*2K4T9, SEATTLE\",Shopping,Sale,-42.17,\n";
        var r = CsvStatementParser.Parse(csv);
        Assert.Equal("chase-card", r.Profile);
        Assert.Equal(3, r.Transactions.Count);
        Assert.Equal(-18.99m, r.Transactions[0].Amount);
        Assert.Equal(new DateOnly(2026, 9, 18), r.Transactions[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 19), r.Transactions[0].PostedDate);
        Assert.Equal("Entertainment", r.Transactions[0].SourceCategory);
        Assert.Equal(500m, r.Transactions[1].Amount);
        Assert.Equal("AMAZON.COM*2K4T9, SEATTLE", r.Transactions[2].Description); // quoted field with comma
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Capital_one_debit_credit_columns_and_pnc_withdrawals_deposits()
    {
        var capOne = CsvStatementParser.Parse("Transaction Date,Posted Date,Card No.,Description,Category,Debit,Credit\n2026-09-05,2026-09-06,1234,COSTCO WHSE #1234,Merchandise,65.00,\n2026-09-07,2026-09-08,1234,CAPITAL ONE MOBILE PYMT,Payment/Credit,,200.00\n");
        Assert.Equal("capital-one", capOne.Profile);
        Assert.Equal(-65m, capOne.Transactions[0].Amount);
        Assert.Equal(200m, capOne.Transactions[1].Amount);

        var pnc = CsvStatementParser.Parse("Date,Description,Withdrawals,Deposits,Category,Balance\n09/12/2026,ATT PAYMENT,85.00,,Utilities,2450.10\n09/13/2026,DIRECT DEPOSIT RIDGELINE PARTNERS,,4222.07,Income,6672.17\n");
        Assert.Equal("pnc", pnc.Profile);
        Assert.Equal(-85m, pnc.Transactions[0].Amount);
        Assert.Equal(4_222.07m, pnc.Transactions[1].Amount);
    }

    [Fact]
    public void Amex_and_discover_positive_charges_are_flipped_to_money_out()
    {
        var amex = CsvStatementParser.Parse("Date,Description,Card Member,Account #,Amount\n09/15/2026,HILTON GARDEN INN ST LOUIS MO,KEVIN,-11009,189.40\n09/16/2026,AMEX PAYMENT RECEIVED,KEVIN,-11009,-300.00\n");
        Assert.Equal("amex", amex.Profile);
        Assert.Equal(-189.40m, amex.Transactions[0].Amount);
        Assert.Equal(300m, amex.Transactions[1].Amount);

        var discover = CsvStatementParser.Parse("Trans. Date,Post Date,Description,Amount,Category\n09/01/2026,09/02/2026,SHELL OIL 57444,45.20,Gasoline\n");
        Assert.Equal("discover", discover.Profile);
        Assert.Equal(-45.20m, discover.Transactions[0].Amount);
    }

    [Fact]
    public void Wells_fargo_headerless_and_parenthesised_negatives()
    {
        var wf = CsvStatementParser.Parse("\"09/10/2026\",\"-60.00\",\"*\",\"\",\"CITY OF SPRINGFIELD WATER\"\n\"09/11/2026\",\"1200.00\",\"*\",\"\",\"ONLINE TRANSFER FROM CHK\"\n");
        Assert.Equal("wells-fargo", wf.Profile);
        Assert.Equal(-60m, wf.Transactions[0].Amount);
        Assert.Equal("CITY OF SPRINGFIELD WATER", wf.Transactions[0].Description);

        Assert.True(CsvStatementParser.TryMoney("($1,234.56)", out var paren));
        Assert.Equal(-1_234.56m, paren);
        Assert.True(CsvStatementParser.TryMoney("45.20-", out var trailing));
        Assert.Equal(-45.20m, trailing);
    }

    [Fact]
    public void Ofx_sgml_style_reads_every_stmttrn_with_fitid()
    {
        const string ofx = "OFXHEADER:100\nDATA:OFXSGML\n<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>\n" +
                           "<STMTTRN>\n<TRNTYPE>DEBIT\n<DTPOSTED>20260918120000.000[-5:CST]\n<DTUSER>20260917\n<TRNAMT>-140.00\n<FITID>2026091801\n<NAME>AMEREN MISSOURI\n<MEMO>ELECTRIC\n</STMTTRN>\n" +
                           "<STMTTRN>\n<TRNTYPE>CREDIT\n<DTPOSTED>20260919\n<TRNAMT>4222.07\n<FITID>2026091901\n<NAME>RIDGELINE PARTNERS PAYROLL\n</STMTTRN>\n" +
                           "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        Assert.True(OfxStatementParser.LooksLikeOfx(ofx));
        var r = OfxStatementParser.Parse(ofx);
        Assert.Equal(2, r.Transactions.Count);
        Assert.Equal(new DateOnly(2026, 9, 17), r.Transactions[0].Date);        // DTUSER preferred
        Assert.Equal(new DateOnly(2026, 9, 18), r.Transactions[0].PostedDate);
        Assert.Equal(-140m, r.Transactions[0].Amount);
        Assert.Equal("2026091801", r.Transactions[0].ExternalId);
        Assert.Equal("AMEREN MISSOURI", r.Transactions[0].Description);
        Assert.Equal("ELECTRIC", r.Transactions[0].Memo);
        Assert.Equal(4_222.07m, r.Transactions[1].Amount);
    }

    [Theory]
    [InlineData("SQ *BLUE BOTTLE COFFEE 1234 SAN FRANCISCO CA", "Blue Bottle Coffee")]
    [InlineData("HULU 877-8244858 CA", "Hulu")]
    [InlineData("COSTCO WHSE #1234", "Costco Whse")]
    [InlineData("AMZN Mktp US*2K4T9Q1", "Amzn Mktp Us")]
    [InlineData("AMAZON.COM*2K4T9 SEATTLE WA", "Amazon.com")]
    [InlineData("SHELL OIL 57444 ST LOUIS MO", "Shell Oil")]
    [InlineData("ATT PAYMENT", "Att Payment")]
    public void Merchant_names_are_cleaned_for_grouping(string raw, string expected) => Assert.Equal(expected, Merchants.Normalize(raw));

    [Fact]
    public void Hash_ids_are_stable_and_distinguish_repeated_identical_lines()
    {
        var a = Merchants.HashId("card:1", new(2026, 9, 18), -18.99m, "HULU 877-8244858 CA");
        var b = Merchants.HashId("card:1", new(2026, 9, 18), -18.99m, "hulu 877-8244858 ca ");
        var c = Merchants.HashId("card:1", new(2026, 9, 18), -18.99m, "HULU 877-8244858 CA", occurrence: 1);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(32, a.Length);
    }
}

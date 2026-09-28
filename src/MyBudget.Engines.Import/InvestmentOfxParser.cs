using System.Globalization;
using System.Text.RegularExpressions;

namespace MyBudget.Engines.Import;

/// <summary>
/// An investment OFX/QFX — what a 401(k) or brokerage hands you, as opposed to the bank statements
/// <see cref="OfxStatementParser"/> reads. Those carry &lt;STMTTRN&gt; blocks; this one carries
/// &lt;BUYMF&gt; purchases, &lt;INVEXPENSE&gt; fees, an &lt;INVPOSLIST&gt; of what is held now and a
/// &lt;SECLIST&gt; naming the funds.
///
/// It produces the same <see cref="ParsedLot"/> shape as a tax-lot export, so the importer that
/// already turns lots into holdings and trades needs no second implementation.
///
/// OFX is SGML, not XML: leaf tags are not closed, so each block is read with a regex rather than a
/// document parser.
/// </summary>
public static partial class InvestmentOfxParser
{
    public static bool LooksLikeInvestmentOfx(string content) =>
        content.Contains("<INVSTMTRS>", StringComparison.OrdinalIgnoreCase)
        || content.Contains("<INVTRANLIST>", StringComparison.OrdinalIgnoreCase)
        || content.Contains("<INVPOSLIST>", StringComparison.OrdinalIgnoreCase);

    public static LotParseResult Parse(string content)
    {
        var warnings = new List<string>();
        var skipped = new List<string>();
        var lots = new List<ParsedLot>();

        var accountId = Tag(content, "ACCTID");

        // The securities list names the funds. A 401(k) fund has no market ticker, so the identifier
        // the file gives is used as one and the real name is carried alongside it.
        var securities = new Dictionary<string, (string Symbol, string Name)>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in SecInfoRegex().Matches(content))
        {
            var id = Tag(m.Value, "UNIQUEID");
            if (string.IsNullOrEmpty(id)) continue;
            var ticker = Tag(m.Value, "TICKER");
            securities[id] = (string.IsNullOrWhiteSpace(ticker) ? id : ticker, Tag(m.Value, "SECNAME"));
        }

        // What is held now, and what it is worth. The price here is the one the whole import is valued at.
        decimal? price = null;
        DateOnly? pricedAsOf = null;
        decimal? positionUnits = null;
        string? positionId = null;
        foreach (Match m in PositionRegex().Matches(content))
        {
            positionId = Tag(m.Value, "UNIQUEID");
            if (Money(Tag(m.Value, "UNITPRICE")) is { } p) price = p;
            if (Money(Tag(m.Value, "UNITS")) is { } u) positionUnits = u;
            if (OfxDate(Tag(m.Value, "DTPRICEASOF")) is { } d) pricedAsOf = d;
        }

        // Vanguard labels a purchase with the fund's CUSIP but the position with its own internal id,
        // so a trade's security is often not in the list. With a single fund in the file there is no
        // ambiguity about which one it is; with several there would be, so those are reported instead.
        (string Symbol, string Name)? only = securities.Count == 1 ? securities.Values.First() : null;

        (string Symbol, string Name) Resolve(string id)
        {
            if (securities.TryGetValue(id, out var known)) return known;
            if (only is { } single) return single;
            return (id, "");
        }

        foreach (Match m in BuyRegex().Matches(content))
        {
            var id = Tag(m.Value, "UNIQUEID");
            var units = Money(Tag(m.Value, "UNITS"));
            var unitPrice = Money(Tag(m.Value, "UNITPRICE"));
            var traded = OfxDate(Tag(m.Value, "DTTRADE"));

            if (units is not { } u || u == 0 || unitPrice is not { } up || traded is not { } date)
            {
                warnings.Add($"A purchase was missing units, price or trade date and was left out (id {Tag(m.Value, "FITID")}).");
                continue;
            }

            var (symbol, name) = Resolve(id);
            // A security in a plan statement is identified by the plan, not by a market symbol, and the
            // statement is the only place its price exists.
            lots.Add(new ParsedLot(symbol.ToUpperInvariant(), name, u, up, date, price, pricedAsOf, null, accountId, PricedFromStatement: true));
        }

        // A fee is money out with no units attached, so it is not a lot — but it is not noise either.
        // Recorded as its own thing: what a plan costs to run is a figure worth adding up.
        var fees = new List<ParsedFee>();
        foreach (Match m in ExpenseRegex().Matches(content))
        {
            var total = Money(Tag(m.Value, "TOTAL"));
            var date = OfxDate(Tag(m.Value, "DTTRADE"));
            if (total is not { } amount || date is not { } on)
            {
                warnings.Add($"A fee was missing its amount or date and was left out (id {Tag(m.Value, "FITID")}).");
                continue;
            }

            var (symbol, _) = Resolve(Tag(m.Value, "UNIQUEID"));
            fees.Add(new ParsedFee(symbol.ToUpperInvariant(), on, Math.Abs(amount), Tag(m.Value, "MEMO") is { Length: > 0 } memo ? memo : "Plan fee"));
        }

        if (lots.Count == 0 && fees.Count == 0)
            warnings.Add("No purchases or fees found. This may be a bank statement rather than an investment one.");

        // The statement is the authority on what is held. Fees here carry no share count, so the
        // purchases alone always overstate the position; reporting the gap is not enough, the importer
        // is given the real figure to settle on.
        var positions = new List<ParsedPosition>();
        if (positionUnits is { } held)
        {
            var (symbol, _) = Resolve(positionId ?? "");
            positions.Add(new ParsedPosition(symbol.ToUpperInvariant(), held, pricedAsOf ?? DateOnly.FromDateTime(DateTime.Today)));

            if (lots.Count > 0 && Math.Round(lots.Sum(l => l.Quantity) - held, 4) != 0)
                warnings.Add($"{symbol}: purchases in this file total {lots.Sum(l => l.Quantity):0.####} shares against {held:0.####} held" +
                             (fees.Count > 0 ? $"; the {fees.Count} fee(s) recorded were taken in shares, and the difference has been applied." : "; the difference has been recorded."));
        }

        return new LotParseResult(lots, skipped, warnings, positions, fees);
    }

    private static string Tag(string block, string name)
    {
        var m = Regex.Match(block, $"<{name}>([^<]*)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static decimal? Money(string raw) =>
        decimal.TryParse(raw.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>OFX dates are yyyyMMdd with an optional time and bracketed zone: 20260402160000.000[-5:EST].</summary>
    private static DateOnly? OfxDate(string raw) =>
        raw.Length >= 8 && DateOnly.TryParseExact(raw[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    [GeneratedRegex(@"<BUYMF>(.*?)</BUYMF>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BuyRegex();

    [GeneratedRegex(@"<INVEXPENSE>(.*?)</INVEXPENSE>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ExpenseRegex();

    [GeneratedRegex(@"<INVPOS>(.*?)</INVPOS>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PositionRegex();

    [GeneratedRegex(@"<SECINFO>(.*?)</SECINFO>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SecInfoRegex();
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace MyBudget.Engines.Import;

/// <summary>
/// Tolerant OFX / QFX reader. Handles SGML-style files (no closing tags) and XML-style ones; reads every
/// STMTTRN block: TRNTYPE, DTPOSTED, DTUSER, TRNAMT, FITID, NAME, MEMO. OFX amounts are already signed
/// from the owner's side (charges negative).
/// </summary>
public static partial class OfxStatementParser
{
    public static bool LooksLikeOfx(string content)
        => content.Contains("<OFX>", StringComparison.OrdinalIgnoreCase) || content.Contains("OFXHEADER", StringComparison.OrdinalIgnoreCase) || content.Contains("<STMTTRN>", StringComparison.OrdinalIgnoreCase);

    public static ParseResult Parse(string content)
    {
        var warnings = new List<string>();
        var list = new List<ParsedTransaction>();
        foreach (Match block in StmtTrn().Matches(content))
        {
            var body = block.Groups[1].Value;
            var posted = Tag(body, "DTPOSTED");
            var user = Tag(body, "DTUSER");
            var amountText = Tag(body, "TRNAMT");
            var fitId = Tag(body, "FITID");
            var name = Tag(body, "NAME");
            var memo = Tag(body, "MEMO");
            var type = Tag(body, "TRNTYPE");
            if (!TryOfxDate(posted, out var postedDate)) { warnings.Add($"Transaction {fitId ?? "?"}: unreadable DTPOSTED '{posted}', skipped."); continue; }
            if (!decimal.TryParse(amountText, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) { warnings.Add($"Transaction {fitId ?? "?"}: unreadable TRNAMT '{amountText}', skipped."); continue; }
            var date = TryOfxDate(user, out var userDate) ? userDate : postedDate;
            var desc = !string.IsNullOrWhiteSpace(name) ? name : !string.IsNullOrWhiteSpace(memo) ? memo : type ?? "(no description)";
            list.Add(new ParsedTransaction(date, postedDate, amount, desc.Trim(), string.IsNullOrWhiteSpace(fitId) ? null : fitId.Trim(), null, memo?.Trim()));
        }
        if (list.Count == 0 && warnings.Count == 0) warnings.Add("No <STMTTRN> blocks found.");
        return new ParseResult("ofx", list, warnings);
    }

    private static string? Tag(string body, string tag)
    {
        var m = Regex.Match(body, $@"<{tag}>([^<\r\n]*)", RegexOptions.IgnoreCase);
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim()) : null;
    }

    private static bool TryOfxDate(string? s, out DateOnly d)
    {
        d = default;
        if (string.IsNullOrWhiteSpace(s) || s.Length < 8) return false;
        return DateOnly.TryParseExact(s[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }

    [GeneratedRegex(@"<STMTTRN>(.*?)(?=</STMTTRN>|<STMTTRN>|</BANKTRANLIST>|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StmtTrn();
}

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyBudget.Engines.Import;

/// <summary>Turns "SQ *BLUE BOTTLE COFFEE 1234 SAN FRANCISCO CA" into "Blue Bottle Coffee" so lines group by payee (BIL-9).</summary>
public static partial class Merchants
{
    public static string Normalize(string description)
    {
        var s = description.Trim();
        s = Prefixes().Replace(s, "");                  // SQ *, TST*, PAYPAL *, POS DEBIT, etc.
        s = BankTails().Replace(s, " ");               // WEB ID: 123, PPD ID: 456, transaction#: 789, REF: …
        s = OrderCodes().Replace(s, " ");               // AMAZON.COM*2K4T9 → AMAZON.COM
        s = Phones().Replace(s, " ");                   // 877-8244858
        s = StoreNumbers().Replace(s, " ");             // #1234, store 0456, long digit runs
        s = Dates().Replace(s, " ");                    // 09/18, 09/18/26
        s = TrailingLocation().Replace(s, "");          // "... SAN FRANCISCO CA", "... ST LOUIS MO"
        s = Regex.Replace(s, @"[^\w&'\.\- ]", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', '-', '.', '*');
        if (s.Length == 0) return description.Trim();
        return TitleCase(s);
    }

    /// <summary>Plain title case; consistent rather than clever, since grouping only needs the same output for the same input.</summary>
    private static string TitleCase(string s)
        => string.Join(' ', s.Split(' ').Select(w => w.Length > 1 ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant() : w.ToUpperInvariant()));

    /// <summary>Stable id for CSV lines without an institution id: same date, amount, description and source → same id.</summary>
    public static string HashId(string source, DateOnly date, decimal amount, string description, int occurrence = 0)
    {
        var text = $"{source}|{date:yyyy-MM-dd}|{amount:0.00}|{description.Trim().ToUpperInvariant()}|{occurrence}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    [GeneratedRegex(@"^(SQ \*|SQ\*|TST\*|TST \*|PAYPAL \*|PP\*|POS DEBIT |DEBIT CARD PURCHASE |CHECKCARD |PURCHASE AUTHORIZED ON \d\d/\d\d )", RegexOptions.IgnoreCase)]
    private static partial Regex Prefixes();

    [GeneratedRegex(@"\b(WEB|PPD|CCD|ARC|TEL|CTX)\s+ID:\s*\S+|\btransaction#:\s*\d+|\bREF:\s*\S+|\bINFO:\s*\S+|\bIID:\s*\S+|\bTRN:\s*\S+|\bRECD:\s*\S+|\bABA/CONTR\s+BNK-\d+", RegexOptions.IgnoreCase)]
    private static partial Regex BankTails();

    /// <summary>A star followed by an order/reference code containing a digit.</summary>
    [GeneratedRegex(@"\*\s*[A-Z0-9]*\d[A-Z0-9]*", RegexOptions.IgnoreCase)]
    private static partial Regex OrderCodes();

    [GeneratedRegex(@"\b\d{3}[-\.]\d{3}[-\.]?\d{4}\b|\b\d{3}[-\.]\d{7}\b|\b8(00|33|44|55|66|77|88)-?\d{3}-?\d{4}\b")]
    private static partial Regex Phones();

    [GeneratedRegex(@"(#\s?\d+|\bSTORE\s?\d+|\b\d{4,}\b|\b[A-Z0-9]{2,}\d{3,}[A-Z0-9]*\b)", RegexOptions.IgnoreCase)]
    private static partial Regex StoreNumbers();

    [GeneratedRegex(@"\b\d{1,2}/\d{1,2}(/\d{2,4})?\b")]
    private static partial Regex Dates();

    [GeneratedRegex(@"\s+([A-Z][A-Za-z\.]+\s){0,2}(AL|AK|AZ|AR|CA|CO|CT|DE|FL|GA|HI|ID|IL|IN|IA|KS|KY|LA|ME|MD|MA|MI|MN|MS|MO|MT|NE|NV|NH|NJ|NM|NY|NC|ND|OH|OK|OR|PA|RI|SC|SD|TN|TX|UT|VT|VA|WA|WV|WI|WY)\s*$")]
    private static partial Regex TrailingLocation();
}

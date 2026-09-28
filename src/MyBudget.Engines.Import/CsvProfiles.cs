namespace MyBudget.Engines.Import;

/// <summary>How one institution lays out its CSV export. Column names are matched case-insensitively after trimming.</summary>
public sealed record CsvProfile(
    string Key,
    string Name,
    /// <summary>Header names that identify this layout (all must be present).</summary>
    IReadOnlyList<string> Signature,
    string DateColumn,
    string? PostedDateColumn,
    /// <summary>Single signed amount column, or null when debit/credit are separate.</summary>
    string? AmountColumn,
    string? DebitColumn,
    string? CreditColumn,
    string DescriptionColumn,
    string? CategoryColumn,
    string? MemoColumn,
    /// <summary>True when the file's positive amounts are charges (Amex, Discover): they are negated to "money out".</summary>
    bool PositiveIsCharge,
    string? DateFormat = null,
    bool HasHeader = true,
    string? Notes = null);

/// <summary>Built-in layouts for the institutions in the 2026 sheet. Unknown files fall back to a generic Date/Description/Amount guess.</summary>
public static class CsvProfiles
{
    public static readonly IReadOnlyList<CsvProfile> All =
    [
        new("chase-card", "Chase credit card", ["Transaction Date", "Post Date", "Description", "Amount"], "Transaction Date", "Post Date", "Amount", null, null, "Description", "Category", "Memo", false),
        new("chase-checking", "Chase checking / savings", ["Details", "Posting Date", "Description", "Amount"], "Posting Date", null, "Amount", null, null, "Description", null, "Type", false),
        new("capital-one", "Capital One card", ["Transaction Date", "Posted Date", "Description", "Debit", "Credit"], "Transaction Date", "Posted Date", null, "Debit", "Credit", "Description", "Category", null, false),
        new("amex", "American Express", ["Date", "Description", "Amount"], "Date", null, "Amount", null, null, "Description", "Category", "Extended Details", true,
            Notes: "Amex lists charges as positive amounts"),
        new("discover", "Discover", ["Trans. Date", "Post Date", "Description", "Amount"], "Trans. Date", "Post Date", "Amount", null, null, "Description", "Category", null, true,
            Notes: "Discover lists purchases as positive amounts"),
        new("citi", "Citi", ["Status", "Date", "Description", "Debit", "Credit"], "Date", null, null, "Debit", "Credit", "Description", null, null, false,
            Notes: "Citi's Debit column is already negative on some exports; handled"),
        new("pnc", "PNC", ["Date", "Description", "Withdrawals", "Deposits"], "Date", null, null, "Withdrawals", "Deposits", "Description", "Category", null, false),
        new("wells-fargo", "Wells Fargo", [], "1", null, "2", null, null, "5", null, null, false, HasHeader: false,
            Notes: "No header: date, amount, *, blank, description"),
        // Listed after the card profiles on purpose: this signature is a subset of Chase's card export,
        // which also has Transaction Date, Description, Type and Amount. Chase is matched first because
        // it additionally requires Post Date, which Wealthfront does not have.
        new("wealthfront", "Wealthfront cash account", ["Transaction date", "Description", "Type", "Amount"], "Transaction date", null, "Amount", null, null, "Description", null, "Type", false,
            Notes: "Amounts are already signed: deposits positive, withdrawals negative"),
        new("generic", "Generic (Date, Description, Amount)", ["Date", "Description", "Amount"], "Date", null, "Amount", null, null, "Description", null, null, false),
    ];

    public static CsvProfile? ByKey(string key) => All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Picks the first profile whose signature columns all appear in the header; Wells Fargo is detected by a header-less 5-column row.</summary>
    public static CsvProfile? Detect(IReadOnlyList<string> headerCells)
    {
        var set = headerCells.Select(h => h.Trim().Trim('"')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Most specific first: amex's signature is a subset of others, so check it late.
        foreach (var p in All.Where(p => p.Signature.Count > 0).OrderByDescending(p => p.Signature.Count))
            if (p.Signature.All(set.Contains)) return p;
        if (headerCells.Count == 5 && DateOnly.TryParseExact(headerCells[0].Trim('"'), "M/d/yyyy", out _)) return ByKey("wells-fargo");
        return null;
    }
}

namespace MyBudget.Engines.Import;

public enum ImportKind { Statement, TaxLots }

/// <summary>
/// Which importer a file is for, decided by the file rather than by which screen it was dropped on.
/// A brokerage account can hand you either a cash statement or a tax-lot export, so the account type
/// is a poor signal; the columns are definitive. Lives here so the UI and the API agree by
/// construction instead of each carrying its own guess.
/// </summary>
public static class ImportFileKind
{
    public static ImportKind Detect(string fileName, string content)
    {
        // OFX/QFX are always statements; they have no notion of a tax lot.
        if (fileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase)) return ImportKind.Statement;

        var rows = CsvStatementParser.ReadRows(content);
        if (rows.Count == 0) return ImportKind.Statement;

        var header = rows[0].Select(h => h.Trim().Trim('"')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Has(params string[] names) => names.Any(header.Contains);

        // The same four columns TaxLotParser requires. Anything short of all four is a statement, so a
        // near-miss still reaches the statement importer and reports its own missing columns.
        return Has("Ticker", "Symbol")
            && Has("Quantity", "Shares")
            && Has("Unit Cost", "Cost/Share", "Cost Per Share")
            && Has("Acquisition Date", "Acquired", "Date Acquired", "Open Date")
            ? ImportKind.TaxLots
            : ImportKind.Statement;
    }
}

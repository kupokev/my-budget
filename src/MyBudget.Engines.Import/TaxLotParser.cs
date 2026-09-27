namespace MyBudget.Engines.Import;

/// <summary>One open lot from a brokerage "tax lots" export: what to turn into a Buy trade.</summary>
public sealed record ParsedLot(string Ticker, string? Description, decimal Quantity, decimal UnitCost, DateOnly Acquired, decimal? Price, DateOnly? PriceDate, string? AccountName, string? AccountNumber);

public sealed record LotParseResult(IReadOnlyList<ParsedLot> Lots, IReadOnlyList<string> Skipped, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads a tax-lot export (J.P. Morgan / Chase layout: Ticker, Quantity, Unit Cost, Acquisition Date, Price, Pricing Date …).
/// Cash, money-market sweeps and rows without a ticker or acquisition date are skipped and listed. Column names are
/// matched case-insensitively so other brokerages' exports work when they use the same words.
/// </summary>
public static class TaxLotParser
{
    public static LotParseResult Parse(string content)
    {
        var rows = CsvStatementParser.ReadRows(content);
        var warnings = new List<string>(); var skipped = new List<string>(); var lots = new List<ParsedLot>();
        if (rows.Count == 0) return new([], [], ["File is empty."]);
        var header = rows[0].Select((h, i) => (h: h.Trim().Trim('"'), i)).GroupBy(x => x.h, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        int Col(params string[] names) { foreach (var n in names) if (header.TryGetValue(n, out var i)) return i; return -1; }
        var ticker = Col("Ticker", "Symbol"); var qty = Col("Quantity", "Shares"); var unit = Col("Unit Cost", "Cost/Share", "Cost Per Share"); var acq = Col("Acquisition Date", "Acquired", "Date Acquired", "Open Date");
        var desc = Col("Description", "Security"); var price = Col("Price", "Last Price"); var priceDate = Col("Pricing Date", "Price Date", "As of"); var cls = Col("Asset Class"); var acctName = Col("Account name", "Account"); var acctNo = Col("Account number");
        if (ticker < 0 || qty < 0 || unit < 0 || acq < 0)
            return new([], [], [$"Need Ticker, Quantity, Unit Cost and Acquisition Date columns; found: {string.Join(", ", header.Keys.Take(12))}…"]);

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count <= ticker || row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Count < 3 || (row.Count == 2 && row[0].Length == 1)) continue; // footnotes block
            string Cell(int i) => i >= 0 && i < row.Count ? row[i].Trim() : "";
            var t = Cell(ticker).ToUpperInvariant();
            var assetClass = Cell(cls);
            if (string.IsNullOrEmpty(t) || assetClass.Contains("Cash", StringComparison.OrdinalIgnoreCase) || assetClass.Contains("Money Market", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(Cell(desc))) skipped.Add($"{(t.Length > 0 ? t + " " : "")}{Cell(desc)} ({(string.IsNullOrEmpty(assetClass) ? "no ticker" : assetClass)})");
                continue;
            }
            if (!CsvStatementParser.TryMoney(Cell(qty), out var q) || q == 0) { warnings.Add($"Row {r + 1} ({t}): unreadable quantity '{Cell(qty)}'."); continue; }
            if (!CsvStatementParser.TryMoney(Cell(unit), out var u)) { warnings.Add($"Row {r + 1} ({t}): unreadable unit cost '{Cell(unit)}'."); continue; }
            if (!CsvStatementParser.TryDate(Cell(acq), null, out var a)) { warnings.Add($"Row {r + 1} ({t}): unreadable acquisition date '{Cell(acq)}'."); continue; }
            decimal? p = CsvStatementParser.TryMoney(Cell(price), out var pv) ? pv : null;
            DateOnly? pd = CsvStatementParser.TryDate(Cell(priceDate).Split(' ')[0], null, out var pdv) ? pdv : null;
            lots.Add(new ParsedLot(t, Cell(desc), q, u, a, p, pd, Cell(acctName), Cell(acctNo)));
        }
        return new(lots, skipped, warnings);
    }
}

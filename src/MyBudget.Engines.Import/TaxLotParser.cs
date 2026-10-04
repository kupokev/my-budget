namespace MyBudget.Engines.Import;

/// <summary>One open lot from a brokerage "tax lots" export: what to turn into a Buy trade.</summary>
public sealed record ParsedLot(string Ticker, string? Description, decimal Quantity, decimal UnitCost, DateOnly Acquired, decimal? Price, DateOnly? PriceDate, string? AccountName, string? AccountNumber, bool IsCashEquivalent = false, bool PricedFromStatement = false);

/// <summary>A charge the statement lists against a holding.</summary>
public sealed record ParsedFee(string Ticker, DateOnly Date, decimal Amount, string? Description);

/// <summary>What a statement says is held right now, as opposed to the rows that got it there.</summary>
public sealed record ParsedPosition(string Ticker, decimal Units, DateOnly AsOf);

/// <summary>
/// Money into or out of the account that the statement identifies as such. <paramref name="Amount"/>
/// is positive; <paramref name="Kind"/> says the direction. <paramref name="ExternalId"/> lets a
/// re-import recognise one it already has.
/// </summary>
public sealed record ParsedContribution(DateOnly Date, decimal Amount, MyBudget.Domain.ContributionKind Kind, string Description, string ExternalId);

public sealed record LotParseResult(IReadOnlyList<ParsedLot> Lots, IReadOnlyList<string> Skipped, IReadOnlyList<string> Warnings,
    /// <summary>Holdings the statement reports directly. Empty for a tax-lot export, which lists only lots.</summary>
    IReadOnlyList<ParsedPosition>? Positions = null,
    /// <summary>Charges the statement lists. Recorded rather than noted, so plan costs can be totalled.</summary>
    IReadOnlyList<ParsedFee>? Fees = null,
    /// <summary>Deposits, withdrawals and payroll contributions the statement lists. Empty for a tax-lot export.</summary>
    IReadOnlyList<ParsedContribution>? Contributions = null,
    /// <summary>
    /// The first day the file accounts for every movement of money in and out, when it is that kind of
    /// file (an activity export or an investment OFX). Null for a tax-lot export, which says nothing about it.
    /// </summary>
    DateOnly? ContributionsCoverFrom = null);

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
            if (string.IsNullOrEmpty(t))
            {
                if (!string.IsNullOrEmpty(Cell(desc))) skipped.Add($"{Cell(desc)} (no ticker)");
                continue;
            }

            // Money-market funds and the cash sweep are not tax lots — they hold at $1 and carry no
            // acquisition date — but they are real money, and leaving them out made the account total
            // read short by exactly their value. They come in as holdings priced from the file, dated
            // from the statement, so the account adds up. Short/long term is meaningless at a $1 NAV.
            var isCashLike = assetClass.Contains("Cash", StringComparison.OrdinalIgnoreCase)
                          || assetClass.Contains("Money Market", StringComparison.OrdinalIgnoreCase);

            if (!CsvStatementParser.TryMoney(Cell(qty), out var q) || q == 0) { warnings.Add($"Row {r + 1} ({t}): unreadable quantity '{Cell(qty)}'."); continue; }
            if (!CsvStatementParser.TryMoney(Cell(unit), out var u)) { warnings.Add($"Row {r + 1} ({t}): unreadable unit cost '{Cell(unit)}'."); continue; }

            decimal? p = CsvStatementParser.TryMoney(Cell(price), out var pv) ? pv : null;
            DateOnly? pd = CsvStatementParser.TryDate(Cell(priceDate).Split(' ')[0], null, out var pdv) ? pdv : null;

            if (!CsvStatementParser.TryDate(Cell(acq), null, out var a))
            {
                if (isCashLike && pd is { } priced) a = priced;
                else { warnings.Add($"Row {r + 1} ({t}): unreadable acquisition date '{Cell(acq)}'."); continue; }
            }
            lots.Add(new ParsedLot(t, Cell(desc), q, u, a, p, pd, Cell(acctName), Cell(acctNo), isCashLike));
        }
        return new(lots, skipped, warnings);
    }
}

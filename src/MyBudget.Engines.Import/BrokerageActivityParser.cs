using System.Globalization;
using MyBudget.Domain;

namespace MyBudget.Engines.Import;

/// <summary>
/// A brokerage's account-activity CSV (J.P. Morgan / Chase layout: Trade Date, Type, Description, Ticker,
/// Amount USD, Tran Code Description …). Chase offers no QFX for investment accounts, so this is the only
/// place its deposits and withdrawals can be read from.
///
/// Only money crossing the account's edge is taken. Each row's Type says what it is:
/// <list type="bullet">
/// <item><c>BNK</c> — a bank-link transfer: in when the amount is positive, out when negative. Its
/// description carries Chase's contribution code, e.g. <c>IRA:C2025RTHB</c> (a 2025 Roth contribution).</item>
/// <item><c>DBS</c> / <c>WDL</c> — cash moving into or out of the deposit sweep inside the account.</item>
/// <item>Buy, Sell, Reinvest, Exchange, Dividend, Interest — trades and earnings, not contributions.</item>
/// </list>
/// Any other type is listed in the warnings rather than guessed at, so a withdrawal code this hasn't
/// seen yet is noticed instead of silently dropped.
/// </summary>
public static class BrokerageActivityParser
{
    private static readonly HashSet<string> NotMoneyInOrOut = new(StringComparer.OrdinalIgnoreCase)
        { "DBS", "WDL", "Buy", "Sell", "Reinvest", "Exchange", "Dividend", "Interest" };

    public static bool LooksLikeActivity(IReadOnlyCollection<string> header) =>
        header.Contains("Trade Date", StringComparer.OrdinalIgnoreCase)
        && header.Contains("Tran Code Description", StringComparer.OrdinalIgnoreCase)
        && header.Contains("Amount USD", StringComparer.OrdinalIgnoreCase);

    public static LotParseResult Parse(string content)
    {
        var rows = CsvStatementParser.ReadRows(content);
        if (rows.Count == 0) return new([], [], ["File is empty."]);
        var header = rows[0].Select((h, i) => (h: h.Trim().Trim('"').TrimStart('\uFEFF'), i))
            .GroupBy(x => x.h, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        int Col(string name) => header.TryGetValue(name, out var i) ? i : -1;
        int date = Col("Trade Date"), type = Col("Type"), desc = Col("Description"), amount = Col("Amount USD");
        if (date < 0 || type < 0 || amount < 0)
            return new([], [], ["Need Trade Date, Type and Amount USD columns."]);

        var contributions = new List<ParsedContribution>();
        var warnings = new List<string>();
        var unknown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        DateOnly? earliest = null;
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Cell(int i) => i >= 0 && i < row.Count ? row[i].Trim() : "";
            if (!DateOnly.TryParse(Cell(date), CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var on)) continue;
            if (earliest is null || on < earliest) earliest = on;

            var kind = Cell(type);
            if (NotMoneyInOrOut.Contains(kind)) continue;
            if (!kind.Equals("BNK", StringComparison.OrdinalIgnoreCase))
            {
                unknown[kind] = unknown.GetValueOrDefault(kind) + 1;
                continue;
            }
            if (!decimal.TryParse(Cell(amount).Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) || value == 0) continue;
            var description = Cell(desc);
            contributions.Add(new ParsedContribution(on, Math.Abs(value), value > 0 ? ContributionKind.Personal : ContributionKind.Withdrawal,
                description.Length > 0 ? description : value > 0 ? "Deposit" : "Withdrawal",
                $"activity:{on:yyyyMMdd}:{value}:{description}"));
        }
        foreach (var (kind, count) in unknown)
            warnings.Add($"{count} row(s) of type \"{kind}\" weren't recognised and weren't counted as contributions. If any are money in or out of the account, add them by hand.");
        if (contributions.Count == 0)
            warnings.Add("No deposits or withdrawals in this file; the period it covers is recorded as having none.");

        return new LotParseResult([], [], warnings, null, null, contributions, earliest);
    }
}

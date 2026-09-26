using System.Globalization;

namespace MyBudget.Engines.Import;

public static class CsvStatementParser
{
    /// <summary>Parses a CSV export. With no profile key, the header is used to detect the institution.</summary>
    public static ParseResult Parse(string content, string? profileKey = null)
    {
        var rows = ReadRows(content);
        var warnings = new List<string>();
        if (rows.Count == 0) return new ParseResult("none", [], ["File is empty."]);

        var profile = profileKey is not null ? CsvProfiles.ByKey(profileKey) ?? throw new ArgumentException($"Unknown CSV profile '{profileKey}'.") : CsvProfiles.Detect(rows[0]);
        if (profile is null)
        {
            warnings.Add("Could not recognise the column layout; tried the generic Date/Description/Amount layout.");
            profile = CsvProfiles.ByKey("generic")!;
        }

        Dictionary<string, int> index;
        int start;
        if (profile.HasHeader)
        {
            index = rows[0].Select((h, i) => (h: h.Trim().Trim('"'), i)).GroupBy(x => x.h, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
            start = 1;
        }
        else
        {
            index = Enumerable.Range(0, rows[0].Count).ToDictionary(i => (i + 1).ToString(), i => i);
            start = 0;
        }

        int Col(string? name) => name is not null && index.TryGetValue(name, out var i) ? i : -1;
        var dateCol = Col(profile.DateColumn);
        var descCol = Col(profile.DescriptionColumn);
        if (dateCol < 0 || descCol < 0)
            return new ParseResult(profile.Key, [], [$"The file has no '{profile.DateColumn}' or '{profile.DescriptionColumn}' column for the {profile.Name} layout."]);
        var postedCol = Col(profile.PostedDateColumn);
        var amountCol = Col(profile.AmountColumn);
        var debitCol = Col(profile.DebitColumn);
        var creditCol = Col(profile.CreditColumn);
        var catCol = Col(profile.CategoryColumn);
        var memoCol = Col(profile.MemoColumn);

        var list = new List<ParsedTransaction>();
        for (var r = start; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace)) continue;
            string Cell(int i) => i >= 0 && i < row.Count ? row[i].Trim() : "";

            if (!TryDate(Cell(dateCol), profile.DateFormat, out var date)) { warnings.Add($"Row {r + 1}: unreadable date '{Cell(dateCol)}', skipped."); continue; }
            DateOnly? posted = TryDate(Cell(postedCol), profile.DateFormat, out var pd) ? pd : null;

            decimal amount;
            if (amountCol >= 0)
            {
                if (!TryMoney(Cell(amountCol), out amount)) { warnings.Add($"Row {r + 1}: unreadable amount '{Cell(amountCol)}', skipped."); continue; }
                if (profile.PositiveIsCharge) amount = -amount;
            }
            else
            {
                TryMoney(Cell(debitCol), out var debit);
                TryMoney(Cell(creditCol), out var credit);
                // Debit = money out. Some exports already sign debits negative; normalise to "out is negative".
                amount = -Math.Abs(debit) + Math.Abs(credit);
            }

            var desc = Cell(descCol);
            if (desc.Length == 0) desc = "(no description)";
            list.Add(new ParsedTransaction(date, posted, amount, desc, null, catCol >= 0 ? NullIfEmpty(Cell(catCol)) : null, memoCol >= 0 ? NullIfEmpty(Cell(memoCol)) : null));
        }
        return new ParseResult(profile.Key, list, warnings);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    public static bool TryDate(string s, string? format, out DateOnly d)
    {
        d = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Trim('"');
        if (format is not null && DateOnly.TryParseExact(s, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return true;
        foreach (var f in new[] { "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "M/d/yy", "MM/dd/yy", "yyyyMMdd", "d-MMM-yyyy" })
            if (DateOnly.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return true;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) { d = DateOnly.FromDateTime(dt); return true; }
        return false;
    }

    public static bool TryMoney(string s, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Trim('"').Replace("$", "").Replace(",", "").Replace(" ", "");
        var negative = false;
        if (s.StartsWith('(') && s.EndsWith(')')) { negative = true; s = s[1..^1]; }
        if (s.EndsWith('-')) { negative = true; s = s[..^1]; }
        if (!decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount)) return false;
        if (negative) amount = -Math.Abs(amount);
        return true;
    }

    /// <summary>RFC-4180-ish reader: quoted fields, doubled quotes, embedded newlines, CRLF or LF.</summary>
    public static List<List<string>> ReadRows(string content)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        content = content.TrimStart('﻿');
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows.Where(r => !(r.Count == 1 && string.IsNullOrWhiteSpace(r[0]))).ToList();
    }
}

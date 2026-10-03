using System.Text.RegularExpressions;

namespace MyBudget.Api;

/// <summary>
/// Turns a model's Markdown into plain prose for places that show text as-is. Asking the model not to
/// format is not enough — a small local model writes headings, bold and tables regardless — so the
/// monthly summary goes through this before it reaches the dashboard.
///
/// Headings and rules are dropped (they label, they don't say anything); list items and table rows
/// become sentences; emphasis and code marks are removed and the words kept.
/// </summary>
public static partial class PlainText
{
    public static string FromMarkdown(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sentences = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || Rule().IsMatch(line) || TableDivider().IsMatch(line) || line.StartsWith('#')) continue;

            if (line.StartsWith('|'))
            {
                // A header row is the one directly above the |---| divider: its labels aren't data.
                if (i + 1 < lines.Length && TableDivider().IsMatch(lines[i + 1].Trim())) continue;
                var cells = line.Trim('|').Split('|').Select(c => Inline(c)).Where(c => c.Length > 0 && c != "—" && c != "-");
                line = string.Join(", ", cells);
            }
            else
            {
                line = Inline(ListMarker().Replace(line, ""));
            }

            if (line.Length == 0) continue;
            sentences.Add(EndsSentence().IsMatch(line) ? line : line + ".");
        }

        return Spaces().Replace(string.Join(" ", sentences), " ").Trim();
    }

    private static string Inline(string s)
    {
        s = s.Replace("**", "").Replace("__", "").Replace("`", "");
        s = Emphasis().Replace(s, "$1");
        return s.Trim();
    }

    [GeneratedRegex(@"^([-*_]\s*){3,}$")] private static partial Regex Rule();
    [GeneratedRegex(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")] private static partial Regex TableDivider();
    [GeneratedRegex(@"^([-*+]|\d+[.)])\s+")] private static partial Regex ListMarker();
    [GeneratedRegex(@"(?<![\w*])[*_](\S(?:.*?\S)?)[*_](?![\w*])")] private static partial Regex Emphasis();
    [GeneratedRegex(@"[.!?:;]$")] private static partial Regex EndsSentence();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
}

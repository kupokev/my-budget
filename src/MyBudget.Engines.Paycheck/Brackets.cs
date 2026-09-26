namespace MyBudget.Engines.Paycheck;

public static class Brackets
{
    /// <summary>Marginal tax on <paramref name="taxable"/> across ordered brackets, with a human-readable trace.</summary>
    public static (decimal Tax, string Formula) Tax(decimal taxable, IReadOnlyList<Bracket> brackets)
    {
        if (taxable <= 0) return (0m, "taxable ≤ 0 → $0");
        var ordered = brackets.OrderBy(b => b.Over).ToList();
        decimal tax = 0;
        var parts = new List<string>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var lower = ordered[i].Over;
            if (taxable <= lower) break;
            var upper = i + 1 < ordered.Count ? ordered[i + 1].Over : decimal.MaxValue;
            var slice = Math.Min(taxable, upper) - lower;
            if (slice <= 0) continue;
            var piece = slice * ordered[i].Rate;
            tax += piece;
            parts.Add($"{slice:N2} × {ordered[i].Rate:P1}");
        }
        return (tax, string.Join(" + ", parts) + $" = {tax:N2}");
    }

    public static IReadOnlyList<Bracket> Scale(IReadOnlyList<Bracket> brackets, decimal factor)
        => brackets.Select(b => new Bracket(b.Over * factor, b.Rate)).ToList();
}

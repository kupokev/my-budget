using MyBudget.Domain;

namespace MyBudget.Engines.Investments;

/// <summary>
/// One holding's trades and its price history, oldest price first, with the account it sits in and the
/// dividends it paid out as cash (pay date and amount; reinvested ones are already trades).
/// </summary>
public sealed record HoldingHistory(string Ticker, IReadOnlyList<Trade> Trades, IReadOnlyList<(DateOnly Date, decimal Price)> Prices,
    int AccountId = 0, IReadOnlyList<(DateOnly Date, decimal Amount)>? Dividends = null)
{
    public IReadOnlyList<(DateOnly Date, decimal Amount)> CashDividends => Dividends ?? [];
}

public sealed record PortfolioPoint(DateOnly Date, decimal Value, decimal Cost, decimal Contributed);

/// <summary>
/// What the portfolio was worth on past dates, rebuilt from trades and stored prices rather than
/// from saved snapshots — so a corrected trade corrects the history too. Each point uses the same
/// rule as today's Market value tile: open lots as of that date (FIFO, <see cref="Portfolio.Analyze"/>)
/// × the latest close on or before it. The right-hand end of the chart therefore equals the tile.
/// </summary>
public static class PortfolioHistory
{
    /// <summary>
    /// Weekdays from <paramref name="from"/> to <paramref name="to"/> (markets don't close on weekends,
    /// so those points would only repeat Friday), thinned evenly to at most <paramref name="maxPoints"/>.
    /// The last date is always <paramref name="to"/>, even on a weekend, so the chart ends at today.
    /// </summary>
    public static IReadOnlyList<DateOnly> SampleDates(DateOnly from, DateOnly to, int maxPoints = 90)
    {
        var days = new List<DateOnly>();
        for (var d = from; d < to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days.Add(d);
        days.Add(to);
        if (days.Count <= maxPoints) return days;
        return Enumerable.Range(0, maxPoints)
            .Select(i => days[(int)Math.Round(i * (days.Count - 1) / (double)(maxPoints - 1))])
            .Distinct().ToList();
    }

    /// <summary>
    /// Market value, cost basis and net contributions (<see cref="Contributions.On"/>) of every
    /// holding on <paramref name="date"/>. A holding with shares
    /// but no close yet on or before the date (a statement-priced fund before its first statement) is
    /// valued at its earliest known price instead, so the line doesn't drop to zero for want of a quote;
    /// one with no price at all counts its cost but no value, as the tile does.
    /// </summary>
    public static PortfolioPoint ValueOn(IEnumerable<HoldingHistory> holdings, DateOnly date, IEnumerable<RecordedContribution>? recorded = null,
        IReadOnlyDictionary<int, DateOnly>? coverFrom = null)
    {
        decimal value = 0, cost = 0;
        foreach (var h in holdings)
        {
            var trades = h.Trades.Where(t => t.Date <= date).ToList();
            if (trades.Count == 0) continue;
            var s = Portfolio.Summarize(Portfolio.Analyze(trades, date), PriceOn(h.Prices, date), date);
            value += s.MarketValue ?? 0;
            cost += s.CostBasis;
        }
        return new PortfolioPoint(date, value, cost, Contributions.On(holdings, recorded ?? [], date, coverFrom));
    }

    private static decimal? PriceOn(IReadOnlyList<(DateOnly Date, decimal Price)> prices, DateOnly date)
    {
        if (prices.Count == 0) return null;
        int lo = 0, hi = prices.Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (prices[mid].Date <= date) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return found >= 0 ? prices[found].Price : prices[0].Price;
    }
}

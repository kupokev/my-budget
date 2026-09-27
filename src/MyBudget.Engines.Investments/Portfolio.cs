using MyBudget.Domain;

namespace MyBudget.Engines.Investments;

public sealed record Lot(int TradeId, DateOnly Acquired, decimal Shares, decimal CostPerShare, decimal RemainingShares, bool FromReinvest, decimal DisallowedLossAdded);

public enum GainTerm { Short, Long }

public sealed record RealizedGain(int SellTradeId, DateOnly SellDate, int LotTradeId, DateOnly Acquired, decimal Shares, decimal Proceeds, decimal CostBasis, decimal Gain, GainTerm Term, int DaysHeld,
    bool WashSale, decimal DisallowedLoss, string Formula);

public sealed record WashSaleWarning(int SellTradeId, DateOnly SellDate, decimal Loss, DateOnly WindowOpens, DateOnly WindowCloses, DateOnly EarliestSafeRepurchase,
    IReadOnlyList<int> ReplacementTradeIds, decimal DisallowedLoss, bool WindowStillOpen, string Message);

public sealed record PositionSummary(decimal Shares, decimal CostBasis, decimal? Price, DateOnly? PriceDate, decimal? MarketValue, decimal? UnrealizedGain, IReadOnlyList<Lot> OpenLots);

/// <summary>Lot-level accounting for one holding. Trades are matched FIFO; each buy or reinvest is its own lot.</summary>
public static class Portfolio
{
    public static decimal SharesHeldOn(IEnumerable<Trade> trades, DateOnly date)
        => trades.Where(t => t.Date <= date).Sum(t => t.Kind == TradeKind.Sell ? -t.Shares : t.Shares);

    /// <summary>Held more than one year (acquired date + 1 year &lt; sell date) is long term, per IRS holding-period rules.</summary>
    public static GainTerm TermFor(DateOnly acquired, DateOnly sold) => sold > acquired.AddYears(1) ? GainTerm.Long : GainTerm.Short;

    public sealed record Result(IReadOnlyList<Lot> OpenLots, IReadOnlyList<RealizedGain> Realized, IReadOnlyList<WashSaleWarning> WashSales);

    /// <summary>
    /// Walks trades in date order: buys/reinvests open lots (cost = shares × price + fees), sells consume lots FIFO and
    /// record gains. Then wash-sale rule (INV-5): a loss on a sell is disallowed to the extent shares of the same
    /// holding were bought within 30 days before or after; the disallowed loss is added to the replacement lot's basis.
    /// </summary>
    public static Result Analyze(IEnumerable<Trade> trades, DateOnly asOf)
    {
        var ordered = trades.OrderBy(t => t.Date).ThenBy(t => t.Kind == TradeKind.Sell ? 1 : 0).ThenBy(t => t.Id).ToList();
        var lots = new List<MutableLot>();
        var realized = new List<RealizedGain>();

        foreach (var t in ordered)
        {
            if (t.Kind != TradeKind.Sell)
            {
                var cost = t.Shares * t.Price + t.Fees;
                lots.Add(new MutableLot(t.Id, t.Date, t.Shares, t.Shares == 0 ? 0 : cost / t.Shares, t.Shares, t.Kind == TradeKind.Reinvest));
                continue;
            }
            var toSell = t.Shares;
            var proceedsPerShare = t.Shares == 0 ? 0 : (t.Shares * t.Price - t.Fees) / t.Shares;
            foreach (var lot in lots.Where(l => l.Remaining > 0).OrderBy(l => l.Acquired).ThenBy(l => l.TradeId))
            {
                if (toSell <= 0) break;
                var take = Math.Min(toSell, lot.Remaining);
                var proceeds = R(take * proceedsPerShare);
                var basis = R(take * lot.CostPerShare);
                var term = TermFor(lot.Acquired, t.Date);
                realized.Add(new RealizedGain(t.Id, t.Date, lot.TradeId, lot.Acquired, take, proceeds, basis, R(proceeds - basis), term, t.Date.DayNumber - lot.Acquired.DayNumber, false, 0,
                    $"{take:0.####} sh × {proceedsPerShare:N4} − basis {take:0.####} × {lot.CostPerShare:N4} = {proceeds - basis:N2} ({term.ToString().ToLower()}-term, {t.Date.DayNumber - lot.Acquired.DayNumber} days)"));
                lot.Remaining -= take;
                toSell -= take;
            }
            if (toSell > 0)
                realized.Add(new RealizedGain(t.Id, t.Date, 0, t.Date, toSell, R(toSell * proceedsPerShare), 0, R(toSell * proceedsPerShare), GainTerm.Short, 0, false, 0, $"{toSell:0.####} sh sold with no matching lot: check the trade history"));
        }

        // Wash sales: for each sell lot-slice at a loss, replacement shares bought in [sell−30, sell+30] excluding the lot sold.
        var warnings = new List<WashSaleWarning>();
        var buys = ordered.Where(t => t.Kind != TradeKind.Sell).ToList();
        var adjusted = new List<RealizedGain>();
        var replacementUsed = new Dictionary<int, decimal>(); // trade id → shares already treated as replacement
        foreach (var sellGroup in realized.Where(g => g.Gain < 0).GroupBy(g => g.SellTradeId))
        {
            var sell = ordered.First(t => t.Id == sellGroup.Key);
            var opens = sell.Date.AddDays(-30); var closes = sell.Date.AddDays(30);
            var lossShares = sellGroup.Sum(g => g.Shares);
            var loss = -sellGroup.Sum(g => g.Gain);
            var replacements = buys.Where(b => b.Id != sellGroup.First().LotTradeId && b.Date >= opens && b.Date <= closes)
                .Select(b => (b, available: b.Shares - replacementUsed.GetValueOrDefault(b.Id))).Where(x => x.available > 0).ToList();
            var replacementShares = Math.Min(lossShares, replacements.Sum(x => x.available));
            var disallowed = lossShares == 0 ? 0 : R(loss * replacementShares / lossShares);
            var ids = new List<int>();
            var remaining = replacementShares;
            foreach (var (b, available) in replacements.OrderBy(x => x.b.Date))
            {
                if (remaining <= 0) break;
                var use = Math.Min(available, remaining);
                replacementUsed[b.Id] = replacementUsed.GetValueOrDefault(b.Id) + use;
                var lot = lots.First(l => l.TradeId == b.Id);
                var addBasis = disallowed * use / replacementShares;
                lot.CostPerShare += lot.Shares == 0 ? 0 : addBasis / lot.Shares;
                lot.DisallowedAdded += addBasis;
                ids.Add(b.Id);
                remaining -= use;
            }
            var windowOpen = closes >= asOf;
            var msg = disallowed > 0
                ? $"Sold at a {loss:C} loss on {sell.Date:MMM d}; {replacementShares:0.####} replacement shares bought within 30 days → {disallowed:C} of the loss is disallowed and added to the replacement lot's basis."
                : windowOpen ? $"Sold at a {loss:C} loss on {sell.Date:MMM d}; buying this holding before {closes.AddDays(1):MMM d, yyyy} would disallow the loss (including DRIP reinvestments)."
                : $"Sold at a {loss:C} loss on {sell.Date:MMM d}; window closed {closes:MMM d} with no repurchase, loss stands.";
            warnings.Add(new WashSaleWarning(sell.Id, sell.Date, loss, opens, closes, closes.AddDays(1), ids, disallowed, windowOpen && disallowed == 0, msg));
            foreach (var g in sellGroup)
            {
                var share = lossShares == 0 ? 0 : g.Shares / lossShares;
                var d = R(disallowed * share);
                adjusted.Add(g with { WashSale = d > 0, DisallowedLoss = d, Gain = R(g.Gain + d), Formula = g.Formula + (d > 0 ? $"; wash sale: {d:N2} disallowed" : "") });
            }
        }
        var finalRealized = realized.Where(g => g.Gain >= 0).Concat(adjusted).OrderBy(g => g.SellDate).ThenBy(g => g.Acquired).ToList();
        var openLots = lots.Where(l => l.Remaining > 0).Select(l => new Lot(l.TradeId, l.Acquired, l.Shares, R4(l.CostPerShare), l.Remaining, l.FromReinvest, R(l.DisallowedAdded))).ToList();
        return new Result(openLots, finalRealized, warnings);
    }

    public static PositionSummary Summarize(Result r, decimal? price, DateOnly? priceDate)
    {
        var shares = r.OpenLots.Sum(l => l.RemainingShares);
        var basis = R(r.OpenLots.Sum(l => l.RemainingShares * l.CostPerShare));
        var value = price is { } p ? R(shares * p) : (decimal?)null;
        return new PositionSummary(shares, basis, price, priceDate, value, value is { } v ? R(v - basis) : null, r.OpenLots);
    }

    /// <summary>DRIP (INV-2a): the reinvest trade a dividend produces: fractional shares at the reinvestment price, basis = the dividend amount.</summary>
    public static Trade Reinvest(DividendPayment dividend, decimal reinvestPrice, DateOnly date)
        => new()
        {
            HoldingId = dividend.HoldingId, Date = date, Kind = TradeKind.Reinvest, Price = reinvestPrice,
            Shares = reinvestPrice <= 0 ? 0 : Math.Round(dividend.Amount / reinvestPrice, 6), Fees = 0, DividendPaymentId = dividend.Id,
            Notes = $"DRIP: {dividend.Amount:C} ÷ {reinvestPrice:N4}",
        };

    private sealed class MutableLot(int tradeId, DateOnly acquired, decimal shares, decimal costPerShare, decimal remaining, bool fromReinvest)
    {
        public int TradeId = tradeId; public DateOnly Acquired = acquired; public decimal Shares = shares; public decimal CostPerShare = costPerShare; public decimal Remaining = remaining; public bool FromReinvest = fromReinvest; public decimal DisallowedAdded;
    }

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    private static decimal R4(decimal d) => Math.Round(d, 4, MidpointRounding.AwayFromZero);
}

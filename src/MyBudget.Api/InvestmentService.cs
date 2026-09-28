using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Investments;
using MyBudget.Engines.Paycheck;

namespace MyBudget.Api;

/// <summary>Fetches prices and dividends, posts dividends from shares held on the ex-date, creates DRIP trades, and builds the portfolio view.</summary>
public sealed class InvestmentService(BudgetDbContext db, IMarketDataProvider market, PaycheckService paychecks)
{
    public async Task<MarketSyncResultDto> SyncAsync(int holdingId, CancellationToken ct = default)
    {
        var h = await db.Holdings.Include(x => x.Trades).Include(x => x.Dividends).FirstOrDefaultAsync(x => x.Id == holdingId, ct)
            ?? throw new KeyNotFoundException("Holding not found.");
        // Nothing to fetch for cash: it holds at $1.00 and the statement already priced it. Asking
        // anyway produced a 404 on every sync for tickers that are not tickers at all.
        if (h.IsCashEquivalent) return new MarketSyncResultDto(h.Ticker, 0, 0, 0, null, null);

        var firstTrade = h.Trades.Where(t => t.Kind != TradeKind.Sell).MinBy(t => t.Date)?.Date;
        var from = firstTrade ?? DateOnly.FromDateTime(DateTime.Today).AddYears(-1);
        MarketData data;
        try { data = await market.FetchAsync(h.Ticker, from, ct); }
        catch (Exception ex) { return new MarketSyncResultDto(h.Ticker, 0, 0, 0, null, ex.Message); }

        // Prices: keep one row per day.
        var have = await db.Prices.Where(p => p.Ticker == h.Ticker).Select(p => p.Date).ToHashSetAsync(ct);
        var pricesAdded = 0;
        foreach (var (date, close) in data.Prices)
        {
            if (have.Contains(date)) continue;
            db.Prices.Add(new PriceSnapshot { Ticker = h.Ticker, Date = date, Price = close, Source = DataSource.Fetched });
            have.Add(date); pricesAdded++;
        }
        if (data.LastPrice is { } lp)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var row = await db.Prices.FirstOrDefaultAsync(p => p.Ticker == h.Ticker && p.Date == today, ct);
            if (row is null && !have.Contains(today)) { db.Prices.Add(new PriceSnapshot { Ticker = h.Ticker, Date = today, Price = lp, Source = DataSource.Fetched }); pricesAdded++; }
            else if (row is not null && row.Source == DataSource.Fetched) row.Price = lp;
        }
        await db.SaveChangesAsync(ct);

        // Dividends: shares held the day before the ex-date × per-share.
        var dividendsAdded = 0;
        foreach (var (exDate, perShare) in data.Dividends)
        {
            if (firstTrade is null || exDate <= firstTrade) continue;
            if (h.Dividends.Any(d => d.ExDate == exDate)) continue;
            var shares = Portfolio.SharesHeldOn(h.Trades, exDate.AddDays(-1));
            if (shares <= 0) continue;
            h.Dividends.Add(new DividendPayment { ExDate = exDate, PerShare = perShare, SharesHeld = shares, Amount = Math.Round(shares * perShare, 2), Source = DataSource.Fetched });
            dividendsAdded++;
        }
        await db.SaveChangesAsync(ct);
        var reinvests = await ApplyDripAsync(h, ct);
        return new MarketSyncResultDto(h.Ticker, pricesAdded, dividendsAdded, reinvests, data.LastPrice, null);
    }

    /// <summary>INV-2a: for a DRIP holding, every dividend without a reinvest trade gets one at the close on its pay date (or ex-date).</summary>
    public async Task<int> ApplyDripAsync(Holding h, CancellationToken ct = default)
    {
        if (!h.Drip) return 0;
        var created = 0;
        foreach (var d in h.Dividends.Where(d => d.Amount > 0).OrderBy(d => d.ExDate))
        {
            if (h.Trades.Any(t => t.DividendPaymentId == d.Id)) { d.Reinvested = true; continue; }
            var date = d.PayDate ?? d.ExDate;
            var price = await db.Prices.Where(p => p.Ticker == h.Ticker && p.Date >= date).OrderBy(p => p.Date).FirstOrDefaultAsync(ct)
                        ?? await db.Prices.Where(p => p.Ticker == h.Ticker && p.Date <= date).OrderByDescending(p => p.Date).FirstOrDefaultAsync(ct);
            if (price is null) continue;
            var trade = Portfolio.Reinvest(d, price.Price, date);
            trade.HoldingId = h.Id;
            h.Trades.Add(trade);
            d.Reinvested = true;
            created++;
        }
        await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<PortfolioDto> PortfolioAsync(DateOnly asOf, int year, CancellationToken ct = default)
    {
        var holdings = await db.Holdings.Include(h => h.Account).Include(h => h.Trades).Include(h => h.Dividends).Where(h => h.IsActive).OrderBy(h => h.Ticker).ToListAsync(ct);
        var warnings = new List<string>();
        var positions = new List<PositionDto>();
        decimal st = 0, lt = 0;
        foreach (var h in holdings)
        {
            var r = Portfolio.Analyze(h.Trades, asOf);
            var recent = await db.Prices.Where(p => p.Ticker == h.Ticker && p.Date <= asOf).OrderByDescending(p => p.Date).Take(2).ToListAsync(ct);
            var price = recent.FirstOrDefault();
            var previous = recent.Skip(1).FirstOrDefault();
            var s = Portfolio.Summarize(r, price?.Price, price?.Date);
            if (price is null && s.Shares > 0) warnings.Add($"{h.Ticker}: no price yet; run Refresh or enter one.");
            var divYtd = h.Dividends.Where(d => d.ExDate.Year == year).Sum(d => d.Amount);
            var day = DayMove(s.Shares, price?.Price, previous?.Price);
            var est = EstimateDividends(h, s.Shares, s.MarketValue, asOf, year);
            var realizedYear = r.Realized.Where(g => g.SellDate.Year == year).ToList();
            var taxAdvantaged = h.Account is not null && AccountTypes.IsTaxAdvantaged(h.Account.Type);
            if (!taxAdvantaged)
            {
                st += realizedYear.Where(g => g.Term == GainTerm.Short).Sum(g => g.Gain);
                lt += realizedYear.Where(g => g.Term == GainTerm.Long).Sum(g => g.Gain);
            }
            positions.Add(new PositionDto(ToDto(h), taxAdvantaged, s.Shares, s.CostBasis, s.Price, s.PriceDate, s.MarketValue, s.UnrealizedGain, divYtd,
                r.OpenLots.Select(l => new LotDto(l.TradeId, l.Acquired, l.Shares, l.CostPerShare, l.RemainingShares, l.FromReinvest, l.DisallowedLossAdded)).ToList(),
                h.Trades.OrderByDescending(t => t.Date).Select(ToDto).ToList(),
                h.Dividends.OrderByDescending(d => d.ExDate).Select(ToDto).ToList(),
                r.Realized.Select(g => new RealizedGainDto(g.SellTradeId, g.SellDate, g.Acquired, g.Shares, g.Proceeds, g.CostBasis, g.Gain, g.Term.ToString(), g.DaysHeld, g.WashSale, g.DisallowedLoss, g.Formula)).ToList(),
                r.WashSales.Select(w => new WashSaleDto(w.SellTradeId, w.SellDate, w.Loss, w.WindowOpens, w.WindowCloses, w.EarliestSafeRepurchase, w.DisallowedLoss, w.WindowStillOpen, w.Message)).ToList(),
                previous?.Price, previous?.Date, day.Change, day.Percent,
                s.CostBasis == 0 || s.UnrealizedGain is not { } ug ? null : Round(ug / s.CostBasis * 100m),
                est.Amount, est.Yield, est.Formula));
        }

        GainsTaxDto? tax = null;
        if (st != 0 || lt != 0)
        {
            var (marginal, ordinary, moRate, t15, t20, note) = await OrdinaryContextAsync(year, asOf);
            if (note is not null) warnings.Add(note);
            var e = GainsTax.Estimate(new(st, lt, marginal, ordinary, t15, t20, moRate));
            tax = new GainsTaxDto(st, lt, e.ShortTermTax, e.LongTermTax, e.MissouriTax, e.Total, marginal, e.Steps);
        }
        return new PortfolioDto(asOf, year, positions, positions.Sum(p => p.MarketValue ?? 0), positions.Sum(p => p.CostBasis), positions.Sum(p => p.UnrealizedGain ?? 0),
            positions.Sum(p => p.DividendsThisYear), st, lt, tax, warnings);
    }

    /// <summary>Ordinary taxable income, marginal rate, MO rate and LTCG thresholds for the year, from the W-2 estimate and the tax tables.</summary>
    public async Task<(decimal Marginal, decimal OrdinaryTaxable, decimal MoRate, decimal T15, decimal T20, string? Note)> OrdinaryContextAsync(int year, DateOnly asOf)
    {
        var taxYear = await db.TaxYears.Include(t => t.Brackets).FirstOrDefaultAsync(t => t.Year == year) ?? await db.TaxYears.Include(t => t.Brackets).OrderByDescending(t => t.Year).FirstAsync();
        var federal = ReferenceSeed.ToFederalRules(taxYear);
        var mo = ReferenceSeed.ToMissouriRules(taxYear);
        var moRate = mo.Brackets.Max(b => b.Rate);
        decimal taxable = 0; var status = FederalFilingStatus.SingleOrMarriedFilingSeparately; string? note = null;
        var w2 = await db.IncomeSources.Include(s => s.Withholdings).Where(s => s.IsActive && s.Type == IncomeSourceType.W2Salary && s.PaySchedules.Count > 0).ToListAsync();
        foreach (var s in w2)
        {
            try
            {
                var yr = await paychecks.YearAsync(s.Id, year);
                taxable += yr.Gross - yr.PreTax;
                status = s.Withholdings.Where(w => w.EffectiveDate <= asOf).OrderByDescending(w => w.EffectiveDate).FirstOrDefault()?.FederalStatus ?? status;
            }
            catch (InvalidOperationException ex) { note = ex.Message; }
        }
        var stdDed = federal.StandardDeduction[status];
        var ordinary = Math.Max(0, taxable - stdDed);
        var marginal = SelfEmployment.MarginalRate(ordinary, federal.Brackets[status]);
        var (t15, t20) = status switch
        {
            FederalFilingStatus.MarriedFilingJointly => (taxYear.LtcgThreshold15MarriedJointly, taxYear.LtcgThreshold20MarriedJointly),
            FederalFilingStatus.HeadOfHousehold => (taxYear.LtcgThreshold15HeadOfHousehold, taxYear.LtcgThreshold20HeadOfHousehold),
            _ => (taxYear.LtcgThreshold15Single, taxYear.LtcgThreshold20Single),
        };
        return (marginal, ordinary, moRate, t15, t20, note);
    }

    /// <summary>Turns a tax-lot export into holdings and buy trades in one account; lots already present (same date, shares, cost) are left alone.</summary>
    /// <summary>
    /// Makes a cash position equal <paramref name="balance"/> by recording the difference, rather than
    /// adding another lot. A sweep is one pot that every statement restates: appending each statement
    /// would stack last month's cash on top of this month's. Booking the delta leaves the position at
    /// the stated figure and keeps the movement visible.
    /// </summary>
    /// <returns>True when something changed; false when it already matched.</returns>
    private static bool SetCashBalance(Holding h, decimal balance, DateOnly asOf, string source)
    {
        var held = MyBudget.Engines.Investments.Portfolio.SharesHeldOn(h.Trades, asOf);
        var delta = balance - held;
        if (delta == 0) return false;

        h.Trades.Add(new Trade
        {
            Date = asOf,
            Kind = delta > 0 ? TradeKind.Buy : TradeKind.Sell,
            Shares = Math.Abs(delta),
            Price = 1m,
            Notes = $"cash balance {balance:N2} {source}",
        });
        return true;
    }

    /// <summary>Sets a cash position by hand, for when the balance moved but no new export exists.</summary>
    public async Task<decimal> SetCashBalanceAsync(int holdingId, decimal balance, DateOnly asOf, CancellationToken ct = default)
    {
        var h = await db.Holdings.Include(x => x.Trades).FirstOrDefaultAsync(x => x.Id == holdingId, ct)
            ?? throw new KeyNotFoundException("Holding not found.");
        if (!h.IsCashEquivalent) throw new InvalidOperationException($"{h.Ticker} is a traded holding; record a buy or sell instead of setting a balance.");

        if (SetCashBalance(h, balance, asOf, "entered by hand")) await db.SaveChangesAsync(ct);
        return balance;
    }

    public async Task<LotImportResultDto> ImportLotsAsync(int accountId, string content, CancellationToken ct = default)
    {
        var account = await db.Accounts.FindAsync([accountId], ct) ?? throw new KeyNotFoundException("Account not found.");
        var parsed = MyBudget.Engines.Import.TaxLotParser.Parse(content);
        var holdings = await db.Holdings.Include(h => h.Trades).Where(h => h.AccountId == accountId).ToListAsync(ct);
        int created = 0, imported = 0, present = 0, prices = 0;
        foreach (var group in parsed.Lots.GroupBy(l => l.Ticker))
        {
            var h = holdings.FirstOrDefault(x => x.Ticker == group.Key);
            if (h is null)
            {
                h = new Holding { Ticker = group.Key, Name = group.First().Description, AccountId = accountId };
                db.Holdings.Add(h); holdings.Add(h); created++;
            }
            // Set on every import, not only on creation, so a holding first seen before this was
            // understood gets corrected the next time its statement comes through.
            if (group.Any(l => l.IsCashEquivalent)) h.IsCashEquivalent = true;

            if (h.IsCashEquivalent)
            {
                var stated = group.Sum(l => l.Quantity);
                var asOf = group.Max(l => l.PriceDate ?? l.Acquired);
                if (SetCashBalance(h, stated, asOf, $"from statement {asOf:yyyy-MM-dd}")) imported++;
                else present++;
            }
            else
            {
                foreach (var lot in group)
                {
                    if (h.Trades.Any(t => t.Kind != TradeKind.Sell && t.Date == lot.Acquired && t.Shares == lot.Quantity && t.Price == lot.UnitCost)) { present++; continue; }
                    h.Trades.Add(new Trade { Date = lot.Acquired, Kind = TradeKind.Buy, Shares = lot.Quantity, Price = lot.UnitCost, Notes = "from tax-lot import" });
                    imported++;
                }
            }
            var priced = group.FirstOrDefault(l => l.Price is not null);
            if (priced?.Price is { } px)
            {
                var date = priced.PriceDate ?? DateOnly.FromDateTime(DateTime.Today);
                if (!await db.Prices.AnyAsync(p => p.Ticker == group.Key && p.Date == date, ct))
                {
                    db.Prices.Add(new PriceSnapshot { Ticker = group.Key, Date = date, Price = px, Source = DataSource.Manual }); prices++;
                }
            }
        }
        await db.SaveChangesAsync(ct);

        // A tax-lot export carries no prices beyond the one on the row and no dividend history at all,
        // so fetch both now rather than leaving the screen to be filled by a second click. Best effort:
        // a ticker the provider doesn't know shouldn't fail an import that already succeeded.
        int pricesFetched = 0, dividendsFetched = 0;
        var fetchErrors = new List<string>();
        foreach (var h in holdings.Where(x => parsed.Lots.Any(l => l.Ticker == x.Ticker)))
        {
            try
            {
                var r = await SyncAsync(h.Id, ct);
                if (r.Error is { } err) fetchErrors.Add($"{h.Ticker}: {err}");
                else { pricesFetched += r.PricesAdded; dividendsFetched += r.DividendsAdded; }
            }
            catch (Exception ex) { fetchErrors.Add($"{h.Ticker}: {ex.Message}"); }
        }

        return new LotImportResultDto(account.Name, created, imported, present, prices,
            parsed.Lots.Select(l => l.Ticker).Distinct().OrderBy(t => t).ToList(), parsed.Skipped, parsed.Warnings,
            pricesFetched, dividendsFetched, fetchErrors);
    }

    public static HoldingDto ToDto(Holding h) => new() { Id = h.Id, Ticker = h.Ticker, Name = h.Name, AccountId = h.AccountId, AccountName = h.Account?.Name, Drip = h.Drip, IsCashEquivalent = h.IsCashEquivalent, IsActive = h.IsActive, Notes = h.Notes };
    public static TradeDto ToDto(Trade t) => new() { Id = t.Id, HoldingId = t.HoldingId, Date = t.Date, Kind = t.Kind, Shares = t.Shares, Price = t.Price, Fees = t.Fees, Notes = t.Notes, DividendPaymentId = t.DividendPaymentId };
    public static DividendDto ToDto(DividendPayment d) => new() { Id = d.Id, HoldingId = d.HoldingId, ExDate = d.ExDate, PayDate = d.PayDate, PerShare = d.PerShare, SharesHeld = d.SharesHeld, Amount = d.Amount, Reinvested = d.Reinvested, Source = d.Source };

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The move between the two most recent closes, in dollars against the shares held and as a percent.
    /// Null until a ticker has two prices; a single imported price has nothing to compare against.
    /// </summary>
    private static (decimal? Change, decimal? Percent) DayMove(decimal shares, decimal? price, decimal? previous)
    {
        if (price is not { } now || previous is not { } then || then == 0 || shares <= 0) return (null, null);
        return (Round((now - then) * shares), Round((now - then) / then * 100m));
    }


    /// <summary>
    /// The calendar year's dividends: what has already been paid, plus the payments still to come at
    /// the latest per-share rate against the shares held now. The cadence is read from the gaps between
    /// recorded ex-dates rather than assumed, and a finished year is simply its actual total.
    ///
    /// Using the latest per-share rate and today's share count (rather than annualizing the year so far)
    /// matters when the position grew during the year: the early payments were on fewer shares and would
    /// drag the projection down.
    /// </summary>
    public static (decimal? Amount, decimal? Yield, string? Formula) EstimateDividends(Holding h, decimal shares, decimal? marketValue, DateOnly asOf, int year)
    {
        decimal? Yield(decimal amount) => marketValue is > 0 ? Round(amount / marketValue.Value * 100m) : null;

        var paid = h.Dividends.Where(d => d.ExDate.Year == year).OrderBy(d => d.ExDate).ToList();
        var paidTotal = Round(paid.Sum(d => d.Amount));

        if (year < asOf.Year)
            return paid.Count == 0 ? (null, null, $"Nothing paid in {year}.")
                                   : (paidTotal, Yield(paidTotal), $"{year} is complete: {paid.Count} payment{(paid.Count == 1 ? "" : "s")} totalling {paidTotal:C}.");

        if (shares <= 0) return (null, null, null);
        if (paid.Count == 0) return (null, null, $"No dividends recorded in {year} yet.");

        var months = PaymentIntervalMonths(h.Dividends.OrderBy(d => d.ExDate).Select(d => d.ExDate).ToList());
        if (months is not { } every)
            return (paidTotal, Yield(paidTotal), $"{paid.Count} payment{(paid.Count == 1 ? "" : "s")} totalling {paidTotal:C}; not enough history to tell how often it pays, so nothing is projected.");

        var last = paid[^1];
        var perPayment = Round(last.PerShare * shares);

        var next = last.ExDate.AddMonths(every);
        var remaining = 0;
        while (next.Year == year) { remaining++; next = next.AddMonths(every); }

        var amount = Round(paidTotal + remaining * perPayment);
        var cadence = every switch { 1 => "monthly", 3 => "quarterly", 6 => "twice a year", 12 => "yearly", _ => $"every {every} months" };
        var formula = remaining == 0
            ? $"{paid.Count} paid in {year} totalling {paidTotal:C}; none left this year ({cadence})."
            : $"{paid.Count} paid in {year} totalling {paidTotal:C}, plus {remaining} more {cadence} at {last.PerShare:N4}/share × {shares:0.####} shares ({perPayment:C} each) = {amount:C}";
        return (amount, Yield(amount), formula);
    }

    /// <summary>
    /// How many months apart the payments fall, from the median gap between ex-dates. Returns null when
    /// there are fewer than two payments or the spacing doesn't match a normal schedule.
    /// </summary>
    public static int? PaymentIntervalMonths(IReadOnlyList<DateOnly> exDates)
    {
        if (exDates.Count < 2) return null;
        var gaps = new List<int>();
        for (var i = 1; i < exDates.Count; i++) gaps.Add(exDates[i].DayNumber - exDates[i - 1].DayNumber);
        gaps.Sort();
        var median = gaps[gaps.Count / 2];
        return median switch
        {
            >= 24 and <= 38 => 1,
            >= 80 and <= 100 => 3,
            >= 165 and <= 195 => 6,
            >= 350 and <= 380 => 12,
            _ => null,
        };
    }
}

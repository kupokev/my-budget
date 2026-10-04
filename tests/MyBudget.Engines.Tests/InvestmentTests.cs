using MyBudget.Domain;
using MyBudget.Engines.Investments;
using MyBudget.Engines.Paycheck;

namespace MyBudget.Engines.Tests;

public class InvestmentTests
{
    private static Trade T(int id, string date, TradeKind kind, decimal shares, decimal price, decimal fees = 0)
        => new() { Id = id, HoldingId = 1, Date = DateOnly.Parse(date), Kind = kind, Shares = shares, Price = price, Fees = fees };

    [Fact]
    public void History_values_open_lots_at_the_close_on_or_before_each_date()
    {
        var trades = new[] { T(1, "2026-01-05", TradeKind.Buy, 10, 100m), T(2, "2026-02-02", TradeKind.Sell, 4, 110m) };
        var prices = new List<(DateOnly, decimal)> { (new(2026, 1, 5), 100m), (new(2026, 1, 30), 108m), (new(2026, 2, 27), 120m) };
        var h = new[] { new HoldingHistory("TST", trades, prices) };

        Assert.Equal(new PortfolioPoint(new(2026, 1, 1), 0m, 0m, 0m), PortfolioHistory.ValueOn(h, new(2026, 1, 1)));       // before the first buy
        Assert.Equal(new PortfolioPoint(new(2026, 2, 1), 1_080m, 1_000m, 1_000m), PortfolioHistory.ValueOn(h, new(2026, 2, 1))); // 10 × Friday's 108 close
        Assert.Equal(new PortfolioPoint(new(2026, 3, 2), 720m, 600m, 1_000m), PortfolioHistory.ValueOn(h, new(2026, 3, 2)));  // 6 left × 120; selling took nothing out
    }

    [Fact]
    public void History_samples_weekdays_thinned_to_the_limit_and_ends_on_the_day_asked()
    {
        var month = PortfolioHistory.SampleDates(new(2026, 9, 1), new(2026, 10, 3)); // ends on a Saturday
        Assert.DoesNotContain(month.SkipLast(1), d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(new DateOnly(2026, 10, 3), month[^1]);

        var threeYears = PortfolioHistory.SampleDates(new(2023, 10, 1), new(2026, 10, 1));
        Assert.Equal(90, threeYears.Count);
        Assert.Equal(new DateOnly(2023, 10, 2), threeYears[0]);
        Assert.Equal(new DateOnly(2026, 10, 1), threeYears[^1]);
    }

    [Fact]
    public void Fifo_lots_classify_short_and_long_term_and_track_remaining_shares()
    {
        var trades = new[]
        {
            T(1, "2024-03-01", TradeKind.Buy, 10, 100m, 1m),     // lot A: 1001 / 10 = 100.10 per share
            T(2, "2025-06-01", TradeKind.Buy, 10, 120m),          // lot B
            T(3, "2025-09-15", TradeKind.Sell, 15, 150m, 1m),     // 10 from A (long), 5 from B (short)
        };
        var r = Portfolio.Analyze(trades, new(2026, 9, 26));
        Assert.Equal(2, r.Realized.Count);
        var a = r.Realized[0]; var b = r.Realized[1];
        Assert.Equal(GainTerm.Long, a.Term);
        Assert.Equal(10m, a.Shares);
        Assert.Equal(1_001m, a.CostBasis);
        Assert.Equal(Math.Round(10 * (15 * 150m - 1m) / 15, 2), a.Proceeds);
        Assert.Equal(GainTerm.Short, b.Term);
        Assert.Equal(5m, b.Shares);
        Assert.Equal(600m, b.CostBasis);
        Assert.Equal(5m, Portfolio.SharesHeldOn(trades, new(2025, 12, 31)));
        var open = Assert.Single(r.OpenLots);
        Assert.Equal(5m, open.RemainingShares);
        Assert.Empty(r.WashSales);
    }

    [Fact]
    public void Exactly_one_year_is_still_short_term()
    {
        Assert.Equal(GainTerm.Short, Portfolio.TermFor(new(2025, 3, 1), new(2026, 3, 1)));
        Assert.Equal(GainTerm.Long, Portfolio.TermFor(new(2025, 3, 1), new(2026, 3, 2)));
    }

    [Fact]
    public void Wash_sale_disallows_the_loss_and_adds_it_to_the_replacement_lot()
    {
        var trades = new[]
        {
            T(1, "2026-01-10", TradeKind.Buy, 100, 50m),
            T(2, "2026-06-01", TradeKind.Sell, 100, 40m),          // $1,000 loss
            T(3, "2026-06-20", TradeKind.Buy, 60, 41m),            // 60 replacement shares within 30 days
        };
        var r = Portfolio.Analyze(trades, new(2026, 9, 26));
        var w = Assert.Single(r.WashSales);
        Assert.Equal(1_000m, w.Loss);
        Assert.Equal(600m, w.DisallowedLoss);                      // 60/100 of the loss
        Assert.Equal(new DateOnly(2026, 7, 2), w.EarliestSafeRepurchase);
        Assert.Contains(3, w.ReplacementTradeIds);
        var gain = Assert.Single(r.Realized);
        Assert.True(gain.WashSale);
        Assert.Equal(-400m, gain.Gain);                            // only 400 of the loss is recognised
        var lot = Assert.Single(r.OpenLots);
        Assert.Equal(51m, lot.CostPerShare);                       // 41 + 600/60
        Assert.Equal(600m, lot.DisallowedLossAdded);
    }

    [Fact]
    public void Contributions_count_only_what_sales_and_dividends_in_the_account_could_not_pay_for()
    {
        var ira = new HoldingHistory("AAA", new[]
        {
            T(1, "2026-01-05", TradeKind.Buy, 10, 100m, 1m),       // 1,001: all new money
            T(2, "2026-01-20", TradeKind.Reinvest, 0.5m, 110m),    // the holding paying itself
            T(3, "2026-02-02", TradeKind.Sell, 4, 110m, 1m),       // 439 into the account's cash
            new Trade { Id = 4, HoldingId = 1, Date = new(2026, 3, 1), Kind = TradeKind.Buy, Shares = 0.2m, Price = 112m, Notes = $"{TradeNotes.StatementAdjustment}: 6.7 shares held on 2026-03-01" },
        }, [], AccountId: 1, Dividends: [(new(2026, 3, 15), 61m)]);
        var other = new HoldingHistory("BBB", new[] { T(5, "2026-04-01", TradeKind.Buy, 6, 100m) }, [], AccountId: 1); // 600: 500 from cash, 100 new
        var elsewhere = new HoldingHistory("CCC", new[] { T(6, "2026-04-01", TradeKind.Buy, 1, 50m) }, [], AccountId: 2);  // another account's cash can't pay for it

        var all = new[] { ira, other, elsewhere };
        Assert.Equal(1_001m, Contributions.On(all, [], new(2026, 3, 31)));
        Assert.Equal(1_151m, Contributions.On(all, [], new(2026, 4, 1)));
    }

    [Fact]
    public void Drawing_down_a_cash_sweep_to_buy_is_not_new_money()
    {
        var sweep = new HoldingHistory("SWEEP", new[]
        {
            T(1, "2026-09-25", TradeKind.Buy, 11_000m, 1m),        // statement balance
            T(2, "2026-09-28", TradeKind.Sell, 5_500m, 1m),        // balance entered lower after buying
        }, [], AccountId: 1);
        var stock = new HoldingHistory("JEPI", new[] { T(3, "2026-09-28", TradeKind.Buy, 100, 55m) }, [], AccountId: 1);
        Assert.Equal(11_000m, Contributions.On([sweep, stock], [], new(2026, 9, 30)));
    }

    [Fact]
    public void Recorded_contributions_replace_the_estimate_from_their_first_date()
    {
        var ira = new HoldingHistory("AAA", new[]
        {
            T(1, "2025-03-01", TradeKind.Buy, 10, 100m),          // estimated: 1,000 new money
            T(2, "2026-01-10", TradeKind.Buy, 70, 100m),          // covered by the recorded deposit, not counted again
            T(3, "2026-09-25", TradeKind.Buy, 5_000m, 1m),         // a sweep balance entered late: not new money either
        }, [], AccountId: 1);
        var recorded = new[] { new RecordedContribution(1, new(2026, 1, 5), 7_000m), new RecordedContribution(1, new(2026, 3, 1), -250m) };

        Assert.Equal(1_000m, Contributions.On([ira], recorded, new(2025, 12, 31)));
        Assert.Equal(8_000m, Contributions.On([ira], recorded, new(2026, 2, 1)));
        var a = Assert.Single(Contributions.For([ira], recorded, new(2026, 9, 30)));
        Assert.Equal((1_000m, 6_750m, 7_750m), (a.Estimated, a.Recorded, a.Total));
        Assert.Contains("before Jan 5, 2026", a.Formula);
    }

    [Fact]
    public void A_statement_period_with_no_deposits_means_none_were_made()
    {
        var roth = new HoldingHistory("PHK", new[]
        {
            T(1, "2025-06-01", TradeKind.Buy, 100, 5m),            // before the statement: estimated, 500
            T(2, "2025-11-03", TradeKind.Buy, 200, 5m),            // inside it, with no deposit: not new money
        }, [], AccountId: 1);
        var recorded = new[] { new RecordedContribution(1, new(2026, 2, 18), 1_400m) };
        var cover = new Dictionary<int, DateOnly> { [1] = new(2025, 10, 1) };

        Assert.Equal(500m, Contributions.On([roth], recorded, new(2025, 12, 31), cover));
        Assert.Equal(1_900m, Contributions.On([roth], recorded, new(2026, 3, 1), cover));
        Assert.Equal(1_500m, Contributions.On([roth], recorded, new(2025, 12, 31)));   // without the cover date, Nov's buy looks new
    }

    [Fact]
    public void Wash_sale_does_not_count_shares_the_same_sale_disposed_of_as_replacements()
    {
        var trades = new[]
        {
            T(1, "2026-01-10", TradeKind.Buy, 100, 50m),
            T(2, "2026-05-20", TradeKind.Buy, 50, 48m),            // inside the window, but sold below
            T(3, "2026-06-01", TradeKind.Sell, 150, 40m),          // $1,400 loss across both lots
        };
        var r = Portfolio.Analyze(trades, new(2026, 9, 26));
        var w = Assert.Single(r.WashSales);
        Assert.Equal(0m, w.DisallowedLoss);
        Assert.Empty(w.ReplacementTradeIds);
        Assert.Equal(-1_400m, r.Realized.Sum(g => g.Gain));
    }

    [Fact]
    public void Selling_a_replacement_lot_uses_the_basis_the_wash_sale_added()
    {
        var trades = new[]
        {
            T(1, "2026-01-10", TradeKind.Buy, 100, 50m),
            T(2, "2026-06-01", TradeKind.Sell, 100, 40m),          // $1,000 loss, all disallowed
            T(3, "2026-06-20", TradeKind.Buy, 100, 41m),           // basis 41 + 10 = 51
            T(4, "2026-09-01", TradeKind.Sell, 100, 45m),
        };
        var r = Portfolio.Analyze(trades, new(2026, 9, 26));
        var later = Assert.Single(r.Realized, g => g.SellTradeId == 4);
        Assert.Equal(5_100m, later.CostBasis);
        Assert.Equal(-600m, later.Gain);                           // the deferred loss comes back here
        Assert.Empty(r.OpenLots);
    }

    [Fact]
    public void Wash_sale_basis_goes_on_the_replacement_shares_only()
    {
        var trades = new[]
        {
            T(1, "2026-01-10", TradeKind.Buy, 100, 50m),
            T(2, "2026-06-01", TradeKind.Sell, 100, 40m),          // $1,000 loss
            T(3, "2026-06-20", TradeKind.Buy, 200, 41m),           // 100 of these replace the sold shares
        };
        var r = Portfolio.Analyze(trades, new(2026, 9, 26));
        Assert.Equal(1_000m, Assert.Single(r.WashSales).DisallowedLoss);
        Assert.Equal(2, r.OpenLots.Count);
        var replacement = Assert.Single(r.OpenLots, l => l.DisallowedLossAdded > 0);
        Assert.Equal(100m, replacement.RemainingShares);
        Assert.Equal(51m, replacement.CostPerShare);
        var rest = Assert.Single(r.OpenLots, l => l.DisallowedLossAdded == 0);
        Assert.Equal(100m, rest.RemainingShares);
        Assert.Equal(41m, rest.CostPerShare);
    }

    [Fact]
    public void Open_wash_sale_window_is_reported_when_no_repurchase_yet()
    {
        var trades = new[] { T(1, "2026-01-10", TradeKind.Buy, 10, 50m), T(2, "2026-09-10", TradeKind.Sell, 10, 45m) };
        var r = Portfolio.Analyze(trades, new(2026, 9, 26));
        var w = Assert.Single(r.WashSales);
        Assert.True(w.WindowStillOpen);
        Assert.Equal(0m, w.DisallowedLoss);
        Assert.Equal(new DateOnly(2026, 10, 11), w.EarliestSafeRepurchase);
        Assert.Contains("DRIP", w.Message);
    }

    [Fact]
    public void Drip_reinvest_buys_fractional_shares_with_basis_equal_to_the_dividend()
    {
        var div = new DividendPayment { Id = 7, HoldingId = 1, ExDate = new(2026, 9, 1), PerShare = 0.5m, SharesHeld = 42.5m, Amount = 21.25m };
        var t = Portfolio.Reinvest(div, 85.00m, new(2026, 9, 15));
        Assert.Equal(TradeKind.Reinvest, t.Kind);
        Assert.Equal(0.25m, t.Shares);
        Assert.Equal(7, t.DividendPaymentId);
        var r = Portfolio.Analyze([T(1, "2026-01-01", TradeKind.Buy, 42.5m, 80m), t], new(2026, 9, 26));
        Assert.Equal(42.75m, r.OpenLots.Sum(l => l.RemainingShares));
        Assert.Equal(21.25m, r.OpenLots.Single(l => l.FromReinvest).RemainingShares * r.OpenLots.Single(l => l.FromReinvest).CostPerShare);
    }

    [Fact]
    public void Gains_tax_stacks_long_term_on_ordinary_income()
    {
        var e = GainsTax.Estimate(new(ShortTermGain: 1_000m, LongTermGain: 10_000m, OrdinaryMarginalRate: 0.24m, OrdinaryTaxableIncome: 45_000m, Ltcg15Threshold: 49_450m, Ltcg20Threshold: 545_500m, MissouriRate: 0.047m));
        Assert.Equal(240m, e.ShortTermTax);
        // stack = 46,000; 3,450 at 0%, 6,550 at 15% = 982.50
        Assert.Equal(982.50m, e.LongTermTax);
        Assert.Equal(Math.Round(11_000m * 0.047m, 2), e.MissouriTax);
        Assert.Equal(e.ShortTermTax + e.LongTermTax + e.MissouriTax, e.Total);
    }

    [Fact]
    public void Self_employment_set_aside_uses_the_wage_base_left_after_w2_wages()
    {
        var f = RuleSets.Federal(2026);
        var e = SelfEmployment.Estimate(new(2026, NetProfit: 20_000m, W2SocialSecurityWages: 170_000m, W2MedicareWages: 170_000m, FederalMarginalRate: 0.24m, MissouriRate: 0.047m, f, AsOf: new(2026, 9, 26)));
        Assert.Equal(18_470m, e.SeEarnings);
        Assert.Equal(Math.Round(Math.Min(18_470m, 184_500m - 170_000m) * 0.124m, 2), e.SocialSecurityTax); // only 14,500 of room
        Assert.Equal(Math.Round(18_470m * 0.029m, 2), e.MedicareTax);
        Assert.True(e.SetAsideFraction is > 0.30m and < 0.45m);
        Assert.Equal(4, e.Schedule.Count);
        Assert.Equal(3, e.Schedule.Count(q => q.Past));                        // Apr 15, Jun 15 and Sep 15 are behind Sep 26
        Assert.Equal(e.FederalRemaining, e.Schedule.Single(q => !q.Past).Federal); // everything left lands on Jan 15
        Assert.Equal(0.24m, SelfEmployment.MarginalRate(150_000m, f.Brackets[FederalFilingStatus.SingleOrMarriedFilingSeparately]));
    }
}

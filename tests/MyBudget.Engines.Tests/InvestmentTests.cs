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

        Assert.Equal(new PortfolioPoint(new(2026, 1, 1), 0m, 0m), PortfolioHistory.ValueOn(h, new(2026, 1, 1)));       // before the first buy
        Assert.Equal(new PortfolioPoint(new(2026, 2, 1), 1_080m, 1_000m), PortfolioHistory.ValueOn(h, new(2026, 2, 1))); // 10 × Friday's 108 close
        Assert.Equal(new PortfolioPoint(new(2026, 3, 2), 720m, 600m), PortfolioHistory.ValueOn(h, new(2026, 3, 2)));    // 6 left × 120
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

using MyBudget.Domain;
using MyBudget.Engines.Rewards;

namespace MyBudget.Engines.Tests;

/// <summary>The doc's tension made concrete: IHG Diamond and Hilton Diamond both want $40K of card spend a year.</summary>
public class RewardsOptimizerTests
{
    private static readonly Category Restaurants = new() { Id = 1, Name = "Restaurants", PlannedMonthly = 600m };
    private static readonly Category Groceries = new() { Id = 2, Name = "Groceries", PlannedMonthly = 700m };
    private static readonly Category Gas = new() { Id = 3, Name = "Gas", PlannedMonthly = 250m };
    private static readonly Category Other = new() { Id = 4, Name = "Other", PlannedMonthly = 800m };
    private static readonly Category Utilities = new() { Id = 5, Name = "Utilities" };
    private static readonly Category Housing = new() { Id = 6, Name = "Housing", IsCardEligible = false };

    private static (Card ihg, Card surpass, Card sapphire, LoyaltyProgram ihgProg, LoyaltyProgram hiltonProg) Fixture()
    {
        var ihgProg = new LoyaltyProgram { Id = 1, Name = "IHG One Rewards", PointValueCents = 0.5m, Priority = 1, TargetTier = "Diamond", CurrentTier = "Platinum", PointsBalance = 85_000m,
            Tiers = [new() { Name = "Platinum", Rank = 3 }, new() { Name = "Diamond", Rank = 4 }] };
        var hiltonProg = new LoyaltyProgram { Id = 2, Name = "Hilton Honors", PointValueCents = 0.5m, Priority = 2, TargetTier = "Diamond", CurrentTier = "Gold",
            Tiers = [new() { Name = "Gold", Rank = 2 }, new() { Name = "Diamond", Rank = 3 }] };
        var ihg = new Card { Id = 10, Name = "IHG Premier", AnnualFee = 99m, LoyaltyProgram = ihgProg, LoyaltyProgramId = 1,
            EarnRules = [new() { CategoryId = 3, PointsPerDollar = 5 }, new() { CategoryId = 1, PointsPerDollar = 5 }, new() { PointsPerDollar = 3 }],
            Thresholds = [new() { Id = 1, Amount = 20_000m, RewardKind = ThresholdRewardKind.Credit, Description = "$100 + 10K pts", ValueDollars = 150m },
                          new() { Id = 2, Amount = 40_000m, RewardKind = ThresholdRewardKind.Status, Description = "Diamond", TierName = "Diamond" }] };
        var surpass = new Card { Id = 11, Name = "Hilton Surpass", AnnualFee = 150m, LoyaltyProgram = hiltonProg, LoyaltyProgramId = 2,
            EarnRules = [new() { CategoryId = 1, PointsPerDollar = 6 }, new() { CategoryId = 2, PointsPerDollar = 6 }, new() { CategoryId = 3, PointsPerDollar = 6 }, new() { PointsPerDollar = 3 }],
            Thresholds = [new() { Id = 3, Amount = 15_000m, RewardKind = ThresholdRewardKind.FreeNight, Description = "Free night", ValueDollars = 250m },
                          new() { Id = 4, Amount = 40_000m, RewardKind = ThresholdRewardKind.Status, Description = "Diamond", TierName = "Diamond" }] };
        var sapphire = new Card { Id = 12, Name = "Sapphire Preferred", AnnualFee = 95m, PointValueCents = 1.25m,
            EarnRules = [new() { CategoryId = 1, PointsPerDollar = 3 }, new() { PointsPerDollar = 1 }] };
        ihgProg.Paths = [new() { Id = 1, TierName = "Platinum", Kind = StatusPathKind.HoldCard, CardId = 10, Card = ihg },
                         new() { Id = 2, TierName = "Diamond", Kind = StatusPathKind.CardSpend, Threshold = 40_000m, CardId = 10, Card = ihg },
                         new() { Id = 3, TierName = "Diamond", Kind = StatusPathKind.Nights, Threshold = 70 }];
        hiltonProg.Paths = [new() { Id = 4, TierName = "Gold", Kind = StatusPathKind.HoldCard, CardId = 11, Card = surpass },
                            new() { Id = 5, TierName = "Diamond", Kind = StatusPathKind.CardSpend, Threshold = 40_000m, CardId = 11, Card = surpass },
                            new() { Id = 6, TierName = "Diamond", Kind = StatusPathKind.Nights, Threshold = 50 },
                            new() { Id = 7, TierName = "Diamond", Kind = StatusPathKind.Stays, Threshold = 25 },
                            new() { Id = 8, TierName = "Diamond", Kind = StatusPathKind.ProgramSpend, Threshold = 11_500m }];
        ihgProg.Progress = [new() { Year = 2026, Nights = 12 }];
        hiltonProg.Progress = [new() { Year = 2026, Nights = 18, Stays = 9, ProgramSpend = 4_200m }];
        return (ihg, surpass, sapphire, ihgProg, hiltonProg);
    }

    private static List<CardSpend> Spend(Card ihg, Card surpass, int months)
        => Enumerable.Range(1, months).SelectMany(m => new[]
        {
            new CardSpend { CardId = ihg.Id, Period = new(2026, m, 1), CategoryId = 2, Amount = 650m },
            new CardSpend { CardId = ihg.Id, Period = new(2026, m, 1), CategoryId = 3, Amount = 240m },
            new CardSpend { CardId = ihg.Id, Period = new(2026, m, 1), CategoryId = 4, Amount = 500m },
            new CardSpend { CardId = surpass.Id, Period = new(2026, m, 1), CategoryId = 1, Amount = 550m },
            new CardSpend { CardId = surpass.Id, Period = new(2026, m, 1), CategoryId = 4, Amount = 300m },
        }).ToList();

    private static RewardsInput Input(DateOnly asOf, List<Card> cards, List<LoyaltyProgram> programs, List<CardSpend> spend, List<Bill>? bills = null, Dictionary<int, decimal>? accrual = null, List<Label>? labels = null)
        => new(2026, asOf, cards, programs, spend, [Restaurants, Groceries, Gas, Other, Utilities, Housing], labels ?? [], bills ?? [], accrual ?? new());

    [Fact]
    public void Both_diamonds_cannot_be_hit_on_the_projected_spend_and_the_report_says_so_in_priority_order()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        var r = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hiltonProg], Spend(ihg, surpass, 9)));

        Assert.Equal(4, r.Plan.MonthsLeft);                     // Sep–Dec
        Assert.Equal(2_350m, r.Plan.ProjectedMonthly);          // 600 + 700 + 250 + 800

        // IHG: 9 × 1,390 = 12,510 YTD → (40,000 − 12,510) / 4 = 6,872.50 needed; only 2,350 available.
        var ihgStatus = r.Programs.Single(p => p.Name == "IHG One Rewards");
        Assert.Equal("Platinum", ihgStatus.HeldTier);
        Assert.False(ihgStatus.TargetReached);
        Assert.Equal(6_872.50m, ihgStatus.PlannedPath!.RequiredMonthly);
        var ihgAlloc = Assert.Single(r.Plan.Allocations);
        Assert.Equal(ihg.Id, ihgAlloc.CardId);
        Assert.Equal(2_350m, ihgAlloc.Monthly);

        Assert.Equal(2, r.Plan.Gaps.Count);
        var ihgGap = r.Plan.Gaps[0];
        Assert.Equal(4_522.50m, ihgGap.ShortfallMonthly);
        Assert.Contains(ihgGap.Alternatives, a => a == "58 more nights");
        var hiltonGap = r.Plan.Gaps[1];
        Assert.Equal(0m, hiltonGap.AllocatedMonthly);           // nothing left after IHG
        Assert.Equal(Math.Round((40_000m - 7_650m) / 4, 2), hiltonGap.RequiredMonthly);
        Assert.Contains(hiltonGap.Alternatives, a => a == "32 more nights");
        Assert.Contains(hiltonGap.Alternatives, a => a == "16 more stays");
        Assert.Contains(hiltonGap.Alternatives, a => a == "$7,300.00 more eligible program spend");

        // Thresholds: Surpass free night at 15K is 7,350 away → 1,837.50/mo.
        var freeNight = r.Thresholds.Single(t => t.ThresholdId == 3);
        Assert.Equal(7_650m, freeNight.YtdSpend);
        Assert.Equal(1_837.50m, freeNight.RequiredMonthly);
        Assert.False(freeNight.OnPace);                          // 7,650 ÷ 8 elapsed months × 4 left → 11,475
        Assert.Contains("÷ 4 months left", freeNight.Formula);
    }

    [Fact]
    public void With_enough_spend_both_goals_are_covered_and_the_rest_goes_to_the_best_value_card()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        var big = new Category { Id = 4, Name = "Other", PlannedMonthly = 20_000m };
        var input = Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hiltonProg], Spend(ihg, surpass, 9)) with { Categories = [Restaurants, Groceries, Gas, big, Utilities, Housing] };
        var r = RewardsOptimizer.Run(input);

        Assert.Empty(r.Plan.Gaps);
        Assert.Equal(2, r.Plan.Allocations.Count);
        Assert.Equal(6_872.50m, r.Plan.Allocations[0].Monthly);
        Assert.Equal(8_087.50m, r.Plan.Allocations[1].Monthly);
        Assert.All(r.Programs, p => Assert.Contains("on plan", p.HowReached));

        // Restaurants: Sapphire 3× at 1.25¢ = 3.75¢/$ beats Surpass 6× at 0.5¢ = 3¢/$ — but goal cards take the categories they earn most on first,
        // so whatever is left over is routed by value.
        var leftover = r.Plan.Routing.Where(x => x.Reason.StartsWith("best value")).ToList();
        Assert.NotEmpty(leftover);
        Assert.Equal(21_550m, r.Plan.Routing.Sum(x => x.Monthly));
    }

    [Fact]
    public void Holding_the_Aspire_grants_Hilton_Diamond_without_spend()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        var aspire = new Card { Id = 13, Name = "Hilton Aspire", AnnualFee = 550m, LoyaltyProgram = hiltonProg, LoyaltyProgramId = 2, EarnRules = [new() { PointsPerDollar = 3 }] };
        hiltonProg.Paths.Add(new() { Id = 9, TierName = "Diamond", Kind = StatusPathKind.HoldCard, CardId = 13, Card = aspire });
        var r = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire, aspire], [ihgProg, hiltonProg], Spend(ihg, surpass, 9)));

        var hilton = r.Programs.Single(p => p.Name == "Hilton Honors");
        Assert.True(hilton.TargetReached);
        Assert.Equal("Diamond", hilton.HeldTier);
        Assert.Single(r.Plan.Gaps); // only IHG is short now
        Assert.Equal(-550m + r.Earnings.Single(e => e.CardId == 13).YtdDollars, r.Earnings.Single(e => e.CardId == 13).NetValue);
    }

    [Fact]
    public void Bill_recommendation_weighs_the_bank_discount_unless_a_goal_card_is_short()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        var att = new Bill { Id = 1, Name = "AT&T", CategoryId = 5, Category = Utilities, ProjectedAmount = 85m, BankAutopayDiscount = 5m, FundingAccountId = 1 };
        var hulu = new Bill { Id = 2, Name = "Hulu", CategoryId = 5, Category = Utilities, ProjectedAmount = 18.99m, FundingAccountId = 1 };
        var mortgage = new Bill { Id = 3, Name = "Mortgage", CategoryId = 6, Category = Housing, ProjectedAmount = 2_100m, FundingAccountId = 1, IsCardEligible = false };
        var accrual = new Dictionary<int, decimal> { [1] = 85m, [2] = 18.99m, [3] = 2_100m };

        // No goals short: only Sapphire (no goal) → AT&T $85 × 1 × 1.25¢ = $1.06 < $5 discount → bank.
        var calm = RewardsOptimizer.Run(Input(new(2026, 9, 26), [sapphire], [], [], [att, hulu, mortgage], accrual));
        var attRec = calm.Bills.Single(b => b.BillName == "AT&T");
        Assert.Null(attRec.CardId);
        Assert.StartsWith("Pay from bank", attRec.Recommendation);
        Assert.Equal(1.06m, attRec.CardValue);
        Assert.Contains(calm.Bills, b => b.BillName == "Hulu" && b.CardId == sapphire.Id);
        Assert.DoesNotContain(calm.Bills, b => b.BillName == "Mortgage");

        // IHG short: utilities routed to the IHG card → AT&T goes on the card despite the discount.
        var tight = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hiltonProg], Spend(ihg, surpass, 9), [att, hulu, mortgage], accrual));
        var attTight = tight.Bills.Single(b => b.BillName == "AT&T");
        Assert.Equal(ihg.Id, attTight.CardId);
        Assert.Contains("status goal that is short", attTight.Recommendation);
    }

    [Fact]
    public void An_earn_rule_only_counts_in_the_years_it_covers()
    {
        // A promo rate for 2026 only; every other year falls back to the card's base rate.
        var card = new Card
        {
            Id = 20, Name = "Cash back", PointValueCents = 1.0m,
            EarnRules = [new() { CategoryId = 4, PointsPerDollar = 5, StartYear = 2026, EndYear = 2026 }, new() { PointsPerDollar = 1.5m }],
        };
        var spend2026 = new List<CardSpend> { new() { CardId = 20, Period = new(2026, 3, 1), CategoryId = 4, Amount = 1_000m } };
        var spend2027 = new List<CardSpend> { new() { CardId = 20, Period = new(2027, 3, 1), CategoryId = 4, Amount = 1_000m } };

        var promo = RewardsOptimizer.Run(Input(new(2026, 12, 31), [card], [], spend2026));
        Assert.Equal(5_000m, promo.Earnings.Single().YtdPoints);
        Assert.Equal(50m, promo.Earnings.Single().YtdDollars);

        var after = RewardsOptimizer.Run(Input(new(2027, 12, 31), [card], [], spend2027) with { Year = 2027 });
        Assert.Equal(1_500m, after.Earnings.Single().YtdPoints);

        Assert.Equal(5m, RewardsOptimizer.EarnRate(card, 4, null, 2026));
        Assert.Equal(1.5m, RewardsOptimizer.EarnRate(card, 4, null, 2027));
        Assert.Equal(5m, RewardsOptimizer.EarnRate(card, 4));           // no year given: first matching rule
    }

    [Fact]
    public void A_threshold_only_counts_in_the_years_it_covers()
    {
        var card = new Card
        {
            Id = 21, Name = "Promo card", PointValueCents = 1.0m, EarnRules = [new() { PointsPerDollar = 1 }],
            Thresholds = [new() { Id = 30, Amount = 10_000m, RewardKind = ThresholdRewardKind.Credit, Description = "2026 only credit", StartYear = 2026, EndYear = 2026 },
                          new() { Id = 31, Amount = 5_000m, RewardKind = ThresholdRewardKind.Credit, Description = "always" }],
        };
        var r2026 = RewardsOptimizer.Run(Input(new(2026, 6, 1), [card], [], []));
        Assert.Equal(2, r2026.Thresholds.Count);
        var r2027 = RewardsOptimizer.Run(Input(new(2027, 6, 1), [card], [], []) with { Year = 2027 });
        Assert.Equal("always", Assert.Single(r2027.Thresholds).Description);
    }

    [Fact]
    public void A_label_beats_the_category_rate_so_the_same_category_can_earn_differently()
    {
        // "General merchandise" earns 1× everywhere, except at Amazon where this card pays 5×.
        var amazon = new Label { Id = 1, Name = "Amazon", CategoryId = 4, PlannedMonthly = 300m };
        var card = new Card
        {
            Id = 30, Name = "Cash back", PointValueCents = 1.0m,
            EarnRules = [new() { CategoryId = 4, LabelId = 1, PointsPerDollar = 5 }, new() { CategoryId = 4, PointsPerDollar = 1 }, new() { PointsPerDollar = 1 }],
        };
        Assert.Equal(5m, RewardsOptimizer.EarnRate(card, 4, 1));        // General merchandise at Amazon
        Assert.Equal(1m, RewardsOptimizer.EarnRate(card, 4, null));     // same category at Costco
        Assert.Equal(1m, RewardsOptimizer.EarnRate(card, 9, 1));        // a different category at Amazon → no match, base rate

        // A label-only rule covers that label in any category.
        var anywhere = new Card { Id = 31, Name = "Label card", PointValueCents = 1.0m, EarnRules = [new() { LabelId = 1, PointsPerDollar = 4 }, new() { PointsPerDollar = 1 }] };
        Assert.Equal(4m, RewardsOptimizer.EarnRate(anywhere, 9, 1));
        Assert.Equal(1m, RewardsOptimizer.EarnRate(anywhere, 9, null));

        // Earnings use the label on each spend row.
        var spend = new List<CardSpend> { new() { CardId = 30, Period = new(2026, 3, 1), CategoryId = 4, LabelId = 1, Amount = 200m }, new() { CardId = 30, Period = new(2026, 3, 1), CategoryId = 4, Amount = 100m } };
        var r = RewardsOptimizer.Run(Input(new(2026, 12, 31), [card], [], spend, labels: [amazon]));
        Assert.Equal(200m * 5 + 100m * 1, r.Earnings.Single().YtdPoints);

        // Planning splits the category: the label's planned spend is carved out, not added on top.
        var plan = RewardsOptimizer.Run(Input(new(2026, 12, 31), [card], [], [], labels: [amazon]));
        Assert.Equal(2_350m, plan.Plan.ProjectedMonthly);                 // unchanged total
        var amazonRoute = plan.Plan.Routing.Single(x => x.LabelId == 1);
        Assert.Equal(300m, amazonRoute.Monthly);
        Assert.Equal(5m, amazonRoute.PointsPerDollar);
        Assert.Equal("Other · Amazon", amazonRoute.Category);
        Assert.Equal(500m, plan.Plan.Routing.Single(x => x.CategoryId == 4 && x.LabelId is null).Monthly);  // 800 planned − 300 to Amazon
    }

    [Fact]
    public void Monthly_earnings_use_category_rates_and_net_out_the_annual_fee()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        var r = RewardsOptimizer.Run(Input(new(2026, 3, 1), [ihg, surpass, sapphire], [ihgProg, hiltonProg], Spend(ihg, surpass, 2)));

        var s = r.Earnings.Single(e => e.CardId == surpass.Id);
        // Per month: 550 restaurants × 6 + 300 other × 3 = 3,300 + 900 = 4,200 pts = $21.00 at 0.5¢
        Assert.Equal(2, s.Months.Count);
        Assert.Equal(4_200m, s.Months[0].Points);
        Assert.Equal(21.00m, s.Months[0].Dollars);
        Assert.Equal(42.00m - 150m, s.NetValue);
        Assert.Contains("− annual fee $150.00", s.Formula);

        var i = r.Earnings.Single(e => e.CardId == ihg.Id);
        // 650 groceries × 3 (no grocery rule → base) + 240 gas × 5 + 500 other × 3 = 1,950 + 1,200 + 1,500 = 4,650 pts/mo
        Assert.Equal(4_650m, i.Months[0].Points);
    }

    [Fact]
    public void Status_earned_last_year_is_held_this_year_without_spending_again()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        ihgProg.CurrentTier = "Diamond";   // $40,000 spent in 2025 bought Diamond for 2026
        var r = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hiltonProg], Spend(ihg, surpass, 9)));
        var ihgStatus = r.Programs.Single(p => p.ProgramId == 1);
        Assert.True(ihgStatus.TargetReached);
        Assert.Contains("already Diamond", ihgStatus.HowReached);
        // With IHG settled, the projected spend is free to chase Hilton instead.
        Assert.Contains("Hilton", string.Join(" ", r.Plan.Steps));
    }

    [Fact]
    public void Next_years_plan_still_asks_for_the_spend_because_status_lapses()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        ihgProg.CurrentTier = "Diamond";
        var input = new RewardsInput(2027, new(2027, 1, 1), [ihg, surpass, sapphire], [ihgProg, hiltonProg], [],
            [Restaurants, Groceries, Gas, Other, Utilities, Housing], [], [], new Dictionary<int, decimal>(), CarryCurrentTier: false);
        var status = RewardsOptimizer.Run(input).Programs.Single(p => p.ProgramId == 1);
        Assert.False(status.TargetReached);
    }

    [Fact]
    public void Holding_a_card_can_grant_status_in_a_program_that_card_has_nothing_to_do_with()
    {
        var (ihg, surpass, sapphire, ihgProg, _) = Fixture();
        var hertz = new LoyaltyProgram { Id = 3, Name = "Hertz Gold Plus Rewards", Priority = 5, TargetTier = "Five Star",
            Tiers = [new() { Name = "Gold", Rank = 0 }, new() { Name = "Five Star", Rank = 1 }],
            Paths = [new() { Id = 20, TierName = "Five Star", Kind = StatusPathKind.HoldCard, CardId = ihg.Id, Card = ihg }] };
        var status = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hertz], [])).Programs.Single(p => p.ProgramId == 3);
        Assert.True(status.TargetReached);
        Assert.Equal("Five Star", status.HeldTier);
    }

    [Fact]
    public void A_status_path_the_program_has_since_dropped_is_ignored()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        ihgProg.Paths.Single(x => x.Kind == StatusPathKind.Nights).EndYear = 2025;
        var status = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hiltonProg], [])).Programs.Single(p => p.ProgramId == 1);
        Assert.DoesNotContain(status.Paths, x => x.Kind == StatusPathKind.Nights);
    }

    [Fact]
    public void A_perk_for_simply_holding_the_card_counts_toward_its_yearly_value()
    {
        var (ihg, surpass, sapphire, ihgProg, hiltonProg) = Fixture();
        ihg.Perks = [new() { Description = "TSA PreCheck credit", AnnualValue = 20m },
                     new() { Description = "Expired offer", AnnualValue = 500m, EndYear = 2024 }];
        var e = RewardsOptimizer.Run(Input(new(2026, 9, 26), [ihg, surpass, sapphire], [ihgProg, hiltonProg], Spend(ihg, surpass, 9))).Earnings.Single(x => x.CardId == ihg.Id);
        Assert.Equal(20m, e.PerksValue);
        Assert.Equal(e.YtdDollars + e.ThresholdRewardsValue + 20m - e.AnnualFee, e.NetValue);
        Assert.Contains("perks", e.Formula);
    }
}

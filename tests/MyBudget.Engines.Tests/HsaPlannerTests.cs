using MyBudget.Domain;
using MyBudget.Engines.Hsa;

namespace MyBudget.Engines.Tests;

/// <summary>Numbers from the 2026 sheet's HSA note, the Phase 2 done-when check.</summary>
public class HsaPlannerTests
{
    private static readonly HsaLimits Limits2026 = new(4_400m, 8_750m, 1_000m);

    private static List<HsaTier> FamilyExceptFebruary()
        => Enumerable.Range(1, 12).Select(m => m == 2 ? HsaTier.NotEligible : HsaTier.Family).ToList();

    [Fact]
    public void Sheet_example_family_not_covered_in_February()
    {
        var plan = HsaPlanner.Plan(new HsaPlanInput(2026, FamilyExceptFebruary(), Limits2026,
            [
                new(new(2026, 1, 15), 1_288m, HsaContributionSource.Employer),   // Inspira
                new(new(2026, 8, 31), 2_805m, HsaContributionSource.Direct),     // Fidelity through August
            ],
            AsOf: new(2026, 9, 1), PaychecksLeft: 8));

        Assert.Equal(11, plan.EligibleMonths);
        Assert.Equal(8_020.83m, plan.AnnualLimit);
        Assert.Equal(6_732.83m, plan.AnnualLimit - plan.Employer);   // "due to not covered in Feb"
        Assert.Equal(3_927.83m, plan.Room);
        Assert.Equal(4, plan.MonthsLeft);                              // Sep, Oct, Nov, Dec
        Assert.Equal(981.96m, plan.RecommendedMonthly);                // "about $982/month"
        Assert.Equal(490.98m, plan.RecommendedPerPaycheck);
        Assert.False(plan.OverContributed);
        Assert.Contains("8,750.00 family × 11/12", plan.LimitFormula);
    }

    [Fact]
    public void Full_year_self_only_with_catch_up()
    {
        var months = Enumerable.Repeat(HsaTier.SelfOnly, 12).ToList();
        var plan = HsaPlanner.Plan(new HsaPlanInput(2026, months, Limits2026, [], new(2026, 1, 1), 26, CatchUpEligible: true));
        Assert.Equal(5_400m, plan.AnnualLimit);
        Assert.Equal(12, plan.MonthsLeft);
        Assert.Equal(450m, plan.RecommendedMonthly);
    }

    [Fact]
    public void Target_below_limit_and_over_contribution_are_reported()
    {
        var months = Enumerable.Repeat(HsaTier.Family, 12).ToList();
        var capped = HsaPlanner.Plan(new HsaPlanInput(2026, months, Limits2026, [], new(2026, 6, 1), 14, TargetAmount: 5_000m));
        Assert.Equal(5_000m, capped.Target);
        Assert.Equal(7, capped.MonthsLeft);
        Assert.Equal(714.29m, capped.RecommendedMonthly);

        var over = HsaPlanner.Plan(new HsaPlanInput(2026, months, Limits2026,
            [new(new(2026, 3, 1), 9_000m, HsaContributionSource.Direct)], new(2026, 6, 1), 14));
        Assert.True(over.OverContributed);
        Assert.Equal(250m, over.OverBy);
        Assert.Equal(0m, over.RecommendedMonthly);
    }
}

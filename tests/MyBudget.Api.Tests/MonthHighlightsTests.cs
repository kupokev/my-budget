using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>"What changed this month", from the real October 2026 numbers it was built against.</summary>
public class MonthHighlightsTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);

    private static BudgetHistoryDto Line(string name, decimal projected, decimal? actual, DateOnly? paidOn = null, int id = 0) =>
        new(id, name, projected, null, Enumerable.Range(1, 12).Select(m => m == 10
            ? new BudgetMonthDto(new(2026, m, 1), null, false, projected, false, actual, actual - projected, paidOn, null)
            : new BudgetMonthDto(new(2026, m, 1), null, false, 0, false, null, null, null, null)).ToList());

    private static readonly List<BudgetHistoryDto> October =
    [
        Line("Ameren MO", 150m, 318.56m),
        Line("House Payment", 1_412.48m, 1_412.48m),
        Line("YMCA", 52m, 52m),
        Line("Lawn Care", 200m, null, new(2026, 10, 31)),   // scheduled: not still to go, not paid either
        Line("Groceries", 400m, null),
    ];

    [Fact]
    public void Over_plan_lines_paid_so_far_and_net_worth_are_stated_with_their_working()
    {
        var list = MonthHighlights.Build(Today, October, [], hsa: null, goals: [], netWorth: 295_283.29m, netWorthChange: 0m);

        Assert.Equal("Ameren MO came to $318.56 against $150.00 planned, $168.56 over.", list[0].Text);
        Assert.Equal("bad", list[0].Tone);
        Assert.Contains("$318.56 actual − $150.00 projected", list[0].Formula);
        Assert.Equal("Paid so far in October: $1,783.04 of $2,214.48 planned, with $400.00 still to go.", list[1].Text);
        Assert.Equal("Net worth is $295,283.29, unchanged since last month.", list[^1].Text);
        Assert.DoesNotContain(list, h => h.Link == "rewards");   // Home stays a health snapshot
    }

    [Fact]
    public void Hsa_pace_and_goals_behind_are_listed()
    {
        var hsa = new HsaPlanDto(2026, Today, 4_400m, 8_750m, 1_000m, "", 12, 8_020.83m, "", 0, 0, 0, 5_215m, 2_805.83m,
            8_020.83m, "", 2_805.83m, 3, 935.28m, 6, 467.64m, "", false, 0, ["limit 8,020.83"]);
        var hsaGoal = new GoalProgressDto(
            new GoalDto { Name = "Max out HSA Contributions", Metric = GoalMetric.AccountTypeContributions, AccountType = AccountType.Hsa },
            5_215m, "", 6_059.69m, 0.75m, 0.65m, "Not On Track", 844.69m, "prorated target 6,059.69; current 5,215.00");
        var other = new GoalProgressDto(new GoalDto { Name = "Emergency fund" }, 1_000m, "", 2_000m, 0.75m, 0.4m, "Not On Track", 1_000m, "");

        var list = MonthHighlights.Build(Today, October, [], hsa, [hsaGoal, other], 295_283.29m, null);

        // The HSA goal joins the HSA line rather than repeating it; other goals still get their own.
        var hsaLine = Assert.Single(list, h => h.Text.Contains("HSA"));
        Assert.Equal("HSA: $5,215.00 of $8,020.83 in, $844.69 behind pace. $935.28 a month reaches it by December.", hsaLine.Text);
        Assert.Equal("bad", hsaLine.Tone);
        Assert.Contains(list, h => h.Text == "Emergency fund is behind: $1,000.00 short of where it should be by now.");
    }

    [Fact]
    public void A_line_making_its_last_payment_frees_its_monthly_cost_and_one_starting_next_month_is_flagged()
    {
        List<BudgetHistoryDto> grid = [Line("Lawyer", 1_550m, null, id: 7), Line("Old gym", 0m, null, id: 8)];
        List<BudgetLineDto> lines =
        [
            new() { Id = 7, Name = "Lawyer", ProjectedAmount = 1_550m, MonthlyAccrual = 1_550m, StartDate = new(2026, 1, 15), EndDate = new(2026, 10, 15) },
            // Ended this month too, but nothing was due: not announced as money freed.
            new() { Id = 8, Name = "Old gym", ProjectedAmount = 40m, MonthlyAccrual = 40m, EndDate = new(2026, 10, 1) },
            new() { Id = 9, Name = "Streaming", ProjectedAmount = 17.99m, MonthlyAccrual = 17.99m, StartDate = new(2026, 11, 5) },
        ];

        var list = MonthHighlights.Build(Today, grid, lines, null, [], 295_283.29m, 0m);

        var ends = Assert.Single(list, h => h.Text.Contains("ends"));
        Assert.Equal("Lawyer ends with this month's payment, freeing $1,550.00 a month from November.", ends.Text);
        Assert.Equal("good", ends.Tone);
        Assert.Contains(list, h => h.Text == "Streaming starts in November: $17.99 a month.");
    }

    [Fact]
    public void Money_freed_next_month_is_set_against_what_the_hsa_needs_from_then()
    {
        List<BudgetHistoryDto> grid = [Line("Lawyer", 1_550m, null, id: 7)];
        List<BudgetLineDto> lines = [new() { Id = 7, Name = "Lawyer", ProjectedAmount = 1_550m, MonthlyAccrual = 1_550m, EndDate = new(2026, 10, 15) }];
        var hsa = new HsaPlanDto(2026, Today, 4_400m, 8_750m, 1_000m, "", 12, 8_020.83m, "", 0, 0, 0, 5_215m, 2_805.83m,
            8_020.83m, "", 2_805.83m, 3, 935.28m, 6, 467.64m, "", false, 0, []);

        var list = MonthHighlights.Build(Today, grid, lines, hsa, [], 295_283.29m, 0m);

        // Three months left counts October; the freed money starts in November, so it is spread over two.
        var line = Assert.Single(list, h => h.Text.StartsWith("From November"));
        Assert.Equal("From November, the $1,550.00 a month freed by Lawyer covers the HSA: its $2,805.83 still to go is $1,402.92 a month " +
                     "over November and December, leaving $147.08 a month to spare.", line.Text);
        Assert.Equal("good", line.Tone);
    }
}

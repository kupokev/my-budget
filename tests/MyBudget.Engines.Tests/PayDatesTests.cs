using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Engines.Tests;

/// <summary>
/// Mirrors the 2026 sheet: semi-monthly (15th and last day) through January, then bi-weekly from
/// February 1 anchored on Friday 2026-02-06 — the "24 → 26 periods" split.
/// </summary>
public class PayDatesTests
{
    private static readonly PaySchedule SemiMonthly2025 = new()
    {
        Frequency = PayFrequency.SemiMonthly, EffectiveDate = new(2025, 1, 1),
        AnchorPayDate = new(2025, 1, 15), FirstPayDay = 15, SecondPayDay = 31,
    };

    private static readonly PaySchedule BiWeeklyFromFeb2026 = new()
    {
        Frequency = PayFrequency.BiWeekly, EffectiveDate = new(2026, 2, 1), AnchorPayDate = new(2026, 2, 6),
    };

    [Fact]
    public void Year_2026_has_two_semimonthly_checks_in_January_then_biweekly()
    {
        var dates = PayDates.Generate([SemiMonthly2025, BiWeeklyFromFeb2026], new(2026, 1, 1), new(2026, 12, 31));

        // Jan 15 is a Thursday; Jan 31 is a Saturday so it shifts to Friday Jan 30.
        Assert.Equal([new(2026, 1, 15), new(2026, 1, 30)], dates.Where(d => d.Month == 1));
        Assert.Equal(new DateOnly(2026, 2, 6), dates.First(d => d.Month == 2));
        Assert.Equal(new DateOnly(2026, 12, 25), dates.Last());
        Assert.Equal(26, dates.Count); // 2 semi-monthly in January + 24 bi-weekly Feb 6 … Dec 25
        Assert.All(dates.Where(d => d >= new DateOnly(2026, 2, 1)), d => Assert.Equal(DayOfWeek.Friday, d.DayOfWeek));
    }

    [Fact]
    public void Three_paycheck_months_in_2026_are_May_and_October()
    {
        var dates = PayDates.Generate([SemiMonthly2025, BiWeeklyFromFeb2026], new(2026, 1, 1), new(2026, 12, 31));
        var triples = PayDates.ThreePaycheckMonths(dates);

        Assert.Equal([(2026, 5), (2026, 10)], triples.Select(t => (t.Year, t.Month)));
        Assert.Equal([new(2026, 5, 1), new(2026, 5, 15), new(2026, 5, 29)], triples[0].Dates);
    }

    [Fact]
    public void Full_year_of_semimonthly_is_24_checks_and_never_three_in_a_month()
    {
        var dates = PayDates.Generate([SemiMonthly2025], new(2025, 1, 1), new(2025, 12, 31));
        Assert.Equal(24, dates.Count);
        Assert.Empty(PayDates.ThreePaycheckMonths(dates));
        Assert.All(dates, d => Assert.NotEqual(DayOfWeek.Saturday, d.DayOfWeek));
        Assert.All(dates, d => Assert.NotEqual(DayOfWeek.Sunday, d.DayOfWeek));
    }

    [Fact]
    public void Monthly_schedule_clamps_to_month_end()
    {
        var s = new PaySchedule { Frequency = PayFrequency.Monthly, EffectiveDate = new(2026, 1, 1), AnchorPayDate = new(2026, 1, 31), PayOnPriorBusinessDay = false };
        var dates = PayDates.Generate([s], new(2026, 1, 1), new(2026, 3, 31));
        Assert.Equal([new(2026, 1, 31), new(2026, 2, 28), new(2026, 3, 31)], dates);
    }

    [Theory]
    [InlineData(PayFrequency.BiWeekly, 26)]
    [InlineData(PayFrequency.SemiMonthly, 24)]
    [InlineData(PayFrequency.Monthly, 12)]
    public void Paychecks_per_year(PayFrequency f, int expected) => Assert.Equal(expected, PayDates.PaychecksPerYear(f));
}

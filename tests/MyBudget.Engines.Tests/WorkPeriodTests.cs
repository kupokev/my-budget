using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Engines.Tests;

/// <summary>
/// Live pay versus arrears: the same money, a different period. Under live pay the cheque covers work
/// up to and including payday, so its last days have not been worked yet. A week in arrears closes the
/// period a week before. Employers switch between them — Kevin's does in October 2026 — and the switch
/// is a second effective-dated schedule, not an edit to the first.
/// </summary>
public class WorkPeriodTests
{
    private static PaySchedule BiWeekly(int lagDays) => new()
    {
        Frequency = PayFrequency.BiWeekly,
        EffectiveDate = new DateOnly(2026, 1, 1),
        AnchorPayDate = new DateOnly(2026, 1, 9),
        PayLagDays = lagDays,
    };

    [Fact]
    public void Live_pay_covers_the_fortnight_ending_on_payday()
    {
        var (start, end) = PayDates.WorkPeriod(BiWeekly(0), new DateOnly(2026, 10, 2));

        Assert.Equal(new DateOnly(2026, 9, 19), start);
        Assert.Equal(new DateOnly(2026, 10, 2), end);      // payday itself: days not yet worked are paid
    }

    [Fact]
    public void A_week_in_arrears_shifts_the_same_fortnight_back_by_a_week()
    {
        var (start, end) = PayDates.WorkPeriod(BiWeekly(7), new DateOnly(2026, 10, 9));

        Assert.Equal(new DateOnly(2026, 9, 19), start);
        Assert.Equal(new DateOnly(2026, 10, 2), end);      // closed a week before it is paid
    }

    [Fact]
    public void The_period_is_still_a_fortnight_whatever_the_lag()
    {
        foreach (var lag in new[] { 0, 7, 14 })
        {
            var (start, end) = PayDates.WorkPeriod(BiWeekly(lag), new DateOnly(2026, 10, 9));
            Assert.Equal(13, end.DayNumber - start.DayNumber);
        }
    }

    [Fact]
    public void Semi_monthly_periods_are_the_halves_of_the_month_they_end_in()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.SemiMonthly, EffectiveDate = new(2026, 1, 1), AnchorPayDate = new(2026, 1, 15) };

        var firstHalf = PayDates.WorkPeriod(schedule, new DateOnly(2026, 10, 15));
        Assert.Equal(new DateOnly(2026, 10, 1), firstHalf.Start);

        var secondHalf = PayDates.WorkPeriod(schedule, new DateOnly(2026, 10, 31));
        Assert.Equal(new DateOnly(2026, 10, 16), secondHalf.Start);
    }
}

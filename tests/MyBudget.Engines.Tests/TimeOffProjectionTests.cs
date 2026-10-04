using MyBudget.Engines.Ledger;

namespace MyBudget.Engines.Tests;

public class TimeOffProjectionTests
{
    // Biweekly Thursdays from Oct 1, 2026.
    private static readonly DateOnly[] PayDates = Enumerable.Range(0, 30).Select(i => new DateOnly(2026, 10, 1).AddDays(14 * i)).ToArray();

    [Fact]
    public void Accrues_per_paycheck_after_the_stub_and_before_the_day_off()
    {
        // Stub Oct 1 says 40h; 4.62h a check. Checks Oct 15, 29, Nov 12, 26 land before Dec 10; Dec 10 itself doesn't count.
        var f = TimeOffProjection.Project(40m, new(2026, 10, 1), new(2026, 12, 10), 4.62m, PayDates);
        Assert.Equal(4, f.Paychecks);
        Assert.Equal(58.48m, f.Hours);
        Assert.Equal("40h on Oct 1, 2026 + 4 paychecks × 4.62h = 58.48h by Dec 10, 2026", f.Formula);
    }

    [Fact]
    public void A_yearly_grant_lands_on_the_first_of_its_month()
    {
        var f = TimeOffProjection.Project(16m, new(2026, 10, 1), new(2027, 2, 15), 0m, PayDates, annualGrant: 40m, grantMonth: 1);
        Assert.Equal(1, f.Grants);
        Assert.Equal(56m, f.Hours);
    }

    [Fact]
    public void Accrual_stops_at_the_cap_and_is_lost_not_banked()
    {
        // 75h + 10 checks × 4h would be 115h; the 80h cap holds it at 80.
        var f = TimeOffProjection.Project(75m, new(2026, 10, 1), new(2027, 3, 1), 4m, PayDates, cap: 80m);
        Assert.Equal(80m, f.Hours);
        Assert.True(f.HitCap);
        Assert.Contains("held at the 80h cap", f.Formula);
    }
}

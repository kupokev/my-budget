namespace MyBudget.Engines.Ledger;

/// <summary>A bucket's balance carried forward to a date, and the working behind it.</summary>
public sealed record TimeOffForecast(DateOnly On, decimal Hours, decimal StartBalance, DateOnly StartDate,
    int Paychecks, decimal AccruedHours, int Grants, decimal GrantedHours, bool HitCap, string Formula);

/// <summary>
/// Carries a time-off balance forward from the last stub: each paycheck after it adds the accrual, each
/// yearly grant date adds the grant, and a cap stops growth at the cap — applied in date order, because
/// hours accrued while at the cap are lost and can't be made up later. Planned time off isn't
/// subtracted: this answers "what will I have", and a trip's hours are compared against it.
/// </summary>
public static class TimeOffProjection
{
    /// <param name="payDates">Every pay date for the job; only those after the stub and before <paramref name="on"/> count.</param>
    /// <param name="grantMonth">Month the yearly grant lands, on its first day.</param>
    public static TimeOffForecast Project(decimal startBalance, DateOnly startDate, DateOnly on, decimal accrualPerPaycheck,
        IEnumerable<DateOnly> payDates, decimal? annualGrant = null, int grantMonth = 1, decimal? cap = null)
    {
        // A check paid on the day the time off starts doesn't help: the hours are needed that morning.
        var events = payDates.Where(d => d > startDate && d < on).Select(d => (Date: d, Hours: accrualPerPaycheck, Grant: false)).ToList();
        if (annualGrant is > 0)
            for (var y = startDate.Year; y <= on.Year; y++)
            {
                var grant = new DateOnly(y, Math.Clamp(grantMonth, 1, 12), 1);
                if (grant > startDate && grant <= on) events.Add((grant, annualGrant.Value, true));
            }

        decimal balance = startBalance, accrued = 0, granted = 0;
        var hitCap = false;
        foreach (var e in events.OrderBy(e => e.Date))
        {
            var add = e.Hours;
            if (cap is { } c && balance + add > c) { add = Math.Max(0, c - balance); hitCap = true; }
            balance += add;
            if (e.Grant) granted += add; else accrued += add;
        }

        var checks = events.Count(e => !e.Grant);
        var grants = events.Count(e => e.Grant);
        var formula = $"{startBalance:0.##}h on {startDate:MMM d, yyyy}"
                      + (checks > 0 ? $" + {checks} paycheck{(checks == 1 ? "" : "s")} × {accrualPerPaycheck:0.##}h" : "")
                      + (grants > 0 ? $" + {grants} yearly grant{(grants == 1 ? "" : "s")} of {annualGrant:0.##}h" : "")
                      + (hitCap ? $", held at the {cap:0.##}h cap" : "")
                      + $" = {balance:0.##}h by {on:MMM d, yyyy}";
        return new TimeOffForecast(on, Math.Round(balance, 2), startBalance, startDate, checks, Math.Round(accrued, 2), grants, Math.Round(granted, 2), hitCap, formula);
    }
}

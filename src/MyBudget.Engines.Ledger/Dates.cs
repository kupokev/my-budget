namespace MyBudget.Engines.Ledger;

internal static class Dates
{
    /// <summary>Day-of-month clamped to the month's length; 31 always means "last day".</summary>
    public static DateOnly OnDay(int year, int month, int day)
        => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    public static DateOnly PriorBusinessDay(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(-1),
        DayOfWeek.Sunday => d.AddDays(-2),
        _ => d,
    };

    public static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
}

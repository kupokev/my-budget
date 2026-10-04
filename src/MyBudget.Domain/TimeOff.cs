namespace MyBudget.Domain;

/// <summary>
/// One kind of paid time off at one employer, named as that employer names it. Employers differ: one
/// keeps a single PTO bucket covering vacation and sickness, another separates PTO from sick time and
/// adds a floating-holiday bucket. Some accrue a little every paycheck, some grant a block once a year,
/// some do both. So a bucket says how it grows, and its balance is whatever the latest stub said
/// (<see cref="PaycheckTimeOff"/>).
/// </summary>
public class TimeOffBucket
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public required string Name { get; set; }

    /// <summary>Hours added on every paycheck; null when this bucket doesn't accrue.</summary>
    public decimal? AccrualHoursPerPaycheck { get; set; }

    /// <summary>Hours granted once a year, in <see cref="GrantMonth"/>; null when there's no grant.</summary>
    public decimal? AnnualGrantHours { get; set; }

    /// <summary>Month (1–12) the yearly grant lands. January when not set.</summary>
    public int? GrantMonth { get; set; }

    /// <summary>The most this bucket can hold; accrual stops at it. Null for no cap.</summary>
    public decimal? MaxHours { get; set; }

    /// <summary>A working day, for showing hours as days.</summary>
    public decimal HoursPerDay { get; set; } = 8;

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<PaycheckTimeOff> StubLines { get; set; } = [];
}

/// <summary>
/// One bucket's line on a pay stub: what this check added, what was taken, and the balance printed.
/// The newest stub's balance is where projection starts, and its accrual is the rate when the bucket
/// doesn't set one — so the stubs teach the app the rate rather than it being typed twice.
/// </summary>
public class PaycheckTimeOff
{
    public int Id { get; set; }
    public int PaycheckId { get; set; }
    public Paycheck? Paycheck { get; set; }
    public int BucketId { get; set; }
    public TimeOffBucket? Bucket { get; set; }
    public decimal? Accrued { get; set; }
    public decimal? Used { get; set; }
    public decimal Balance { get; set; }
}

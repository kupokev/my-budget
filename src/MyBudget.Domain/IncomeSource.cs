namespace MyBudget.Domain;

/// <summary>An employer or other source of money (INC-1).</summary>
public class IncomeSource
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public IncomeSourceType Type { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Employment ended: no pay dates or estimates after this date.</summary>
    public DateOnly? EndDate { get; set; }

    public List<SalaryRate> SalaryRates { get; set; } = [];
    public List<PaySchedule> PaySchedules { get; set; } = [];
    public List<DeductionElection> Deductions { get; set; } = [];
    public List<WithholdingElection> Withholdings { get; set; } = [];
    public List<PaycheckOverride> Overrides { get; set; } = [];
}

/// <summary>
/// One specific check that isn't a normal period: a fraction of the usual gross (a live-to-arrears switch
/// paying one week of a two-week period), or an explicit gross. Fixed deductions stay whole unless prorated.
/// </summary>
public class PaycheckOverride
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public DateOnly PayDate { get; set; }
    /// <summary>0.5 = half the normal gross. Ignored when GrossAmount is set.</summary>
    public decimal? GrossFraction { get; set; }
    public decimal? GrossAmount { get; set; }
    /// <summary>Scale fixed per-check deductions (medical, dental…) by the same fraction. Percent-of-gross ones scale automatically.</summary>
    public bool ProrateFixedDeductions { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Annual salary in effect from a date (INC-2). A raise is a new row, never an edit.</summary>
public class SalaryRate
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public decimal AnnualAmount { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

/// <summary>
/// Pay cadence in effect from a date (INC-3). Effective-dated because the 2026 sheet switched
/// from 24 to 26 periods mid-year.
/// </summary>
public class PaySchedule
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public PayFrequency Frequency { get; set; }
    public DateOnly EffectiveDate { get; set; }

    /// <summary>A known pay date; every other pay date is derived from it. Required for bi-weekly and monthly.</summary>
    public DateOnly AnchorPayDate { get; set; }

    /// <summary>Shift a pay date that lands on a weekend to the preceding Friday, as most employers do.</summary>
    public bool PayOnPriorBusinessDay { get; set; } = true;

    /// <summary>Semi-monthly only: the two pay days of the month. 31 means "last day of the month".</summary>
    public int? FirstPayDay { get; set; }
    public int? SecondPayDay { get; set; }
}

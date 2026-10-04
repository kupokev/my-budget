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

    /// <summary>
    /// Where the net pay lands. A cheque is rarely one deposit: a fixed amount into each of several
    /// accounts and whatever is left into the last one. Without this, a $2,150 deposit looks like an
    /// unexplained credit rather than part of a cheque.
    /// </summary>
    public List<DepositSplit> DepositSplits { get; set; } = [];

    /// <summary>Paid time off, in whatever buckets this employer keeps.</summary>
    public List<TimeOffBucket> TimeOffBuckets { get; set; } = [];

    /// <summary>
    /// How one cheque's net divides across accounts, in order. Fixed amounts come out first and the
    /// remainder row takes what is left, which is how payroll systems do it — so a raise or a
    /// deduction change moves only the remainder, exactly as it does in real life.
    /// </summary>
    public IReadOnlyList<(DepositSplit Split, decimal Amount)> SplitOf(decimal net)
    {
        var ordered = DepositSplits.Where(s => s.IsActive).OrderBy(s => s.Order).ToList();
        var result = new List<(DepositSplit, decimal)>();
        var left = net;

        foreach (var split in ordered.Where(s => !s.IsRemainder))
        {
            var take = Math.Min(Math.Max(0m, left), split.Amount ?? 0m);
            result.Add((split, take));
            left -= take;
        }

        foreach (var split in ordered.Where(s => s.IsRemainder))
        {
            result.Add((split, Math.Max(0m, left)));
            left = 0m;
        }

        return result;
    }
}

/// <summary>One account a paycheck is deposited into, and how much of it goes there.</summary>
public class DepositSplit
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>A fixed amount per cheque. Ignored when <see cref="IsRemainder"/> is set.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Takes whatever is left after the fixed amounts. Only one row should have this.</summary>
    public bool IsRemainder { get; set; }

    /// <summary>Fixed amounts are taken in this order, which matters when a cheque is short.</summary>
    public int Order { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
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

    /// <summary>
    /// Days between the end of the work period and the day it is paid.
    ///
    /// 0 is "live" or current pay: the cheque covers work up to and including the pay date, so part of
    /// it is for days not yet worked. 7 is a week in arrears: the period closed a week before payday.
    /// Employers move between the two — the switch is a new schedule with a later anchor and a lag —
    /// and the difference decides which period a cheque is for, not how much it is.
    /// </summary>
    public int PayLagDays { get; set; }

    /// <summary>Semi-monthly only: the two pay days of the month. 31 means "last day of the month".</summary>
    public int? FirstPayDay { get; set; }
    public int? SecondPayDay { get; set; }
}

namespace MyBudget.Domain;

public enum GoalKind { Financial, NonFinancial }

/// <summary>What a financial goal's "current" value is read from, so it fills itself (GOL-1).</summary>
public enum GoalMetric
{
    /// <summary>Typed in by hand.</summary>
    Manual,
    /// <summary>Accounts + investments − cards − loans, from latest balances.</summary>
    NetWorth,
    /// <summary>Sum of the latest balances of the selected accounts.</summary>
    AccountBalances,
    /// <summary>HSA contributions in the goal's year (all sources).</summary>
    HsaContributed,
    /// <summary>401(k) employee deferrals estimated from paychecks in the goal's year.</summary>
    Retirement401kContributed,
    /// <summary>Net money in for transactions in a category within the goal's dates (side income).</summary>
    CategoryInflow,
    /// <summary>Money out for transactions in a category within the goal's dates (keep spending under X).</summary>
    CategoryOutflow,
    /// <summary>Remaining balance of a loan (a payoff goal: target is what you want it down to).</summary>
    LoanBalance,
}

public enum GoalStatus { NotStarted, InProgress, Done }

/// <summary>A goal with a prorated "on track" target (GOL-2). Non-financial goals just carry a status (GOL-3).</summary>
public class Goal
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public GoalKind Kind { get; set; }
    public GoalMetric Metric { get; set; }
    public decimal TargetAmount { get; set; }
    /// <summary>Value at the start date; progress is measured from here (e.g. net worth on Jan 1).</summary>
    public decimal? StartValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    /// <summary>For Manual: the current value. For other metrics: ignored.</summary>
    public decimal? ManualCurrent { get; set; }
    /// <summary>Comma-separated account ids for AccountBalances.</summary>
    public string? AccountIds { get; set; }
    public int? CategoryId { get; set; }
    public int? LoanId { get; set; }
    /// <summary>True when smaller is better (spend under X, loan balance down to X).</summary>
    public bool LowerIsBetter { get; set; }
    public GoalStatus Status { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

namespace MyBudget.Domain;

public enum HsaTier { NotEligible, SelfOnly, Family }
public enum HsaContributionSource { Employer, Payroll, Direct }

/// <summary>One HSA year: per-month eligibility and tier, an optional target, and limit overrides (HSA-1, HSA-1a).</summary>
public class HsaYear
{
    public int Id { get; set; }
    public int Year { get; set; }
    /// <summary>Contribute to this amount instead of the computed limit, if set.</summary>
    public decimal? TargetAmount { get; set; }
    /// <summary>Reach the target by this date; default December 31 (direct contributions are allowed until the filing deadline).</summary>
    public DateOnly? TargetDate { get; set; }
    public bool CatchUpEligible { get; set; }
    public decimal? LimitOverrideSelfOnly { get; set; }
    public decimal? LimitOverrideFamily { get; set; }
    public string? Notes { get; set; }
    public List<HsaMonth> Months { get; set; } = [];
    public List<HsaContribution> Contributions { get; set; } = [];
}

public class HsaMonth
{
    public int Id { get; set; }
    public int HsaYearId { get; set; }
    public HsaYear? HsaYear { get; set; }
    public int Month { get; set; }
    public HsaTier Tier { get; set; }
}

public class HsaContribution
{
    public int Id { get; set; }
    public int HsaYearId { get; set; }
    public HsaYear? HsaYear { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public HsaContributionSource Source { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public string? Notes { get; set; }
}

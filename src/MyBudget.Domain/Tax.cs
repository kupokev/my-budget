namespace MyBudget.Domain;

public enum Jurisdiction { Federal, Missouri }

/// <summary>Federal W-4 filing status as Pub 15-T groups it (Single and MFS share a table).</summary>
public enum FederalFilingStatus { SingleOrMarriedFilingSeparately, MarriedFilingJointly, HeadOfHousehold }

/// <summary>Missouri MO W-4 filing status; it selects the standard deduction used by the MO withholding formula.</summary>
public enum MissouriFilingStatus { Single, MarriedSpouseWorks, MarriedOneIncome, HeadOfHousehold }

/// <summary>
/// Reference tax rules for one year (the design doc's TaxTable). Loaded or confirmed each January.
/// Every rate is a fraction (0.062, not 6.2). Brackets live in <see cref="TaxBracket"/>.
/// </summary>
public class TaxYear
{
    public int Id { get; set; }
    public int Year { get; set; }

    // Federal standard deduction by W-4 status (Pub 15-T's standard table is these brackets shifted by it).
    public decimal FederalStandardDeductionSingle { get; set; }
    public decimal FederalStandardDeductionMarriedJointly { get; set; }
    public decimal FederalStandardDeductionHeadOfHousehold { get; set; }

    public decimal SocialSecurityRate { get; set; }
    public decimal SocialSecurityWageBase { get; set; }
    public decimal MedicareRate { get; set; }
    public decimal AdditionalMedicareRate { get; set; }
    public decimal AdditionalMedicareThreshold { get; set; }

    /// <summary>Flat supplemental (bonus) withholding rate, and the higher rate above the cumulative threshold.</summary>
    public decimal SupplementalRate { get; set; }
    public decimal SupplementalHighRate { get; set; }
    public decimal SupplementalHighThreshold { get; set; }

    // Missouri: standard deduction by MO W-4 status, flat supplemental rate; brackets in TaxBracket (status-independent).
    public decimal MissouriStandardDeductionSingle { get; set; }
    public decimal MissouriStandardDeductionMarriedSpouseWorks { get; set; }
    public decimal MissouriStandardDeductionMarriedOneIncome { get; set; }
    public decimal MissouriStandardDeductionHeadOfHousehold { get; set; }
    public decimal MissouriSupplementalRate { get; set; }

    /// <summary>Where the numbers came from, and whether a human has checked them against the published tables.</summary>
    public string? Source { get; set; }
    public bool Verified { get; set; }
    public string? Notes { get; set; }

    public List<TaxBracket> Brackets { get; set; } = [];
}

/// <summary>One marginal bracket: the rate applies to taxable income over <see cref="Over"/> up to the next bracket.</summary>
public class TaxBracket
{
    public int Id { get; set; }
    public int TaxYearId { get; set; }
    public TaxYear? TaxYear { get; set; }
    public Jurisdiction Jurisdiction { get; set; }
    /// <summary>Federal only; null for Missouri whose brackets don't vary by status.</summary>
    public FederalFilingStatus? FilingStatus { get; set; }
    public decimal Over { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>Annual contribution limits (the design doc's LimitTable).</summary>
public class ContributionLimits
{
    public int Id { get; set; }
    public int Year { get; set; }
    public decimal HsaSelfOnly { get; set; }
    public decimal HsaFamily { get; set; }
    public decimal HsaCatchUp { get; set; }
    public decimal Retirement401kEmployee { get; set; }
    public decimal Retirement401kCatchUp { get; set; }
    public decimal Retirement401kTotal { get; set; }
    public decimal Ira { get; set; }
    public decimal IraCatchUp { get; set; }
    public string? Source { get; set; }
    public bool Verified { get; set; }
}

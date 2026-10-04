namespace MyBudget.Domain;

/// <summary>How a deduction affects taxable wages.</summary>
public enum DeductionTreatment
{
    /// <summary>Section 125 cafeteria plan (medical, dental, vision, payroll HSA): reduces FICA wages and income-tax wages.</summary>
    PreTaxSection125,
    /// <summary>Traditional 401(k): reduces federal and Missouri income-tax wages but not FICA wages.</summary>
    PreTaxRetirement,
    /// <summary>Roth 401(k) or any after-tax deduction: reduces nothing, comes out of net.</summary>
    PostTax,
}

public enum DeductionKind { Medical, Dental, Vision, Life, Disability, Hsa, Retirement401k, Roth401k, Other }

/// <summary>A benefit election in effect from a date (INC-6): a fixed per-check amount or a percent of gross.</summary>
public class DeductionElection
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public required string Name { get; set; }
    public DeductionKind Kind { get; set; }
    public DeductionTreatment Treatment { get; set; }
    public decimal? AmountPerCheck { get; set; }
    /// <summary>Fraction of gross per check (0.06 = 6%). Exactly one of amount or percent is set.</summary>
    public decimal? PercentOfGross { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

/// <summary>W-4 and MO W-4 inputs in effect from a date (INC-9a). All statuses supported; v1 data is Single with no adjustments.</summary>
public class WithholdingElection
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public DateOnly EffectiveDate { get; set; }

    public FederalFilingStatus FederalStatus { get; set; }
    /// <summary>W-4 Step 2(c) checkbox.</summary>
    public bool MultipleJobs { get; set; }
    /// <summary>W-4 Step 3 total annual credits.</summary>
    public decimal DependentCredits { get; set; }
    /// <summary>W-4 Step 4(a) other annual income.</summary>
    public decimal OtherIncome { get; set; }
    /// <summary>W-4 Step 4(b) annual deductions beyond the standard deduction.</summary>
    public decimal Deductions { get; set; }
    /// <summary>W-4 Step 4(c) extra withholding per check.</summary>
    public decimal ExtraWithholding { get; set; }

    public MissouriFilingStatus MissouriStatus { get; set; }
    public decimal MissouriExtraWithholding { get; set; }
}

public enum PaycheckKind { Regular, Supplemental }
public enum PaycheckLineCategory { Earning, PreTaxDeduction, Tax, PostTaxDeduction }

/// <summary>An actual pay stub, entered by hand, for comparison to the estimate (INC-7).</summary>
public class Paycheck
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public DateOnly PayDate { get; set; }
    public PaycheckKind Kind { get; set; }
    public decimal Gross { get; set; }
    public decimal Net { get; set; }
    public string? Notes { get; set; }
    public List<PaycheckLine> Lines { get; set; } = [];

    /// <summary>Time-off lines as printed on the stub: accrued, used, balance per bucket.</summary>
    public List<PaycheckTimeOff> TimeOff { get; set; } = [];
}

public class PaycheckLine
{
    public int Id { get; set; }
    public int PaycheckId { get; set; }
    public Paycheck? Paycheck { get; set; }
    public PaycheckLineCategory Category { get; set; }
    public required string Name { get; set; }
    public decimal Amount { get; set; }
}

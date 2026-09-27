namespace MyBudget.Domain;

public enum LoanKind { Mortgage, Heloc, Auto, Personal, Other }

/// <summary>A loan or line of credit (DBT-1). Balance snapshots drive the payoff projection from today.</summary>
public class Loan
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public LoanKind Kind { get; set; }
    public string? Lender { get; set; }
    /// <summary>The lender's loan / account number (ADR-0008).</summary>
    public string? AccountNumber { get; set; }
    public decimal OriginalPrincipal { get; set; }
    /// <summary>Annual rate as a fraction (0.0625 = 6.25%).</summary>
    public decimal AnnualRate { get; set; }
    public int TermMonths { get; set; }
    public DateOnly StartDate { get; set; }
    /// <summary>Scheduled principal + interest payment. Null means compute it from principal/rate/term.</summary>
    public decimal? ScheduledPayment { get; set; }
    /// <summary>Extra principal paid every month on top of the scheduled payment.</summary>
    public decimal ExtraMonthlyPayment { get; set; }
    /// <summary>The line that carries this loan's payment in the ledger, if any.</summary>
    public int? BudgetLineId { get; set; }
    public BudgetLine? BudgetLine { get; set; }
    /// <summary>The asset this loan is secured against, if any. A house can carry both a mortgage and an equity loan.</summary>
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<LoanBalance> Balances { get; set; } = [];
}

public class LoanBalance
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public Loan? Loan { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Balance { get; set; }
}

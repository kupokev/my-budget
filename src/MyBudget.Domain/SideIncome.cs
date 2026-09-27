namespace MyBudget.Domain;

/// <summary>Money received from a 1099 source (INC-8). Entered when it arrives; feeds the set-aside estimate.</summary>
public class IncomeReceipt
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A quarterly estimated tax payment already made (INC-8).</summary>
public class EstimatedTaxPayment
{
    public int Id { get; set; }
    public int Year { get; set; }
    public Jurisdiction Jurisdiction { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

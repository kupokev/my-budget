namespace MyBudget.Domain;

/// <summary>
/// A tag that says *where* a transaction happened, next to the category that says *what kind* it was:
/// "General merchandise" at Amazon vs at Costco. Earn rules can pay a different rate per label, so one
/// card can earn 5× at Amazon and its base rate on the same category elsewhere.
/// </summary>
public class Label
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>The category these purchases usually fall in; used when planning spend.</summary>
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    /// <summary>Planned monthly spend carrying this label, carved out of its category's planned amount.</summary>
    public decimal? PlannedMonthly { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

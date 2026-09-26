namespace MyBudget.Domain;

public enum ImportFormat { Csv, Ofx }

/// <summary>One uploaded statement file (ADR-0001: import is a manual upload per institution).</summary>
public class ImportBatch
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public ImportFormat Format { get; set; }
    public string? Profile { get; set; }
    public DateTime ImportedAt { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public int? CardId { get; set; }
    public Card? Card { get; set; }
    public int RowCount { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public DateOnly? FirstDate { get; set; }
    public DateOnly? LastDate { get; set; }
}

/// <summary>
/// A statement line on a bank account or card (BIL-7). Amount is signed from the owner's point of view:
/// negative = money out (a purchase, a payment), positive = money in (a deposit, a refund, a credit).
/// </summary>
public class Transaction
{
    public int Id { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public int? CardId { get; set; }
    public Card? Card { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? PostedDate { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    /// <summary>Cleaned-up payee, for grouping (BIL-9).</summary>
    public string? Merchant { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    /// <summary>The tracked bill this line pays, if any (BIL-3 actuals come from these).</summary>
    public int? BillId { get; set; }
    public Bill? Bill { get; set; }
    /// <summary>Money moved between own accounts (or a card payment): excluded from spending.</summary>
    public bool IsTransfer { get; set; }
    /// <summary>OFX FITID, or a hash of date/amount/description/source for CSV; unique per source for de-duplication.</summary>
    public required string ExternalId { get; set; }
    public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public string? Notes { get; set; }
    /// <summary>Set when the user categorized it by hand; auto-categorization never overrides these.</summary>
    public bool IsManuallyCategorized { get; set; }
}

public enum RuleMatch { Contains, StartsWith, Regex }

/// <summary>"Always file lines matching X under category Y (and bill Z)". Created from the Transactions page (BIL-8).</summary>
public class CategoryRule
{
    public int Id { get; set; }
    public required string Pattern { get; set; }
    public RuleMatch Match { get; set; } = RuleMatch.Contains;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? BillId { get; set; }
    public Bill? Bill { get; set; }
    public bool MarkAsTransfer { get; set; }
    /// <summary>Lower runs first.</summary>
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

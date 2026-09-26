namespace MyBudget.Domain;

/// <summary>A spending bucket: a utility tracked individually, or a variable category like Restaurants (BIL-8).</summary>
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A recurring or one-off obligation (BIL-1, BIL-2).</summary>
public class Bill
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public BillFrequency Frequency { get; set; } = BillFrequency.Monthly;
    /// <summary>Day of month the bill is due (1–31; 31 means last day).</summary>
    public int DueDay { get; set; }
    /// <summary>
    /// For non-monthly bills: a known due date from which the others are derived (e.g. car insurance
    /// due 2026-03-15 every six months). For one-off bills: the single due date.
    /// </summary>
    public DateOnly? AnchorDueDate { get; set; }
    public bool IsAutopay { get; set; }
    public decimal ProjectedAmount { get; set; }

    /// <summary>What pays the bill: a bank account or a card (BIL-2).</summary>
    public PaymentMethodKind PaymentMethod { get; set; } = PaymentMethodKind.Account;
    public int? PaymentAccountId { get; set; }
    public Account? PaymentAccount { get; set; }
    public int? PaymentCardId { get; set; }
    public Card? PaymentCard { get; set; }

    /// <summary>
    /// Where the money really comes from, always set. For a card-paid bill this is the account the
    /// cost rolls up under for transfer needs (BIL-2, ACC-2a), not the account that pays the card.
    /// </summary>
    public int FundingAccountId { get; set; }
    public Account? FundingAccount { get; set; }

    /// <summary>Optional monthly discount for paying from a bank account instead of a card (RWD-4a). Stored now, used in Phase 3.</summary>
    public decimal? BankAutopayDiscount { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public List<BillPeriod> Periods { get; set; } = [];
}

/// <summary>
/// One bill in one month (BIL-3). Period is the first day of the month. Holds the actual amount
/// plus optional per-month overrides, because real due dates and amounts drift month to month:
/// a null override means "use the bill's default". A row may exist with only an override and no actual.
/// </summary>
public class BillPeriod
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public Bill? Bill { get; set; }
    public DateOnly Period { get; set; }
    /// <summary>This month's due date when it differs from the bill's due day.</summary>
    public DateOnly? DueDate { get; set; }
    /// <summary>This month's expected amount when it differs from the bill's projected amount.</summary>
    public decimal? ProjectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Notes { get; set; }
}

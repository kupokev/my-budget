namespace MyBudget.Domain;

/// <summary>A spending bucket: a utility tracked individually, or a variable category like Restaurants (BIL-8).</summary>
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>False for categories that can never go on a card (mortgage, car payment).</summary>
    public bool IsCardEligible { get; set; } = true;
}

/// <summary>A recurring or one-off obligation (BIL-1, BIL-2).</summary>
public class BudgetLine
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    /// <summary>Optionally narrows the line to one merchant, so Amazon can be budgeted apart from the rest of its category.</summary>
    public int? LabelId { get; set; }
    public Label? Label { get; set; }
    /// <summary>The biller's account / membership / policy number (ADR-0008).</summary>
    public string? AccountNumber { get; set; }
    public BudgetFrequency Frequency { get; set; } = BudgetFrequency.Monthly;
    /// <summary>Day of month the line is due (1–31; 31 means last day).</summary>
    public int DueDay { get; set; }
    /// <summary>
    /// For non-monthly lines: a known due date from which the others are derived (e.g. car insurance
    /// due 2026-03-15 every six months). For one-off lines: the single due date.
    /// </summary>
    public DateOnly? AnchorDueDate { get; set; }
    public bool IsAutopay { get; set; }
    public decimal ProjectedAmount { get; set; }

    /// <summary>What pays the line: a bank account or a card (BIL-2).</summary>
    public PaymentMethodKind PaymentMethod { get; set; } = PaymentMethodKind.Account;
    public int? PaymentAccountId { get; set; }
    public Account? PaymentAccount { get; set; }
    public int? PaymentCardId { get; set; }
    public Card? PaymentCard { get; set; }

    /// <summary>
    /// Where the money really comes from, always set. For a card-paid line this is the account the
    /// cost rolls up under for transfer needs (BIL-2, ACC-2a), not the account that pays the card.
    /// </summary>
    public int FundingAccountId { get; set; }
    public Account? FundingAccount { get; set; }

    /// <summary>Optional monthly discount for paying from a bank account instead of a card (RWD-4a).</summary>
    public decimal? BankAutopayDiscount { get; set; }
    /// <summary>False when the biller won't take a card (mortgage, HELOC, car loan).</summary>
    public bool IsCardEligible { get; set; } = true;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public List<BudgetPeriod> Periods { get; set; } = [];
}

/// <summary>
/// One line in one month (BIL-3). Period is the first day of the month. Holds the actual amount
/// plus optional per-month overrides, because real due dates and amounts drift month to month:
/// a null override means "use the budget line's default". A row may exist with only an override and no actual.
/// </summary>
public class BudgetPeriod
{
    public int Id { get; set; }
    public int BudgetLineId { get; set; }
    public BudgetLine? BudgetLine { get; set; }
    public DateOnly Period { get; set; }
    /// <summary>This month's due date when it differs from the line's due day.</summary>
    public DateOnly? DueDate { get; set; }
    /// <summary>This month's expected amount when it differs from the line's projected amount.</summary>
    public decimal? ProjectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Notes { get; set; }
}

namespace MyBudget.Domain;

/// <summary>A credit card (CC-1). Rewards fields come in Phase 3 on separate entities.</summary>
public class Card
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>
    /// A short name for tables. Card names as the issuer writes them — "WELLS FARGO CASH WISE VISA
    /// PLATINUM® CARD" — blow out every column they appear in. The full name stays for the places
    /// that identify the card; this is what gets shown in a list.
    /// </summary>
    public string? Nickname { get; set; }

    public string? Issuer { get; set; }
    public string? Network { get; set; }
    public string? AccountNumber { get; set; }
    public decimal Apr { get; set; }
    public decimal? PromoApr { get; set; }
    public DateOnly? PromoAprExpires { get; set; }
    /// <summary>Day of month the statement closes and the balance is reported.</summary>
    public int StatementDay { get; set; }
    public int DueDay { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal AnnualFee { get; set; }
    /// <summary>1–12; month the annual fee posts. Null when there is no fee.</summary>
    public int? AnnualFeeMonth { get; set; }

    /// <summary>When the card was opened, so a waived first year can be told from a free card.</summary>
    public DateOnly? OpenedOn { get; set; }

    /// <summary>
    /// What the fee was in past years, when it differed. <see cref="AnnualFee"/> is only a fallback for
    /// years this does not cover: an issuer waives the first year and raises the fee later, and editing
    /// a single number would rewrite what a card cost you in a year that has already been and gone.
    /// </summary>
    public List<CardFee> Fees { get; set; } = [];

    /// <summary>
    /// The fee charged in a year: the latest schedule row starting on or before it, or the plain
    /// annual fee when no rows have been entered.
    /// </summary>
    public decimal FeeFor(int year)
    {
        var row = Fees.Where(f => f.FromYear <= year).OrderByDescending(f => f.FromYear).FirstOrDefault();
        return row?.Amount ?? AnnualFee;
    }
    /// <summary>
    /// Keep the annual fee as a budget line. Off by default: a fee is money you will spend, but no row
    /// should appear in someone's budget without them asking for it. Deleting the line turns this back
    /// off, so the tick always reflects what is actually there.
    /// </summary>
    public bool BudgetAnnualFee { get; set; }

    /// <summary>The budget line created for the annual fee, so it can be kept in step or removed.</summary>
    public int? FeeBudgetLineId { get; set; }
    public BudgetLine? FeeBudgetLine { get; set; }

    /// <summary>The bank account that pays this card's statement.</summary>
    public int? PayingAccountId { get; set; }
    public Account? PayingAccount { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Co-branded program whose points this card earns, if any.</summary>
    public int? LoyaltyProgramId { get; set; }
    public LoyaltyProgram? LoyaltyProgram { get; set; }
    /// <summary>Cents per point for this card's currency when it isn't a program (cash back = 1.0); overrides the program's value if set.</summary>
    public decimal? PointValueCents { get; set; }
    public List<CardBalance> Balances { get; set; } = [];
    public List<EarnRule> EarnRules { get; set; } = [];
    public List<SpendThreshold> Thresholds { get; set; } = [];
    public List<CardPerk> Perks { get; set; } = [];
}

/// <summary>Statement or month-end balance snapshot (CC-3, CC-4).</summary>
public class CardBalance
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// The annual fee as it applied from a given year onward, until another row supersedes it. A waived
/// first year is a row of zero; a rise is a new row. Years before the earliest row fall back to the
/// card's current fee.
/// </summary>
public class CardFee
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }

    /// <summary>First year this amount was charged.</summary>
    public int FromYear { get; set; }

    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

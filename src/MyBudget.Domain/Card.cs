namespace MyBudget.Domain;

/// <summary>A credit card (CC-1). Rewards fields come in Phase 3 on separate entities.</summary>
public class Card
{
    public int Id { get; set; }
    public required string Name { get; set; }
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

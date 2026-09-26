namespace MyBudget.Domain;

/// <summary>How a loyalty tier can be earned in a year.</summary>
public enum StatusPathKind
{
    /// <summary>Calendar-year spend on a specific co-branded card.</summary>
    CardSpend,
    /// <summary>Granted just for holding a specific card.</summary>
    HoldCard,
    Nights,
    Stays,
    /// <summary>Eligible spend with the program itself (hotel folios).</summary>
    ProgramSpend,
    QualifyingPoints,
}

public enum ThresholdRewardKind { Status, FreeNight, Credit, BonusPoints, Other }

/// <summary>A hotel/airline program (IHG One Rewards, Hilton Honors). Priority orders the optimizer: 1 first (RWD-2, RWD-4).</summary>
public class LoyaltyProgram
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>What you value one point at, in cents (0.5 = half a cent). Overridable per card.</summary>
    public decimal PointValueCents { get; set; } = 1.0m;
    public decimal PointsBalance { get; set; }
    public string? CurrentTier { get; set; }
    /// <summary>The tier the optimizer should plan for; null = no status goal in this program.</summary>
    public string? TargetTier { get; set; }
    public int Priority { get; set; } = 99;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<LoyaltyTier> Tiers { get; set; } = [];
    public List<StatusPath> Paths { get; set; } = [];
    public List<LoyaltyProgress> Progress { get; set; } = [];
}

public class LoyaltyTier
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public LoyaltyProgram? Program { get; set; }
    public required string Name { get; set; }
    /// <summary>Higher is better.</summary>
    public int Rank { get; set; }
}

/// <summary>One way to reach a tier (RWD-2): e.g. Diamond via $40,000 on the IHG Premier, or via 70 nights.</summary>
public class StatusPath
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public LoyaltyProgram? Program { get; set; }
    public required string TierName { get; set; }
    public StatusPathKind Kind { get; set; }
    public decimal Threshold { get; set; }
    /// <summary>For CardSpend and HoldCard: which card.</summary>
    public int? CardId { get; set; }
    public Card? Card { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Year-to-date qualifying activity entered by hand (nights, stays, program spend, qualifying points).</summary>
public class LoyaltyProgress
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public LoyaltyProgram? Program { get; set; }
    public int Year { get; set; }
    public int Nights { get; set; }
    public int Stays { get; set; }
    public decimal ProgramSpend { get; set; }
    public decimal QualifyingPoints { get; set; }
}

/// <summary>Points per dollar on a card for a category (RWD-1). CategoryId null = the base rate for everything else.</summary>
public class EarnRule
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal PointsPerDollar { get; set; }
    /// <summary>Annual spend cap for this rate, if any; spend past it earns the base rate (not enforced in v1, shown as a note).</summary>
    public decimal? AnnualSpendCap { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A calendar-year spend threshold on a card and what it unlocks (RWD-1): free night at $15K, Diamond at $40K, $100 credit at $20K.</summary>
public class SpendThreshold
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public decimal Amount { get; set; }
    public ThresholdRewardKind RewardKind { get; set; }
    public required string Description { get; set; }
    /// <summary>What the reward is worth to you in dollars, for net-value math.</summary>
    public decimal? ValueDollars { get; set; }
    /// <summary>For Status rewards: the tier granted (matches a StatusPath of kind CardSpend).</summary>
    public string? TierName { get; set; }
}

/// <summary>Actual spend on a card in a month, by category, entered from statements until import exists (RWD-3, RWD-5).</summary>
public class CardSpend
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public DateOnly Period { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

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
    /// <summary>What holding this tier gets you: breakfast, upgrades, lounge access, late checkout.</summary>
    public string? Benefits { get; set; }
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
    /// <summary>Years this path was offered; null means always. Programs change their qualification rules.</summary>
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }

    public bool AppliesIn(int year) => (StartYear is null || year >= StartYear) && (EndYear is null || year <= EndYear);
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
    /// <summary>Narrows the rule to purchases carrying this label. Category + label beats label, which beats category, which beats the base rate.</summary>
    public int? LabelId { get; set; }
    public Label? Label { get; set; }
    public decimal PointsPerDollar { get; set; }
    /// <summary>Annual spend cap for this rate, if any; spend past it earns the base rate (not enforced in v1, shown as a note).</summary>
    public decimal? AnnualSpendCap { get; set; }
    /// <summary>Years this rate was in force; null means always. Issuers change earn rates, so old years keep their own numbers.</summary>
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }

    public bool AppliesIn(int year) => (StartYear is null || year >= StartYear) && (EndYear is null || year <= EndYear);
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
    /// <summary>Years this threshold was offered; null means always.</summary>
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }

    public bool AppliesIn(int year) => (StartYear is null || year >= StartYear) && (EndYear is null || year <= EndYear);
}

/// <summary>
/// Something a card gives you for simply holding it, with no spend threshold and no loyalty program:
/// a TSA PreCheck or Global Entry credit, a travel credit, free checked bags. Counted in the card's
/// yearly value so the annual fee can be judged against it.
/// </summary>
public class CardPerk
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public required string Description { get; set; }

    /// <summary>
    /// What it is worth over a year without doing anything — a credit that simply arrives. Leave it at
    /// zero for a perk you have to use, and set <see cref="ValuePerUse"/> instead.
    /// </summary>
    public decimal AnnualValue { get; set; }

    /// <summary>
    /// What one use is worth: a checked bag on a round trip, a night on points, a quarterly credit
    /// claimed. A perk counted whether or not it was used flatters the card — a $50 credit you forget
    /// to spend is a fee you paid for nothing — so these only count once they are logged.
    /// </summary>
    public decimal ValuePerUse { get; set; }

    /// <summary>The window <see cref="MaxUsesPerPeriod"/> applies over.</summary>
    public PerkPeriod Period { get; set; } = PerkPeriod.Year;

    /// <summary>How many uses can count in a period; null means as many as you log.</summary>
    public int? MaxUsesPerPeriod { get; set; }

    /// <summary>Years the perk was offered; null means always.</summary>
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }

    public List<CardPerkUse> Uses { get; set; } = [];

    public bool AppliesIn(int year) => (StartYear is null || year >= StartYear) && (EndYear is null || year <= EndYear);

    /// <summary>True when the perk has to be used to be worth anything.</summary>
    public bool IsEarned => ValuePerUse > 0;

    /// <summary>
    /// What the perk actually returned in a year: the flat value, plus each period's uses capped and
    /// priced. A quarterly credit missed in Q1 cannot be claimed twice in Q2, which is why the cap is
    /// applied per period rather than over the year.
    /// </summary>
    public decimal ValueIn(int year)
    {
        if (!AppliesIn(year)) return 0m;
        if (!IsEarned) return AnnualValue;

        var earned = 0m;
        foreach (var period in Uses.Where(u => u.Date.Year == year).GroupBy(u => PeriodKey(u.Date)))
        {
            var counted = MaxUsesPerPeriod is { } cap ? Math.Min(cap, period.Count()) : period.Count();
            earned += counted * ValuePerUse;
        }
        return AnnualValue + earned;
    }

    /// <summary>Uses that counted in the period containing <paramref name="on"/>, and the cap if any.</summary>
    public (int Used, int? Cap) UsesIn(DateOnly on)
    {
        var key = PeriodKey(on);
        var used = Uses.Count(u => u.Date.Year == on.Year && PeriodKey(u.Date) == key);
        return (used, MaxUsesPerPeriod);
    }

    private int PeriodKey(DateOnly date) => Period == PerkPeriod.Quarter ? (date.Month - 1) / 3 : 0;
}

/// <summary>The window a perk's cap applies over.</summary>
public enum PerkPeriod { Year, Quarter }

/// <summary>One time a perk was actually used: a bag checked, a reward night taken, a credit claimed.</summary>
public class CardPerkUse
{
    public int Id { get; set; }
    public int CardPerkId { get; set; }
    public CardPerk? Perk { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
}

/// <summary>Actual spend on a card in a month, by category and label (RWD-3, RWD-5).
/// Derived from transactions on read, never stored: a stored copy only an import refreshed left
/// hand-entered card transactions out of the rewards figures.</summary>
public class CardSpend
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public DateOnly Period { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? LabelId { get; set; }
    public Label? Label { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

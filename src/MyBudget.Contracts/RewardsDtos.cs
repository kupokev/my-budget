using System.ComponentModel.DataAnnotations;
using MyBudget.Domain;

namespace MyBudget.Contracts;

// ---- Editable data -------------------------------------------------------------------------

public sealed class EarnRuleDto
{
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    /// <summary>Narrows the rule to purchases carrying this label.</summary>
    public int? LabelId { get; set; }
    [Range(0, 100)] public decimal PointsPerDollar { get; set; } = 1;
    public decimal? AnnualSpendCap { get; set; }
    /// <summary>Years this rate applies; blank means always.</summary>
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }
}

public sealed class SpendThresholdDto
{
    public int Id { get; set; }
    [Range(0, 10_000_000)] public decimal Amount { get; set; }
    public ThresholdRewardKind RewardKind { get; set; }
    [Required, StringLength(200)] public string Description { get; set; } = "";
    public decimal? ValueDollars { get; set; }
    public string? TierName { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
}

public sealed class CardPerkDto
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Description { get; set; } = "";
    /// <summary>Worth over a year without doing anything. Zero for a perk you have to use.</summary>
    [Range(0, 100_000)] public decimal AnnualValue { get; set; }

    /// <summary>What one use is worth — a checked bag, a reward night, a credit claimed.</summary>
    [Range(0, 100_000)] public decimal ValuePerUse { get; set; }

    public PerkPeriod Period { get; set; } = PerkPeriod.Year;

    /// <summary>Uses that can count in a period; null means as many as you log.</summary>
    [Range(1, 366)] public int? MaxUsesPerPeriod { get; set; }

    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }

    /// <summary>Logged uses, newest first.</summary>
    public List<CardPerkUseDto> Uses { get; set; } = [];

    /// <summary>What it has actually returned this year, and where the current period stands.</summary>
    public decimal ValueThisYear { get; set; }
    public int UsedThisPeriod { get; set; }
}

public sealed class CardPerkUseDto
{
    public int Id { get; set; }
    public int CardPerkId { get; set; }
    public DateOnly Date { get; set; }
    [StringLength(200)] public string? Note { get; set; }
}

/// <summary>Rewards side of a card, edited separately from the CC-1 basics.</summary>
public sealed class CardRewardsDto
{
    public int CardId { get; set; }
    public int? LoyaltyProgramId { get; set; }
    public decimal? PointValueCents { get; set; }
    public List<EarnRuleDto> EarnRules { get; set; } = [];
    public List<SpendThresholdDto> Thresholds { get; set; } = [];
    public List<CardPerkDto> Perks { get; set; } = [];
}

public sealed class StatusPathDto
{
    public int Id { get; set; }
    [Required] public string TierName { get; set; } = "";
    public StatusPathKind Kind { get; set; }
    public decimal Threshold { get; set; }
    public int? CardId { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }
}

public sealed class LoyaltyTierDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Benefits { get; set; }
}

public sealed class LoyaltyProgressDto
{
    public int Year { get; set; }
    public int Nights { get; set; }
    public int Stays { get; set; }
    public decimal ProgramSpend { get; set; }
    public decimal QualifyingPoints { get; set; }
}

public sealed class LoyaltyProgramDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(0, 100)] public decimal PointValueCents { get; set; } = 1;
    public decimal PointsBalance { get; set; }
    public string? CurrentTier { get; set; }
    public string? TargetTier { get; set; }
    public int Priority { get; set; } = 99;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    /// <summary>The tier ladder in ascending order, each with what it gets you.</summary>
    public List<LoyaltyTierDto> Tiers { get; set; } = [];
    public List<StatusPathDto> Paths { get; set; } = [];
    public List<LoyaltyProgressDto> Progress { get; set; } = [];
}

public sealed class CardSpendDto
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public DateOnly Period { get; set; }
    public int? CategoryId { get; set; }
    public int? LabelId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

// ---- Optimizer report (RWD-3..6). Every number carries its formula. --------------------------

public sealed record ThresholdProgressDto(int CardId, string CardName, int ThresholdId, decimal Amount, ThresholdRewardKind Kind, string Description,
    decimal YtdSpend, decimal Remaining, decimal RequiredMonthly, decimal ProjectedYearEnd, bool Reached, bool OnPace, string Formula);

public sealed record PathProgressDto(int PathId, string TierName, StatusPathKind Kind, decimal Threshold, decimal Current, decimal Remaining,
    decimal? RequiredMonthly, bool Reached, string? CardName, string Formula);

public sealed record ProgramStatusDto(int ProgramId, string Name, int Priority, string? CurrentTier, string? TargetTier, string? HeldTier,
    bool TargetReached, string HowReached, PathProgressDto? PlannedPath, IReadOnlyList<PathProgressDto> Paths, decimal PointsBalance, decimal PointsValueDollars,
    IReadOnlyList<LoyaltyTierDto> Tiers);

public sealed record AllocationDto(int CardId, string CardName, decimal Monthly, string Reason);

public sealed record GapDto(string Program, string Tier, decimal RequiredMonthly, decimal AllocatedMonthly, decimal ShortfallMonthly, IReadOnlyList<string> Alternatives);

public sealed record CategoryRouteDto(int? CategoryId, int? LabelId, string Category, decimal Monthly, int? CardId, string CardName, decimal PointsPerDollar, decimal CentsPerDollar, string Reason);

public sealed record SpendPlanDto(int Year, DateOnly AsOf, int MonthsLeft, decimal ProjectedMonthly, string ProjectedMonthlySource,
    IReadOnlyList<AllocationDto> Allocations, IReadOnlyList<CategoryRouteDto> Routing, IReadOnlyList<GapDto> Gaps, IReadOnlyList<string> Steps);

public sealed record BudgetRecommendationDto(int BudgetLineId, string LineName, decimal Monthly, int? CardId, string Recommendation, decimal CardValue, decimal BankDiscount, string Formula);

public sealed record MonthEarningsDto(DateOnly Period, decimal Spend, decimal Points, decimal Dollars);

public sealed record CardEarningsDto(int CardId, string CardName, decimal PointValueCents, IReadOnlyList<MonthEarningsDto> Months,
    decimal YtdSpend, decimal YtdPoints, decimal YtdDollars, decimal AnnualFee, decimal ThresholdRewardsValue, decimal PerksValue, decimal NetValue, string Formula);

public sealed record RewardsReportDto(int Year, DateOnly AsOf, IReadOnlyList<ThresholdProgressDto> Thresholds, IReadOnlyList<ProgramStatusDto> Programs,
    SpendPlanDto Plan, IReadOnlyList<BudgetRecommendationDto> BudgetLines, IReadOnlyList<CardEarningsDto> Earnings, IReadOnlyList<string> Warnings);

using System.ComponentModel.DataAnnotations;
using MyBudget.Domain;

namespace MyBudget.Contracts;

// ---- Investments (INV-1..5) ------------------------------------------------------------------

public sealed class HoldingDto
{
    public int Id { get; set; }
    [Required, StringLength(12)] public string Ticker { get; set; } = "";
    public string? Name { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public bool Drip { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public sealed class TradeDto
{
    public int Id { get; set; }
    public int HoldingId { get; set; }
    public DateOnly Date { get; set; }
    public TradeKind Kind { get; set; }
    [Range(0.000001, 1_000_000_000)] public decimal Shares { get; set; }
    [Range(0, 1_000_000)] public decimal Price { get; set; }
    public decimal Fees { get; set; }
    public string? Notes { get; set; }
    public int? DividendPaymentId { get; set; }
}

public sealed class DividendDto
{
    public int Id { get; set; }
    public int HoldingId { get; set; }
    public DateOnly ExDate { get; set; }
    public DateOnly? PayDate { get; set; }
    public decimal PerShare { get; set; }
    public decimal SharesHeld { get; set; }
    public decimal Amount { get; set; }
    public bool Reinvested { get; set; }
    public DataSource Source { get; set; }
}

public sealed record LotDto(int TradeId, DateOnly Acquired, decimal Shares, decimal CostPerShare, decimal RemainingShares, bool FromReinvest, decimal DisallowedLossAdded);
public sealed record RealizedGainDto(int SellTradeId, DateOnly SellDate, DateOnly Acquired, decimal Shares, decimal Proceeds, decimal CostBasis, decimal Gain, string Term, int DaysHeld, bool WashSale, decimal DisallowedLoss, string Formula);
public sealed record WashSaleDto(int SellTradeId, DateOnly SellDate, decimal Loss, DateOnly WindowOpens, DateOnly WindowCloses, DateOnly EarliestSafeRepurchase, decimal DisallowedLoss, bool WindowStillOpen, string Message);

public sealed record PositionDto(HoldingDto Holding, bool TaxAdvantaged, decimal Shares, decimal CostBasis, decimal? Price, DateOnly? PriceDate, decimal? MarketValue, decimal? UnrealizedGain, decimal DividendsThisYear,
    IReadOnlyList<LotDto> Lots, IReadOnlyList<TradeDto> Trades, IReadOnlyList<DividendDto> Dividends, IReadOnlyList<RealizedGainDto> Realized, IReadOnlyList<WashSaleDto> WashSales);

public sealed record GainsTaxDto(decimal ShortTermGain, decimal LongTermGain, decimal ShortTermTax, decimal LongTermTax, decimal MissouriTax, decimal Total, decimal OrdinaryMarginalRate, IReadOnlyList<string> Steps);

public sealed record PortfolioDto(DateOnly AsOf, int Year, IReadOnlyList<PositionDto> Positions, decimal TotalValue, decimal TotalCost, decimal TotalUnrealized, decimal DividendsThisYear,
    decimal RealizedShortTerm, decimal RealizedLongTerm, GainsTaxDto? Tax, IReadOnlyList<string> Warnings);

public sealed record MarketSyncResultDto(string Ticker, int PricesAdded, int DividendsAdded, int ReinvestsCreated, decimal? LastPrice, string? Error);

public sealed record LotImportResultDto(string AccountName, int HoldingsCreated, int LotsImported, int LotsAlreadyPresent, int PricesRecorded, IReadOnlyList<string> Tickers, IReadOnlyList<string> Skipped, IReadOnlyList<string> Warnings);

// ---- Assets (ACC-4a) --------------------------------------------------------------------------

public sealed class AssetDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public AssetKind Kind { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? LatestValue { get; set; }
    public DateOnly? LatestAsOf { get; set; }

    /// <summary>Every recorded value, newest first, each with the change from the one before it.</summary>
    public List<AssetValuePointDto> History { get; set; } = [];
    /// <summary>Loans secured against this asset, with their latest balances.</summary>
    public List<AssetLoanDto> Loans { get; set; } = [];
    /// <summary>Sum of the attached loans' latest balances.</summary>
    public decimal LoanBalance { get; set; }
    /// <summary>Latest value less what is still owed against it. Null until a value has been recorded.</summary>
    public decimal? Equity { get; set; }
    /// <summary>How the equity figure was reached.</summary>
    public string? EquityFormula { get; set; }
    /// <summary>Change since the previous recorded value, and since roughly a year before the latest one.</summary>
    public decimal? ChangeSincePrior { get; set; }
    public decimal? ChangeOverYear { get; set; }
}

/// <summary>One recorded valuation, with the move from the previous record.</summary>
public sealed record AssetValuePointDto(int Id, DateOnly AsOf, decimal Value, decimal? Change, decimal? ChangePercent);

public sealed record AssetLoanDto(int LoanId, string Name, LoanKind Kind, decimal Balance, DateOnly? BalanceAsOf);

public sealed class AssetValueDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Value { get; set; }
}

// ---- Receivables (DBT-2) -----------------------------------------------------------------------

public sealed class ObligationDto
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Description { get; set; } = "";
    public decimal? MonthlyAmount { get; set; }
    public int? BudgetLineId { get; set; }
    public decimal ShareOfLine { get; set; } = 1.0m;
    public DateOnly StartPeriod { get; set; }
    public DateOnly? EndPeriod { get; set; }
    /// <summary>1 = every month, 3 = quarterly, 6 = twice a year, 12 = yearly.</summary>
    [Range(1, 24)] public int EveryMonths { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public sealed class ReceivableChargeDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    [Required, StringLength(200)] public string Description { get; set; } = "";
}

public sealed class PaymentAllocationDto
{
    public DateOnly? Period { get; set; }
    public decimal Amount { get; set; }
}

public sealed class ReceivablePaymentDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public List<PaymentAllocationDto> Allocations { get; set; } = [];
}

public sealed class PersonDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ObligationDto> Obligations { get; set; } = [];
    public List<ReceivableChargeDto> Charges { get; set; } = [];
    public List<ReceivablePaymentDto> Payments { get; set; } = [];
}

public sealed record PeriodRowDto(DateOnly Period, decimal Expected, decimal Paid, decimal Balance, string Status, string Detail);

public sealed record PersonLedgerDto(PersonDto Person, IReadOnlyList<PeriodRowDto> Periods, decimal OneOffCharged, decimal OneOffPaid, decimal OneOffBalance,
    decimal TotalExpected, decimal TotalPaid, decimal TotalOwed, decimal Unallocated, IReadOnlyList<string> Steps);

// ---- 1099 (INC-8) ------------------------------------------------------------------------------

public sealed class IncomeReceiptDto
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class EstimatedTaxPaymentDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public Jurisdiction Jurisdiction { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed record SourceYtdDto(int IncomeSourceId, string Name, decimal YearToDate);
public sealed record QuarterlyPaymentDto(int Quarter, DateOnly DueDate, decimal Federal, decimal Missouri, bool Past);

public sealed record SelfEmploymentDto(int Year, DateOnly AsOf, IReadOnlyList<SourceYtdDto> Sources, decimal NetProfitYtd, decimal ProjectedAnnual, string ProjectionMethod,
    decimal W2Wages, decimal FederalMarginalRate, decimal MissouriRate,
    decimal SeEarnings, decimal SocialSecurityTax, decimal MedicareTax, decimal SelfEmploymentTax, decimal HalfSeDeduction, decimal FederalIncomeTax, decimal MissouriIncomeTax,
    decimal TotalTax, decimal SetAsideFraction, decimal FederalPaid, decimal MissouriPaid, decimal FederalRemaining, decimal MissouriRemaining,
    IReadOnlyList<QuarterlyPaymentDto> Schedule, IReadOnlyList<string> Steps, IReadOnlyList<string> Warnings);

// ---- Alerts (ALT-1..6) and home (HOME-1) ---------------------------------------------------------

public enum AlertSeverity { Info, Warning, Danger }

public sealed record AlertDto(string Kind, AlertSeverity Severity, string Title, string Detail, string? Link);

/// <summary>Status: Low = below the 3-month minimum (red); Marginal = within 25% above it (yellow); Healthy = more than 25% above (green).</summary>
public sealed record RainyDayDto(decimal MonthlyExpenses, string ExpensesFormula, decimal Balance, IReadOnlyList<string> Accounts, decimal MonthsCovered, decimal LowTarget, decimal HighTarget, decimal ComfortTarget, string Status, string Verdict);

public sealed record HomeDashboardDto(
    DateOnly AsOf, IReadOnlyList<AlertDto> Alerts,
    IReadOnlyList<UpcomingLineDto> UpcomingLines, PayDateDto? NextPayDate, IReadOnlyList<AccountNeedDto> Needs,
    decimal SpendingThisMonth, decimal SpendingLastMonth, IReadOnlyList<SpendingCategoryDto> TopCategories, int Uncategorized,
    IReadOnlyList<ProgramStatusDto> Programs, IReadOnlyList<GoalProgressDto> Goals,
    decimal NetWorth, decimal? NetWorthChange, RainyDayDto RainyDay, bool AiEnabled);

// ---- Local AI (AI-1, AI-2) ----------------------------------------------------------------------

public sealed record AiStatusDto(bool Enabled, string? BaseUrl, string? Model, IReadOnlyList<string> Tools, bool Reachable, string? Error);

public sealed class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public sealed class ChatRequest
{
    public List<ChatMessageDto> Messages { get; set; } = [];
}

public sealed record ToolCallDto(string Name, string Arguments, string Result);
public sealed record ChatResponseDto(string Reply, IReadOnlyList<ToolCallDto> ToolCalls, string Model);
public sealed record AiSummaryDto(int Year, int Month, string Narrative, IReadOnlyList<ToolCallDto> ToolCalls, string Model);

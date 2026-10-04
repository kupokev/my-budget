using System.ComponentModel.DataAnnotations;
using MyBudget.Domain;

namespace MyBudget.Contracts;

// ---- Import (BIL-7) ------------------------------------------------------------------------

public sealed record ImportProfileDto(string Key, string Name, string? Notes);

public sealed class ImportRowDto
{
    public DateOnly Date { get; set; }
    public DateOnly? PostedDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string? Merchant { get; set; }
    public string ExternalId { get; set; } = "";
    public string? SourceCategory { get; set; }
    public string? Memo { get; set; }
    public bool IsDuplicate { get; set; }
    public int? CategoryId { get; set; }
    public int? LabelId { get; set; }
    public int? BudgetLineId { get; set; }
    public bool IsTransfer { get; set; }
    /// <summary>Set when this row is pay landing in an account rather than spending.</summary>
    public int? IncomeSourceId { get; set; }
    /// <summary>Why the category/line/transfer flag was suggested (rule, source category, line name).</summary>
    public string? SuggestionSource { get; set; }
    public bool Skip { get; set; }
}

public sealed record ImportPreviewDto(string Profile, string ProfileName, int? AccountId, int? CardId, string SourceName,
    IReadOnlyList<ImportRowDto> Rows, int NewCount, int DuplicateCount, DateOnly? FirstDate, DateOnly? LastDate, IReadOnlyList<string> Warnings);

public sealed class ImportCommitRequest
{
    [Required] public string FileName { get; set; } = "";
    public string Profile { get; set; } = "";
    public int? AccountId { get; set; }
    public int? CardId { get; set; }
    public List<ImportRowDto> Rows { get; set; } = [];
}

public sealed record ImportResultDto(int BatchId, int Imported, int Duplicates, int Skipped, int BudgetMonthsUpdated, int Reconciled);

public sealed record ImportBatchDto(int Id, string FileName, ImportFormat Format, string? Profile, DateTime ImportedAt, string SourceName,
    int RowCount, int ImportedCount, int DuplicateCount, DateOnly? FirstDate, DateOnly? LastDate);

// ---- Transactions (BIL-8/9) ------------------------------------------------------------------

public sealed record TransactionDto(int Id, int? AccountId, int? CardId, string SourceName, DateOnly Date, DateOnly? PostedDate, decimal Amount,
    string Description, string? Merchant, int? CategoryId, string? CategoryName, int? BudgetLineId, string? LineName, bool IsTransfer, string? Notes, bool IsManuallyCategorized,
    TransactionOrigin Origin, string? CounterpartyName, int? ReconciledWithId, string? ReconciledWithSummary, int? RepaymentFromPersonId, string? RepaymentFromPersonName,
    int? LabelId, string? LabelName, int? IncomeSourceId = null, string? IncomeSourceName = null);

public sealed record ReconcileCandidateDto(TransactionDto Transaction, int DaysApart, decimal AmountDifference);

/// <summary>
/// A transaction entered by hand. Exactly one of <see cref="AccountId"/> or <see cref="CardId"/> is set:
/// money moves out of an account or onto a card. Setting <see cref="CounterpartyAccountId"/> makes it a
/// transfer and creates the mirror row on the other account.
/// </summary>
public sealed class TransactionCreateDto
{
    public int? AccountId { get; set; }
    public int? CardId { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>Negative for money out, positive for money in.</summary>
    public decimal Amount { get; set; }
    [Required, StringLength(200)] public string Description { get; set; } = "";
    public int? CategoryId { get; set; }
    public int? LabelId { get; set; }
    public int? BudgetLineId { get; set; }
    public bool IsTransfer { get; set; }
    /// <summary>Pay from this income source; makes the row a deposit rather than spending.</summary>
    public int? IncomeSourceId { get; set; }
    /// <summary>The other account in a transfer; the mirror row is created there automatically.</summary>
    public int? CounterpartyAccountId { get; set; }
    public string? Notes { get; set; }
}

public sealed class TransactionUpdateDto
{
    public int? CategoryId { get; set; }
    public int? LabelId { get; set; }
    public int? BudgetLineId { get; set; }
    public bool IsTransfer { get; set; }
    public int? IncomeSourceId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Money in that repays what this person owes: creates (or removes) the payment on their ledger.</summary>
    public int? RepaymentFromPersonId { get; set; }
    /// <summary>Also create a rule so future lines with this merchant get the same category/line.</summary>
    public bool CreateRule { get; set; }
    public string? RulePattern { get; set; }
    /// <summary>Apply the new rule to existing lines that aren't manually categorized.</summary>
    public bool ApplyRuleToExisting { get; set; } = true;
}

public sealed class CategoryRuleDto
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Pattern { get; set; } = "";
    public RuleMatch Match { get; set; } = RuleMatch.Contains;
    public int? CategoryId { get; set; }
    public int? LabelId { get; set; }
    public int? BudgetLineId { get; set; }
    public bool MarkAsTransfer { get; set; }
    /// <summary>Tag matches as pay from this income source.</summary>
    public int? IncomeSourceId { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

// ---- Spending (RPT-0, BIL-8, BIL-9) --------------------------------------------------------

public sealed record SpendingCategoryDto(int? CategoryId, string Name, decimal ThisMonth, decimal LastMonth, decimal Delta, decimal YearToDate, decimal MonthlyAverage, int Count);

public sealed record SpendingSummaryDto(int Year, int Month, decimal ThisMonth, decimal LastMonth, decimal Delta, decimal YearToDate,
    IReadOnlyList<SpendingCategoryDto> Categories, int UncategorizedCount, decimal UncategorizedAmount, decimal IncomeThisMonth);

/// <summary>
/// What the Budget grid records as paid, run up day by day, this month against last, for the dashboard
/// chart. Both series are cumulative, so each day's figure includes everything paid before it that month.
/// </summary>
public sealed record CumulativeSpendDto(
    int Year, int Month, DateOnly AsOf, int DayOfMonth,
    IReadOnlyList<string> Days,
    /// <summary>Cumulative spend for each day so far this month; stops at today rather than flat-lining to month end.</summary>
    IReadOnlyList<decimal> ThisMonth,
    /// <summary>The whole of last month, for comparison past today's day-of-month.</summary>
    IReadOnlyList<decimal> LastMonth,
    string ThisMonthLabel, string LastMonthLabel,
    decimal SpentThisWeek, decimal ThisMonthToDate, decimal LastMonthToSameDay, decimal Difference,
    string Summary);

public sealed record SpendingRowDto(int? CategoryId, string Name, IReadOnlyList<decimal> Months, decimal Total);

public sealed record SpendingMatrixDto(int Year, IReadOnlyList<SpendingRowDto> Rows, IReadOnlyList<decimal> MonthTotals, decimal Total);

public sealed record MerchantTotalDto(string Merchant, int Count, decimal Total);

public sealed record CategoryDrilldownDto(int? CategoryId, string Name, int Year, int? Month, decimal Total, IReadOnlyList<MerchantTotalDto> Merchants, IReadOnlyList<TransactionDto> Transactions);

// ---- Goals (GOL-1..3) -----------------------------------------------------------------------

public sealed class GoalDto
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    public GoalKind Kind { get; set; }
    public GoalMetric Metric { get; set; } = GoalMetric.Manual;
    public decimal TargetAmount { get; set; }
    public decimal? StartValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal? ManualCurrent { get; set; }
    public List<int> AccountIds { get; set; } = [];
    public AccountType? AccountType { get; set; }
    public int? CategoryId { get; set; }
    public int? LoanId { get; set; }
    public bool LowerIsBetter { get; set; }
    /// <summary>Time off this goal needs — a trip, say: from which bucket, how many hours, starting when. All or none.</summary>
    public int? TimeOffBucketId { get; set; }
    [Range(0, 2000)] public decimal? TimeOffHours { get; set; }
    public DateOnly? TimeOffStarts { get; set; }
    public GoalStatus Status { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record GoalProgressDto(GoalDto Goal, decimal Current, string CurrentSource, decimal ProratedTarget, decimal ElapsedFraction,
    decimal ProgressFraction, string StatusText, decimal MissingAmount, string Formula);

// ---- Reports (RPT-1, ACC-4) --------------------------------------------------------------------

public sealed record YoyRowDto(string Name, decimal ThisYear, decimal LastYear, decimal Delta, decimal? DeltaPercent, IReadOnlyList<decimal> ThisMonths, IReadOnlyList<decimal> LastMonths);

public sealed record YearOverYearDto(int Year, int PriorYear, IReadOnlyList<YoyRowDto> Categories, IReadOnlyList<YoyRowDto> BudgetLines, decimal CategoriesThisYear, decimal CategoriesLastYear, decimal BudgetThisYear, decimal BudgetLastYear);

public sealed record NetWorthLineDto(string Name, string Kind, decimal Balance, DateOnly? AsOf);

public sealed record NetWorthPointDto(DateOnly Period, decimal Assets, decimal Cards, decimal Loans, decimal Total);

public sealed record NetWorthDto(DateOnly AsOf, decimal Assets, decimal Cards, decimal Loans, decimal Total, IReadOnlyList<NetWorthLineDto> Lines, IReadOnlyList<NetWorthPointDto> History, string Formula);

/// <summary>Which importer a chosen file is for: "Statement" or "TaxLots".</summary>
public sealed record ImportKindDto(string Kind);

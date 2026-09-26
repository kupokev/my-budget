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
    public int? BillId { get; set; }
    public bool IsTransfer { get; set; }
    /// <summary>Why the category/bill/transfer flag was suggested (rule, source category, bill name).</summary>
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

public sealed record ImportResultDto(int BatchId, int Imported, int Duplicates, int Skipped, int BillMonthsUpdated, int CardMonthsUpdated);

public sealed record ImportBatchDto(int Id, string FileName, ImportFormat Format, string? Profile, DateTime ImportedAt, string SourceName,
    int RowCount, int ImportedCount, int DuplicateCount, DateOnly? FirstDate, DateOnly? LastDate);

// ---- Transactions (BIL-8/9) ------------------------------------------------------------------

public sealed record TransactionDto(int Id, int? AccountId, int? CardId, string SourceName, DateOnly Date, DateOnly? PostedDate, decimal Amount,
    string Description, string? Merchant, int? CategoryId, string? CategoryName, int? BillId, string? BillName, bool IsTransfer, string? Notes, bool IsManuallyCategorized);

public sealed class TransactionUpdateDto
{
    public int? CategoryId { get; set; }
    public int? BillId { get; set; }
    public bool IsTransfer { get; set; }
    public string? Notes { get; set; }
    /// <summary>Also create a rule so future lines with this merchant get the same category/bill.</summary>
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
    public int? BillId { get; set; }
    public bool MarkAsTransfer { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

// ---- Spending (RPT-0, BIL-8, BIL-9) --------------------------------------------------------

public sealed record SpendingCategoryDto(int? CategoryId, string Name, decimal ThisMonth, decimal LastMonth, decimal Delta, decimal YearToDate, decimal MonthlyAverage, int Count);

public sealed record SpendingSummaryDto(int Year, int Month, decimal ThisMonth, decimal LastMonth, decimal Delta, decimal YearToDate,
    IReadOnlyList<SpendingCategoryDto> Categories, int UncategorizedCount, decimal UncategorizedAmount, decimal IncomeThisMonth);

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
    public int? CategoryId { get; set; }
    public int? LoanId { get; set; }
    public bool LowerIsBetter { get; set; }
    public GoalStatus Status { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record GoalProgressDto(GoalDto Goal, decimal Current, string CurrentSource, decimal ProratedTarget, decimal ElapsedFraction,
    decimal ProgressFraction, string StatusText, decimal MissingAmount, string Formula);

// ---- Reports (RPT-1, ACC-4) --------------------------------------------------------------------

public sealed record YoyRowDto(string Name, decimal ThisYear, decimal LastYear, decimal Delta, decimal? DeltaPercent, IReadOnlyList<decimal> ThisMonths, IReadOnlyList<decimal> LastMonths);

public sealed record YearOverYearDto(int Year, int PriorYear, IReadOnlyList<YoyRowDto> Categories, IReadOnlyList<YoyRowDto> Bills, decimal CategoriesThisYear, decimal CategoriesLastYear, decimal BillsThisYear, decimal BillsLastYear);

public sealed record NetWorthLineDto(string Name, string Kind, decimal Balance, DateOnly? AsOf);

public sealed record NetWorthPointDto(DateOnly Period, decimal Assets, decimal Cards, decimal Loans, decimal Total);

public sealed record NetWorthDto(DateOnly AsOf, decimal Assets, decimal Cards, decimal Loans, decimal Total, IReadOnlyList<NetWorthLineDto> Lines, IReadOnlyList<NetWorthPointDto> History, string Formula);

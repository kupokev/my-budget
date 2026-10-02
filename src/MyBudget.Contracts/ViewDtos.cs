using MyBudget.Domain;

namespace MyBudget.Contracts;

// Read-only, computed views. Every number carries the formula that produced it (auditability).

public sealed record NeedLineDto(int BudgetLineId, string LineName, BudgetFrequency Frequency, decimal ProjectedAmount, decimal MonthlyAccrual, string Formula, bool PaidByCard);

public sealed record AccountNeedDto(
    int AccountId, string AccountName, TransferCadence Cadence,
    decimal MonthlyNeed, decimal PerPaycheckNeed, string PerPaycheckFormula,
    /// <summary>Transfers actually recorded this month, and the resulting Long/Short (ACC-3).</summary>
    decimal TransferredThisMonth, decimal LongShort,
    IReadOnlyList<NeedLineDto> Lines);

public sealed record TransferNeedsDto(DateOnly AsOf, int PaychecksPerYear, string PaychecksSource, IReadOnlyList<AccountNeedDto> Accounts);

public sealed record PayDateDto(DateOnly Date, string IncomeSource, bool ThirdCheckOfMonth);

public sealed record PayCalendarDto(int Year, IReadOnlyList<PayDateDto> PayDates, IReadOnlyList<string> ThreePaycheckMonths);

/// <summary>
/// A line falling due. <see cref="IsPaid"/> when that month's row on the Budget grid has an actual or a
/// paid-on date; <see cref="PaidOn"/> and <see cref="PaidAmount"/> are what was recorded there.
/// </summary>
public sealed record UpcomingLineDto(int BudgetLineId, string LineName, DateOnly DueDate, decimal Amount, string PaidVia, string FundingAccount, bool IsAutopay,
    DateOnly? PaidOn = null, decimal? PaidAmount = null)
{
    public bool IsPaid => PaidOn is not null || PaidAmount is not null;
}

/// <summary>A line's month in the year grid. DueDate is the override if set, else the generated date, else null when not due.</summary>
public sealed record BudgetMonthDto(
    DateOnly Period, DateOnly? DueDate, bool DueDateIsOverride,
    decimal Projected, bool ProjectedIsOverride,
    decimal? Actual, decimal? Variance, DateOnly? PaidOn, string? Notes,
    string? ConfirmationNumber = null);

/// <summary>
/// What a budget line's spending splits into by label, month by month. A line like "General
/// Merchandise" covers Amazon, Costco and the rest; the split says which of them the money went to.
/// Actuals only — a label has no budget of its own unless it has been given its own line.
/// </summary>
public sealed record BudgetLabelRowDto(int? LabelId, string LabelName, IReadOnlyList<decimal> Months, decimal Total);

public sealed record BudgetLabelBreakdownDto(int BudgetLineId, int Year, IReadOnlyList<BudgetLabelRowDto> Rows);

public sealed record BudgetHistoryDto(int BudgetLineId, string LineName, decimal Projected, decimal? AverageActual, IReadOnlyList<BudgetMonthDto> Months);

public sealed record CardSummaryDto(
    int CardId, string CardName, string? PayingAccount, int StatementDay, int DueDay,
    decimal? LatestBalance, DateOnly? LatestBalanceAsOf, decimal CreditLimit, decimal? Utilization,
    IReadOnlyList<string> BudgetLines, decimal MonthlyBudgetSpend,
    /// <summary>What was actually charged to the card this month, summed from its transactions.</summary>
    decimal SpentThisMonth,
    /// <summary>The same figure for the month before, so the two can be read against each other.</summary>
    decimal SpentLastMonth);

public sealed record HomeDto(
    DateOnly AsOf,
    IReadOnlyList<UpcomingLineDto> UpcomingLines,
    PayDateDto? NextPayDate,
    IReadOnlyList<AccountNeedDto> NeedsThisPayPeriod);

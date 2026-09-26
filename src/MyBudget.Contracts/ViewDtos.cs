using MyBudget.Domain;

namespace MyBudget.Contracts;

// Read-only, computed views. Every number carries the formula that produced it (auditability).

public sealed record NeedLineDto(int BillId, string BillName, BillFrequency Frequency, decimal ProjectedAmount, decimal MonthlyAccrual, string Formula, bool PaidByCard);

public sealed record AccountNeedDto(
    int AccountId, string AccountName, TransferCadence Cadence,
    decimal MonthlyNeed, decimal PerPaycheckNeed, string PerPaycheckFormula,
    /// <summary>Transfers actually recorded this month, and the resulting Long/Short (ACC-3).</summary>
    decimal TransferredThisMonth, decimal LongShort,
    IReadOnlyList<NeedLineDto> Lines);

public sealed record TransferNeedsDto(DateOnly AsOf, int PaychecksPerYear, string PaychecksSource, IReadOnlyList<AccountNeedDto> Accounts);

public sealed record PayDateDto(DateOnly Date, string IncomeSource, bool ThirdCheckOfMonth);

public sealed record PayCalendarDto(int Year, IReadOnlyList<PayDateDto> PayDates, IReadOnlyList<string> ThreePaycheckMonths);

public sealed record UpcomingBillDto(int BillId, string BillName, DateOnly DueDate, decimal Amount, string PaidVia, string FundingAccount, bool IsAutopay);

/// <summary>A bill's month in the year grid. DueDate is the override if set, else the generated date, else null when not due.</summary>
public sealed record BillMonthDto(
    DateOnly Period, DateOnly? DueDate, bool DueDateIsOverride,
    decimal Projected, bool ProjectedIsOverride,
    decimal? Actual, decimal? Variance, DateOnly? PaidOn, string? Notes);

public sealed record BillHistoryDto(int BillId, string BillName, decimal Projected, decimal? AverageActual, IReadOnlyList<BillMonthDto> Months);

public sealed record CardSummaryDto(
    int CardId, string CardName, string? PayingAccount, int StatementDay, int DueDay,
    decimal? LatestBalance, DateOnly? LatestBalanceAsOf, decimal CreditLimit, decimal? Utilization,
    IReadOnlyList<string> Bills, decimal MonthlyBillSpend);

public sealed record HomeDto(
    DateOnly AsOf,
    IReadOnlyList<UpcomingBillDto> UpcomingBills,
    PayDateDto? NextPayDate,
    IReadOnlyList<AccountNeedDto> NeedsThisPayPeriod);

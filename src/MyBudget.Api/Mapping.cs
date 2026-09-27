using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>Entity ↔ DTO copies. Deliberately boring so every field is visible.</summary>
internal static class Mapping
{
    internal static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static AccountDto ToDto(this Account a) => ToDto(a, DateOnly.FromDateTime(DateTime.Today));

    public static AccountDto ToDto(this Account a, DateOnly asOf)
    {
        var current = BalanceMath.Of(a, asOf);
        var has = a.Balances.Count > 0 || a.Transactions.Count > 0;
        return new()
        {
            Id = a.Id, Name = a.Name, Institution = a.Institution, Type = a.Type, AccountNumber = a.AccountNumber,
            MinimumBalance = a.MinimumBalance, TransferCadence = a.TransferCadence, IsRainyDayFund = a.IsRainyDayFund,
            Notes = a.Notes, IsActive = a.IsActive,
            LatestBalance = has ? current.Balance : null, LatestBalanceAsOf = current.SnapshotAsOf, BalanceDetail = has ? current.Detail : null,
        };
    }

    public static void Apply(this Account a, AccountDto d)
    {
        a.Name = d.Name.Trim(); a.Institution = d.Institution; a.Type = d.Type; a.AccountNumber = d.AccountNumber;
        a.MinimumBalance = d.MinimumBalance; a.TransferCadence = d.TransferCadence; a.IsRainyDayFund = d.IsRainyDayFund;
        a.Notes = d.Notes; a.IsActive = d.IsActive;
    }

    public static AccountBalanceDto ToDto(this AccountBalance b) => new() { Id = b.Id, AccountId = b.AccountId, AsOf = b.AsOf, Balance = b.Balance };
    /// <summary>A manual transfer row seen through the account ledger.</summary>
    public static TransferDto ToTransferDto(this Transaction t) => new() { Id = t.Id, AccountId = t.AccountId ?? 0, Date = t.Date, Amount = t.Amount, Notes = t.Notes, CounterpartyAccountId = t.CounterpartyAccountId, CounterpartyName = t.CounterpartyAccount?.Name, LinkedTransferId = t.LinkedTransactionId };

    public static CardDto ToDto(this Card c) => new()
    {
        Id = c.Id, Name = c.Name, Issuer = c.Issuer, Network = c.Network, AccountNumber = c.AccountNumber, Apr = c.Apr,
        PromoApr = c.PromoApr, PromoAprExpires = c.PromoAprExpires, StatementDay = c.StatementDay, DueDay = c.DueDay,
        CreditLimit = c.CreditLimit, AnnualFee = c.AnnualFee, AnnualFeeMonth = c.AnnualFeeMonth,
        PayingAccountId = c.PayingAccountId, Notes = c.Notes, IsActive = c.IsActive,
        LatestBalance = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.Balance,
        LatestBalanceAsOf = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.AsOf,
        LoyaltyProgramId = c.LoyaltyProgramId,
    };

    public static void Apply(this Card c, CardDto d)
    {
        c.Name = d.Name.Trim(); c.Issuer = d.Issuer; c.Network = d.Network; c.AccountNumber = d.AccountNumber; c.Apr = d.Apr;
        c.PromoApr = d.PromoApr; c.PromoAprExpires = d.PromoAprExpires; c.StatementDay = d.StatementDay; c.DueDay = d.DueDay;
        c.CreditLimit = d.CreditLimit; c.AnnualFee = d.AnnualFee; c.AnnualFeeMonth = d.AnnualFeeMonth;
        c.PayingAccountId = d.PayingAccountId; c.Notes = d.Notes; c.IsActive = d.IsActive;
    }

    public static CardBalanceDto ToDto(this CardBalance b) => new() { Id = b.Id, CardId = b.CardId, AsOf = b.AsOf, Balance = b.Balance };

    public static CategoryDto ToDto(this Category c) => new() { Id = c.Id, Name = c.Name, IsActive = c.IsActive, PlannedMonthly = c.PlannedMonthly, IsCardEligible = c.IsCardEligible };

    public static BillDto ToDto(this Bill b) => new()
    {
        Id = b.Id, Name = b.Name, CategoryId = b.CategoryId, AccountNumber = b.AccountNumber, Frequency = b.Frequency, DueDay = b.DueDay,
        AnchorDueDate = b.AnchorDueDate, IsAutopay = b.IsAutopay, ProjectedAmount = b.ProjectedAmount,
        PaymentMethod = b.PaymentMethod, PaymentAccountId = b.PaymentAccountId, PaymentCardId = b.PaymentCardId,
        FundingAccountId = b.FundingAccountId, BankAutopayDiscount = b.BankAutopayDiscount, IsCardEligible = b.IsCardEligible,
        StartDate = b.StartDate, EndDate = b.EndDate, Notes = b.Notes, IsActive = b.IsActive,
    };

    public static void Apply(this Bill b, BillDto d)
    {
        b.Name = d.Name.Trim(); b.CategoryId = d.CategoryId; b.AccountNumber = Clean(d.AccountNumber); b.Frequency = d.Frequency; b.DueDay = d.DueDay;
        b.AnchorDueDate = d.AnchorDueDate; b.IsAutopay = d.IsAutopay; b.ProjectedAmount = d.ProjectedAmount;
        b.PaymentMethod = d.PaymentMethod;
        b.PaymentAccountId = d.PaymentMethod == PaymentMethodKind.Account ? d.PaymentAccountId : null;
        b.PaymentCardId = d.PaymentMethod == PaymentMethodKind.Card ? d.PaymentCardId : null;
        b.FundingAccountId = d.FundingAccountId; b.BankAutopayDiscount = d.BankAutopayDiscount; b.IsCardEligible = d.IsCardEligible;
        b.StartDate = d.StartDate; b.EndDate = d.EndDate; b.Notes = d.Notes; b.IsActive = d.IsActive;
    }

    public static BillPeriodDto ToDto(this BillPeriod p) => new()
    {
        Id = p.Id, BillId = p.BillId, Period = p.Period, DueDate = p.DueDate, ProjectedAmount = p.ProjectedAmount,
        ActualAmount = p.ActualAmount, PaidOn = p.PaidOn, Notes = p.Notes,
    };

    public static IncomeSourceDto ToDto(this IncomeSource s) => new()
    {
        Id = s.Id, Name = s.Name, Type = s.Type, Notes = s.Notes, IsActive = s.IsActive, EndDate = s.EndDate,
        Overrides = s.Overrides.OrderBy(o => o.PayDate).Select(o => new PaycheckOverrideDto { Id = o.Id, PayDate = o.PayDate, GrossPercent = o.GrossFraction is { } f ? f * 100m : null, GrossAmount = o.GrossAmount, ProrateFixedDeductions = o.ProrateFixedDeductions, Notes = o.Notes }).ToList(),
        SalaryRates = s.SalaryRates.OrderBy(r => r.EffectiveDate).Select(r => new SalaryRateDto { Id = r.Id, IncomeSourceId = r.IncomeSourceId, AnnualAmount = r.AnnualAmount, EffectiveDate = r.EffectiveDate }).ToList(),
        PaySchedules = s.PaySchedules.OrderBy(p => p.EffectiveDate).Select(p => new PayScheduleDto
        {
            Id = p.Id, IncomeSourceId = p.IncomeSourceId, Frequency = p.Frequency, EffectiveDate = p.EffectiveDate, AnchorPayDate = p.AnchorPayDate,
            PayOnPriorBusinessDay = p.PayOnPriorBusinessDay, FirstPayDay = p.FirstPayDay, SecondPayDay = p.SecondPayDay,
        }).ToList(),
        Deductions = s.Deductions.OrderBy(d => d.EffectiveDate).ThenBy(d => d.Name).Select(d => new DeductionElectionDto
        {
            Id = d.Id, Name = d.Name, Kind = d.Kind, Treatment = d.Treatment, AmountPerCheck = d.AmountPerCheck,
            PercentOfGross = d.PercentOfGross is { } p ? p * 100m : null, EffectiveDate = d.EffectiveDate, EndDate = d.EndDate,
        }).ToList(),
        Withholdings = s.Withholdings.OrderBy(w => w.EffectiveDate).Select(w => new WithholdingElectionDto
        {
            Id = w.Id, EffectiveDate = w.EffectiveDate, FederalStatus = w.FederalStatus, MultipleJobs = w.MultipleJobs, DependentCredits = w.DependentCredits,
            OtherIncome = w.OtherIncome, Deductions = w.Deductions, ExtraWithholding = w.ExtraWithholding, MissouriStatus = w.MissouriStatus, MissouriExtraWithholding = w.MissouriExtraWithholding,
        }).ToList(),
    };

    /// <summary>Replaces the rates, schedules, deductions and W-4s wholesale; they are small lists edited as a unit.</summary>
    public static void Apply(this IncomeSource s, IncomeSourceDto d)
    {
        s.Name = d.Name.Trim(); s.Type = d.Type; s.Notes = d.Notes; s.IsActive = d.IsActive; s.EndDate = d.EndDate;
        s.Overrides.Clear();
        s.Overrides.AddRange(d.Overrides.Where(o => o.GrossPercent is not null || o.GrossAmount is not null).GroupBy(o => o.PayDate).Select(g => g.First()).Select(o => new PaycheckOverride
        {
            PayDate = o.PayDate, GrossFraction = o.GrossAmount is null && o.GrossPercent is { } p ? p / 100m : null, GrossAmount = o.GrossAmount, ProrateFixedDeductions = o.ProrateFixedDeductions, Notes = o.Notes,
        }));
        s.SalaryRates.Clear();
        s.SalaryRates.AddRange(d.SalaryRates.Select(r => new SalaryRate { AnnualAmount = r.AnnualAmount, EffectiveDate = r.EffectiveDate }));
        s.PaySchedules.Clear();
        s.PaySchedules.AddRange(d.PaySchedules.Select(p => new PaySchedule
        {
            Frequency = p.Frequency, EffectiveDate = p.EffectiveDate, AnchorPayDate = p.AnchorPayDate,
            PayOnPriorBusinessDay = p.PayOnPriorBusinessDay,
            FirstPayDay = p.Frequency == PayFrequency.SemiMonthly ? p.FirstPayDay : null,
            SecondPayDay = p.Frequency == PayFrequency.SemiMonthly ? p.SecondPayDay : null,
        }));
        s.Deductions.Clear();
        s.Deductions.AddRange(d.Deductions.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => new DeductionElection
        {
            Name = x.Name.Trim(), Kind = x.Kind, Treatment = x.Treatment,
            AmountPerCheck = x.PercentOfGross is > 0 ? null : x.AmountPerCheck,
            PercentOfGross = x.PercentOfGross is > 0 ? x.PercentOfGross / 100m : null,
            EffectiveDate = x.EffectiveDate, EndDate = x.EndDate,
        }));
        s.Withholdings.Clear();
        s.Withholdings.AddRange(d.Withholdings.Select(x => new WithholdingElection
        {
            EffectiveDate = x.EffectiveDate, FederalStatus = x.FederalStatus, MultipleJobs = x.MultipleJobs, DependentCredits = x.DependentCredits,
            OtherIncome = x.OtherIncome, Deductions = x.Deductions, ExtraWithholding = x.ExtraWithholding, MissouriStatus = x.MissouriStatus, MissouriExtraWithholding = x.MissouriExtraWithholding,
        }));
    }
}

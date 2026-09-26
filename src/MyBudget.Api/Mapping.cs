using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>Entity ↔ DTO copies. Deliberately boring so every field is visible.</summary>
internal static class Mapping
{
    public static AccountDto ToDto(this Account a) => new()
    {
        Id = a.Id, Name = a.Name, Institution = a.Institution, Type = a.Type, LastFour = a.LastFour,
        MinimumBalance = a.MinimumBalance, TransferCadence = a.TransferCadence, IsRainyDayFund = a.IsRainyDayFund,
        Notes = a.Notes, IsActive = a.IsActive,
        LatestBalance = a.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.Balance,
        LatestBalanceAsOf = a.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.AsOf,
    };

    public static void Apply(this Account a, AccountDto d)
    {
        a.Name = d.Name.Trim(); a.Institution = d.Institution; a.Type = d.Type; a.LastFour = d.LastFour;
        a.MinimumBalance = d.MinimumBalance; a.TransferCadence = d.TransferCadence; a.IsRainyDayFund = d.IsRainyDayFund;
        a.Notes = d.Notes; a.IsActive = d.IsActive;
    }

    public static AccountBalanceDto ToDto(this AccountBalance b) => new() { Id = b.Id, AccountId = b.AccountId, AsOf = b.AsOf, Balance = b.Balance };
    public static TransferDto ToDto(this Transfer t) => new() { Id = t.Id, AccountId = t.AccountId, Date = t.Date, Amount = t.Amount, Notes = t.Notes };

    public static CardDto ToDto(this Card c) => new()
    {
        Id = c.Id, Name = c.Name, Issuer = c.Issuer, Network = c.Network, LastFour = c.LastFour, Apr = c.Apr,
        PromoApr = c.PromoApr, PromoAprExpires = c.PromoAprExpires, StatementDay = c.StatementDay, DueDay = c.DueDay,
        CreditLimit = c.CreditLimit, AnnualFee = c.AnnualFee, AnnualFeeMonth = c.AnnualFeeMonth,
        PayingAccountId = c.PayingAccountId, Notes = c.Notes, IsActive = c.IsActive,
        LatestBalance = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.Balance,
        LatestBalanceAsOf = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.AsOf,
    };

    public static void Apply(this Card c, CardDto d)
    {
        c.Name = d.Name.Trim(); c.Issuer = d.Issuer; c.Network = d.Network; c.LastFour = d.LastFour; c.Apr = d.Apr;
        c.PromoApr = d.PromoApr; c.PromoAprExpires = d.PromoAprExpires; c.StatementDay = d.StatementDay; c.DueDay = d.DueDay;
        c.CreditLimit = d.CreditLimit; c.AnnualFee = d.AnnualFee; c.AnnualFeeMonth = d.AnnualFeeMonth;
        c.PayingAccountId = d.PayingAccountId; c.Notes = d.Notes; c.IsActive = d.IsActive;
    }

    public static CardBalanceDto ToDto(this CardBalance b) => new() { Id = b.Id, CardId = b.CardId, AsOf = b.AsOf, Balance = b.Balance };

    public static CategoryDto ToDto(this Category c) => new() { Id = c.Id, Name = c.Name, IsActive = c.IsActive };

    public static BillDto ToDto(this Bill b) => new()
    {
        Id = b.Id, Name = b.Name, CategoryId = b.CategoryId, Frequency = b.Frequency, DueDay = b.DueDay,
        AnchorDueDate = b.AnchorDueDate, IsAutopay = b.IsAutopay, ProjectedAmount = b.ProjectedAmount,
        PaymentMethod = b.PaymentMethod, PaymentAccountId = b.PaymentAccountId, PaymentCardId = b.PaymentCardId,
        FundingAccountId = b.FundingAccountId, BankAutopayDiscount = b.BankAutopayDiscount,
        StartDate = b.StartDate, EndDate = b.EndDate, Notes = b.Notes, IsActive = b.IsActive,
    };

    public static void Apply(this Bill b, BillDto d)
    {
        b.Name = d.Name.Trim(); b.CategoryId = d.CategoryId; b.Frequency = d.Frequency; b.DueDay = d.DueDay;
        b.AnchorDueDate = d.AnchorDueDate; b.IsAutopay = d.IsAutopay; b.ProjectedAmount = d.ProjectedAmount;
        b.PaymentMethod = d.PaymentMethod;
        b.PaymentAccountId = d.PaymentMethod == PaymentMethodKind.Account ? d.PaymentAccountId : null;
        b.PaymentCardId = d.PaymentMethod == PaymentMethodKind.Card ? d.PaymentCardId : null;
        b.FundingAccountId = d.FundingAccountId; b.BankAutopayDiscount = d.BankAutopayDiscount;
        b.StartDate = d.StartDate; b.EndDate = d.EndDate; b.Notes = d.Notes; b.IsActive = d.IsActive;
    }

    public static BillPeriodDto ToDto(this BillPeriod p) => new()
    {
        Id = p.Id, BillId = p.BillId, Period = p.Period, DueDate = p.DueDate, ProjectedAmount = p.ProjectedAmount,
        ActualAmount = p.ActualAmount, PaidOn = p.PaidOn, Notes = p.Notes,
    };

    public static IncomeSourceDto ToDto(this IncomeSource s) => new()
    {
        Id = s.Id, Name = s.Name, Type = s.Type, Notes = s.Notes, IsActive = s.IsActive,
        SalaryRates = s.SalaryRates.OrderBy(r => r.EffectiveDate).Select(r => new SalaryRateDto { Id = r.Id, IncomeSourceId = r.IncomeSourceId, AnnualAmount = r.AnnualAmount, EffectiveDate = r.EffectiveDate }).ToList(),
        PaySchedules = s.PaySchedules.OrderBy(p => p.EffectiveDate).Select(p => new PayScheduleDto
        {
            Id = p.Id, IncomeSourceId = p.IncomeSourceId, Frequency = p.Frequency, EffectiveDate = p.EffectiveDate, AnchorPayDate = p.AnchorPayDate,
            PayOnPriorBusinessDay = p.PayOnPriorBusinessDay, FirstPayDay = p.FirstPayDay, SecondPayDay = p.SecondPayDay,
        }).ToList(),
    };

    /// <summary>Replaces the rates and schedules wholesale; they are small lists edited as a unit.</summary>
    public static void Apply(this IncomeSource s, IncomeSourceDto d)
    {
        s.Name = d.Name.Trim(); s.Type = d.Type; s.Notes = d.Notes; s.IsActive = d.IsActive;
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
    }
}

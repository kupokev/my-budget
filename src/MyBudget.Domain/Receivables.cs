namespace MyBudget.Domain;

/// <summary>Someone who owes you money (DBT-2): Sam, Robin.</summary>
public class Person
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Obligation> Obligations { get; set; } = [];
    public List<ReceivableCharge> Charges { get; set; } = [];
    public List<ReceivablePayment> Payments { get; set; } = [];
}

/// <summary>
/// A recurring amount owed (DBT-2a): a fixed amount, or a share of a tracked line's projected amount, due
/// every <see cref="EveryMonths"/> months counting from <see cref="StartPeriod"/> (1 = monthly, 3 = quarterly).
/// </summary>
public class Obligation
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public required string Description { get; set; }
    public decimal? MonthlyAmount { get; set; }
    public int? BudgetLineId { get; set; }
    public BudgetLine? BudgetLine { get; set; }
    /// <summary>Fraction of the line's projected amount (1.0 = all of it). Used when BudgetLineId is set.</summary>
    public decimal ShareOfLine { get; set; } = 1.0m;
    public DateOnly StartPeriod { get; set; }
    public DateOnly? EndPeriod { get; set; }
    /// <summary>1 = every month, 3 = quarterly, 6 = twice a year, 12 = yearly.</summary>
    public int EveryMonths { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>Whether this obligation falls due in the given month.</summary>
    public bool DueIn(DateOnly period)
    {
        if (StartPeriod > period || (EndPeriod is { } end && end < period)) return false;
        var every = EveryMonths <= 0 ? 1 : EveryMonths;
        var months = (period.Year - StartPeriod.Year) * 12 + period.Month - StartPeriod.Month;
        return months % every == 0;
    }

    public string Cadence => EveryMonths switch { <= 1 => "monthly", 3 => "quarterly", 6 => "twice a year", 12 => "yearly", var n => $"every {n} months" };
}

/// <summary>A one-off amount owed (DBT-2a): a reimbursement you fronted.</summary>
public class ReceivableCharge
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
}

/// <summary>Money received, applied to one or more months and/or the one-off balance (DBT-2b).</summary>
public class ReceivablePayment
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public List<PaymentAllocation> Allocations { get; set; } = [];
}

/// <summary>Part of a payment applied to a month's obligations (Period set) or to one-off charges (Period null).</summary>
public class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public ReceivablePayment? Payment { get; set; }
    public DateOnly? Period { get; set; }
    public decimal Amount { get; set; }
}

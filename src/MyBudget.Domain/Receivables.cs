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

/// <summary>A recurring monthly amount owed (DBT-2a): a fixed amount, or a share of a tracked bill's projected amount.</summary>
public class Obligation
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public required string Description { get; set; }
    public decimal? MonthlyAmount { get; set; }
    public int? BillId { get; set; }
    public Bill? Bill { get; set; }
    /// <summary>Fraction of the bill's projected amount (1.0 = all of it). Used when BillId is set.</summary>
    public decimal ShareOfBill { get; set; } = 1.0m;
    public DateOnly StartPeriod { get; set; }
    public DateOnly? EndPeriod { get; set; }
    public bool IsActive { get; set; } = true;
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

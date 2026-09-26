namespace MyBudget.Domain;

public enum IncomeSourceType { W2Salary, Contract1099, Reimbursement, Other }

public enum PayFrequency
{
    /// <summary>Every two weeks, 26 checks a year (27 in some years).</summary>
    BiWeekly,
    /// <summary>Twice a month on fixed days, 24 checks a year.</summary>
    SemiMonthly,
    /// <summary>Once a month, 12 checks a year.</summary>
    Monthly,
}

public enum AccountType { Checking, Savings, HighYieldSavings, TreasuryBill, Hsa, Retirement401k, Ira, Brokerage, Crypto, Cash }

/// <summary>How money is moved into an account to cover the bills funded from it (replaces the sheet's Monthly vs PPP columns).</summary>
public enum TransferCadence { Monthly, PerPaycheck }

public enum BillFrequency { Monthly, Quarterly, SemiAnnual, Annual, OneOff }

/// <summary>What actually pays the bill: a bank account directly, or a credit card whose statement is paid later.</summary>
public enum PaymentMethodKind { Account, Card }

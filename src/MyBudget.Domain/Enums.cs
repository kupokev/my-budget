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

/// <summary>
/// Fixed list, because behaviour hangs off it: HSA accounts feed the HSA planner, tax-advantaged types are
/// excluded from the gains-tax estimate, and brokerage-style accounts are valued from their holdings.
/// Adding one means a value here plus a label in <see cref="AccountTypes.Display"/>.
/// </summary>
public enum AccountType { Checking, Savings, HighYieldSavings, Hsa, Retirement401k, TraditionalIra, RothIra, Brokerage, Crypto, Cash, Other }

public static class AccountTypes
{
    /// <summary>Gains and dividends inside these are not taxed as they happen, so the portfolio's tax estimate skips them.</summary>
    public static bool IsTaxAdvantaged(AccountType t) => t is AccountType.Hsa or AccountType.Retirement401k or AccountType.TraditionalIra or AccountType.RothIra;

    /// <summary>How the type is written in the UI; the enum name is not shown anywhere.</summary>
    public static string Display(AccountType t) => t switch
    {
        AccountType.HighYieldSavings => "High Yield Savings (HYSA)",
        AccountType.Hsa => "Health Savings Account (HSA)",
        AccountType.Retirement401k => "401(k)",
        AccountType.TraditionalIra => "Traditional IRA",
        AccountType.RothIra => "Roth IRA",
        _ => t.ToString(),
    };
}

/// <summary>How money is moved into an account to cover the lines funded from it (replaces the sheet's Monthly vs PPP columns).</summary>
public enum TransferCadence { Monthly, PerPaycheck }

/// <summary>
/// How a budget line recurs. <see cref="Variable"/> is the odd one: spend you plan for but that has no
/// due date and no biller (groceries, restaurants, gas). Its projected amount is simply a monthly figure.
/// Appended last so stored values of the other members keep their meaning.
/// </summary>
public enum BudgetFrequency { Monthly, Quarterly, SemiAnnual, Annual, OneOff, Variable }

/// <summary>What actually pays the line: a bank account directly, or a credit card whose statement is paid later.</summary>
public enum PaymentMethodKind { Account, Card }

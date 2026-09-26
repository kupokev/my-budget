namespace MyBudget.Domain;

/// <summary>A place money sits (ACC-1). The account number is stored in full for matching statements later (ADR-0008).</summary>
public class Account
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Institution { get; set; }
    public AccountType Type { get; set; }
    public string? AccountNumber { get; set; }
    public decimal MinimumBalance { get; set; }
    public TransferCadence TransferCadence { get; set; } = TransferCadence.Monthly;
    /// <summary>Counts toward the emergency fund regardless of type (ACC-5).</summary>
    public bool IsRainyDayFund { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public List<AccountBalance> Balances { get; set; } = [];
    public List<Transfer> Transfers { get; set; } = [];
}

/// <summary>Balance as of a date. Entered manually or by statement import; feeds Long/Short and net worth.</summary>
public class AccountBalance
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>An actual transfer into an account to fund its bills (ACC-3). Negative means money moved out.</summary>
public class Transfer
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

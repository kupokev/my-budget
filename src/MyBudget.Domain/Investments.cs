namespace MyBudget.Domain;

public enum TradeKind { Buy, Sell, Reinvest }
public enum DataSource { Manual, Fetched }

/// <summary>A ticker held in one brokerage account (INV-1). DRIP turns dividends into reinvest trades (INV-2a).</summary>
public class Holding
{
    public int Id { get; set; }
    public required string Ticker { get; set; }
    public string? Name { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public bool Drip { get; set; }

    /// <summary>
    /// A money-market fund or a broker's cash sweep: it holds at $1.00 and has no market quote. Some,
    /// like Chase's "QACDS", are internal codes no provider has ever heard of, so asking for a price
    /// only ever returns a 404. Its price comes from the statement instead.
    /// </summary>
    public bool IsCashEquivalent { get; set; }

    /// <summary>
    /// A fund whose price comes from the statement because no market quotes it: a 401(k) collective
    /// trust like "Target Retire 2050 Tr II" has real shares and a real NAV, but it is sold only
    /// inside the plan and its identifier is the plan's, not a ticker. Unlike cash this is a genuine
    /// position that rises and falls — it simply cannot be looked up, so asking only ever 404s.
    /// </summary>
    public bool PricedFromStatement { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<Trade> Trades { get; set; } = [];
    public List<DividendPayment> Dividends { get; set; } = [];
    public List<InvestmentFee> Fees { get; set; } = [];
}

/// <summary>
/// A charge taken out of a holding: a plan's administrative fee, an expense-ratio deduction, an
/// advisory charge. Kept apart from trades because it is a cost of holding rather than a change in
/// position — and because the point of recording it is to be able to add it up and see what a plan
/// costs to run. A plan often takes it in shares, in which case the share count moves too.
/// </summary>
public class InvestmentFee
{
    public int Id { get; set; }
    public int HoldingId { get; set; }
    public Holding? Holding { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>What was taken, as a positive amount.</summary>
    public decimal Amount { get; set; }

    public string? Description { get; set; }
    public DataSource Source { get; set; }
}

/// <summary>A buy, sell, or dividend reinvestment. Each buy/reinvest is its own lot (INV-4/5 need exact lot dates).</summary>
public class Trade
{
    public int Id { get; set; }
    public int HoldingId { get; set; }
    public Holding? Holding { get; set; }
    public DateOnly Date { get; set; }
    public TradeKind Kind { get; set; }
    public decimal Shares { get; set; }
    public decimal Price { get; set; }
    public decimal Fees { get; set; }
    /// <summary>Why you bought/sold, what you were watching for (INV-1a).</summary>
    public string? Notes { get; set; }
    /// <summary>Set on auto-generated reinvest trades so a re-sync doesn't duplicate them.</summary>
    public int? DividendPaymentId { get; set; }
    public DividendPayment? DividendPayment { get; set; }
}

/// <summary>A dividend on a holding: fetched or entered; amount = shares held on the ex-date × per-share (INV-2).</summary>
public class DividendPayment
{
    public int Id { get; set; }
    public int HoldingId { get; set; }
    public Holding? Holding { get; set; }
    public DateOnly ExDate { get; set; }
    public DateOnly? PayDate { get; set; }
    public decimal PerShare { get; set; }
    public decimal SharesHeld { get; set; }
    public decimal Amount { get; set; }
    public bool Reinvested { get; set; }
    public DataSource Source { get; set; }
}

public class PriceSnapshot
{
    public int Id { get; set; }
    public required string Ticker { get; set; }
    public DateOnly Date { get; set; }
    public decimal Price { get; set; }
    public DataSource Source { get; set; }
}

public enum AssetKind { Home, Vehicle, Other }

/// <summary>Home, vehicle, or other non-account asset valued by hand (ACC-4a); snapshots feed net worth.</summary>
public class Asset
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public AssetKind Kind { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<AssetValue> Values { get; set; } = [];
    /// <summary>Loans secured against this asset. Their balances come off its value to give equity.</summary>
    public List<Loan> Loans { get; set; } = [];
}

public class AssetValue
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Value { get; set; }
}

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
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<Trade> Trades { get; set; } = [];
    public List<DividendPayment> Dividends { get; set; } = [];
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
}

public class AssetValue
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Value { get; set; }
}

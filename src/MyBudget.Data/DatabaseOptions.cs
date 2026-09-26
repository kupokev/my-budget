namespace MyBudget.Data;

public enum DatabaseProvider { InMemory, PostgreSQL }

/// <summary>Bound from the "Database" configuration section (ADR-0005).</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.InMemory;
    public string? ConnectionString { get; set; }
    /// <summary>In-memory store name; tests give each API instance its own so parallel fixtures don't share (and double-seed) one store.</summary>
    public string Name { get; set; } = "MyBudget";
}

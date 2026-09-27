namespace MyBudget.Data;

/// <summary>
/// Where the data lives. <see cref="Sqlite"/> is the desktop default: one file on this machine, no
/// server. <see cref="PostgreSQL"/> is for the shared API a phone would sync against later.
/// </summary>
public enum DatabaseProvider { InMemory, PostgreSQL, Sqlite }

/// <summary>Bound from the "Database" configuration section (ADR-0005).</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.InMemory;
    public string? ConnectionString { get; set; }
    /// <summary>In-memory store name; tests give each API instance its own so parallel fixtures don't share (and double-seed) one store.</summary>
    public string Name { get; set; } = "MyBudget";
}

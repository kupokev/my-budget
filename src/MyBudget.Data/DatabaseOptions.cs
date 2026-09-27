namespace MyBudget.Data;

/// <summary>
/// Where the data lives. <see cref="Sqlite"/> is the desktop default: one file on this machine, no
/// server. <see cref="InMemory"/> is for tests.
///
/// A PostgreSQL provider was here for the sync server ADR-0010 anticipates. It was removed because
/// nothing used it: the branch was dead, the driver shipped in every package, and re-adding it is a
/// package reference and a case label on the day that server actually exists.
/// </summary>
public enum DatabaseProvider { InMemory, Sqlite }

/// <summary>Bound from the "Database" configuration section (ADR-0005).</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.InMemory;
    public string? ConnectionString { get; set; }
    /// <summary>In-memory store name; tests give each API instance its own so parallel fixtures don't share (and double-seed) one store.</summary>
    public string Name { get; set; } = "MyBudget";
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyBudget.Data;

namespace MyBudget.Api;

/// <summary>
/// Whole-budget export and restore, for taking a snapshot before an upgrade and for handing someone
/// their own copy of the app's data.
///
/// The file is the SQLite database itself rather than a JSON dump. Nearly every table here points at
/// another by id — a transaction at a card, a loan at an asset, a budget line at a label — so a dump
/// and reload would have to renumber every key and rewrite every reference, which is a lot of code
/// whose bugs would be silent and would land on real data. A database copy is exact by construction,
/// and it is still an ordinary SQLite file that any tool can open.
/// </summary>
public sealed class BackupService(BudgetDbContext db, DatabaseOptions options)
{
    /// <summary>Restores are staged next to the database and swapped in at startup, when nothing holds it open.</summary>
    public const string PendingSuffix = ".pending";

    public bool Supported => options.Provider == DatabaseProvider.Sqlite;

    /// <summary>The database file behind the current connection string.</summary>
    public string DatabasePath =>
        new SqliteConnectionStringBuilder(options.ConnectionString ?? "").DataSource
        ?? throw new InvalidOperationException("No SQLite data source is configured.");

    public string SuggestedFileName => $"mybudget-{DateTime.Now:yyyy-MM-dd-HHmm}.mybudget";

    /// <summary>
    /// A consistent copy, taken with VACUUM INTO so it includes anything still sitting in the
    /// write-ahead log and is compacted on the way out. Never a raw file copy of a live database.
    /// </summary>
    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        Require();
        var temp = Path.Combine(Path.GetTempPath(), $"mybudget-export-{Guid.NewGuid():N}.db");
        try
        {
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {temp}", ct);
            return await File.ReadAllBytesAsync(temp, ct);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>
    /// Checks the upload really is a MyBudget database, keeps a copy of what is being replaced, and
    /// stages the new one. The swap happens at startup because the running app holds the file open.
    /// </summary>
    public async Task<BackupImportResult> StageImportAsync(Stream upload, CancellationToken ct = default)
    {
        Require();
        var staged = Path.Combine(Path.GetTempPath(), $"mybudget-import-{Guid.NewGuid():N}.db");
        try
        {
            await using (var f = File.Create(staged)) await upload.CopyToAsync(f, ct);

            var (ok, problem, counts) = Inspect(staged);
            if (!ok) return new BackupImportResult(false, problem, null, counts);

            // Keep what is being replaced, so a restore of the wrong file is recoverable.
            var safety = Path.Combine(
                Path.GetDirectoryName(DatabasePath)!,
                $"mybudget-replaced-{DateTime.Now:yyyy-MM-dd-HHmm}.mybudget");
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {safety}", ct);

            File.Move(staged, DatabasePath + PendingSuffix, overwrite: true);
            return new BackupImportResult(true, null, safety, counts);
        }
        finally
        {
            if (File.Exists(staged)) File.Delete(staged);
        }
    }

    /// <summary>Reads the candidate to confirm it is a MyBudget database and says what is in it.</summary>
    private static (bool Ok, string? Problem, IReadOnlyDictionary<string, int> Counts) Inspect(string path)
    {
        var empty = new Dictionary<string, int>();
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open();

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "select name from sqlite_master where type='table'";
                using var r = cmd.ExecuteReader();
                while (r.Read()) tables.Add(r.GetString(0));
            }

            if (!tables.Contains("__EFMigrationsHistory"))
                return (false, "That file isn't a MyBudget budget: it has no migration history.", empty);
            foreach (var required in new[] { "Accounts", "BudgetLines", "Transactions" })
                if (!tables.Contains(required))
                    return (false, $"That file is missing the {required} table, so it isn't a MyBudget budget.", empty);

            var counts = new Dictionary<string, int>();
            foreach (var (label, table) in new[]
                     {
                         ("Accounts", "Accounts"), ("Budget lines", "BudgetLines"), ("Transactions", "Transactions"),
                         ("Cards", "Cards"), ("Holdings", "Holdings"), ("Assets", "Assets"), ("Loans", "Loans"),
                     })
            {
                if (!tables.Contains(table)) continue;
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"select count(*) from \"{table}\"";
                counts[label] = Convert.ToInt32(cmd.ExecuteScalar());
            }
            return (true, null, counts);
        }
        catch (Exception ex)
        {
            return (false, $"That file couldn't be opened as a database: {ex.Message}", empty);
        }
    }

    private void Require()
    {
        if (!Supported) throw new InvalidOperationException("Export and restore are only available for the local SQLite database.");
    }
}

public sealed record BackupImportResult(bool Ok, string? Problem, string? ReplacedCopyPath, IReadOnlyDictionary<string, int> Counts);

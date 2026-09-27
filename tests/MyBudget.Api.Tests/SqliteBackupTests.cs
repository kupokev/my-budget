using Microsoft.Data.Sqlite;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// Backup against a real SQLite file, which is the only place it works and the only place it matters:
/// this is the snapshot taken before an upgrade. The rest of the suite runs on the in-memory provider,
/// where <see cref="BackupService.Supported"/> is false, so a round trip is never exercised there.
/// Each test gets its own database file and deletes it afterwards.
/// </summary>
public sealed class SqliteBackupTests : IAsyncLifetime
{
    private const string Key = "dev";

    private string _dir = null!;
    private string _dbPath = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mybudget-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "mybudget.db");
        _app = await StartAsync();
        _client = Client();
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = BudgetApiHost.CreateBuilder([], new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={_dbPath}",
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        await BudgetApiHost.PrepareDatabaseAsync(app);
        app.MapBudgetApi(Key);
        await app.StartAsync();
        return app;
    }

    private HttpClient Client()
    {
        var c = _app.GetTestClient();
        c.DefaultRequestHeaders.Add(ApiKeyHeader.Name, Key);
        return c;
    }

    /// <summary>
    /// Relaunches the app: shut down first, then run <paramref name="whileClosed"/> against the file
    /// with nothing holding it, then start again. The order is the point — a staged database swapped in
    /// while a connection is still open gets overwritten by that connection's write-ahead log on close.
    /// </summary>
    private async Task RestartAsync(Action? whileClosed = null)
    {
        _client.Dispose();
        await _app.DisposeAsync();
        // Disposing the host does not close pooled SQLite connections, and a pooled connection still
        // holds the write-ahead log. The real app exits the process instead, which does close them.
        SqliteConnection.ClearAllPools();
        whileClosed?.Invoke();
        _app = await StartAsync();
        _client = Client();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public async Task Backup_is_available_and_points_at_the_budget_file()
    {
        var status = await _client.GetFromJsonAsync<BackupStatusDto>("api/backup/status", ApiFixture.Json);

        Assert.True(status!.Supported);
        Assert.Equal(_dbPath, status.DatabasePath);
        Assert.False(status.RestorePending);
    }

    [Fact]
    public async Task An_export_restores_the_accounts_it_was_taken_with()
    {
        // Something recognisable to look for on the other side of the round trip.
        var saved = await Post<AccountDto>("api/accounts", new AccountDto
        {
            Name = "Round Trip Checking", Institution = "Test Bank", Type = AccountType.Checking, IsActive = true,
        });

        var backup = await _client.GetByteArrayAsync("api/backup/export");

        // A real SQLite file, not an empty or truncated one.
        Assert.StartsWith("SQLite format 3", System.Text.Encoding.ASCII.GetString(backup, 0, 15));

        // Now lose the account, so a restore that silently does nothing cannot pass.
        var deleted = await _client.DeleteAsync($"api/accounts/{saved.Id}");
        deleted.EnsureSuccessStatusCode();
        Assert.DoesNotContain(await Accounts(), a => a.Id == saved.Id);

        var result = await Import(backup, "budget.mybudget");
        Assert.True(result.Ok, result.Problem);
        Assert.Null(result.Problem);
        Assert.True(result.Counts["Accounts"] >= 1);

        // The restore is staged, not applied live: the running app still sees the current data.
        Assert.True(File.Exists(_dbPath + BackupService.PendingSuffix));
        Assert.DoesNotContain(await Accounts(), a => a.Id == saved.Id);
        var pendingStatus = await _client.GetFromJsonAsync<BackupStatusDto>("api/backup/status", ApiFixture.Json);
        Assert.True(pendingStatus!.RestorePending);

        // The swap happens at startup, when nothing holds the file. This is what LocalApi does.
        await RestartAsync(ApplyPendingRestore);

        var restored = await Accounts();
        var account = Assert.Single(restored, a => a.Id == saved.Id);
        Assert.Equal("Round Trip Checking", account.Name);
    }

    [Fact]
    public async Task A_safety_copy_of_the_replaced_budget_is_kept()
    {
        var backup = await _client.GetByteArrayAsync("api/backup/export");

        var result = await Import(backup, "budget.mybudget");

        Assert.True(result.Ok, result.Problem);
        Assert.False(string.IsNullOrWhiteSpace(result.ReplacedCopyPath));
        Assert.True(File.Exists(result.ReplacedCopyPath), $"no safety copy at {result.ReplacedCopyPath}");
    }

    [Fact]
    public async Task A_file_that_is_not_a_budget_leaves_the_database_alone()
    {
        var result = await Import("SQLite format 3\0 but not a budget"u8.ToArray(), "random.db");

        Assert.False(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.Problem));
        Assert.False(File.Exists(_dbPath + BackupService.PendingSuffix));
    }

    /// <summary>Mirrors <c>LocalApi.ApplyPendingRestore</c>: drop the sidecars, move the staged file in.</summary>
    private void ApplyPendingRestore()
    {
        var pending = _dbPath + BackupService.PendingSuffix;
        Assert.True(File.Exists(pending));
        foreach (var sidecar in new[] { _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(sidecar)) File.Delete(sidecar);
        File.Move(pending, _dbPath, overwrite: true);
    }

    private async Task<BackupImportDto> Import(byte[] bytes, string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", fileName);

        var r = await _client.PostAsync("api/backup/import", form);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BackupImportDto>(ApiFixture.Json))!;
    }

    private async Task<List<AccountDto>> Accounts() =>
        (await _client.GetFromJsonAsync<List<AccountDto>>("api/accounts", ApiFixture.Json))!;

    private async Task<T> Post<T>(string url, T body)
    {
        var r = await _client.PostAsJsonAsync(url, body, ApiFixture.Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }
}

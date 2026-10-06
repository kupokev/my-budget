using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// Queries that only fail on the real provider. The EF in-memory provider runs any C# in a query, so a
/// method SQLite can't translate (Math.Sign on a decimal, for one) passes every other test and then
/// throws in the app. These run against a SQLite file, migrated the way the desktop app migrates it.
/// </summary>
public sealed class SqliteQueryTests : IAsyncLifetime
{
    private const string Key = "dev";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"mybudget-test-{Guid.NewGuid()}.db");
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = BudgetApiHost.CreateBuilder([], new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={_path};Pooling=False",
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.WebHost.UseTestServer();
        _app = builder.Build();
        await BudgetApiHost.PrepareDatabaseAsync(_app);
        _app.MapBudgetApi(Key);
        await _app.StartAsync();
        _client = _app.GetTestClient();
        _client.DefaultRequestHeaders.Add(ApiKeyHeader.Name, Key);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        File.Delete(_path);
    }

    [Fact]
    public async Task Reconcile_candidates_translate_on_sqlite()
    {
        int manualId;
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            var account = new Account { Name = "Wealthfront" };
            db.Accounts.Add(account);
            var manual = new Transaction { Account = account, Date = new DateOnly(2026, 9, 1), Amount = 5.81m, Description = "Interest", Origin = TransactionOrigin.Manual, ExternalId = "manual:1" };
            db.Transactions.AddRange(manual,
                new Transaction { Account = account, Date = new DateOnly(2026, 9, 1), Amount = 5.81m, Description = "August interest", Origin = TransactionOrigin.Imported, ExternalId = "a" },
                new Transaction { Account = account, Date = new DateOnly(2026, 9, 5), Amount = -5.81m, Description = "Money out", Origin = TransactionOrigin.Imported, ExternalId = "b" });
            await db.SaveChangesAsync();
            manualId = manual.Id;
        }

        var res = await _client.GetAsync($"api/transactions/{manualId}/reconcile-candidates");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var list = await res.Content.ReadFromJsonAsync<List<ReconcileCandidateDto>>(ApiFixture.Json);

        // The money-out line is the right size but the wrong direction.
        var only = Assert.Single(list!);
        Assert.Equal("August interest", only.Transaction.Description);
    }
}

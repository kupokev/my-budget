using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MyBudget.Api;

namespace MyBudget.Desktop;

/// <summary>
/// The API, running inside this process against a SQLite file on this machine. There is no server to
/// host and nothing listens beyond the loopback interface.
///
/// It stays an HTTP API rather than becoming direct method calls so the same endpoints can be exposed
/// over the network later, when a phone needs something to sync against.
/// </summary>
public sealed class LocalApi : IAsyncDisposable
{
    private WebApplication? app;

    public string BaseUrl { get; private set; } = "";

    /// <summary>Generated per run. The port is loopback-only, so this exists to refuse anything that isn't us.</summary>
    public string ApiKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>~/.local/share/MyBudget/mybudget.db on Linux, the platform equivalent elsewhere.</summary>
    public static string DatabasePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                "MyBudget");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "mybudget.db");
        }
    }

    public async Task StartAsync()
    {
        var port = FreeLoopbackPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var builder = BudgetApiHost.CreateBuilder([], new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={DatabasePath}",
            ["Api:Key"] = ApiKey,
        });
        builder.WebHost.UseUrls(BaseUrl);

        app = builder.Build();
        await BudgetApiHost.PrepareDatabaseAsync(app);   // migrates the file, then tops up reference data
        app.MapBudgetApi(ApiKey);
        await app.StartAsync();
    }

    /// <summary>Ask the OS for a port rather than guessing one that might already be taken.</summary>
    private static int FreeLoopbackPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (app is null) return;
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await app.StopAsync(stopping.Token); } catch (OperationCanceledException) { /* shutting down anyway */ }
        await app.DisposeAsync();
    }
}

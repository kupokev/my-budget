using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using MyBudget.Contracts;

namespace MyBudget.Api.Tests;

/// <summary>
/// Assembles the API exactly the way the desktop app does — <see cref="BudgetApiHost.CreateBuilder"/>,
/// then <see cref="BudgetApiHost.MapBudgetApi"/> — but over an in-memory transport and the EF in-memory
/// provider, with a fresh database per test class. There is no entry point or appsettings file to boot
/// from: configuration is stated here so the test says what it depends on.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private const string Key = "dev";

    private WebApplication _app = null!;

    public HttpClient Client { get; private set; } = null!;
    public HttpClient Anonymous { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = BudgetApiHost.CreateBuilder([], new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory",
            ["Database:Name"] = "test-" + Guid.NewGuid(),
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.WebHost.UseTestServer();

        _app = builder.Build();
        await BudgetApiHost.PrepareDatabaseAsync(_app);
        _app.MapBudgetApi(Key);
        await _app.StartAsync();

        Client = _app.GetTestClient();
        Client.DefaultRequestHeaders.Add(ApiKeyHeader.Name, Key);
        Anonymous = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        Anonymous?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    public async Task<T> Get<T>(string url) => (await Client.GetFromJsonAsync<T>(url, Json))!;

    public Task<T> Post<T>(string url, T body) => Post<T, T>(url, body);

    public async Task<TOut> Post<TIn, TOut>(string url, TIn body)
    {
        var r = await Client.PostAsJsonAsync(url, body, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<TOut>(Json))!;
    }

    public Task<T> Put<T>(string url, T body) => Put<T, T>(url, body);

    public async Task<TOut> Put<TIn, TOut>(string url, TIn body)
    {
        var r = await Client.PutAsJsonAsync(url, body, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<TOut>(Json))!;
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MyBudget.Contracts;

namespace MyBudget.Api.Tests;

/// <summary>Boots the API in Development against the in-memory provider with the dev seed, one instance per test class.</summary>
public sealed class ApiFixture : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Database:Name", "test-" + Guid.NewGuid());
        });

    public HttpClient Client { get; }
    public HttpClient Anonymous { get; }

    public ApiFixture()
    {
        Client = _factory.CreateClient();
        Client.DefaultRequestHeaders.Add(ApiKeyHeader.Name, "dev");
        Anonymous = _factory.CreateClient();
    }

    public async Task<T> Get<T>(string url) => (await Client.GetFromJsonAsync<T>(url, Json))!;

    public Task<T> Post<T>(string url, T body) => Post<T, T>(url, body);

    public async Task<TOut> Post<TIn, TOut>(string url, TIn body)
    {
        var r = await Client.PostAsJsonAsync(url, body, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<TOut>(Json))!;
    }

    public async Task<T> Put<T>(string url, T body)
    {
        var r = await Client.PutAsJsonAsync(url, body, Json);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public void Dispose() => _factory.Dispose();
}

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyBudget.Api.Endpoints;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>
/// The API, assembled in one place so it can run two ways: as its own process (the server a phone
/// would sync against later), or inside the desktop app on a loopback port with a SQLite file
/// behind it. Same endpoints and same services either way.
/// </summary>
public static class BudgetApiHost
{
    /// <param name="settings">
    /// Applied before anything reads configuration, because <see cref="ServiceCollectionExtensions.AddBudgetData"/>
    /// resolves the provider immediately. Adding them to the builder afterwards is too late.
    /// </param>
    public static WebApplicationBuilder CreateBuilder(string[] args, IDictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (settings is { Count: > 0 }) builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddBudgetData(builder.Configuration);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<PaycheckService>();
        builder.Services.AddScoped<ImportService>();
        builder.Services.AddScoped<InvestmentService>();
        builder.Services.AddScoped<AlertsService>();
        builder.Services.AddScoped<AiTools>();
        builder.Services.AddScoped<BackupService>();
        builder.Services.AddHttpClient<IMarketDataProvider, YahooMarketDataProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
        var configured = builder.Configuration.GetSection(AiOptions.Section).Get<AiOptions>() ?? new AiOptions();
        builder.Services.AddSingleton(configured);
        builder.Services.AddScoped<AiOptionsProvider>();
        builder.Services.AddHttpClient<AiService>(c => c.Timeout = TimeSpan.FromMinutes(4));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        return builder;
    }

    /// <summary>
    /// Brings the schema up to date and seeds. A relational database is migrated so a model change
    /// never costs the data; the in-memory one used by tests has no migrations and is just created.
    /// Demo data is only ever written to an empty in-memory database, never to a real file.
    /// </summary>
    public static async Task PrepareDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<DatabaseOptions>();

        if (options.Provider == DatabaseProvider.InMemory)
        {
            if (!app.Configuration.GetValue<bool>("Database:SkipDevSeed")) await DevSeed.SeedAsync(db);
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        // Year-keyed reference tables (tax tables, HSA limits) are additive: only missing years are added.
        await ReferenceSeed.SeedAsync(db);

        // One settings row, seeded from configuration the first time so an existing appsettings still applies.
        if (!await db.AppSettings.AnyAsync())
        {
            var configured = scope.ServiceProvider.GetRequiredService<AiOptions>();
            db.AppSettings.Add(new AppSettings
            {
                Id = 1, AiEnabled = configured.Enabled, AiBaseUrl = configured.BaseUrl,
                AiModel = configured.Model, AiMaxToolRounds = configured.MaxToolRounds,
            });
            await db.SaveChangesAsync();
        }
    }

    public static WebApplication MapBudgetApi(this WebApplication app, string? apiKey)
    {
        app.UseApiKey(apiKey, allowAnonymousPaths: ["/health"]);

        app.MapGet("/health", (DatabaseOptions db, IHostEnvironment env) =>
            new HealthDto("ok", db.Provider.ToString(), env.EnvironmentName));

        app.MapGroup("/api")
            .MapAccounts()
            .MapCards()
            .MapBudget()
            .MapIncome()
            .MapViews()
            .MapPaycheck()
            .MapReference()
            .MapHsa()
            .MapLoans()
            .MapRewards()
            .MapImport()
            .MapTransactions()
            .MapSpending()
            .MapGoals()
            .MapReports()
            .MapInvestments()
            .MapReceivables()
            .MapSideIncome()
            .MapBuildout();

        return app;
    }
}

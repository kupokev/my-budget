using MyBudget.Api;
using MyBudget.Api.Endpoints;
using MyBudget.Contracts;
using MyBudget.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBudgetData(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PaycheckService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<InvestmentService>();
builder.Services.AddScoped<AlertsService>();
builder.Services.AddScoped<AiTools>();
builder.Services.AddHttpClient<IMarketDataProvider, YahooMarketDataProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
var aiOptions = builder.Configuration.GetSection(AiOptions.Section).Get<AiOptions>() ?? new AiOptions();
builder.Services.AddSingleton(aiOptions);
builder.Services.AddHttpClient<AiService>(c => c.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
    var options = scope.ServiceProvider.GetRequiredService<DatabaseOptions>();
    if (options.Provider == DatabaseProvider.InMemory && !app.Configuration.GetValue<bool>("Database:SkipDevSeed"))
        await DevSeed.SeedAsync(db);
}

// Year-keyed reference tables are seeded for every provider; only missing years are added.
using (var scope = app.Services.CreateScope())
    await ReferenceSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<BudgetDbContext>());

app.UseApiKey(builder.Configuration["Api:Key"], allowAnonymousPaths: ["/health"]);

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

app.Run();

public partial class Program;

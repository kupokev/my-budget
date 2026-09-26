using MyBudget.Api;
using MyBudget.Api.Endpoints;
using MyBudget.Contracts;
using MyBudget.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBudgetData(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
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

app.UseApiKey(builder.Configuration["Api:Key"], allowAnonymousPaths: ["/health"]);

app.MapGet("/health", (DatabaseOptions db, IHostEnvironment env) =>
    new HealthDto("ok", db.Provider.ToString(), env.EnvironmentName));

app.MapGroup("/api")
    .MapAccounts()
    .MapCards()
    .MapBills()
    .MapIncome()
    .MapViews();

app.Run();

public partial class Program;

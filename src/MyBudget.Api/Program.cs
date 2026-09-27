using MyBudget.Api;

var builder = BudgetApiHost.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

await BudgetApiHost.PrepareDatabaseAsync(app);
app.MapBudgetApi(builder.Configuration["Api:Key"]);
app.Run();

public partial class Program;

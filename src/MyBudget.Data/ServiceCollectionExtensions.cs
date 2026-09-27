using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyBudget.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BudgetDbContext"/> using the provider named in configuration:
    /// in-memory for tests, SQLite for the desktop app (ADR-0010).
    /// </summary>
    public static IServiceCollection AddBudgetData(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddSingleton(options);
        services.AddDbContext<BudgetDbContext>(db =>
        {
            switch (options.Provider)
            {
                case DatabaseProvider.InMemory:
                    db.UseInMemoryDatabase(options.Name);
                    break;
                case DatabaseProvider.Sqlite:
                    if (string.IsNullOrWhiteSpace(options.ConnectionString))
                        throw new InvalidOperationException("Database:ConnectionString is required when Database:Provider is Sqlite.");
                    db.UseSqlite(options.ConnectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown database provider '{options.Provider}'.");
            }
        });
        return services;
    }
}

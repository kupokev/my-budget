using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyBudget.Data;

/// <summary>
/// Only used by "dotnet ef" when generating migrations. Migrations are authored against SQLite
/// because that is what the desktop app runs on; the file path here is never opened.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite("Data Source=design-time.db").Options);
}

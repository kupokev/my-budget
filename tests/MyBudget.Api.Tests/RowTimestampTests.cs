using Microsoft.EntityFrameworkCore;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// Every row records when it was created and last changed (ADR-0012). Without it, "when did I mark
/// that paid?" had no answer anywhere in the database.
/// </summary>
public class RowTimestampTests
{
    private static BudgetDbContext NewDb(string name)
        => new(new DbContextOptionsBuilder<BudgetDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public void Every_table_has_both_columns()
    {
        using var db = NewDb(Guid.NewGuid().ToString());
        var missing = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && (t.FindProperty(BudgetDbContext.CreatedAt) is null || t.FindProperty(BudgetDbContext.UpdatedAt) is null))
            .Select(t => t.ClrType.Name).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public async Task A_new_row_is_stamped_and_an_edit_moves_only_the_changed_time()
    {
        var name = Guid.NewGuid().ToString();
        int id;
        DateTime created;
        await using (var db = NewDb(name))
        {
            var label = new Label { Name = "Spire" };
            db.Labels.Add(label);
            var before = DateTime.UtcNow;
            await db.SaveChangesAsync();
            id = label.Id;
            created = (DateTime)db.Entry(label).Property(BudgetDbContext.CreatedAt).CurrentValue!;
            Assert.InRange(created, before, DateTime.UtcNow);
            Assert.Equal(created, db.Entry(label).Property(BudgetDbContext.UpdatedAt).CurrentValue);
        }

        await Task.Delay(20);
        await using (var db = NewDb(name))
        {
            var label = await db.Labels.SingleAsync(l => l.Id == id);
            label.Name = "Spire Energy";
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb(name))
        {
            var row = await db.Labels.Where(l => l.Id == id)
                .Select(l => new { Created = EF.Property<DateTime?>(l, BudgetDbContext.CreatedAt), Updated = EF.Property<DateTime?>(l, BudgetDbContext.UpdatedAt) })
                .SingleAsync();
            Assert.Equal(created, row.Created);
            Assert.True(row.Updated > created);
        }
    }
}

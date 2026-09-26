using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

public static class BillEndpoints
{
    public static RouteGroupBuilder MapBills(this RouteGroupBuilder api)
    {
        var cats = api.MapGroup("/categories");
        cats.MapGet("/", async (BudgetDbContext db) => (await db.Categories.OrderBy(c => c.Name).ToListAsync()).Select(c => c.ToDto()));
        cats.MapPost("/", async (CategoryDto dto, BudgetDbContext db) =>
        {
            var c = new Category { Name = dto.Name.Trim(), IsActive = dto.IsActive };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{c.Id}", c.ToDto());
        });
        cats.MapPut("/{id:int}", async (int id, CategoryDto dto, BudgetDbContext db) =>
        {
            var c = await db.Categories.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.Name = dto.Name.Trim(); c.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(c.ToDto());
        });

        var g = api.MapGroup("/bills");

        g.MapGet("/", async (BudgetDbContext db) => (await db.Bills.OrderBy(b => b.Name).ToListAsync()).Select(b => b.ToDto()));

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await db.Bills.FindAsync(id) is { } b ? Results.Ok(b.ToDto()) : Results.NotFound());

        g.MapPost("/", async (BillDto dto, BudgetDbContext db) =>
        {
            if (Validate(dto) is { } problem) return problem;
            var b = new Bill { Name = dto.Name };
            b.Apply(dto);
            db.Bills.Add(b);
            await db.SaveChangesAsync();
            return Results.Created($"/api/bills/{b.Id}", b.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, BillDto dto, BudgetDbContext db) =>
        {
            if (Validate(dto) is { } problem) return problem;
            var b = await db.Bills.FindAsync(id);
            if (b is null) return Results.NotFound();
            b.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(b.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var b = await db.Bills.FindAsync(id);
            if (b is null) return Results.NotFound();
            db.Bills.Remove(b);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // BIL-3: one row per bill, one column per month of the year, with projected-vs-actual variance.
        g.MapGet("/history", async (int? year, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            var bills = await db.Bills.Include(b => b.Actuals).OrderBy(b => b.Name).ToListAsync();
            var from = new DateOnly(y, 1, 1);
            var to = new DateOnly(y, 12, 31);
            return bills.Select(b =>
            {
                var due = BillDueDates.Between(b, from, to).Select(d => new DateOnly(d.Year, d.Month, 1)).ToHashSet();
                var months = Enumerable.Range(1, 12).Select(m =>
                {
                    var period = new DateOnly(y, m, 1);
                    var actual = b.Actuals.FirstOrDefault(a => a.Period == period)?.Amount;
                    var projected = due.Contains(period) ? b.ProjectedAmount : 0m;
                    return new BillMonthDto(period, actual, projected, actual is { } a ? a - projected : null);
                }).ToList();
                var actuals = months.Where(m => m.Actual is not null).Select(m => m.Actual!.Value).ToList();
                return new BillHistoryDto(b.Id, b.Name, b.ProjectedAmount, actuals.Count > 0 ? Math.Round(actuals.Average(), 2) : null, months);
            });
        });

        // Upsert the actual for a month. Period is any date in that month.
        g.MapPut("/{id:int}/actuals/{period}", async (int id, DateOnly period, BillActualDto dto, BudgetDbContext db) =>
        {
            if (await db.Bills.FindAsync(id) is null) return Results.NotFound();
            var p = new DateOnly(period.Year, period.Month, 1);
            var a = await db.BillActuals.FirstOrDefaultAsync(x => x.BillId == id && x.Period == p)
                    ?? db.BillActuals.Add(new BillActual { BillId = id, Period = p }).Entity;
            a.Amount = dto.Amount; a.PaidOn = dto.PaidOn; a.Notes = dto.Notes;
            await db.SaveChangesAsync();
            return Results.Ok(a.ToDto());
        });

        g.MapDelete("/{id:int}/actuals/{period}", async (int id, DateOnly period, BudgetDbContext db) =>
        {
            var p = new DateOnly(period.Year, period.Month, 1);
            var a = await db.BillActuals.FirstOrDefaultAsync(x => x.BillId == id && x.Period == p);
            if (a is null) return Results.NotFound();
            db.BillActuals.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/upcoming", async (int? days, BudgetDbContext db, TimeProvider clock) =>
        {
            var asOf = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            return await Upcoming(db, asOf, days ?? 14);
        });

        return api;
    }

    internal static async Task<List<UpcomingBillDto>> Upcoming(BudgetDbContext db, DateOnly asOf, int days)
    {
        var to = asOf.AddDays(days);
        var bills = await db.Bills.Include(b => b.PaymentAccount).Include(b => b.PaymentCard).Include(b => b.FundingAccount).Where(b => b.IsActive).ToListAsync();
        return bills
            .SelectMany(b => BillDueDates.Between(b, asOf, to).Select(d => new UpcomingBillDto(
                b.Id, b.Name, d, b.ProjectedAmount,
                b.PaymentMethod == PaymentMethodKind.Card ? $"Card: {b.PaymentCard?.Name}" : b.PaymentAccount?.Name ?? "—",
                b.FundingAccount?.Name ?? "—", b.IsAutopay)))
            .OrderBy(u => u.DueDate).ThenBy(u => u.BillName)
            .ToList();
    }

    private static IResult? Validate(BillDto d)
    {
        var errors = new Dictionary<string, string[]>();
        if (d.PaymentMethod == PaymentMethodKind.Account && d.PaymentAccountId is null) errors["PaymentAccountId"] = ["Pick the account that pays this bill."];
        if (d.PaymentMethod == PaymentMethodKind.Card && d.PaymentCardId is null) errors["PaymentCardId"] = ["Pick the card that pays this bill."];
        if (d.Frequency != BillFrequency.Monthly && d.AnchorDueDate is null) errors["AnchorDueDate"] = ["Non-monthly bills need a known due date to step from."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }
}

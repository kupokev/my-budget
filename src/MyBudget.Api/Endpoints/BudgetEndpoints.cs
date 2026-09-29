using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

public static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudget(this RouteGroupBuilder api)
    {
        var cats = api.MapGroup("/categories");
        cats.MapGet("/", async (BudgetDbContext db) => (await db.Categories.OrderBy(c => c.Name).ToListAsync()).Select(c => c.ToDto()));
        cats.MapPost("/", async (CategoryDto dto, BudgetDbContext db) =>
        {
            var c = new Category { Name = dto.Name.Trim(), IsActive = dto.IsActive, IsCardEligible = dto.IsCardEligible };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{c.Id}", c.ToDto());
        });
        cats.MapPut("/{id:int}", async (int id, CategoryDto dto, BudgetDbContext db) =>
        {
            var c = await db.Categories.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.Name = dto.Name.Trim(); c.IsActive = dto.IsActive; c.IsCardEligible = dto.IsCardEligible;
            await db.SaveChangesAsync();
            return Results.Ok(c.ToDto());
        });
        // Refused rather than cascaded when anything still points at it: nulling a category out of
        // historical transactions or budget lines would quietly rewrite what was already recorded.
        // Retiring it (IsActive = false) is the way to take one out of circulation and keep history.
        cats.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var c = await db.Categories.FindAsync(id);
            if (c is null) return Results.NotFound();

            var used = new Dictionary<string, int>
            {
                ["budget lines"] = await db.BudgetLines.CountAsync(x => x.CategoryId == id),
                ["transactions"] = await db.Transactions.CountAsync(x => x.CategoryId == id),
                ["labels"] = await db.Labels.CountAsync(x => x.CategoryId == id),
                ["import rules"] = await db.CategoryRules.CountAsync(x => x.CategoryId == id),
                ["card earn rules"] = await db.EarnRules.CountAsync(x => x.CategoryId == id),
                ["goals"] = await db.Goals.CountAsync(x => x.CategoryId == id),
            };

            var blocking = used.Where(kv => kv.Value > 0).Select(kv => $"{kv.Value} {kv.Key}").ToList();
            if (blocking.Count > 0)
                return Results.Problem(
                    $"\"{c.Name}\" is still used by {string.Join(", ", blocking)}. Move those across first, or untick Active to retire it and keep the history.",
                    statusCode: StatusCodes.Status409Conflict);

            db.Categories.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var labels = api.MapGroup("/labels");
        labels.MapGet("/", async (BudgetDbContext db) => (await db.Labels.OrderBy(l => l.Name).ToListAsync()).Select(ToDto));
        labels.MapPost("/", async (LabelDto dto, BudgetDbContext db) =>
        {
            var l = new Label { Name = dto.Name.Trim() };
            Apply(l, dto);
            db.Labels.Add(l);
            await db.SaveChangesAsync();
            return Results.Created($"/api/labels/{l.Id}", ToDto(l));
        });
        labels.MapPut("/{id:int}", async (int id, LabelDto dto, BudgetDbContext db) =>
        {
            var l = await db.Labels.FindAsync(id);
            if (l is null) return Results.NotFound();
            Apply(l, dto);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(l));
        });
        labels.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var l = await db.Labels.FindAsync(id);
            if (l is null) return Results.NotFound();
            db.Labels.Remove(l);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var g = api.MapGroup("/budget");

        g.MapGet("/", async (BudgetDbContext db) => (await db.BudgetLines.OrderBy(b => b.Name).ToListAsync()).Select(b => b.ToDto()));

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await db.BudgetLines.FindAsync(id) is { } b ? Results.Ok(b.ToDto()) : Results.NotFound());

        g.MapPost("/", async (BudgetLineDto dto, BudgetDbContext db) =>
        {
            if (Validate(dto) is { } problem) return problem;
            var b = new BudgetLine { Name = dto.Name };
            b.Apply(dto);
            db.BudgetLines.Add(b);
            await db.SaveChangesAsync();
            return Results.Created($"/api/budget/{b.Id}", b.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, BudgetLineDto dto, BudgetDbContext db) =>
        {
            if (Validate(dto) is { } problem) return problem;
            var b = await db.BudgetLines.FindAsync(id);
            if (b is null) return Results.NotFound();
            b.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(b.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            // A card's fee line can be deleted from here; the card's tick has to follow, or it would
            // claim to be budgeting a fee that has no line.
            await CardFeeBudget.ForgetLineAsync(db, id);
            var b = await db.BudgetLines.FindAsync(id);
            if (b is null) return Results.NotFound();
            db.BudgetLines.Remove(b);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // BIL-3: one row per line, one column per month of the year, with projected-vs-actual variance.
        // Per-month overrides (due date, projected) win over the line's defaults.
        g.MapGet("/history", async (int? year, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            var lines = await db.BudgetLines.Include(b => b.Periods).Include(b => b.Amounts).OrderBy(b => b.Name).ToListAsync();
            var from = new DateOnly(y, 1, 1);
            var to = new DateOnly(y, 12, 31);
            return lines.Select(b =>
            {
                var generated = BudgetDueDates.Between(b, from, to).ToLookup(d => new DateOnly(d.Year, d.Month, 1));
                var months = Enumerable.Range(1, 12).Select(m =>
                {
                    var period = new DateOnly(y, m, 1);
                    var row = b.Periods.FirstOrDefault(p => p.Period == period);
                    var dueDefault = generated[period].Cast<DateOnly?>().FirstOrDefault();
                    var due = row?.DueDate ?? dueDefault;
                    var projected = row?.ProjectedAmount ?? (dueDefault is not null ? b.AmountFor(period) : 0m);
                    var actual = row?.ActualAmount;
                    return new BudgetMonthDto(period, due, row?.DueDate is not null, projected, row?.ProjectedAmount is not null,
                        actual, actual is { } a ? a - projected : null, row?.PaidOn, row?.Notes);
                }).ToList();
                var actuals = months.Where(m => m.Actual is not null).Select(m => m.Actual!.Value).ToList();
                return new BudgetHistoryDto(b.Id, b.Name, b.ProjectedAmount, actuals.Count > 0 ? Math.Round(actuals.Average(), 2) : null, months);
            });
        });

        // Upsert a line's month. Period is any date in that month. A row with nothing set is deleted.
        g.MapPut("/{id:int}/periods/{period}", async (int id, DateOnly period, BudgetPeriodDto dto, BudgetDbContext db) =>
        {
            if (await db.BudgetLines.FindAsync(id) is null) return Results.NotFound();
            var p = new DateOnly(period.Year, period.Month, 1);
            var row = await db.BudgetPeriods.FirstOrDefaultAsync(x => x.BudgetLineId == id && x.Period == p);
            var empty = dto.DueDate is null && dto.ProjectedAmount is null && dto.ActualAmount is null && dto.PaidOn is null && string.IsNullOrWhiteSpace(dto.Notes);
            if (empty)
            {
                if (row is not null) { db.BudgetPeriods.Remove(row); await db.SaveChangesAsync(); }
                return Results.Ok(new BudgetPeriodDto { BudgetLineId = id, Period = p });
            }
            row ??= db.BudgetPeriods.Add(new BudgetPeriod { BudgetLineId = id, Period = p }).Entity;
            row.DueDate = dto.DueDate; row.ProjectedAmount = dto.ProjectedAmount; row.ActualAmount = dto.ActualAmount;
            row.PaidOn = dto.PaidOn; row.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(row.ToDto());
        });

        g.MapDelete("/{id:int}/periods/{period}", async (int id, DateOnly period, BudgetDbContext db) =>
        {
            var p = new DateOnly(period.Year, period.Month, 1);
            var row = await db.BudgetPeriods.FirstOrDefaultAsync(x => x.BudgetLineId == id && x.Period == p);
            if (row is null) return Results.NotFound();
            db.BudgetPeriods.Remove(row);
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

    private static LabelDto ToDto(Label l) => new() { Id = l.Id, Name = l.Name, CategoryId = l.CategoryId, IsActive = l.IsActive, Notes = l.Notes };

    private static void Apply(Label l, LabelDto d)
    {
        l.Name = d.Name.Trim(); l.CategoryId = d.CategoryId; l.IsActive = d.IsActive; l.Notes = d.Notes;
    }

    internal static async Task<List<UpcomingLineDto>> Upcoming(BudgetDbContext db, DateOnly asOf, int days)
    {
        var to = asOf.AddDays(days);
        var lines = await db.BudgetLines.Include(b => b.PaymentAccount).Include(b => b.PaymentCard).Include(b => b.FundingAccount).Include(b => b.Periods).Include(b => b.Amounts).Where(b => b.IsActive).ToListAsync();
        return lines
            .SelectMany(b => BudgetDueDates.Between(b, asOf, to, DueOverrides(b)).Select(d => new UpcomingLineDto(
                b.Id, b.Name, d, b.Periods.FirstOrDefault(p => p.Period == new DateOnly(d.Year, d.Month, 1))?.ProjectedAmount ?? b.ProjectedAmount,
                b.PaymentMethod switch
                {
                    PaymentMethodKind.Card => $"Card: {b.PaymentCard?.Name}",
                    PaymentMethodKind.Cash => "Cash",
                    _ => b.PaymentAccount?.Name ?? "—",
                },
                b.FundingAccount?.Name ?? "—", b.IsAutopay)))
            .OrderBy(u => u.DueDate).ThenBy(u => u.LineName)
            .ToList();
    }

    internal static Dictionary<DateOnly, DateOnly> DueOverrides(BudgetLine b)
        => b.Periods.Where(p => p.DueDate is not null).ToDictionary(p => p.Period, p => p.DueDate!.Value);

    /// <summary>Projected overrides for one month, keyed by line id, for transfer-needs math.</summary>
    internal static Dictionary<int, decimal> ProjectedOverrides(IEnumerable<BudgetLine> lines, DateOnly month)
    {
        var period = new DateOnly(month.Year, month.Month, 1);
        return lines.SelectMany(b => b.Periods.Where(p => p.Period == period && p.ProjectedAmount is not null).Select(p => (b.Id, p.ProjectedAmount!.Value)))
            .ToDictionary(x => x.Id, x => x.Value);
    }

    private static IResult? Validate(BudgetLineDto d)
    {
        var errors = new Dictionary<string, string[]>();
        if (d.PaymentMethod == PaymentMethodKind.Account && d.PaymentAccountId is null) errors["PaymentAccountId"] = ["Pick the account that pays this budget line."];
        if (d.PaymentMethod == PaymentMethodKind.Card && d.PaymentCardId is null) errors["PaymentCardId"] = ["Pick the card that pays this budget line."];
        if (d.Frequency is not (BudgetFrequency.Monthly or BudgetFrequency.Variable) && d.AnchorDueDate is null) errors["AnchorDueDate"] = ["Non-monthly budget lines need a known due date to step from."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }
}

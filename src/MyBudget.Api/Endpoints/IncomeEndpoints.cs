using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

public static class IncomeEndpoints
{
    public static RouteGroupBuilder MapIncome(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/income-sources");

        g.MapGet("/", async (BudgetDbContext db) =>
            (await Query(db).OrderBy(s => s.Name).ToListAsync()).Select(s => s.ToDto()));

        g.MapGet("/{id:int}", async (int id, BudgetDbContext db) =>
            await Query(db).FirstOrDefaultAsync(s => s.Id == id) is { } s ? Results.Ok(s.ToDto()) : Results.NotFound());

        g.MapPost("/", async (IncomeSourceDto dto, BudgetDbContext db) =>
        {
            if (Validate(dto) is { } problem) return problem;
            var s = new IncomeSource { Name = dto.Name };
            s.Apply(dto);
            db.IncomeSources.Add(s);
            await db.SaveChangesAsync();
            return Results.Created($"/api/income-sources/{s.Id}", s.ToDto());
        });

        g.MapPut("/{id:int}", async (int id, IncomeSourceDto dto, BudgetDbContext db) =>
        {
            if (Validate(dto) is { } problem) return problem;
            var s = await Query(db).FirstOrDefaultAsync(x => x.Id == id);
            if (s is null) return Results.NotFound();
            s.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(s.ToDto());
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var s = await db.IncomeSources.FindAsync(id);
            if (s is null) return Results.NotFound();
            db.IncomeSources.Remove(s);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // INC-3 / INC-4: every pay date in a year across all active sources, with 3-check months flagged.
        g.MapGet("/pay-calendar", async (int? year, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            return await PayCalendar(db, y);
        });

        return api;
    }

    /// <summary>
    /// What has come in this month, and what is still to come: every W-2 check dated this month at its
    /// take-home (a stub's net when one was entered, the estimator's otherwise) plus 1099 payments logged.
    /// Checks are run from January so year-to-date caps (Social Security) apply as they would on the stub.
    /// </summary>
    internal static async Task<MonthIncomeDto> MonthIncome(BudgetDbContext db, PaycheckService paychecks, DateOnly today)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var items = new List<IncomeItemDto>();

        var stubs = await db.Paychecks.Include(p => p.IncomeSource).Where(p => p.PayDate >= start && p.PayDate <= end).ToListAsync();
        var w2 = await db.IncomeSources.Where(s => s.IsActive && s.Type == IncomeSourceType.W2Salary).ToListAsync();
        foreach (var source in w2)
        {
            List<(DateOnly Date, PaycheckService.Context Context, MyBudget.Engines.Paycheck.PaycheckResult Result)> runs;
            try { runs = await paychecks.YearToDateAsync(source.Id, today.Year, end); }
            catch (InvalidOperationException) { continue; }   // no salary or W-4 yet: nothing to estimate
            foreach (var run in runs.Where(r => r.Date >= start))
            {
                var stub = stubs.FirstOrDefault(s => s.IncomeSourceId == source.Id && s.PayDate == run.Date);
                items.Add(stub is null
                    ? new(run.Date, source.Name, run.Result.Net, "estimated take-home")
                    : new(run.Date, source.Name, stub.Net, "take-home from the stub"));
            }
        }
        // A stub on a date the schedule doesn't generate — a bonus, an off-cycle check — still came in.
        foreach (var stub in stubs.Where(s => items.All(i => i.Date != s.PayDate || i.Source != s.IncomeSource?.Name)))
            items.Add(new(stub.PayDate, stub.IncomeSource?.Name ?? "Paycheck", stub.Net, "take-home from the stub"));

        var receipts = await db.IncomeReceipts.Include(r => r.IncomeSource).Where(r => r.Date >= start && r.Date <= end).ToListAsync();
        items.AddRange(receipts.Select(r => new IncomeItemDto(r.Date, r.IncomeSource?.Name ?? "1099", r.Amount, "1099, nothing withheld")));

        items = items.OrderBy(i => i.Date).ToList();
        return new MonthIncomeDto(items.Where(i => i.Date <= today).Sum(i => i.Amount), items.Sum(i => i.Amount), items);
    }

    internal static async Task<PayCalendarDto> PayCalendar(BudgetDbContext db, int year)
    {
        var sources = await Query(db).Where(s => s.IsActive && s.PaySchedules.Count > 0).ToListAsync();
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var all = new List<PayDateDto>();
        var tripleMonths = new SortedSet<string>();
        foreach (var s in sources)
        {
            var dates = PayDates.Generate(s.PaySchedules, from, s.EndDate is { } end && end < to ? end : to);
            var triples = PayDates.ThreePaycheckMonths(dates);
            var thirdChecks = triples.SelectMany(t => t.Dates.Skip(2)).ToHashSet();
            foreach (var t in triples) tripleMonths.Add($"{new DateOnly(t.Year, t.Month, 1):MMMM yyyy} ({s.Name})");
            all.AddRange(dates.Select(d => new PayDateDto(d, s.Name, thirdChecks.Contains(d))));
        }
        return new PayCalendarDto(year, all.OrderBy(d => d.Date).ThenBy(d => d.IncomeSource).ToList(), tripleMonths.ToList());
    }

    /// <summary>The W-2 schedule in effect on a date, for per-paycheck math. Falls back to monthly when none exists.</summary>
    internal static async Task<(int PaychecksPerYear, string Source)> PaychecksPerYear(BudgetDbContext db, DateOnly asOf)
    {
        var w2 = await Query(db).Where(s => s.IsActive && s.Type == IncomeSourceType.W2Salary).ToListAsync();
        var current = w2.Where(s => s.EndDate is null || s.EndDate >= asOf).SelectMany(s => s.PaySchedules.Select(p => (Source: s, Schedule: p)))
            .Where(x => x.Schedule.EffectiveDate <= asOf)
            .OrderByDescending(x => x.Schedule.EffectiveDate)
            .FirstOrDefault();
        if (current.Schedule is null) return (12, "no W-2 pay schedule in effect; assuming 12 checks/year");
        var n = PayDates.PaychecksPerYear(current.Schedule.Frequency);
        return (n, $"{current.Source.Name}: {current.Schedule.Frequency} since {current.Schedule.EffectiveDate:yyyy-MM-dd} → {n} checks/year");
    }

    private static IQueryable<IncomeSource> Query(BudgetDbContext db) => db.IncomeSources.Include(s => s.SalaryRates).Include(s => s.DepositSplits).Include(s => s.PaySchedules).Include(s => s.Deductions).Include(s => s.Withholdings).Include(s => s.Overrides);

    private static IResult? Validate(IncomeSourceDto d)
    {
        var errors = new Dictionary<string, string[]>();
        if (d.SalaryRates.GroupBy(r => r.EffectiveDate).Any(g => g.Count() > 1)) errors["SalaryRates"] = ["Two salary rates share an effective date."];
        if (d.PaySchedules.GroupBy(p => p.EffectiveDate).Any(g => g.Count() > 1)) errors["PaySchedules"] = ["Two pay schedules share an effective date."];
        if (d.PaySchedules.Any(p => p.Frequency == PayFrequency.SemiMonthly && (p.FirstPayDay is null || p.SecondPayDay is null)))
            errors["PaySchedules"] = ["Semi-monthly schedules need both pay days."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }
}

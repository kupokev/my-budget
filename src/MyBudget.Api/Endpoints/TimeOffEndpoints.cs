using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

/// <summary>
/// Paid time off, read off the pay stubs: each bucket's latest balance carried forward through the job's
/// pay dates (<see cref="TimeOffProjection"/>), and goals that need time off checked against it.
/// </summary>
public static class TimeOffEndpoints
{
    public static RouteGroupBuilder MapTimeOff(this RouteGroupBuilder api)
    {
        api.MapGet("/time-off", async (DateOnly? on, BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            return await Status(db, today, on ?? new DateOnly(today.Year, 12, 31));
        });
        api.MapGet("/time-off/goals", async (BudgetDbContext db, TimeProvider clock) =>
            await GoalChecks(db, DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));
        return api;
    }

    private static Task<List<TimeOffBucket>> Buckets(BudgetDbContext db) =>
        db.TimeOffBuckets.Include(b => b.IncomeSource).ThenInclude(s => s!.PaySchedules)
            .Include(b => b.StubLines).ThenInclude(l => l.Paycheck)
            .Where(b => b.IsActive && b.IncomeSource!.IsActive).ToListAsync();

    internal static async Task<List<TimeOffStatusDto>> Status(BudgetDbContext db, DateOnly today, DateOnly on) =>
        (await Buckets(db)).Select(b => Forecast(b, today, on)).OrderBy(s => s.Source).ThenBy(s => s.Bucket).ToList();

    /// <summary>One bucket from its newest stub on or before today, carried to <paramref name="on"/>.</summary>
    internal static TimeOffStatusDto Forecast(TimeOffBucket b, DateOnly today, DateOnly on)
    {
        var latest = b.StubLines.Where(l => l.Paycheck!.PayDate <= today).OrderByDescending(l => l.Paycheck!.PayDate).FirstOrDefault();
        var (rate, rateSource) = b.AccrualHoursPerPaycheck is { } set ? (set, "set on the bucket")
            : latest?.Accrued is { } learned ? (learned, $"accrued on the {latest.Paycheck!.PayDate:MMM d} stub")
            : (0m, "no accrual set or on a stub");
        var source = b.IncomeSource!;
        if (latest is null)
            return new(b.Id, source.Id, source.Name, b.Name, b.HoursPerDay, null, null, rate, rateSource, on, null, null);

        var start = latest.Paycheck!.PayDate;
        var end = source.EndDate is { } ended && ended < on ? ended : on;
        var payDates = PayDates.Generate(source.PaySchedules, start.AddDays(1), end);
        var f = TimeOffProjection.Project(latest.Balance, start, on, rate, payDates, b.AnnualGrantHours, b.GrantMonth ?? 1, b.MaxHours);
        return new(b.Id, source.Id, source.Name, b.Name, b.HoursPerDay, latest.Balance, start, rate, rateSource, on, f.Hours, f.Formula);
    }

    /// <summary>Active goals with time off still ahead of them, each checked against its bucket on the day it starts.</summary>
    internal static async Task<List<TimeOffGoalCheckDto>> GoalChecks(BudgetDbContext db, DateOnly today)
    {
        var goals = await db.Goals.Where(g => g.IsActive && g.Status != GoalStatus.Done && g.TimeOffBucketId != null
                                              && g.TimeOffHours != null && g.TimeOffStarts != null && g.TimeOffStarts >= today).ToListAsync();
        if (goals.Count == 0) return [];
        var buckets = (await Buckets(db)).ToDictionary(b => b.Id);
        var checks = new List<TimeOffGoalCheckDto>();
        foreach (var g in goals.OrderBy(g => g.TimeOffStarts))
        {
            if (!buckets.TryGetValue(g.TimeOffBucketId!.Value, out var b)) continue;
            var s = Forecast(b, today, g.TimeOffStarts!.Value);
            var need = g.TimeOffHours!.Value;
            checks.Add(new(g.Id, g.Name, $"{s.Source} {s.Bucket}", need, g.TimeOffStarts.Value, s.ProjectedHours,
                s.ProjectedHours is { } h && h >= need, s.HoursPerDay,
                s.Formula is null ? "No stub has recorded this bucket's balance yet." : $"{s.Formula}; needs {need:0.##}h"));
        }
        return checks;
    }
}

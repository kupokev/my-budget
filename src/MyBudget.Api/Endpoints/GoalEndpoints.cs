using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

public static class GoalEndpoints
{
    public static RouteGroupBuilder MapGoals(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/goals");
        g.MapGet("/", async (BudgetDbContext db) => (await db.Goals.OrderBy(x => x.EndDate).ThenBy(x => x.Name).ToListAsync()).Select(ToDto));
        g.MapPost("/", async (GoalDto dto, BudgetDbContext db) =>
        {
            var e = new Goal { Name = dto.Name };
            Apply(e, dto);
            db.Goals.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/goals/{e.Id}", ToDto(e));
        });
        g.MapPut("/{id:int}", async (int id, GoalDto dto, BudgetDbContext db) =>
        {
            var e = await db.Goals.FindAsync(id);
            if (e is null) return Results.NotFound();
            Apply(e, dto);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var e = await db.Goals.FindAsync(id);
            if (e is null) return Results.NotFound();
            db.Goals.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // GOL-1/2: current from the linked metric, prorated target by date, status.
        g.MapGet("/progress", async (BudgetDbContext db, PaycheckService paychecks, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var goals = await db.Goals.Where(x => x.IsActive).OrderBy(x => x.EndDate).ThenBy(x => x.Name).ToListAsync();
            var list = new List<GoalProgressDto>();
            foreach (var goal in goals)
            {
                var (current, source) = await Current(goal, db, paychecks, today);
                list.Add(Progress(goal, current, source, today));
            }
            return list;
        });

        return api;
    }

    internal static GoalProgressDto Progress(Goal goal, decimal current, string source, DateOnly today)
    {
        var dto = ToDto(goal);
        if (goal.Kind == GoalKind.NonFinancial)
            return new GoalProgressDto(dto, 0, "status only", 0, 0, goal.Status == GoalStatus.Done ? 1 : goal.Status == GoalStatus.InProgress ? 0.5m : 0, goal.Status.ToString(), 0, "non-financial goal: status is set by hand");

        var start = goal.StartValue ?? 0m;
        var span = Math.Max(1, goal.EndDate.DayNumber - goal.StartDate.DayNumber);
        var elapsedDays = Math.Clamp((today < goal.EndDate ? today : goal.EndDate).DayNumber - goal.StartDate.DayNumber, 0, span);
        var elapsed = (decimal)elapsedDays / span;
        var prorated = Math.Round(start + (goal.TargetAmount - start) * elapsed, 2);
        var totalDistance = goal.TargetAmount - start;
        var progress = totalDistance == 0 ? 1 : Math.Clamp((current - start) / totalDistance, 0, 1);
        // "Lower is better" goals (spend under X) can only be judged reached at the end date; being under the cap early is just on track.
        var reached = goal.LowerIsBetter ? today >= goal.EndDate && current <= goal.TargetAmount : current >= goal.TargetAmount;
        var onTrack = goal.LowerIsBetter ? current <= prorated : current >= prorated;
        var status = goal.Status == GoalStatus.Done ? "Done"
            : reached ? (today >= goal.EndDate ? "Done" : "Exceeded")
            : onTrack ? "On Track" : "Not On Track";
        var missing = Math.Round(goal.LowerIsBetter ? Math.Max(0, current - prorated) : Math.Max(0, prorated - current), 2);
        var formula = $"prorated target = {start:N2} + ({goal.TargetAmount:N2} − {start:N2}) × {elapsedDays}/{span} days = {prorated:N2}; current {current:N2} ({source})";
        return new GoalProgressDto(dto, Math.Round(current, 2), source, prorated, Math.Round(elapsed, 4), Math.Round(progress, 4), status, missing, formula);
    }

    private static async Task<(decimal, string)> Current(Goal goal, BudgetDbContext db, PaycheckService paychecks, DateOnly today)
    {
        var through = today < goal.EndDate ? today : goal.EndDate;
        switch (goal.Metric)
        {
            case GoalMetric.Manual:
                return (goal.ManualCurrent ?? 0m, "entered by hand");
            case GoalMetric.NetWorth:
            {
                var nw = await ReportEndpoints.NetWorth(db, today, history: false);
                return (nw.Total, "net worth from latest balances");
            }
            case GoalMetric.AccountBalances:
            {
                var ids = (goal.AccountIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList();
                var accounts = await db.Accounts.Include(a => a.Balances).Where(a => ids.Contains(a.Id)).ToListAsync();
                var sum = accounts.Sum(a => a.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.Balance ?? 0m);
                return (sum, $"latest balances of {string.Join(", ", accounts.Select(a => a.Name))}");
            }
            case GoalMetric.HsaContributed:
            {
                var year = goal.EndDate.Year;
                var sum = await db.HsaYears.Where(h => h.Year == year).SelectMany(h => h.Contributions).SumAsync(c => c.Amount);
                return (sum, $"HSA contributions recorded for {year}");
            }
            case GoalMetric.Retirement401kContributed:
            {
                var year = goal.EndDate.Year;
                var sources = await db.IncomeSources.Include(s => s.Deductions).Where(s => s.IsActive && s.Type == IncomeSourceType.W2Salary && s.PaySchedules.Count > 0).ToListAsync();
                decimal sum = 0;
                foreach (var s in sources)
                {
                    var names = s.Deductions.Where(d => d.Kind is DeductionKind.Retirement401k or DeductionKind.Roth401k).Select(d => d.Name).ToHashSet();
                    try
                    {
                        var runs = await paychecks.YearToDateAsync(s.Id, year, through);
                        sum += runs.Sum(r => r.Result.PreTaxDeductions.Concat(r.Result.PostTaxDeductions).Where(l => names.Contains(l.Name)).Sum(l => l.Amount));
                    }
                    catch (InvalidOperationException) { }
                }
                return (sum, $"401(k) deferrals estimated from {year} paychecks through {through:MMM d}");
            }
            case GoalMetric.CategoryInflow:
            case GoalMetric.CategoryOutflow:
            {
                var q = db.Transactions.Where(t => t.CategoryId == goal.CategoryId && t.Date >= goal.StartDate && t.Date <= goal.EndDate && !t.IsTransfer);
                var sum = goal.Metric == GoalMetric.CategoryInflow ? await q.Where(t => t.Amount > 0).SumAsync(t => t.Amount) : await q.Where(t => t.Amount < 0).SumAsync(t => -t.Amount);
                var name = goal.CategoryId is { } c ? (await db.Categories.FindAsync(c))?.Name : null;
                return (sum, $"{(goal.Metric == GoalMetric.CategoryInflow ? "money in" : "money out")} in {name ?? "category"} {goal.StartDate:MMM d} – {goal.EndDate:MMM d, yyyy}");
            }
            case GoalMetric.LoanBalance:
            {
                var loan = goal.LoanId is { } l ? await db.Loans.Include(x => x.Balances).FirstOrDefaultAsync(x => x.Id == l) : null;
                var latest = loan?.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
                return (latest?.Balance ?? loan?.OriginalPrincipal ?? 0m, loan is null ? "no loan selected" : $"{loan.Name} balance as of {latest?.AsOf:yyyy-MM-dd}");
            }
            default:
                return (0m, "unknown metric");
        }
    }

    private static GoalDto ToDto(Goal g) => new()
    {
        Id = g.Id, Name = g.Name, Kind = g.Kind, Metric = g.Metric, TargetAmount = g.TargetAmount, StartValue = g.StartValue, StartDate = g.StartDate, EndDate = g.EndDate,
        ManualCurrent = g.ManualCurrent, AccountIds = (g.AccountIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList(),
        CategoryId = g.CategoryId, LoanId = g.LoanId, LowerIsBetter = g.LowerIsBetter, Status = g.Status, Notes = g.Notes, IsActive = g.IsActive,
    };

    private static void Apply(Goal e, GoalDto d)
    {
        e.Name = d.Name.Trim(); e.Kind = d.Kind; e.Metric = d.Kind == GoalKind.NonFinancial ? GoalMetric.Manual : d.Metric; e.TargetAmount = d.TargetAmount; e.StartValue = d.StartValue;
        e.StartDate = d.StartDate; e.EndDate = d.EndDate < d.StartDate ? d.StartDate : d.EndDate; e.ManualCurrent = d.ManualCurrent;
        e.AccountIds = d.AccountIds.Count == 0 ? null : string.Join(",", d.AccountIds); e.CategoryId = d.CategoryId; e.LoanId = d.LoanId;
        e.LowerIsBetter = d.LowerIsBetter; e.Status = d.Status; e.Notes = d.Notes; e.IsActive = d.IsActive;
    }
}

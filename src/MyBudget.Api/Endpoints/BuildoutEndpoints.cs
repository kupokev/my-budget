using MyBudget.Contracts;
using MyBudget.Data;

namespace MyBudget.Api.Endpoints;

public static class BuildoutEndpoints
{
    public static RouteGroupBuilder MapBuildout(this RouteGroupBuilder api)
    {
        // kind: optional filter (e.g. "rewards" for the Cards page).
        api.MapGet("/alerts", async (string? kind, BudgetDbContext db, AlertsService alerts, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var needs = await ViewEndpoints.Needs(db, today);
            var upcoming = await BudgetEndpoints.Upcoming(db, today, 14);
            var rewards = await RewardsEndpoints.Report(db, today.Year, today);
            var hsa = await HsaEndpoints.PlanAsync(db, today.Year, today);
            var all = await alerts.ComputeAsync(today, needs, upcoming, rewards, hsa);
            return kind is null ? all : all.Where(a => a.Kind == kind).ToList();
        });

        api.MapGet("/rainy-day", async (AlertsService alerts, TimeProvider clock) => await alerts.RainyDayAsync(DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));

        // HOME-1, full version: everything at a glance.
        api.MapGet("/home/dashboard", async (BudgetDbContext db, AlertsService alerts, PaycheckService paychecks, AiOptions ai, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var needs = await ViewEndpoints.Needs(db, today);
            var upcoming = await BudgetEndpoints.Upcoming(db, today, 14);
            var calendar = await IncomeEndpoints.PayCalendar(db, today.Year);
            var rewards = await RewardsEndpoints.Report(db, today.Year, today);
            var hsa = await HsaEndpoints.PlanAsync(db, today.Year, today);
            var spending = await SpendingEndpoints.Summary(db, today.Year, today.Month);
            var goals = await GoalEndpoints.AllProgress(db, paychecks, today);
            var nw = await ReportEndpoints.NetWorth(db, today, history: true);
            var prev = nw.History.Count >= 2 ? nw.History[^2].Total : (decimal?)null;
            // Home is a financial-health snapshot; rewards are something to dig into on purpose, so they stay off it.
            var alertList = (await alerts.ComputeAsync(today, needs, upcoming, rewards, hsa)).Where(a => a.Kind != "rewards").ToList();
            return new HomeDashboardDto(today, alertList, upcoming, calendar.PayDates.FirstOrDefault(d => d.Date >= today), needs.Accounts,
                spending.ThisMonth, spending.LastMonth, spending.Categories.Take(6).ToList(), spending.UncategorizedCount,
                rewards.Programs, goals, nw.Total, prev is { } p ? nw.Total - p : null, await alerts.RainyDayAsync(today), ai.Enabled);
        });

        var a = api.MapGroup("/ai");
        a.MapGet("/status", async (AiService svc) => await svc.StatusAsync());
        a.MapPost("/chat", async (ChatRequest req, AiService svc, TimeProvider clock) =>
            await PaycheckEndpoints.Guarded(() => svc.ChatAsync(req.Messages, DateOnly.FromDateTime(clock.GetLocalNow().DateTime))));
        a.MapGet("/summary", async (int? year, int? month, AiService svc, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            return await PaycheckEndpoints.Guarded(() => svc.SummaryAsync(year ?? today.Year, month ?? today.Month, today));
        });

        return api;
    }
}

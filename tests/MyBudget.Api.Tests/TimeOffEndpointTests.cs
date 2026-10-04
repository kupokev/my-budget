using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// Paid time off through the paycheck system: buckets on the job, balances off the stubs, the rate
/// learned from the stub, and a goal that needs days off checked against the projection.
/// </summary>
public class TimeOffEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public TimeOffEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_stub_balance_projects_forward_and_a_trip_goal_is_checked_against_it()
    {
        var job = (await _api.Get<List<IncomeSourceDto>>("api/income-sources")).Single(s => s.Name == "Ridgeline Partners");
        job.TimeOffBuckets.Add(new TimeOffBucketDto { Name = "PTO (test)" });   // no rate: learned from the stub
        job = await _api.Put($"api/income-sources/{job.Id}", job);
        var bucket = job.TimeOffBuckets.Single(b => b.Name == "PTO (test)");

        var today = (await _api.Get<HomeDashboardDto>("api/home/dashboard")).AsOf;
        var stub = await _api.Post("api/paychecks", new PaycheckDto
        {
            IncomeSourceId = job.Id, PayDate = today.AddDays(-1), Gross = 4_000m, Net = 3_000m,
            TimeOff = [new() { BucketId = bucket.Id, Accrued = 4.62m, Used = 8m, Balance = 40m }],
        });

        var on = today.AddDays(90);
        var status = (await _api.Get<List<TimeOffStatusDto>>($"api/time-off?on={on:yyyy-MM-dd}")).Single(s => s.BucketId == bucket.Id);
        Assert.Equal(40m, status.Balance);
        Assert.Equal(4.62m, status.RatePerPaycheck);
        Assert.StartsWith("accrued on the", status.RateSource);
        Assert.True(status.ProjectedHours > 40m);
        Assert.StartsWith("40h on", status.Formula);

        var goal = await _api.Post("api/goals", new GoalDto
        {
            Name = "Trip (time off test)", Kind = GoalKind.NonFinancial, StartDate = today, EndDate = on,
            TimeOffBucketId = bucket.Id, TimeOffHours = 40m, TimeOffStarts = on,
        });
        var check = Assert.Single(await _api.Get<List<TimeOffGoalCheckDto>>("api/time-off/goals"), c => c.GoalId == goal.Id);
        Assert.True(check.Enough);
        var home = await _api.Get<HomeDashboardDto>("api/home/dashboard");
        Assert.Contains(home.Highlights, h => h.Text.StartsWith("Trip (time off test):") && h.Text.EndsWith("You can book the time off.") && h.Tone == "good");

        // A bucket the stubs mention is retired, not deleted, when removed from the job.
        job.TimeOffBuckets.RemoveAll(b => b.Id == bucket.Id);
        job = await _api.Put($"api/income-sources/{job.Id}", job);
        var retired = Assert.Single(job.TimeOffBuckets, b => b.Id == bucket.Id);
        Assert.False(retired.IsActive);

        await _api.Client.DeleteAsync($"api/goals/{goal.Id}");
        await _api.Client.DeleteAsync($"api/paychecks/{stub.Id}");
        job.TimeOffBuckets.RemoveAll(b => b.Id == bucket.Id);
        job = await _api.Put($"api/income-sources/{job.Id}", job);
        Assert.DoesNotContain(job.TimeOffBuckets, b => b.Id == bucket.Id);   // nothing on stubs now: actually removed
    }
}

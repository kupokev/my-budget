using System.Net;
using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

public class CalculatorEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public CalculatorEndpointTests(ApiFixture api) => _api = api;

    private async Task<int> EmployerId() => (await _api.Get<List<IncomeSourceDto>>("api/income-sources")).Single(s => s.Name == "Ridgeline Partners").Id;

    [Fact]
    public async Task Reference_tables_are_seeded_for_2025_and_2026_and_editable()
    {
        var years = await _api.Get<List<ReferenceYearSummaryDto>>("api/reference-years");
        Assert.Contains(years, y => y.Year == 2026);
        Assert.Contains(years, y => y.Year == 2025);

        var t = await _api.Get<TaxYearDto>("api/tax-tables/2026");
        Assert.Equal(16_100m, t.FederalStandardDeductionSingle);
        Assert.Equal(6.2m, t.SocialSecurityRatePercent);
        Assert.Equal(184_500m, t.SocialSecurityWageBase);
        Assert.Contains(t.Brackets, b => b.Jurisdiction == Jurisdiction.Federal && b.FilingStatus == FederalFilingStatus.SingleOrMarriedFilingSeparately && b.Over == 105_700m && b.RatePercent == 24m);
        Assert.Contains("Missouri 2026", t.Notes);

        var limits = await _api.Get<ContributionLimitsDto>("api/limits/2026");
        Assert.Equal(8_750m, limits.HsaFamily);
    }

    [Fact]
    public async Task Estimate_uses_seeded_elections_and_shows_its_work()
    {
        var id = await EmployerId();
        var e = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={id}&date=2026-09-18");
        Assert.Equal(26, e.PeriodsPerYear);
        Assert.Equal(170_000m, e.AnnualSalary);
        Assert.Equal(6_538.46m, e.Gross);
        Assert.Contains(e.PreTaxDeductions, l => l.Name == "Medical" && l.Amount == 160m);
        Assert.Contains(e.PreTaxDeductions, l => l.Name.StartsWith("401(k)") && l.Amount == 392.31m);
        Assert.Contains(e.PostTaxDeductions, l => l.Name.StartsWith("Life"));
        Assert.Equal(e.Gross - e.TotalPreTax - e.TotalTaxes - e.TotalPostTax, e.Net);
        Assert.NotEmpty(e.Steps);
        Assert.Contains(e.Warnings, w => w.Contains("not marked verified"));
    }

    [Fact]
    public async Task Year_view_has_26_checks_in_2026_and_a_year_end_figure()
    {
        var id = await EmployerId();
        var y = await _api.Get<YearEstimateDto>($"api/paycheck/year?sourceId={id}&year=2026");
        Assert.Equal(26, y.Checks.Count);
        Assert.Equal(y.Checks.Sum(c => c.Net), y.Net);
        Assert.True(y.FederalLiability > 0);
        Assert.Contains("liability", y.FederalYearEndFormula);
        // January checks use the 165,128 salary (semi-monthly), February onward 170,000 bi-weekly.
        Assert.Equal(Math.Round(165_128m / 24, 2), y.Checks[0].Gross);
        Assert.Equal(6_538.46m, y.Checks[^1].Gross);
    }

    [Fact]
    public async Task What_if_raising_401k_lowers_federal_tax_and_net()
    {
        var id = await EmployerId();
        var r = await _api.Client.PostAsJsonAsync("api/paycheck/what-if", new WhatIfRequest { IncomeSourceId = id, PayDate = new(2026, 9, 18), Retirement401kPercent = 12m }, ApiFixture.Json);
        r.EnsureSuccessStatusCode();
        var w = (await r.Content.ReadFromJsonAsync<WhatIfResponse>(ApiFixture.Json))!;
        Assert.True(w.Scenario.FederalIncomeTax.Amount < w.Baseline.FederalIncomeTax.Amount);
        Assert.True(w.Scenario.Net < w.Baseline.Net);
        Assert.Equal(w.Baseline.SocialSecurity.Amount, w.Scenario.SocialSecurity.Amount); // 401(k) doesn't touch FICA
        Assert.True(w.ScenarioYear.Federal < w.BaselineYear.Federal);
    }

    [Fact]
    public async Task Bonus_flat_and_aggregate_both_work()
    {
        var id = await EmployerId();
        var flat = await _api.Post<SupplementalRequest, PaycheckEstimateDto>("api/paycheck/supplemental", new() { IncomeSourceId = id, PayDate = new(2026, 12, 11), Amount = 10_000m, Method = SupplementalMethod.Flat });
        Assert.Equal(2_200m, flat.FederalIncomeTax.Amount);
        Assert.Equal(470m, flat.MissouriIncomeTax.Amount);
        var agg = await _api.Post<SupplementalRequest, PaycheckEstimateDto>("api/paycheck/supplemental", new() { IncomeSourceId = id, PayDate = new(2026, 12, 11), Amount = 10_000m, Method = SupplementalMethod.Aggregate });
        Assert.Contains("aggregate", agg.FederalIncomeTax.Name);
    }

    [Fact]
    public async Task Actual_stub_compares_line_by_line_to_the_estimate()
    {
        var id = await EmployerId();
        var est = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={id}&date=2026-09-04");
        var stub = new PaycheckDto
        {
            IncomeSourceId = id, PayDate = new(2026, 9, 4), Gross = est.Gross, Net = est.Net + 3.10m,
            Lines = [new() { Category = PaycheckLineCategory.Tax, Name = "Federal income tax", Amount = est.FederalIncomeTax.Amount - 3.10m },
                     new() { Category = PaycheckLineCategory.Tax, Name = "Union dues", Amount = 12m }],
        };
        var created = await _api.Post("api/paychecks", stub);
        var cmp = await _api.Get<PaycheckCompareDto>($"api/paychecks/{created.Id}/compare");
        Assert.True(cmp.WithinFiveDollars);
        Assert.Equal(3.10m, cmp.NetVariance);
        Assert.Equal(-3.10m, cmp.Rows.Single(r => r.Name == "Federal income tax").Variance);
        Assert.Null(cmp.Rows.Single(r => r.Name == "Medical").Actual);          // estimated but not entered
        Assert.Null(cmp.Rows.Single(r => r.Name == "Union dues").Estimated);    // entered but not estimated
    }

    [Fact]
    public async Task Hsa_plan_reproduces_the_sheet_and_updates_when_a_month_changes()
    {
        var plan = await _api.Get<HsaPlanDto>("api/hsa/2026/plan?asOf=2026-09-01");
        Assert.Equal(8_020.83m, plan.AnnualLimit);
        Assert.Equal(6_732.83m, plan.AnnualLimit - plan.Employer);
        Assert.Equal(3_927.83m, plan.Room);
        Assert.Equal(981.96m, plan.RecommendedMonthly);
        Assert.True(plan.PaychecksLeft > 0);

        var year = await _api.Get<HsaYearDto>("api/hsa/2026");
        year.Months[1] = HsaTier.Family; // covered in February after all
        await _api.Put("api/hsa/2026", year);
        var full = await _api.Get<HsaPlanDto>("api/hsa/2026/plan?asOf=2026-09-01");
        Assert.Equal(8_750m, full.AnnualLimit);
        year.Months[1] = HsaTier.NotEligible;
        await _api.Put("api/hsa/2026", year);
    }

    [Fact]
    public async Task Loan_projection_runs_from_the_latest_balance_and_honours_extra_principal()
    {
        var loans = await _api.Get<List<LoanDto>>("api/loans");
        var m = loans.Single(l => l.Name == "Mortgage");
        var p = await _api.Get<LoanProjectionDto>($"api/loans/{m.Id}/projection");
        Assert.Equal(283_000m, p.FromBalance);
        Assert.Equal(1_896.20m, p.ScheduledPayment);
        Assert.NotNull(p.PayoffDate);
        Assert.True(p.ScheduledBalanceNow < 300_000m);
        var faster = await _api.Get<LoanProjectionDto>($"api/loans/{m.Id}/projection?extra=300");
        Assert.True(faster.MonthsRemaining < p.MonthsRemaining);
        Assert.True(faster.InterestSavedByExtra > 0);
    }

    [Fact]
    public async Task Odd_check_override_and_employment_end_date_are_honoured()
    {
        var id = await EmployerId();
        var src = await _api.Get<IncomeSourceDto>($"api/income-sources/{id}");
        var normal = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={id}&date=2026-10-23");

        // Live → arrears switch: the Oct 23 check carries one week of pay; medical etc. still come out in full.
        src.Overrides.Add(new PaycheckOverrideDto { PayDate = new(2026, 10, 23), GrossPercent = 50, Notes = "live to arrears" });
        await _api.Put($"api/income-sources/{id}", src);
        var half = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={id}&date=2026-10-23");
        Assert.Equal(Math.Round(normal.Gross / 2, 2), half.Gross);
        Assert.Equal(normal.PreTaxDeductions.Single(l => l.Name == "Medical").Amount, half.PreTaxDeductions.Single(l => l.Name == "Medical").Amount);
        Assert.Equal(Math.Round(half.Gross * 0.06m, 2), half.PreTaxDeductions.Single(l => l.Name.StartsWith("401(k)")).Amount);
        Assert.Contains(half.Warnings, w => w.Contains("50%") && w.Contains("live to arrears"));
        var nextNormal = await _api.Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={id}&date=2026-11-06");
        Assert.Equal(normal.Gross, nextNormal.Gross);

        // Employment ends Nov 30: December checks disappear from the year and the estimate refuses a December date.
        src = await _api.Get<IncomeSourceDto>($"api/income-sources/{id}");
        src.EndDate = new(2026, 11, 30);
        await _api.Put($"api/income-sources/{id}", src);
        var year = await _api.Get<YearEstimateDto>($"api/paycheck/year?sourceId={id}&year=2026");
        Assert.DoesNotContain(year.Checks, c => c.Date.Month == 12);
        var refused = await _api.Client.GetAsync($"api/paycheck/estimate?sourceId={id}&date=2026-12-11");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var calendar = await _api.Get<PayCalendarDto>("api/income-sources/pay-calendar?year=2026");
        Assert.DoesNotContain(calendar.PayDates, d => d.IncomeSource == "Ridgeline Partners" && d.Date.Month == 12);

        // Put things back for the other tests.
        src.EndDate = null; src.Overrides.Clear();
        await _api.Put($"api/income-sources/{id}", src);
    }

    [Fact]
    public async Task Estimate_for_a_source_without_salary_is_a_400_not_a_500()
    {
        var chroma = (await _api.Get<List<IncomeSourceDto>>("api/income-sources")).Single(s => s.Name == "Chroma");
        var r = await _api.Client.GetAsync($"api/paycheck/estimate?sourceId={chroma.Id}&date=2026-09-18");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}

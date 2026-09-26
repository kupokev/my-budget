using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

public class HistoryEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public HistoryEndpointTests(ApiFixture api) => _api = api;

    private const string ChaseCsv = "Transaction Date,Post Date,Description,Category,Type,Amount,Memo\n" +
        "09/11/2026,09/12/2026,HULU 877-8244858 CA,Entertainment,Sale,-18.99,\n" +
        "09/14/2026,09/15/2026,SQ *BLUE BOTTLE COFFEE SAN FRANCISCO CA,Food & Drink,Sale,-6.50,\n" +
        "09/16/2026,09/17/2026,Payment Thank You-Mobile,,Payment,500.00,\n" +
        "09/18/2026,09/19/2026,SHELL OIL 57444 ST LOUIS MO,Gas,Sale,-45.20,\n";

    private async Task<ImportPreviewDto> Preview(int cardId, string csv, string name = "chase.csv")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(csv), "file", name);
        form.Add(new StringContent(cardId.ToString()), "cardId");
        var r = await _api.Client.PostAsync("api/import/preview", form);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ImportPreviewDto>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Preview_suggests_rules_bills_transfers_and_statement_categories_then_commit_fills_bill_actuals_and_card_spend()
    {
        var cards = await _api.Get<List<CardDto>>("api/cards");
        var ihg = cards.Single(c => c.CatalogKey == "chase-ihg-premier");
        var bills = await _api.Get<List<BillDto>>("api/bills");
        var hulu = bills.Single(b => b.Name == "Hulu");
        var categories = await _api.Get<List<CategoryDto>>("api/categories");

        var pv = await Preview(ihg.Id, ChaseCsv);
        Assert.Equal("chase-card", pv.Profile);
        Assert.Equal(4, pv.NewCount);
        var huluRow = pv.Rows.Single(r => r.Description.StartsWith("HULU"));
        Assert.Equal(categories.Single(c => c.Name == "Subscriptions").Id, huluRow.CategoryId);   // seeded rule "HULU"
        Assert.StartsWith("rule", huluRow.SuggestionSource);
        Assert.True(pv.Rows.Single(r => r.Description.StartsWith("Payment")).IsTransfer);
        Assert.Equal(categories.Single(c => c.Name == "Gas").Id, pv.Rows.Single(r => r.Description.StartsWith("SHELL")).CategoryId); // statement category "Gas"
        Assert.Equal("Blue Bottle Coffee", pv.Rows.Single(r => r.Description.Contains("BLUE BOTTLE")).Merchant);

        // Match Hulu to its bill before committing.
        huluRow.BillId = hulu.Id;
        var result = await _api.Post<ImportCommitRequest, ImportResultDto>("api/import/commit", new() { FileName = "chase.csv", Profile = pv.Profile, CardId = ihg.Id, Rows = pv.Rows.ToList() });
        Assert.Equal(4, result.Imported);
        Assert.Equal(1, result.BillMonthsUpdated);
        Assert.Equal(1, result.CardMonthsUpdated);

        var history = await _api.Get<List<BillHistoryDto>>("api/bills/history?year=2026");
        Assert.Equal(18.99m, history.Single(h => h.BillId == hulu.Id).Months.Single(m => m.Period == new DateOnly(2026, 9, 1)).Actual);

        var spend = await _api.Get<List<CardSpendDto>>($"api/card-spend?year=2026&cardId={ihg.Id}");
        var sept = spend.Where(s => s.Period == new DateOnly(2026, 9, 1)).ToList();
        Assert.Equal(18.99m + 6.50m + 45.20m, sept.Sum(s => s.Amount));                 // the $500 payment is a transfer, excluded
        Assert.DoesNotContain(sept, s => s.Amount == 500m);

        // Re-importing the same file: everything is a duplicate.
        var again = await Preview(ihg.Id, ChaseCsv);
        Assert.Equal(0, again.NewCount);
        Assert.Equal(4, again.DuplicateCount);
    }

    [Fact]
    public async Task Categorizing_with_a_rule_applies_to_existing_lines_and_spending_summary_reflects_it()
    {
        var cards = await _api.Get<List<CardDto>>("api/cards");
        var surpass = cards.Single(c => c.CatalogKey == "amex-hilton-surpass");
        var categories = await _api.Get<List<CategoryDto>>("api/categories");
        var restaurants = categories.Single(c => c.Name == "Restaurants").Id;
        const string csv = "Date,Description,Amount\n08/02/2026,TST* PAPPYS SMOKEHOUSE ST LOUIS MO,32.10\n08/20/2026,TST* PAPPYS SMOKEHOUSE ST LOUIS MO,28.40\n08/21/2026,KROGER #0456,84.12\n";
        var pv = await Preview(surpass.Id, csv, "amex.csv");
        Assert.Equal("amex", pv.Profile);
        await _api.Post<ImportCommitRequest, ImportResultDto>("api/import/commit", new() { FileName = "amex.csv", Profile = pv.Profile, CardId = surpass.Id, Rows = pv.Rows.ToList() });

        var lines = await _api.Get<List<TransactionDto>>($"api/transactions?year=2026&month=8&cardId={surpass.Id}");
        var pappys = lines.Where(t => t.Description.Contains("PAPPYS")).ToList();
        Assert.Equal(2, pappys.Count);
        Assert.All(pappys, t => Assert.Null(t.CategoryId));

        // Categorize one with "always" → the other gets it too.
        await _api.Put<TransactionUpdateDto, TransactionDto>($"api/transactions/{pappys[0].Id}", new() { CategoryId = restaurants, CreateRule = true });
        lines = await _api.Get<List<TransactionDto>>($"api/transactions?year=2026&month=8&cardId={surpass.Id}");
        Assert.All(lines.Where(t => t.Description.Contains("PAPPYS")), t => Assert.Equal(restaurants, t.CategoryId));
        Assert.Contains(await _api.Get<List<CategoryRuleDto>>("api/category-rules"), r => r.Pattern == "Pappys Smokehouse");

        var summary = await _api.Get<SpendingSummaryDto>("api/spending/summary?year=2026&month=8");
        Assert.Equal(60.50m, summary.Categories.Single(c => c.Name == "Restaurants").ThisMonth);
        Assert.Equal(1, summary.UncategorizedCount);                       // Kroger

        var drill = await _api.Get<CategoryDrilldownDto>($"api/spending/category/{restaurants}?year=2026&month=8");
        Assert.Equal("Pappys Smokehouse", Assert.Single(drill.Merchants).Merchant);
        Assert.Equal(60.50m, drill.Total);
    }

    [Fact]
    public async Task Goals_fill_themselves_from_metrics_and_prorate_the_target()
    {
        var progress = await _api.Get<List<GoalProgressDto>>("api/goals/progress");
        var hsa = progress.Single(p => p.Goal.Name == "Max the HSA");
        Assert.Equal(1_288m + 2_805m, hsa.Current);                        // seeded contributions
        Assert.Contains("HSA contributions", hsa.CurrentSource);
        Assert.True(hsa.ProratedTarget > 0 && hsa.ProratedTarget <= hsa.Goal.TargetAmount);
        Assert.Contains(hsa.StatusText, new[] { "On Track", "Not On Track", "Exceeded", "Done" });

        var rainy = progress.Single(p => p.Goal.Name.StartsWith("Rainy-day"));
        Assert.Contains("latest balances of", rainy.CurrentSource);

        var books = progress.Single(p => p.Goal.Name == "Read 12 books");
        Assert.Equal("InProgress", books.StatusText);

        var created = await _api.Post("api/goals", new GoalDto { Name = "Manual test", Kind = GoalKind.Financial, Metric = GoalMetric.Manual, TargetAmount = 100m, ManualCurrent = 100m, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) });
        var again = await _api.Get<List<GoalProgressDto>>("api/goals/progress");
        Assert.Equal("Exceeded", again.Single(p => p.Goal.Id == created.Id).StatusText); // reached before the end date
        await _api.Client.DeleteAsync($"api/goals/{created.Id}");
    }

    [Fact]
    public async Task Net_worth_and_year_over_year_and_export_work()
    {
        var nw = await _api.Get<NetWorthDto>("api/reports/net-worth");
        Assert.Equal(nw.Assets - nw.Cards - nw.Loans, nw.Total);
        Assert.Equal(24, nw.History.Count);
        Assert.Contains(nw.Lines, l => l.Kind == "loan" && l.Balance == -283_000m);

        var yoy = await _api.Get<YearOverYearDto>("api/reports/year-over-year?year=2026");
        Assert.Equal(2025, yoy.PriorYear);
        Assert.All(yoy.Categories, r => { Assert.Equal(12, r.ThisMonths.Count); Assert.Equal(12, r.LastMonths.Count); });

        var csv = await _api.Client.GetStringAsync("api/export/bills.csv?year=2026");
        Assert.StartsWith("Bill,Category,Funded from,Projected,Jan", csv);
        Assert.Contains("Mortgage", csv);
    }
}

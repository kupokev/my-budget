using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyBudget.Contracts;

namespace MyBudget.UI.Services;

/// <summary>Typed client over the MyBudget API. One method per endpoint; no business logic here.</summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task<HealthDto?> GetHealthAsync() => http.GetFromJsonAsync<HealthDto>("health", Json);

    // Accounts
    public Task<List<AccountDto>> GetAccountsAsync() => Get<List<AccountDto>>("api/accounts");
    public Task<AccountDto> SaveAccountAsync(AccountDto a) => a.Id == 0 ? Post("api/accounts", a) : Put($"api/accounts/{a.Id}", a);
    public Task DeleteAccountAsync(int id) => Delete($"api/accounts/{id}");
    public Task<List<AccountBalanceDto>> GetAccountBalancesAsync(int id) => Get<List<AccountBalanceDto>>($"api/accounts/{id}/balances");
    public Task<AccountBalanceDto> SaveAccountBalanceAsync(int id, AccountBalanceDto b) => Post($"api/accounts/{id}/balances", b);
    public Task<List<TransferDto>> GetTransfersAsync(int id, int? year = null, int? month = null)
        => Get<List<TransferDto>>(Q($"api/accounts/{id}/transfers", ("year", year), ("month", month)));
    public Task<TransferDto> AddTransferAsync(int id, TransferDto t) => Post($"api/accounts/{id}/transfers", t);
    public Task DeleteTransferAsync(int accountId, int transferId) => Delete($"api/accounts/{accountId}/transfers/{transferId}");

    // Cards
    public Task<List<CardDto>> GetCardsAsync() => Get<List<CardDto>>("api/cards");
    public Task<List<CardSummaryDto>> GetCardSummaryAsync() => Get<List<CardSummaryDto>>("api/cards/summary");
    public Task<CardDto> SaveCardAsync(CardDto c) => c.Id == 0 ? Post("api/cards", c) : Put($"api/cards/{c.Id}", c);
    public Task DeleteCardAsync(int id) => Delete($"api/cards/{id}");
    public Task<CardBalanceDto> SaveCardBalanceAsync(int id, CardBalanceDto b) => Post($"api/cards/{id}/balances", b);

    // Categories & lines
    public Task<List<CategoryDto>> GetCategoriesAsync() => Get<List<CategoryDto>>("api/categories");
    public Task<List<LabelDto>> GetLabelsAsync() => Get<List<LabelDto>>("api/labels");
    public Task<LabelDto> SaveLabelAsync(LabelDto l) => l.Id == 0 ? Post("api/labels", l) : Put($"api/labels/{l.Id}", l);
    public Task DeleteLabelAsync(int id) => Delete($"api/labels/{id}");
    public Task<CategoryDto> SaveCategoryAsync(CategoryDto c) => c.Id == 0 ? Post("api/categories", c) : Put($"api/categories/{c.Id}", c);
    public Task DeleteCategoryAsync(int id) => Delete($"api/categories/{id}");
    public Task<List<BudgetLineDto>> GetBudgetLinesAsync() => Get<List<BudgetLineDto>>("api/budget");
    public Task<BudgetLineDto> SaveBudgetLineAsync(BudgetLineDto b) => b.Id == 0 ? Post("api/budget", b) : Put($"api/budget/{b.Id}", b);
    public Task DeleteBudgetLineAsync(int id) => Delete($"api/budget/{id}");
    public Task<List<BudgetHistoryDto>> GetBudgetHistoryAsync(int year) => Get<List<BudgetHistoryDto>>($"api/budget/history?year={year}");
    public Task<BudgetPeriodDto> SaveBudgetPeriodAsync(int lineId, DateOnly period, BudgetPeriodDto p) => Put($"api/budget/{lineId}/periods/{period:yyyy-MM-dd}", p);
    public Task DeleteBudgetPeriodAsync(int lineId, DateOnly period) => Delete($"api/budget/{lineId}/periods/{period:yyyy-MM-dd}");
    public Task<List<UpcomingLineDto>> GetUpcomingLinesAsync(int days) => Get<List<UpcomingLineDto>>($"api/budget/upcoming?days={days}");

    // Income
    public Task<List<IncomeSourceDto>> GetIncomeSourcesAsync() => Get<List<IncomeSourceDto>>("api/income-sources");
    public Task<IncomeSourceDto> SaveIncomeSourceAsync(IncomeSourceDto s) => s.Id == 0 ? Post("api/income-sources", s) : Put($"api/income-sources/{s.Id}", s);
    public Task DeleteIncomeSourceAsync(int id) => Delete($"api/income-sources/{id}");
    public Task<PayCalendarDto> GetPayCalendarAsync(int year) => Get<PayCalendarDto>($"api/income-sources/pay-calendar?year={year}");

    // Paycheck (Phase 2)
    public Task<PaycheckEstimateDto> GetPaycheckEstimateAsync(int sourceId, DateOnly date) => Get<PaycheckEstimateDto>($"api/paycheck/estimate?sourceId={sourceId}&date={date:yyyy-MM-dd}");
    public Task<YearEstimateDto> GetPaycheckYearAsync(int sourceId, int year) => Get<YearEstimateDto>($"api/paycheck/year?sourceId={sourceId}&year={year}");
    public Task<WhatIfResponse> WhatIfAsync(WhatIfRequest req) => Post<WhatIfRequest, WhatIfResponse>("api/paycheck/what-if", req);
    public Task<PaycheckEstimateDto> SupplementalAsync(SupplementalRequest req) => Post<SupplementalRequest, PaycheckEstimateDto>("api/paycheck/supplemental", req);
    public Task<List<PaycheckDto>> GetPaychecksAsync(int? sourceId = null, int? year = null) => Get<List<PaycheckDto>>(Q("api/paychecks", ("sourceId", sourceId), ("year", year)));
    public Task<PaycheckDto> SavePaycheckAsync(PaycheckDto p) => p.Id == 0 ? Post("api/paychecks", p) : Put($"api/paychecks/{p.Id}", p);
    public Task DeletePaycheckAsync(int id) => Delete($"api/paychecks/{id}");
    public Task<PaycheckCompareDto> ComparePaycheckAsync(int id) => Get<PaycheckCompareDto>($"api/paychecks/{id}/compare");

    // Reference tables
    public Task<List<ReferenceYearSummaryDto>> GetReferenceYearsAsync() => Get<List<ReferenceYearSummaryDto>>("api/reference-years");
    public Task<TaxYearDto> GetTaxYearAsync(int year) => Get<TaxYearDto>($"api/tax-tables/{year}");
    public Task<TaxYearDto> SaveTaxYearAsync(TaxYearDto t) => Put($"api/tax-tables/{t.Year}", t);
    public Task<TaxYearDto> CopyTaxYearAsync(int year, int fromYear) => Post<object, TaxYearDto>($"api/tax-tables/{year}/copy-from/{fromYear}", new { });
    public Task<ContributionLimitsDto> GetLimitsAsync(int year) => Get<ContributionLimitsDto>($"api/limits/{year}");
    public Task<ContributionLimitsDto> SaveLimitsAsync(ContributionLimitsDto l) => Put($"api/limits/{l.Year}", l);

    // HSA
    public Task<HsaYearDto> GetHsaYearAsync(int year) => Get<HsaYearDto>($"api/hsa/{year}");
    public Task<HsaYearDto> SaveHsaYearAsync(HsaYearDto y) => Put($"api/hsa/{y.Year}", y);
    public Task<HsaYearDto> AddHsaContributionAsync(int year, HsaContributionDto c) => Post<HsaContributionDto, HsaYearDto>($"api/hsa/{year}/contributions", c);
    public Task DeleteHsaContributionAsync(int year, int id) => Delete($"api/hsa/{year}/contributions/{id}");
    public Task<HsaPlanDto> GetHsaPlanAsync(int year) => Get<HsaPlanDto>($"api/hsa/{year}/plan");

    // Loans
    public Task<List<LoanDto>> GetLoansAsync() => Get<List<LoanDto>>("api/loans");
    public Task<LoanDto> SaveLoanAsync(LoanDto l) => l.Id == 0 ? Post("api/loans", l) : Put($"api/loans/{l.Id}", l);
    public Task DeleteLoanAsync(int id) => Delete($"api/loans/{id}");
    public Task<LoanBalanceDto> SaveLoanBalanceAsync(int id, LoanBalanceDto b) => Post($"api/loans/{id}/balances", b);
    public Task<LoanProjectionDto> GetLoanProjectionAsync(int id, decimal? extra = null) => Get<LoanProjectionDto>(extra is { } e ? $"api/loans/{id}/projection?extra={e}" : $"api/loans/{id}/projection");

    // Rewards (Phase 3)
    public Task<CardRewardsDto> GetCardRewardsAsync(int cardId) => Get<CardRewardsDto>($"api/cards/{cardId}/rewards");
    public Task<CardRewardsDto> SaveCardRewardsAsync(CardRewardsDto r) => Put($"api/cards/{r.CardId}/rewards", r);
    public Task<CardPerkUseDto> LogPerkUseAsync(int perkId, DateOnly date, string? note)
        => Post<CardPerkUseDto, CardPerkUseDto>($"api/perks/{perkId}/uses", new() { Date = date, Note = note });
    public Task<CardPerkUseDto> UpdatePerkUseAsync(CardPerkUseDto use) => Put($"api/perks/uses/{use.Id}", use);
    public Task RemovePerkUseAsync(int useId) => Delete($"api/perks/uses/{useId}");
    public Task<List<LoyaltyProgramDto>> GetLoyaltyProgramsAsync() => Get<List<LoyaltyProgramDto>>("api/loyalty-programs");
    public Task<LoyaltyProgramDto> SaveLoyaltyProgramAsync(LoyaltyProgramDto p) => p.Id == 0 ? Post("api/loyalty-programs", p) : Put($"api/loyalty-programs/{p.Id}", p);
    public Task DeleteLoyaltyProgramAsync(int id) => Delete($"api/loyalty-programs/{id}");
    public Task<RewardsReportDto> GetRewardsReportAsync(int year) => Get<RewardsReportDto>($"api/rewards/report?year={year}");

    // Import & history (Phase 4)
    public Task<List<ImportProfileDto>> GetImportProfilesAsync() => Get<List<ImportProfileDto>>("api/import/profiles");
    public async Task<ImportPreviewDto> PreviewImportAsync(string fileName, Stream file, int? accountId, int? cardId, string? profile)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(file), "file", fileName);
        if (accountId is { } a) form.Add(new StringContent(a.ToString()), "accountId");
        if (cardId is { } c) form.Add(new StringContent(c.ToString()), "cardId");
        if (!string.IsNullOrEmpty(profile)) form.Add(new StringContent(profile), "profile");
        var r = await http.PostAsync("api/import/preview", form);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<ImportPreviewDto>(Json))!;
    }
    public Task<ImportResultDto> CommitImportAsync(ImportCommitRequest req) => Post<ImportCommitRequest, ImportResultDto>("api/import/commit", req);
    public Task<List<ImportBatchDto>> GetImportBatchesAsync() => Get<List<ImportBatchDto>>("api/import/batches");
    public Task DeleteImportBatchAsync(int id) => Delete($"api/import/batches/{id}");
    public Task<List<TransactionDto>> GetTransactionsAsync(int? year = null, int? month = null, int? categoryId = null, bool? uncategorized = null, string? search = null, int? accountId = null, int? cardId = null, int? lineId = null, bool? unreconciled = null)
        => Get<List<TransactionDto>>(Q("api/transactions", ("year", year), ("month", month), ("categoryId", categoryId), ("uncategorized", uncategorized == true ? "true" : null), ("search", string.IsNullOrWhiteSpace(search) ? null : search), ("accountId", accountId), ("cardId", cardId), ("lineId", lineId), ("unreconciled", unreconciled == true ? "true" : null)));
    public Task<List<ReconcileCandidateDto>> GetReconcileCandidatesAsync(int id, bool all = false) => Get<List<ReconcileCandidateDto>>(Q($"api/transactions/{id}/reconcile-candidates", ("all", all ? "true" : null)));
    public Task<TransactionDto> ReconcileAsync(int id, int otherId) => Post<object, TransactionDto>($"api/transactions/{id}/reconcile/{otherId}", new { });
    public Task UnreconcileAsync(int id) => Delete($"api/transactions/{id}/reconcile");
    public Task<TransactionDto> UpdateTransactionAsync(int id, TransactionUpdateDto u) => Put<TransactionUpdateDto, TransactionDto>($"api/transactions/{id}", u);
    public Task<TransactionDto> CreateTransactionAsync(TransactionCreateDto t) => Post<TransactionCreateDto, TransactionDto>("api/transactions", t);
    public Task DeleteTransactionAsync(int id) => Delete($"api/transactions/{id}");
    public Task<BudgetLabelBreakdownDto> GetBudgetLabelsAsync(int lineId, int year)
        => Get<BudgetLabelBreakdownDto>($"api/budget/{lineId}/labels?year={year}");
    public Task<List<CategoryRuleDto>> GetCategoryRulesAsync() => Get<List<CategoryRuleDto>>("api/category-rules");
    public Task<CategoryRuleDto> SaveCategoryRuleAsync(CategoryRuleDto r) => r.Id == 0 ? Post("api/category-rules", r) : Put($"api/category-rules/{r.Id}", r);
    public Task DeleteCategoryRuleAsync(int id) => Delete($"api/category-rules/{id}");
    public Task ApplyCategoryRulesAsync() => Post<object, object>("api/category-rules/apply", new { });
    public Task<List<GoalDto>> GetGoalsAsync() => Get<List<GoalDto>>("api/goals");
    public Task<GoalDto> SaveGoalAsync(GoalDto g) => g.Id == 0 ? Post("api/goals", g) : Put($"api/goals/{g.Id}", g);
    public Task DeleteGoalAsync(int id) => Delete($"api/goals/{id}");
    public Task<List<GoalProgressDto>> GetGoalProgressAsync() => Get<List<GoalProgressDto>>("api/goals/progress");
    public Task<YearOverYearDto> GetYearOverYearAsync(int year) => Get<YearOverYearDto>($"api/reports/year-over-year?year={year}");
    public Task<NetWorthDto> GetNetWorthAsync() => Get<NetWorthDto>("api/reports/net-worth");
    public async Task<string> ExportCsvAsync(string name, int? year = null)
    {
        var r = await http.GetAsync(Q($"api/export/{name}.csv", ("year", year)));
        await ThrowIfFailed(r);
        return await r.Content.ReadAsStringAsync();
    }

    // Build-out (Phase 5)
    public Task<PortfolioDto> GetPortfolioAsync(int? year = null) => Get<PortfolioDto>(Q("api/investments/portfolio", ("year", year)));
    public Task<PortfolioHistoryDto> GetPortfolioHistoryAsync(int months) => Get<PortfolioHistoryDto>(Q("api/investments/history", ("months", months)));
    public Task<List<HoldingDto>> GetHoldingsAsync() => Get<List<HoldingDto>>("api/investments/holdings");
    public Task<HoldingDto> SaveHoldingAsync(HoldingDto h) => h.Id == 0 ? Post("api/investments/holdings", h) : Put($"api/investments/holdings/{h.Id}", h);
    public Task DeleteHoldingAsync(int id) => Delete($"api/investments/holdings/{id}");
    public Task<MarketSyncResultDto> SyncHoldingAsync(int id) => Post<object, MarketSyncResultDto>($"api/investments/holdings/{id}/sync", new { });
    public Task<List<MarketSyncResultDto>> SyncAllHoldingsAsync() => Post<object, List<MarketSyncResultDto>>("api/investments/sync-all", new { });
    public Task<TradeDto> SaveTradeAsync(TradeDto t) => t.Id == 0 ? Post("api/investments/trades", t) : Put($"api/investments/trades/{t.Id}", t);
    public Task DeleteTradeAsync(int id) => Delete($"api/investments/trades/{id}");
    public Task<DividendDto> AddDividendAsync(DividendDto d) => Post("api/investments/dividends", d);
    public Task DeleteDividendAsync(int id) => Delete($"api/investments/dividends/{id}");
    public Task SetPriceAsync(string ticker, DateOnly date, decimal price) => Post<object, object>(Q("api/investments/prices", ("ticker", ticker), ("date", date.ToString("yyyy-MM-dd")), ("price", price)), new { });
    public async Task<LotImportResultDto> ImportLotsAsync(string fileName, Stream file, int accountId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(file), "file", fileName);
        form.Add(new StringContent(accountId.ToString()), "accountId");
        var r = await http.PostAsync("api/investments/import-lots", form);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<LotImportResultDto>(Json))!;
    }
    /// <summary>Asks the API which importer a file is for, so one upload box can serve both.</summary>
    public async Task<ImportKindDto> DetectImportKindAsync(string fileName, Stream file)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(file), "file", fileName);
        var r = await http.PostAsync("api/import/detect", form);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<ImportKindDto>(Json))!;
    }

    public Task<decimal> SetCashBalanceAsync(int holdingId, decimal balance, DateOnly asOf)
        => Post<CashBalanceDto, decimal>($"api/investments/holdings/{holdingId}/cash-balance", new() { Balance = balance, AsOf = asOf });

    public Task<List<AssetDto>> GetAssetsAsync() => Get<List<AssetDto>>("api/assets");
    public Task<AssetDto> SaveAssetAsync(AssetDto a) => a.Id == 0 ? Post("api/assets", a) : Put($"api/assets/{a.Id}", a);
    public Task DeleteAssetAsync(int id) => Delete($"api/assets/{id}");
    public Task<AssetDto> SaveAssetValueAsync(int id, AssetValueDto v) => Post<AssetValueDto, AssetDto>($"api/assets/{id}/values", v);
    public Task<AssetDto> DeleteAssetValueAsync(int id, int valueId) => Delete<AssetDto>($"api/assets/{id}/values/{valueId}");
    public Task<List<PersonDto>> GetPeopleAsync() => Get<List<PersonDto>>("api/people");
    public Task<List<PersonLedgerDto>> GetLedgersAsync() => Get<List<PersonLedgerDto>>("api/people/ledgers");
    public Task<PersonDto> SavePersonAsync(PersonDto p) => p.Id == 0 ? Post("api/people", p) : Put($"api/people/{p.Id}", p);
    public Task DeletePersonAsync(int id) => Delete($"api/people/{id}");
    public Task<List<TimeOffStatusDto>> GetTimeOffAsync(DateOnly? on = null) => Get<List<TimeOffStatusDto>>(Q("api/time-off", ("on", on?.ToString("yyyy-MM-dd"))));
    public Task<List<TimeOffGoalCheckDto>> GetTimeOffGoalChecksAsync() => Get<List<TimeOffGoalCheckDto>>("api/time-off/goals");
    public Task<List<IncomeReceiptDto>> GetReceiptsAsync(int year) => Get<List<IncomeReceiptDto>>($"api/side-income/receipts?year={year}");
    public Task<IncomeReceiptDto> AddReceiptAsync(IncomeReceiptDto r) => Post("api/side-income/receipts", r);
    public Task<IncomeReceiptDto> UpdateReceiptAsync(IncomeReceiptDto r) => Put($"api/side-income/receipts/{r.Id}", r);
    public Task DeleteReceiptAsync(int id) => Delete($"api/side-income/receipts/{id}");
    public Task<List<EstimatedTaxPaymentDto>> GetEstimatedPaymentsAsync(int year) => Get<List<EstimatedTaxPaymentDto>>($"api/side-income/payments?year={year}");
    public Task<EstimatedTaxPaymentDto> AddEstimatedPaymentAsync(EstimatedTaxPaymentDto p) => Post("api/side-income/payments", p);
    public Task DeleteEstimatedPaymentAsync(int id) => Delete($"api/side-income/payments/{id}");
    public Task<SelfEmploymentDto> GetSelfEmploymentAsync(int year, decimal? projected) => Get<SelfEmploymentDto>(Q("api/side-income/estimate", ("year", year), ("projected", projected)));
    public Task<List<AlertDto>> GetAlertsAsync(string? kind = null) => Get<List<AlertDto>>(Q("api/alerts", ("kind", kind)));
    public Task<RainyDayDto> GetRainyDayAsync() => Get<RainyDayDto>("api/rainy-day");
    public Task<HomeDashboardDto> GetDashboardAsync() => Get<HomeDashboardDto>("api/home/dashboard");
    public Task<CumulativeSpendDto> GetCumulativeSpendAsync() => Get<CumulativeSpendDto>("api/spending/cumulative");
    public Task<AiStatusDto> GetAiStatusAsync() => Get<AiStatusDto>("api/ai/status");
    public Task<AppSettingsDto> GetAppSettingsAsync() => Get<AppSettingsDto>("api/settings");
    public Task<AppSettingsDto> SaveAppSettingsAsync(AppSettingsDto s) => Put<AppSettingsDto, AppSettingsDto>("api/settings", s);
    public Task<AiProbeDto> ProbeAiAsync(string? baseUrl, string? apiKey) => Get<AiProbeDto>(Q("api/settings/ai/probe", ("baseUrl", baseUrl), ("apiKey", apiKey)));
    public Task<BackupStatusDto> GetBackupStatusAsync() => Get<BackupStatusDto>("api/backup/status");

    public async Task<(byte[] Content, string FileName)> ExportBudgetAsync()
    {
        var r = await http.GetAsync("api/backup/export");
        await ThrowIfFailed(r);
        var name = r.Content.Headers.ContentDisposition?.FileNameStar ?? r.Content.Headers.ContentDisposition?.FileName ?? "budget.mybudget";
        return (await r.Content.ReadAsByteArrayAsync(), name.Trim('"'));
    }

    public async Task<BackupImportDto> ImportBudgetAsync(string fileName, Stream file)
    {
        using var form = new MultipartFormDataContent();
        using var content = new StreamContent(file);
        form.Add(content, "file", fileName);
        var r = await http.PostAsync("api/backup/import", form);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<BackupImportDto>(Json))!;
    }
    public Task<ChatResponseDto> ChatAsync(ChatRequest req) => Post<ChatRequest, ChatResponseDto>("api/ai/chat", req);
    public Task<AiSummaryDto> GetAiSummaryAsync() => Get<AiSummaryDto>("api/ai/summary");

    // Views
    public Task<TransferNeedsDto> GetTransferNeedsAsync(DateOnly? asOf = null)
        => Get<TransferNeedsDto>(asOf is { } d ? $"api/transfer-needs?asOf={d:yyyy-MM-dd}" : "api/transfer-needs");
    public Task<HomeDto> GetHomeAsync() => Get<HomeDto>("api/home");

    /// <summary>Builds a query string, leaving out null values (an empty "cardId=" fails nullable binding on the API).</summary>
    private static string Q(string path, params (string Name, object? Value)[] args)
    {
        var parts = args.Where(a => a.Value is not null).Select(a => $"{a.Name}={Uri.EscapeDataString(a.Value!.ToString()!)}").ToList();
        return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
    }

    private async Task<T> Get<T>(string url)
    {
        var r = await http.GetAsync(url);
        await ThrowIfFailed(r);   // carries the API's own message through, not just the status code
        return await r.Content.ReadFromJsonAsync<T>(Json) ?? throw new InvalidOperationException($"Empty response from {url}");
    }

    private Task<T> Post<T>(string url, T body) => Post<T, T>(url, body);

    private async Task<TOut> Post<TIn, TOut>(string url, TIn body)
    {
        var r = await http.PostAsJsonAsync(url, body, Json);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<TOut>(Json))!;
    }

    private Task<T> Put<T>(string url, T body) => Put<T, T>(url, body);

    private async Task<TOut> Put<TIn, TOut>(string url, TIn body)
    {
        var r = await http.PutAsJsonAsync(url, body, Json);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<TOut>(Json))!;
    }

    private async Task Delete(string url)
    {
        var r = await http.DeleteAsync(url);
        await ThrowIfFailed(r);
    }

    private async Task<T> Delete<T>(string url)
    {
        var r = await http.DeleteAsync(url);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task ThrowIfFailed(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode) return;
        var body = await r.Content.ReadAsStringAsync();
        throw new ApiException((int)r.StatusCode, Readable(body) ?? r.ReasonPhrase ?? "Request failed");
    }

    /// <summary>
    /// Minimal APIs return failures as a ProblemDetails document. Showing the raw JSON in the error
    /// banner buries the one sentence that was written for the reader, so pull it back out.
    /// </summary>
    private static string? Readable(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        if (!body.TrimStart().StartsWith('{')) return body;

        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var field in (ReadOnlySpan<string>)["detail", "title"])
                if (doc.RootElement.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var text = v.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
        }
        catch (JsonException) { /* not a problem document; show it as it came */ }

        return body;
    }
}

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

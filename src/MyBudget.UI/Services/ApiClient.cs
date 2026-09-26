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

    // Categories & bills
    public Task<List<CategoryDto>> GetCategoriesAsync() => Get<List<CategoryDto>>("api/categories");
    public Task<CategoryDto> SaveCategoryAsync(CategoryDto c) => c.Id == 0 ? Post("api/categories", c) : Put($"api/categories/{c.Id}", c);
    public Task<List<BillDto>> GetBillsAsync() => Get<List<BillDto>>("api/bills");
    public Task<BillDto> SaveBillAsync(BillDto b) => b.Id == 0 ? Post("api/bills", b) : Put($"api/bills/{b.Id}", b);
    public Task DeleteBillAsync(int id) => Delete($"api/bills/{id}");
    public Task<List<BillHistoryDto>> GetBillHistoryAsync(int year) => Get<List<BillHistoryDto>>($"api/bills/history?year={year}");
    public Task<BillPeriodDto> SaveBillPeriodAsync(int billId, DateOnly period, BillPeriodDto p) => Put($"api/bills/{billId}/periods/{period:yyyy-MM-dd}", p);
    public Task DeleteBillPeriodAsync(int billId, DateOnly period) => Delete($"api/bills/{billId}/periods/{period:yyyy-MM-dd}");
    public Task<List<UpcomingBillDto>> GetUpcomingBillsAsync(int days) => Get<List<UpcomingBillDto>>($"api/bills/upcoming?days={days}");

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
    public Task<List<CatalogEntryDto>> GetCardCatalogAsync() => Get<List<CatalogEntryDto>>("api/card-catalog");
    public Task<CardDto> AddCardFromCatalogAsync(string key, int? payingAccountId) => Post<object, CardDto>(Q($"api/cards/from-catalog/{key}", ("payingAccountId", payingAccountId)), new { });
    public Task<CardRewardsDto> GetCardRewardsAsync(int cardId) => Get<CardRewardsDto>($"api/cards/{cardId}/rewards");
    public Task<CardRewardsDto> SaveCardRewardsAsync(CardRewardsDto r) => Put($"api/cards/{r.CardId}/rewards", r);
    public Task<List<LoyaltyProgramDto>> GetLoyaltyProgramsAsync() => Get<List<LoyaltyProgramDto>>("api/loyalty-programs");
    public Task<LoyaltyProgramDto> SaveLoyaltyProgramAsync(LoyaltyProgramDto p) => p.Id == 0 ? Post("api/loyalty-programs", p) : Put($"api/loyalty-programs/{p.Id}", p);
    public Task DeleteLoyaltyProgramAsync(int id) => Delete($"api/loyalty-programs/{id}");
    public Task<List<CardSpendDto>> GetCardSpendAsync(int year, int? cardId = null) => Get<List<CardSpendDto>>(Q("api/card-spend", ("year", year), ("cardId", cardId)));
    public Task SaveCardSpendAsync(CardSpendDto s) => http.PutAsJsonAsync("api/card-spend", s, Json).ContinueWith(t => ThrowIfFailed(t.Result)).Unwrap();
    public Task<CategoryDto> SaveCategoryPlanAsync(CategoryDto c) => Put($"api/categories/{c.Id}/plan", c);
    public Task<RewardsReportDto> GetRewardsReportAsync(int year) => Get<RewardsReportDto>($"api/rewards/report?year={year}");

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
        => await http.GetFromJsonAsync<T>(url, Json) ?? throw new InvalidOperationException($"Empty response from {url}");

    private Task<T> Post<T>(string url, T body) => Post<T, T>(url, body);

    private async Task<TOut> Post<TIn, TOut>(string url, TIn body)
    {
        var r = await http.PostAsJsonAsync(url, body, Json);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<TOut>(Json))!;
    }

    private async Task<T> Put<T>(string url, T body)
    {
        var r = await http.PutAsJsonAsync(url, body, Json);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task Delete(string url)
    {
        var r = await http.DeleteAsync(url);
        await ThrowIfFailed(r);
    }

    private static async Task ThrowIfFailed(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode) return;
        var body = await r.Content.ReadAsStringAsync();
        throw new ApiException((int)r.StatusCode, string.IsNullOrWhiteSpace(body) ? r.ReasonPhrase ?? "Request failed" : body);
    }
}

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

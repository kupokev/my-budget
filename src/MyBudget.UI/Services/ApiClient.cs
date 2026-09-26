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
        => Get<List<TransferDto>>($"api/accounts/{id}/transfers?year={year}&month={month}");
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
    public Task<BillActualDto> SaveBillActualAsync(int billId, DateOnly period, BillActualDto a) => Put($"api/bills/{billId}/actuals/{period:yyyy-MM-dd}", a);
    public Task DeleteBillActualAsync(int billId, DateOnly period) => Delete($"api/bills/{billId}/actuals/{period:yyyy-MM-dd}");
    public Task<List<UpcomingBillDto>> GetUpcomingBillsAsync(int days) => Get<List<UpcomingBillDto>>($"api/bills/upcoming?days={days}");

    // Income
    public Task<List<IncomeSourceDto>> GetIncomeSourcesAsync() => Get<List<IncomeSourceDto>>("api/income-sources");
    public Task<IncomeSourceDto> SaveIncomeSourceAsync(IncomeSourceDto s) => s.Id == 0 ? Post("api/income-sources", s) : Put($"api/income-sources/{s.Id}", s);
    public Task DeleteIncomeSourceAsync(int id) => Delete($"api/income-sources/{id}");
    public Task<PayCalendarDto> GetPayCalendarAsync(int year) => Get<PayCalendarDto>($"api/income-sources/pay-calendar?year={year}");

    // Views
    public Task<TransferNeedsDto> GetTransferNeedsAsync(DateOnly? asOf = null)
        => Get<TransferNeedsDto>(asOf is { } d ? $"api/transfer-needs?asOf={d:yyyy-MM-dd}" : "api/transfer-needs");
    public Task<HomeDto> GetHomeAsync() => Get<HomeDto>("api/home");

    private async Task<T> Get<T>(string url)
        => await http.GetFromJsonAsync<T>(url, Json) ?? throw new InvalidOperationException($"Empty response from {url}");

    private async Task<T> Post<T>(string url, T body)
    {
        var r = await http.PostAsJsonAsync(url, body, Json);
        await ThrowIfFailed(r);
        return (await r.Content.ReadFromJsonAsync<T>(Json))!;
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

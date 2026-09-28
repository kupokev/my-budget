using System.Net;
using System.Net.Http.Json;
using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// A card's annual fee is money that will be spent, so it can be kept as a budget line — but only
/// when asked for. The tick on the card and the line itself have to stay in step in both directions:
/// unticking removes the line, and deleting the line unticks.
/// </summary>
public class CardFeeBudgetTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _api;
    public CardFeeBudgetTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Ticking_the_box_creates_a_line_named_after_the_card()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var card = await _api.Post("api/cards", Card("Surpass fee card", 150m, feeMonth: 3, account.Id, budget: true));

        Assert.NotNull(card.FeeBudgetLineId);

        var line = (await _api.Get<List<BudgetLineDto>>("api/budget")).Single(b => b.Id == card.FeeBudgetLineId);
        Assert.Equal("Surpass fee card", line.Name);
        Assert.Equal(150m, line.ProjectedAmount);
        Assert.Equal(BudgetFrequency.Annual, line.Frequency);
        Assert.Equal(3, line.AnchorDueDate!.Value.Month);
        Assert.Equal(PaymentMethodKind.Card, line.PaymentMethod);
        Assert.Equal(card.Id, line.PaymentCardId);

        var categories = await _api.Get<List<CategoryDto>>("api/categories");
        var labels = await _api.Get<List<LabelDto>>("api/labels");
        Assert.Equal(categories.Single(c => c.Name == "Fees").Id, line.CategoryId);
        Assert.Equal(labels.Single(l => l.Name == "Credit Card").Id, line.LabelId);
    }

    [Fact]
    public async Task A_zero_fee_never_gets_a_line_even_when_ticked()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();

        var card = await _api.Post("api/cards", Card("No fee card", 0m, feeMonth: 1, account.Id, budget: true));

        Assert.Null(card.FeeBudgetLineId);
        Assert.False(card.BudgetAnnualFee);          // the tick corrects itself rather than lying
    }

    [Fact]
    public async Task Unticking_removes_the_line()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var card = await _api.Post("api/cards", Card("Dropped fee card", 95m, feeMonth: 6, account.Id, budget: true));
        var lineId = card.FeeBudgetLineId!.Value;

        card.BudgetAnnualFee = false;
        var updated = await _api.Put($"api/cards/{card.Id}", card);

        Assert.Null(updated.FeeBudgetLineId);
        Assert.DoesNotContain(await _api.Get<List<BudgetLineDto>>("api/budget"), b => b.Id == lineId);
    }

    [Fact]
    public async Task Deleting_the_line_unticks_the_card()
    {
        var account = (await _api.Get<List<AccountDto>>("api/accounts")).First();
        var card = await _api.Post("api/cards", Card("Deleted line card", 550m, feeMonth: 9, account.Id, budget: true));

        var deleted = await _api.Client.DeleteAsync($"api/budget/{card.FeeBudgetLineId}");
        deleted.EnsureSuccessStatusCode();

        var after = (await _api.Get<List<CardDto>>("api/cards")).Single(c => c.Id == card.Id);
        Assert.False(after.BudgetAnnualFee);
        Assert.Null(after.FeeBudgetLineId);
    }

    [Fact]
    public async Task A_card_with_no_paying_account_says_so_rather_than_guessing_where_the_money_comes_from()
    {
        var refused = await _api.Client.PostAsJsonAsync("api/cards",
            Card("Orphan card", 99m, feeMonth: 2, payingAccountId: null, budget: true), ApiFixture.Json);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("account that pays", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public void A_long_card_name_falls_back_to_the_nickname_for_display()
    {
        var card = new CardDto { Name = "WELLS FARGO CASH WISE VISA PLATINUM® CARD", Nickname = "Wells Fargo" };
        Assert.Equal("Wells Fargo", card.Display);

        var unnamed = new CardDto { Name = "Freedom Unlimited" };
        Assert.Equal("Freedom Unlimited", unnamed.Display);
    }

    private static CardDto Card(string name, decimal fee, int feeMonth, int? payingAccountId, bool budget) => new()
    {
        Name = name, AnnualFee = fee, AnnualFeeMonth = feeMonth, PayingAccountId = payingAccountId,
        BudgetAnnualFee = budget, StatementDay = 1, DueDay = 15, IsActive = true,
    };
}

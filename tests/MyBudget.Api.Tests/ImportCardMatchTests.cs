using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// Two cards can end in the same four digits — "…81007" and "…01007" both end 1007 — which used to
/// throw while building the lookup and failed the whole import with a bare 500.
/// </summary>
public class ImportCardMatchTests
{
    private static readonly List<CategoryRule> NoRules = [];
    private static readonly List<Category> NoCategories = [];
    private static readonly List<BudgetLine> NoLines = [];

    [Fact]
    public void A_payment_to_an_unambiguous_card_names_that_card()
    {
        var row = new ImportRowDto { Description = "Payment to Chase card ending in 9039", Amount = -250m };

        ImportService.Suggest(row, NoRules, NoCategories, NoLines, Cards(("9039", "IHG Premier")));

        Assert.True(row.IsTransfer);
        Assert.Equal("Payment to IHG Premier", row.Merchant);
    }

    [Fact]
    public void A_payment_matching_two_cards_names_neither_and_says_why()
    {
        var row = new ImportRowDto { Description = "Payment to card ending in 1007", Amount = -500m };

        ImportService.Suggest(row, NoRules, NoCategories, NoLines,
            Cards(("1007", "Delta SkyMiles Gold"), ("1007", "Hilton Honors Surpass")));

        // Still certainly a card payment, but putting it against the wrong card would be worse than
        // leaving it for the person to pick.
        Assert.True(row.IsTransfer);
        Assert.Equal("Payment to card …1007", row.Merchant);
        Assert.Contains("2 cards end in 1007", row.SuggestionSource);
        Assert.Contains("Delta SkyMiles Gold", row.SuggestionSource);
        Assert.Contains("Hilton Honors Surpass", row.SuggestionSource);
    }

    private static Dictionary<string, IReadOnlyList<string>> Cards(params (string Last4, string Name)[] cards)
        => cards.GroupBy(c => c.Last4)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(c => c.Name).OrderBy(n => n).ToList());
}

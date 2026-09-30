using MyBudget.Contracts;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>
/// Pay landing in an account is neither spending nor a transfer: it is tagged to the income source so
/// the Paycheck screen can add up what actually arrived across the accounts the cheque splits into.
/// </summary>
public class PayrollDepositImportTests
{
    private static readonly List<CategoryRule> NoRules = [];
    private static readonly List<Category> NoCategories = [];
    private static readonly List<BudgetLine> NoLines = [];

    private static List<IncomeSource> Sources(params string[] names)
        => [.. names.Select((n, i) => new IncomeSource { Id = i + 1, Name = n })];

    [Fact]
    public void A_payroll_deposit_is_tagged_to_the_only_income_source()
    {
        var row = new ImportRowDto { Description = "DIRECT DEP PAYROLL 09/25", Amount = 1_284.17m };

        ImportService.Suggest(row, NoRules, NoCategories, NoLines, null, Sources("Day job"));

        Assert.Equal(1, row.IncomeSourceId);
        Assert.False(row.IsTransfer);
        Assert.Null(row.CategoryId);
        Assert.Contains("payroll deposit", row.SuggestionSource);
    }

    [Fact]
    public void With_several_jobs_the_named_one_wins()
    {
        var row = new ImportRowDto { Description = "PAYROLL ACME CORP", Merchant = "Payroll Acme Corp", Amount = 900m };

        ImportService.Suggest(row, NoRules, NoCategories, NoLines, null, Sources("Day job", "Acme Corp"));

        Assert.Equal(2, row.IncomeSourceId);
    }

    [Fact]
    public void With_several_jobs_and_no_name_match_it_is_left_to_be_picked()
    {
        var row = new ImportRowDto { Description = "DIRECT DEP", Amount = 900m };

        ImportService.Suggest(row, NoRules, NoCategories, NoLines, null, Sources("Day job", "Acme Corp"));

        // Guessing which job would put the deposit against the wrong cheque.
        Assert.Null(row.IncomeSourceId);
        Assert.Contains("pick the income source", row.SuggestionSource);
    }

    [Fact]
    public void Money_out_is_never_a_deposit_even_with_payroll_wording()
    {
        var row = new ImportRowDto { Description = "PAYROLL SERVICE FEE", Amount = -39m };

        ImportService.Suggest(row, NoRules, NoCategories, NoLines, null, Sources("Day job"));

        Assert.Null(row.IncomeSourceId);
    }

    [Fact]
    public void A_rule_can_tag_pay_so_the_next_import_arrives_marked()
    {
        var rules = new List<CategoryRule> { new() { Pattern = "DIRECT DEP", IncomeSourceId = 7 } };
        var row = new ImportRowDto { Description = "DIRECT DEP EMPLOYER", Amount = 1_000m };

        ImportService.Suggest(row, rules, NoCategories, NoLines, null, Sources("Day job"));

        Assert.Equal(7, row.IncomeSourceId);
    }
}

using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Engines.Tests;

public class SinkingFundAndTransferNeedsTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 26);

    [Fact]
    public void Semi_annual_car_insurance_accrues_one_sixth_per_month()
    {
        var line = new BudgetLine { Name = "Car insurance", Frequency = BudgetFrequency.SemiAnnual, ProjectedAmount = 612m, AnchorDueDate = new(2026, 3, 15), FundingAccountId = 1 };
        var a = SinkingFund.MonthlyAccrual(line, AsOf);
        Assert.Equal(102m, a.Monthly);
        Assert.Contains("× 2/yr ÷ 12", a.Formula);
    }

    [Fact]
    public void Annual_fee_accrues_one_twelfth_and_rounds_to_cents()
    {
        var line = new BudgetLine { Name = "Costco", Frequency = BudgetFrequency.Annual, ProjectedAmount = 65m, AnchorDueDate = new(2026, 11, 1), FundingAccountId = 1 };
        Assert.Equal(5.42m, SinkingFund.MonthlyAccrual(line, AsOf).Monthly);
    }

    [Fact]
    public void One_off_bill_spreads_over_months_until_due()
    {
        var line = new BudgetLine { Name = "Property tax", Frequency = BudgetFrequency.OneOff, ProjectedAmount = 3000m, AnchorDueDate = new(2026, 12, 31), FundingAccountId = 1 };
        var a = SinkingFund.MonthlyAccrual(line, AsOf);
        Assert.Equal(1000m, a.Monthly); // Sep→Dec = 3 months
        Assert.Contains("÷ 3 months", a.Formula);
    }

    [Fact]
    public void Card_paid_bill_rolls_up_to_its_funding_account_not_the_card()
    {
        const int automatedBills = 3, chaseMain = 2;
        var lines = new[]
        {
            new BudgetLine { Id = 1, Name = "Hulu", Frequency = BudgetFrequency.Monthly, ProjectedAmount = 18.99m, PaymentMethod = PaymentMethodKind.Card, PaymentCardId = 9, FundingAccountId = automatedBills },
            new BudgetLine { Id = 2, Name = "Water", Frequency = BudgetFrequency.Monthly, ProjectedAmount = 60m, PaymentMethod = PaymentMethodKind.Account, PaymentAccountId = automatedBills, FundingAccountId = automatedBills },
            new BudgetLine { Id = 3, Name = "Car insurance", Frequency = BudgetFrequency.SemiAnnual, ProjectedAmount = 612m, AnchorDueDate = new(2026, 3, 15), FundingAccountId = chaseMain },
            new BudgetLine { Id = 4, Name = "Old gym", Frequency = BudgetFrequency.Monthly, ProjectedAmount = 40m, FundingAccountId = chaseMain, EndDate = new(2026, 6, 30) },
        };

        var needs = TransferNeeds.Compute(lines, paychecksPerYear: 26, AsOf);

        var auto = Assert.Single(needs, n => n.AccountId == automatedBills);
        Assert.Equal(78.99m, auto.Monthly);
        Assert.Equal(36.46m, auto.PerPaycheck); // 78.99 × 12 / 26
        Assert.Contains(auto.Lines, l => l.LineName == "Hulu" && l.PaidByCard);

        var main = Assert.Single(needs, n => n.AccountId == chaseMain);
        Assert.Equal(102m, main.Monthly);
        Assert.DoesNotContain(main.Lines, l => l.LineName == "Old gym"); // ended before asOf
        Assert.DoesNotContain(needs, n => n.AccountId == 9); // the card is never a funding account
    }

    [Fact]
    public void Due_dates_for_semi_annual_bill_step_from_anchor_in_both_directions()
    {
        var line = new BudgetLine { Name = "Car insurance", Frequency = BudgetFrequency.SemiAnnual, ProjectedAmount = 612m, AnchorDueDate = new(2026, 3, 15), DueDay = 15, FundingAccountId = 1 };
        var due = BudgetDueDates.Between(line, new(2025, 1, 1), new(2026, 12, 31));
        Assert.Equal([new(2025, 3, 15), new(2025, 9, 15), new(2026, 3, 15), new(2026, 9, 15)], due);
    }

    [Fact]
    public void Due_date_override_replaces_that_months_generated_date_even_across_the_window_edge()
    {
        var line = new BudgetLine { Name = "Water", Frequency = BudgetFrequency.Monthly, DueDay = 20, ProjectedAmount = 60m, FundingAccountId = 1 };
        var overrides = new Dictionary<DateOnly, DateOnly>
        {
            [new(2026, 10, 1)] = new(2026, 10, 3),   // moved earlier within the month
            [new(2026, 9, 1)] = new(2026, 10, 1),    // September's line slipped into October
        };
        var due = BudgetDueDates.Between(line, new(2026, 10, 1), new(2026, 11, 30), overrides);
        Assert.Equal([new(2026, 10, 1), new(2026, 10, 3), new(2026, 11, 20)], due);
    }

    [Fact]
    public void Projected_override_for_the_month_changes_a_monthly_lines_accrual_and_says_so()
    {
        var electric = new BudgetLine { Id = 7, Name = "Electric", Frequency = BudgetFrequency.Monthly, ProjectedAmount = 140m, FundingAccountId = 1 };
        var needs = TransferNeeds.Compute([electric], 26, AsOf, new Dictionary<int, decimal> { [7] = 210m });
        var line = Assert.Single(Assert.Single(needs).Lines);
        Assert.Equal(210m, line.MonthlyAccrual);
        Assert.Contains("September 2026", line.Formula);
        Assert.Contains("default $140.00", line.Formula);
    }

    [Fact]
    public void Monthly_bill_on_the_31st_clamps_in_short_months()
    {
        var line = new BudgetLine { Name = "Rent", Frequency = BudgetFrequency.Monthly, DueDay = 31, ProjectedAmount = 1m, FundingAccountId = 1 };
        var due = BudgetDueDates.Between(line, new(2026, 2, 1), new(2026, 4, 30));
        Assert.Equal([new(2026, 2, 28), new(2026, 3, 31), new(2026, 4, 30)], due);
    }

    [Fact]
    public void A_variable_line_accrues_its_monthly_figure_and_never_falls_due()
    {
        var groceries = new BudgetLine { Id = 9, Name = "Groceries", Frequency = BudgetFrequency.Variable, ProjectedAmount = 700m, FundingAccountId = 1 };

        var accrual = SinkingFund.MonthlyAccrual(groceries, AsOf);
        Assert.Equal(700m, accrual.Monthly);
        Assert.Contains("planned per month", accrual.Formula);

        // No due date at all, so it never shows up in an upcoming window.
        Assert.Empty(BudgetDueDates.Between(groceries, new(2026, 1, 1), new(2026, 12, 31)));
        Assert.Equal(0, BudgetDueDates.OccurrencesPerYear(BudgetFrequency.Variable));
    }

    [Fact]
    public void A_variable_line_takes_a_month_override_like_any_other()
    {
        var holiday = new BudgetLine { Id = 10, Name = "Restaurants", Frequency = BudgetFrequency.Variable, ProjectedAmount = 600m, FundingAccountId = 1 };
        var accrual = SinkingFund.MonthlyAccrual(holiday, AsOf, projectedThisMonth: 950m);
        Assert.Equal(950m, accrual.Monthly);
        Assert.Contains("default $600.00", accrual.Formula);
    }
}

using Microsoft.EntityFrameworkCore;
using MyBudget.Api.Endpoints;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api;

/// <summary>ALT-1..6 plus housekeeping, computed on request from the same data the pages show. No push; the Home page is the inbox.</summary>
public sealed class AlertsService(BudgetDbContext db, InvestmentService investments, PaycheckService paychecks)
{
    public async Task<List<AlertDto>> ComputeAsync(DateOnly today, TransferNeedsDto needs, IReadOnlyList<UpcomingLineDto> upcoming, RewardsReportDto rewards, HsaPlanDto? hsa)
    {
        var alerts = new List<AlertDto>();
        var accounts = await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).Where(a => a.IsActive).ToListAsync();

        // ALT-1: a line is due soon and its funding account hasn't received this month's transfer need.
        foreach (var n in needs.Accounts.Where(n => n.LongShort < 0))
        {
            var soon = upcoming.Where(b => b.FundingAccount == n.AccountName && b.DueDate <= today.AddDays(7)).ToList();
            if (soon.Count == 0) continue;
            alerts.Add(new("transfer", AlertSeverity.Warning, $"{n.AccountName} is {Math.Abs(n.LongShort):C} short of this month's transfer",
                $"{string.Join(", ", soon.Select(b => $"{b.LineName} {b.Amount:C} on {b.DueDate:MMM d}"))} come out of it; moved {n.TransferredThisMonth:C} of {n.MonthlyNeed:C} so far.", "accounts"));
        }

        // ALT-2: balance below what the next two weeks of lines (plus the minimum) need.
        foreach (var a in accounts)
        {
            if (a.Balances.Count == 0 && a.Transactions.Count == 0) continue;
            var cur = BalanceMath.Of(a, today);
            var due = upcoming.Where(b => b.FundingAccount == a.Name && b.DueDate <= today.AddDays(14)).Sum(b => b.Amount);
            if (due > 0 && cur.Balance < due + a.MinimumBalance)
                alerts.Add(new("balance", AlertSeverity.Danger, $"{a.Name} balance {cur.Balance:C} won't cover {due:C} due in the next 14 days",
                    $"Keep {a.MinimumBalance:C} minimum; short by {due + a.MinimumBalance - cur.Balance:C} ({cur.Detail}).", "accounts"));
        }

        // ALT-3: rewards goals short or thresholds behind pace.
        foreach (var gap in rewards.Plan.Gaps)
            alerts.Add(new("rewards", AlertSeverity.Warning, $"{gap.Program} {gap.Tier} is {gap.ShortfallMonthly:C}/mo behind the spend it needs",
                gap.Alternatives.Count > 0 ? "Other ways: " + string.Join("; ", gap.Alternatives) : "No other path defined.", "rewards"));
        foreach (var t in rewards.Thresholds.Where(t => !t.Reached && !t.OnPace && t.Remaining > 0 && t.YtdSpend > 0))
            alerts.Add(new("rewards", AlertSeverity.Info, $"{t.CardName}: {t.Description} needs {t.RequiredMonthly:C}/mo", $"YTD pace lands around {t.ProjectedYearEnd:C} of {t.Amount:C}", "rewards"));

        // ALT-4: HSA behind the straight-line pace to the target.
        if (hsa is { AnnualLimit: > 0 })
        {
            var pace = Math.Round(hsa.Target * today.Month / 12m, 2);
            if (hsa.Contributed < pace && hsa.RemainingToTarget > 0)
                alerts.Add(new("hsa", AlertSeverity.Warning, $"HSA is {pace - hsa.Contributed:C} behind pace", $"Contributed {hsa.Contributed:C} of {hsa.Target:C}; {hsa.RecommendedMonthly:C}/month for the rest of the year catches up.", "hsa"));
        }

        // ALT-5: wash-sale windows open.
        try
        {
            var portfolio = await investments.PortfolioAsync(today, today.Year);
            foreach (var p in portfolio.Positions)
            foreach (var w in p.WashSales.Where(w => w.WindowStillOpen))
                alerts.Add(new("wash-sale", AlertSeverity.Info, $"{p.Holding.Ticker}: wash-sale window open until {w.WindowCloses:MMM d}", w.Message, "investments"));
        }
        catch (Exception ex) { alerts.Add(new("investments", AlertSeverity.Info, "Could not evaluate investments", ex.Message, "investments")); }

        // ALT-6: annual fee posting this month or next.
        foreach (var c in await db.Cards.Where(c => c.IsActive && c.AnnualFee > 0 && c.AnnualFeeMonth != null).ToListAsync())
        {
            var next = new DateOnly(today.Year, c.AnnualFeeMonth!.Value, 1);
            if (next < new DateOnly(today.Year, today.Month, 1)) next = next.AddYears(1);
            if (next <= today.AddDays(45))
                alerts.Add(new("fee", AlertSeverity.Info, $"{c.Name} annual fee {c.AnnualFee:C} posts in {next:MMMM}", "Decide whether the card is earning its keep (Cards → yearly cost vs value).", "cards"));
        }

        // Housekeeping.
        var uncategorized = await db.Transactions.CountAsync(t => t.CategoryId == null && !t.IsTransfer && t.Amount < 0);
        if (uncategorized > 0) alerts.Add(new("transactions", AlertSeverity.Info, $"{uncategorized} uncategorized transactions", "Spending totals are incomplete until they're filed.", "transactions"));
        var taxYear = await db.TaxYears.FirstOrDefaultAsync(t => t.Year == today.Year);
        if (taxYear is null) alerts.Add(new("tax", AlertSeverity.Warning, $"No {today.Year} tax tables", "Paycheck estimates fall back to the latest year available.", "tax-tables"));
        else if (!taxYear.Verified) alerts.Add(new("tax", AlertSeverity.Info, $"{today.Year} tax tables not verified", "Confirm them against Pub 15-T and the Missouri formula, then tick Verified.", "tax-tables"));

        return alerts.OrderBy(a => a.Severity == AlertSeverity.Danger ? 0 : a.Severity == AlertSeverity.Warning ? 1 : 2).ToList();
    }

    /// <summary>ACC-5: monthly expenses = line accruals + planned variable spend; compare marked accounts to 3–6 months of that.</summary>
    public async Task<RainyDayDto> RainyDayAsync(DateOnly today)
    {
        var lines = await db.BudgetLines.Include(b => b.Periods).Where(b => b.IsActive).ToListAsync();
        var dated = lines.Where(b => b.Frequency != BudgetFrequency.Variable).Sum(b => SinkingFund.MonthlyAccrual(b, today).Monthly);
        var variable = lines.Where(b => b.Frequency == BudgetFrequency.Variable).Sum(b => SinkingFund.MonthlyAccrual(b, today).Monthly);
        var monthly = Math.Round(dated + variable, 2);
        var marked = await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).Where(a => a.IsActive && a.IsRainyDayFund).ToListAsync();
        var balance = marked.Sum(a => BalanceMath.Of(a, today).Balance);
        var months = monthly == 0 ? 0 : Math.Round(balance / monthly, 1);
        var low = 3 * monthly; var comfort = Math.Round(low * 1.25m, 2); var high = 6 * monthly;
        var status = marked.Count == 0 || balance < low ? "Low" : balance < comfort ? "Marginal" : "Healthy";
        var verdict = marked.Count == 0 ? "No accounts marked as rainy-day fund (Accounts → edit → rainy-day)."
            : status == "Low" ? $"Below the 3-month minimum by {low - balance:C}."
            : status == "Marginal" ? $"Above the minimum, but less than 25% over it ({comfort:C})."
            : months <= 6 ? "More than 25% above the minimum; within the 3–6 month range." : $"Above 6 months by {balance - high:C}.";
        return new RainyDayDto(monthly, $"budget: {dated:C}/mo dated + {variable:C}/mo variable", balance, marked.Select(a => a.Name).ToList(), months, low, high, comfort, status, verdict);
    }
}

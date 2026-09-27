using System.Text;
using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

/// <summary>RPT-1 year-over-year, ACC-4 net worth, RPT-2 CSV export.</summary>
public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReports(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/reports");

        g.MapGet("/year-over-year", async (int? year, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            var lines = await db.Transactions.Include(t => t.Category).Where(t => !t.IsTransfer && t.Amount < 0 && (t.Date.Year == y || t.Date.Year == y - 1)).ToListAsync();
            var cats = lines.GroupBy(t => t.Category?.Name ?? "Uncategorized").Select(grp => Row(grp.Key,
                Enumerable.Range(1, 12).Select(m => -grp.Where(t => t.Date.Year == y && t.Date.Month == m).Sum(t => t.Amount)).ToList(),
                Enumerable.Range(1, 12).Select(m => -grp.Where(t => t.Date.Year == y - 1 && t.Date.Month == m).Sum(t => t.Amount)).ToList())).OrderByDescending(r => r.ThisYear).ToList();

            var periods = await db.BudgetPeriods.Include(p => p.BudgetLine).Where(p => p.ActualAmount != null && (p.Period.Year == y || p.Period.Year == y - 1)).ToListAsync();
            var lineRows = periods.GroupBy(p => p.BudgetLine!.Name).Select(grp => Row(grp.Key,
                Enumerable.Range(1, 12).Select(m => grp.Where(p => p.Period.Year == y && p.Period.Month == m).Sum(p => p.ActualAmount ?? 0)).ToList(),
                Enumerable.Range(1, 12).Select(m => grp.Where(p => p.Period.Year == y - 1 && p.Period.Month == m).Sum(p => p.ActualAmount ?? 0)).ToList())).OrderByDescending(r => r.ThisYear).ToList();

            return new YearOverYearDto(y, y - 1, cats, lineRows, cats.Sum(c => c.ThisYear), cats.Sum(c => c.LastYear), lineRows.Sum(b => b.ThisYear), lineRows.Sum(b => b.LastYear));
        });

        g.MapGet("/net-worth", async (BudgetDbContext db, TimeProvider clock) => await NetWorth(db, DateOnly.FromDateTime(clock.GetLocalNow().DateTime), history: true));

        var x = api.MapGroup("/export");
        x.MapGet("/transactions.csv", async (int? year, BudgetDbContext db) =>
        {
            var q = TransactionEndpoints.Query(db);
            if (year is { } y) q = q.Where(t => t.Date.Year == y);
            var rows = await q.OrderBy(t => t.Date).ToListAsync();
            var sb = new StringBuilder("Date,Posted,Source,Description,Merchant,Amount,Category,Budget line,Transfer,Notes\n");
            foreach (var t in rows)
                sb.Append(Csv(t.Date.ToString("yyyy-MM-dd"), t.PostedDate?.ToString("yyyy-MM-dd"), t.Account?.Name ?? t.Card?.Name, t.Description, t.Merchant, t.Amount.ToString("0.00"), t.Category?.Name, t.BudgetLine?.Name, t.IsTransfer ? "yes" : "", t.Notes));
            return Results.Text(sb.ToString(), "text/csv");
        });
        x.MapGet("/budget.csv", async (int? year, BudgetDbContext db, TimeProvider clock) =>
        {
            var y = year ?? clock.GetLocalNow().Year;
            var lines = await db.BudgetLines.Include(b => b.Periods).Include(b => b.Category).Include(b => b.FundingAccount).OrderBy(b => b.Name).ToListAsync();
            var sb = new StringBuilder("Budget line,Category,Funded from,Projected," + string.Join(",", Enumerable.Range(1, 12).Select(m => new DateOnly(y, m, 1).ToString("MMM"))) + ",Total\n");
            foreach (var b in lines)
            {
                var months = Enumerable.Range(1, 12).Select(m => b.Periods.FirstOrDefault(p => p.Period == new DateOnly(y, m, 1))?.ActualAmount).ToList();
                sb.Append(Csv([b.Name, b.Category?.Name, b.FundingAccount?.Name, b.ProjectedAmount.ToString("0.00"), .. months.Select(m => m?.ToString("0.00")), months.Sum(m => m ?? 0).ToString("0.00")]));
            }
            return Results.Text(sb.ToString(), "text/csv");
        });
        x.MapGet("/accounts.csv", async (BudgetDbContext db) =>
        {
            var accounts = await db.Accounts.Include(a => a.Balances).OrderBy(a => a.Name).ToListAsync();
            var sb = new StringBuilder("Account,Institution,Type,Latest balance,As of\n");
            foreach (var a in accounts)
            {
                var latest = a.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
                sb.Append(Csv(a.Name, a.Institution, a.Type.ToString(), latest?.Balance.ToString("0.00"), latest?.AsOf.ToString("yyyy-MM-dd")));
            }
            return Results.Text(sb.ToString(), "text/csv");
        });

        return api;
    }

    internal static async Task<NetWorthDto> NetWorth(BudgetDbContext db, DateOnly asOf, bool history)
    {
        var accounts = await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).Where(a => a.IsActive).ToListAsync();
        var cards = await db.Cards.Include(c => c.Balances).Where(c => c.IsActive).ToListAsync();
        var loans = await db.Loans.Include(l => l.Balances).Where(l => l.IsActive).ToListAsync();

        static (decimal, DateOnly?) Latest<T>(IEnumerable<T> items, Func<T, DateOnly> date, Func<T, decimal> amount, DateOnly asOf)
        {
            var b = items.Where(i => date(i) <= asOf).OrderByDescending(date).FirstOrDefault();
            return b is null ? (0m, null) : (amount(b), date(b));
        }

        var lines = new List<NetWorthLineDto>();
        decimal assets = 0, cardDebt = 0, loanDebt = 0;
        // Brokerage accounts with holdings are valued from the holdings (shares × latest price) instead of a typed snapshot.
        var holdings = await db.Holdings.Include(h => h.Trades).Where(h => h.IsActive).ToListAsync();
        var valuedAccounts = new HashSet<int>();
        foreach (var grp in holdings.GroupBy(h => h.AccountId))
        {
            decimal value = 0; DateOnly? priced = null; var any = false;
            foreach (var h in grp)
            {
                var shares = MyBudget.Engines.Investments.Portfolio.SharesHeldOn(h.Trades, asOf);
                if (shares <= 0) continue;
                var price = await db.Prices.Where(p => p.Ticker == h.Ticker && p.Date <= asOf).OrderByDescending(p => p.Date).FirstOrDefaultAsync();
                if (price is null) continue;
                any = true; value += Math.Round(shares * price.Price, 2); priced = priced is null || price.Date > priced ? price.Date : priced;
            }
            if (!any) continue;
            var acct = accounts.FirstOrDefault(a => a.Id == grp.Key);
            if (acct is null) continue;
            valuedAccounts.Add(acct.Id); assets += value; lines.Add(new(acct.Name + " (holdings)", "investments", value, priced));
        }
        foreach (var a in accounts.Where(a => !valuedAccounts.Contains(a.Id))) { var cur = BalanceMath.Of(a, asOf); assets += cur.Balance; lines.Add(new(a.Name, "account", cur.Balance, cur.SnapshotAsOf)); }
        foreach (var asset in await db.Assets.Include(x => x.Values).Where(x => x.IsActive).ToListAsync())
        {
            var (val, d) = Latest(asset.Values, v => v.AsOf, v => v.Value, asOf);
            if (d is null) continue;
            assets += val; lines.Add(new(asset.Name, asset.Kind.ToString().ToLowerInvariant(), val, d));
        }
        foreach (var c in cards) { var (bal, d) = Latest(c.Balances, b => b.AsOf, b => b.Balance, asOf); cardDebt += bal; if (d is not null) lines.Add(new(c.Name, "card", -bal, d)); }
        foreach (var l in loans)
        {
            var (bal, d) = Latest(l.Balances, b => b.AsOf, b => b.Balance, asOf);
            if (d is null) { bal = l.OriginalPrincipal; }
            loanDebt += bal; lines.Add(new(l.Name, "loan", -bal, d));
        }
        var total = assets - cardDebt - loanDebt;

        var points = new List<NetWorthPointDto>();
        if (history)
        {
            for (var i = 23; i >= 0; i--)
            {
                var monthEnd = new DateOnly(asOf.Year, asOf.Month, 1).AddMonths(-i + 1).AddDays(-1);
                if (monthEnd > asOf) monthEnd = asOf;
                var a = accounts.Where(x => !valuedAccounts.Contains(x.Id)).Sum(x => BalanceMath.Of(x, monthEnd).Balance)
                        + (await db.Assets.Include(x => x.Values).Where(x => x.IsActive).ToListAsync()).Sum(x => Latest(x.Values, v => v.AsOf, v => v.Value, monthEnd).Item1)
                        + lines.Where(l => l.Kind == "investments").Sum(l => l.Balance); // holdings valued at the latest price for every point (no price history walk)
                var c = cards.Sum(x => Latest(x.Balances, b => b.AsOf, b => b.Balance, monthEnd).Item1);
                var l = loans.Sum(x => { var (bal, d) = Latest(x.Balances, b => b.AsOf, b => b.Balance, monthEnd); return d is null && x.StartDate <= monthEnd ? x.OriginalPrincipal : bal; });
                points.Add(new(monthEnd, a, c, l, a - c - l));
            }
        }
        return new NetWorthDto(asOf, assets, cardDebt, loanDebt, total, lines.OrderByDescending(l => l.Balance).ToList(), points,
            $"accounts {assets:N2} − cards {cardDebt:N2} − loans {loanDebt:N2} = {total:N2} (latest balance on or before {asOf:yyyy-MM-dd}; holdings at latest price; home/vehicle values from Assets)");
    }

    private static YoyRowDto Row(string name, List<decimal> thisMonths, List<decimal> lastMonths)
    {
        var t = thisMonths.Sum(); var l = lastMonths.Sum();
        return new YoyRowDto(name, R(t), R(l), R(t - l), l == 0 ? null : Math.Round((t - l) / l, 4), thisMonths.Select(R).ToList(), lastMonths.Select(R).ToList());
    }

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    private static string Csv(params string?[] cells) => string.Join(",", cells.Select(c =>
    {
        var s = c ?? "";
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    })) + "\n";
}

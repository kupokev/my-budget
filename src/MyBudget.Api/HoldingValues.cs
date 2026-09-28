using Microsoft.EntityFrameworkCore;
using MyBudget.Data;

namespace MyBudget.Api;

/// <summary>
/// What an account's holdings are worth: shares held on a date × the latest price on or before it.
///
/// An investment account has no typed balance — its worth is whatever it holds — so this is the only
/// answer for one. It lives here because two screens need the same number: the net-worth report has
/// always used it, while the accounts list showed a dash for the very accounts holding the most money.
/// </summary>
public static class HoldingValues
{
    public readonly record struct Valued(decimal Value, DateOnly? PricedAsOf);

    /// <summary>Keyed by account id; an account with no priced holdings is absent rather than zero.</summary>
    public static async Task<Dictionary<int, Valued>> ByAccountAsync(BudgetDbContext db, DateOnly asOf, CancellationToken ct = default)
    {
        var holdings = await db.Holdings.Include(h => h.Trades).Where(h => h.IsActive).ToListAsync(ct);
        var result = new Dictionary<int, Valued>();

        foreach (var perAccount in holdings.GroupBy(h => h.AccountId))
        {
            decimal value = 0;
            DateOnly? priced = null;
            var any = false;

            foreach (var h in perAccount)
            {
                var shares = MyBudget.Engines.Investments.Portfolio.SharesHeldOn(h.Trades, asOf);
                if (shares <= 0) continue;

                var price = await db.Prices
                    .Where(p => p.Ticker == h.Ticker && p.Date <= asOf)
                    .OrderByDescending(p => p.Date)
                    .FirstOrDefaultAsync(ct);
                if (price is null) continue;

                any = true;
                value += Math.Round(shares * price.Price, 2);
                if (priced is null || price.Date > priced) priced = price.Date;
            }

            if (any) result[perAccount.Key] = new Valued(value, priced);
        }

        return result;
    }
}

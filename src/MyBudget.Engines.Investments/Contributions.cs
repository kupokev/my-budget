using MyBudget.Domain;

namespace MyBudget.Engines.Investments;

/// <summary>Money recorded into (positive) or out of (negative) one account on one date.</summary>
public sealed record RecordedContribution(int AccountId, DateOnly Date, decimal Amount);

/// <summary>
/// One account's net contributions on a date: what's recorded, plus the estimate standing in for the
/// time before recording starts. <see cref="RecordedFrom"/> is when that is — the earlier of the date
/// statements cover the account from and its first entry — or null when neither exists.
/// </summary>
public sealed record AccountContributions(int AccountId, decimal Estimated, DateOnly? RecordedFrom, decimal Recorded, decimal Total, string Formula);

/// <summary>
/// Money put into the investment accounts, as opposed to what it grew into.
///
/// Where a statement says what came in — a 401(k) purchase names its source, a brokerage lists
/// deposits and withdrawals — those recorded entries are the answer, and a stretch of a statement with
/// no deposit in it means none was made. For the time before an account's statements start there is
/// nothing to read, so it is estimated from what the account bought: a
/// running cash pot takes in sale proceeds and cash dividends, each buy is paid from it first, and only
/// the part the pot can't cover counts as new money. The estimate never falls, because nothing it can
/// see shows money leaving.
/// </summary>
public static class Contributions
{
    /// <summary>Net contributions across every account on <paramref name="date"/>.</summary>
    public static decimal On(IEnumerable<HoldingHistory> holdings, IEnumerable<RecordedContribution> recorded, DateOnly date,
        IReadOnlyDictionary<int, DateOnly>? coverFrom = null)
        => For(holdings, recorded, date, coverFrom).Sum(a => a.Total);

    /// <summary>
    /// Net contributions per account on <paramref name="date"/>, with how each was reached.
    /// <paramref name="coverFrom"/> gives, per account, the date its imported statements cover money in
    /// and out from.
    /// </summary>
    public static IReadOnlyList<AccountContributions> For(IEnumerable<HoldingHistory> holdings, IEnumerable<RecordedContribution> recorded, DateOnly date,
        IReadOnlyDictionary<int, DateOnly>? coverFrom = null)
    {
        var byAccount = holdings.ToLookup(h => h.AccountId);
        var entries = recorded.ToLookup(r => r.AccountId);
        var result = new List<AccountContributions>();
        foreach (var accountId in byAccount.Select(g => g.Key).Union(entries.Select(g => g.Key)).Order())
        {
            var mine = entries[accountId].ToList();
            DateOnly? firstEntry = mine.Count > 0 ? mine.Min(r => r.Date) : null;
            DateOnly? covered = coverFrom is not null && coverFrom.TryGetValue(accountId, out var c) ? c : null;
            DateOnly? recordedFrom = firstEntry is { } f && (covered is null || f < covered) ? f : covered;
            var cutoff = recordedFrom is { } from && from <= date ? from : date.AddDays(1);
            var (estimated, buys, paidFromPot) = Estimate(byAccount[accountId], cutoff);
            var counted = mine.Where(r => r.Date <= date).ToList();
            var recordedTotal = R(counted.Sum(r => r.Amount));

            var parts = new List<string>();
            if (buys > 0)
                parts.Add($"Estimated{(cutoff <= date ? $" before {cutoff:MMM d, yyyy}" : "")}: {estimated:C} — buys of {buys:C}, less {paidFromPot:C} paid from the account's own sale proceeds and cash dividends");
            if (recordedFrom is { } rf && rf <= date)
                parts.Add($"Recorded from {rf:MMM d, yyyy}: {recordedTotal:C} across {counted.Count} entr{(counted.Count == 1 ? "y" : "ies")}");
            result.Add(new AccountContributions(accountId, estimated, recordedFrom, recordedTotal, R(estimated + recordedTotal),
                parts.Count > 0 ? string.Join(". ", parts) + "." : "Nothing recorded or bought yet."));
        }
        return result;
    }

    /// <summary>The cash-pot estimate over everything the account did before <paramref name="before"/>.</summary>
    private static (decimal NewMoney, decimal Buys, decimal PaidFromPot) Estimate(IEnumerable<HoldingHistory> account, DateOnly before)
    {
        var events = account.SelectMany(h =>
                h.Trades.Where(t => t.Date < before && t.Kind != TradeKind.Reinvest && !IsAdjustment(t))
                    .Select(t => (t.Date, Amount: t.Kind == TradeKind.Buy ? -(t.Shares * t.Price + t.Fees) : t.Shares * t.Price - t.Fees))
                    .Concat(h.CashDividends.Where(d => d.Date < before)))
            .OrderBy(e => e.Date).ThenByDescending(e => e.Amount > 0); // a day's cash arrives before that day's buys
        decimal pot = 0, newMoney = 0, buys = 0;
        foreach (var (_, amount) in events)
        {
            if (amount >= 0) { pot += amount; continue; }
            var cost = -amount;
            var fromPot = Math.Min(pot, cost);
            pot -= fromPot;
            buys += cost;
            newMoney += cost - fromPot;
        }
        return (R(newMoney), R(buys), R(buys - newMoney));
    }

    /// <summary>A trade that only corrects a share count to a statement; no money moved.</summary>
    private static bool IsAdjustment(Trade t) => t.Notes?.StartsWith(TradeNotes.StatementAdjustment) ?? false;

    private static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

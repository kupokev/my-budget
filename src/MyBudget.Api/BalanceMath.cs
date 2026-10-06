using MyBudget.Domain;

namespace MyBudget.Api;

/// <summary>
/// An account's balance on a date = the latest snapshot on or before that date + every transaction (imported
/// lines and manual transfers alike) dated after the snapshot up to that date.
/// </summary>
public static class BalanceMath
{
    public sealed record Current(decimal Balance, DateOnly? SnapshotAsOf, decimal SnapshotBalance, decimal TransfersSince, int TransferCount, string Detail);

    public static Current Of(Account a, DateOnly asOf)
    {
        var snap = a.Balances.Where(b => b.AsOf <= asOf).OrderByDescending(b => b.AsOf).FirstOrDefault();
        var since = a.Transactions.Where(t => t.Counts && t.Date <= asOf && (snap is null || t.Date > snap.AsOf)).ToList();
        var moved = since.Sum(t => t.Amount);
        var balance = (snap?.Balance ?? 0m) + moved;
        var detail = snap is null
            ? (since.Count == 0 ? "no balance recorded" : $"no snapshot; {since.Count} transaction(s) totalling {moved:C}")
            : $"{snap.Balance:C} on {snap.AsOf:MMM d}" + (since.Count > 0 ? $" {(moved >= 0 ? "+" : "−")} {Math.Abs(moved):C} in {since.Count} transaction(s) since = {balance:C}" : "");
        return new Current(Math.Round(balance, 2), snap?.AsOf, snap?.Balance ?? 0m, Math.Round(moved, 2), since.Count, detail);
    }

    /// <summary>
    /// The account's balance just after each transaction, by the same rule as <see cref="Of"/>: a
    /// statement balance includes everything dated on or before its day. A row after the latest earlier
    /// statement runs forward from it; a row on or before the first statement runs back from it, since
    /// that statement already counts it. Rows on one day run in id order. A hand-entered row reconciled
    /// with an imported line doesn't count, so it gets no balance of its own.
    /// </summary>
    public static Dictionary<int, (decimal Balance, string Detail)> Running(IEnumerable<AccountBalance> snapshots, IEnumerable<Transaction> transactions)
    {
        var snaps = snapshots.OrderBy(b => b.AsOf).ToList();
        var counted = transactions.Where(t => t.Counts).OrderBy(t => t.Date).ThenBy(t => t.Id).ToList();
        // upTo[k] = the sum of the first k rows, so any run of rows is one subtraction.
        var upTo = new decimal[counted.Count + 1];
        for (var k = 0; k < counted.Count; k++) upTo[k + 1] = upTo[k] + counted[k].Amount;
        int FirstAfter(DateOnly d) { var k = 0; while (k < counted.Count && counted[k].Date <= d) k++; return k; }

        var result = new Dictionary<int, (decimal, string)>();
        for (var i = 0; i < counted.Count; i++)
        {
            var t = counted[i];
            if (snaps.LastOrDefault(b => b.AsOf < t.Date) is { } before)
            {
                var from = FirstAfter(before.AsOf);
                var sum = upTo[i + 1] - upTo[from];
                result[t.Id] = (Math.Round(before.Balance + sum, 2),
                    $"{before.Balance:C} statement balance on {before.AsOf:MMM d, yyyy} {Sign(sum)} {Math.Abs(sum):C} in {i + 1 - from} transaction(s) since, up to this one");
            }
            else if (snaps.FirstOrDefault(b => b.AsOf >= t.Date) is { } after)
            {
                var to = FirstAfter(after.AsOf);
                var back = upTo[to] - upTo[i + 1];
                result[t.Id] = (Math.Round(after.Balance - back, 2), to == i + 1
                    ? $"{after.Balance:C} statement balance on {after.AsOf:MMM d, yyyy}, which includes this transaction"
                    : $"{after.Balance:C} statement balance on {after.AsOf:MMM d, yyyy} {Sign(-back)} {Math.Abs(back):C} in {to - i - 1} later transaction(s) it already includes");
            }
            else
            {
                // No statement balance at all: the sum of everything so far.
                result[t.Id] = (Math.Round(upTo[i + 1], 2), $"no statement balance recorded; {i + 1} transaction(s) up to this one total {upTo[i + 1]:C}");
            }
        }
        return result;

        static string Sign(decimal d) => d >= 0 ? "+" : "−";
    }
}

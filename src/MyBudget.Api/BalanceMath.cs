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
}

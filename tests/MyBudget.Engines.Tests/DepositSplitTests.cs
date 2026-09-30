using MyBudget.Domain;

namespace MyBudget.Engines.Tests;

/// <summary>
/// A paycheck is rarely one deposit. Kevin's goes to four accounts: three fixed amounts and the rest
/// into the fourth — so a $2,150 credit in one account's statement is part of a cheque, not an
/// unexplained deposit, and a raise moves only the remainder.
/// </summary>
public class DepositSplitTests
{
    private static IncomeSource Source(params (int Account, decimal? Amount, bool Remainder)[] splits)
    {
        var source = new IncomeSource { Name = "Fox Rothschild LLP" };
        var order = 0;
        foreach (var (account, amount, remainder) in splits)
            source.DepositSplits.Add(new DepositSplit { AccountId = account, Amount = amount, IsRemainder = remainder, Order = order++ });
        return source;
    }

    [Fact]
    public void Fixed_amounts_come_out_first_and_the_rest_goes_to_the_remainder()
    {
        var source = Source((1, 1_000m, false), (2, 2_150m, false), (3, 250m, false), (4, null, true));

        var split = source.SplitOf(5_000m);

        Assert.Equal(1_000m, split[0].Amount);
        Assert.Equal(2_150m, split[1].Amount);
        Assert.Equal(250m, split[2].Amount);
        Assert.Equal(1_600m, split[3].Amount);      // whatever is left
        Assert.Equal(5_000m, split.Sum(s => s.Amount));
    }

    [Fact]
    public void A_raise_moves_only_the_remainder()
    {
        var source = Source((1, 1_000m, false), (2, 2_150m, false), (4, null, true));

        var before = source.SplitOf(5_000m);
        var after = source.SplitOf(5_400m);

        Assert.Equal(before[0].Amount, after[0].Amount);
        Assert.Equal(before[1].Amount, after[1].Amount);
        Assert.Equal(400m, after[2].Amount - before[2].Amount);
    }

    [Fact]
    public void A_short_cheque_fills_the_fixed_amounts_in_order_and_leaves_the_remainder_empty()
    {
        // The short cheque at a live-to-arrears switch: payroll cannot pay what is not there.
        var source = Source((1, 1_000m, false), (2, 2_150m, false), (4, null, true));

        var split = source.SplitOf(1_500m);

        Assert.Equal(1_000m, split[0].Amount);
        Assert.Equal(500m, split[1].Amount);        // partly filled
        Assert.Equal(0m, split[2].Amount);          // nothing left over
        Assert.Equal(1_500m, split.Sum(s => s.Amount));
    }

    [Fact]
    public void With_no_remainder_row_the_fixed_amounts_are_all_that_is_accounted_for()
    {
        var source = Source((1, 1_000m, false));

        Assert.Equal(1_000m, source.SplitOf(5_000m).Sum(s => s.Amount));
    }

    [Fact]
    public void An_inactive_split_is_left_out()
    {
        var source = Source((1, 1_000m, false), (4, null, true));
        source.DepositSplits[0].IsActive = false;

        var split = Assert.Single(source.SplitOf(5_000m));
        Assert.Equal(5_000m, split.Amount);
    }
}

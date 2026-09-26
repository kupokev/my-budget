using MyBudget.Domain;

namespace MyBudget.Data;

/// <summary>
/// Built-in catalog of common cards with earn rates and thresholds pre-filled (rewards open question:
/// pick from a catalog, correct after). Values are from public card terms as of 2026 and are a
/// starting point, not gospel: check the issuer's current terms after adding a card. Category names
/// are matched to (or created as) app categories.
/// </summary>
public static class CardCatalog
{
    public sealed record Earn(string? Category, decimal PointsPerDollar, decimal? Cap = null, string? Notes = null);
    public sealed record Threshold(decimal Amount, ThresholdRewardKind Kind, string Description, decimal? ValueDollars = null, string? Tier = null);
    public sealed record Entry(string Key, string Name, string Issuer, string Network, decimal AnnualFee, string? Program, decimal PointValueCents,
        IReadOnlyList<Earn> EarnRules, IReadOnlyList<Threshold> Thresholds, string? HoldTier = null, string? Notes = null);

    public const string Ihg = "IHG One Rewards";
    public const string Hilton = "Hilton Honors";

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("chase-ihg-premier", "Chase IHG One Rewards Premier", "Chase", "Mastercard", 99m, Ihg, 0.5m,
            [new("Hotels (IHG)", 10), new("Travel", 5), new("Gas", 5), new("Restaurants", 5), new(null, 3)],
            [new(20_000m, ThresholdRewardKind.Credit, "$100 statement credit + 10,000 points at $20K", 150m),
             new(40_000m, ThresholdRewardKind.Status, "IHG Diamond Elite at $40K", null, "Diamond")],
            HoldTier: "Platinum", Notes: "Platinum just for holding; anniversary free night (≤40K pts) not modelled"),
        new("amex-hilton-surpass", "Amex Hilton Honors Surpass", "American Express", "Amex", 150m, Hilton, 0.5m,
            [new("Hotels (Hilton)", 12), new("Restaurants", 6), new("Groceries", 6), new("Gas", 6), new("Online retail", 4), new(null, 3)],
            [new(15_000m, ThresholdRewardKind.FreeNight, "Free night reward at $15K", 250m),
             new(40_000m, ThresholdRewardKind.Status, "Hilton Diamond at $40K", null, "Diamond")],
            HoldTier: "Gold", Notes: "Gold just for holding"),
        new("amex-hilton-aspire", "Amex Hilton Honors Aspire", "American Express", "Amex", 550m, Hilton, 0.5m,
            [new("Hotels (Hilton)", 14), new("Travel", 7), new("Restaurants", 7), new(null, 3)],
            [],
            HoldTier: "Diamond", Notes: "Diamond just for holding (the alternate path to Diamond in Future Enhancements); annual free night + resort/airline credits not modelled"),
        new("chase-sapphire-preferred", "Chase Sapphire Preferred", "Chase", "Visa", 95m, null, 1.25m,
            [new("Travel", 2), new("Restaurants", 3), new("Streaming", 3), new(null, 1)],
            [], Notes: "Points valued at portal rate 1.25¢; 5x on Chase Travel portal bookings not modelled"),
        new("chase-freedom-unlimited", "Chase Freedom Unlimited", "Chase", "Visa", 0m, null, 1.0m,
            [new("Restaurants", 3), new("Drugstore", 3), new(null, 1.5m)], []),
        new("citi-double-cash", "Citi Double Cash", "Citi", "Mastercard", 0m, null, 1.0m, [new(null, 2)], []),
        new("capital-one-quicksilver", "Capital One Quicksilver", "Capital One", "Visa", 0m, null, 1.0m, [new(null, 1.5m)], []),
        new("capital-one-savorone", "Capital One SavorOne", "Capital One", "Mastercard", 0m, null, 1.0m,
            [new("Restaurants", 3), new("Groceries", 3), new("Streaming", 3), new("Entertainment", 3), new(null, 1)], []),
        new("capital-one-venture", "Capital One Venture", "Capital One", "Visa", 95m, null, 1.0m, [new(null, 2)], []),
        new("wells-fargo-active-cash", "Wells Fargo Active Cash", "Wells Fargo", "Visa", 0m, null, 1.0m, [new(null, 2)], []),
        new("discover-it", "Discover it Cash Back", "Discover", "Discover", 0m, null, 1.0m,
            [new(null, 1)], [], Notes: "5% rotating quarterly categories (cap $1,500/quarter) not modelled; add an earn rule for the current quarter's category"),
        new("amex-blue-cash-everyday", "Amex Blue Cash Everyday", "American Express", "Amex", 0m, null, 1.0m,
            [new("Groceries", 3, 6_000m), new("Gas", 3, 6_000m), new("Online retail", 3, 6_000m), new(null, 1)], []),
    ];

    /// <summary>Program definitions with tiers and non-card paths. 2026 rules; confirm each January.</summary>
    public static IReadOnlyList<LoyaltyProgram> Programs() =>
    [
        new()
        {
            Name = Ihg, PointValueCents = 0.5m, Priority = 1, TargetTier = "Diamond",
            Tiers = [new() { Name = "Club", Rank = 0 }, new() { Name = "Silver", Rank = 1 }, new() { Name = "Gold", Rank = 2 }, new() { Name = "Platinum", Rank = 3 }, new() { Name = "Diamond", Rank = 4 }],
            Paths =
            [
                new() { TierName = "Silver", Kind = StatusPathKind.Nights, Threshold = 10 },
                new() { TierName = "Gold", Kind = StatusPathKind.Nights, Threshold = 20 }, new() { TierName = "Gold", Kind = StatusPathKind.QualifyingPoints, Threshold = 40_000 },
                new() { TierName = "Platinum", Kind = StatusPathKind.Nights, Threshold = 40 }, new() { TierName = "Platinum", Kind = StatusPathKind.QualifyingPoints, Threshold = 60_000 },
                new() { TierName = "Diamond", Kind = StatusPathKind.Nights, Threshold = 70 }, new() { TierName = "Diamond", Kind = StatusPathKind.QualifyingPoints, Threshold = 120_000 },
            ],
            Notes = "Card paths (Platinum for holding Premier, Diamond at $40K) are added when the card is added from the catalog.",
        },
        new()
        {
            Name = Hilton, PointValueCents = 0.5m, Priority = 2, TargetTier = "Diamond",
            Tiers = [new() { Name = "Member", Rank = 0 }, new() { Name = "Silver", Rank = 1 }, new() { Name = "Gold", Rank = 2 }, new() { Name = "Diamond", Rank = 3 }],
            Paths =
            [
                new() { TierName = "Silver", Kind = StatusPathKind.Nights, Threshold = 10 }, new() { TierName = "Silver", Kind = StatusPathKind.Stays, Threshold = 4 },
                new() { TierName = "Gold", Kind = StatusPathKind.Nights, Threshold = 20 }, new() { TierName = "Gold", Kind = StatusPathKind.Stays, Threshold = 10 },
                new() { TierName = "Diamond", Kind = StatusPathKind.Nights, Threshold = 50 }, new() { TierName = "Diamond", Kind = StatusPathKind.Stays, Threshold = 25 },
                new() { TierName = "Diamond", Kind = StatusPathKind.ProgramSpend, Threshold = 11_500m, Notes = "2026 eligible Hilton spend path" },
            ],
            Notes = "2026 lowered night/stay/spend paths (LoyaltyLobby). Card paths added with the Surpass/Aspire cards.",
        },
    ];
}

namespace MyBudget.Engines.Import;

/// <summary>A statement line as parsed. Amount is signed from the owner's side: negative = money out.</summary>
public sealed record ParsedTransaction(
    DateOnly Date,
    DateOnly? PostedDate,
    decimal Amount,
    string Description,
    /// <summary>Institution-supplied id (OFX FITID) when present.</summary>
    string? ExternalId,
    /// <summary>Institution-supplied category text, if the file had one (kept as a hint only).</summary>
    string? SourceCategory,
    string? Memo);

public sealed record ParseResult(string Profile, IReadOnlyList<ParsedTransaction> Transactions, IReadOnlyList<string> Warnings);

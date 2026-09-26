namespace MyBudget.UI;

/// <summary>Where the API lives and the shared key that authenticates every call (ADR-0006).</summary>
public sealed class ApiClientOptions
{
    public required string BaseUrl { get; init; }
    public required string ApiKey { get; init; }
}

namespace MyBudget.Contracts;

/// <summary>Header carrying the single shared API key (ADR-0006).</summary>
public static class ApiKeyHeader
{
    public const string Name = "X-Api-Key";
}

namespace MyBudget.Domain;

/// <summary>
/// Settings the user can change from inside the app. A single row (Id 1).
///
/// These used to live in the API's appsettings.json, which was reasonable while the API was a service
/// you deployed. Now it runs inside the desktop app, so there is no file to edit and nowhere to put
/// connection details but the database.
/// </summary>
/// <summary>
/// Which API the model server speaks. Ollama's own and the OpenAI-compatible one differ in both path
/// and response shape, and the base URL doesn't reliably say which you have: a gateway may serve the
/// OpenAI routes under /api, /v1, or something else again.
/// </summary>
public enum AiApiStyle { Auto, Ollama, OpenAiCompatible }

public class AppSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Whether the assistant and the monthly narrative are available at all.</summary>
    public bool AiEnabled { get; set; }

    /// <summary>Where Ollama is listening, e.g. http://nas.local:11434.</summary>
    public string AiBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>The model to ask, e.g. llama3.1. It must support tool calling (ADR-0003).</summary>
    public string AiModel { get; set; } = "llama3.1";

    /// <summary>
    /// Bearer token for endpoints that want one. A local Ollama usually needs none; anything behind a
    /// gateway generally does. Stored in the local database file, sent only to the address above.
    /// </summary>
    public string? AiApiKey { get; set; }

    /// <summary>Set from Test connection, which reports what actually answered.</summary>
    public AiApiStyle AiApiStyle { get; set; } = AiApiStyle.Auto;

    /// <summary>How many times the model may call tools before it has to answer with what it has.</summary>
    public int AiMaxToolRounds { get; set; } = 6;
}

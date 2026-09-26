namespace MyBudget.UI.Services;

/// <summary>Host-provided "save this text as a file" (native dialog on Photino; Android later). Keeps host APIs out of the UI library (ADR-0004).</summary>
public interface IFileSaver
{
    /// <summary>Returns the saved path, or null if the user cancelled.</summary>
    Task<string?> SaveTextAsync(string suggestedFileName, string content);
}

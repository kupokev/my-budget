using MyBudget.UI.Services;
using Photino.NET;

namespace MyBudget.Desktop;

/// <summary>Native save dialog through Photino; the UI library only sees IFileSaver.</summary>
public sealed class PhotinoFileSaver : IFileSaver
{
    private PhotinoWindow? _window;
    public void Attach(PhotinoWindow window) => _window = window;

    public Task<string?> SaveTextAsync(string suggestedFileName, string content)
    {
        if (_window is null) return Task.FromResult<string?>(null);
        var path = _window.ShowSaveFile("Save as", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), [("CSV", ["*.csv"])]);
        if (string.IsNullOrEmpty(path)) return Task.FromResult<string?>(null);
        if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && suggestedFileName.EndsWith(".csv")) path += ".csv";
        File.WriteAllText(path, content);
        return Task.FromResult<string?>(path);
    }
}

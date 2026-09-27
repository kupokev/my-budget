using MyBudget.UI.Services;
using Photino.NET;

namespace MyBudget.Desktop;

/// <summary>Native save dialog through Photino; the UI library only sees IFileSaver.</summary>
public sealed class PhotinoFileSaver : IFileSaver
{
    private PhotinoWindow? _window;
    public void Attach(PhotinoWindow window) => _window = window;

    public Task<string?> SaveTextAsync(string suggestedFileName, string content)
        => Save(suggestedFileName, path => File.WriteAllText(path, content));

    public Task<string?> SaveBytesAsync(string suggestedFileName, byte[] content)
        => Save(suggestedFileName, path => File.WriteAllBytes(path, content));

    /// <summary>
    /// The dialog filters on whatever extension the caller suggested, and puts it back on if the
    /// typed name lost it, so a budget export doesn't land as an extensionless file.
    /// </summary>
    private Task<string?> Save(string suggestedFileName, Action<string> write)
    {
        if (_window is null) return Task.FromResult<string?>(null);

        var extension = Path.GetExtension(suggestedFileName);
        var filters = string.IsNullOrEmpty(extension)
            ? new[] { ("All files", new[] { "*.*" }) }
            : [(extension.TrimStart('.').ToUpperInvariant(), new[] { $"*{extension}" })];

        var path = _window.ShowSaveFile("Save as", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), filters);
        if (string.IsNullOrEmpty(path)) return Task.FromResult<string?>(null);
        if (!string.IsNullOrEmpty(extension) && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) path += extension;

        write(path);
        return Task.FromResult<string?>(path);
    }
}

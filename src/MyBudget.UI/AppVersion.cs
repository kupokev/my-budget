using System.Reflection;

namespace MyBudget.UI;

/// <summary>
/// Which build this is, shown next to the sidebar title so a bug report or an upgrade can be pinned
/// to a version. Release packages are stamped by <c>dotnet publish -p:Version</c>; a local build has
/// no meaningful number, so it says "dev" rather than the SDK's default 1.0.0, which would be a lie.
/// </summary>
public static class AppVersion
{
    public static string Display { get; } = Resolve();

    private static string Resolve()
    {
#if DEBUG
        return "dev";
#else
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version)) return string.Empty;

        // SourceLink appends "+<commit sha>", which is noise in a sidebar.
        var plus = version.IndexOf('+');
        return "v" + (plus > 0 ? version[..plus] : version);
#endif
    }
}

using MyBudget.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBudget.UI;
using Photino.Blazor;

// Dollar figures must not depend on whether the session has LANG set.
AppCulture.Apply();

// WebKitGTK under Wayland misbehaves for Photino; force X11 (XWayland) unless the user set a backend.
if (OperatingSystem.IsLinux()
    && string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GDK_BACKEND")))
{
    Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
}

// The API runs inside this process against a SQLite file under the user's data folder, so there is
// nothing to host and nothing to start first. Point MYBUDGET_API_URL at a server to use a shared one
// instead, which is how a phone would eventually talk to the same data.
var remoteUrl = Environment.GetEnvironmentVariable("MYBUDGET_API_URL");
MyBudget.Desktop.LocalApi? localApi = null;
ApiClientOptions apiOptions;

if (string.IsNullOrWhiteSpace(remoteUrl))
{
    localApi = new MyBudget.Desktop.LocalApi();
    try
    {
        await localApi.StartAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not open the database at {MyBudget.Desktop.LocalApi.DatabasePath}: {ex.Message}");
        return 1;
    }
    apiOptions = new ApiClientOptions { BaseUrl = localApi.BaseUrl, ApiKey = localApi.ApiKey };
    Console.WriteLine($"MyBudget: {MyBudget.Desktop.LocalApi.DatabasePath}");
}
else
{
    apiOptions = new ApiClientOptions
    {
        BaseUrl = remoteUrl,
        ApiKey = Environment.GetEnvironmentVariable("MYBUDGET_API_KEY") ?? "dev",
    };
}

var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
builder.Services.AddMyBudgetUI(apiOptions);
var fileSaver = new MyBudget.Desktop.PhotinoFileSaver();
builder.Services.AddSingleton<MyBudget.UI.Services.IFileSaver>(fileSaver);
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddFilter("System.Net.Http", LogLevel.Warning);
});

builder.RootComponents.Add<App>("app");

var app = builder.Build();
fileSaver.Attach(app.MainWindow);

app.MainWindow
    .SetLogVerbosity(0)
    .SetTitle("MyBudget")
    .SetSize(1400, 900)
    .SetUseOsDefaultSize(false)
    .SetUseOsDefaultLocation(true)
    .SetResizable(true);

// On Linux the window's icon is not really the app's to set: the desktop matches the window's
// WM_CLASS against a .desktop file and uses the icon named there. Setting it through the toolkit
// alone leaves the default in place, which is why this looked like it was being ignored.
//
// So do both: tell the window, and make sure a .desktop entry exists that claims this window. The
// installed package ships one; a dev build or an AppImage registers its own, named so it can't be
// mistaken for the installed app.
var icon = Path.Combine(AppContext.BaseDirectory, "wwwroot", "icons", "mybudget-linux.png");
if (File.Exists(icon))
{
    if (OperatingSystem.IsLinux()) RegisterDesktopEntry(icon);

    app.MainWindow.IconFile = icon;
    app.MainWindow.SetIconFile(icon);
    app.MainWindow.RegisterWindowCreatedHandler((_, _) => app.MainWindow.SetIconFile(icon));
}

AppDomain.CurrentDomain.UnhandledException += (_, error) =>
{
    try { app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString()); }
    catch { /* window may already be gone */ }
};

app.Run();

if (localApi is not null) await localApi.DisposeAsync();
return 0;

/// <summary>
/// Gives a build that runs outside the package a launcher of its own, so the desktop can put an icon on
/// its window. The installed package ships /usr/share/applications/mybudget.desktop and needs nothing.
///
/// Every build runs as "MyBudget.Desktop", so earlier versions wrote the same per-user file from the
/// installed app and from a dev build alike — whichever ran last owned it, and because a per-user entry
/// overrides the system one, the menu's "MyBudget" could open the dev build from the repo. Now a dev
/// build or an AppImage writes its own, distinctly named entry, and the shared old file is removed.
/// </summary>
static void RegisterDesktopEntry(string iconPath)
{
    try
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(exe)) return;

        var dir = Path.Combine(home, ".local", "share", "applications");
        File.Delete(Path.Combine(dir, "MyBudget.Desktop.desktop"));   // the shared entry older versions wrote

        // Installed from a package: the packaged entry already claims the window.
        if (AppContext.BaseDirectory.StartsWith("/usr/", StringComparison.Ordinal)) return;

        // An AppImage runs from a temporary mount; launch the image file itself, not the mount.
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        var (file, name, command) = string.IsNullOrWhiteSpace(appImage)
            ? ("mybudget-dev.desktop", "MyBudget (dev build)", exe)
            : ("mybudget-appimage.desktop", "MyBudget (AppImage)", appImage);

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), $"""
            [Desktop Entry]
            Version=1.0
            Type=Application
            Name={name}
            Comment=Personal budget
            Exec="{command.Replace("\"", "\\\"")}"
            Icon={iconPath}
            Terminal=false
            Categories=Office;Finance;
            StartupNotify=true
            StartupWMClass={Path.GetFileName(exe)}
            """);
    }
    catch
    {
        // An icon is not worth failing a launch over.
    }
}

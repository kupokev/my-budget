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
// installed package ships one, but a dev build runs from bin/ and matches nothing, so it registers
// one for itself pointing at the icon beside the binary.
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
/// Writes a per-user .desktop entry whose StartupWMClass matches this process, so the desktop can
/// tie the running window to an icon. Harmless when the packaged entry already covers it: this one
/// is named after the executable, so an installed run and a dev run do not fight over the same file.
/// </summary>
static void RegisterDesktopEntry(string iconPath)
{
    try
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(exe)) return;

        var processName = Path.GetFileName(exe);
        if (string.IsNullOrWhiteSpace(processName)) return;

        var dir = Path.Combine(home, ".local", "share", "applications");
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, processName + ".desktop"), $"""
            [Desktop Entry]
            Version=1.0
            Type=Application
            Name=MyBudget
            Comment=Personal budget
            Exec="{exe.Replace("\"", "\\\"")}"
            Icon={iconPath}
            Terminal=false
            Categories=Office;Finance;
            StartupNotify=true
            StartupWMClass={processName}
            """);
    }
    catch
    {
        // An icon is not worth failing a launch over.
    }
}

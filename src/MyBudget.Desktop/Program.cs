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

AppDomain.CurrentDomain.UnhandledException += (_, error) =>
{
    try { app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString()); }
    catch { /* window may already be gone */ }
};

app.Run();

if (localApi is not null) await localApi.DisposeAsync();
return 0;

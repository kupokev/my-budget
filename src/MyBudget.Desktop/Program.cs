using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBudget.UI;
using Photino.Blazor;

// WebKitGTK under Wayland misbehaves for Photino; force X11 (XWayland) unless the user set a backend.
if (OperatingSystem.IsLinux()
    && string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GDK_BACKEND")))
{
    Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
}

var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

var apiOptions = new ApiClientOptions
{
    BaseUrl = Environment.GetEnvironmentVariable("MYBUDGET_API_URL") ?? "http://localhost:5210",
    ApiKey = Environment.GetEnvironmentVariable("MYBUDGET_API_KEY") ?? "dev",
};
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

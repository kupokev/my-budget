using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

public static class BuildoutEndpoints
{
    public static RouteGroupBuilder MapBuildout(this RouteGroupBuilder api)
    {
        // kind: optional filter (e.g. "rewards" for the Cards page).
        api.MapGet("/alerts", async (string? kind, BudgetDbContext db, AlertsService alerts, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var needs = await ViewEndpoints.Needs(db, today);
            var upcoming = await BudgetEndpoints.Upcoming(db, today, 14);
            var rewards = await RewardsEndpoints.Report(db, today.Year, today);
            var hsa = await HsaEndpoints.PlanAsync(db, today.Year, today);
            var all = await alerts.ComputeAsync(today, needs, upcoming, rewards, hsa);
            return kind is null ? all : all.Where(a => a.Kind == kind).ToList();
        });

        api.MapGet("/rainy-day", async (AlertsService alerts, TimeProvider clock) => await alerts.RainyDayAsync(DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));

        // HOME-1, full version: everything at a glance.
        api.MapGet("/home/dashboard", async (BudgetDbContext db, AlertsService alerts, PaycheckService paychecks, AiOptionsProvider aiOptions, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var needs = await ViewEndpoints.Needs(db, today);
            var upcoming = await BudgetEndpoints.Upcoming(db, today, 14);
            var calendar = await IncomeEndpoints.PayCalendar(db, today.Year);
            var rewards = await RewardsEndpoints.Report(db, today.Year, today);
            var hsa = await HsaEndpoints.PlanAsync(db, today.Year, today);
            var spending = await SpendingEndpoints.Summary(db, today.Year, today.Month);
            var goals = await GoalEndpoints.AllProgress(db, paychecks, today);
            var nw = await ReportEndpoints.NetWorth(db, today, history: true);
            var prev = nw.History.Count >= 2 ? nw.History[^2].Total : (decimal?)null;
            // Home is a financial-health snapshot; rewards are something to dig into on purpose, so they stay off it.
            var alertList = (await alerts.ComputeAsync(today, needs, upcoming, rewards, hsa)).Where(a => a.Kind != "rewards").ToList();
            return new HomeDashboardDto(today, alertList, upcoming, calendar.PayDates.FirstOrDefault(d => d.Date >= today), needs.Accounts,
                spending.ThisMonth, spending.LastMonth, spending.Categories.Take(6).ToList(), spending.UncategorizedCount,
                rewards.Programs, goals, nw.Total, prev is { } p ? nw.Total - p : null, await alerts.RainyDayAsync(today), (await aiOptions.GetAsync()).Enabled);
        });

        var s = api.MapGroup("/settings");
        s.MapGet("/", async (BudgetDbContext db) => ToDto(await Settings(db)));
        s.MapPut("/", async (AppSettingsDto dto, BudgetDbContext db) =>
        {
            var row = await Settings(db);
            row.AiEnabled = dto.AiEnabled;
            row.AiBaseUrl = (dto.AiBaseUrl ?? "").Trim().TrimEnd('/');
            row.AiModel = (dto.AiModel ?? "").Trim();
            row.AiApiKey = string.IsNullOrWhiteSpace(dto.AiApiKey) ? null : dto.AiApiKey.Trim();
            row.AiMaxToolRounds = Math.Clamp(dto.AiMaxToolRounds, 1, 20);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(row));
        });
        // Probes a base URL without saving it, so the settings page can confirm the address and offer
        // the models that are actually installed rather than asking the name to be typed blind.
        // Probes an address without saving it. Tries Ollama's native endpoint first, then the
        // OpenAI-compatible one, because a local runtime behind a proxy usually serves the latter.
        s.MapGet("/ai/probe", async (string? baseUrl, string? apiKey, IHttpClientFactory factory, BudgetDbContext db) =>
        {
            var saved = await Settings(db);
            var url = (string.IsNullOrWhiteSpace(baseUrl) ? saved.AiBaseUrl : baseUrl!).Trim().TrimEnd('/');
            // Blank means "use the saved one", so the key doesn't have to be retyped to retest.
            var key = string.IsNullOrWhiteSpace(apiKey) ? saved.AiApiKey : apiKey;
            using var http = factory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(8);
            if (!string.IsNullOrWhiteSpace(key))
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

            var attempts = new List<string>();
            foreach (var (path, shape) in new[] { ("/api/tags", "ollama"), ("/v1/models", "openai") })
            {
                // An address already ending in /v1 is an OpenAI-compatible one; don't glue /api/tags onto it.
                if (shape == "ollama" && url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) continue;
                var probeUrl = shape == "openai" && url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? $"{url}/models" : $"{url}{path}";
                try
                {
                    using var r = await http.GetAsync(probeUrl);
                    var body = await r.Content.ReadAsStringAsync();
                    if (!r.IsSuccessStatusCode) { attempts.Add($"{probeUrl} answered {(int)r.StatusCode}"); continue; }
                    if (!LooksLikeJson(body))
                    {
                        attempts.Add($"{probeUrl} returned a web page, not JSON — usually a proxy or sign-in page in front of the model server");
                        continue;
                    }
                    var models = ReadModels(body, shape);
                    return new AiProbeDto(true, url, models, models.Count == 0 ? "Reachable, but no models are installed." : null);
                }
                catch (Exception ex) { attempts.Add($"{probeUrl}: {ex.Message}"); }
            }
            return new AiProbeDto(false, url, [], string.Join("; ", attempts));
        });

        var b = api.MapGroup("/backup");
        b.MapGet("/status", (BackupService backup) =>
        {
            if (!backup.Supported) return new BackupStatusDto(false, null, 0, false);
            var path = backup.DatabasePath;
            return new BackupStatusDto(true, path,
                File.Exists(path) ? new FileInfo(path).Length : 0,
                File.Exists(path + BackupService.PendingSuffix));
        });
        b.MapGet("/export", async (BackupService backup) =>
        {
            if (!backup.Supported) return Results.BadRequest("Export is only available for the local budget file.");
            var bytes = await backup.ExportAsync();
            return Results.File(bytes, "application/vnd.sqlite3", backup.SuggestedFileName);
        });
        // Every refusal comes back as a BackupImportDto so the page always has something to show,
        // rather than a raw error the user has to interpret.
        b.MapPost("/import", async (HttpRequest req, BackupService backup) =>
        {
            static IResult No(string why) => Results.Ok(new BackupImportDto(false, why, null, new Dictionary<string, int>()));

            if (!backup.Supported) return No("Restore is only available for the local budget file.");
            if (!req.HasFormContentType) return No("Send the budget file as multipart/form-data.");
            var form = await req.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return No("No file was uploaded.");

            await using var stream = file.OpenReadStream();
            var r = await backup.StageImportAsync(stream);
            return Results.Ok(new BackupImportDto(r.Ok, r.Problem, r.ReplacedCopyPath, r.Counts));
        }).DisableAntiforgery();

        var a = api.MapGroup("/ai");
        a.MapGet("/status", async (AiService svc) => await svc.StatusAsync());
        a.MapPost("/chat", async (ChatRequest req, AiService svc, TimeProvider clock) =>
            await PaycheckEndpoints.Guarded(() => svc.ChatAsync(req.Messages, DateOnly.FromDateTime(clock.GetLocalNow().DateTime))));
        a.MapGet("/summary", async (int? year, int? month, AiService svc, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            return await PaycheckEndpoints.Guarded(() => svc.SummaryAsync(year ?? today.Year, month ?? today.Month, today));
        });

        return api;
    }

    private static async Task<AppSettings> Settings(BudgetDbContext db)
        => await db.AppSettings.FirstOrDefaultAsync() ?? throw new InvalidOperationException("Settings row is missing; it is created at startup.");

    private static AppSettingsDto ToDto(AppSettings s) => new()
    {
        AiEnabled = s.AiEnabled, AiBaseUrl = s.AiBaseUrl, AiModel = s.AiModel, AiApiKey = s.AiApiKey, AiMaxToolRounds = s.AiMaxToolRounds,
    };

    private static bool LooksLikeJson(string body)
    {
        var t = body.TrimStart();
        return t.StartsWith('{') || t.StartsWith('[');
    }

    /// <summary>Ollama lists models under "models[].name"; an OpenAI-compatible server under "data[].id".</summary>
    private static List<string> ReadModels(string body, string shape)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var (arrayName, idName) = shape == "ollama" ? ("models", "name") : ("data", "id");
            if (!doc.RootElement.TryGetProperty(arrayName, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array) return [];
            return arr.EnumerateArray()
                .Select(m => m.TryGetProperty(idName, out var n) ? n.GetString() ?? "" : "")
                .Where(x => x.Length > 0).OrderBy(x => x).ToList();
        }
        catch { return []; }
    }
}

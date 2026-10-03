using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyBudget.Api.Endpoints;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api;

public sealed class AiOptions
{
    public const string Section = "Ai";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.1";
    public string? ApiKey { get; set; }
    public AiApiStyle Style { get; set; } = AiApiStyle.Auto;
    public int MaxToolRounds { get; set; } = 6;
}

/// <summary>
/// ADR-0003: the model only calls these fixed, tested functions and narrates what they return. It never sees the
/// database. Each tool is a thin wrapper over the same code the pages use, so an answer here matches the page.
/// </summary>
public sealed class AiTools(BudgetDbContext db, PaycheckService paychecks, InvestmentService investments, TimeProvider clock)
{
    public sealed record Tool(string Name, string Description, object Parameters);

    public static readonly IReadOnlyList<Tool> Catalog =
    [
        new("spend_by_category", "Spending by category for a month: this month vs last month, year to date, uncategorized count.", Obj(("year", "integer"), ("month", "integer"))),
        new("account_balances", "Latest balance of every account and card.", Obj()),
        new("upcoming_bills", "Dated budget lines due in the next N days with amounts and funding accounts.", Obj(("days", "integer"))),
        new("transfer_needs", "Required transfer per account this month and per paycheck, and what's been moved so far.", Obj()),
        new("bill_status", "Each budget line's projected vs actual amount for a month.", Obj(("year", "integer"), ("month", "integer"))),
        new("rewards_progress", "Loyalty status progress, card thresholds, spend plan gaps for a year.", Obj(("year", "integer"))),
        new("hsa_plan", "HSA limit, contributions, room, recommended pace for a year.", Obj(("year", "integer"))),
        new("goals_progress", "Every goal's current value, prorated target and status.", Obj()),
        new("net_worth", "Net worth from latest balances and its 24-month history.", Obj()),
        new("portfolio", "Investment positions, unrealized and realized gains, dividends, wash-sale warnings for a year.", Obj(("year", "integer"))),
    ];

    public async Task<string> InvokeAsync(string name, JsonElement args)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        int Int(string key, int fallback) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var v) && v.TryGetInt32(out var i) ? i : fallback;
        object result = name switch
        {
            "spend_by_category" => await SpendingEndpoints.Summary(db, Int("year", today.Year), Int("month", today.Month)),
            "account_balances" => await AccountBalancesAsync(today),
            "upcoming_bills" => await BudgetEndpoints.Upcoming(db, today, Math.Clamp(Int("days", 14), 1, 90)),
            "transfer_needs" => await ViewEndpoints.Needs(db, today),
            "bill_status" => await BudgetStatus(Int("year", today.Year), Int("month", today.Month)),
            "rewards_progress" => Slim(await RewardsEndpoints.Report(db, Int("year", today.Year), today)),
            "hsa_plan" => await HsaEndpoints.PlanAsync(db, Int("year", today.Year), today),
            "goals_progress" => await GoalEndpoints.AllProgress(db, paychecks, today),
            "net_worth" => await ReportEndpoints.NetWorth(db, today, history: true),
            "portfolio" => SlimPortfolio(await investments.PortfolioAsync(today, Int("year", today.Year))),
            _ => throw new KeyNotFoundException($"Unknown tool '{name}'."),
        };
        return JsonSerializer.Serialize(result, Json);
    }

    private async Task<object> BudgetStatus(int year, int month)
    {
        var period = new DateOnly(year, month, 1);
        var lines = await db.BudgetLines.Include(b => b.Periods).Include(b => b.Amounts).Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
        return lines.Select(b =>
        {
            var p = b.Periods.FirstOrDefault(x => x.Period == period);
            return new { b.Name, projected = p?.ProjectedAmount ?? b.ProjectedAmount, actual = p?.ActualAmount, dueDay = b.DueDay, paidVia = b.PaymentMethod.ToString() };
        });
    }

    /// <summary>
    /// What the model is told about balances. Investment accounts are valued from their holdings:
    /// they carry no typed balance, so without this the assistant would report an account holding six
    /// figures as zero and narrate it as fact.
    /// </summary>
    private async Task<object> AccountBalancesAsync(DateOnly today)
    {
        var held = await HoldingValues.ByAccountAsync(db, today);
        var accounts = await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).Where(a => a.IsActive).ToListAsync();
        var cards = await db.Cards.Include(c => c.Balances).Where(c => c.IsActive).ToListAsync();

        return new
        {
            accounts = accounts.Select(a =>
            {
                if (held.TryGetValue(a.Id, out var valued))
                    return new { a.Name, a.Type, balance = valued.Value, detail = $"holdings valued to {valued.PricedAsOf:yyyy-MM-dd}" };
                var current = BalanceMath.Of(a, today);
                return new { a.Name, a.Type, balance = current.Balance, detail = current.Detail };
            }),
            cards = cards.Select(c =>
            {
                var latest = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
                return new { c.Name, balance = latest?.Balance, asOf = latest?.AsOf };
            }),
        };
    }

    private static object Slim(RewardsReportDto r) => new
    {
        programs = r.Programs.Select(p => new { p.Name, p.CurrentTier, p.TargetTier, p.HeldTier, p.TargetReached, p.HowReached, planned = p.PlannedPath is { } pp ? new { pp.TierName, pp.Current, pp.Threshold, pp.RequiredMonthly } : null }),
        thresholds = r.Thresholds.Select(t => new { t.CardName, t.Description, t.Amount, t.YtdSpend, t.Remaining, t.RequiredMonthly, t.OnPace, t.Reached }),
        plan = new { r.Plan.MonthsLeft, r.Plan.ProjectedMonthly, gaps = r.Plan.Gaps, allocations = r.Plan.Allocations },
        earnings = r.Earnings.Select(e => new { e.CardName, e.YtdSpend, e.YtdPoints, e.YtdDollars, e.AnnualFee, e.NetValue }),
    };

    private static object SlimPortfolio(PortfolioDto p) => new
    {
        p.AsOf, p.TotalValue, p.TotalCost, p.TotalUnrealized, p.DividendsThisYear, p.RealizedShortTerm, p.RealizedLongTerm, tax = p.Tax,
        positions = p.Positions.Select(x => new { x.Holding.Ticker, x.Holding.AccountName, x.Shares, x.CostBasis, x.Price, x.MarketValue, x.UnrealizedGain, x.DividendsThisYear, washSales = x.WashSales.Select(w => w.Message) }),
    };

    private static object Obj(params (string Name, string Type)[] props) => new
    {
        type = "object",
        properties = props.ToDictionary(p => p.Name, p => (object)new { type = p.Type }),
        required = props.Select(p => p.Name).ToArray(),
    };

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
}

/// <summary>Ollama /api/chat with tools. Runs the tool loop, records every call for the audit trail, returns the final text.</summary>
public sealed class AiService(HttpClient http, AiOptionsProvider optionsProvider, AiTools tools)
{
    private const string System =
        "You are the assistant inside MyBudget, a personal budget app. Answer only from the results of the tools you call; " +
        "never guess or invent numbers. If no tool can answer the question, reply exactly: \"I can't answer that yet — there's no query function for it.\" " +
        "Be concise and plain. Money in US dollars. Today's date is {today}.";

    /// <summary>
    /// The candidate chat endpoints for a base URL, best guess first. Ollama serves /api/chat; an
    /// OpenAI-compatible server serves /v1/chat/completions, except when the base already ends in /v1
    /// or the gateway mounts the routes somewhere else — so both /v1/... and /... are offered.
    /// </summary>
    public static IReadOnlyList<(string Url, bool OpenAi)> ChatEndpoints(AiOptions o)
    {
        var url = o.BaseUrl.TrimEnd('/');
        var ollama = (Url: $"{url}/api/chat", OpenAi: false);
        var openAi = url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? [(Url: $"{url}/chat/completions", OpenAi: true)]
            : new[] { (Url: $"{url}/v1/chat/completions", OpenAi: true), (Url: $"{url}/chat/completions", OpenAi: true) };

        return o.Style switch
        {
            AiApiStyle.Ollama => [ollama],
            AiApiStyle.OpenAiCompatible => openAi,
            // Auto: a base ending in /v1 is plainly OpenAI-shaped; otherwise try Ollama, then fall back.
            _ when url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) => openAi,
            _ => [ollama, .. openAi],
        };
    }

    /// <summary>The assistant's own record of a call. OpenAI wants an id, a type and arguments as a JSON string.</summary>
    private static object AssistantCall(bool openAi, string id, string name, string argText) => openAi
        ? new { id, type = "function", function = new { name, arguments = argText } }
        : new { function = new { name, arguments = JsonSerializer.Deserialize<object>(argText) } };

    /// <summary>A tool's answer. OpenAI matches it to the call by id; Ollama matches by position.</summary>
    private static object ToolResult(bool openAi, string id, string result) => openAi
        ? new { role = "tool", tool_call_id = id, content = result }
        : new { role = "tool", content = result };

    private static string Snippet(string body)
    {
        var t = body.Trim().ReplaceLineEndings(" ");
        return t.Length == 0 ? "" : $": {(t.Length > 160 ? t[..160] + "…" : t)}";
    }

    private static void Authorize(HttpClient http, AiOptions o)
    {
        http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(o.ApiKey)
            ? null
            : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", o.ApiKey);
    }

    public async Task<AiStatusDto> StatusAsync(CancellationToken ct = default)
    {
        var options = await optionsProvider.GetAsync(ct);
        if (!options.Enabled) return new(false, options.BaseUrl, options.Model, AiTools.Catalog.Select(t => t.Name).ToList(), false, "Turn the assistant on under Admin → Settings and point it at your Ollama instance.");
        try
        {
            Authorize(http, options);
            var reachable = false;
            string? problem = null;
            foreach (var (url, _) in ChatEndpoints(options))
            {
                // Ask for the model list that sits beside each chat endpoint.
                var listUrl = url.Replace("/api/chat", "/api/tags").Replace("/chat/completions", "/models");
                using var r = await http.GetAsync(listUrl, ct);
                if (r.IsSuccessStatusCode) { reachable = true; break; }
                problem ??= $"{listUrl} answered {(int)r.StatusCode}.";
            }
            return new(true, options.BaseUrl, options.Model, AiTools.Catalog.Select(t => t.Name).ToList(), reachable, reachable ? null : problem);
        }
        catch (Exception ex) { return new(true, options.BaseUrl, options.Model, AiTools.Catalog.Select(t => t.Name).ToList(), false, ex.Message); }
    }

    /// <param name="system">Replaces the assistant's usual instructions, for a caller that supplies the facts itself.</param>
    /// <param name="useTools">False sends no function definitions: the answer comes from what is in the messages.</param>
    public async Task<ChatResponseDto> ChatAsync(IReadOnlyList<ChatMessageDto> history, DateOnly today, CancellationToken ct = default,
        string? system = null, bool useTools = true)
    {
        var options = await optionsProvider.GetAsync(ct);
        if (!options.Enabled) throw new InvalidOperationException("The assistant is off. Turn it on under Admin → Settings.");
        var messages = new List<object> { new { role = "system", content = (system ?? System).Replace("{today}", today.ToString("yyyy-MM-dd")) } };
        messages.AddRange(history.Select(m => new { role = m.Role, content = m.Content }));
        var toolDefs = AiTools.Catalog.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = t.Parameters } }).ToList();
        var calls = new List<ToolCallDto>();
        // Results keyed by call, so a repeat is answered from memory instead of rerunning the query.
        var answered = new Dictionary<string, string>();
        var toolsWithheld = !useTools;

        // Settled on the first round and reused, so a fallback costs one extra request per conversation.
        (string Url, bool OpenAi)? endpoint = null;

        for (var round = 0; round <= options.MaxToolRounds; round++)
        {
            Authorize(http, options);

            string? payload = null;
            var problems = new List<string>();
            foreach (var candidate in endpoint is { } known ? [known] : ChatEndpoints(options))
            {
                object body = (candidate.OpenAi, toolsWithheld) switch
                {
                    (true, false) => new { model = options.Model, messages, tools = toolDefs, stream = false, temperature = 0.1 },
                    (true, true) => new { model = options.Model, messages, stream = false, temperature = 0.1 },
                    (false, false) => new { model = options.Model, messages, tools = toolDefs, stream = false, options = new { temperature = 0.1 } },
                    _ => new { model = options.Model, messages, stream = false, options = new { temperature = 0.1 } },
                };
                HttpResponseMessage res;
                try
                {
                    res = await http.PostAsJsonAsync(candidate.Url, body, AiTools.Json, ct);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    // The model didn't answer in time. Say which round, because the summary makes
                    // several calls and a model that is merely slow will die on the first one.
                    throw new InvalidOperationException(
                        $"{options.Model} didn't answer within the time limit (round {round + 1} of up to {options.MaxToolRounds + 1}). " +
                        "A large model over a remote connection can be too slow for this; try a smaller one, or lower Tool rounds under Admin → Settings.");
                }
                using var _ = res;
                var text = await res.Content.ReadAsStringAsync(ct);
                if (res.IsSuccessStatusCode) { endpoint = candidate; payload = text; break; }
                problems.Add($"{candidate.Url} answered {(int)res.StatusCode}{Snippet(text)}");
            }

            if (payload is null)
            {
                // A "model not found" means the address and key are fine and only the name is wrong,
                // which is a different fix from the address being wrong.
                var modelRejected = problems.Any(p => p.Contains("model", StringComparison.OrdinalIgnoreCase)
                                                      && p.Contains("not found", StringComparison.OrdinalIgnoreCase));
                throw new InvalidOperationException(modelRejected
                    ? $"The server at {options.BaseUrl} doesn't have a model called \"{options.Model}\". " +
                      "Press Test connection under Admin → Settings and pick one from the list it returns."
                    : $"Couldn't reach a chat endpoint on {options.BaseUrl}. Tried: {string.Join("; ", problems)}. " +
                      "Set the API style explicitly under Admin → Settings if the address is right.");
            }

            var openAi = endpoint!.Value.OpenAi;
            using var doc = JsonDocument.Parse(payload);
            // Ollama returns one "message"; an OpenAI-compatible server wraps it in "choices[0]".
            var msg = openAi
                ? doc.RootElement.GetProperty("choices")[0].GetProperty("message")
                : doc.RootElement.GetProperty("message");
            var content = msg.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            if (!msg.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.ValueKind != JsonValueKind.Array || toolCalls.GetArrayLength() == 0)
                return new ChatResponseDto(content, calls, options.Model);

            var assistantCalls = new List<object>();
            var toolMessages = new List<object>();
            var allRepeats = true;

            foreach (var call in toolCalls.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? "";
                // Ollama returns arguments as an object; an OpenAI-compatible server returns them as a
                // JSON *string*. Taking the raw text of a string keeps its quotes and escapes, so
                // echoing it back double-encodes it and the server rejects the whole request.
                var raw = fn.TryGetProperty("arguments", out var a) ? a : default;
                using var argsDoc = raw.ValueKind == JsonValueKind.String
                    ? JsonDocument.Parse(string.IsNullOrWhiteSpace(raw.GetString()) ? "{}" : raw.GetString()!)
                    : null;
                var args = argsDoc?.RootElement ?? raw;
                var argText = args.ValueKind == JsonValueKind.Undefined ? "{}" : args.GetRawText();

                // An OpenAI-compatible server matches a result to its call by id. Without one the
                // model never sees an answer and asks the same question again, which is what a
                // seven-identical-calls transcript looks like.
                var id = call.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(id)) id = $"call_{calls.Count + 1}";

                var key = $"{name}|{argText}";
                if (answered.TryGetValue(key, out var cached))
                {
                    // Already asked and answered: hand back the same result rather than doing the work twice.
                    toolMessages.Add(ToolResult(openAi, id, cached));
                    assistantCalls.Add(AssistantCall(openAi, id, name, argText));
                    continue;
                }

                allRepeats = false;
                string result;
                try { result = await tools.InvokeAsync(name, args); }
                catch (Exception ex) { result = JsonSerializer.Serialize(new { error = ex.Message }); }
                answered[key] = result;

                calls.Add(new ToolCallDto(name, argText, result.Length > 4000 ? result[..4000] + "…" : result));
                assistantCalls.Add(AssistantCall(openAi, id, name, argText));
                toolMessages.Add(ToolResult(openAi, id, result));
            }

            // OpenAI expects no content alongside tool calls; Ollama is happy either way.
            messages.Add(openAi
                ? new { role = "assistant", content = (string?)null, tool_calls = assistantCalls }
                : (object)new { role = "assistant", content, tool_calls = assistantCalls });
            messages.AddRange(toolMessages);

            // A model that only repeats itself will never finish. Take the tools away and make it
            // answer from what it already has, rather than burning the remaining rounds.
            if (allRepeats && !toolsWithheld)
            {
                toolsWithheld = true;
                messages.Add(new
                {
                    role = "user",
                    content = "You already have the results you need above. Answer now in plain prose, using only those numbers. Do not call any more functions.",
                });
            }
        }
        // Out of rounds. Say which model and what it did, because the fix is usually a different
        // model rather than anything about the question.
        return new ChatResponseDto(
            $"{options.Model} kept asking for data instead of answering, and ran out of its {options.MaxToolRounds} tool rounds. " +
            "It looked up " + (calls.Count == 0 ? "nothing" : string.Join(", ", calls.Select(c => c.Name).Distinct())) +
            ". A model that follows tool-calling instructions more closely usually fixes this; raising Tool rounds under Admin → Settings rarely does.",
            calls, options.Model);
    }

    /// <summary>
    /// AI-1: a short note on the month, written from the dashboard's "What changed this month" lines and
    /// nothing else. The app has already done the arithmetic and picked the facts; the model only says
    /// which matter and why. It used to fetch five raw results itself, which overflowed a small model's
    /// context window — the instructions were the first thing dropped — and left it doing sums it got wrong.
    /// </summary>
    public async Task<AiSummaryDto> SummaryAsync(IReadOnlyList<HighlightDto> highlights, int year, int month, DateOnly today, CancellationToken ct = default)
    {
        var facts = highlights.Select(h => h.Text).ToList();
        // Each fact says whether it is good news or a concern: without that, a small model treats every
        // line as a problem and skips money being freed up — the most useful thing it could point out.
        static string Tag(string tone) => tone switch { "good" => "[good news] ", "bad" => "[concern] ", _ => "" };
        const string system =
            "You write a short note for the top of a personal budget dashboard. Use only the facts you are given. " +
            "Quote amounts exactly as written; never add, change or calculate numbers, and don't reinterpret what a number " +
            "means — \"a month\" stays a monthly amount. Be calm and matter-of-fact: never call anything critical, urgent or " +
            "pressing. Write plain sentences — no Markdown, no lists, no headings. Today's date is {today}.";
        var prompt = $"Here is what the budget app found for {new DateOnly(year, month, 1):MMMM yyyy}:\n" +
                     string.Join("\n", highlights.Select(h => "- " + Tag(h.Tone) + h.Text)) + "\n\n" +
                     "In at most four sentences, say what matters most this month. Include good news as well as concerns. " +
                     "Where one fact bears on another — money being freed up against an amount that needs covering — connect them. " +
                     "If nothing stands out, say that in one sentence.";
        var r = await ChatAsync([new ChatMessageDto { Role = "user", Content = prompt }], today, ct, system, useTools: false);
        return new AiSummaryDto(year, month, PlainText.FromMarkdown(r.Reply), facts, r.Model);
    }
}

/// <summary>
/// Reads the AI connection details out of the database rather than configuration, so they can be
/// changed from the Settings page while the app is running. Configuration is only a fallback for the
/// very first run, before the settings row exists — there is no appsettings file any more (ADR-0011).
/// </summary>
public sealed class AiOptionsProvider(BudgetDbContext db, AiOptions configured)
{
    public async Task<AiOptions> GetAsync(CancellationToken ct = default)
    {
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (row is null) return configured;
        return new AiOptions
        {
            Enabled = row.AiEnabled,
            BaseUrl = row.AiBaseUrl,
            Model = row.AiModel,
            ApiKey = row.AiApiKey,
            Style = row.AiApiStyle,
            MaxToolRounds = row.AiMaxToolRounds,
        };
    }
}

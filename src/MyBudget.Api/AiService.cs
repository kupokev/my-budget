using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyBudget.Api.Endpoints;
using MyBudget.Contracts;
using MyBudget.Data;

namespace MyBudget.Api;

public sealed class AiOptions
{
    public const string Section = "Ai";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.1";
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
            "account_balances" => new
            {
                accounts = (await db.Accounts.Include(a => a.Balances).Include(a => a.Transactions).Where(a => a.IsActive).ToListAsync()).Select(a => { var c = BalanceMath.Of(a, today); return new { a.Name, a.Type, balance = c.Balance, detail = c.Detail }; }),
                cards = (await db.Cards.Include(c => c.Balances).Where(c => c.IsActive).ToListAsync()).Select(c => new { c.Name, balance = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.Balance, asOf = c.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault()?.AsOf }),
            },
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
        var lines = await db.BudgetLines.Include(b => b.Periods).Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
        return lines.Select(b =>
        {
            var p = b.Periods.FirstOrDefault(x => x.Period == period);
            return new { b.Name, projected = p?.ProjectedAmount ?? b.ProjectedAmount, actual = p?.ActualAmount, dueDay = b.DueDay, paidVia = b.PaymentMethod.ToString() };
        });
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
public sealed class AiService(HttpClient http, AiOptions options, AiTools tools)
{
    private const string System =
        "You are the assistant inside MyBudget, a personal budget app. Answer only from the results of the tools you call; " +
        "never guess or invent numbers. If no tool can answer the question, reply exactly: \"I can't answer that yet — there's no query function for it.\" " +
        "Be concise and plain. Money in US dollars. Today's date is {today}.";

    public async Task<AiStatusDto> StatusAsync(CancellationToken ct = default)
    {
        if (!options.Enabled) return new(false, options.BaseUrl, options.Model, AiTools.Catalog.Select(t => t.Name).ToList(), false, "Set Ai:Enabled=true and Ai:BaseUrl/Ai:Model in appsettings to turn this on.");
        try
        {
            using var r = await http.GetAsync($"{options.BaseUrl.TrimEnd('/')}/api/tags", ct);
            return new(true, options.BaseUrl, options.Model, AiTools.Catalog.Select(t => t.Name).ToList(), r.IsSuccessStatusCode, r.IsSuccessStatusCode ? null : $"Ollama answered {(int)r.StatusCode}");
        }
        catch (Exception ex) { return new(true, options.BaseUrl, options.Model, AiTools.Catalog.Select(t => t.Name).ToList(), false, ex.Message); }
    }

    public async Task<ChatResponseDto> ChatAsync(IReadOnlyList<ChatMessageDto> history, DateOnly today, CancellationToken ct = default)
    {
        if (!options.Enabled) throw new InvalidOperationException("Local AI is not enabled (Ai:Enabled).");
        var messages = new List<object> { new { role = "system", content = System.Replace("{today}", today.ToString("yyyy-MM-dd")) } };
        messages.AddRange(history.Select(m => new { role = m.Role, content = m.Content }));
        var toolDefs = AiTools.Catalog.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = t.Parameters } }).ToList();
        var calls = new List<ToolCallDto>();

        for (var round = 0; round <= options.MaxToolRounds; round++)
        {
            var body = new { model = options.Model, messages, tools = toolDefs, stream = false, options = new { temperature = 0.1 } };
            using var res = await http.PostAsJsonAsync($"{options.BaseUrl.TrimEnd('/')}/api/chat", body, AiTools.Json, ct);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Ollama answered {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync(ct)}");
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var msg = doc.RootElement.GetProperty("message");
            var content = msg.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            if (!msg.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.ValueKind != JsonValueKind.Array || toolCalls.GetArrayLength() == 0)
                return new ChatResponseDto(content, calls, options.Model);

            var assistantCalls = new List<object>();
            var toolMessages = new List<object>();
            foreach (var call in toolCalls.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? "";
                var args = fn.TryGetProperty("arguments", out var a) ? a : default;
                var argText = args.ValueKind == JsonValueKind.Undefined ? "{}" : args.GetRawText();
                string result;
                try { result = await tools.InvokeAsync(name, args); }
                catch (Exception ex) { result = JsonSerializer.Serialize(new { error = ex.Message }); }
                calls.Add(new ToolCallDto(name, argText, result.Length > 4000 ? result[..4000] + "…" : result));
                assistantCalls.Add(new { function = new { name, arguments = args.ValueKind == JsonValueKind.Undefined ? new { } : JsonSerializer.Deserialize<object>(argText) } });
                toolMessages.Add(new { role = "tool", content = result });
            }
            messages.Add(new { role = "assistant", content, tool_calls = assistantCalls });
            messages.AddRange(toolMessages);
        }
        return new ChatResponseDto("I couldn't finish answering that within the tool budget.", calls, options.Model);
    }

    /// <summary>AI-1: the month's narrative, built only from pre-fetched tool results.</summary>
    public async Task<AiSummaryDto> SummaryAsync(int year, int month, DateOnly today, CancellationToken ct = default)
    {
        var prompt = $"Write a short monthly summary for {new DateOnly(year, month, 1):MMMM yyyy}: what changed and why, in plain English, 5 to 8 sentences. " +
                     $"Call spend_by_category for {year}-{month} and bill_status for {year}-{month}, then rewards_progress for {year}, goals_progress, and net_worth. " +
                     "Use only those results. Mention the biggest category changes, any budget lines over projection, status goals that are short, goals off track, and the net worth change.";
        var r = await ChatAsync([new ChatMessageDto { Role = "user", Content = prompt }], today, ct);
        return new AiSummaryDto(year, month, r.Reply, r.ToolCalls, r.Model);
    }
}

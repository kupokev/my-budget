using System.Net.Http.Json;
using System.Text.Json;

namespace MyBudget.Api;

public sealed record MarketData(string Ticker, IReadOnlyList<(DateOnly Date, decimal Close)> Prices, IReadOnlyList<(DateOnly ExDate, decimal Amount)> Dividends, decimal? LastPrice, string Source);

/// <summary>Free price + dividend history. Swappable; the app only ever needs closes and ex-date dividend amounts (ADR-0009).</summary>
public interface IMarketDataProvider
{
    Task<MarketData> FetchAsync(string ticker, DateOnly from, CancellationToken ct = default);
}

/// <summary>
/// Yahoo Finance's unauthenticated chart endpoint: daily closes and dividend events (ex-date + per-share amount).
/// No key, no cost. It is unofficial, so failures are reported and everything stays editable by hand.
/// </summary>
public sealed class YahooMarketDataProvider(HttpClient http) : IMarketDataProvider
{
    public async Task<MarketData> FetchAsync(string ticker, DateOnly from, CancellationToken ct = default)
    {
        var range = from <= DateOnly.FromDateTime(DateTime.Today).AddYears(-2) ? "5y" : from <= DateOnly.FromDateTime(DateTime.Today).AddYears(-1) ? "2y" : "1y";
        var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker.Trim().ToUpperInvariant())}?range={range}&interval=1d&events=div";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) MyBudget/1.0");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Yahoo returned {(int)res.StatusCode} for {ticker}.");
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var chart = doc.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            throw new InvalidOperationException($"Yahoo error for {ticker}: {err.GetProperty("description").GetString()}");
        var result = chart.GetProperty("result")[0];

        var prices = new List<(DateOnly, decimal)>();
        if (result.TryGetProperty("timestamp", out var ts) && result.GetProperty("indicators").GetProperty("quote")[0].TryGetProperty("close", out var closes))
        {
            for (var i = 0; i < ts.GetArrayLength(); i++)
            {
                var c = closes[i];
                if (c.ValueKind != JsonValueKind.Number) continue;
                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime);
                if (date >= from) prices.Add((date, Math.Round(c.GetDecimal(), 4)));
            }
        }
        var dividends = new List<(DateOnly, decimal)>();
        if (result.TryGetProperty("events", out var events) && events.TryGetProperty("dividends", out var divs))
            foreach (var d in divs.EnumerateObject())
            {
                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(d.Value.GetProperty("date").GetInt64()).UtcDateTime);
                if (date >= from) dividends.Add((date, Math.Round(d.Value.GetProperty("amount").GetDecimal(), 6)));
            }
        decimal? last = result.GetProperty("meta").TryGetProperty("regularMarketPrice", out var rmp) && rmp.ValueKind == JsonValueKind.Number ? Math.Round(rmp.GetDecimal(), 4) : prices.LastOrDefault().Item2;
        return new MarketData(ticker.ToUpperInvariant(), prices, dividends.OrderBy(x => x.Item1).ToList(), last, "Yahoo Finance chart API");
    }
}

using MyBudget.Api;
using MyBudget.Domain;

namespace MyBudget.Api.Tests;

/// <summary>Brokerage exports and Yahoo disagree about class shares; BRKB in a tax-lot file is BRK-B upstream.</summary>
public class MarketDataTickerTests
{
    [Theory]
    [InlineData("BRKB", "BRK-B")]
    [InlineData("BRKA", "BRK-A")]
    [InlineData("brkb", "BRK-B")]
    public void A_class_share_written_without_a_separator_gets_one(string input, string expected)
        => Assert.Equal(expected, YahooMarketDataProvider.ClassShareVariant(input));

    [Theory]
    [InlineData("VTI")]          // too short to be a class share
    [InlineData("BRK-B")]        // already has one
    [InlineData("BRK.B")]
    [InlineData("JEPQ")]         // ends in Q, not a class letter
    [InlineData("GOOGLE")]       // too long
    public void Anything_else_is_left_alone(string input)
        => Assert.Null(YahooMarketDataProvider.ClassShareVariant(input));
}

/// <summary>The payment cadence is read from the gaps between ex-dates rather than assumed.</summary>
public class PaymentIntervalTests
{
    private static List<DateOnly> Every(int months, int count, DateOnly from)
        => Enumerable.Range(0, count).Select(i => from.AddMonths(i * months)).ToList();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void A_regular_schedule_is_recognised(int months)
        => Assert.Equal(months, InvestmentService.PaymentIntervalMonths(Every(months, 5, new DateOnly(2025, 1, 15))));

    [Fact]
    public void One_payment_is_not_a_schedule()
        => Assert.Null(InvestmentService.PaymentIntervalMonths([new DateOnly(2026, 3, 1)]));

    [Fact]
    public void Spacing_that_matches_nothing_normal_is_left_alone()
        => Assert.Null(InvestmentService.PaymentIntervalMonths(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 20), new DateOnly(2026, 6, 9)]));

    [Fact]
    public void A_monthly_payer_whose_dates_wander_is_still_monthly()
    {
        // Real ex-dates drift by a few days; JEPQ's land anywhere from the 1st to the 3rd.
        List<DateOnly> dates = [new(2026, 1, 2), new(2026, 2, 1), new(2026, 3, 3), new(2026, 4, 1), new(2026, 5, 4)];
        Assert.Equal(1, InvestmentService.PaymentIntervalMonths(dates));
    }
}

/// <summary>
/// The dividend projection, against real figures: JEPQ pays monthly and the position was built up
/// during the year, so annualizing the year-to-date average would understate what the shares now held
/// will actually pay.
/// </summary>
public class DividendEstimateTests
{
    private static Holding Jepq()
    {
        // Eight monthly payments, Feb through Sep 2026, on a position that grew as lots were added.
        var perShare = new[] { 0.510m, 0.498m, 0.521m, 0.540m, 0.564m, 0.637m, 0.705m, 0.683m };
        var shares = new[] { 520m, 526m, 531m, 537m, 539.4m, 544.4m, 550.2m, 556.7m };
        var h = new Holding { Ticker = "JEPQ", Name = "JPMorgan Nasdaq Equity Premium Income ETF" };
        for (var i = 0; i < perShare.Length; i++)
        {
            var ex = new DateOnly(2026, i + 2, 1);
            h.Dividends.Add(new DividendPayment
            {
                ExDate = ex, PerShare = perShare[i], SharesHeld = shares[i],
                Amount = Math.Round(perShare[i] * shares[i], 2), Source = DataSource.Fetched,
            });
        }
        return h;
    }

    [Fact]
    public void The_rest_of_the_year_is_priced_at_the_latest_rate_against_the_shares_held_now()
    {
        var h = Jepq();
        var paid = h.Dividends.Sum(d => d.Amount);

        var (amount, yield, formula) = InvestmentService.EstimateDividends(
            h, shares: 563.1044m, marketValue: 34_484.51m, asOf: new DateOnly(2026, 9, 27), year: 2026);

        // Three payments left in the year: October, November, December.
        var expected = Math.Round(paid + 3 * Math.Round(0.683m * 563.1044m, 2), 2);
        Assert.Equal(expected, amount);
        Assert.Contains("plus 3 more monthly", formula);
        Assert.Contains("0.6830/share", formula);
        Assert.True(amount > paid);
        Assert.NotNull(yield);
    }

    [Fact]
    public void A_finished_year_reports_what_was_actually_paid()
    {
        var h = Jepq();
        var (amount, _, formula) = InvestmentService.EstimateDividends(
            h, shares: 563.1044m, marketValue: 34_484.51m, asOf: new DateOnly(2027, 3, 1), year: 2026);

        Assert.Equal(Math.Round(h.Dividends.Sum(d => d.Amount), 2), amount);
        Assert.Contains("2026 is complete", formula);
    }

    [Fact]
    public void A_single_payment_projects_nothing_and_says_why()
    {
        var h = new Holding { Ticker = "NEW", Name = "Recently bought" };
        h.Dividends.Add(new DividendPayment { ExDate = new(2026, 8, 1), PerShare = 0.25m, SharesHeld = 100m, Amount = 25m });

        var (amount, _, formula) = InvestmentService.EstimateDividends(
            h, shares: 100m, marketValue: 5_000m, asOf: new DateOnly(2026, 9, 27), year: 2026);

        Assert.Equal(25m, amount);
        Assert.Contains("not enough history", formula);
    }

    [Fact]
    public void Nothing_paid_this_year_estimates_nothing()
    {
        var (amount, _, formula) = InvestmentService.EstimateDividends(
            new Holding { Ticker = "NONE", Name = "No payer" }, shares: 10m, marketValue: 100m,
            asOf: new DateOnly(2026, 9, 27), year: 2026);

        Assert.Null(amount);
        Assert.Contains("No dividends recorded", formula);
    }
}

/// <summary>
/// Picking the chat endpoint. The base URL alone doesn't say which API a server speaks: a gateway can
/// serve the OpenAI routes under /api, which is what broke the monthly summary while the connection
/// test was passing.
/// </summary>
public class ChatEndpointTests
{
    [Fact]
    public void A_streamed_openai_reply_is_stitched_back_into_one_message()
    {
        // Open WebUI streams for some models even when asked not to.
        var sse = """
            data: {"choices":[{"delta":{"reasoning_content":"thinking","role":"assistant"}}]}

            data: {"choices":[{"delta":{"content":"Rent is "}}]}

            data: {"choices":[{"delta":{"content":"covered."}}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_a","function":{"name":"spend_by_category","arguments":"{\"mon"}}]}}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"th\":9}"}}]}}]}

            data: [DONE]
            """;

        using var doc = System.Text.Json.JsonDocument.Parse(AiService.Unstream(sse, openAi: true));
        var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        Assert.Equal("Rent is covered.", msg.GetProperty("content").GetString());
        var call = msg.GetProperty("tool_calls")[0];
        Assert.Equal("call_a", call.GetProperty("id").GetString());
        Assert.Equal("spend_by_category", call.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("{\"month\":9}", call.GetProperty("function").GetProperty("arguments").GetString());
    }

    [Fact]
    public void A_streamed_ollama_reply_is_stitched_back_into_one_message()
    {
        var ndjson = "{\"message\":{\"role\":\"assistant\",\"content\":\"All \"}}\n{\"message\":{\"content\":\"good.\"},\"done\":true}\n";

        using var doc = System.Text.Json.JsonDocument.Parse(AiService.Unstream(ndjson, openAi: false));
        Assert.Equal("All good.", doc.RootElement.GetProperty("message").GetProperty("content").GetString());
        Assert.False(doc.RootElement.GetProperty("message").TryGetProperty("tool_calls", out var tc) && tc.ValueKind == System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public void A_plain_reply_is_left_alone()
        => Assert.Equal("{\"message\":{\"content\":\"hi\"}}", AiService.Unstream("{\"message\":{\"content\":\"hi\"}}", openAi: false));

    private static List<string> Urls(string baseUrl, AiApiStyle style)
        => AiService.ChatEndpoints(new AiOptions { BaseUrl = baseUrl, Style = style }).Select(e => e.Url).ToList();

    [Fact]
    public void A_gateway_serving_openai_routes_under_api_is_reached_by_falling_back()
    {
        var urls = Urls("https://ai.example.com/api", AiApiStyle.Auto);

        // Ollama's path is tried first, then the OpenAI ones beneath the same base.
        Assert.Equal("https://ai.example.com/api/api/chat", urls[0]);
        Assert.Contains("https://ai.example.com/api/v1/chat/completions", urls);
        Assert.Contains("https://ai.example.com/api/chat/completions", urls);
    }

    [Fact]
    public void A_base_that_already_ends_in_v1_is_never_tried_as_ollama()
    {
        var urls = Urls("http://localhost:11434/v1", AiApiStyle.Auto);
        Assert.Equal(["http://localhost:11434/v1/chat/completions"], urls);
        Assert.DoesNotContain(urls, u => u.Contains("/api/chat"));
    }

    [Fact]
    public void A_plain_ollama_address_leads_with_its_own_api()
        => Assert.Equal("http://localhost:11434/api/chat", Urls("http://localhost:11434", AiApiStyle.Auto)[0]);

    [Theory]
    [InlineData(AiApiStyle.Ollama, "/api/chat")]
    [InlineData(AiApiStyle.OpenAiCompatible, "/chat/completions")]
    public void Pinning_the_style_stops_the_other_one_being_tried(AiApiStyle style, string expected)
    {
        var urls = Urls("https://ai.example.com/api", style);
        Assert.All(urls, u => Assert.Contains(expected, u));
        Assert.NotEmpty(urls);
    }

    [Fact]
    public void A_trailing_slash_does_not_double_up()
        => Assert.DoesNotContain(Urls("http://localhost:11434/", AiApiStyle.Auto), u => u.Contains("//api"));
}

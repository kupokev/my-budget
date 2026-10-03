namespace MyBudget.Api.Tests;

/// <summary>The monthly summary is shown as plain text, and the model formats it anyway — from a real reply.</summary>
public class PlainTextTests
{
    [Fact]
    public void Markdown_from_a_real_summary_becomes_plain_sentences()
    {
        const string reply = """
            **Current Net Worth (as of 2026‑10‑02)**
            - **Assets:** $515,552.19
            - **Loans:** $220,268.90
            - **Net Worth:** **$295,283.29**

            Your net worth is **$295,283.29**. You’re **On Track** for the 2026 net‑worth goal.

            ---

            ### Goals Progress
            | Goal | Target | Current | Status | Gap |
            |------|--------|---------|--------|-----|
            | **2026 Net Worth** | $300,000 | $295,283.29 | **On Track** | $0 |
            | **Attend Fabcon 2026** | — | — | **Done** | — |

            **Next Steps:**
            1. **Contribute** to the HSA to close the gap.
            2. Keep monitoring the 401(k) contribution target.
            """;

        var text = PlainText.FromMarkdown(reply);

        Assert.DoesNotContain("*", text);
        Assert.DoesNotContain("#", text);
        Assert.DoesNotContain("|", text);
        Assert.DoesNotContain("---", text);
        Assert.DoesNotContain("\n", text);
        Assert.Equal(
            "Current Net Worth (as of 2026‑10‑02). Assets: $515,552.19. Loans: $220,268.90. Net Worth: $295,283.29. " +
            "Your net worth is $295,283.29. You’re On Track for the 2026 net‑worth goal. " +
            "2026 Net Worth, $300,000, $295,283.29, On Track, $0. Attend Fabcon 2026, Done. " +
            "Next Steps: Contribute to the HSA to close the gap. Keep monitoring the 401(k) contribution target.",
            text);
    }

    [Fact]
    public void Plain_prose_is_left_alone()
    {
        const string prose = "Spending was $1,200 in October, down $300 from September. Groceries rose by $45.";
        Assert.Equal(prose, PlainText.FromMarkdown(prose));
    }
}

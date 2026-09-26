namespace MyBudget.Engines.Amortization;

public sealed record ScheduleRow(int Number, DateOnly Date, decimal Payment, decimal Interest, decimal Principal, decimal Extra, decimal Balance);

public sealed record Schedule(
    decimal StartingBalance, decimal AnnualRate, decimal ScheduledPayment, string PaymentFormula,
    IReadOnlyList<ScheduleRow> Rows, DateOnly? PayoffDate, decimal TotalInterest, decimal TotalPaid,
    /// <summary>Interest saved and months cut versus the same loan with no extra payments (only when extra > 0).</summary>
    decimal? InterestSavedByExtra, int? MonthsSavedByExtra);

/// <summary>Standard monthly-compounding amortization (DBT-1). Rates are annual fractions; interest is rounded to cents each month, as lenders do.</summary>
public static class Amortization
{
    /// <summary>Level payment that amortizes <paramref name="principal"/> over <paramref name="termMonths"/>.</summary>
    public static (decimal Payment, string Formula) Payment(decimal principal, decimal annualRate, int termMonths)
    {
        if (termMonths <= 0) throw new ArgumentOutOfRangeException(nameof(termMonths));
        var r = annualRate / 12m;
        if (r == 0) return (Round(principal / termMonths), $"{principal:N2} ÷ {termMonths}");
        var factor = (double)r * Math.Pow(1 + (double)r, termMonths) / (Math.Pow(1 + (double)r, termMonths) - 1);
        var payment = Round(principal * (decimal)factor);
        return (payment, $"{principal:N2} × r(1+r)^n ÷ ((1+r)^n − 1), r = {annualRate:P3}/12, n = {termMonths}");
    }

    /// <summary>
    /// Projects from a balance forward: each month interest = balance × rate/12, principal = payment − interest,
    /// plus any extra. Stops when paid off or after <paramref name="maxMonths"/>. Use it for the original
    /// schedule (balance = principal, first date = first payment) or for "from today" (balance = latest snapshot).
    /// </summary>
    public static Schedule Project(decimal balance, decimal annualRate, decimal payment, DateOnly firstPaymentDate, decimal extraMonthly = 0, int maxMonths = 600, string? paymentFormula = null)
    {
        var rows = new List<ScheduleRow>();
        var r = annualRate / 12m;
        var remaining = Round(balance);
        decimal totalInterest = 0, totalPaid = 0;
        var date = firstPaymentDate;
        for (var n = 1; n <= maxMonths && remaining > 0; n++)
        {
            var interest = Round(remaining * r);
            if (payment <= interest && extraMonthly <= 0)
                throw new InvalidOperationException($"Payment {payment:C} does not cover monthly interest {interest:C}; the balance never falls.");
            var scheduledPrincipal = Math.Min(remaining, payment - interest);
            var extra = Math.Min(remaining - scheduledPrincipal, extraMonthly);
            // A cents-rounded payment leaves a few dollars after the last scheduled month (0.4¢ × 360 months, compounded);
            // lenders fold that into the final payment, so absorb a residual under 1% of the payment.
            if (remaining - scheduledPrincipal - extra < payment * 0.01m) { scheduledPrincipal = remaining - extra; }
            var principal = scheduledPrincipal + extra;
            remaining = Round(remaining - principal);
            var paid = interest + principal;
            totalInterest += interest; totalPaid += paid;
            rows.Add(new ScheduleRow(n, date, Round(paid), interest, Round(scheduledPrincipal), Round(extra), remaining));
            date = date.AddMonths(1);
        }

        decimal? saved = null; int? monthsSaved = null;
        if (extraMonthly > 0)
        {
            var baseline = Project(balance, annualRate, payment, firstPaymentDate, 0, maxMonths, paymentFormula);
            saved = Round(baseline.TotalInterest - totalInterest);
            monthsSaved = baseline.Rows.Count - rows.Count;
        }

        return new Schedule(Round(balance), annualRate, payment, paymentFormula ?? $"{payment:N2} as given",
            rows, rows.Count > 0 && remaining == 0 ? rows[^1].Date : null, Round(totalInterest), Round(totalPaid), saved, monthsSaved);
    }

    /// <summary>The balance the original schedule says should remain after <paramref name="paymentsMade"/> payments (for comparing to a real statement).</summary>
    public static decimal ScheduledBalanceAfter(decimal principal, decimal annualRate, decimal payment, int paymentsMade)
    {
        var s = Project(principal, annualRate, payment, new DateOnly(2000, 1, 1), 0, Math.Max(1, paymentsMade));
        return paymentsMade <= 0 ? Round(principal) : (paymentsMade <= s.Rows.Count ? s.Rows[paymentsMade - 1].Balance : 0m);
    }

    private static decimal Round(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}

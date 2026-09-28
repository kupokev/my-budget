using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using Amort = MyBudget.Engines.Amortization.Amortization;

namespace MyBudget.Api.Endpoints;

public static class LoanEndpoints
{
    public static RouteGroupBuilder MapLoans(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/loans");

        g.MapGet("/", async (BudgetDbContext db) => (await db.Loans.Include(l => l.Balances).OrderBy(l => l.Name).ToListAsync()).Select(ToDto));

        g.MapPost("/", async (LoanDto dto, BudgetDbContext db) =>
        {
            var l = new Loan { Name = dto.Name };
            Apply(l, dto);
            db.Loans.Add(l);
            await db.SaveChangesAsync();
            return Results.Created($"/api/loans/{l.Id}", ToDto(l));
        });

        g.MapPut("/{id:int}", async (int id, LoanDto dto, BudgetDbContext db) =>
        {
            var l = await db.Loans.Include(x => x.Balances).FirstOrDefaultAsync(x => x.Id == id);
            if (l is null) return Results.NotFound();
            Apply(l, dto);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(l));
        });

        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var l = await db.Loans.FindAsync(id);
            if (l is null) return Results.NotFound();
            db.Loans.Remove(l);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:int}/balances", async (int id, LoanBalanceDto dto, BudgetDbContext db) =>
        {
            if (await db.Loans.FindAsync(id) is null) return Results.NotFound();
            var b = await db.Set<LoanBalance>().FirstOrDefaultAsync(x => x.LoanId == id && x.AsOf == dto.AsOf)
                    ?? db.Set<LoanBalance>().Add(new LoanBalance { LoanId = id, AsOf = dto.AsOf }).Entity;
            b.Balance = dto.Balance;
            await db.SaveChangesAsync();
            return Results.Ok(new LoanBalanceDto { Id = b.Id, LoanId = id, AsOf = b.AsOf, Balance = b.Balance });
        });

        // DBT-1: schedule from the latest balance snapshot (or the original principal), payoff date, interest remaining,
        // what extra principal saves, and what the original schedule says the balance should be today.
        g.MapGet("/{id:int}/projection", async (int id, decimal? extra, BudgetDbContext db, TimeProvider clock) =>
        {
            var l = await db.Loans.Include(x => x.Balances).FirstOrDefaultAsync(x => x.Id == id);
            if (l is null) return Results.NotFound();
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

            var (payment, formula) = l.ScheduledPayment is { } sp ? (sp, $"{sp:N2} as entered") : Amort.Payment(l.OriginalPrincipal, l.AnnualRate, l.TermMonths);
            var latest = l.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
            var known = LoanBalanceMath.Of(l, today);
            var fromBalance = known.Balance;
            var fromDate = known.AsOf ?? l.StartDate;
            var firstPayment = new DateOnly(fromDate.Year, fromDate.Month, 1).AddMonths(1);
            var extraMonthly = extra ?? l.ExtraMonthlyPayment;

            try
            {
                var s = Amort.Project(fromBalance, l.AnnualRate, payment, firstPayment, extraMonthly, paymentFormula: formula);
                var paymentsMade = Math.Max(0, (today.Year - l.StartDate.Year) * 12 + today.Month - l.StartDate.Month);
                var scheduledNow = paymentsMade > 0 ? Amort.ScheduledBalanceAfter(l.OriginalPrincipal, l.AnnualRate, payment, paymentsMade) : l.OriginalPrincipal;
                return Results.Ok(new LoanProjectionDto(l.Id, l.Name, payment, formula, extraMonthly, fromBalance, fromDate,
                    latest is null ? "original principal (no balance snapshot yet)" : $"balance snapshot {latest.AsOf:yyyy-MM-dd}",
                    s.PayoffDate, s.Rows.Count, s.TotalInterest, s.TotalPaid, s.InterestSavedByExtra, s.MonthsSavedByExtra,
                    scheduledNow, paymentsMade,
                    s.Rows.Select(r => new ScheduleRowDto(r.Number, r.Date, r.Payment, r.Interest, r.Principal, r.Extra, r.Balance)).ToList()));
            }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });

        return api;
    }

    private static LoanDto ToDto(Loan l)
    {
        var latest = l.Balances.OrderByDescending(b => b.AsOf).FirstOrDefault();
        var current = LoanBalanceMath.Of(l, DateOnly.FromDateTime(DateTime.Today));
        return new()
        {
            Id = l.Id, Name = l.Name, Kind = l.Kind, Lender = l.Lender, AccountNumber = l.AccountNumber, OriginalPrincipal = l.OriginalPrincipal, AnnualRatePercent = l.AnnualRate * 100m,
            TermMonths = l.TermMonths, StartDate = l.StartDate, ScheduledPayment = l.ScheduledPayment, ExtraMonthlyPayment = l.ExtraMonthlyPayment,
            BudgetLineId = l.BudgetLineId, AssetId = l.AssetId, Notes = l.Notes, IsActive = l.IsActive, LatestBalance = latest?.Balance, LatestBalanceAsOf = latest?.AsOf,
            EffectiveBalance = current.Balance, BalanceIsEstimate = current.IsEstimate,
        };
    }

    private static void Apply(Loan l, LoanDto d)
    {
        l.Name = d.Name.Trim(); l.Kind = d.Kind; l.Lender = d.Lender; l.AccountNumber = Mapping.Clean(d.AccountNumber); l.OriginalPrincipal = d.OriginalPrincipal; l.AnnualRate = d.AnnualRatePercent / 100m;
        l.TermMonths = d.TermMonths; l.StartDate = d.StartDate; l.ScheduledPayment = d.ScheduledPayment; l.ExtraMonthlyPayment = d.ExtraMonthlyPayment;
        l.BudgetLineId = d.BudgetLineId; l.AssetId = d.AssetId; l.Notes = d.Notes; l.IsActive = d.IsActive;
    }
}

using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Paycheck;

namespace MyBudget.Api.Endpoints;

/// <summary>INC-8: 1099 receipts, estimated payments made, and the set-aside estimate with a quarterly schedule.</summary>
public static class SideIncomeEndpoints
{
    public static RouteGroupBuilder MapSideIncome(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/side-income");

        g.MapGet("/receipts", async (int? year, BudgetDbContext db) =>
        {
            var q = db.IncomeReceipts.AsQueryable();
            if (year is { } y) q = q.Where(r => r.Date.Year == y);
            return (await q.OrderByDescending(r => r.Date).ToListAsync()).Select(r => new IncomeReceiptDto { Id = r.Id, IncomeSourceId = r.IncomeSourceId, Date = r.Date, Amount = r.Amount, Notes = r.Notes });
        });
        g.MapPost("/receipts", async (IncomeReceiptDto dto, BudgetDbContext db) =>
        {
            var r = new IncomeReceipt { IncomeSourceId = dto.IncomeSourceId, Date = dto.Date, Amount = dto.Amount, Notes = dto.Notes };
            db.IncomeReceipts.Add(r);
            await db.SaveChangesAsync();
            return Results.Created($"/api/side-income/receipts/{r.Id}", new IncomeReceiptDto { Id = r.Id, IncomeSourceId = r.IncomeSourceId, Date = r.Date, Amount = r.Amount, Notes = r.Notes });
        });
        // A payment logged with the wrong date or amount is corrected, not deleted and retyped.
        g.MapPut("/receipts/{id:int}", async (int id, IncomeReceiptDto dto, BudgetDbContext db) =>
        {
            var r = await db.IncomeReceipts.FindAsync(id);
            if (r is null) return Results.NotFound();
            if (dto.Amount <= 0) return Results.Problem("A payment received needs an amount above zero.", statusCode: 400);
            r.Date = dto.Date; r.Amount = dto.Amount; r.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(new IncomeReceiptDto { Id = r.Id, IncomeSourceId = r.IncomeSourceId, Date = r.Date, Amount = r.Amount, Notes = r.Notes });
        });
        g.MapDelete("/receipts/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var r = await db.IncomeReceipts.FindAsync(id);
            if (r is null) return Results.NotFound();
            db.IncomeReceipts.Remove(r);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/payments", async (int? year, BudgetDbContext db) =>
        {
            var q = db.EstimatedTaxPayments.AsQueryable();
            if (year is { } y) q = q.Where(p => p.Year == y);
            return (await q.OrderByDescending(p => p.Date).ToListAsync()).Select(p => new EstimatedTaxPaymentDto { Id = p.Id, Year = p.Year, Jurisdiction = p.Jurisdiction, Date = p.Date, Amount = p.Amount, Notes = p.Notes });
        });
        g.MapPost("/payments", async (EstimatedTaxPaymentDto dto, BudgetDbContext db) =>
        {
            var p = new EstimatedTaxPayment { Year = dto.Year, Jurisdiction = dto.Jurisdiction, Date = dto.Date, Amount = dto.Amount, Notes = dto.Notes };
            db.EstimatedTaxPayments.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/side-income/payments/{p.Id}", new EstimatedTaxPaymentDto { Id = p.Id, Year = p.Year, Jurisdiction = p.Jurisdiction, Date = p.Date, Amount = p.Amount, Notes = p.Notes });
        });
        g.MapDelete("/payments/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var p = await db.EstimatedTaxPayments.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.EstimatedTaxPayments.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // projected: null = annualize YTD (YTD ÷ months elapsed × 12); a value = use it as the year's net profit.
        g.MapGet("/estimate", async (int? year, decimal? projected, BudgetDbContext db, InvestmentService inv, PaycheckService paychecks, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var y = year ?? today.Year;
            var asOf = y == today.Year ? today : new DateOnly(y, 12, 31);
            var sources = await db.IncomeSources.Where(s => s.IsActive && s.Type == IncomeSourceType.Contract1099).OrderBy(s => s.Name).ToListAsync();
            var receipts = await db.IncomeReceipts.Where(r => r.Date.Year == y).ToListAsync();
            var per = sources.Select(s => new SourceYtdDto(s.Id, s.Name, receipts.Where(r => r.IncomeSourceId == s.Id).Sum(r => r.Amount))).ToList();
            var ytd = per.Sum(s => s.YearToDate);
            var months = Math.Max(1, asOf.Month);
            var annual = projected ?? Math.Round(ytd / months * 12, 2);
            var method = projected is not null ? "your projection" : $"YTD {ytd:C} ÷ {months} months × 12";

            var warnings = new List<string>();
            var (marginal, _, moRate, _, _, note) = await inv.OrdinaryContextAsync(y, asOf);
            if (note is not null) warnings.Add(note);
            decimal w2Ss = 0, w2Med = 0;
            foreach (var s in await db.IncomeSources.Where(s => s.IsActive && s.Type == IncomeSourceType.W2Salary && s.PaySchedules.Count > 0).ToListAsync())
            {
                try { var yr = await paychecks.YearAsync(s.Id, y); w2Ss += yr.Gross - yr.PreTax; w2Med += yr.Gross - yr.PreTax; }
                catch (InvalidOperationException) { }
            }
            var taxYear = await db.TaxYears.Include(t => t.Brackets).FirstOrDefaultAsync(t => t.Year == y) ?? await db.TaxYears.Include(t => t.Brackets).OrderByDescending(t => t.Year).FirstAsync();
            if (taxYear.Year != y) warnings.Add($"Using {taxYear.Year} tax tables for {y}.");
            var paid = await db.EstimatedTaxPayments.Where(p => p.Year == y).ToListAsync();
            var fedPaid = paid.Where(p => p.Jurisdiction == Jurisdiction.Federal).Sum(p => p.Amount);
            var moPaid = paid.Where(p => p.Jurisdiction == Jurisdiction.Missouri).Sum(p => p.Amount);
            if (sources.Count == 0) warnings.Add("No active 1099 income sources; add one under Income with type Contract 1099.");

            var e = SelfEmployment.Estimate(new(y, annual, w2Ss, w2Med, marginal, moRate, ReferenceSeed.ToFederalRules(taxYear), fedPaid, moPaid, asOf));
            return new SelfEmploymentDto(y, asOf, per, ytd, annual, method, w2Ss, marginal, moRate,
                e.SeEarnings, e.SocialSecurityTax, e.MedicareTax, e.SelfEmploymentTax, e.HalfSeDeduction, e.FederalIncomeTax, e.MissouriIncomeTax, e.TotalTax, e.SetAsideFraction,
                fedPaid, moPaid, e.FederalRemaining, e.MissouriRemaining, e.Schedule.Select(q => new QuarterlyPaymentDto(q.Quarter, q.DueDate, q.Federal, q.Missouri, q.Past)).ToList(), e.Steps, warnings);
        });

        return api;
    }
}

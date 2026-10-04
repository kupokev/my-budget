using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

public static class PaycheckEndpoints
{
    public static RouteGroupBuilder MapPaycheck(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/paycheck");

        g.MapGet("/estimate", async (int sourceId, DateOnly? date, PaycheckService svc, TimeProvider clock) =>
            await Guarded(async () => await svc.EstimateAsync(sourceId, date ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime))));

        g.MapGet("/year", async (int sourceId, int? year, PaycheckService svc, TimeProvider clock) =>
            await Guarded(async () => await svc.YearAsync(sourceId, year ?? clock.GetLocalNow().Year)));

        g.MapPost("/what-if", async (WhatIfRequest req, PaycheckService svc) =>
            await Guarded(async () => new WhatIfResponse(
                await svc.EstimateAsync(req.IncomeSourceId, req.PayDate),
                await svc.EstimateAsync(req.IncomeSourceId, req.PayDate, req),
                await svc.YearAsync(req.IncomeSourceId, req.PayDate.Year),
                await svc.YearAsync(req.IncomeSourceId, req.PayDate.Year, req))));

        g.MapPost("/supplemental", async (SupplementalRequest req, PaycheckService svc) =>
            await Guarded(async () => await svc.SupplementalAsync(req)));

        // Actual stubs (INC-7).
        var p = api.MapGroup("/paychecks");
        p.MapGet("/", async (int? sourceId, int? year, BudgetDbContext db) =>
        {
            var q = db.Paychecks.Include(x => x.Lines).Include(x => x.TimeOff).AsQueryable();
            if (sourceId is { } s) q = q.Where(x => x.IncomeSourceId == s);
            if (year is { } y) q = q.Where(x => x.PayDate.Year == y);
            return (await q.OrderByDescending(x => x.PayDate).ToListAsync()).Select(ToDto);
        });
        p.MapPost("/", async (PaycheckDto dto, BudgetDbContext db) =>
        {
            var e = new Paycheck { IncomeSourceId = dto.IncomeSourceId };
            Apply(e, dto);
            db.Paychecks.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/paychecks/{e.Id}", ToDto(e));
        });
        p.MapPut("/{id:int}", async (int id, PaycheckDto dto, BudgetDbContext db) =>
        {
            var e = await db.Paychecks.Include(x => x.Lines).Include(x => x.TimeOff).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            Apply(e, dto);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        p.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            // Loaded with its lines so they go with it on any provider, not only where the database cascades.
            var e = await db.Paychecks.Include(x => x.Lines).Include(x => x.TimeOff).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            db.Paychecks.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        p.MapGet("/{id:int}/compare", async (int id, BudgetDbContext db, PaycheckService svc) =>
        {
            var e = await db.Paychecks.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            return await Guarded(async () =>
            {
                var est = await svc.EstimateAsync(e.IncomeSourceId, e.PayDate);
                var rows = new List<CompareRowDto> { new("Gross", est.Gross, e.Gross, e.Gross - est.Gross) };
                var actualLines = e.Lines.ToDictionary(l => l.Name.Trim(), l => l.Amount, StringComparer.OrdinalIgnoreCase);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void Row(string name, decimal? estimated)
                {
                    var actual = actualLines.TryGetValue(name, out var a) ? a : (decimal?)null;
                    seen.Add(name);
                    rows.Add(new CompareRowDto(name, estimated, actual, actual is { } x && estimated is { } y ? x - y : null));
                }
                foreach (var l in est.PreTaxDeductions) Row(l.Name, l.Amount);
                Row(est.FederalIncomeTax.Name, est.FederalIncomeTax.Amount);
                Row(est.SocialSecurity.Name, est.SocialSecurity.Amount);
                Row(est.Medicare.Name, est.Medicare.Amount);
                Row(est.MissouriIncomeTax.Name, est.MissouriIncomeTax.Amount);
                foreach (var l in est.PostTaxDeductions) Row(l.Name, l.Amount);
                foreach (var l in e.Lines.Where(l => !seen.Contains(l.Name.Trim())))
                    rows.Add(new CompareRowDto(l.Name, null, l.Amount, null));
                rows.Add(new CompareRowDto("Net", est.Net, e.Net, e.Net - est.Net));
                var netVar = e.Net - est.Net;
                return new PaycheckCompareDto(ToDto(e), est, rows, netVar, Math.Abs(netVar) <= 5m);
            });
        });

        return api;
    }

    private static PaycheckDto ToDto(Paycheck e) => new()
    {
        Id = e.Id, IncomeSourceId = e.IncomeSourceId, PayDate = e.PayDate, Kind = e.Kind, Gross = e.Gross, Net = e.Net, Notes = e.Notes,
        Lines = e.Lines.Select(l => new PaycheckLineDto { Category = l.Category, Name = l.Name, Amount = l.Amount }).ToList(),
        TimeOff = e.TimeOff.Select(t => new PaycheckTimeOffDto { BucketId = t.BucketId, Accrued = t.Accrued, Used = t.Used, Balance = t.Balance }).ToList(),
    };

    private static void Apply(Paycheck e, PaycheckDto d)
    {
        e.PayDate = d.PayDate; e.Kind = d.Kind; e.Gross = d.Gross; e.Net = d.Net; e.Notes = d.Notes;
        e.Lines.Clear();
        e.Lines.AddRange(d.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Name)).Select(l => new PaycheckLine { Category = l.Category, Name = l.Name.Trim(), Amount = l.Amount }));
        // A bucket counts as on this stub only with a balance; accrued and used alone say nothing to project from.
        e.TimeOff.Clear();
        e.TimeOff.AddRange(d.TimeOff.Where(t => t.Balance is not null).GroupBy(t => t.BucketId).Select(g => g.First())
            .Select(t => new PaycheckTimeOff { BucketId = t.BucketId, Accrued = t.Accrued, Used = t.Used, Balance = t.Balance!.Value }));
    }

    /// <summary>Turns the service's "not configured" exceptions into 400s with the message, instead of 500s.</summary>
    internal static async Task<IResult> Guarded<T>(Func<Task<T>> work)
    {
        try { return Results.Ok(await work()); }
        catch (KeyNotFoundException ex) { return Results.NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 400); }
    }
}

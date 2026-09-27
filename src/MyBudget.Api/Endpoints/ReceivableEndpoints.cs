using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

/// <summary>DBT-2a–c: recurring obligations and one-off charges per person, payments applied to periods, per-period status and running balance.</summary>
public static class ReceivableEndpoints
{
    public static RouteGroupBuilder MapReceivables(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/people");
        g.MapGet("/", async (BudgetDbContext db) => (await Query(db).OrderBy(p => p.Name).ToListAsync()).Select(ToDto));
        g.MapPost("/", async (PersonDto dto, BudgetDbContext db) =>
        {
            var e = new Person { Name = dto.Name };
            Apply(e, dto);
            db.People.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/people/{e.Id}", ToDto(e));
        });
        g.MapPut("/{id:int}", async (int id, PersonDto dto, BudgetDbContext db) =>
        {
            var e = await Query(db).FirstOrDefaultAsync(p => p.Id == id);
            if (e is null) return Results.NotFound();
            Apply(e, dto);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        g.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var e = await db.People.FindAsync(id);
            if (e is null) return Results.NotFound();
            db.People.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        g.MapGet("/{id:int}/ledger", async (int id, BudgetDbContext db, TimeProvider clock) =>
        {
            var p = await Query(db).FirstOrDefaultAsync(x => x.Id == id);
            return p is null ? Results.NotFound() : Results.Ok(Ledger(p, DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));
        });
        g.MapGet("/ledgers", async (BudgetDbContext db, TimeProvider clock) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            return (await Query(db).Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync()).Select(p => Ledger(p, today));
        });
        return api;
    }

    /// <summary>Oldest unpaid months first, then one-off charges, then the month after the payment date for anything left.</summary>
    internal static List<PaymentAllocation> AutoAllocate(Person p, decimal amount, DateOnly date)
    {
        var ledger = Ledger(p, date);
        var allocations = new List<PaymentAllocation>();
        var left = amount;
        foreach (var r in ledger.Periods.OrderBy(r => r.Period))
        {
            if (left <= 0) break;
            var owed = r.Expected - r.Paid;
            if (owed <= 0) continue;
            var take = Math.Min(left, owed);
            allocations.Add(new PaymentAllocation { Period = r.Period, Amount = take }); left -= take;
        }
        if (left > 0 && ledger.OneOffBalance > 0) { var take = Math.Min(left, ledger.OneOffBalance); allocations.Add(new PaymentAllocation { Period = null, Amount = take }); left -= take; }
        if (left > 0) allocations.Add(new PaymentAllocation { Period = new DateOnly(date.Year, date.Month, 1).AddMonths(1), Amount = left });
        return allocations;
    }

    internal static PersonLedgerDto Ledger(Person p, DateOnly today)
    {
        var steps = new List<string>();
        var thisPeriod = new DateOnly(today.Year, today.Month, 1);
        var active = p.Obligations.Where(o => o.IsActive).ToList();
        var first = active.Select(o => o.StartPeriod).DefaultIfEmpty(thisPeriod).Min();
        var last = active.Select(o => o.EndPeriod ?? thisPeriod).DefaultIfEmpty(thisPeriod).Max();
        if (last < thisPeriod) last = thisPeriod;
        var prepaidUntil = p.Payments.SelectMany(x => x.Allocations).Where(a => a.Period is not null).Select(a => a.Period!.Value).DefaultIfEmpty(thisPeriod).Max();
        if (prepaidUntil > last) last = prepaidUntil;

        var rows = new List<PeriodRowDto>();
        decimal running = 0;
        for (var period = new DateOnly(first.Year, first.Month, 1); period <= last; period = period.AddMonths(1))
        {
            var expectedParts = active.Where(o => o.DueIn(period))
                .Select(o => (Description: o.EveryMonths > 1 ? $"{o.Description} ({o.Cadence})" : o.Description,
                              Amount: o.BudgetLineId is not null && o.BudgetLine is not null ? Math.Round(o.BudgetLine.ProjectedAmount * o.ShareOfLine, 2) : o.MonthlyAmount ?? 0m)).ToList();
            var expected = expectedParts.Sum(x => x.Amount);
            var paid = p.Payments.SelectMany(x => x.Allocations).Where(a => a.Period == period).Sum(a => a.Amount);
            running += paid - expected;
            var status = period > thisPeriod ? (paid > 0 ? "Prepaid" : "Upcoming")
                : paid >= expected && expected > 0 ? "Paid" : paid > 0 ? "Partial" : expected > 0 ? (period == thisPeriod ? "Due" : "Missed") : "—";
            rows.Add(new PeriodRowDto(period, expected, paid, running, status, string.Join(", ", expectedParts.Select(x => $"{x.Description} {x.Amount:C}"))));
        }
        var oneOffCharged = p.Charges.Sum(c => c.Amount);
        var oneOffPaid = p.Payments.SelectMany(x => x.Allocations).Where(a => a.Period is null).Sum(a => a.Amount);
        var unallocated = p.Payments.Sum(x => x.Amount - x.Allocations.Sum(a => a.Amount));
        var totalExpected = rows.Where(r => r.Period <= thisPeriod).Sum(r => r.Expected) + oneOffCharged;
        var totalPaid = rows.Sum(r => r.Paid) + oneOffPaid;
        steps.Add($"Expected through {thisPeriod:MMM yyyy}: {rows.Where(r => r.Period <= thisPeriod).Sum(r => r.Expected):C} recurring + {oneOffCharged:C} one-off = {totalExpected:C}");
        steps.Add($"Paid: {rows.Sum(r => r.Paid):C} applied to months (incl. prepaid) + {oneOffPaid:C} to one-offs = {totalPaid:C}" + (unallocated > 0 ? $"; {unallocated:C} received but not yet applied" : ""));
        steps.Add($"Owed = {totalExpected:C} − {totalPaid:C} − unallocated {unallocated:C} = {totalExpected - totalPaid - unallocated:C}");
        return new PersonLedgerDto(ToDto(p), rows, oneOffCharged, oneOffPaid, oneOffCharged - oneOffPaid, totalExpected, totalPaid, totalExpected - totalPaid - unallocated, unallocated, steps);
    }

    private static IQueryable<Person> Query(BudgetDbContext db)
        => db.People.Include(p => p.Obligations).ThenInclude(o => o.BudgetLine).Include(p => p.Charges).Include(p => p.Payments).ThenInclude(x => x.Allocations);

    private static PersonDto ToDto(Person p) => new()
    {
        Id = p.Id, Name = p.Name, Notes = p.Notes, IsActive = p.IsActive,
        Obligations = p.Obligations.OrderBy(o => o.StartPeriod).Select(o => new ObligationDto { Id = o.Id, Description = o.Description, MonthlyAmount = o.MonthlyAmount, BudgetLineId = o.BudgetLineId, ShareOfLine = o.ShareOfLine, StartPeriod = o.StartPeriod, EndPeriod = o.EndPeriod, EveryMonths = o.EveryMonths, IsActive = o.IsActive }).ToList(),
        Charges = p.Charges.OrderByDescending(c => c.Date).Select(c => new ReceivableChargeDto { Id = c.Id, Date = c.Date, Amount = c.Amount, Description = c.Description }).ToList(),
        Payments = p.Payments.OrderByDescending(x => x.Date).Select(x => new ReceivablePaymentDto { Id = x.Id, Date = x.Date, Amount = x.Amount, Notes = x.Notes, Allocations = x.Allocations.OrderBy(a => a.Period).Select(a => new PaymentAllocationDto { Period = a.Period, Amount = a.Amount }).ToList() }).ToList(),
    };

    private static void Apply(Person e, PersonDto d)
    {
        e.Name = d.Name.Trim(); e.Notes = d.Notes; e.IsActive = d.IsActive;
        e.Obligations.Clear();
        e.Obligations.AddRange(d.Obligations.Where(o => !string.IsNullOrWhiteSpace(o.Description)).Select(o => new Obligation
        {
            Description = o.Description.Trim(), MonthlyAmount = o.BudgetLineId is null ? o.MonthlyAmount : null, BudgetLineId = o.BudgetLineId, ShareOfLine = o.ShareOfLine <= 0 ? 1m : o.ShareOfLine,
            StartPeriod = new DateOnly(o.StartPeriod.Year, o.StartPeriod.Month, 1), EndPeriod = o.EndPeriod is { } ep ? new DateOnly(ep.Year, ep.Month, 1) : null,
            EveryMonths = o.EveryMonths <= 0 ? 1 : o.EveryMonths, IsActive = o.IsActive,
        }));
        e.Charges.Clear();
        e.Charges.AddRange(d.Charges.Where(c => !string.IsNullOrWhiteSpace(c.Description)).Select(c => new ReceivableCharge { Date = c.Date, Amount = c.Amount, Description = c.Description.Trim() }));
        e.Payments.Clear();
        e.Payments.AddRange(d.Payments.Where(x => x.Amount != 0).Select(x => new ReceivablePayment
        {
            Date = x.Date, Amount = x.Amount, Notes = x.Notes,
            Allocations = x.Allocations.Where(a => a.Amount != 0).Select(a => new PaymentAllocation { Period = a.Period is { } pd ? new DateOnly(pd.Year, pd.Month, 1) : null, Amount = a.Amount }).ToList(),
        }));
    }
}

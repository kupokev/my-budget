using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

public static class InvestmentEndpoints
{
    public static RouteGroupBuilder MapInvestments(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/investments");

        g.MapGet("/portfolio", async (int? year, DateOnly? asOf, InvestmentService svc, TimeProvider clock) =>
        {
            var today = asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            return await svc.PortfolioAsync(today, year ?? today.Year);
        });

        g.MapGet("/holdings", async (BudgetDbContext db) => (await db.Holdings.Include(h => h.Account).OrderBy(h => h.Ticker).ToListAsync()).Select(InvestmentService.ToDto));
        g.MapPost("/holdings", async (HoldingDto dto, BudgetDbContext db) =>
        {
            var h = new Holding { Ticker = dto.Ticker.Trim().ToUpperInvariant(), Name = dto.Name, AccountId = dto.AccountId, Drip = dto.Drip, IsActive = dto.IsActive, Notes = dto.Notes };
            db.Holdings.Add(h);
            await db.SaveChangesAsync();
            return Results.Created($"/api/investments/holdings/{h.Id}", InvestmentService.ToDto(h));
        });
        g.MapPut("/holdings/{id:int}", async (int id, HoldingDto dto, BudgetDbContext db, InvestmentService svc) =>
        {
            var h = await db.Holdings.Include(x => x.Trades).Include(x => x.Dividends).FirstOrDefaultAsync(x => x.Id == id);
            if (h is null) return Results.NotFound();
            h.Ticker = dto.Ticker.Trim().ToUpperInvariant(); h.Name = dto.Name; h.AccountId = dto.AccountId; h.Drip = dto.Drip; h.IsActive = dto.IsActive; h.Notes = dto.Notes;
            await db.SaveChangesAsync();
            if (h.Drip) await svc.ApplyDripAsync(h);
            return Results.Ok(InvestmentService.ToDto(h));
        });
        g.MapDelete("/holdings/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var h = await db.Holdings.FindAsync(id);
            if (h is null) return Results.NotFound();
            db.Holdings.Remove(h);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        g.MapPost("/holdings/{id:int}/sync", async (int id, InvestmentService svc) => await PaycheckEndpoints.Guarded(() => svc.SyncAsync(id)));
        g.MapPost("/sync-all", async (BudgetDbContext db, InvestmentService svc) =>
        {
            var ids = await db.Holdings.Where(h => h.IsActive).Select(h => h.Id).ToListAsync();
            var results = new List<MarketSyncResultDto>();
            foreach (var id in ids) results.Add(await svc.SyncAsync(id));
            return results;
        });

        g.MapPost("/trades", async (TradeDto dto, BudgetDbContext db) =>
        {
            if (await db.Holdings.FindAsync(dto.HoldingId) is null) return Results.NotFound("Holding not found.");
            var t = new Trade { HoldingId = dto.HoldingId, Date = dto.Date, Kind = dto.Kind, Shares = dto.Shares, Price = dto.Price, Fees = dto.Fees, Notes = dto.Notes };
            db.Trades.Add(t);
            await db.SaveChangesAsync();
            return Results.Created($"/api/investments/trades/{t.Id}", InvestmentService.ToDto(t));
        });
        g.MapPut("/trades/{id:int}", async (int id, TradeDto dto, BudgetDbContext db) =>
        {
            var t = await db.Trades.FindAsync(id);
            if (t is null) return Results.NotFound();
            t.Date = dto.Date; t.Kind = dto.Kind; t.Shares = dto.Shares; t.Price = dto.Price; t.Fees = dto.Fees; t.Notes = dto.Notes;
            await db.SaveChangesAsync();
            return Results.Ok(InvestmentService.ToDto(t));
        });
        g.MapDelete("/trades/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var t = await db.Trades.FindAsync(id);
            if (t is null) return Results.NotFound();
            db.Trades.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Manual dividend (when the fetch missed one) — amount from shares held the day before the ex-date.
        g.MapPost("/dividends", async (DividendDto dto, BudgetDbContext db, InvestmentService svc) =>
        {
            var h = await db.Holdings.Include(x => x.Trades).Include(x => x.Dividends).FirstOrDefaultAsync(x => x.Id == dto.HoldingId);
            if (h is null) return Results.NotFound();
            var shares = dto.SharesHeld > 0 ? dto.SharesHeld : MyBudget.Engines.Investments.Portfolio.SharesHeldOn(h.Trades, dto.ExDate.AddDays(-1));
            var d = new DividendPayment { ExDate = dto.ExDate, PayDate = dto.PayDate, PerShare = dto.PerShare, SharesHeld = shares, Amount = dto.Amount > 0 ? dto.Amount : Math.Round(shares * dto.PerShare, 2), Source = DataSource.Manual };
            h.Dividends.Add(d);
            await db.SaveChangesAsync();
            await svc.ApplyDripAsync(h);
            return Results.Ok(InvestmentService.ToDto(d));
        });
        g.MapDelete("/dividends/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var d = await db.Dividends.FindAsync(id);
            if (d is null) return Results.NotFound();
            db.Dividends.Remove(d);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/prices", async (string ticker, DateOnly date, decimal price, BudgetDbContext db) =>
        {
            var t = ticker.Trim().ToUpperInvariant();
            var row = await db.Prices.FirstOrDefaultAsync(p => p.Ticker == t && p.Date == date) ?? db.Prices.Add(new PriceSnapshot { Ticker = t, Date = date }).Entity;
            row.Price = price; row.Source = DataSource.Manual;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // ---- Assets (ACC-4a) ----
        var a = api.MapGroup("/assets");
        a.MapGet("/", async (BudgetDbContext db) => (await db.Assets.Include(x => x.Values).OrderBy(x => x.Name).ToListAsync()).Select(ToDto));
        a.MapPost("/", async (AssetDto dto, BudgetDbContext db) =>
        {
            var e = new Asset { Name = dto.Name.Trim(), Kind = dto.Kind, Notes = dto.Notes, IsActive = dto.IsActive };
            db.Assets.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/assets/{e.Id}", ToDto(e));
        });
        a.MapPut("/{id:int}", async (int id, AssetDto dto, BudgetDbContext db) =>
        {
            var e = await db.Assets.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            e.Name = dto.Name.Trim(); e.Kind = dto.Kind; e.Notes = dto.Notes; e.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        a.MapDelete("/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var e = await db.Assets.FindAsync(id);
            if (e is null) return Results.NotFound();
            db.Assets.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        a.MapPost("/{id:int}/values", async (int id, AssetValueDto dto, BudgetDbContext db) =>
        {
            var e = await db.Assets.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            var v = e.Values.FirstOrDefault(x => x.AsOf == dto.AsOf) ?? new AssetValue { AsOf = dto.AsOf };
            v.Value = dto.Value;
            if (v.Id == 0) e.Values.Add(v);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });

        return api;
    }

    private static AssetDto ToDto(Asset e)
    {
        var latest = e.Values.OrderByDescending(v => v.AsOf).FirstOrDefault();
        return new() { Id = e.Id, Name = e.Name, Kind = e.Kind, Notes = e.Notes, IsActive = e.IsActive, LatestValue = latest?.Value, LatestAsOf = latest?.AsOf };
    }
}

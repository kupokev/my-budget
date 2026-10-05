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

        // accounts: comma-separated account ids to chart; omitted for every account.
        g.MapGet("/history", async (int? months, DateOnly? asOf, string? accounts, InvestmentService svc, TimeProvider clock) =>
        {
            var today = asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var ids = (accounts ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : (int?)null).OfType<int>().ToList();
            return await svc.HistoryAsync(today, Math.Clamp(months ?? 12, 1, 60), ids);
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
        // multipart/form-data: file (tax-lot export), accountId
        g.MapPost("/import-lots", async (HttpRequest request, InvestmentService svc) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.Problem("No file uploaded.", statusCode: 400);
            if (!int.TryParse(form["accountId"], out var accountId)) return Results.Problem("Pick the account the lots belong to.", statusCode: 400);
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            return await PaycheckEndpoints.Guarded(() => svc.ImportLotsAsync(accountId, content));
        }).DisableAntiforgery();

        g.MapPost("/sync-all", async (BudgetDbContext db, InvestmentService svc) =>
        {
            var ids = await db.Holdings.Where(h => h.IsActive).Select(h => h.Id).ToListAsync();
            var results = new List<MarketSyncResultDto>();
            foreach (var id in ids) results.Add(await svc.SyncAsync(id));
            return results;
        });

        // A sweep balance moves without a new export, so it can be typed. Traded holdings are refused:
        // there the buy or sell is the record, not the resulting number.
        g.MapGet("/accounts/{accountId:int}/contributions", async (int accountId, DateOnly? asOf, InvestmentService svc, TimeProvider clock) =>
            await PaycheckEndpoints.Guarded(() => svc.ContributionsAsync(accountId, asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime))));
        g.MapPut("/accounts/{accountId:int}/contributions-from", async (int accountId, ContributionsCoverDto dto, InvestmentService svc) =>
            await PaycheckEndpoints.Guarded(async () => { await svc.SetContributionsRecordedFromAsync(accountId, dto.From); return dto; }));
        g.MapPost("/contributions", async (ContributionDto dto, BudgetDbContext db) =>
        {
            if (await db.Accounts.FindAsync(dto.AccountId) is null) return Results.NotFound("Account not found.");
            var c = new InvestmentContribution { AccountId = dto.AccountId, Date = dto.Date, Amount = dto.Amount, Kind = dto.Kind, Description = dto.Description, Source = DataSource.Manual };
            db.InvestmentContributions.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/investments/contributions/{c.Id}", InvestmentService.ToDto(c));
        });
        // A corrected entry keeps its statement id, so re-importing the statement doesn't bring the original back.
        g.MapPut("/contributions/{id:int}", async (int id, ContributionDto dto, BudgetDbContext db) =>
        {
            var c = await db.InvestmentContributions.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.Date = dto.Date; c.Amount = dto.Amount; c.Kind = dto.Kind; c.Description = dto.Description;
            await db.SaveChangesAsync();
            return Results.Ok(InvestmentService.ToDto(c));
        });
        g.MapDelete("/contributions/{id:int}", async (int id, BudgetDbContext db) =>
        {
            var c = await db.InvestmentContributions.FindAsync(id);
            if (c is null) return Results.NotFound();
            db.InvestmentContributions.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/holdings/{id:int}/cash-balance", async (int id, CashBalanceDto dto, InvestmentService svc) =>
            await PaycheckEndpoints.Guarded(() => svc.SetCashBalanceAsync(id, dto.Balance, dto.AsOf)));

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
        a.MapGet("/", async (BudgetDbContext db) => (await db.Assets.Include(x => x.Values).Include(x => x.Loans).ThenInclude(l => l.Balances).OrderBy(x => x.Name).ToListAsync()).Select(ToDto));
        a.MapPost("/", async (AssetDto dto, BudgetDbContext db) =>
        {
            var e = new Asset { Name = dto.Name.Trim(), Kind = dto.Kind, Notes = dto.Notes, IsActive = dto.IsActive };
            db.Assets.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/assets/{e.Id}", ToDto(e));
        });
        a.MapPut("/{id:int}", async (int id, AssetDto dto, BudgetDbContext db) =>
        {
            var e = await db.Assets.Include(x => x.Values).Include(x => x.Loans).ThenInclude(l => l.Balances).FirstOrDefaultAsync(x => x.Id == id);
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
            var e = await db.Assets.Include(x => x.Values).Include(x => x.Loans).ThenInclude(l => l.Balances).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            var v = e.Values.FirstOrDefault(x => x.AsOf == dto.AsOf) ?? new AssetValue { AsOf = dto.AsOf };
            v.Value = dto.Value;
            if (v.Id == 0) e.Values.Add(v);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });
        // A mistyped valuation should be removable without deleting the asset.
        a.MapDelete("/{id:int}/values/{valueId:int}", async (int id, int valueId, BudgetDbContext db) =>
        {
            var e = await db.Assets.Include(x => x.Values).Include(x => x.Loans).ThenInclude(l => l.Balances).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            var v = e.Values.FirstOrDefault(x => x.Id == valueId);
            if (v is null) return Results.NotFound();
            e.Values.Remove(v);
            db.Remove(v);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(e));
        });

        return api;
    }

    /// <summary>
    /// An asset with its valuation history and whatever is secured against it. Equity is the latest
    /// value less the attached loans' latest balances; net worth still counts the asset and the loans
    /// separately, so this is a view of the same numbers, not a second source of them.
    /// </summary>
    private static AssetDto ToDto(Asset e)
    {
        var ordered = e.Values.OrderByDescending(v => v.AsOf).ToList();
        var latest = ordered.FirstOrDefault();

        var history = new List<AssetValuePointDto>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var prior = i + 1 < ordered.Count ? ordered[i + 1] : null;
            decimal? change = prior is null ? null : Math.Round(ordered[i].Value - prior.Value, 2);
            decimal? pct = prior is null || prior.Value == 0 ? null : Math.Round((ordered[i].Value - prior.Value) / prior.Value * 100m, 2);
            history.Add(new AssetValuePointDto(ordered[i].Id, ordered[i].AsOf, ordered[i].Value, change, pct));
        }

        var loans = e.Loans.Where(l => l.IsActive).Select(l =>
        {
            var b = l.Balances.OrderByDescending(x => x.AsOf).FirstOrDefault();
            return new AssetLoanDto(l.Id, l.Name, l.Kind, b?.Balance ?? 0m, b?.AsOf);
        }).OrderByDescending(l => l.Balance).ToList();

        var owed = Math.Round(loans.Sum(l => l.Balance), 2);
        decimal? equity = latest is null ? null : Math.Round(latest.Value - owed, 2);
        var formula = latest is null ? null
            : loans.Count == 0 ? $"{latest.Value:C} valued {latest.AsOf:yyyy-MM-dd}, nothing secured against it"
            : $"{latest.Value:C} − {string.Join(" − ", loans.Select(l => $"{l.Name} {l.Balance:C}"))} = {equity:C}";

        // A year-on-year move needs the closest record to twelve months before the latest one.
        decimal? overYear = null;
        if (latest is not null)
        {
            var target = latest.AsOf.AddYears(-1);
            var prior = ordered.Where(v => v.AsOf <= target).OrderByDescending(v => v.AsOf).FirstOrDefault();
            if (prior is not null) overYear = Math.Round(latest.Value - prior.Value, 2);
        }

        return new()
        {
            Id = e.Id, Name = e.Name, Kind = e.Kind, Notes = e.Notes, IsActive = e.IsActive,
            LatestValue = latest?.Value, LatestAsOf = latest?.AsOf,
            History = history, Loans = loans, LoanBalance = owed, Equity = equity, EquityFormula = formula,
            ChangeSincePrior = history.FirstOrDefault()?.Change, ChangeOverYear = overYear,
        };
    }
}

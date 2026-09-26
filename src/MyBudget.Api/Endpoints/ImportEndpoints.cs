using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Import;

namespace MyBudget.Api.Endpoints;

public static class ImportEndpoints
{
    public static RouteGroupBuilder MapImport(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/import").DisableAntiforgery();

        g.MapGet("/profiles", () => CsvProfiles.All.Select(p => new ImportProfileDto(p.Key, p.Name, p.Notes)).Append(new ImportProfileDto("ofx", "OFX / QFX", "Detected automatically")));

        // multipart/form-data: file, accountId?, cardId?, profile?
        g.MapPost("/preview", async (HttpRequest request, ImportService svc) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.Problem("No file uploaded.", statusCode: 400);
            if (file.Length > 20 * 1024 * 1024) return Results.Problem("File larger than 20 MB.", statusCode: 400);
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            int? accountId = int.TryParse(form["accountId"], out var a) ? a : null;
            int? cardId = int.TryParse(form["cardId"], out var c) ? c : null;
            var profile = string.IsNullOrWhiteSpace(form["profile"]) ? null : form["profile"].ToString();
            return await PaycheckEndpoints.Guarded(() => svc.PreviewAsync(file.FileName, content, accountId, cardId, profile == "auto" ? null : profile));
        });

        g.MapPost("/commit", async (ImportCommitRequest req, ImportService svc) => await PaycheckEndpoints.Guarded(() => svc.CommitAsync(req)));

        g.MapGet("/batches", async (BudgetDbContext db) =>
            (await db.ImportBatches.Include(b => b.Account).Include(b => b.Card).OrderByDescending(b => b.ImportedAt).ToListAsync())
                .Select(b => new ImportBatchDto(b.Id, b.FileName, b.Format, b.Profile, b.ImportedAt, b.Account?.Name ?? b.Card?.Name ?? "—", b.RowCount, b.ImportedCount, b.DuplicateCount, b.FirstDate, b.LastDate)));

        // Undo an import: removes its transactions and re-syncs the months they touched.
        g.MapDelete("/batches/{id:int}", async (int id, BudgetDbContext db, ImportService svc) =>
        {
            var batch = await db.ImportBatches.FindAsync(id);
            if (batch is null) return Results.NotFound();
            var lines = await db.Transactions.Where(t => t.ImportBatchId == id).ToListAsync();
            db.Transactions.RemoveRange(lines);
            db.ImportBatches.Remove(batch);
            await db.SaveChangesAsync();
            await svc.SyncAsync(lines);
            return Results.NoContent();
        });

        return api;
    }
}

using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Import;

namespace MyBudget.Api;

/// <summary>Turns a parsed statement into transactions: suggests categories/bills/transfers, flags duplicates, commits, and syncs bill actuals and card spend.</summary>
public sealed class ImportService(BudgetDbContext db)
{
    public const int ReconcileWindowDays = 3;

    private static readonly string[] TransferHints = ["PAYMENT THANK YOU", "AUTOPAY", "AUTOMATIC PAYMENT", "ONLINE TRANSFER", "PAYMENT RECEIVED", "MOBILE PYMT", "MOBILE PMT", "MOBILE PAYMENT", "PAYMENT - THANK YOU", "TRANSFER TO", "TRANSFER FROM", "INTERNET PAYMENT", "ACH PAYMENT", "DIRECTPAY", "PAYMENT TO CHASE CARD", "CCPYMT", "CARD PAYMENT", "MONEYLINE", "WEALTHFRONT", "ACCT_XFER"];

    public async Task<ImportPreviewDto> PreviewAsync(string fileName, string content, int? accountId, int? cardId, string? profileKey)
    {
        if (accountId is null && cardId is null) throw new InvalidOperationException("Pick the account or card this statement belongs to.");
        var sourceName = accountId is { } a ? (await db.Accounts.FindAsync(a))?.Name ?? throw new KeyNotFoundException("Account not found.")
                                            : (await db.Cards.FindAsync(cardId!.Value))?.Name ?? throw new KeyNotFoundException("Card not found.");
        var source = accountId is { } aid ? $"account:{aid}" : $"card:{cardId}";

        ParseResult parsed = OfxStatementParser.LooksLikeOfx(content) || fileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase)
            ? OfxStatementParser.Parse(content)
            : CsvStatementParser.Parse(content, profileKey);
        var profileName = parsed.Profile == "ofx" ? "OFX / QFX" : CsvProfiles.ByKey(parsed.Profile)?.Name ?? parsed.Profile;

        var existing = await db.Transactions.Where(t => t.AccountId == accountId && t.CardId == cardId).Select(t => t.ExternalId).ToHashSetAsync();
        var manual = await db.Transactions.Where(t => t.AccountId == accountId && t.CardId == cardId && t.Origin == TransactionOrigin.Manual && t.ReconciledWithId == null).Select(t => new { t.Date, t.Amount, t.Description }).ToListAsync();
        var rules = await db.CategoryRules.Include(r => r.Category).Include(r => r.Bill).Where(r => r.IsActive).OrderBy(r => r.Priority).ToListAsync();
        var categories = await db.Categories.Where(c => c.IsActive).ToListAsync();
        var bills = await db.Bills.Where(b => b.IsActive).ToListAsync();
        var cardsByLast4 = (await db.Cards.Where(c => c.IsActive && c.AccountNumber != null && c.AccountNumber.Length >= 4).ToListAsync()).ToDictionary(c => c.AccountNumber![^4..], c => c.Name);

        var rows = new List<ImportRowDto>();
        var seen = new Dictionary<string, int>();
        foreach (var p in parsed.Transactions.OrderBy(t => t.Date))
        {
            string externalId;
            if (p.ExternalId is { } fit) externalId = fit;
            else
            {
                var key = $"{p.Date:yyyy-MM-dd}|{p.Amount:0.00}|{p.Description.Trim().ToUpperInvariant()}";
                var n = seen.GetValueOrDefault(key);
                seen[key] = n + 1;
                externalId = Merchants.HashId(source, p.Date, p.Amount, p.Description, n);
            }
            var row = new ImportRowDto
            {
                Date = p.Date, PostedDate = p.PostedDate, Amount = p.Amount, Description = p.Description, Merchant = Merchants.Normalize(p.Description),
                ExternalId = externalId, SourceCategory = p.SourceCategory, Memo = p.Memo, IsDuplicate = existing.Contains(externalId),
            };
            Suggest(row, rules, categories, bills, cardsByLast4);
            var match = manual.FirstOrDefault(m => m.Amount == row.Amount && Math.Abs(m.Date.DayNumber - row.Date.DayNumber) <= ReconcileWindowDays);
            if (!row.IsDuplicate && match is not null)
            {
                row.IsTransfer = true;
                row.SuggestionSource = $"will reconcile with your entry \"{match.Description}\" on {match.Date:MMM d}";
            }
            rows.Add(row);
        }
        return new ImportPreviewDto(parsed.Profile, profileName, accountId, cardId, sourceName, rows,
            rows.Count(r => !r.IsDuplicate), rows.Count(r => r.IsDuplicate), rows.MinBy(r => r.Date)?.Date, rows.MaxBy(r => r.Date)?.Date, parsed.Warnings);
    }

    public static void Suggest(ImportRowDto row, List<CategoryRule> rules, List<Category> categories, List<Bill> bills, IReadOnlyDictionary<string, string>? cardsByLast4 = null)
    {
        var text = $"{row.Description} {row.Merchant} {row.Memo}";
        // "Payment to Chase card ending in 9039" → the card whose number ends in 9039: a card payment, i.e. a transfer.
        var ending = System.Text.RegularExpressions.Regex.Match(row.Description, @"(?:ending in|ending|x{2,}|\.{3})\s*(\d{4})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (ending.Success)
        {
            var last4 = ending.Groups[1].Value;
            if (cardsByLast4 is not null && cardsByLast4.TryGetValue(last4, out var cardName))
            {
                row.IsTransfer = true; row.Merchant = $"Payment to {cardName}"; row.SuggestionSource = $"card payment ({cardName})";
                return;
            }
            if (row.Description.Contains("card", StringComparison.OrdinalIgnoreCase))
            {
                row.IsTransfer = true; row.Merchant = $"Payment to card …{last4}"; row.SuggestionSource = $"card payment; no card on file ends in {last4}";
                return;
            }
        }
        var rule = rules.FirstOrDefault(r => Matches(r, text));
        if (rule is not null)
        {
            row.CategoryId = rule.CategoryId ?? rule.Bill?.CategoryId;
            row.BillId = rule.BillId;
            row.IsTransfer = rule.MarkAsTransfer;
            row.SuggestionSource = $"rule \"{rule.Pattern}\"";
            return;
        }
        if (TransferHints.Any(h => row.Description.Contains(h, StringComparison.OrdinalIgnoreCase)))
        {
            row.IsTransfer = true;
            row.SuggestionSource = "looks like a payment/transfer";
            return;
        }
        // A bill whose name appears in the description (e.g. "HULU", "AT&T") — only for money out.
        if (row.Amount < 0)
        {
            var bill = bills.FirstOrDefault(b => b.Name.Length >= 3 && row.Description.Contains(b.Name, StringComparison.OrdinalIgnoreCase));
            if (bill is not null)
            {
                row.BillId = bill.Id; row.CategoryId = bill.CategoryId; row.SuggestionSource = $"bill name \"{bill.Name}\"";
                return;
            }
        }
        if (row.SourceCategory is { } sc)
        {
            var cat = categories.FirstOrDefault(c => c.Name.Equals(sc, StringComparison.OrdinalIgnoreCase))
                      ?? categories.FirstOrDefault(c => sc.Contains(c.Name, StringComparison.OrdinalIgnoreCase) || c.Name.Contains(sc, StringComparison.OrdinalIgnoreCase));
            if (cat is not null) { row.CategoryId = cat.Id; row.SuggestionSource = $"statement category \"{sc}\""; }
        }
    }

    public static bool Matches(CategoryRule r, string text) => r.Match switch
    {
        RuleMatch.Contains => text.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase),
        RuleMatch.StartsWith => text.StartsWith(r.Pattern, StringComparison.OrdinalIgnoreCase),
        RuleMatch.Regex => SafeRegex(r.Pattern, text),
        _ => false,
    };

    private static bool SafeRegex(string pattern, string text)
    {
        try { return System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
        catch { return false; }
    }

    public async Task<ImportResultDto> CommitAsync(ImportCommitRequest req)
    {
        if (req.AccountId is null && req.CardId is null) throw new InvalidOperationException("Pick the account or card this statement belongs to.");
        var existing = await db.Transactions.Where(t => t.AccountId == req.AccountId && t.CardId == req.CardId).Select(t => t.ExternalId).ToHashSetAsync();
        var batch = new ImportBatch
        {
            FileName = req.FileName, Format = req.Profile == "ofx" ? ImportFormat.Ofx : ImportFormat.Csv, Profile = req.Profile, ImportedAt = DateTime.Now,
            AccountId = req.AccountId, CardId = req.CardId, RowCount = req.Rows.Count,
        };
        db.ImportBatches.Add(batch);
        int imported = 0, dupes = 0, skipped = 0;
        var touched = new List<Transaction>();
        foreach (var r in req.Rows)
        {
            if (r.Skip) { skipped++; continue; }
            if (existing.Contains(r.ExternalId)) { dupes++; continue; }
            existing.Add(r.ExternalId);
            var t = new Transaction
            {
                AccountId = req.AccountId, CardId = req.CardId, Date = r.Date, PostedDate = r.PostedDate, Amount = r.Amount, Description = r.Description.Trim(),
                Merchant = r.Merchant, ExternalId = r.ExternalId, CategoryId = r.CategoryId, BillId = r.BillId, IsTransfer = r.IsTransfer, ImportBatch = batch,
                Notes = r.Memo, Origin = TransactionOrigin.Imported,
            };
            db.Transactions.Add(t);
            touched.Add(t);
            imported++;
        }
        batch.ImportedCount = imported; batch.DuplicateCount = dupes;
        batch.FirstDate = touched.MinBy(t => t.Date)?.Date; batch.LastDate = touched.MaxBy(t => t.Date)?.Date;
        await db.SaveChangesAsync();
        var reconciled = await AutoReconcileAsync(touched);
        var (billMonths, cardMonths) = await SyncAsync(touched);
        return new ImportResultDto(batch.Id, imported, dupes, skipped, billMonths, cardMonths, reconciled);
    }

    /// <summary>Links each new imported line to an unreconciled manual row on the same source with the same amount within the window.</summary>
    public async Task<int> AutoReconcileAsync(IReadOnlyCollection<Transaction> imported)
    {
        var count = 0;
        foreach (var t in imported.Where(t => t.Origin == TransactionOrigin.Imported && t.ReconciledWithId == null))
        {
            var lo = t.Date.AddDays(-ReconcileWindowDays); var hi = t.Date.AddDays(ReconcileWindowDays);
            var candidates = await db.Transactions.Where(m => m.Origin == TransactionOrigin.Manual && m.ReconciledWithId == null && m.AccountId == t.AccountId && m.CardId == t.CardId && m.Amount == t.Amount && m.Date >= lo && m.Date <= hi).ToListAsync();
            var m = candidates.OrderBy(c => Math.Abs(c.Date.DayNumber - t.Date.DayNumber)).FirstOrDefault();
            if (m is null) continue;
            Link(t, m); count++;
        }
        await db.SaveChangesAsync();
        return count;
    }

    /// <summary>Pair an imported line with a manual row: the imported line inherits the manual row's transfer flag, counterparty, category and bill when it has none of its own.</summary>
    public static void Link(Transaction imported, Transaction manual)
    {
        imported.ReconciledWithId = manual.Id; manual.ReconciledWithId = imported.Id;
        imported.IsTransfer = imported.IsTransfer || manual.IsTransfer;
        imported.CounterpartyAccountId ??= manual.CounterpartyAccountId;
        imported.CategoryId ??= manual.CategoryId;
        imported.BillId ??= manual.BillId;
        if (string.IsNullOrWhiteSpace(imported.Notes)) imported.Notes = manual.Notes;
    }

    /// <summary>After transactions change: bill actuals (BIL-3) from matched lines, card spend (RWD-3) from card lines, for the affected months.</summary>
    public async Task<(int BillMonths, int CardMonths)> SyncAsync(IReadOnlyCollection<Transaction> changed)
    {
        var billMonths = changed.Where(t => t.BillId is not null).Select(t => (t.BillId!.Value, Period: new DateOnly(t.Date.Year, t.Date.Month, 1))).Distinct().ToList();
        foreach (var (billId, period) in billMonths)
        {
            var end = period.AddMonths(1);
            var sum = await db.Transactions.Where(t => t.BillId == billId && t.Date >= period && t.Date < end && t.Amount < 0).SumAsync(t => -t.Amount);
            var row = await db.BillPeriods.FirstOrDefaultAsync(p => p.BillId == billId && p.Period == period);
            if (sum == 0 && row is not null && row.Notes == "from import") { row.ActualAmount = null; continue; }
            if (sum == 0) continue;
            row ??= db.BillPeriods.Add(new BillPeriod { BillId = billId, Period = period }).Entity;
            row.ActualAmount = sum;
            row.Notes ??= "from import";
        }

        var cardMonths = changed.Where(t => t.CardId is not null).Select(t => (t.CardId!.Value, Period: new DateOnly(t.Date.Year, t.Date.Month, 1))).Distinct().ToList();
        foreach (var (cardId, period) in cardMonths)
        {
            var end = period.AddMonths(1);
            var sums = await db.Transactions.Where(t => t.CardId == cardId && t.Date >= period && t.Date < end && t.Amount < 0 && !t.IsTransfer)
                .GroupBy(t => t.CategoryId).Select(g => new { CategoryId = g.Key, Sum = g.Sum(t => -t.Amount) }).ToListAsync();
            var rows = await db.CardSpend.Where(s => s.CardId == cardId && s.Period == period).ToListAsync();
            db.CardSpend.RemoveRange(rows);
            db.CardSpend.AddRange(sums.Select(s => new CardSpend { CardId = cardId, Period = period, CategoryId = s.CategoryId, Amount = Math.Round(s.Sum, 2), Notes = "from import" }));
        }
        await db.SaveChangesAsync();
        return (billMonths.Count, cardMonths.Count);
    }

    /// <summary>Re-run the active rules over lines that weren't categorized by hand. Returns how many changed.</summary>
    public async Task<int> ApplyRulesAsync(CategoryRule? only = null)
    {
        var rules = only is not null ? [only] : await db.CategoryRules.Include(r => r.Bill).Where(r => r.IsActive).OrderBy(r => r.Priority).ToListAsync();
        var candidates = await db.Transactions.Where(t => !t.IsManuallyCategorized).ToListAsync();
        var changed = new List<Transaction>();
        foreach (var t in candidates)
        {
            var text = $"{t.Description} {t.Merchant} {t.Notes}";
            var rule = rules.FirstOrDefault(r => Matches(r, text));
            if (rule is null) continue;
            var cat = rule.CategoryId ?? rule.Bill?.CategoryId;
            if (t.CategoryId == cat && t.BillId == rule.BillId && t.IsTransfer == rule.MarkAsTransfer) continue;
            t.CategoryId = cat; t.BillId = rule.BillId; t.IsTransfer = rule.MarkAsTransfer;
            changed.Add(t);
        }
        await db.SaveChangesAsync();
        if (changed.Count > 0) await SyncAsync(changed);
        return changed.Count;
    }
}

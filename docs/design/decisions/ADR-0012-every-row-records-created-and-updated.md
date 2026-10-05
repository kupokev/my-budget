# ADR-0012: Every row records when it was created and last changed

> **Status:** Accepted
> **Date:** 2026-10-04
> **Deciders:** Kevin

## Context

A bill marked paid with no Paid on date left no trace of when it was marked. The dashboard's
paid-so-far chart had to put it on "today", so it moved a day each morning, and the real date could
not be recovered from the database because no table recorded when a row was written. Apps without
this information make questions like "when did I enter that?" impossible to answer after the fact.

## Decision

Every table has two nullable UTC columns, `CreatedAt` and `UpdatedAt`.

- They are EF Core **shadow properties**, added in `BudgetDbContext.OnModelCreating` by a loop over
  every entity type. A new table gets them automatically; nobody has to remember.
- `BudgetDbContext.SaveChanges`/`SaveChangesAsync` set them: both on insert, `UpdatedAt` on every
  update. `CreatedAt` is never rewritten once set.
- They are nullable because rows that existed before migration `RowTimestamps` have no true value.
  Null means "before tracking began", not a guessed date.
- Read them with `EF.Property<DateTime?>(row, BudgetDbContext.CreatedAt)` or
  `db.Entry(row).Property(...)`.

## Consequences

### Positive

- Every record from now on can answer "when was this entered, and when was it last touched?"
- No domain class changes, and no table can be left out.

### Negative

- Shadow properties don't appear on the domain classes, so showing them needs `EF.Property` in a
  query rather than a plain property access.

### Risks

- Writes that bypass the change tracker (`ExecuteUpdate`, `ExecuteDelete`, raw SQL) don't stamp
  anything. None exist today apart from `VACUUM INTO` for backups, which copies the file as-is.
- An endpoint that "edits" by deleting and re-adding rows gives them a new `CreatedAt`. Edits should
  update rows in place, as CLAUDE.md already requires for corrections.

## Alternatives Considered

### An `IAuditable` interface with real properties on each entity

The values would be visible on the classes, but every one of the ~57 entities would need editing,
and a new entity could forget the interface. Rejected in favour of the loop, which can't be forgotten.

### A full change-history table

This would record every old value, not just when the row changed. It's more than the problem needs
today; the timestamps don't rule it out later.

## References

- Migration `RowTimestamps` in `src/MyBudget.Data/Migrations/`
- `tests/MyBudget.Api.Tests/RowTimestampTests.cs`

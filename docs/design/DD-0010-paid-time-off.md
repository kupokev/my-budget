# DD-0010: Paid time off

> **Status:** Active
> **Last updated:** 2026-10-03
> **Related ADRs:** —

## Context / Problem

A pay stub prints time-off balances, but nothing recorded them, so "do I have the PTO for that trip?"
needed the HR site and mental arithmetic. Employers keep time off differently: one bucket covering
vacation and sickness; separate PTO, sick and floating-holiday buckets; accrual every paycheck; a block
granted once a year; a cap that stops accrual. The design has to hold all of those without modelling
any one employer's policy.

## Design

Time off is part of the paycheck system: buckets belong to the job, balances come off the stubs.

- **`TimeOffBucket`** (per income source): a name as the employer uses it, and how it grows — hours per
  paycheck (`AccrualHoursPerPaycheck`), a yearly grant (`AnnualGrantHours` in `GrantMonth`), a cap
  (`MaxHours`), and `HoursPerDay` for showing days. Edited on the income source. Matched by id on save
  because stubs reference it; a bucket a stub mentions is retired (inactive), never deleted.
- **`PaycheckTimeOff`** (per stub, per bucket): accrued, used, and the balance as printed. A bucket
  without a balance isn't on that stub.
- **Projection** (`Engines.Ledger/TimeOffProjection`, pure): from the newest stub's balance on or before
  today, add the accrual for each pay date after the stub and before the target day (a check paid on the
  day off doesn't help), add each yearly grant falling in between, and stop at the cap — in date order,
  because hours accrued at the cap are lost. Planned time off isn't subtracted; a goal's hours are
  compared against the projection. The rate is the bucket's own if set, otherwise what the newest stub
  accrued, so stubs teach the rate rather than it being typed twice. Every forecast carries its formula.
- **Goals** can need time off: a bucket, hours, and the day it starts (`TimeOffBucketId`,
  `TimeOffHours`, `TimeOffStarts`; all three or none). `TimeOffBucketId` is a plain column, not a
  foreign key — adding one to the existing Goals table makes SQLite rebuild it outside a transaction.
- **Where it shows:** the stubs table and stub editor (Paycheck → Actual stubs); the goal editor, which
  previews the projection as the fields change; the Goals list; "What changed this month" on Home
  ("…will be 72h (9 days) by Mar 2, enough for the 40h it needs. You can book the time off."), which the
  AI note reads; and the Assistant's `time_off` tool.
- **API:** `GET /api/time-off?on=` (each bucket's balance, rate and projection), `GET /api/time-off/goals`.

## Open Questions

- Use-it-or-lose-it resets at year end aren't modelled; the yearly grant adds, it doesn't replace.
- Hours already booked for other trips aren't subtracted from later goals' projections.

## Status

Built 2026-10-03: buckets, stub lines, projection, goal check, dashboard line, Assistant tool, migration
`PaidTimeOff`.

## References

DD-0009 (income and pay schedules), DD-0007 (local AI tools), `TimeOffEndpoints`, `MonthHighlights`.

# ADR-0010: SQLite in-process, desktop-first; the API becomes a sync surface

> **Status:** Accepted
> **Date:** 2026-09-27
> **Deciders:** Kevin
> **Supersedes the hosting half of:** ADR-0005 (no Docker, systemd and existing PostgreSQL)

## Context

The app was built as a Blazor desktop client talking over HTTP to an ASP.NET API, with the API
holding the data: the EF Core in-memory provider in development, PostgreSQL in production on a box
on the home network (ADR-0005).

Two things pushed against that in practice.

The in-memory database is wiped on every restart. Once real data started going in — a tax-lot export
of 95 lots across 15 holdings — every restart destroyed it, and restarts happen constantly while the
app is being built.

More fundamentally, the budgeting work happens on one desktop machine. Standing up and maintaining a
database server plus a service on another box is real operational weight for a single-user app that
is only ever open in one place. Kevin: *"I think I would like this to operate solely on the machine
instead of having to host an API."*

The phone is still wanted eventually, and a phone does need something to sync against.

## Decision

The desktop app hosts the API **inside its own process**, on a loopback port, against a **SQLite**
file under the user's local data folder (`~/.local/share/MyBudget/mybudget.db` on Linux).

There is one process and one file. Nothing is hosted, nothing listens off-machine, and the app is
launched by running it.

The HTTP layer stays. The UI still talks to `ApiClient` over HTTP to `127.0.0.1`, rather than being
refactored to call services directly, because that same API is what a phone will sync against later.
`MyBudget.Api` remains a standalone executable for that day; `BudgetApiHost` holds the wiring both
entry points share.

Setting `MYBUDGET_API_URL` points the desktop app at a remote API instead, which is the migration
path to a shared server without a code change.

### Schema changes

Relational providers are brought up to date with **EF Core migrations** (`Database.MigrateAsync`),
not `EnsureCreated`. With real data in a real file, a model change must not cost the data, and this
project's model changes often. The in-memory provider used by tests has no migrations and needs none.

### Seeding

Demo data is only ever written to an **empty in-memory** database. A SQLite or PostgreSQL file gets
reference data only — tax tables, contribution limits — which is additive and only fills in missing
years.

## Consequences

### Positive

- Data survives restarts, which is the whole point.
- Nothing to install, configure, back up or keep running besides the app itself. Backing up is
  copying one file.
- Schema changes are migrations, so they are reviewable and reversible rather than destructive.
- The desktop app no longer depends on a service being up before it starts.

### Negative

- The API is now started twice over in two places (its own `Program`, and `LocalApi`). `BudgetApiHost`
  keeps that to one set of wiring, but it is still two entry points to keep honest.
- A loopback port is still bound, so the app is not literally serverless. The port is chosen from the
  OS at startup and the API key is generated per run, so nothing is guessable or fixed.
- SQLite and PostgreSQL differ in type handling (decimals in particular). Migrations are authored
  against SQLite; a future PostgreSQL deployment will need its own migration set.
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 pulls `SQLitePCLRaw` 2.1.11, which carries
  GHSA-2m69-gcr7-jv3q. Pinned forward to 2.1.13 in `Directory.Packages.props`; remove the pin once EF
  ships a version that does it itself.

### What is deliberately not decided

Whether the phone eventually syncs against a PostgreSQL server or against this same SQLite file
served from the desktop. That is a decision for when the phone work starts.

# ADR-0005: No Docker; API on a Linux server under systemd, PostgreSQL for production, in-memory for dev

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

The original design doc proposed hosting the API as a Docker container on the home lab and left
PostgreSQL vs. SQLite open. Kevin does not want Docker anywhere in this project, already has a
PostgreSQL server on his network that sees little use, and follows a convention in his other apps
of developing against an in-memory database that can be thrown away and rebuilt freely.

## Decision

- **Production:** the API is published with `dotnet publish` and run as a **systemd service** on a
  Linux server on the home network, talking to the **existing PostgreSQL** server via Npgsql.
- **Development:** the API uses the **EF Core in-memory provider**, seeded on startup, so the model
  can change without migrations while it is still settling.
- The provider is chosen by configuration (`Database:Provider` = `InMemory` | `PostgreSQL`), with
  in-memory the default in the Development environment and PostgreSQL required in Production.
- No Dockerfile, no compose file, no container-based test tooling.

## Consequences

### Positive

- Zero infrastructure to run locally; a restart gives a clean, seeded database.
- Production reuses a server Kevin already maintains.

### Negative

- The in-memory provider is not relational: it does not enforce foreign keys, unique indexes, or
  SQL semantics. Anything that depends on those must be tested against PostgreSQL before a
  release, and the model must stay provider-neutral (no Postgres-only column types in Phase 1).
- Migrations are only exercised on the PostgreSQL path, so they need a deliberate check before
  each publish (see `docs/distribution/`).

### Risks

- Divergence between what "works" in dev and what works on PostgreSQL. Mitigation: a small
  PostgreSQL smoke test run before publishing, and keeping query logic simple.

## Alternatives Considered

### Docker container on the home lab

Rejected outright by Kevin's preference.

### SQLite for development

Relational, so closer to production, but requires migrations to keep pace with model churn during
early phases, which is the friction the in-memory convention avoids. Could be revisited once the
model stabilizes.

### SQLite for production

Simpler, but a PostgreSQL server already exists, and the phone phase means two clients hitting one
database over the network anyway.

## References

- DD-0001: Architecture overview
- `docs/distribution/` for the publish and systemd steps

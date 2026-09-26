# ADR-0001: No paid account aggregation

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

Needed a way to get bank/card transactions and balances into the app. Chase (and most banks) do
publish an FDX-aligned account-data API, but it's aimed at registered third-party companies —
security review, formal onboarding — not something an individual can self-serve an API key for.
Practical alternatives are a paid aggregator (Plaid, SimpleFIN, MX) or manual/CSV import.

## Decision

Import via CSV/OFX statement export per institution, or manual entry. No paid aggregator, now or
as a default assumption for future features.

## Consequences

### Positive

- No recurring cost, and no third-party data broker sitting in the data path.
- Nothing to revoke or rotate if a data-sharing agreement changes or an aggregator has an outage.

### Negative

- Every import is a manual "upload statement" action rather than a live sync.
- Category/rewards features (BIL-7/8/9, rewards optimizer) depend on statement data being
  reasonably complete and current, since there's no live feed to fall back on.

### Risks

- If a statement format changes (bank redesigns its export), the importer for that institution
  needs updating before that account's data flows again.

## Alternatives Considered

### Plaid/SimpleFIN/MX

Would automate the import step but costs money for personal use, and adds an ongoing external
dependency on a data broker. Rejected.

### Chase's own FDX API directly

Not practically available to an individual without acting as a registered data recipient
(security review, formal onboarding). Not viable as-is.

## References

- DD-0001: Architecture overview
- This same no-paid-service default applies elsewhere in the app unless a specific ADR revisits
  it (e.g. home/vehicle valuation is manual entry for the same reason — no free Zillow/KBB/CarFax
  API exists).

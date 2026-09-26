# ADR-0008: Store full account and reference numbers

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

The original requirements doc (and ADR-0006, following it) said "no full account numbers stored,
last 4 only." That came from a productized-app mindset. Kevin wants the real identifiers on record:
the AAA membership ID, the AT&T account number, loan numbers, card and bank account numbers. They
are useful on their own (looking one up when calling a biller) and, if statement import or an
aggregator like Plaid is ever tied in, they are the natural key for matching transactions and
balances to bills, loans and accounts.

## Decision

Bills, loans, cards and bank accounts each get a free-text `AccountNumber` (up to 60 characters)
stored as entered. No masking in the database. Bill and loan numbers show in full. Bank account and
card numbers show as •••• plus the last four everywhere, in lists and in editors, with a show/hide
toggle to reveal the full number on demand.

This amends ADR-0006's "last four only" line. Everything else in ADR-0006 stands: LAN-only, single
shared API key, TLS in transit, single user. It does not change ADR-0001 (no paid aggregator);
it just keeps that door easy to open later.

## Consequences

### Positive

- One place to find every account, member, policy and loan number.
- Statement import (Phase 4) can match on the number instead of on names.

### Negative

- The database now holds identifiers worth protecting. Acceptable on a home-network server with a
  single user; if the API is ever exposed beyond the LAN, revisit disk encryption on the server.

## Alternatives Considered

### Last four only (the original doc)

Rejected: not enough to match statements, and this is a personal tool, not a multi-tenant product.

## References

- ADR-0006: LAN-only, single API key, TLS, no MFA (amended)
- DD-0002: Core ledger

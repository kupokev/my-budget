# ADR-0006: LAN-only access, single API key, TLS in transit, no MFA

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

The original design doc listed "sign-in with MFA" and "encrypted at rest" as non-functional
requirements. This is a single-user app on a home network; those requirements were heavier than
the actual threat model. Kevin does want encryption in transit in case the phone app is ever used
away from home, though he expects to front that with cloudflared or similar rather than exposing
the API directly.

## Decision

- The API is reachable only on the home network. No public exposure is planned; remote access, if
  it happens, goes through a tunnel (cloudflared or equivalent) that Kevin sets up outside the app.
- Authentication is a **single shared API key** sent as a request header by every client. No user
  accounts, no login page, no MFA, no identity framework.
- **TLS in transit:** Kestrel serves HTTPS with a configurable certificate (the .NET dev cert in
  development, a locally issued cert in production).
- No application-level encryption at rest. Disk encryption on the server, if wanted, is an OS
  concern.
- Account numbers are still stored as last-four only, as in the original doc.

## Consequences

### Positive

- Nothing to log into; the desktop app just works on the LAN.
- One key to rotate, stored in the client's local config and the API's environment.

### Negative

- Anyone on the home network with the key has full access. Acceptable for this household.

### Risks

- If the API is ever exposed directly to the internet by mistake, an API key alone is thin
  protection. Mitigation: bind Kestrel to the LAN interface, document the tunnel approach in
  `docs/distribution/`.

## Alternatives Considered

### ASP.NET Core Identity with MFA

The original proposal. Rejected as disproportionate for one user on a LAN.

### No authentication at all

Tempting on a LAN, but the phone phase and any tunnel make a shared key worth the few lines it
costs. Rejected.

## References

- DD-0001: Architecture overview

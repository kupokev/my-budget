# ADR-0002: MAUI Blazor Hybrid for shared desktop/phone UI

> **Status:** Superseded by ADR-0004 (desktop host); shared-Razor-UI principle stands
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

Wanted a desktop app and, eventually, a phone app, both talking to the same backend, without
maintaining two separate frontends. Desktop is the priority for v1; phone should be addable later
without a rewrite.

## Decision

.NET MAUI Blazor Hybrid: one shared Razor UI component set, packaged as both a desktop app and a
phone app (iOS/Android), both calling the same ASP.NET Core API. Desktop ships first; phone is a
later phase using the same components.

## Consequences

### Positive

- UI code is written once (Razor components) and used by both shells; a phone-specific feature
  later means adding to the shared codebase, not forking it.
- Stays within the .NET stack throughout, matching existing background and tooling.

### Negative

- MAUI has its own platform quirks (packaging, per-platform testing) compared to a pure web app.

### Risks

- If MAUI proves troublesome on a particular platform, the fallback (Blazor Web App) is a real
  rework, not a small pivot.

## Alternatives Considered

### React frontend over the same API

Viable, but abandons the .NET-throughout preference and doesn't share code with a native shell as
directly.

### Blazor Web App (Server/WASM) in a browser

Simpler, no native app to install, but the goal is an actual desktop/phone app rather than a
browser tab. Kept as a fallback if MAUI proves troublesome.

## References

- DD-0001: Architecture overview

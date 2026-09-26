# ADR-0004: Photino.Blazor desktop host on Linux, MAUI only for Android

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

ADR-0002 chose .NET MAUI Blazor Hybrid for a shared desktop/phone UI. MAUI has no Linux desktop
target, and Kevin only uses Linux (Arch) on the desktop, with an Android phone for the later phone
phase. Windows and macOS are not in the picture. The semantic-modeler repo already hit this and
runs Photino.Blazor on Linux with a MAUI host for the other platforms, both over one shared Razor
class library.

## Decision

- All UI lives in one Razor class library (`MyBudget.UI`) with no host-specific code.
- The desktop app is a **Photino.Blazor** host (`MyBudget.Desktop`) targeting Linux. It is the
  only desktop host; no Windows or macOS builds.
- The phone app, when that phase comes, is a **MAUI Blazor Hybrid host for Android only**,
  referencing the same `MyBudget.UI` library. It is not created ahead of that phase.

This supersedes the "desktop via MAUI" part of ADR-0002. The shared-Razor-UI principle from
ADR-0002 stands.

## Consequences

### Positive

- Builds and runs on the machine the app is developed and used on, with no MAUI workload.
- Photino is a thin WebView host with few moving parts; the UI library stays host-agnostic so the
  Android host later is additive.

### Negative

- Two host projects eventually (Photino, MAUI Android) instead of one MAUI single-project.
- Photino uses the system WebKitGTK on Linux; a distro WebKit update can change rendering.

### Risks

- Anything that leaks host-specific APIs into `MyBudget.UI` (file dialogs, window control) breaks
  the Android host later. Keep such calls behind an interface implemented per host.

## Alternatives Considered

### MAUI Blazor Hybrid for desktop (ADR-0002 as written)

Not possible on Linux. Rejected.

### Blazor Web App in a browser

Would also work on Linux, but the goal is a desktop app rather than a browser tab. Kept as the
fallback if Photino proves troublesome.

### Avalonia or Electron.NET

Avalonia would mean a non-Razor UI, losing the shared-components goal. Electron.NET is far heavier
than Photino for the same result. Both rejected.

## References

- ADR-0002: MAUI Blazor Hybrid for shared desktop/phone UI (partly superseded)
- DD-0001: Architecture overview
- semantic-modeler repo: `SemanticModeler.App` (Photino) / `SemanticModeler.App.Maui`

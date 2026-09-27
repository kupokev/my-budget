# ADR-0007: No UI component framework; hand-written CSS plus QuickGrid

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

Blazor projects typically pull in a component framework (MudBlazor, Radzen, Bootstrap). Kevin has
used MudBlazor in semantic-modeler and it has caused several headaches; he doubts Bootstrap would
be better. His rule for this project: whichever option is lighter and lower maintenance.

## Decision

- **No third-party component or CSS framework.**
- Styling is one hand-written stylesheet in `MyBudget.UI` using CSS custom properties, with light
  and dark themes via `prefers-color-scheme`. Palette: black/charcoal and gray base with hunter
  green accents (Kevin's favourite colour), brighter green in dark mode for legibility.
- Tables use the first-party **Microsoft.AspNetCore.Components.QuickGrid** (ships with ASP.NET
  Core, same release cadence as the rest of the stack).
- Forms use built-in `EditForm` and input components. Small reusable pieces (money input, date
  picker wrapper, card/panel) are written locally as they are needed.
- Charts remain FactFoundry.Blazor.Charts per the original design.

## Consequences

### Positive

- Nothing to upgrade or fight; no framework theme system to bend to the app's needs.
- The whole look lives in one file the app owns.

### Negative

- Anything fancier than tables, forms, and panels (modal dialogs, autocomplete) is written by hand.
  The app has few screens and one user, so this cost is small.

### Risks

- Reinventing a component badly. Mitigation: keep components minimal and add them only when a
  screen actually needs them.

## Alternatives Considered

### MudBlazor

Known headaches for Kevin. Rejected.

### Bootstrap (Blazor template default)

Lighter than MudBlazor but still a framework whose version and markup conventions have to be
tracked, and the Blazor template's Bootstrap copy is stale. Rejected as not clearly better.

### Tailwind / Pico / other CSS-only libraries

Pico-style classless CSS would be nearly as light, but a hand-written sheet is one fewer
dependency for a single-user app with a handful of screens.

## References

- DD-0001: Architecture overview
- ADR-0004: Photino.Blazor desktop host on Linux

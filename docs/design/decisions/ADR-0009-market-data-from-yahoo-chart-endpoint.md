# ADR-0009: Market data from Yahoo Finance's unauthenticated chart endpoint

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin (by the no-paid-integrations principle), Claude

## Context

INV-2/INV-3 need dividend history (ex-date, per-share amount) and daily prices for a handful of
tickers, automatically, at no cost. Keyed free tiers (Alpha Vantage, Finnhub) ration requests or
put dividends behind paid plans; Stooq has prices but not dividends.

## Decision

Use Yahoo Finance's `v8/finance/chart/{ticker}?events=div` endpoint: no key, daily closes and
dividend events (ex-date + amount) in one call. It sits behind an `IMarketDataProvider` interface
so it can be swapped. Every fetched value is stored with `Source = Fetched` and can be overridden
by hand; a failed fetch is reported per ticker and never blocks the page.

## Consequences

### Positive

- Zero cost, one request per ticker per refresh, dividends and prices together.

### Negative

- Unofficial: Yahoo can change or block it. When it breaks, the provider is replaced or the
  numbers are typed in; nothing else in the app depends on the source.
- Dividend events carry the ex-date only; pay date is left blank and DRIP reinvests at the close
  on the ex-date unless the pay date is edited.

## Alternatives Considered

- **Alpha Vantage / Finnhub free keys:** rationed or dividends paywalled. Rejected for now.
- **Manual entry only:** always available as the fallback; too tedious as the primary path.

## References

- DD-0006 Investments

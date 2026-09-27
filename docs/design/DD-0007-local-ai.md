# DD-0007: Local AI, tool-calling only (Phase 5)

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0003

## Design

- **Transport:** Ollama `/api/chat` with `tools`, `stream:false`, low temperature. Configured by
  `Ai:Enabled`, `Ai:BaseUrl`, `Ai:Model`, `Ai:MaxToolRounds`; off by default.
- **Tools (`AiTools.Catalog`):** `spend_by_category`, `account_balances`, `upcoming_bills`,
  `transfer_needs`, `bill_status`, `rewards_progress`, `hsa_plan`, `goals_progress`, `net_worth`,
  `portfolio`. Each is a thin wrapper over the same internal function the corresponding page
  calls, so the model's number is the page's number. The model never receives a query interface.
- **Loop (`AiService.ChatAsync`):** system prompt forbids guessing and fixes the refusal wording;
  tool calls are executed, results appended as `tool` messages, up to MaxToolRounds; every call
  (name, arguments, result) is returned for the audit trail the Assistant page shows.
- **Monthly narrative (AI-1):** a fixed prompt that names the tools to call for the month and
  restricts the narrative to their results.
- **Adding a question:** add a tool to the catalog and a case in `InvokeAsync`; nothing else.

## Status

Built 2026-09-26, untested against a live Ollama (none reachable here). The status endpoint reports
reachability; the chat endpoint returns 400 while disabled.

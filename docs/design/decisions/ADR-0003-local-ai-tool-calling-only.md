# ADR-0003: Local AI restricted to tool-calling

> **Status:** Accepted
> **Date:** 2026-09-26
> **Deciders:** Kevin

## Context

Wanted an AI-generated monthly narrative summary and a chat window to ask questions about the
data, running on the user's existing Ollama + Open WebUI instance (private, free). The user had
already seen poor accuracy from AI systems that access a database directly, and did not want that
repeated here.

## Decision

The model never writes or runs its own database query. It only calls a small, fixed, tested set of
query functions (spend by category, account balance, rewards progress, bill status, etc.) and
narrates only what those functions return. A question outside that set gets an honest "can't
answer that yet," never a guess. New functions are added deliberately as new questions come up —
the model never invents one at runtime.

## Consequences

### Positive

- Every answer is traceable to a tested function's real output — no hallucinated joins or
  invented numbers.
- The narrative summary (AI-1) and chat (AI-2) both consume the same function set, so accuracy
  guarantees apply equally to both features.

### Negative

- Every new kind of question requires a new tool function before the AI can answer it — this is a
  deliberate limitation, not a gap to be smoothed over with a more "flexible" data-access mode.

### Risks

- If the tool-function set grows large and ad hoc over time, it could become its own maintenance
  burden; worth periodically consolidating overlapping functions.

## Alternatives Considered

### Natural-language to SQL

The model translates a question into a SQL query and runs it directly against the database.
Rejected: this is exactly the failure mode the user had already experienced elsewhere — wrong
joins, hallucinated columns, confidently wrong numbers.

### RAG over exported data

Retrieve relevant rows as context and let the model reason over them. Rejected for the same
reason as NL-to-SQL: still lets the model's judgment stand between the real number and the
answer, rather than a tested function doing the arithmetic.

## References

- DD-0001: Architecture overview

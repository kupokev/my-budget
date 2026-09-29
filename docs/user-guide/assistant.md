# Assistant

**What it answers:** questions about your own numbers, in plain English.

Ask "what did I spend on groceries in August?" or "how close am I to Diamond?" and it answers from
your data.

## Setting it up

Off until you configure it in [Settings](settings.md). It runs against **your own** local AI server
(Ollama) — nothing is sent to a third party. You need a model that supports tool calling; llama3.1
and qwen2.5 both work.

## What it can and cannot do

The model **never** writes or runs a database query. It can only call a fixed set of tested
functions: spend by category, account balances, upcoming bills, transfer needs, bill status, rewards
progress, HSA plan, goal progress, net worth.

It then describes what those returned. Ask something outside that set and it says it cannot answer
yet, rather than guessing.

Every answer has a **What it looked at** disclosure listing exactly which functions were called and
what they returned. If an answer looks wrong, that shows you whether the model misread the data or
the data itself is wrong.

## Why it works this way

A model allowed to query your finances freely will eventually invent a number and state it
confidently. Restricting it to tested functions means every figure it quotes came from the same code
that draws the screens.

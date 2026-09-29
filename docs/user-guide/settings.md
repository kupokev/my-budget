# Settings

**What it answers:** how do I back this up, and how do I turn on the assistant?

## Backup and restore

**Export budget** writes your entire budget to a single `.mybudget` file. It is a consistent snapshot
taken properly, not a copy of a file that is in use, so it is safe to take while the app is running.

Do this **before upgrading versions** and before any large import.

**Import budget** restores one. It checks the file really is a MyBudget database before doing
anything, keeps a copy of what is being replaced, and stages the restore — the swap happens when you
next start the app, when nothing holds the file open. That is what makes it safe.

This is also how you give someone else their own copy: export, hand them the file, they import it.

## Local AI

The assistant runs against your own Ollama instance. Nothing is sent to a third party.

| Field | Notes |
| --- | --- |
| Enabled | Off by default |
| Base URL | Your Ollama address, e.g. `http://localhost:11434` |
| API key | Only if your server needs one |
| Model | Must support tool calling — llama3.1 or qwen2.5 |
| Max tool rounds | How many function calls one answer may make |

**Test connection** probes the address *without saving it* and lists the models your server actually
has, so you pick from a list rather than typing from memory.

See [Assistant](assistant.md) for what it can and cannot do.

## Where your data lives

```
~/.local/share/MyBudget/mybudget.db
```

One file. Installing a new version never touches it.

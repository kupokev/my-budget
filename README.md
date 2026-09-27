# MyBudget

A personal budget app that runs entirely on your own machine. One process, one SQLite file, no
account to create, no service to host, and nothing sent anywhere.

It does the ordinary things — bills, accounts, credit cards, debts, transactions — plus four that a
spreadsheet handles badly:

- **Paycheck estimates.** Federal and Missouri withholding per check and per year, including
  bonuses, supplemental pay and 1099 income, worked through the IRS percentage method.
- **HSA planning.** Month-by-month eligibility, the annual limit that follows from it, and the
  contribution pace needed to reach it.
- **Card rewards.** Spend thresholds and loyalty status against a projected year, weighed against
  any per-bill discount for paying from a bank account instead.
- **Investments.** Manual trades or a tax-lot import, automatic price and dividend fetch, DRIP,
  short and long-term gain classification, wash-sale checks.

There is an optional assistant that runs against your own Ollama instance. It never writes a database
query: it calls a fixed set of tested functions and narrates what they return, and says so plainly
when a question falls outside them.

> Built for one person's use, not as a product. It's public because the code may be useful to
> someone, not because it's supported. No multi-user support, and none planned.

## Install

Grab a package from [Releases](https://github.com/kupokev/my-budget/releases).

| Distribution | Command |
| --- | --- |
| Debian, Ubuntu | `sudo apt install ./mybudget_<version>_amd64.deb` |
| Fedora, RHEL | `sudo dnf install ./mybudget-<version>-1.x86_64.rpm` |
| Arch | `sudo pacman -U ./mybudget-<version>-1-x86_64.pkg.tar.zst` |
| Anything else | `chmod +x MyBudget-<version>-x86_64.AppImage` and run it |

Each package bundles the .NET runtime, so nothing needs installing first — except **WebKitGTK**,
which draws the window. The deb, rpm and pacman packages pull it in. The AppImage can't, so it
expects it to be there already; that's the usual reason an AppImage opens no window.

Linux x86-64 only. Android is a later phase using the same UI.

## Your data

Everything lives in one file:

```
~/.local/share/MyBudget/mybudget.db
```

It is an ordinary SQLite database, outside anything a package owns, so installing, upgrading or
removing MyBudget never touches it. Schema changes are applied as migrations when the app starts, so
a database from an older version comes forward on its own.

**Admin → Settings → Back up and restore** exports the whole budget as one file and restores one.
Worth doing before a version jump, and it's also how you'd hand someone their own copy.

## Building from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and WebKitGTK.

```
git clone https://github.com/kupokev/my-budget.git
cd my-budget
dotnet test          # 160-odd tests, no database required
./run.sh             # stop, build, launch
```

`run.sh` exists because the running app holds its copy of the UI assembly open: building while it
runs leaves a stale copy and the app shows old markup with no error anywhere. The script stops it
first, verifies the assembly actually refreshed, and refuses to launch if it didn't.

To build the Linux packages locally, see [docs/distribution](docs/distribution/README.md).

## How it's put together

```
MyBudget.Desktop (one process)
  ├── Photino window ──► MyBudget.UI (Razor) ──► ApiClient ──► HTTP to 127.0.0.1
  └── the API        ──► minimal APIs ──► EF Core ──► SQLite
```

The desktop app hosts the API inside itself on a loopback port the OS assigns, with a key generated
per run. The HTTP hop looks redundant for a single-machine app and is deliberate: a phone will need
something to sync against, and this way it gets a working API rather than a port of one. Setting
`MYBUDGET_API_URL` points the app at a remote API instead.

| Project | What it holds |
| --- | --- |
| `MyBudget.Domain` | Entities. No dependencies. |
| `MyBudget.Engines.*` | Paycheck, HSA, amortization, rewards, import, investments, ledger. Pure libraries with no UI or database, tested against real figures. |
| `MyBudget.Contracts` | DTOs shared by the API and the UI. |
| `MyBudget.Data` | EF Core context, migrations, seed. |
| `MyBudget.Api` | Minimal APIs. Also runs standalone, for a future sync server. |
| `MyBudget.UI` | Razor class library. Hand-written CSS, no component framework. |
| `MyBudget.Desktop` | Photino host. Starts the API in-process. |

Two rules the code sticks to. **Every calculated number is traceable**: anything computed carries the
formula that produced it, and the UI shows it on hover. And **no paid integrations**: statements are
imported from CSV or OFX files, market data comes from a free endpoint, and home and vehicle values
are typed in.

Design documents and the decision record are in [docs/design](docs/design/DESIGN.md). Each notable
decision has an ADR explaining what was rejected and why.

## Built with AI assistance

Most of this code was written by Claude, working from my direction, in an extended pairing session.
I chose what it should do and how it should behave; Claude wrote it, and I reviewed and steered as it
went. The reasoning behind the notable decisions is recorded in the
[ADRs](docs/design/decisions) rather than left implicit, so you can see what was considered and
rejected, not just what was built.

Two things that should temper how much you trust it. The calculation engines are covered by tests
written against real figures rather than convenient ones, but **the tax logic in particular has not
been verified against an accountant** — federal and Missouri withholding are implemented from the IRS
percentage method and published tables, and `docs/audit` is where estimates get checked against real
pay stubs. Check anything that matters to you. And treat the numbers as a planning aid, not advice.

## Contributing

It's a personal project with one user, so there's no roadmap to contribute against and issues may sit.
Fork it freely — that's what the licence is for.

## Licence

[MIT](LICENSE).

Built on ASP.NET Core and Blazor, Entity Framework Core, SQLite via SQLitePCLRaw, Photino,
FactFoundry.Blazor.Charts and Npgsql, each under its own licence. Every release carries a
`THIRD-PARTY-NOTICES.md` listing all of them including transitive packages, generated from that
build's own dependency graph; installed packages put a copy in `/usr/share/doc/mybudget/`.

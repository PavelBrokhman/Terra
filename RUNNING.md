# Running Terra

How to build and run the project. Two runnable targets: the **console**
(`Terra.Console`, text feed) and the **browser viewer** (`Terra.Web`, live SSE feed).

## Prerequisites

- **.NET 8 SDK** (`net8.0`). Check with:
  ```bash
  dotnet --version      # expect 8.x
  ```
  Install from <https://dotnet.microsoft.com/download/dotnet/8.0> if missing.

No database, network, or other services are required — Phase 1 is fully
in-memory and writes only to stdout (and optionally a file).

## Build

From the repository root:

```bash
dotnet build Terra.sln
```

A clean build produces **0 warnings**. Treat any warning as a regression.

## Run

```bash
dotnet run --project src/Terra.Console
```

This runs with defaults and prints a header, per-tick output, and a final
summary. Use `--help` to see all options:

```bash
dotnet run --project src/Terra.Console -- --help
```

> Everything after `--` is passed to the app, not to `dotnet`.

### Options

All options have defaults; override with `--key=value` or `--key value`.

| Option | Default | Meaning |
|--------|---------|---------|
| `--ticks=N` | `200` | number of ticks to run |
| `--seed=N` | `42` | PRNG seed (determinism — see below) |
| `--width=N` | `400` | world width in pixels |
| `--height=N` | `400` | world height in pixels |
| `--plants=N` | `30` | initial plant count |
| `--herbivores=N` | `10` | initial herbivore count |
| `--carnivores=N` | `3` | initial carnivore count |
| `--json-file=PATH` | _(off)_ | also write a JSON-Lines event stream to `PATH` |
| `--quiet` | _(off)_ | suppress per-tick summary lines (keeps births/deaths/etc.) |
| `--infant` | _(off)_ | spawn the initial population at radius 1 (shows growth) |
| `--creatures=PATH` | _(off)_ | load behaviour from a DSL `.json` file or folder (see below) |
| `--help`, `-h` | — | print help and exit |

### Custom creatures (DSL)

Behaviour can be authored as **text (JSON) rules** instead of compiled code — the
in-process DSL adapter of `IOrganismBehavior`. Point `--creatures` at a file or a
folder of `*.json`; the population is built from them (per-kind counts from
`--plants`/`--herbivores`/`--carnivores`). Sample creatures live in `creatures/`:

```bash
dotnet run --project src/Terra.Console -- --ticks=400 --seed=7 --creatures=creatures --quiet
```

A creature file (see `creatures/grazer.json`):

```json
{
  "name": "Grazer",
  "species": "Herbivore",
  "prey": "Plant",
  "threat": "Carnivore",
  "rules": [
    { "when": "threat_in_range",   "do": "defend",   "priority": 100 },
    { "when": "prey_in_eat_range", "do": "eat",      "priority": 40 },
    { "when": "prey_visible",      "do": "approach", "priority": 30 },
    { "when": "always",            "do": "wander",   "priority": 1 }
  ]
}
```

The highest-priority rule whose `when` signal is active wins. Signals: `always`,
`can_reproduce`, `hungry`, `not_full`, `threat_in_range`, `prey_in_eat_range`,
`prey_in_attack_range`, `prey_visible`, `carcass_in_range`, `carcass_visible`.
Actions: `idle`, `reproduce`, `wander`, `defend`, `eat`, `eat_carcass`, `attack`,
`approach`, `approach_carcass`.

### Examples

```bash
# Short deterministic run
dotnet run --project src/Terra.Console -- --ticks=100 --seed=42

# Bigger world, more organisms
dotnet run --project src/Terra.Console -- --width=800 --height=800 --plants=80 --herbivores=25 --carnivores=6

# Capture the machine-readable event stream while watching a quiet console
dotnet run --project src/Terra.Console -- --ticks=500 --quiet --json-file=run.jsonl
```

Press **Ctrl-C** at any time — the run stops gracefully via a cancellation
token and still prints the final summary.

## Output

Two independent presentation layers consume the **same** event stream:

- **stdout** — human-readable lines (`HumanTextFormatter`; `--quiet` drops the
  per-tick `TickCompleted` summaries).
- **JSON-Lines file** (when `--json-file` is set) — one JSON object per event,
  the stable machine-readable schema. Consume it with any JSONL tooling, e.g.:
  ```bash
  cat run.jsonl | jq 'select(.type == "OrganismDied")'
  ```

The engine never knows which presenters are attached — they are just
subscribers on the event bus.

## Determinism

The same `--seed` produces an **identical** event stream (verified by a test).
Simulation time is tick-based, never wall-clock. When filing a bug, include the
exact command line (especially `--seed`, `--ticks`, and the counts) so the run
can be reproduced bit-for-bit.

## Web viewer (browser UI)

A browser-based live view of the same simulation — the engine streams its event
feed to the page over **Server-Sent Events** (logic is decoupled from rendering;
the web client is just another subscriber).

```bash
dotnet run --project src/Terra.Web
# then open http://localhost:5000
```

Open it in a **real browser** (Chrome / Edge / Firefox) — **not** VS Code's
embedded Simple Browser, which struggles with the live SSE feed.

The page has a **ticks** box (default 5000) and a **Run** button — set the tick
count and click **Run** to start (or restart) a run. It shows a live event log
(born / moved / ate / attack / died …) plus a P/H/C stats line, updating per
tick. Other run parameters come from the `Simulation` config section:

```bash
# DSL creatures + custom params
dotnet run --project src/Terra.Web -- --Simulation:Creatures=creatures --Simulation:Seed=7 --Simulation:TickDelayMs=100
```

| Setting | Default | Meaning |
|---------|---------|---------|
| `Simulation:Seed` | `42` | PRNG seed |
| `Simulation:Width` / `:Height` | `400` | world size |
| `Simulation:MaxTicks` | `5000` | default tick budget (the Run box overrides it) |
| `Simulation:TickDelayMs` | `150` | wall-clock pacing between ticks (so it's watchable) |
| `Simulation:Plants` / `:Herbivores` / `:Carnivores` | `30`/`10`/`3` | per-kind counts |
| `Simulation:Creatures` | _(off)_ | DSL creature file/folder |
| `Simulation:StreamMoves` | `false` | also stream `OrganismMoved` (off = much lighter feed) |

> A browser that connects mid-run sees events from connect time onward (no
> history replay yet).

## Project layout

```
src/
  Terra.Engine/              core simulation — no dependencies beyond BCL
  Terra.Behaviors.Default/   reference Plant / Herbivore / Carnivore behaviors
  Terra.Behaviors.Dsl/       text-file (JSON) creature behaviour interpreter
  Terra.Presentation.Text/   human + JSON-Lines event formatters
  Terra.Console/             CLI entry point (text/console)
  Terra.Web/                 ASP.NET Core browser viewer (SSE live feed)
tests/
  Terra.Engine.Tests/        xUnit tests (see TESTING.md)
```

## Running the published binary (optional)

To produce a standalone executable instead of `dotnet run`:

```bash
dotnet publish src/Terra.Console -c Release -o ./publish
./publish/Terra.Console --ticks=100 --seed=42        # .\publish\Terra.Console.exe on Windows
```

## See also

- [TESTING.md](TESTING.md) — how to run and write tests
- [Plans/Phase1_Status.md](Plans/Phase1_Status.md) — what is and isn't implemented
- [Plans/Terrarium2.0_Modernization.md](Plans/Terrarium2.0_Modernization.md) — roadmap

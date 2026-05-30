# Running Terra

How to build and run the project. Current runnable target is the **Phase 1**
console simulation (`Terra.Console`).

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
| `--help`, `-h` | — | print help and exit |

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

## Project layout

```
src/
  Terra.Engine/              core simulation — no dependencies beyond BCL
  Terra.Behaviors.Default/   reference Plant / Herbivore / Carnivore behaviors
  Terra.Presentation.Text/   human + JSON-Lines event formatters
  Terra.Console/             CLI entry point (thin wiring layer)
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

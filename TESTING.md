# Testing Terra

How to run, filter, and write tests. Tests live in `tests/Terra.Engine.Tests`
(the simulation core, 162) and `tests/Terra.Node.Tests` (Phase 3 networking
domain — zones, quota, ownership, spawn policy, tick pacing, 41). Both use
**xUnit**. `dotnet test` from the root runs all 203.

## Prerequisites

- **.NET 8 SDK** (same as building — see [RUNNING.md](RUNNING.md)).

No extra setup: the test project restores `xunit`, `xunit.runner.visualstudio`,
`Microsoft.NET.Test.Sdk`, and `coverlet.collector` on first build.

## Run all tests

From the repository root:

```bash
dotnet test
```

The suite should be **all green with 0 warnings**. A failing test or a new
warning is a regression — fix it before pushing.

## Run a subset

`dotnet test` supports `--filter` (xUnit `FullyQualifiedName` / trait matching):

```bash
# One test class
dotnet test --filter "FullyQualifiedName~SimulationTests"

# One test method
dotnet test --filter "FullyQualifiedName~SimulationTests.Run_IsDeterministic_ForSameSeed"

# Everything matching a keyword
dotnet test --filter "FullyQualifiedName~GameRules"
```

Useful flags:

```bash
dotnet test -v minimal          # quieter output
dotnet test --no-build          # skip rebuild when binaries are current
dotnet test --logger "console;verbosity=detailed"
```

## Coverage

`coverlet.collector` is referenced, so you can collect coverage without extra
installs:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

This writes a `coverage.cobertura.xml` under `TestResults/`. Turn it into an
HTML report with [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
if you want a browsable view:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport
```

## What is covered

The current suite exercises the engine end to end. By file:

| Test file | Focus |
|-----------|-------|
| `PositionTests` | coordinate / distance math |
| `SpeciesTraitsTests` | trait records and validation |
| `GameRulesTests` | ported formulas (energy, lifespan, speed, metabolism, …) |
| `SpatialGridTests` | spatial grid bucketing / neighbor queries |
| `WorldTests` | world state, add/remove, organism count |
| `EventBusTests` | publish/subscribe, multiple subscribers |
| `OrganismActionTests` | action records (Idle / Move / Eat / Attack / Defend / Reproduce) |
| `DefaultBehaviorsTests` | reference Plant / Herbivore / Carnivore behavior |
| `PresentationTests` | human + JSON-Lines formatters, polymorphic event roundtrip |
| `SimulationTests` | tick loop, movement, death, collision/clip, carcass+rot, camouflage, action feedback, and **determinism for a fixed seed** |
| `EatActionTests` | herbivore eats plant, full-eater rule, food chunks, diet rules |
| `CombatTests` | attack/defense rolls, damage, defend, kill threshold, Defended event |
| `GrowthTests` | growth toward mature radius |
| `ReproductionTests` | incubation, offspring, cooldown, seed spreading, repro events |
| `DslTests` | DSL interpreter (signals → actions), priority, loader |

### `tests/Terra.Node.Tests` — Phase 3 networking domain

| Test file | Focus |
|-----------|-------|
| `ZoneGridTests` | fixed zone grid: full coverage of the world, issue/release, full-world refusal |
| `QuotaTests` | borrowed quota: take, overdraw refusal, release, return in full |
| `OwnerIndexTests` | ownership derived from `Species` **by reference** — identical record definitions from two participants must not merge |
| `SpawnPlannerTests` | the three spawn policies, no anchor after dying out, bounded retries when packed |
| `ParticipantRegistryTests` | join/leave, timeout sweep, extinction returning quota + zone |
| `TickPacerTests` | fixed vs adaptive pacing, floor and ceiling |

## Conventions for new tests

- **Mirror the structure.** One test class per production type, named
  `<Type>Tests`, in the test project matching the source project
  (`tests/Terra.Engine.Tests` for `src/Terra.Engine`,
  `tests/Terra.Node.Tests` for `src/Terra.Node`).
- **Naming.** `MethodOrBehavior_ExpectedResult_Condition`
  (e.g. `Run_IsDeterministic_ForSameSeed`). Keep it descriptive — `--filter`
  matches on these names.
- **Arrange / Act / Assert.** Keep the three sections visually distinct.
- **Determinism first.** Anything touching the simulation must be reproducible:
  pass an explicit `Seed` (via `SimulationConfig`) or a seeded `Random`. Never
  rely on wall-clock time, `Guid.NewGuid()`, or unordered-collection iteration
  order in assertions — these break reproducibility and are the project's #1
  correctness invariant.
- **Test through public contracts.** Prefer driving behavior via
  `IOrganismBehavior`, `IEventBus`, and the public engine API rather than
  reaching into internals.
- **No warnings.** The build is warning-clean; keep it that way (nullable
  annotations included).

## CI expectation

Before pushing, a green local run of:

```bash
dotnet build Terra.sln    # 0 warnings
dotnet test               # all pass
```

is the minimum bar. Reproduce any reported simulation bug with its `--seed`
before fixing, and add a regression test that pins the corrected behavior.

## See also

- [RUNNING.md](RUNNING.md) — build and run the app
- [Plans/Phase1_Status.md](Plans/Phase1_Status.md) — coverage gaps and deferred mechanics

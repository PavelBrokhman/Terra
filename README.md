# Terra

Modern reimagining of Microsoft Research's **Terrarium 2.0** — an AI programming game where user-written organisms live and compete in a shared simulated ecosystem.

## Status

**Phase 1 + 2** ✅ — full ecosystem mechanics, text + JSON-Lines event streams,
a **browser viewer** (SSE), and **text-file (DSL) creature behaviour**.
See [Plans/Phase1_Status.md](Plans/Phase1_Status.md) for what is and isn't implemented.

Quick run:

```bash
# console (text feed)
dotnet run --project src/Terra.Console -- --ticks=100 --seed=42 --creatures=creatures

# browser viewer — then open http://localhost:5000
dotnet run --project src/Terra.Web
```

162 passing tests, 0 warnings.

See [RUNNING.md](RUNNING.md) for full build/run instructions and [TESTING.md](TESTING.md) for test guidance.

## Project Structure

```
Terra/
├── Plans/                      Design docs, game-rules extract, status
│   ├── 02_GodSim_Design_Review.md  God-sim direction: locked decisions
│   └── source/                 Original design specs (reference)
├── src/
│   ├── Terra.Engine/           Core simulation (no dependencies)
│   ├── Terra.Behaviors.Default/  Reference Plant/Herbivore/Carnivore
│   ├── Terra.Presentation.Text/  Human + JSON-Lines event formatters
│   └── Terra.Console/          CLI entry point
├── tests/
│   └── Terra.Engine.Tests/     xUnit tests
├── legacy/
│   └── Terrarium2.0/           Original MSR source (reference only)
└── Terra.sln
```

## Roadmap

See [Plans/Terrarium2.0_Modernization.md](Plans/Terrarium2.0_Modernization.md) for the full modernization plan,
and [Plans/02_GodSim_Design_Review.md](Plans/02_GodSim_Design_Review.md) for the locked design decisions
that steer the project toward a multiplayer god-sim.

Design source material (preserved verbatim as reference):

- [Plans/source/01_World_System_Spec.md](Plans/source/01_World_System_Spec.md) — God Simulation World System
- [Plans/source/02_Engine_Spec.md](Plans/source/02_Engine_Spec.md) — God-Simulation Engine (full system spec)
- [Plans/source/03_Balance_Variants.md](Plans/source/03_Balance_Variants.md) — Balance / constraint variants
- [Plans/source/04_Animal_World_Spec.md](Plans/source/04_Animal_World_Spec.md) — Original "Животный мир" game spec (client-server, microservice behaviour, tick modes)

High-level phases:

- **Phase 0** ✅ — Source control, scaffolding
- **Phase 1** ✅ — Text-only MVP: simulation engine + text event stream, no graphics
- **Phase 2** ⏸️ — User-loadable organism sandbox (also: Eat/Attack/Reproduce actions)
- **Phase 3** ⏸️ — Visual presentation layers (2D / 2.5D / 3D)
- **Phase 4** ⏸️ — Networked multi-user ecosystem

## Architectural principle

**Logic is decoupled from presentation.** The simulation engine emits a versioned event stream. Text output, 2D rendering, 3D rendering, and web viewers are all independent subscribers — the engine never knows which are connected.

## License

MIT — see [LICENSE](LICENSE).

## Legacy

The `legacy/Terrarium2.0/` directory contains the original Microsoft Research Terrarium 2.0 source code, preserved as reference for game rules and balance parameters. Its original license applies (see `legacy/Terrarium2.0/license.rtf`). It is not built as part of this project.

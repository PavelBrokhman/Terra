# Terra

Modern reimagining of Microsoft Research's **Terrarium 2.0** — an AI programming game where user-written organisms live and compete in a shared simulated ecosystem.

## Status

**Phase 0** — Source control setup. No code yet.

## Project Structure

```
Terra/
├── Plans/              Design documents and modernization roadmap
├── legacy/
│   └── Terrarium2.0/   Original MSR Terrarium 2.0 source (reference only)
└── README.md
```

## Roadmap

See [Plans/Terrarium2.0_Modernization.md](Plans/Terrarium2.0_Modernization.md) for the full modernization plan.

High-level phases:

- **Phase 0** — Source control, scaffolding *(current)*
- **Phase 1** — Text-only MVP: simulation engine + text event stream, no graphics
- **Phase 2** — User-loadable organism sandbox
- **Phase 3** — Visual presentation layers (2D / 2.5D / 3D)
- **Phase 4** — Networked multi-user ecosystem

## Architectural principle

**Logic is decoupled from presentation.** The simulation engine emits a versioned event stream. Text output, 2D rendering, 3D rendering, and web viewers are all independent subscribers — the engine never knows which are connected.

## License

MIT — see [LICENSE](LICENSE).

## Legacy

The `legacy/Terrarium2.0/` directory contains the original Microsoft Research Terrarium 2.0 source code, preserved as reference for game rules and balance parameters. Its original license applies (see `legacy/Terrarium2.0/license.rtf`). It is not built as part of this project.

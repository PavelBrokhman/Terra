namespace Terra.Engine.Events;

/// <summary>
/// Base type for all domain events emitted by the simulation engine. Events
/// form a **public, versioned schema** — they are not just for internal
/// debugging. Presenters (text, 2D, 3D, network stream) are pure subscribers:
/// the engine does not know how many, if any, are attached.
///
/// All events carry the tick at which they occurred. Timestamps are
/// tick-based, never wall-clock, to preserve determinism and replay fidelity.
/// </summary>
public abstract record SimulationEvent(int Tick);

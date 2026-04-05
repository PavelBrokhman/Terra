using System.Text.Json.Serialization;

namespace Terra.Engine.Events;

/// <summary>
/// Base type for all domain events emitted by the simulation engine. Events
/// form a **public, versioned schema** — they are not just for internal
/// debugging. Presenters (text, 2D, 3D, network stream) are pure subscribers:
/// the engine does not know how many, if any, are attached.
///
/// All events carry the tick at which they occurred. Timestamps are
/// tick-based, never wall-clock, to preserve determinism and replay fidelity.
///
/// The <see cref="JsonPolymorphic"/> metadata makes this type part of the
/// JSON-Lines schema: every serialized event carries a "type" discriminator
/// naming its concrete subclass.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SimulationStarted), nameof(SimulationStarted))]
[JsonDerivedType(typeof(SimulationEnded), nameof(SimulationEnded))]
[JsonDerivedType(typeof(TickCompleted), nameof(TickCompleted))]
[JsonDerivedType(typeof(OrganismBorn), nameof(OrganismBorn))]
[JsonDerivedType(typeof(OrganismMoved), nameof(OrganismMoved))]
[JsonDerivedType(typeof(OrganismDied), nameof(OrganismDied))]
[JsonDerivedType(typeof(OrganismAte), nameof(OrganismAte))]
[JsonDerivedType(typeof(OrganismGrown), nameof(OrganismGrown))]
[JsonDerivedType(typeof(OrganismAttacked), nameof(OrganismAttacked))]
public abstract record SimulationEvent(int Tick);

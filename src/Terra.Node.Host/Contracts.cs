namespace Terra.Node.Host;

/// <summary>
/// The wire contract between a world and a participant. Shared by both sides on
/// purpose: in Terra there is no server type and no client type, only a node that
/// may host a world, join someone else's, or do both (Plans/Phase3_Milestones.md, T3).
/// Transport is REST for control and SSE for the state stream — everything here is
/// plain JSON so another implementation could speak it without this assembly.
/// </summary>
public sealed record SpeciesDto(string Name, string Kind);

/// <summary>What a world says about itself before anyone connects. Onboarding is
/// two-sided: the participant reads this and decides whether the terms suit them (T7).</summary>
public sealed record ConditionsDto(
    string WorldName,
    int Width,
    int Height,
    string TickMode,
    double MinTickMs,
    double MaxTickMs,
    int FreeZones,
    int QuotaOnJoin,
    bool HasRoom,
    int Tick,
    int Living,
    int Participants);

public sealed record ZoneDto(int Index, int X, int Y, int Width, int Height);

public sealed record JoinRequest(string Name, IReadOnlyList<SpeciesDto> Species, string Policy);

public sealed record JoinResponse(int ParticipantId, ZoneDto Zone, int Quota, int Placed, string? Note);

public sealed record PolicyRequest(string Policy);

/// <summary>
/// Manual top-up. The anchor is an organism id, not a coordinate — a participant
/// may not know any coordinates at all. Omitting it lets the spawn policy choose.
/// </summary>
public sealed record TopUpRequest(string SpeciesName, int Count, int? AnchorOrganismId);

public sealed record TopUpResponse(int Placed, string? Refusal);

/// <summary>The participant reports its own observed round-trip; the world has no
/// other way to know how slow its participants are.</summary>
public sealed record HeartbeatRequest(double LastRoundTripMs);

public sealed record HeartbeatResponse(int Tick, int Living, int QuotaUsed, int QuotaTotal, double NextTickMs);

public sealed record ParticipantDto(
    int Id,
    string Name,
    int ZoneIndex,
    int QuotaTotal,
    int QuotaUsed,
    int Living,
    string Policy,
    double SecondsSinceSeen);

public sealed record RefusalResponse(string Refusal);

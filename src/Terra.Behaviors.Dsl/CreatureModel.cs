namespace Terra.Behaviors.Dsl;

/// <summary>
/// Declarative creature definition loaded from a text (JSON) file — the
/// in-process counterpart of a legacy creature DLL: behaviour as data, not code.
/// Interpreted by <see cref="RuleInterpreter"/> against the engine's
/// <c>IOrganismBehavior</c> contract.
/// </summary>
public sealed class CreatureModel
{
    /// <summary>Display name of the creature.</summary>
    public string Name { get; set; } = "Unnamed";

    /// <summary>Species kind: Plant | Herbivore | Carnivore.</summary>
    public string Species { get; set; } = "Herbivore";

    /// <summary>Kind this creature seeks/hunts (e.g. Plant, Herbivore), or null.</summary>
    public string? Prey { get; set; }

    /// <summary>Kind this creature defends against (e.g. Carnivore), or null.</summary>
    public string? Threat { get; set; }

    /// <summary>Behaviour rules. The highest-priority rule whose signal is active wins.</summary>
    public List<Rule> Rules { get; set; } = new();
}

/// <summary>One behaviour rule: when a signal is active, emit an action.</summary>
public sealed class Rule
{
    /// <summary>Signal that must hold for this rule to fire (see <see cref="RuleInterpreter"/>).</summary>
    public string When { get; set; } = "always";

    /// <summary>Action to perform when the rule fires.</summary>
    public string Do { get; set; } = "idle";

    /// <summary>Higher wins when several rules match in the same tick.</summary>
    public int Priority { get; set; }
}

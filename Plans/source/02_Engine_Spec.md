# GOD-SIMULATION ENGINE — FULL SYSTEM DESIGN SPEC

> Исходный документ (input владельца проекта), сохранён дословно как референс.
> Производные решения см. в [../02_GodSim_Design_Review.md](../02_GodSim_Design_Review.md).

## 0. CORE PRINCIPLE (NON-NEGOTIABLE RULE)

The game engine defines no lifeform behavior logic whatsoever.

The engine only provides:
- World simulation systems
- Physics/environment rules
- Execution of user-defined entity models
- Deterministic tick-based simulation loop

All intelligence, behavior, adaptation, and decision-making are fully authored by the player.

The engine is a rule execution machine, not an AI system.

## 1. WORLD SYSTEM (DYNAMIC ENVIRONMENT MODEL)

The world is a continuous evolving simulation space represented as a grid or chunk-based system.

### 1.1 World Structure

```
World
 ├── Regions (grid/chunks)
 │    ├── Climate State
 │    ├── Terrain State
 │    ├── Atmosphere State
 │    ├── Resource Pools
 │    ├── Stability Index
 │    └── Local Event State
```

Each region evolves independently but exchanges influence with neighbors via diffusion rules.

### 1.2 WORLD PARAMETERS

🌡 Climate System
- temperature
- humidity
- seasonal cycle phase
- precipitation probability

Effects:
- modifies resource regeneration
- constrains entity survivability (if defined by user rules)
- drives weather events

🏔 Terrain System
- elevation
- soil fertility
- water accessibility
- biome classification (computed, not predefined behavior)

Effects:
- resource distribution
- movement constraints (pathing cost only, not behavior)
- ecological distribution of resources

🌬 Atmosphere System
- oxygen level
- toxicity index
- atmospheric volatility

Effects:
- modifies survival conditions (if referenced in user rules)
- influences storm/drought probability

⛏ Resource System
- food density
- water reserves
- minerals
- regeneration rates

Rules:
- resources are consumed and regenerated per tick
- depletion affects local stability

⚖ Stability System (Global Emergent Metric)

Derived value per region:

```
Stability =
  f(resource_balance,
    population_pressure,
    climate_volatility,
    event_frequency)
```

Effects:
- higher instability → more random events
- lower stability → faster environmental shifts

### 1.3 ENVIRONMENTAL EVENTS SYSTEM

Events are stochastic but rule-driven, influenced by world state.

Event Types:

🌪 Storm Event
Triggered by: high humidity + high volatility
Effects:
- redistribute water
- damage resource pools
- modify terrain (erosion layer)

🌵 Drought Event
Triggered by: high temperature + low humidity
Effects:
- reduce resource regeneration
- increase regional instability

🌾 Resource Surge
Triggered by: high stability cycle alignment
Effects:
- temporary resource abundance spike
- attracts entity movement (if user-defined behavior uses it)

🌱 Ecological Shift
Triggered by: long-term resource imbalance
Effects:
- gradual change in terrain/resource distribution

## 2. USER-DEFINED ENTITY MODEL SYSTEM (CORE DESIGN)

### 2.1 DESIGN RULE

Entities are fully defined by the user using a structured rule schema.

The engine does NOT define:
- humans
- animals
- plants
- behaviors
- instincts

It only executes rules provided.

### 2.2 ENTITY MODEL STRUCTURE (STRICT FORMAT)

Each entity type must be defined as:

```
EntityModel {
  "name": string,

  "attributes": {
    "<attribute_name>": number | string | enum
  },

  "state": {
    "<internal_state_variable>": value
  },

  "perception": {
    "range": number,
    "filters": [conditions]
  },

  "behavior_rules": [
    {
      "condition": expression,
      "actions": [action_block],
      "priority": number
    }
  ],

  "reaction_rules": [
    {
      "trigger": event_type | condition,
      "effects": [state_changes | behavior_modifiers]
    }
  ],

  "actions": {
    "<action_name>": {
      "inputs": [],
      "effects": []
    }
  }
}
```

### 2.3 ATTRIBUTES (USER-DEFINED)

Examples (NOT pre-defined by engine):
- energy
- hunger
- mobility
- reproduction_rate
- resilience
- social_tendency

Rules:
- purely numeric or categorical
- no semantic meaning enforced by engine

### 2.4 BEHAVIOR RULES (USER WRITTEN LOGIC)

Behavior is evaluated each tick via rule matching.

Format: IF condition THEN execute actions WITH priority

Example structure:

```
IF energy < 20 AND food_nearby > 0
THEN move_to(food) AND eat(food)
PRIORITY 10
```

Execution rules:
- all matching rules evaluated each tick
- highest priority wins or weighted resolution applies
- deterministic ordering unless randomness explicitly used

### 2.5 REACTION RULES (EVENT RESPONSE SYSTEM)

Triggered by:
- environmental events
- entity interactions
- internal thresholds

Example:

```
ON event: "storm"
THEN reduce energy by 10
AND increase shelter_seeking = true
```

### 2.6 ACTION EXECUTION SYSTEM

The engine provides a finite action API, not intelligence:

Examples:
- move(target)
- consume(resource)
- reproduce()
- modify_state(key, value)
- interact(entity)

Actions are:
- deterministic
- validated by engine constraints
- resolved in order per tick

## 3. SIMULATION LOOP (TICK SYSTEM)

The entire world runs in discrete ticks:

### 3.1 TICK ORDER (STRICT)

Each tick executes in this order:

STEP 1 — WORLD UPDATE
- climate evolution
- resource regeneration/depletion
- diffusion between regions

STEP 2 — EVENT RESOLUTION
- evaluate event probabilities
- trigger environmental events
- apply region-level effects

STEP 3 — ENTITY SENSING
- each entity gathers local perception data
- filtered by perception rules

STEP 4 — BEHAVIOR EVALUATION
For each entity:
- evaluate all behavior_rules
- resolve priority conflicts
- select action set

STEP 5 — ACTION EXECUTION
- execute all selected actions
- resolve conflicts (movement, resource contention)

STEP 6 — REACTION PROCESSING
- apply reaction_rules
- modify state or behavior dynamically

STEP 7 — CLEANUP + STATE COMMIT
- finalize world state
- resolve delayed effects
- advance tick counter

## 4. ENVIRONMENT ↔ ENTITY INTERACTION MODEL

Interaction is bidirectional but rule-limited:

### 4.1 ENVIRONMENT → ENTITY
- climate modifies conditions
- events trigger reaction rules
- resources constrain actions

### 4.2 ENTITY → ENVIRONMENT

ONLY via explicit actions:
- consuming resources
- modifying terrain (if allowed in rules)
- relocating entities/resources

No hidden influence exists.

## 5. EMERGENT BEHAVIOR MODEL (CORE OUTCOME)

Emergence arises from:

### 5.1 THREE SYSTEM INTERACTIONS

1. Resource Feedback Loops
- consumption reduces availability
- scarcity changes movement patterns
- movement changes local depletion patterns

2. Environmental Pressure Loops
- climate shifts alter survival conditions
- entities must adapt via their own rules
- adaptation reshapes population distribution

3. Multi-Agent Competition
- overlapping behavior rules create conflict
- resource contention causes indirect competition
- survival strategies emerge from rule optimization

### 5.2 IMPORTANT PRINCIPLE

The engine does NOT simulate intelligence — it only executes interacting rule systems.

All "intelligence-like behavior" is:
- user-defined logic
- interacting constraints
- feedback loops

### 5.3 RESULTING EMERGENT PATTERNS

If rules are well-designed, you can observe:
- migration-like flows
- predator-prey oscillations (if defined)
- civilization clustering (if user-defined)
- ecosystem collapse and recovery cycles
- stable or chaotic equilibria

## 6. MINIMAL VALID ENTITY EXAMPLE

Below is a fully valid user-defined entity model (engine-agnostic):

```json
{
  "name": "SimpleForager",

  "attributes": {
    "energy": 100,
    "mobility": 5,
    "reproduction_rate": 0.1
  },

  "state": {
    "target_food": null
  },

  "perception": {
    "range": 10
  },

  "actions": {
    "move": {},
    "eat": {},
    "reproduce": {}
  },

  "behavior_rules": [
    {
      "condition": "energy < 30 AND food_in_range > 0",
      "actions": ["move_to_nearest_food", "eat"],
      "priority": 10
    },
    {
      "condition": "energy >= 30",
      "actions": ["wander_randomly"],
      "priority": 1
    },
    {
      "condition": "energy > 80",
      "actions": ["reproduce"],
      "priority": 5
    }
  ],

  "reaction_rules": [
    {
      "trigger": "storm",
      "effects": [
        "energy -= 10"
      ]
    }
  ]
}
```

## 7. IMPLEMENTATION READINESS SUMMARY

This system is directly implementable as:

Core Engine Modules
- WorldGridSystem
- ClimateSimulator
- ResourceManager
- EventEngine
- EntityRuntime
- RuleEvaluator
- ActionResolver
- TickScheduler

## 8. FINAL DESIGN INSIGHT

The entire system is:

A deterministic rule-execution engine where complexity emerges from user-authored behavioral systems interacting with a dynamic environment.

- No hidden AI.
- No predefined species logic.
- No scripted outcomes.

Only:
- rules
- constraints
- interaction
- time

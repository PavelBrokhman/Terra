# Phase 1 — Status & Delta from Plan

Состояние на момент завершения MVP этапа 1. Этот документ — дельта между изначальным планом и тем, что реально построено.

## Что построено ✅

### Архитектура
- **Чистое разделение логики и презентации** через `IEventBus` (принцип из главного roadmap соблюдён)
- `Terra.Engine` не зависит ни от чего (кроме BCL)
- `Terra.Behaviors.Default` и `Terra.Presentation.Text` зависят **только** от `Terra.Engine`
- `Terra.Console` — тонкий entry point, wiring-слой

### `IOrganismBehavior` как единственный контракт ✅
- Встроенные Plant/Herbivore/Carnivore реализуют тот же интерфейс, что будут использовать user-creatures в Phase 2+
- `Engine` не знает ни одной конкретной реализации поведения
- Dog-fooding интерфейса с 1-го дня

### Детерминированная симуляция ✅
- Tick-based таймстемпы (никогда wall-clock)
- Seed → идентичный event stream (проверено тестом)
- Сортировка по OrganismId для порядка обработки

### Events как публичная схема ✅
- `[JsonPolymorphic]` discriminator на `SimulationEvent`
- Polymorphic roundtrip через базовый тип (проверено тестом)
- JSON-Lines + human-readable форматтеры используют **один event stream**
- `IEventFormatter.Format()` возвращает `string?` (null = filter skip)

### Game rules — порт из оригинала ✅
Реализованы формулы из `Plans/Phase1_GameRules.md`:
- `MaxEnergy` (base + linear interpolation)
- `EnergyBucket` / `ClassifyEnergyState` (5 баккетов)
- `LifeSpan` (plant/herbivore/carnivore с ×2 мультипликатором хищников)
- `MaxSpeed` / `EyesightRadiusPixels` (линейная интерполяция)
- `MetabolismCost` / `PhotosynthesisGain` / `MovementEnergyCost`

### Consoled CLI ✅
- `--ticks --seed --width --height --plants --herbivores --carnivores --json-file --quiet --help`
- Graceful Ctrl-C через `CancellationToken`
- Dual output: stdout (human) + опциональный JSON-Lines файл
- `QuietHumanFormatter` — пример filter-chain декоратора

### Testing
- **98 unit-тестов** (все green, 0 warnings)
- Покрытие: domain types, GameRules, SpatialGrid, World, EventBus, Simulation (включая детерминизм), default behaviors, all three presenters

## Что отложено (НЕ в Phase 1) ⏸️

### Actions — реализованы частично
| Action | Статус |
|---|---|
| `IdleAction` | ✅ реализован |
| `MoveAction` | ✅ реализован (clamp скорости, clamp границ, energy cost) |
| `EatAction` | ⏸️ silently no-op (логика отложена) |
| `AttackAction` | ⏸️ silently no-op |
| `DefendAction` | ⏸️ silently no-op |
| `ReproduceAction` | ⏸️ silently no-op |

Следствие: хищники и травоядные достигают цели и останавливаются в 8 пикселях от неё (никто никого не ест). Экосистема **не схлопывается**, но и не эволюционирует.

### Mechanics — не реализованы
- **Growth** (изменение радиуса со временем) — организмы стартуют сразу с `MatureRadius`
- **Collision detection** — overlap между организмами разрешён
- **Camouflage** в визибилити — вероятностная проверка не применяется (все в eyesight radius видимы)
- **Plant seed spreading** — растения не распространяются
- **Carcass rot** — мёртвые удаляются сразу (нет `TimeToRot` фазы)

### Events — базовый набор
Реализованы: `SimulationStarted/Ended`, `TickCompleted`, `OrganismBorn/Moved/Died`.

Отложены: `OrganismGrown`, `OrganismAte`, `OrganismAttacked` (с dmg), `OrganismDefended`, `ReproductionStarted/Completed`, `OrganismActionRejected`.

## Известные quirks

1. **Порядок вывода при старте**: `OrganismBorn` события идут **до** `SimulationStarted`, потому что initial spawning происходит до вызова `Run()`. Семантически корректно (setup phase → tick loop), но может быть неожиданно. Исправим когда будет Phase 2 refactor.

2. **Plants capped quickly**: при photosynthesis=550/tick растения быстро достигают `MaxEnergy` и идлят. Без роста/репродукции они просто стоят. Это baseline для следующих шагов.

3. **Herbivores без жертв = wander**: без `EatAction` травоядные находят растение, подходят, останавливаются. Энергия расходуется только на движение и метаболизм — они умирают от old-age или starvation в зависимости от MaxSpeed/расстояния.

## Ссылки

- Детали правил: [Phase1_GameRules.md](Phase1_GameRules.md)
- Главный roadmap: [Terrarium2.0_Modernization.md](Terrarium2.0_Modernization.md)
- Ветка разработки: `DEV-Console`
- Последний коммит Phase 1: `6768ec0`

## Следующие шаги (Phase 1.1 или отдельная подветка)

Приоритет для завершения «живой» экосистемы:

1. **EatAction** (plant→herbivore, animal corpse→carnivore): позволит ресайкл энергии
2. **OrganismAte event** + human/json формат
3. **Growth** до `MatureRadius` (сейчас организмы рождаются взрослыми)
4. **AttackAction + damage/defend formulas** (включает `DamageToKill = 190 × radius`)
5. **ReproduceAction** + 10-tick incubation + `OrganismBorn` с `Generation` > 0
6. **Carcass rot** + `TimeToRot = 60 ticks` + кормление хищников

После этих 6 пунктов симуляция станет самоподдерживающейся экосистемой.

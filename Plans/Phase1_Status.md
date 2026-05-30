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

## Phase 2 — реализовано ✅ (ветка `DEV-Actions`, влита в `DEV`)

Всё, что было отложено из Phase 1, реализовано. Экосистема теперь
**самоподдерживающаяся**: энергия рециклится через еду и туши.

### Actions
| Action | Статус |
|---|---|
| `IdleAction` | ✅ |
| `MoveAction` | ✅ clamp скорости/границ, energy cost, collision-clip |
| `EatAction` | ✅ травоядные едят растения; хищники — туши животных |
| `AttackAction` | ✅ броски атаки/защиты, урон, смерть по `DamageToKill` |
| `DefendAction` | ✅ ×2 защита на тик |
| `ReproduceAction` | ✅ 10-тиковая инкубация → потомок (`Generation+1`) |

### Mechanics
- **Growth** ✅ рост до `MatureRadius` (+ `OrganismGrown`)
- **Collision detection** ✅ `World.IsSpaceFree`; move клипается к свободной точке, рост/спавн избегают overlap
- **Camouflage** ✅ `InvisibleOdds` = camo/100×90% (отдельный детерминированный vision-RNG)
- **Plant seed spreading** ✅ потомство спавнится рядом с родителем (spread-радиус)
- **Carcass rot** ✅ туша живёт `TimeToRot=60` тиков; хищники её доедают, иначе она истлевает

### Events
Реализованы: `SimulationStarted/Ended`, `TickCompleted`, `OrganismBorn/Moved/Died`,
`OrganismGrown`, `OrganismAte`, `OrganismAttacked`, `OrganismDefended`,
`ReproductionStarted/Completed`.

`OrganismActionRejected` — **намеренно опущено** (broadcast-шум). Вместо него —
приватный per-organism feedback через `IWorldView.LastAction`
(Ok / OutOfRange / Unaffordable / Blocked / InvalidTarget / NotReady / Full),
в духе legacy ActionResponse.

## Известные quirks

1. **Порядок вывода при старте**: `OrganismBorn` события идут **до** `SimulationStarted`, потому что initial spawning происходит до вызова `Run()`. Семантически корректно (setup phase → tick loop), но может быть неожиданно. Исправим когда будет Phase 2 refactor.

2. **Туши занимают место**: мёртвые тела участвуют в collision до истлевания — намеренно (хищник подходит к туше на eat-дистанцию, не накладываясь поверх).

3. **Vision-RNG отдельный**: camouflage-броски используют независимый от боевого/спавнового поток — детерминизм сохранён, и изменение сцены не сдвигает боевые броски.

## Ссылки

- Детали правил: [Phase1_GameRules.md](Phase1_GameRules.md)
- Главный roadmap: [Terrarium2.0_Modernization.md](Terrarium2.0_Modernization.md)
- God-sim модель правил: [03_Action_Energy_Model.md](03_Action_Energy_Model.md)
- Ветки: Phase 1 — `DEV-Console`; Phase 2 (механики) — `DEV-Actions`, влита в `DEV`

## Следующие шаги

Все 6 приоритетов «живой экосистемы» закрыты (eat, growth, attack/defend,
reproduce, carcass rot, + camouflage / seed spreading / collision / события /
action-feedback). Экосистема самоподдерживающаяся.

Остаётся **не-механическая** часть Phase 2 — **загрузка пользовательских
организмов**. По god-sim решениям (`02`/`03` доки) это DSL-авторинг
(структурированные правила), а не компилируемый код. Отдельный будущий этап.

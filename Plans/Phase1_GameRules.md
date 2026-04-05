# Phase 1 — Game Rules (Faithful Port from Terrarium 2.0)

Извлечено из оригинального кода `legacy/Terrarium2.0/Client/OrganismBase/` и `Client/Game/`.
Все файловые ссылки — относительно `legacy/Terrarium2.0/Client/`.

---

## 1. Энергетическая модель

### Максимальная энергия
**Файл:** `OrganismBase/Classes/Creature/Attributes/MaximumEnergyPointsAttribute.cs:28-35`

```
MaximumEnergyPerUnitRadius = MaxEnergyBasePerUnitRadius + (points/100) * MaxEnergyMaximumPerUnitRadius
MaxEnergy = MaximumEnergyPerUnitRadius × Radius
```

- `MaxEnergyBasePerUnitRadius` = **19,040** (EngineSettings:99)
- `MaxEnergyMaximumPerUnitRadius` = **380,800** (EngineSettings:85)

### Расход энергии за тик (метаболизм)
**Файл:** `EngineSettings.cs:302, 383`

- **Животные:** `0.001 × Radius` за тик
- **Растения:** `1 × Radius` за тик

### Стоимость движения
**Файл:** `OrganismBase/Classes/State/AnimalState.cs:341-344`

```
EnergyRequired = distance × Radius × speed × 0.005
```

### Питание
**Файл:** `EngineSettings.cs:353, 418, 345, 392`

- Энергия за chunk: **1** (и для мяса, и для растений)
- Chunks в трупе животного: `25 × Radius`
- Chunks в растении: `50 × Radius`

### 5 состояний энергии
**Файл:** `OrganismBase/Enumerations/EnergyState.cs`, `OrganismState.cs:764-822`

`Bucket = MaxEnergy / 5`

| Состояние | Диапазон | Ограничения |
|---|---|---|
| Dead | 0 | — |
| Deterioration | (0, 1×bucket] | — |
| Hungry | (1×bucket, 2×bucket] | — |
| Normal | (2×bucket, 4×bucket] | может есть и размножаться |
| Full | (4×bucket, max] | **не может есть**, может размножаться |

### Энергия роста
**Файл:** `EngineSettings.cs:312, 410`

```
EnergyForGrowth = MaxEnergyBasePerUnitRadius × (1/5) ≈ 3,808 за единицу радиуса
```

### Энергия инкубации (за тик)
**Файл:** `EngineSettings.cs:498-510`

- Животные: `Radius × 93.75` за тик
- Растения: `Radius × 187.5` за тик
- Длительность инкубации: **10 тиков** (EngineSettings:488)

---

## 2. Боевая механика

### Урон атаки
**Файл:** `Game/Classes/Engine/GameEngine.cs:1767-1777`

```
MaxAttackPerRadius = 50 + (points/100) × 25        // → 50-75
AttackDamage = random(0, MaxAttackPerRadius × AttackerRadius)
```

- Базовый: **50** (`BaseInflictedDamagePerUnitOfRadius`, EngineSettings:160)
- Максимум: **+25** (`MaximumInflictedDamagePerUnitOfRadius`, EngineSettings:149)

### Защита (снижение урона)
**Файл:** `GameEngine.cs:1775-1787`

```
MaxDefensePerRadius = 50 + (points/100) × 25       // → 50-75
DefenseDamage = random(0, MaxDefensePerRadius × DefenderRadius)
FinalDamage = max(0, AttackDamage - DefenseDamage)
```

### Хищники — мультипликатор ×2
**Файл:** `EngineSettings.cs:479`

Плотоядные получают **×2** на: attack damage, defense damage, lifespan.

### Дистанция атаки
**Файл:** `GameEngine.cs:1761`

Атакующий должен быть в пределах 1 grid cell (~16 пикселей + размеры существ).

### Порог смерти
**Файл:** `EngineSettings.cs:363`

```
DamageToKill = 190 × Radius
```

---

## 3. Размножение

### Требования
**Файл:** `OrganismBase/Classes/Creature/Organism.cs:258-265`

1. `IsMature == true` (достиг `MatureRadius`)
2. `EnergyState >= Normal`
3. Кулдаун прошёл (`ReadyToReproduce == true`)
4. Не размножается уже

### Кулдаун между размножениями
**Файл:** `EngineSettings.cs:245, 255`

- Животные: `8 × Radius` тиков
- Растения: `25 × Radius` тиков

### Инкубация
**Файл:** `EngineSettings.cs:488`, `GameEngine.cs:2241-2263`

- Длительность: **10 тиков** ровно
- Тратит энергию инкубации каждый тик (см. выше)
- Если энергия падает ниже Normal — инкубация приостанавливается
- Позиция рождения: случайная пустая клетка (`FindEmptyPosition()`)
- Потомок: минимальный радиус, generation = parent + 1, DNA копируется (до 8000 байт)

---

## 4. Характеристики вида (point allocation)

### Бюджет очков
**Файл:** `EngineSettings.cs:29`

**100 очков всего** на вид (`MaxAvailableCharacteristicPoints`).

### Атрибуты (0–100 очков каждый)

| Атрибут | Base | Max | Формула |
|---|---|---|---|
| MaximumEnergyPoints | 19,040/radius | 380,800/radius | Linear |
| MaximumSpeedPoints | 5 | 100 | Linear |
| EatingSpeedPoints | 1/radius | 100/radius | Linear |
| AttackDamagePoints | 50/radius | 75/radius | Linear |
| DefendDamagePoints | 50/radius | 75/radius | Linear |
| EyesightPoints | 5 cells | 15 cells | Linear |
| CamouflagePoints | 0% | 90% invisibility | Linear |

**Общая формула:** `ActualValue = Base + (points/100) × (Max - Base)`

### MatureSize (не part of point budget)
**Файл:** `MatureSizeAttribute.cs`, `EngineSettings.cs:216, 225`

- Диапазон: **25–48 пикселей** в диаметре
- `MatureRadius = MatureSize / 2` (12.5–24 пикселя)

---

## 5. Мир и тики

### Тик
Нет жёсткого рейта в константах. UI оригинала ~30 тиков/сек. Движок просто обрабатывает один turn за итерацию game loop.

### Сетка
**Файл:** `EngineSettings.cs:448-454`

- GridCellWidth = **8 пикселей**
- GridCellHeight = **8 пикселей**
- Размеры мира задаются в runtime

### Движение
**Файл:** `GameEngine.cs:1821-1916`

- Координаты: 2D декартовы
- Направления: любые (8 + диагональ)
- Расстояние: Евклидово
- Скорость ограничена атрибутом Speed
- Нельзя пересекаться с другими существами (кроме самих себя при росте)

### Восприятие
**Файл:** `Animal.cs:166-169`

```
EyesightRadius = 5 + (points/100) × 10 grid cells     // 5-15 cells (40-120 pixels)
InvisibilityOdds = (points/100) × 90%                 // применяется при каждом Scan()
```

Камуфляж проверяется при каждом сканировании — результаты могут отличаться.

### Действия (одно за тик)
**Файл:** `OrganismBase/Classes/Actions/*.cs`

1. **Move** → MoveCompleted
2. **Attack** → AttackCompleted
3. **Defend** → DefendCompleted
4. **Eat** → EatCompleted
5. **Reproduce** → ReproduceCompleted (через 10 тиков)

Одно действие на существо за тик. Действия асинхронные (queue). Событие `Idle` срабатывает каждый тик.

---

## 6. Рост и жизненный цикл

### Рост
**Файл:** `AnimalState.cs:296-321`, `GameEngine.cs:2212-2231`

Автоматический каждый тик если:
- `GrowthWait == 0`
- Достаточно энергии
- Есть место (нет пересечений)

Существо достигает `MatureRadius` за `LifeSpan / 2` тиков.

### Продолжительность жизни
**Файл:** `OrganismState.cs:494-500`, `EngineSettings.cs:264, 273, 282`

```
LifeSpan_herbivore = 50 × MatureRadius ticks
LifeSpan_carnivore = 100 × MatureRadius ticks    (multiplier × 2)
LifeSpan_plant     = 150 × MatureRadius ticks
```

Пример: MatureSize=40 (radius=20):
- Травоядное: 1,000 тиков
- Хищник: 2,000 тиков
- Растение: 3,000 тиков

При `TickAge > LifeSpan` → смерть по `OldAge`.

### Гниение трупа
**Файл:** `EngineSettings.cs:292`

`TimeToRot` = **60 тиков**

---

## 7. Сводная таблица констант

**Файл:** `OrganismBase/Classes/Engine/EngineSettings.cs`

| Константа | Значение | Строка |
|---|---|---|
| MaxAvailableCharacteristicPoints | 100 | 29 |
| InvisibleOddsMaximum | 90 | 39 |
| MaxMatureSize | 48 | 216 |
| MinMatureSize | 25 | 225 |
| MaxSeedSpreadDistance | 1000 | 235 |
| AnimalReproductionWaitPerUnitRadius | 8 | 245 |
| PlantReproductionWaitPerUnitRadius | 25 | 255 |
| AnimalLifeSpanPerUnitMaximumRadius | 50 | 264 |
| CarnivoreLifeSpanMultiplier | 2 | 273 |
| PlantLifeSpanPerUnitMaximumRadius | 150 | 282 |
| TimeToRot | 60 | 292 |
| BaseAnimalEnergyPerUnitOfRadius | 0.001 | 302 |
| BasePlantEnergyPerUnitOfRadius | 1 | 383 |
| MaxEnergyFromLightPerTick | 550 | 400 |
| FoodChunksPerUnitOfRadius | 25 | 345 |
| PlantFoodChunksPerUnitOfRadius | 50 | 392 |
| DamageToKillPerUnitOfRadius | 190 | 363 |
| SpeedBase / SpeedMaximum | 5 / 100 | 115 / 107 |
| BaseEyesightRadius / MaximumEyesightRadius | 5 / 10 | 207 / 195 |
| CarnivoreAttackDefendMultiplier | 2 | 479 |
| TicksToIncubate | 10 | 488 |
| GridCellWidth / GridCellHeight | 8 / 8 | 452 / 454 |

---

## 8. Критичные моменты для порта

1. **5-бакетная энергетическая модель** — центральная механика, влияет на eat/reproduce
2. **Линейная интерполяция атрибутов** из base → max по 100 очкам
3. **×2 мультипликатор хищников** на attack/defense/lifespan — точно ×2
4. **Инкубация ровно 10 тиков** с per-tick расходом энергии
5. **Камуфляж — вероятностный** (проверка при каждом сканировании)
6. **Один action на существо за тик** (но `Idle` всегда срабатывает)
7. **Движение по Евклидову расстоянию**, не по grid

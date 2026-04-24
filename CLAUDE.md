# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Dungeon Core simulator. The player manages two unit types: **방랑자(Wanderer)** with direct RimWorld-style control, and **하수인(Minion)** auto-workers summoned by the Dungeon Core.

## Development Environment

- Unity 2022+ (open project in Unity Editor to build/run)
- New Input System (`UnityEngine.InputSystem`)
- TextMeshPro

There are no CLI build commands; all building and testing is done through the Unity Editor. Play Mode is the primary way to test gameplay.

## Architecture

### Map System (`Assets/1 Script/Map/`)

The world is a flat tile grid (default 30×30). `MapManager` (singleton) owns a `TileData[,]` array and coordinates everything.

- `TileData` — struct with `TileType` (Passable / Impassable) and `MovePenalty`
- `MapManager.OnTileChanged` — static event fired on `SetTile`; listeners should refresh visuals
- `GridRenderer` — listens to `OnTileChanged`; spawns a coloured Plane overlay for each Impassable tile
- `ResourceNode` — marks its tile as Impassable on `Awake`, restores to Passable on `OnDestroy`; on yield adds items to own `Inventory` and registers `HaulJob`

Coordinate helpers: `MapManager.TileToWorld(Vector2Int)` and `WorldToTile(Vector3)`.

### Unit Types (`UnitBrain.cs`, `UnitData.cs`, `MinionData.cs`)

`UnitBrain.BuildTree()` branches on `unitData` type:

| Type | Data | BT Tree | Control |
|---|---|---|---|
| **방랑자** | `UnitData` | Draft → Fight → Work → Idle | 직접 명령 가능 |
| **하수인** | `MinionData` | Detect+Fight → Work → Idle | 자동 (명령 불가) |
| **Enemy** | `UnitData` | Detect → Fight → Idle | AI 자동 |

`MinionData : UnitData` adds:
- `SummonCostType / SummonCostAmount` — summoning resource cost
- `AllowedJobs: List<JobCategory>` — which job categories this minion can perform

`MoveController` — only sends commands to Wanderers (`!(brain.Data is MinionData)`). Minions are selectable (info-only).

### Unit AI — Behavior Tree (`Assets/1 Script/unit/`)

`UnitBrain` runs a BT at a fixed 0.1 s tick interval. BT is paused while `IsKnockedOut` or `IsStaggered`.

`UnitBlackboard` — runtime state: `Target`, `CurrentJob`, `AssignedNode`, `IsDrafted`, `IsUnarmed`, `IsKnockedOut`, `IsStaggered`, `StaggerTimer`, `AttackDamage`, `AttackRange`. HP removed — use `BodyPartSystem.Blood`.

`UnitStats` — all skills are 0–100 floats, grow via `+= growthRate` during play.

`UnitData` (ScriptableObject) — read-only template; initialises `UnitStats` via random ranges in `UnitBrain.Awake`.

### Stat System (`UnitStats.cs`, `UnitData.cs`)

All stats are 0–100. `UnitData` holds a `Vector2` range for each stat used to randomise initial values.

| Category | Fields |
|---|---|
| **속성** | Strength, Toughness, Dexterity, Cognition |
| **전투 기술** | MeleeAttack, MeleeDefense, PrecisionShooting |
| **근접 무기** | OneHandSword, OneHandAxe, OneHandMace, TwoHandSword, TwoHandAxe, TwoHandMace, Polearm |
| **원거리 무기** | Bow, Crossbow, Javelin |
| **마법** | ElementalMagic, DarkMagic, HolyMagic |
| **신체 기술** | MartialArts, Evasion, Athletics, Swimming |
| **노동** | Labor, Mining, Growing, Cutting, Taming, Cooking, Medicine, Research, WeaponCrafting, ArmorCrafting, Alchemy |

Key helpers on `UnitStats`:
- `GetSkillForWeapon(WeaponType)` — returns the matching skill float
- `GrowSkillForWeapon(WeaponType, amount)` — grows the matching skill (capped at 100)
- `GetAttackCombatSkill(bool unarmed)` — MartialArts if unarmed, else MeleeAttack
- `GetDefenseCombatSkill(bool unarmed)` — MartialArts if unarmed, else MeleeDefense

### Combat System (`CombatHandler.cs`)

`Equipment.MainHand` drives weapon type. `CombatHandler.Update()` branches on `weapon.IsRanged` to call `TryFireRanged` vs `TryAttack`.

**Melee attack flow:**
1. Block chance = `defenderMeleeDefense / (attackerMeleeAttack + defenderMeleeDefense)` (unarmed: MartialArts)
2. Dodge chance = `defenderEvasion / (attackerMeleeAttack + defenderEvasion)`
3. On hit → `BodyPartSystem.TakeDamage` or `UnitBrain.TakeDamage`

**Damage formula:**
```
damage = AttackDamage
       × (1 + weaponSkill / 100)
       × [1 + Dexterity/100 × 0.5]   // IsBladed weapons only
       × [1 + Strength/100 × 0.5]    // Unarmed only
       × statusCorrection
       × weightCorrection
```

**Attack interval formula:**
```
interval = weapon.BaseAttackInterval
         / (1 + Dexterity/100)
         / [1 + Strength/100 × 0.5]  // IsLargeWeapon only
         / statusCorrection
         / weightCorrection
```

**Weight correction:** `maxCarryWeight = Strength × 5`. If overloaded: `weightCorrection = maxWeight / currentWeight`.

**Status correction** (limb disability):
- RightArm × 0.5 + LeftArm × 0.3 + RightLeg × 0.1 + LeftLeg × 0.1
- Both arms OR both legs disabled → 0

**Ranged / magic flow:**
1. `TryFireRanged` — Cognition × (1 − distancePenalty) accuracy roll; fail = miss
2. `FireRanged` — PrecisionShooting split: 60–100% hit target, 0–40% friendly fire

### Body-Part System (`BodyPartSystem.cs`)

Seven parts: Head, Chest, Abdomen, LeftArm, RightArm, LeftLeg, RightLeg.

| State | Threshold | Head/Chest/Abdomen | Arm | Leg |
|---|---|---|---|---|
| Disabled | ≤ 0 | TryKnockOut | AttackAbility update | SetLimping/Crawling |
| Destroyed | ≤ −100 | Die() | TryKnockOut | TryKnockOut |

- `TryKnockOut`: `resistChance = Toughness / 100`. Fail → `IsKnockedOut = true`
- `TryStagger`: triggered when Blood < 30%. Duration = `base × (1 − Toughness/100)`, min 0.2s
- `actualDamage = damage / (1 + Toughness / 100)`

### Equipment System (`Equipment.cs`, `WeaponData.cs`, `WeaponType.cs`)

`Equipment` auto-added by `UnitBrain.Awake`. Manages `MainHand` slot.

- `Equip(WeaponData)` — consumes from `Inventory`; syncs `Blackboard.IsUnarmed`, `AttackDamage`, `AttackRange`
- `Unequip()` — returns to `Inventory`
- `OffHandLocked` — true when `IsLargeWeapon`

`WeaponData : ItemData` fields: `WeaponType`, `BaseDamage`, `BaseAttackInterval`, `AttackRange`, `IsLargeWeapon`, `IsBladed`, `IsRanged`.

### Movement (`PawnMover.cs`)

- `SetStats(UnitStats, Inventory)` — enables Athletics speed and weight correction
- Effective speed: `baseMoveSpeed × (1 + Athletics/100) × weightCorrection`
- `Athletics` grows per second while moving

### Job System (`Assets/1 Script/Job/`)

`JobManager` (singleton). Job selection score = `globalPriority × 100 + job.Priority`.

- `JobBase.Category` — virtual `JobCategory` property
- `RequestJob(unit)` — no filter (Wanderers)
- `RequestJobFiltered(unit, allowedCategories)` — Minion job filter
- `MineJob` — Category: Mining. Calls `ResourceNode.Work` each tick
- `HaulJob` — Category: Hauling. ResourceNode → StorageBuilding (2-phase)
- `GroundHaulJob` — Category: Hauling. DroppedItem → StorageBuilding (2-phase)

**HaulJob phases:**
1. GoToSource: move to ResourceNode, consume items into unit inventory
2. GoToStorage: move to StorageBuilding, deposit via `TryAdd()`

**GroundHaulJob phases:**
1. GoToItem: move to DroppedItem, call `PickUp()`
2. GoToStorage: move to StorageBuilding, deposit

### Priority System (`PriorityManager.cs`, `PriorityPanel.cs`)

`PriorityManager` (singleton) — stores `int` priority 0–5 per `JobCategory`. 0 = disabled.

- `Get(category)` / `Set(category, value)` / `Increase` / `Decrease`
- `IsEnabled(category)` — returns false if 0

`PriorityPanel` — code-generated UI on Canvas. P key to toggle. `-/+` buttons per category.

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard.

### Resource & Storage System

**ResourceNode** — `DropYield()` adds items to own `Inventory`, then registers `HaulJob` to nearest compatible `StorageBuilding`.

**StorageBuilding** — per-building `Inventory` with item type filter.
- `allowedTypes: List<ResourceType>` — empty = accept all
- `TryAdd(item, amount)` — respects filter
- `static GetTotal(ResourceType)` — sum across all buildings
- `static Consume(ResourceType, amount)` — consume from all buildings
- `static FindNearest(position, item)` — nearest that accepts the item

`ResourceStorage` — **removed**. Use `StorageBuilding.GetTotal` / `StorageBuilding.Consume` instead.

### Item System (`Assets/1 Script/Item/`)

- `ItemData` — `ItemName`, `Description`, `ResourceType`, `Icon`, `MaxStack`, `Weight`
- `WeaponData : ItemData` — combat fields
- `MinionData : UnitData` — summon cost, AllowedJobs
- `Inventory` — `Add / Consume / GetQuantity / GetTotalWeight / GetTotalByType / ConsumeByType`
- `DroppedItem` — spawned via `DroppedItem.Spawn(item, amount, position)`; auto-registers `GroundHaulJob`
- `InventorySlotUI` — drag-and-drop slot; dragging outside UI calls `DroppedItem.Spawn()` at cursor world position

### Dungeon Core (`Assets/1 Script/core/`)

`DungeonCore` — singleton. Marks tile Impassable; left-click to move.

`DungeonCore.Summon(MinionData, position)` — checks `StorageBuilding.Consume(costType, costAmount)`, instantiates prefab from `minionEntries` list (inspector: MinionData + Prefab pairs).

### Input & Camera

| Script | Trigger | Action |
|---|---|---|
| `SelectionManager` | Left click (pawn/minion) / drag | Select Player-faction units |
| `MoveController` | Right click (ground) | Move **Wanderers** only |
| `MoveController` | Right click (enemy) | Set Target on **Wanderers** only |
| `PawnInventoryPanel` | I key | Open inventory of selected unit |
| `InventorySlotUI` | Drag outside UI | Drop item at cursor world position |
| `PriorityPanel` | P key | Toggle priority panel |
| `CameraController` | WASD / middle-drag / scroll | Pan / rotate / zoom |

### Pathfinding

`Pathfinder` — **Theta*** (8-directional, Euclidean heuristic). Line-of-sight uses Bresenham supercover; corner-cutting blocked.

## ScriptableObject Assets (`Assets/5 Data/`)

| Type | Menu path | Key fields |
|---|---|---|
| `UnitData` | `DungeonColony/UnitData` | RaceName, MaxHP, MoveSpeed, AttackDamage, AttackRange, DetectRange + stat ranges |
| `MinionData` | `DungeonColony/MinionData` | (UnitData fields) + SummonCostType, SummonCostAmount, AllowedJobs |
| `WeaponData` | `DungeonColony/WeaponData` | WeaponType, BaseDamage, BaseAttackInterval, AttackRange, IsLargeWeapon, IsBladed, IsRanged |
| `WallData` | `DungeonColony/WallData` | Hardness, DropAmount |
| `DungeonCoreData` | `DungeonColony/DungeonCoreData` | CoreType, BaseRadius, MaxGrade, per-frame modifiers |
| `ResourceNodeData` | `DungeonColony/ResourceNodeData` | ResourceType, GaugeRequired, DropMin, DropMax, DropItem |
| `ItemData` | `DungeonColony/ItemData` | ItemName, Description, ResourceType, Icon, MaxStack, Weight |

## TODOs in Code

- `CombatHandler` — Cognition moving-target penalty not implemented (distance only)
- `CombatHandler` — Dexterity reload/fire-rate for ranged not implemented
- `UnitInfoUI` — shows basic stats only; weapon skill details not shown

## Upcoming Systems

- **DungeonCore area effects** — unit buffs/debuffs based on proximity and core environment type
- **Enemy spawn / event system**
- **Crafting / processing system**
- **Enemy loot drops** — `DroppedItem.Spawn()` on enemy death

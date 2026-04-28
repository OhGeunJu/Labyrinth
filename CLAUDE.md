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
| **전투 기술** | MeleeAttack, MeleeDefense, RangedDefense, PrecisionShooting |
| **근접 무기** | OneHandSword, OneHandAxe, OneHandMace, TwoHandSword, TwoHandAxe, TwoHandMace, Polearm |
| **원거리 무기** | Bow, Crossbow, Javelin |
| **마법 (카테고리)** | ElementalMagic, DarkMagic, HolyMagic |
| **마법 원소** | ElementalFire/Ice/Lightning/Poison, DarkFire/Ice/Lightning/Poison, HolyFire/Ice/Lightning/Poison |
| **신체 기술** | MartialArts, Evasion, Athletics, Swimming |
| **노동** | Labor, Mining, Growing, Cutting, Taming, Cooking, Medicine, Research, WeaponCrafting, ArmorCrafting, Alchemy |

Key helpers on `UnitStats`:
- `GetSkillForWeapon(WeaponType)` — 원소별 무기 숙련도 반환
- `GrowSkillForWeapon(WeaponType, amount)` — grows the matching skill (capped at 100)
- `GetMagicCategorySkill(WeaponType)` — 마법 카테고리 스킬 반환 (정확도/빈도용, MeleeAttack 역할)
- `GetAttackCombatSkill(bool unarmed)` — MartialArts if unarmed, else MeleeAttack
- `GetDefenseCombatSkill(bool unarmed)` — MartialArts if unarmed, else MeleeDefense

### Damage Type System (`DamageInfo.cs`, `DamageCategory.cs`, `PhysicalType.cs`, `DamageElement.cs`)

모든 피해는 `DamageInfo` struct로 전달됨.

```
DamageCategory: Physical, Elemental, Dark, Holy
PhysicalType:   Slash, Pierce, Blunt          (Category == Physical)
DamageElement:  None, Fire, Ice, Lightning, Poison  (Category != Physical)
```

**물리 피해 효과:**
- `Slash` — 추가 출혈 (actualDamage × 0.5 혈액 추가 감소)
- `Pierce` — 갑옷 관통
- `Blunt` — 데미지 × 1.3

**마법 피해 효과 (EffectChance 확률로 발동):**

| Category | Element | 효과 |
|---|---|---|
| Elemental | Fire | Burning DoT (5초, 5 DPS) |
| Elemental | Ice | 갑옷 관통 |
| Elemental | Lightning | 데미지 × 1.2 |
| Elemental | Poison | AoE — TODO |
| Dark | Fire | HealReduction 10초 |
| Dark | Ice | Slowed 8초 (공격속도/이동속도 감소) |
| Dark | Lightning | Weakened 8초 (공격/방어 확률 감소) |
| Dark | Poison | CorruptionGauge +amount×0.3 |

`WeaponData`에 `DamageCategory`, `PhysicalType`, `DamageElement`, `EffectChance` 필드 추가.  
마법 무기(`WeaponType`이 마법 계열)는 `EffectiveDamageCategory/Element`로 자동 추론됨.

### Status Effect System (`StatusEffectSystem.cs`)

`UnitBrain.Awake`에서 자동 추가. 활성 효과 관리 및 보정값 제공.

| 효과 | 영향 |
|---|---|
| `Burning` | `BodyPartSystem.TakeDamage` DoT 매 프레임 |
| `HealReduction` | `HealMult < 1` |
| `Slowed` | `AttackIntervalMult > 1`, `MoveSpeedMult < 1` |
| `Weakened` | `AttackChanceMod < 1`, `DefenseChanceMod < 1` |
| `CorruptionGauge` | 회복 불가 2차 피통 (Dark Poison 누적) |

- `ApplyEffect(type, duration, strength)` — 동일 타입이면 duration/strength 갱신
- `AddCorruption(amount)` — 부패 게이지 누적

### Combat System (`CombatHandler.cs`)

`Equipment.MainHand` drives weapon type. `CombatHandler.Update()` branches on `weapon.IsRanged` to call `TryFireRanged` vs `TryAttack`.

**Melee attack flow:**
1. Weakened 디버프: attackSkill × AttackChanceMod
2. Block chance = `(defenderMeleeDefense + shield.BlockBonus) / (attackerMeleeAttack + effective)` × DefenseChanceMod
3. Dodge chance = `defenderEvasion / (attackerMeleeAttack + evasion)` × DefenseChanceMod
4. On hit → `BodyPartSystem.TakeDamage(DamageInfo)` or `UnitBrain.TakeDamage(DamageInfo)`

**Ranged attack flow:**
1. `TryFireRanged` — Cognition × (1 − distancePenalty) accuracy roll
2. `TryBlockRanged` — `(RangedDefense + shield.RangedBlockBonus) / (attackerSkill + effective)` × DefenseChanceMod
3. `FireRanged` — PrecisionShooting split: 60–100% hit target, 0–40% friendly fire

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
         × AttackIntervalMult        // StatusEffectSystem (Slowed 등)
```

**Weight correction:** `maxCarryWeight = Strength × 5`. If overloaded: `weightCorrection = maxWeight / currentWeight`.

**Status correction** (limb disability):
- RightArm × 0.5 + LeftArm × 0.3 + RightLeg × 0.1 + LeftLeg × 0.1
- Both arms OR both legs disabled → 0

### Body-Part System (`BodyPartSystem.cs`)

Seven parts: Head, Chest, Abdomen, LeftArm, RightArm, LeftLeg, RightLeg.

| State | Threshold | Head/Chest/Abdomen | Arm | Leg |
|---|---|---|---|---|
| Disabled | ≤ 0 | TryKnockOut | AttackAbility update | SetLimping/Crawling |
| Destroyed | ≤ −100 | Die() | TryKnockOut | TryKnockOut |

- `TryKnockOut`: `resistChance = Toughness / 100`. Fail → `IsKnockedOut = true`
- `TryStagger`: triggered when Blood < 30%. Duration = `base × (1 − Toughness/100)`, min 0.2s
- `TakeDamage(DamageInfo)` 처리 순서:
  1. 타입 배율 (Blunt ×1.3, Lightning ×1.2)
  2. 갑옷 감소 (Pierce/Elemental Ice는 관통)
  3. Toughness 저항 `/ (1 + Toughness/100)`
  4. 베기 추가 출혈
  5. 상태이상 발동 (EffectChance 확률)

**부위 → 갑옷 슬롯 매핑:** Head→Head, Chest/Abdomen→Chest, Arms→Hands, Legs→Legs

### Equipment System (`Equipment.cs`, `WeaponData.cs`, `ArmorData.cs`)

`Equipment` auto-added by `UnitBrain.Awake`.

**슬롯:**
- `MainHand` — 무기 (`Equip` / `Unequip`)
- `OffHand` — 방패 또는 보조 무기 (`EquipOffHand` / `UnequipOffHand`); `OffHandLocked` when IsLargeWeapon
- `Head / Chest / Hands / Legs` — 갑옷 (`EquipArmor(ArmorData)` / `UnequipArmor(ArmorSlot)`)

`WeaponData : ItemData` fields: `WeaponType`, `BaseDamage`, `BaseAttackInterval`, `AttackRange`, `IsLargeWeapon`, `IsBladed`, `IsRanged`, `BlockBonus`, `RangedBlockBonus`, `DamageCategory`, `PhysicalType`, `DamageElement`, `EffectChance`.

`ArmorData : ItemData` fields: `ArmorSlot`, `DamageReduction`.

`WeaponType` enum: Unarmed, Shield, OneHand/TwoHand 근접, Bow/Crossbow/Javelin, ElementalFire/Ice/Lightning/Poison, DarkFire/Ice/Lightning/Poison, HolyFire/Ice/Lightning/Poison.

### Movement (`PawnMover.cs`)

- `SetStats(UnitStats, Inventory)` — enables Athletics speed and weight correction
- Effective speed: `baseMoveSpeed × (1 + Athletics/100) × weightCorrection × MoveSpeedMult`
- `MoveSpeedMult` — `StatusEffectSystem`에서 제공 (Slowed 디버프 반영)
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

`PriorityPanel` — Inspector 기반 UI. P key to toggle.

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
- `WeaponData : ItemData` — combat + damage type fields
- `ArmorData : ItemData` — `ArmorSlot`, `DamageReduction`
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
| `PawnInventoryPanel` | I key | Open inventory / equipment of selected unit |
| `InventorySlotUI` | Drag outside UI | Drop item at cursor world position |
| `PriorityPanel` | P key | Toggle priority panel |
| `CameraController` | WASD / middle-drag / scroll | Pan / rotate / zoom |

### UI System (`Assets/1 Script/UI/`)

- `UnitInfoUI` — 선택 유닛 상세 정보 표시 (혈액바, 신체부위, 장착, 스탯, 상태이상, 노동)
- `PawnInventoryPanel` — I키로 인벤토리 열기; 아이템 클릭 시 장착, 장착 슬롯 클릭 시 해제; 시체 루팅 지원
- `EquipmentSlotUI` — 장비 슬롯 표시 컴포넌트 (슬롯명 + 아이콘 + 클릭 해제)
- `DraggablePanel` — 헤더 오브젝트에 붙이고 `Target`에 이동할 패널 RectTransform 연결; 드래그로 패널 이동

### Pathfinding

`Pathfinder` — **NavMesh 기반** (Unity AI Navigation). `NavMesh.SamplePosition` + `NavMesh.CalculatePath`. 이동/네비게이션은 타일 그리드와 무관 — 오픈월드 전체에서 동작. 씬에 `NavMeshSurface` 베이크 필요 (Unity 6의 AI Navigation 패키지).

## ScriptableObject Assets (`Assets/5 Data/`)

| Type | Menu path | Key fields |
|---|---|---|
| `UnitData` | `DungeonColony/UnitData` | RaceName, MaxHP, MoveSpeed, AttackDamage, AttackRange, DetectRange + stat ranges |
| `MinionData` | `DungeonColony/MinionData` | (UnitData fields) + SummonCostType, SummonCostAmount, AllowedJobs |
| `WeaponData` | `DungeonColony/WeaponData` | WeaponType, BaseDamage, BaseAttackInterval, AttackRange, IsLargeWeapon, IsBladed, IsRanged, BlockBonus, RangedBlockBonus, DamageCategory, PhysicalType, DamageElement, EffectChance |
| `ArmorData` | `DungeonColony/ArmorData` | ArmorSlot, DamageReduction |
| `WallData` | `DungeonColony/WallData` | Hardness, DropAmount |
| `DungeonCoreData` | `DungeonColony/DungeonCoreData` | CoreType, BaseRadius, MaxGrade, per-frame modifiers |
| `ResourceNodeData` | `DungeonColony/ResourceNodeData` | ResourceType, GaugeRequired, DropMin, DropMax, DropItem |
| `ItemData` | `DungeonColony/ItemData` | ItemName, Description, ResourceType, Icon, MaxStack, Weight |

## TODOs in Code

- `CombatHandler` — Cognition moving-target penalty not implemented (distance only)
- `CombatHandler` — Dexterity reload/fire-rate for ranged not implemented
- `StatusEffectSystem` — Elemental Poison AoE 미구현
- `StatusEffectSystem` — Holy 계열 힐/버프 미구현 (아군 전용)

## 최근 변경 (2026-04-28)

오픈월드 전환 + 타르코프식 인벤토리 작업.

### 1. 이동 시스템 — NavMesh 전환
- `Pathfinder.cs` 타일 기반 Theta* → **NavMesh 기반**으로 교체. 같은 시그니처(`List<Vector3> FindPath(from, to)`) 유지하여 `PawnMover`는 무수정.
- 오픈월드 어디서든 이동 가능. 타일 그리드는 더 이상 이동에 관여하지 않음.
- Unity 6: `NavMeshSurface` 컴포넌트로 베이크.

### 2. MapManager — 빌드존 한정으로 축소
- 월드 전체 30×30 그리드 → **미궁핵 주변 50×50 빌드존**.
- 1 tile = 1 world unit, 절대 월드 타일 좌표 (`tile.x = floor(world.x)`).
- `RecenterAt(worldCenter)` — 핵 이동 시 빌드존 재정렬, 타일 데이터 초기화, `OnZoneRecentered` 이벤트 발행.
- 빌드존 밖 타일은 항상 Passable 기본값 반환 — Job/BT 시스템이 빌드존 밖에서도 동작.
- `ResourceNode.Awake/OnDestroy`의 `SetTile` 호출 제거. NavMesh가 콜라이더로 자동 처리.
- `DungeonCore.GridPos`/`SnapToGrid` 제거. 핵 위치 = 빌드존 중심.
- `GridRenderer` `OnZoneRecentered` 구독해 오버레이 정리.

### 3. BuildModeManager — 그리드/자유 토글 + 고스트 미리보기
- `Tab` 키로 **그리드 ↔ 자유 모드** 전환 (둘 다 같은 건축물 사용 가능).
- 그리드: 타일 중심 스냅, `Q/E` 90° 회전.
- 자유: 마우스 위치 그대로, `Q/E` 15° 회전, `Physics.OverlapBox`로 `ResourceNode`/`DungeonCore` 충돌 체크.
- **고스트**: 선택된 프리팹을 미리 인스턴스화 (콜라이더/스크립트 비활성)해서 위치/회전 시각 피드백.

### 4. Item 시스템 분리
- `ItemData`에서 `ResourceType`, `MaxStack` 제거. `Width`, `Height` 추가 (그리드 셀 크기).
- `ItemData.MaxStack` 가상 프로퍼티 (기본 1).
- `ResourceItemData : ItemData` 신규 — `ResourceType`, `Stack` 필드, `MaxStack` 오버라이드.
- 무기/방어구는 `ItemData` 직접 상속이라 자동으로 `MaxStack=1` (1칸 1개).
- 자원 에셋(`Stone_item` 등) 재생성 필요 (ResourceItemData로).

### 5. Inventory — 그리드 기반 (타르코프식)
- `Dictionary<ItemData, int>` → **W×H 셀 배열 + ItemInstance**.
- 기본 8 wide × 10 tall. 컴포넌트 인스펙터에서 조절.
- 같은 ItemData는 `MaxStack` 한도까지 한 인스턴스에 누적, 넘으면 새 인스턴스.
- 아이템 회전 지원 (1×3 ↔ 3×1).
- API: `TryAdd(data, amount) → int`, `TryAddAt(data, amount, x, y, rotated) → bool`, `MoveItem(inst, x, y, rotated)`, `Remove(data, amount) → int`, `RemoveByType(type, amount)`, `GetTotalQuantity`, `GetTotalByType`, `GetTotalWeight`, `GetAt(x, y)`, `RemoveInstance(inst)`.
- 호출부 마이그레이션: `Add` → `TryAdd`, `Consume` → `Remove`, `GetQuantity` → `GetTotalQuantity`, `ConsumeByType` → `RemoveByType` (`ResourceNode`, `StorageBuilding`, `HaulJob`, `GroundHaulJob`, `DroppedItem`, `Equipment`).
- `ItemStack` 구조체 삭제 (사용처 없음).
- `ItemInstance` (런타임 객체) — `Data`, `Quantity`, `GridX`, `GridY`, `Rotated`, `EffectiveWidth/Height`.

### 6. UI — 그리드 인벤토리 / 노드 정보 패널 재작성
- **`InventoryGridUI`** (신규) — `Inventory` 1개를 그리드 시각화. 셀은 에디터에서 미리 배치, 아이템 아이콘만 동적 생성. 자식 `_Items` 컨테이너에 아이콘이 들어감 (LayoutGroup 영향 안 받게 `LayoutElement.ignoreLayout`). API: `Bind(inv)`, `RefreshItems()`, `ScreenToGrid(screenPos)`, `CellSize`.
- **`PawnInventoryPanel`** 재작성 — 인스펙터에서 패널 GameObject + GridContainer 받음. I 키로 토글, 선택된 폰의 Inventory를 GridUI에 바인딩.
- **`NodeInfoPanel`** 재작성 — 노드는 한 종류 자원만 쌓이므로 단일 아이콘 + 수량 텍스트 표시 (그리드 사용 X). Assign/Transfer 버튼, 배정된 폰 리스트(`PawnEntryUI` 프리팹) 지원.
- **`DraggableItem`** (신규) — 아이콘에 자동 추가. 드래그/드롭/회전 처리. `R` 키로 드래그 중 회전. 클릭 셀 오프셋 보존 (Tarkov식).
- **`InventorySlotUI`** 빈 스텁으로 폐기 (씬 참조 보존용).

### 7. Equipment / Pawn 컴포넌트 안전 패턴
- `ResourceNode`, `UnitBrain`, `StorageBuilding`, `Equipment` Awake에서 `GetComponent<X>() ?? AddComponent<X>()` — 프리팹/씬에 이미 컴포넌트가 있어도 중복 생성 방지.

---

## 알려진 이슈

- **인벤토리 클릭 시 첫 자리에 아이콘 잔재** — 다른 위치로 옮긴 후에도 원래 자리에 아이콘이 남아 보이는 버그. `DraggableItem.OnEndDrag`에서 `MoveItem` 성공 시 새 위치에 새 아이콘이 그려지지만 옛 위치도 같이 보이는 현상. 해결 필요.
- **단계 5 미완**: 다중 컨테이너 드래그 / 월드 드롭 / 색 피드백(초록·빨강) / 스프라이트 진짜 회전 미구현.
- **한글 폰트 경고**: 코드로 `AddComponent<TextMeshProUGUI>` 시 기본 폰트(LiberationSans SDF)가 한글 미지원 → `Project Settings → TextMesh Pro → Default Font Asset`에 한글 폰트 지정 권장.
- **핵 이동 시 빌드존 리셋**: 핵 따라 그리드 데이터 초기화. 미리 지어둔 건물의 시각은 사라짐 (실제 GameObject는 그대로 남음). 의도된 동작이지만 향후 정책 결정 필요.

## Upcoming Systems

- **단계 6 인벤토리** — 다중 컨테이너 드래그(시체↔폰, 폰↔창고), 월드 드롭, 드롭 위치 색 피드백, 진짜 스프라이트 회전.
- **DungeonCore area effects** — unit buffs/debuffs based on proximity and core environment type.
- **Enemy spawn / event system**.
- **Crafting / processing system**.
- **Enemy loot drops** — `DroppedItem.Spawn()` on enemy death.

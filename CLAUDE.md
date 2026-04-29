# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Dungeon Core simulator. The player manages two unit types: **방랑자(Wanderer)** with direct RimWorld-style control, and **하수인(Minion)** auto-workers summoned by the Dungeon Core.

## Development Environment

- Unity 2022+ (open project in Unity Editor to build/run)
- New Input System (`UnityEngine.InputSystem`)
- TextMeshPro

There are no CLI build commands; all building and testing is done through the Unity Editor. Play Mode is the primary way to test gameplay.

## 폴더 구조 (`Assets/1 Script/`)

```
1 Script/
├── BuildModeManager.cs      ← 루트: 빌드 모드 토글
├── Combat/                   ← 데미지 타입 정의 (DamageInfo, DamageCategory, DamageElement, PhysicalType)
├── Input/                    ← 입력/카메라 (CameraController, MoveController, SelectionManager, SelectionBox, SelectionIndicator)
├── Item/                     ← 아이템 데이터 + 인벤토리
├── Job/                      ← 작업 시스템 + 우선순위 + 창고
├── Map/                      ← 맵, 그리드, 패스파인딩, 자원 노드
├── UI/                       ← 모든 UI 패널/컴포넌트
├── core/                     ← 던전 코어
└── unit/                     ← 유닛 시스템 (BT, 전투, 신체, 상태이상, 장비, 이동)
```

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
- `CraftJob` — Category: Crafting. CraftingTable에서 RecipeData 기준 제작

**HaulJob phases:**
1. GoToSource: move to ResourceNode, consume items into unit inventory
2. GoToStorage: move to StorageBuilding, deposit via `TryAdd()`

**GroundHaulJob phases:**
1. GoToItem: move to DroppedItem, call `PickUp()`
2. GoToStorage: move to StorageBuilding, deposit

**CraftJob 흐름:**
1. `OnStart`: 재료를 모든 `StorageBuilding`에서 즉시 소비 (예약 개념)
2. `Execute`: 유닛이 테이블 도착 → `Time.time` 기준 `CraftTime` 카운트다운
3. 완료 시 `DroppedItem.Spawn`으로 테이블 위에 결과물 드롭 → HaulJob이 자동 등록되어 창고로 운반

### Priority System (`PriorityManager.cs`, `PriorityPanel.cs`)

`PriorityManager` (singleton) — stores `int` priority 0–5 per `JobCategory`. 0 = disabled.

- `Get(category)` / `Set(category, value)` / `Increase` / `Decrease`
- `IsEnabled(category)` — returns false if 0

`PriorityPanel` — Inspector 기반 UI. P key to toggle.

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting.

### Resource & Storage System

**ResourceNode** — `DropYield()` adds items to own `Inventory`, then registers `HaulJob` to nearest compatible `StorageBuilding`.

**StorageBuilding** — per-building `Inventory` with item type filter.
- `allowedTypes: List<ResourceType>` — empty = accept all
- `TryAdd(item, amount)` — respects filter
- `static GetTotal(ResourceType)` / `Consume(ResourceType, amount)` — ResourceType 기반
- `static GetTotalByItem(ItemData)` / `ConsumeByItem(ItemData, amount)` — ItemData 기반 (제작 재료용)
- `static FindNearest(position, item)` — nearest that accepts the item

`ResourceStorage` — **removed**. Use `StorageBuilding.GetTotal` / `StorageBuilding.Consume` instead.

### Crafting System (`Job/CraftingTable.cs`, `Job/CraftJob.cs`, `Item/RecipeData.cs`, `UI/CraftingTableUI.cs`, `UI/RecipeEntryUI.cs`)

**RecipeData** (ScriptableObject):
- `RecipeName`, `Ingredients: List<{ItemData, int}>`, `Result: ItemData`, `ResultAmount`, `CraftTime`, `Category`

**CraftingTable** (씬 오브젝트):
- `availableRecipes: List<RecipeData>` — 이 테이블에서 만들 수 있는 것
- 좌클릭 시 `OnSelected` 정적 이벤트 발행 → `CraftingTableUI`가 구독
- `QueueOrder(recipe)` → `JobManager.RegisterJob(new CraftJob(...))`
- `CanCraft(recipe)` — 재료 충분 여부 확인 (`StorageBuilding.GetTotalByItem`)

**CraftingTableUI** — 마스터-디테일 레이아웃:
- 왼쪽: ScrollView에 `RecipeEntryUI` 프리팹 인스턴스 목록
- 오른쪽: 선택된 레시피의 디테일 (아이콘, 이름, 설명, 재료, 시간, 제작 버튼)
- ESC로 닫기

**RecipeEntryUI** — 프리팹. `[RequireComponent(typeof(LayoutElement))]`로 VLG 안에서 안전한 높이 보장. 재료 부족 시 텍스트 빨간색.

### Item System (`Assets/1 Script/Item/`)

- `ItemData` — `ItemName`, `Description`, `Icon`, `Weight`, `Width`, `Height`. `MaxStack` 가상 (기본 1)
- `ResourceItemData : ItemData` — `ResourceType`, `Stack` (`MaxStack` 오버라이드)
- `WeaponData : ItemData` — combat + damage type fields
- `ArmorData : ItemData` — `ArmorSlot`, `DamageReduction`
- `MinionData : UnitData` — summon cost, AllowedJobs
- `RecipeData : ScriptableObject` — 제작 레시피 정의
- `Inventory` — 그리드 기반. `TryAdd` / `TryAddAt` / `MoveItem` / `Remove` / `RemoveByType` / `RemoveInstance` / `CanPlaceItem` / `CanPlaceExtern` / `GetTotalQuantity` / `GetTotalByType` / `GetTotalWeight` / `GetAt`
- `ItemInstance` — 런타임 객체: `Data`, `Quantity`, `GridX`, `GridY`, `Rotated`, `EffectiveWidth/Height`
- `DroppedItem` — `DroppedItem.Spawn(item, amount, position)` 정적 팩토리. 작은 큐브 시각 + SphereCollider, 자동으로 `GroundHaulJob` 등록

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
- `InventoryGridUI` — 그리드 시각화. `_activeGrids` 정적 목록으로 다중 컨테이너 드래그 지원. `FindUnderCursor(screenPos, cam)` / `ShowPreview(inst, x, y, rotated, isExternal)` / `ClearPreview` / `ScreenToGrid` 제공
- `DraggableItem` — 아이콘 자동 추가. 드래그/드롭/회전. 다중 컨테이너 이동, 그리드 밖 드롭 시 인벤토리 위치에 `DroppedItem.Spawn`. 색 피드백: 초록(가능)/빨강(불가). R 키로 90° 회전 (스프라이트 실제 회전 + 셀 점유 변경)
- `CraftingTableUI` — 마스터-디테일 레이아웃. 왼쪽 ScrollView, 오른쪽 디테일 패널
- `RecipeEntryUI` — 레시피 목록 항목. Button + TextMeshProUGUI

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
| `ItemData` | `DungeonColony/ItemData` | ItemName, Description, Icon, Width, Height, Weight |
| `ResourceItemData` | `DungeonColony/ResourceItemData` | (ItemData) + ResourceType, Stack |
| `RecipeData` | `DungeonColony/RecipeData` | RecipeName, Ingredients, Result, ResultAmount, CraftTime, Category |

## TODOs in Code

- 현재 미구현 항목 없음 (코드 내 TODO 정리 완료)

## 최근 변경 (2026-04-29)

### 1. 인벤토리 단계 5·6 완료
- **드롭 위치 색 피드백** — `InventoryGridUI.ShowPreview` 가 대상 셀에 초록/빨강 오버레이
- **스프라이트 회전** — R 키 시 `_rt.localRotation = Euler(0,0,-90°)` 로 실제 회전 + 셀 점유 변경
- **월드 드롭** — UI 밖 드롭 시 `_sourceGrid.Inventory.transform.position` 에 `DroppedItem.Spawn` (큐브 0.25 스케일 시각 추가)
- **다중 컨테이너 드래그** — `InventoryGridUI._activeGrids` 정적 목록 + `FindUnderCursor(screenPos, cam)`. `TryPlace` 가 같은 그리드면 `MoveItem`, 다른 그리드면 `TryAddAt` + `RemoveInstance`
- **드래그 좌표계 버그 수정** — `ScreenToGrid` 가 `_itemsRoot` (pivot 0,1) 기준으로 변환하도록 변경. 기존 `(RectTransform)transform` 기준은 InventoryGridUI 피벗에 따라 항상 (0,0) 반환했음
- **`Inventory.CanPlaceExtern(ItemData, x, y, rotated)`** — 다른 인벤 출신 아이템도 색 피드백 가능

### 2. 제작 시스템 (Crafting)
- `RecipeData` (ScriptableObject) + `CraftingTable` (씬 오브젝트) + `CraftJob` (작업) + `CraftingTableUI` (마스터-디테일 UI)
- `JobCategory.Crafting` 추가
- `StorageBuilding.GetTotalByItem` / `ConsumeByItem` 추가 (ItemData 기반 집계/소비)
- 결과물은 항상 테이블 위에 `DroppedItem.Spawn` → HaulJob이 자동으로 창고로 운반

### 3. 폴더 정리
- `Combat/`, `Input/` 신설
- 데미지 타입 → `Combat/`, 카메라/선택/이동 → `Input/`
- 잡다한 루트 파일들을 의미별 폴더로 이동 (ArmorSlot, ResourceType → Item; JobCategory, PriorityManager, StorageBuilding, UnitPriorities → Job; Pathfinder, ResourceNodeData → Map; Equipment, PawnMover → unit)

---

## 알려진 이슈

- **한글 폰트 경고**: 코드로 `AddComponent<TextMeshProUGUI>` 시 기본 폰트(LiberationSans SDF)가 한글 미지원 → `Project Settings → TextMesh Pro → Default Font Asset`에 한글 폰트 지정 권장.
- **핵 이동 시 빌드존 리셋**: 핵 따라 그리드 데이터 초기화. 미리 지어둔 건물의 시각은 사라짐 (실제 GameObject는 그대로 남음). 의도된 동작이지만 향후 정책 결정 필요.
- **CraftJob 재료 손실**: `OnStart`에서 재료를 즉시 소비하므로 유닛이 도중에 죽으면 결과물 없이 재료 손실. 의도된 단순 동작.

## Upcoming Systems

- **DungeonCore area effects** — unit buffs/debuffs based on proximity and core environment type.
- **Enemy spawn / event system**.
- **Enemy loot drops** — `DroppedItem.Spawn()` on enemy death.
- **Research / Tech system** — 제작 레시피 해금 등.
- **Crafting 확장** — 레시피별 요구 스킬, 스킬 성장, 결과 품질 차등.

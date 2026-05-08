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

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research.

### Resource & Storage System

**ResourceNode** — `DropYield()` adds items to own `Inventory`, then registers `HaulJob` to nearest compatible `Storage`.

**Storage** (이전 이름: `StorageBuilding`) — per-building `Inventory` with item type filter. 클릭 가능한 씬 오브젝트.
- `BuildingName` — 표시명 (`StorageBuildingData.Init()`로 설정)
- `allowedTypes: List<ResourceType>` — empty = accept all
- `AllowedTypes` — 읽기 전용 노출
- `SetAllowAll()` / `AddFilter(type)` / `RemoveFilter(type)` / `HasFilter(type)` — 필터 조작
- `CanAccept(ItemData)` — 필터 통과 여부
- `TryAdd(item, amount)` — 필터 통과 시에만 입고
- `Init(StorageBuildingData)` — 빌드 모드 배치 시 초기화
- `static GetTotal(ResourceType)` / `Consume(ResourceType, amount)` — ResourceType 기반
- `static GetTotalByItem(ItemData)` / `ConsumeByItem(ItemData, amount)` — ItemData 기반 (제작 재료용)
- `static FindNearest(position, item)` — nearest that accepts the item
- `static OnSelected` — 좌클릭 시 이벤트 → `StorageBuildingUI`가 구독

**StorageBuildingData** (ScriptableObject, `DungeonColony/StorageBuildingData`):
- `BuildingName`, `Category`, `CostType`, `CostAmount`, `Prefab`
- 빌드 모드에서 창고 배치 시 사용. 프리팹에 `Storage`(+`Inventory`+Collider) 컴포넌트 미리 부착 필요.

**StorageBuildingUI** — 창고 좌클릭 시 열리는 패널.
- 타이틀바 (가로 전체 50px) + 닫기 버튼
- 본문: `InventoryGridUI`로 창고 인벤토리 그리드 시각화 (PawnInventoryPanel과 동일 방식)
- 다른 인벤토리에서 드래그-드롭으로 옮길 수 있음 (`InventoryGridUI._activeGrids` 정적 목록)
- `Storage.CanAccept` 필터가 드롭 시에도 적용됨 (`InventoryGridUI.ReceiveFilter` 델리게이트)

**BuildModeManager** — `placeableStorages: StorageBuildingData[]` 추가. 창고 선택 시 직접 배치 (Blueprint 없음), 자원 소비 후 `Storage.Init()` 호출. 프리팹에 `Storage` 컴포넌트가 없으면 경고 후 취소.

### Crafting System (`Job/CraftingTable.cs`, `Job/CraftJob.cs`, `Item/RecipeData.cs`, `UI/CraftingTableUI.cs`, `UI/RecipeEntryUI.cs`)

**RecipeData** (ScriptableObject):
- `RecipeName`, `Ingredients: List<{ItemData, int}>`, `Result: ItemData`, `ResultAmount`, `CraftTime`, `Category`

**CraftingTable** (씬 오브젝트):
- `tableType: CraftSkill` — 테이블 종류 (WeaponCrafting/ArmorCrafting/Cooking/Alchemy/Research 등)
- `Recipes` — `DataManager.GetRecipesBySkill(tableType)`로 자동 필터된 레시피 목록
- 좌클릭 시 `OnSelected` 정적 이벤트 발행 → `CraftingTableUI`가 구독
- `QueueOrder(recipe)` → `JobManager.RegisterJob(new CraftJob(...))`
- `CanCraft(recipe)` — 재료 충분 여부 확인 (`Storage.GetTotalByItem`)

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

**메인 코어 (`DungeonCore`)** — singleton. 빌드존 기준점. 왼쪽 클릭으로 이동.

- `Summon(MinionData, position)` — 캡 확인 → `Storage.Consume` → `minionEntries` 프리팹 스폰
- `CurrentMinionCount` / `TotalMinionCap` — 살아있는 하수인 수 / 전체 한도 (BaseMinionCap + 서브 코어 합산)
- `IsWithinAnyRange(Vector2Int)` / `IsWithinAnyRangeWorld(Vector3)` — 메인 코어 OR 서브 코어 범위 내 여부
- `RegisterSubCore` / `UnregisterSubCore` — 서브 코어 등록/해제
- `OnUnitSpawned` / `OnUnitDied` 이벤트 구독으로 하수인 수 자동 추적

**서브 코어 (`SubCore`)** — 빌드 모드로 설치하는 보조 건물. 연결성 불필요 (독립 동작).

- 범위 내 빌드존 확장 (`IsWithinAnyRange` 경유)
- 하수인 캡 증가 (`MinionCapBonus`)
- `Start`에서 `DungeonCore.RegisterSubCore`, `OnDestroy`에서 `UnregisterSubCore`
- 보라색 원형 범위 시각

**`SubCoreData`** (ScriptableObject) — `DungeonColony/SubCoreData`
- `CoreName`, `Radius`, `MinionCapBonus`
- `MoveSpeedModifier`, `DamagePerSecond` (범위 효과 — 미적용, 향후 구현)
- `CostType`, `CostAmount` — 건설 자원 비용
- `Prefab`

**`DungeonCoreData`** 변경: `BaseMinionCap` 필드 추가 (기본값 인스펙터에서 설정).

**빌드 모드 (`BuildModeManager`) 변경:**
- `placeableSubCores: SubCoreData[]` 추가 — 휠로 ResourceNode와 함께 선택
- 배치 시 `SubCore.Init(data)` 호출 + 자원 소비 (`TryConsumeSubCoreCost`)
- 범위 체크: `core.IsWithinRange` → `core.IsWithinAnyRange` (서브 코어 범위도 허용)
- `OnGUI`에 `하수인: N/M` 캡 현황 표시

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

- `UnitHPUI` — 선택 유닛의 HP/혈액/신체부위/장착/상태이상 표시 (왼쪽 하단, 항상 표시)
- `UnitStatsUI` — 선택 유닛의 속성/전투/마법/노동 스킬 표시 (C키 토글)
- `PawnInventoryPanel` — I키로 인벤토리 열기; 아이템 클릭 시 장착, 장착 슬롯 클릭 시 해제; 시체 루팅 지원
- `EquipmentSlotUI` — 장비 슬롯 표시 컴포넌트 (슬롯명 + 아이콘 + 클릭 해제)
- `DraggablePanel` — 헤더 오브젝트에 붙이고 `Target`에 이동할 패널 RectTransform 연결; 드래그로 패널 이동
- `InventoryGridUI` — 그리드 시각화. `_activeGrids` 정적 목록으로 다중 컨테이너 드래그 지원. `ReceiveFilter: Func<ItemData, bool>` 델리게이트로 외부 필터 주입 (창고 등). `FindUnderCursor(screenPos, cam)` / `ShowPreview(inst, x, y, rotated, isExternal)` / `ClearPreview` / `ScreenToGrid` 제공
- `DraggableItem` — 아이콘 자동 추가. 드래그/드롭/회전. 다중 컨테이너 이동, 그리드 밖 드롭 시 인벤토리 위치에 `DroppedItem.Spawn`. 색 피드백: 초록(가능)/빨강(불가). R 키로 90° 회전 (스프라이트 실제 회전 + 셀 점유 변경). 드롭 시 `targetGrid.CanReceiveItem` 체크로 필터 적용
- `CraftingTableUI` — 마스터-디테일 레이아웃. 왼쪽 ScrollView, 오른쪽 디테일 패널. 테이블 타입 이름을 헤더로 표시
- `RecipeEntryUI` — 레시피 목록 항목. Button + TextMeshProUGUI
- `StorageBuildingUI` — 창고 클릭 시 열리는 패널. 타이틀바 + `InventoryGridUI` 그리드. `Storage.CanAccept`를 `ReceiveFilter`에 연결해 드래그-드롭 필터 적용
- `ResearchTableUI` — 연구 테이블 클릭 시 열림. 마스터-디테일 (목록 + 상세). 선행 연구/재료/시간 표시
- `ResearchEntryUI` — 연구 목록 항목. 완료(초록) / 잠김(회색) / 재료부족(빨강) 색상 구분

### Pathfinding

**3단계 계층 A* 길찾기** (NavMesh 제거 — 건물 배치 시 재베이크 불필요).

| 단계 | 클래스 | 타일 크기 | 용도 |
|---|---|---|---|
| L1 | `LocalGridManager` | 1×1 | 유닛 주변 정밀 이동 |
| L2 | `PathGrid` (in `HierarchicalPathfinder`) | 100×100 | 지역 경로 |
| L3 | `PathGrid` (in `HierarchicalPathfinder`) | 1000×1000 | 광역 라우팅 |

**`PathGrid`** — 범용 A* 격자. L1·L2·L3 공용. Octile 거리 휴리스틱, 8방향 이동, 이동 비용(cost) 지원.

**`HierarchicalPathfinder`** (MonoBehaviour 싱글톤, 씬에 배치 필요):
- 게임 시작 시 `Terrain` 경사도 샘플링으로 L2·L3 자동 베이킹
- `obstacleLayer` — Inspector에서 건물/벽 레이어 지정 (Terrain 레이어는 제외)
- `MapManager.OnTileChanged` 구독 → 타일 변경 시 L1·L2·L3 해당 셀만 즉시 갱신
- `FindPath(from, to)`: 단거리(L1 범위 내)는 L1 A*, 장거리는 L2 A* + 첫 구간 L1 정밀화

**`LocalGridManager`** — L1 그리드 관리:
- **공유 그리드**: DungeonCore 기준 80×80 타일. DungeonCore 이동 시 재중심.
- **클러스터 그리드**: 공유 범위 밖 유닛들을 20타일 반경 기준으로 그룹화, 그룹 평균 위치 기준 40×40 그리드 생성. 3초 주기 갱신.
- `Physics.CheckBox`로 장애물 감지 시 `obstacleLayer`만 검사.

**`Pathfinder`** (static) — `PawnMover`에서 사용하는 진입점. `HierarchicalPathfinder.Instance.FindPath()`에 위임. Instance 없으면 직선 폴백.

**맵**: Unity Terrain 기반. 개발 중 단일 Terrain 5,000×5,000, 최종 목표 10,000×10,000 (타일 분할). `Terrain.activeTerrain.terrainData.size`로 크기 자동 인식.

**이동 비용**: Terrain 경사도 기반 (`GetSteepness`). 45° 이상 불가, 이하는 경사도 × `slopeCostMult` 만큼 비용 증가. 향후 텍스처 레이어별 비용(도로=0.5, 습지=3 등) 추가 예정.

## ScriptableObject Assets (`Assets/5 Data/`)

| Type | Menu path | Key fields |
|---|---|---|
| `UnitData` | `DungeonColony/UnitData` | RaceName, MaxHP, MoveSpeed, AttackDamage, AttackRange, DetectRange + stat ranges |
| `MinionData` | `DungeonColony/MinionData` | (UnitData fields) + SummonCostType, SummonCostAmount, AllowedJobs, **CanProvideWater** |
| `WeaponData` | `DungeonColony/WeaponData` | WeaponType, BaseDamage, BaseAttackInterval, AttackRange, IsLargeWeapon, IsBladed, IsRanged, BlockBonus, RangedBlockBonus, DamageCategory, PhysicalType, DamageElement, EffectChance |
| `ArmorData` | `DungeonColony/ArmorData` | ArmorSlot, DamageReduction |
| `WallData` | `DungeonColony/WallData` | Hardness, DropAmount |
| `DungeonCoreData` | `DungeonColony/DungeonCoreData` | CoreType, BaseRadius, MaxGrade, **BaseMinionCap**, per-frame modifiers |
| `SubCoreData` | `DungeonColony/SubCoreData` | CoreName, Radius, MinionCapBonus, MoveSpeedModifier, DamagePerSecond, CostType, CostAmount, Prefab |
| `ResourceNodeData` | `DungeonColony/ResourceNodeData` | ResourceType, GaugeRequired, DropMin, DropMax, DropItem |
| `ItemData` | `DungeonColony/ItemData` | ItemName, Description, Icon, Width, Height, Weight |
| `ResourceItemData` | `DungeonColony/ResourceItemData` | (ItemData) + ResourceType, Stack |
| `SeedData` | `DungeonColony/SeedData` | (ItemData) + HarvestItem, HarvestAmount, GrowthTime, MoistureDecay, WaterFillAmount |
| `RecipeData` | `DungeonColony/RecipeData` | RecipeName, Ingredients, Result, ResultAmount, CraftTime, Category |

## TODOs in Code

- 현재 미구현 항목 없음 (코드 내 TODO 정리 완료)

## 최근 변경 (2026-04-30)

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
- `Storage.GetTotalByItem` / `ConsumeByItem` 추가 (ItemData 기반 집계/소비)
- 제작 완료 시 결과물이 작업 유닛 인벤토리로 입고 (자리 없으면 드롭 fallback)
- 제작 시작/완료 로그 추가

### 3. 서브 코어 + 하수인 캡 시스템
- `SubCoreData` (ScriptableObject) + `SubCore` (MonoBehaviour) 신설
- 빌드 모드(B키)로 설치, 연결성 없이 독립 동작 — 설치 위치 무관하게 효과 발동
- 하수인 캡: `DungeonCoreData.BaseMinionCap` + 설치된 서브 코어 `MinionCapBonus` 합산
- `DungeonCore.Summon()` — 캡 초과 시 소환 거부, 소환 성공/실패 로그에 `N/M` 표시
- `IsWithinAnyRange` — 메인 코어 + 서브 코어 범위 합산으로 빌드 가능 영역 판단

### 4. 폴더 정리
- `Combat/`, `Input/` 신설
- 데미지 타입 → `Combat/`, 카메라/선택/이동 → `Input/`
- 잡다한 루트 파일들을 의미별 폴더로 이동

### 5. 건설 블루프린트 시스템
- B키로 건설 모드 진입 → 위치 클릭 시 즉시 건설이 아닌 **예비 설계(Blueprint)** 배치
- `BuildingBlueprint` — SubCoreData.Prefab 고스트 시각(반투명 파란색). 자재 납품 완료 시 실제 건물 스폰 후 자폭
- `ConstructionJob` (Category: Construction) — 창고에서 자재 수거 → 설계도에 납품 2-phase
- `Storage.OnChanged` 구독으로 창고에 자재 들어올 때 자동으로 건설 작업 등록
- `BuildingBlueprint.All` 정적 목록으로 중복 배치 방지

### 6. 건설 UI (`BuildModeUI`, `BuildingItemUI`)
- 화면 하단 가로 바. 왼쪽: 카테고리 버튼(2열 GridLayout), 오른쪽: 선택 카테고리 건물 목록(가로 스크롤)
- `BuildCategory` enum (Resource / Defense / Building / Misc). `SubCoreData.Category` 필드로 탭 배치 결정
- `BuildingItemUI` — 건물 버튼 프리팹 스크립트. background / icon / nameLabel / costLabel SerializeField
- 자원 부족 시 빨간색, 선택 중 파란색. NanumGothic 폰트 지원
- 건설 모드 진입/해제 시 `BuildModeManager.OnActiveChanged` 이벤트로 다른 UI 자동 연동
- 휠 선택 제거 — UI 클릭으로만 건물 선택
- `BuildModeManager` 공개 API: `SelectBuilding`, `SelectedIndex`, `GetBuilding(i)`, `TotalBuildingCount`

### 7. SubCore 범위 효과
- `SubCore.Update()` — 0.3초 간격으로 `Physics.OverlapSphere` 로 범위 내 유닛 탐색
- **우호 유닛**: `MoveSpeedModifier > 0` → `Hasted` / `< 0` → `Slowed` (StatusEffectSystem 연동)
- **적 유닛**: `DamagePerSecond > 0` → 매 틱 `DPS × interval` 피해

### 8. UI 시스템 정비
- `BuildModeManager.OnActiveChanged` — 건설 모드 열릴 때 `UnitInfoUI`, `PawnInventoryPanel`, `PriorityPanel`, `CraftingTableUI`, `DungeonCoreUI`, `NodeInfoPanel`, `PawnRosterPanel` 자동 닫힘
- `PawnRosterPanel` — `panel` SerializeField 추가. 건설 모드 외에는 항상 표시(열리고 닫힘 자동)
- `NodeInfoPanel` — 바깥 클릭 시 자동 닫힘 제거. 직접 클릭한 노드를 열었을 때만 열림
- `SelectionManager` — 자원 노드 클릭 시 `DungeonCore.Deselect()` 호출 제거 (코어창 유지)
- `CloseButton` — 버튼에 붙이고 `Target`에 닫을 패널 연결하면 클릭 시 비활성화

### 9. 제작 시스템 확장
- **`CraftSkill`** — Labor / WeaponCrafting / ArmorCrafting / Cooking / Alchemy / Research
- **`ItemQuality`** — Common(일반) / Uncommon(고급) / Rare(희귀) / Unique(유니크) / Epic(에픽) / Legendary(전설). `StatMultiplier()` (1.0 / 1.2 / 1.5 / 2.0 / 2.5 / 3.0) 제공 (전투 연동은 추후)
- **등급 확률** (스킬 100 기준): 전설 2% / 에픽 5% / 유니크 10% / 희귀 20% / 고급 35% / 일반 28%. 스킬 미달 시 일반 고정
- **장비 vs 소모품 분기**: WeaponData/ArmorData → 등급 판정·수량 고정. 나머지 → 등급 없음·수량 배율(스킬 100 = ×1.5)
- **스킬 성장**: `RecipeData.SkillGrowthAmount` 만큼 제작 완료마다 성장
- **`Inventory.TryAdd`** — `ItemQuality` 파라미터 추가. 같은 등급끼리만 스택
- **`ItemInstance.DisplayName`** — 등급 접두사 포함 표시명 (`[희귀] 철검` 등)
- **`ResearchData`** (ScriptableObject) — 선행 연구 / 재료 / 시간 / 해금 레시피 목록
- **`ResearchManager`** — 완료 연구 추적. `IsUnlocked(recipe)` / `CanResearch(data)` / `Complete(data)`
- **`RecipeData.RequiredResearch`** — null이면 기본 개방, 지정 시 연구 완료 후 해금
- **`RecipeEntryUI`** — 잠긴 레시피는 `[잠김]` 접두사 + 회색 표시, 버튼 비활성

## 최근 변경 (2026-05-01)

### 1. ItemQuality 등급 체계 변경
- 기존 4등급 (Normal/Good/Excellent/Masterwork) → 6등급 (Common/Uncommon/Rare/Unique/Epic/Legendary)
- 한글 표시명: 일반/고급/희귀/유니크/에픽/전설
- `StatMultiplier()`: 1.0 / 1.2 / 1.5 / 2.0 / 2.5 / 3.0
- 등급 확률 (스킬 100): 전설 2% / 에픽 5% / 유니크 10% / 희귀 20% / 고급 35% / 일반 28% (고차 다항식: `t^n`)

### 2. ItemQuality → 전투 스탯 연동
- `Equipment` — `ItemInstance` 추적 추가. 무기/오프핸드/갑옷별 `_mainHandInst`, `_offHandInst`, `_armorInst` 딕셔너리
- `SyncBlackboard()`: `AttackDamage = BaseDamage × Quality.StatMultiplier()`
- `Equipment.GetArmorQualityMult(slot)` — 갑옷 슬롯별 등급 배율 노출
- `BodyPartSystem.TakeDamage()`: `armor.DamageReduction × qualityMult` 적용
- 장착/해제 시 `ItemInstance` 인벤토리 왕복하면서 등급 보존 (`Inventory.TryAdd(item, amount, quality)`)

### 3. CraftingTable 타입 분리
- `CraftingTable.tableType: CraftSkill` 필드 추가 (`WeaponCrafting` 기본값)
- `Recipes` 프로퍼티 → `DataManager.GetRecipesBySkill(tableType)` 자동 조회 (수동 등록 불필요)
- `availableRecipes` 필드 완전 제거
- `CraftSkillExt.DisplayName()` 추가 — "무기 제작대" / "방어구 제작대" 등
- `CraftingTableUI`에 `tableTitleText` 필드 추가 — 테이블 타입 이름 헤더 표시

### 4. Data / 모딩 시스템 (`Assets/1 Script/Data/`)
- `DataManager` (static class, `RuntimeInitializeOnLoadMethod` 자동 초기화) — 게임 데이터 중앙 레지스트리
- 로딩 순서: `Resources/Data/{Items,Recipes,Research}` (SO) → `StreamingAssets/Data/{items,recipes}` (JSON) → `StreamingAssets/Mods/[ModName]/...` (모드)
- `DataSchemas.cs` — JSON 역직렬화 스키마 (ItemSchema/WeaponSchema/ArmorSchema/ResourceItemSchema/RecipeSchema/IngredientSchema)
- 기존 SO 에셋(`Assets/5_Data/`)은 `Assets/Resources/Data/`로 이동됨 (Stone/Ore/Crystal items, stick weapon, stick recipe, research)
- 향후 모드 추가는 `StreamingAssets/Mods/[modname]/` 폴더에 JSON 추가만으로 가능

### 5. ResearchJob 시스템
- `JobCategory.Research` 추가
- `ResearchTable` (씬 오브젝트) — 좌클릭 이벤트 + `availableResearch: List<ResearchData>` + `QueueResearch()` / `CanResearch()`
- `ResearchJob` — `OnStart`에서 재료 즉시 소비, `Execute`에서 `ResearchTime` 카운트다운 후 `ResearchManager.Complete()` 호출 + `Stats.Research` 성장
- `ResearchTableUI` — 마스터-디테일 (목록 + 상세). 선행연구/재료/시간 표시
- `ResearchEntryUI` — 완료(초록)/잠김(회색)/재료부족(빨강) 색상 구분

### 6. UnitInfoUI 분리
- 기존 단일 패널 → 두 개로 분리:
  - `UnitHPUI` — HP/혈액/신체부위/장착/상태이상 (왼쪽 하단, 선택 시 항상 표시)
  - `UnitStatsUI` — 속성/전투/마법/노동 스킬 (C키 토글)

### 7. Storage 시스템 개편
- **`StorageBuilding` → `Storage`** 클래스 이름 변경 (파일명도 `Storage.cs`로). `StorageBuildingData`, `StorageBuildingUI`는 그대로
- `Storage` 클래스 신규 기능:
  - `BuildingName` 표시명 필드
  - 좌클릭 시 `OnSelected` 정적 이벤트 발행
  - 필터 조작 API: `SetAllowAll()` / `AddFilter()` / `RemoveFilter()` / `HasFilter()`
  - `Init(StorageBuildingData)` — 빌드 모드 배치 시 호출
- `StorageBuildingData` (ScriptableObject) — 빌드 모드용. `BuildingName`, `Category`, `CostType`, `CostAmount`, `Prefab`
- `BuildModeManager.placeableStorages` 추가 — 빌드 모드에서 창고 배치 가능. 자원 소비 후 `Storage.Init()` 호출. 프리팹에 `Storage` 컴포넌트가 없으면 경고 후 취소
- `StorageBuildingUI` — 창고 좌클릭 시 열리는 패널. 타이틀바 + `InventoryGridUI`로 그리드 시각화 (PawnInventoryPanel과 동일 방식). 다른 인벤토리에서 드래그-드롭으로 옮기기 가능
- **`InventoryGridUI.ReceiveFilter`** — `Func<ItemData, bool>` 델리게이트. 외부에서 수신 필터 주입. `StorageBuildingUI`가 `Storage.CanAccept`을 연결해 드래그-드롭 시 필터 적용. 프리뷰 색상도 필터 반영

---

## Data / Modding System (`Assets/1 Script/Data/`)

게임 데이터는 두 경로로 로드됨. 나중에 오는 데이터가 앞선 것을 덮어씀.

```
1. Assets/Resources/Data/Items|Recipes|Research   ← 기존 ScriptableObject 에셋
2. StreamingAssets/Data/items|recipes             ← 기본 JSON 콘텐츠
3. StreamingAssets/Mods/[ModName]/items|recipes   ← 모드 JSON
```

**`DataManager`** (static class, `RuntimeInitializeOnLoadMethod` 자동 초기화):
- `GetItem(name)` / `GetRecipe(name)` / `GetResearch(name)` — ID(이름)로 단건 조회
- `GetRecipesBySkill(CraftSkill)` — 제작대 타입별 레시피 목록
- `AllItems` / `AllRecipes` — 전체 조회

**JSON 파일 형식** (`StreamingAssets/Data/`):

아이템 — `type` 필드로 종류 구분:
```json
// items/iron_sword.json
{
  "type": "weapon",
  "itemName": "철검",
  "iconPath": "Icons/iron_sword",
  "weight": 2.0,
  "weaponType": "OneHandSword",
  "isBladed": true,
  "baseDamage": 15,
  "baseAttackInterval": 1.8,
  "attackRange": 1.5,
  "damageCategory": "Physical",
  "physicalType": "Slash",
  "damageElement": "None",
  "effectChance": 0.15
}
// type 값: "weapon" | "armor" | "resource" | 없으면 기본 아이템
```

레시피:
```json
// recipes/iron_sword.json
{
  "recipeName": "철검 제작",
  "ingredients": [
    { "itemName": "철 주괴", "amount": 2 },
    { "itemName": "나무 막대", "amount": 1 }
  ],
  "resultItemName": "철검",
  "resultAmount": 1,
  "craftTime": 10.0,
  "requiredSkillType": "WeaponCrafting",
  "requiredSkillLevel": 0,
  "skillGrowthAmount": 0.5
}
```

열거형은 문자열 (대소문자 무시):
- `weaponType`: `OneHandSword`, `TwoHandAxe`, `Bow`, `ElementalFire`, ...
- `damageCategory`: `Physical`, `Elemental`, `Dark`, `Holy`
- `physicalType`: `Slash`, `Pierce`, `Blunt`
- `damageElement`: `None`, `Fire`, `Ice`, `Lightning`, `Poison`
- `armorSlot`: `Head`, `Chest`, `Hands`, `Legs`
- `resourceType`: `Stone`, `IronOre`, `CopperOre`, `TinOre`, `Coal`, `SilverOre`, `GoldOre`, `MagicStone`, `Ruby`, `Sapphire`, `Topaz`, `Emerald`, `Diamond`, `Amethyst`, `Bronze`, `Steel`, `SilverIngot`, `GoldIngot`, `Wood`
- `requiredSkillType`: `Labor`, `WeaponCrafting`, `ArmorCrafting`, `Cooking`, `Alchemy`, `Research`

**아이콘 스프라이트**: `iconPath`는 `Resources/` 기준 경로 (확장자 제외). 스프라이트를 `Assets/Resources/Icons/` 폴더에 위치시켜야 함.

**기존 SO 에셋**: `Assets/Resources/Data/Items|Recipes|Research` 폴더로 이동하면 DataManager가 자동 수집. 이동하지 않으면 JSON으로 대체하거나 `CraftingTable` 인스펙터에서 직접 참조 불가.

**미구현 JSON 타입** (향후 추가 예정):
- `UnitData` / `MinionData` — 유닛 스탯
- `SubCoreData` — 서브 코어 (프리팹 참조 문제 해결 필요)
- `ResearchData` — 연구 데이터

## 알려진 이슈

- **한글 폰트 경고**: 코드로 `AddComponent<TextMeshProUGUI>` 시 기본 폰트(LiberationSans SDF)가 한글 미지원 → `Project Settings → TextMesh Pro → Default Font Asset`에 한글 폰트 지정 권장.
- **핵 이동 시 빌드존 리셋**: 핵 따라 그리드 데이터 초기화. 미리 지어둔 건물의 시각은 사라짐 (실제 GameObject는 그대로 남음). 의도된 동작이지만 향후 정책 결정 필요.
- **CraftJob 재료 손실**: `OnStart`에서 재료를 즉시 소비하므로 유닛이 도중에 죽으면 결과물 없이 재료 손실. 의도된 단순 동작.
- **Blueprint 반투명 머티리얼**: URP 프로젝트 설정에 따라 코드로 생성한 투명 머티리얼이 불투명으로 렌더될 수 있음. 에디터에서 반투명 머티리얼을 직접 제작 후 `SubCoreData`에 별도 필드로 연결 권장.
- **DataManager SO 미수집**: 기존 RecipeData 에셋이 `Assets/Resources/Data/Recipes/`에 없으면 DataManager가 인식 못함. 에셋을 해당 폴더로 이동하거나 JSON으로 전환 필요.

## 최근 변경 (2026-05-02)

### 1. MapManager 다중 존 구조 준비
- 싱글톤 강제(`Destroy`) 제거 → 씬에 여러 `MapManager` 공존 가능
- `static List<MapManager> _allZones` 레지스트리 추가 (Awake 등록 / OnDestroy 해제)
- `static GetForPosition(Vector3)` — 해당 좌표를 포함하는 존 반환, 없으면 Instance 폴백
- `SetAsPlayerZone()` — DungeonCore가 플레이어 던전존 명시 지정용
- 기존 `Instance` 호출 코드 변경 없음. 미래 다중 정착지 길찾기 확장 진입점

### 2. BloodCap 시스템 (부상 회복 제한)
- `BodyPartSystem`에 `BloodCap` 추가 — 자연회복 상한선
- `HealBlood(amount, degradeCap)` — 포션/마법 힐은 `BloodCap`을 회복량 × 0.5 감소
- `RestoreBloodCap(amount)` — 침대 치료 시 호출, 캡 복구
- 침대 없이는 최대 절반까지만 자연회복 가능

### 3. 침대 시스템
- `Bed.cs` — 점유(`TryOccupy`/`Vacate`) + 배정(`Assign`/`Unassign`) 관리. 클릭 시 `OnSelected` 이벤트
- `BedUI.cs` — 침대 클릭 시 열리는 패널. 선택 유닛 배정 / 배정 해제 버튼
- `BedUICreator.cs` (에디터) — DungeonColony → Create Bed UI 메뉴로 자동 생성
- 프리팹에 Collider + `Bed` 컴포넌트 필요
- `RestNode` (BT) — 혈액 60% 이하 또는 BloodCap 미복구 시 배정된 침대로 이동해 회복
- 방랑자·하수인 BT에 `RestNode` 추가 (Fight → **Rest** → Work)

### 4. BuildingData 통합 (건물 데이터 단순화)
- `SubCoreData`, `StorageBuildingData`, `BedData` → **`BuildingData`** 하나로 통합
- `BuildingData` 필드: `BuildingName`, `Category`, `CostType`, `CostAmount`, `Prefab`, `UsesBlueprint`
- `SubCore` 전용 필드(`Radius`, `MinionCapBonus`, `MoveSpeedModifier`, `DamagePerSecond`)는 컴포넌트 `[SerializeField]`로 이동 — 프리팹 인스펙터에서 직접 설정
- `BuildModeManager` — `placeableSubCores` + `placeableStorages` + `placeableBeds` → `placeableBuildings: BuildingData[]` 단일 배열
- 배치 로직: `UsesBlueprint=true` → Blueprint 2단계, `false` → 즉시 스폰 후 `Storage.Init` / `Bed.Init` 자동 호출
- `BuildModeUI` — 건물 타입별 분기 제거, 단순 루프로 통일
- 기존 `SubCoreData`/`StorageBuildingData`/`BedData` .cs 파일 삭제 → 에셋 재생성 필요

### 5. 순찰 시스템
- `PatrolMode` enum: `Waypoint` (지정 포인트 순환) / `Area` (구역 내 무작위) / `Idle` (중심점 대기)
- `PatrolZone.cs` — 씬 오브젝트. 모드별 순찰 위치 계산. 여러 하수인 배정 가능. 기즈모 시각화
- `UnitBlackboard.AssignedPatrolZone` 추가
- `PatrolNode` (BT) — 배정된 구역 없으면 Failure(작업으로 폴백), 있으면 모드별 이동
- 하수인 BT에 `PatrolNode` 추가 (Fight → Rest → **Patrol** → Work)
- `PatrolZoneUI.cs` — 클릭 시 열림. 모드 전환 버튼 3개 + 배정 유닛 목록 + 해제 버튼
- `PatrolZoneUICreator.cs` (에디터) — DungeonColony → Create PatrolZone UI 메뉴로 자동 생성

## 최근 변경 (2026-05-06)

### 1. 농사 시스템 (Farming)

**새 파일:**
- `SeedData : ItemData` (ScriptableObject) — 씨앗 아이템. `HarvestItem`, `HarvestAmount`, `GrowthTime`, `MoistureDecay`, `WaterFillAmount`. `MaxStack = 99`
- `FarmPlot` (씬 오브젝트, `Job/`) — 농장 토지. 상태: `Empty → Planted → ReadyToHarvest`. 수분 게이지(`Moisture`) 시간 지나면 감소, 0이면 성장 멈춤. 2초 간격으로 Job 자동 등록. 클릭 시 `OnSelected` 이벤트
- `WaterSource` (씬 오브젝트, `Job/`) — 우물. 물 무한 공급처. `FindNearest(position)` 정적 유틸
- `SeedJob` — Category: Farming. 창고에서 씨앗 소비 → 밭 심기 2-phase
- `WaterJob` — Category: Farming. `MinionData.CanProvideWater = true`면 우물 페이즈 스킵, 아니면 우물 → 밭 2-phase
- `FarmHarvestJob` — Category: Farming. 밭 수확 → 창고 운반 2-phase
- `FarmPlotUI` — 농장 클릭 시 열림. 씨앗 선택 버튼 + 상태/성장/수분 표시. ESC로 닫기

**기존 파일 수정:**
- `MinionData` — `CanProvideWater: bool` 플래그 추가 (우물 없이 직접 물 공급)
- `BuildModeManager.Place()` — `FarmPlot.Init(data)` / `WaterSource.Init(data)` 호출 추가

**에디터 스크립트** (`Assets/Editor/FarmingCreator.cs`):
- `DungeonColony → Create FarmPlot Prefab` — 갈색 납작 큐브 + FarmPlot 컴포넌트
- `DungeonColony → Create WaterSource Prefab` — 원통 우물 외형 + WaterSource 컴포넌트
- `DungeonColony → Create FarmPlot UI` — Canvas에 FarmPlotUI 패널 자동 생성 및 연결

### 2. BuildModeManager 통합 (ResourceNode + Building 완전 통합)

- `placeableNodes: ResourceNodeData[]` 제거 → `placeableBuildings: BuildingData[]` 단일 배열로 통합
- `PlaceNode()` 메서드 제거 → `Place()` 하나로 통합
- 제거된 API: `GetNode()`, `NodeBuildingCount`, `IsNodeSelected`, `SelectedNode`
- `BuildModeUI` — `nodeEnd` 오프셋 계산 로직 완전 제거, 단순 루프로 통일
- ResourceNode 프리팹은 컴포넌트에 `ResourceNodeData`를 직접 보유 (배치 시 Init 불필요)

> **설계 원칙:** `BuildingData`는 "배치 방법"만 담당. 각 컴포넌트(`ResourceNode`, `Storage`, `SubCore`, `FarmPlot` 등)는 자기 동작 데이터를 프리팹에서 직접 보유.

### 3. ResourceType 세분화

기존 `Stone / Ore / Crystal` → 20종으로 세분화. `Ore` 제거.

| 분류 | 타입 |
|---|---|
| 석재 | `Stone` |
| 원석 | `CopperOre`, `TinOre`, `IronOre`, `Coal`, `SilverOre`, `GoldOre`, `MagicStone` |
| 보석 | `Ruby`(화염), `Sapphire`(냉기), `Topaz`(번개), `Emerald`(독), `Diamond`(신성), `Amethyst`(암흑) |
| 주괴 | `Bronze`, `Steel`, `SilverIngot`, `GoldIngot` |
| 목재 | `Wood` |

- `ResourceTypeExt.DisplayName()` 확장 메서드 추가 — 한글 표시명 반환
- `NodeInfoPanel` — `ResourceType.ToString()` → `DisplayName()` 으로 교체
- 보석 6종은 기존 마법 원소(Fire/Ice/Lightning/Poison/Holy/Dark)와 1:1 대응

### 4. 아이템 계층 구조 명확화

모든 아이템은 `ItemData`를 상속해야 함:
- `ResourceItemData : ItemData` — 자원 (스택 가능)
- `WeaponData : ItemData` — 무기
- `ArmorData : ItemData` — 방어구
- `SeedData : ItemData` — 씨앗 (신규, MaxStack=99)

## Upcoming Systems

- **교역 UI** — 상인 클릭 시 거래 패널

## 최근 변경 (2026-05-08)

### 1. 벌목 시스템 (Logging)

- `JobCategory.Logging` 기존 enum에 이미 존재
- `LoggingCreator.cs` (Editor) — `DungeonColony → Create Tree Prefab` 메뉴. 줄기+수관 나무 프리팹 생성. `ResourceNode` 컴포넌트 자동 부착.
- 나무는 `ResourceNode`(Wood 타입)로 구현 — 기존 게이지 시스템 재활용
- `WorkNode.TickAssigned`에서 `ResourceType.Wood`이면 `Stats.Cutting`, 그 외는 `Stats.Mining` 사용

### 2. HaulJob 개선

- **중복 등록 방지**: `ResourceNode._haulJobPending` 플래그 추가. 운반 작업이 완료/취소될 때만 새 HaulJob 등록.
- **전량 수거**: HaulJob GoToSource 페이즈에서 `_amount` 고정값이 아닌 노드 현재 재고 전량 수거.
- **완료 콜백**: `HaulJob(onDone: Action)` — 완료/취소 시 `_haulJobPending = false` 자동 해제.
- **임계값**: `ResourceNode.haulThreshold` (기본 10) — 재고가 이 수량 이상일 때만 HaulJob 등록. `NodeInfoPanel`에서 +5/-5 버튼으로 조절 가능.
- **폰 사망 시 job 정리**: `UnitBrain.Die()`에서 `CurrentJob` 자동 취소 → pending 플래그 고착 방지.

### 3. WorkNode 개선

- 거리 체크를 `HasTarget()` 조건 밖으로 분리 — 콜라이더에 끼어도 2.0f 이내면 작업 실행.
- 작업 반경 1.5f → **2.0f** 로 확대 (TickAssigned 포함).

### 4. 3단계 계층 A* 길찾기 (NavMesh 교체)

- NavMesh 제거 — 건물 배치 시 재베이크 불필요
- `PathGrid.cs` — 범용 A* 격자 (L1/L2/L3 공용)
- `LocalGridManager.cs` — L1 공유(80×80) + 클러스터(40×40) 그리드 관리
- `HierarchicalPathfinder.cs` — MonoBehaviour 싱글톤. Terrain 자동 베이킹. 씬에 배치 필요.
- `Pathfinder.cs` — static 진입점 유지, HierarchicalPathfinder에 위임
- 맵: Unity Terrain 기반. 개발용 5,000×5,000 단일 Terrain 권장.

---

## 최근 변경 (2026-05-07)

### 1. 제련 + 가공 시스템

**ResourceType 변경:**
- `Diamond` → `Etherstone` (신성 계열 마법 촉매)
- `Amethyst` → `Voidstone` (어둠 계열 마법 촉매)
- `CutStone` 추가 (다듬은 돌 — 석재 가공대 산출물)
- `Lumber` 추가 (목재 판자 — 제재소 산출물)

**마법 결정 조합 공식 (보석 연마대):**
- 원소 보석(Ruby/Sapphire/Topaz/Emerald) + `MagicStone` → Elemental 계열 결정
- 원소 보석 + `Etherstone` → Holy 계열 결정
- 원소 보석 + `Voidstone` → Dark 계열 결정
- 총 12종 결정 아이템 (일반 `ItemData`, ResourceType 없음)

**CraftSkill 추가:** `Smelting`, `Stonecutting`, `Woodworking`, `Gemcutting`

**새 건물 (CraftingTable 변형):**
- 용광로 (`Smelting`) — 연료 시스템 적용. 광석 → 주괴
- 석재 가공대 (`Stonecutting`) — Stone → CutStone
- 제재소 (`Woodworking`) — Wood → Lumber
- 보석 연마대 (`Gemcutting`) — 보석+촉매석 → 마법 결정 12종

**연료 시스템 (`CraftingTable`):**
- `_requiresFuel: bool` — 인스펙터에서 용광로에만 체크
- `_fuelBurnRate: float` — 초당 연료 소모량 (기본 2). 용광로가 켜져있는 동안 항상 소모
- `_fuelCapacity: float` — 최대 연료량 (기본 100)
- `_acceptedFuels: List<FuelEntry>` — `{Item, FuelValue}` 쌍. 연료 아이템과 개당 연료량
- 연료 30% 이하 → `FuelJob` 자동 등록 (3초 간격 체크)
- `HasFuel` — CraftJob이 연료 없으면 대기

**`FuelJob` (`Job/FuelJob.cs`):**
- Category: Crafting
- Phase 1: 창고 이동 → 연료 아이템 수거
- Phase 2: 용광로 이동 → `AddFuel()` 호출

**`UnitStats` 추가:** `Smelting`, `Stonecutting`, `Woodworking`, `Gemcutting` 노동 스킬 (0–100)

### 2. 허기 시스템

**`UnitBlackboard.Hunger`** (구 `Mood`) — 0~100. 플레이어 팩션 유닛만 시간에 따라 감소.

**허기 감소 (`UnitBrain.UpdateHunger`):**
- `FactionType.Player`가 아니면 적용 안 됨 (Enemy, NPC 등 자동 제외)
- 기본 감소율: `BaseHungerDecayRate = 0.5f` (초당, 약 3.3분에 고갈)
- `DungeonCoreData.HungerRateModifier`로 코어가 감소율 조절 가능
- 허기 0이면 초당 2 피해

**`FoodData : ItemData`** (`Item/FoodData.cs`):
- `NutritionValue` — 섭취 시 허기 회복량
- `EatTime` — 먹는 데 걸리는 시간(초)
- `MaxStack = 20`

**`EatNode` (BT, `BTLeafNodes.cs`):**
- 허기 60 이하 시 트리거
- 인벤토리에 음식 있으면 바로 먹기, 없으면 가장 가까운 창고에서 수거 후 섭취
- BT 순서: Fight → Rest → **Eat** → Work

**DataManager `"food"` 타입 지원** — JSON으로 FoodData 로드 가능

### 3. 요리 시스템

**요리 재료 아이템:** 밀, 밀가루, 생고기, 채소, 허브

**음식 아이템 (FoodData):**

| 아이템 | 영양가 | 필요 스킬 |
|---|---|---|
| 허브차 | 15 | 0 |
| 빵 | 35 | 0 |
| 채소 수프 | 40 | 0 |
| 구운 고기 | 50 | 0 |
| 스튜 | 70 | 15 |
| 진수성찬 | 100 | 40 |

**요리대:** CraftingTable `tableType = Cooking` 설정으로 사용. 기존 CraftJob 재활용.

### 4. 아이템 계층 구조 추가

- `FoodData : ItemData` — 음식 (NutritionValue, EatTime, MaxStack=20)

### 5. 게임 데이터 Excel (`Tools/GameData.xlsx`)

- `Tools/make_gamedata.py` 실행으로 재생성
- 시트: Items / Recipes / Buildings / Enums(참고)
- 드롭다운 유효성 검사, 카테고리별 색상 구분

## 최근 변경 (2026-05-09)

### 1. 시체 루팅 UI

- `CorpseInventoryPanel.cs` — 시체 클릭 시 열리는 인벤토리 패널. `SelectionManager.OnCorpseSelected` 구독
- 씬에서 Inventory_UI 복붙 후 `PawnInventoryPanel` 대신 `CorpseInventoryPanel` 컴포넌트로 교체. `panel` + `gridContainer` 연결
- `PawnInventoryPanel`(내 인벤토리)과 동시에 열려 드래그-드롭 루팅 가능

### 2. 이벤트 시스템 (`Assets/1 Script/Event/`)

**새 파일:**
- `GameEventType` — `Raid`, `Merchant`, `Reinforcement`, `Discovery`
- `GameEventData` (ScriptableObject, `DungeonColony/GameEventData`) — 이름/설명/아이콘/가중치/MinGameTime + 타입별 필드
- `EventManager` (MonoBehaviour 싱글톤) — 씬에 배치. `checkInterval`(기본 300초) 주기로 가중치 기반 랜덤 이벤트 발동. 우클릭 → Force Roll Event로 즉시 테스트
- `MerchantBehaviour.cs` — 사용 안 함 (NpcBrain으로 대체됨)
- `EventNotificationUICreator.cs` (Editor) — `DungeonColony → Create Event Notification UI`

**이벤트별 동작:**
- `Raid` — 맵 가장자리에 `EnemyPrefabs` 스폰. 스폰된 적은 UnitBrain Enemy BT로 자동 공격
- `Merchant` — 맵 가장자리에 상인 프리팹 스폰. NpcData(EventMerchant) BT로 걸어오고 머물다 귀환 소멸
- `Reinforcement` — 알림만 표시. 실제 동작 미구현 (뼈대)
- `Discovery` — 던전 코어 기준 `DiscoveryMinDist`~`DiscoveryMaxDist` 범위 랜덤 위치에 자원 노드 스폰

**`EventNotificationUI`** — 이벤트 발동 시 화면 상단 팝업 (5초 자동 닫힘 + 닫기 버튼)

### 3. NPC 시스템

**새 파일:**
- `NpcType` — `Villager`, `Guard`, `ShopMerchant`, `EventMerchant`
- `NpcData : UnitData` (ScriptableObject, `DungeonColony/NpcData`) — `NpcType`, `WanderRadius`, `FleeDetectRange`, `StayDuration`
- `BTNpcNodes.cs` — NPC 전용 BT 노드: `NpcFleeNode`(적 감지 도주), `NpcWanderNode`(홈포인트 배회), `NpcEventMerchantNode`(이동→대기→귀환→소멸)
- `UnitBrain.Npc.cs` — NPC BT 구성 partial

**NpcType별 BT:**
| NpcType | BT |
|---|---|
| Villager | Flee → Wander → Idle |
| Guard | Detect+Fight → Patrol → Idle |
| ShopMerchant | Idle (고정) |
| EventMerchant | WalkIn → Stay(StayDuration) → WalkOut → Despawn |

**`UnitBlackboard` 추가 필드:** `HomePosition`, `NpcDestination`, `NpcTimer`, `NpcPhase`

### 4. UnitBrain partial class 분리

- `UnitBrain.cs` — `partial` 선언. `BuildTree()` 분기만 유지
- `UnitBrain.Wanderer.cs` — `BuildWandererTree()`
- `UnitBrain.Minion.cs` — `BuildMinionTree()`
- `UnitBrain.Enemy.cs` — `BuildEnemyTree()`
- `UnitBrain.Npc.cs` — `BuildNpcTree()` + NpcType별 서브 메서드

새 유닛 타입 추가 시 파일 하나만 추가하면 됨. `UnitBrain.cs` 본체는 건드리지 않음.

### 5. PawnMover 추가 API

- `IsMoving()` — `_hasTarget` 반환. NPC BT 노드에서 도착 여부 판단에 사용
- `SetDestination(Vector3)` — `SetMoveTarget` 별칭

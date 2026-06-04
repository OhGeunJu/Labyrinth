# CLAUDE.md

Guidance for Claude Code in this repository.

**상세 문서:** `docs/units.md` | `docs/combat.md` | `docs/jobs.md` | `docs/world.md` | `docs/ui.md` | `docs/data.md`

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Dungeon Core 시뮬레이터. 두 유닛 타입: **방랑자(Wanderer)** (직접 제어), **하수인(Minion)** (자동 AI, 던전 코어 소환).

## Development Environment

- Unity 2022+, New Input System (`UnityEngine.InputSystem`), TextMeshPro
- CLI 빌드 없음 — Unity Editor Play Mode로 테스트

## 폴더 구조 (`Assets/1 Script/`)

```
1 Script/
├── BuildModeManager.cs       ← 빌드 모드 토글
├── Combat/                   ← DamageInfo, DamageCategory, DamageElement, PhysicalType
├── Data/                     ← DataManager (static), DataSchemas
├── GameTimeManager.cs        ← 게임 내 시간 (낮/밤, 일수)
├── Event/                    ← GameEventType, GameEventData, EventManager, Merchant, MerchantData
├── Input/                    ← CameraController, MoveController, SelectionManager, SelectionBox, SelectionIndicator
├── Item/                     ← ItemData, FoodData, SeedData, WeaponData, ArmorData, Inventory, DroppedItem
├── Job/                      ← JobManager, 모든 Job 클래스, FarmPlot, WaterSource, CraftingTable, Bed, PatrolZone
├── Map/                      ← MapManager, TileData, GridRenderer, ResourceNode
├── UI/                       ← 모든 UI 패널/컴포넌트
├── core/                     ← DungeonCore, SubCore
└── unit/                     ← UnitBrain (partial), UnitData, UnitStats, UnitBlackboard,
                                 BodyPartSystem, CombatHandler, Equipment, PawnMover, StatusEffectSystem
    ├── UnitBrain.cs          ← partial: BuildTree() 분기만 유지
    ├── UnitBrain.Wanderer.cs ← BuildWandererTree()
    ├── UnitBrain.Minion.cs   ← BuildMinionTree()
    ├── UnitBrain.Enemy.cs    ← BuildEnemyTree()
    └── UnitBrain.Npc.cs      ← BuildNpcTree() + NpcType별 서브 메서드
```

## Architecture — Quick Reference

### Unit Types & BT 순서

| Type | Data Class | BT 순서 |
|---|---|---|
| **방랑자** | `UnitData` | Draft → Fight → Rest → Eat → Work → Idle |
| **하수인** | `MinionData` | Detect+Fight → Rest → Eat → Patrol → Work → Idle |
| **Enemy** | `UnitData` | Detect → Fight → Idle |
| **NPC** | `NpcData` | 타입별 (Villager: Flee→Wander / Guard: Fight→Patrol / EventMerchant: WalkIn→Stay→WalkOut) |

- `MoveController` — 방랑자만 우클릭 명령 (`!(brain.Data is MinionData)`)
- `MinionData.AllowedJobs` — 하수인 작업 필터 (`RequestJobFiltered`)
- `MinionData.CanProvideWater` — 우물 없이 직접 물 공급

### Key Singletons

| Class | 주요 API |
|---|---|
| `MapManager` | `TileToWorld` / `WorldToTile` / `OnTileChanged` static event / `GetForPosition(Vector3)` |
| `JobManager` | `RegisterJob` / `RequestJob(unit)` / `RequestJobFiltered(unit, categories)` |
| `PriorityManager` | `Get/Set/Increase/Decrease/IsEnabled(JobCategory)` |
| `DungeonCore` | `Summon(MinionData, pos)` / `IsWithinAnyRangeWorld(Vector3)` / `RegisterSubCore` |
| `EventManager` | `checkInterval`(기본 300초) 주기 랜덤 이벤트 발동. `OnEventTriggered` static event |
| `GameTimeManager` | 낮30분+밤30분=1일. `NormalizedTime`(0-1) / `IsDay` / `CurrentDay` / `OnDayChanged` / `OnTimeChanged(float)` |
| `HierarchicalPathfinder` | 씬에 배치 필요. `FindPath(from, to)` |
| `ResearchManager` | `IsUnlocked(recipe)` / `CanResearch(data)` / `Complete(data)` |
| `DataManager` | static. `GetItem` / `GetRecipe` / `GetRecipesBySkill(CraftSkill)` / `AllItems` / `AllRecipes` |

### Storage API (자주 사용)

```csharp
Storage.GetTotal(ResourceType)           // ResourceType 기반 집계
Storage.GetTotalByItem(ItemData)         // ItemData 기반 집계 (제작 재료용)
Storage.Consume(ResourceType, amount)
Storage.ConsumeByItem(ItemData, amount)
Storage.FindNearest(position, item)      // 해당 아이템을 받는 가장 가까운 창고
storage.CanAccept(ItemData)              // 필터 통과 여부
storage.TryAdd(item, amount)             // 필터 통과 시에만 입고
```

### Job Categories & Score

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research

Job score = `globalPriority × 100 + job.Priority`. Priority 0 = 비활성.

### Damage Quick Ref

```
DamageCategory: Physical / Elemental / Dark / Holy
PhysicalType:   Slash / Pierce / Blunt          (Physical일 때)
DamageElement:  None / Fire / Ice / Lightning / Poison  (비Physical일 때)
```

### ResourceType

| 분류 | 값 |
|---|---|
| 석재 | Stone, CutStone |
| 원석 | CopperOre, TinOre, IronOre, Coal, SilverOre, GoldOre, MagicStone |
| 보석 | Ruby(화염), Sapphire(냉기), Topaz(번개), Emerald(독), Etherstone(신성), Voidstone(암흑) |
| 주괴 | Bronze, Steel, SilverIngot, GoldIngot |
| 목재 | Wood, Lumber |
| 화폐 | Gold |

`ResourceTypeExt.DisplayName()` — 한글 표시명 반환

### CraftSkill 타입

Labor, WeaponCrafting, ArmorCrafting, Cooking, Alchemy, Research, Smelting, Stonecutting, Woodworking, Gemcutting

### ItemQuality

Common(일반) / Uncommon(고급) / Rare(희귀) / Unique(유니크) / Epic(에픽) / Legendary(전설)

`StatMultiplier()`: 1.0 / 1.2 / 1.5 / 2.0 / 2.5 / 3.0. 같은 등급끼리만 스택.
`ItemInstance.DisplayName` — `[희귀] 철검` 형태 표시명

### 상인 시스템

**골드**: `ResourceItemData` SO (`ResourceType.Gold`, Stack=9999). Storage에 보관, `GetTotalByItem` / `ConsumeByItem`으로 집계.

**ItemData.BaseValue**: 아이템 기준 가격(G). 인벤토리 툴팁에 "가치" 표시 + 상점 판매가 계산 기준.

**MerchantData** (SO, `Create → DungeonColony → MerchantData`):
- `MerchantType`: General / Weapon / Armor
- `DailySlotCount`: 하루 진열 종류 수
- `ItemPool`: 후보 목록 (`Item` + `BasePrice`(0이면 Item.BaseValue) + `MinStock` + `MaxStock` + `Weight` + `Quality`)
- `GoldItem`: Gold ItemData 연결
- `PriceMultiplier`: 마을 전체 구매가 배율 (기본 1.0)
- `ItemOverrides`: 아이템별 개별 배율 (특정 아이템만 비싸게/싸게)
- `SellMultiplier`: 플레이어→상인 판매가 배율 (기본 0.5)
- `CalcPrice(entry)` — 구매가 계산: `BasePrice × GetItemMultiplier(item)`
- `CalcSellPrice(item, quality)` — 판매가 계산: `BaseValue × quality배율 × SellMultiplier`

**Merchant 컴포넌트** (`Assets/1_Script/Event/Merchant.cs`):
- `TryBuy(entry)` — Storage 골드 차감 → 가장 가까운 창고에 아이템 입고
- `TrySell(inst, inventory)` — 인벤토리에서 아이템 1개 제거 → Storage에 골드 추가
- `GetSellPrice(inst)` — 판매가 조회
- `RefreshStock()` — 매일 자정 자동 호출, 가중치 추첨으로 재고 생성

**NPC 연동 흐름** (`NpcType.ShopMerchant`):
1. `NpcData.MerchantData` 필드에 MerchantData SO 연결
2. `UnitBrain` 초기화 시 `Merchant` 컴포넌트 자동 추가 + Data 세팅
3. 매일 `GameTimeManager.OnDayChanged` 에 맞춰 `RefreshStock()` 자동 호출

**이벤트 상인** (`NpcType.EventMerchant`): 별도. `NpcData.StayDuration` 후 소멸, `MerchantData` 미사용.

**UI 구조:**
- `MerchantUI` ← `Merchant.OnSelected` 이벤트로 오픈. 상인 재고(구매)만 담당.
  - 아이템 클릭/더블클릭 → 구매. 단건 구매만 지원.
- `PawnInventoryPanel` — 상인 열림 시 판매 모드 전환. 하단 액션 바에 [판매] 버튼 표시.
  - 아이템 단클릭 → 선택 + 버튼 활성, 더블클릭 → 즉시 판매, 드래그 → MerchantUI 위에 드롭 판매
- `ItemInfoPanel` — 아이템 hover 시 공용 툴팁 (등급 컬러, 스탯, 설명, 상점가/판매가/가치)

**에디터 스크립트**: `DungeonColony → Create Merchant UI` 실행
→ `ItemInfoPanel` + `MerchantUI` 씬 생성 + `MerchantStockEntry.prefab` / `SellItemEntry.prefab` 저장

**마을별 가격 설정 예시**:
```
해안마을 MerchantData: PriceMultiplier=1.0, ItemOverrides: [생선 0.5배, 빵 1.8배]
내륙마을 MerchantData: PriceMultiplier=1.0, ItemOverrides: [생선 2.0배, 밀 0.6배]
```

### 이벤트 시스템

**GameEventData** (SO): `EventName`, `Description`, `Icon`, `Weight`, `GameEventType`
- `Raid`: `EnemyPrefabs[]`, `RaidCount` → 맵 엣지에서 던전 코어 방향으로 이동
- `Merchant`: `MerchantPrefab` → 맵 엣지에 NPC 스폰 (EventMerchant BT로 동작)
- `Reinforcement`: `ReinforcementPrefabs[]`, `ReinforcementCount` → 아군 지원군 스폰
- `Discovery`: `ResourceNodePrefab`, `DiscoveryMinDist/MaxDist` → 던전 코어 주변 랜덤 위치에 자원 노드 생성

**EventNotificationUI**: `EventManager.OnEventTriggered` 구독 → 이벤트 발동 시 화면 상단 팝업 자동 표시. `displayDuration`(기본 5초) 후 자동 닫힘.

### BuildMode

- `placeableBuildings: BuildingData[]` 단일 배열. `UsesBlueprint=true` → 2단계 건설, `false` → 즉시 배치
- 설계 원칙: `BuildingData`는 배치 방법만 담당. 각 컴포넌트(Storage, SubCore, FarmPlot 등)가 동작 데이터를 프리팹 SerializeField로 직접 보유
- `OnActiveChanged` — 건설 모드 토글 시 다른 UI 자동 닫힘

### Input & Camera

| Script | 트리거 | 동작 |
|---|---|---|
| `SelectionManager` | 좌클릭/드래그 | Player 팩션 유닛 선택 |
| `MoveController` | 우클릭 | 방랑자 이동/타겟 설정 |
| `PawnInventoryPanel` | I키 | 인벤토리/장비 패널 |
| `UnitStatsUI` | C키 | 스탯 패널 토글 |
| `PriorityPanel` | P키 | 우선순위 패널 |
| `BuildModeUI` | B키 | 건설 모드 |
| `CameraController` | WASD/중클/스크롤 | 카메라 |

## Stat Grade System (`StatGrade.cs`, `CombatXp.cs`)

모든 스탯 0–130 범위, 5단위 27등급 (F- ~ SSS+). 상세 `docs/units.md` / `docs/combat.md`.

- `StatGrade` enum (27단계) + `StatName` enum (51개) — 통합 식별자
- `UnitData.DefaultMinGrade/MaxGrade` + `StatGrades` 오버라이드 리스트로 등급 정의 (Vector2 range 제거됨)
- `UnitStats.GrowStat(name, amount)` — 모든 성장 진입점. `IsLocked=true` (미니언) 시 차단
- `UnitStats.OnAnyGradeUp` static event — 등급 승급 감지 (UI 알림용)
- 켄시식 XP: `CombatXp.Calculate(my, opp, enemyCount)` = ChallengeMult × Sol × OutnumberedMult
- 전투 외 노동(`BTLeafNodes`, `CraftJob`, `ResearchJob`, `PawnMover.Athletics`)도 SOL/ChallengeMult 적용
- `ResourceNodeData.RequiredSkill` — 노동 도전 대상 레벨

## 알려진 이슈

- **한글 폰트**: `AddComponent<TextMeshProUGUI>` 시 기본 폰트(LiberationSans SDF) 한글 미지원. `Project Settings → TMP → Default Font Asset` 설정 권장.
- **핵 이동 시 빌드존 리셋**: 그리드 데이터 초기화, 건물 시각 사라짐 (GameObject는 유지). 향후 정책 결정 필요.
- **CraftJob 재료 손실**: `OnStart`에서 즉시 소비 — 유닛 사망 시 재료 손실 (의도된 단순 동작).
- **Blueprint 반투명 머티리얼**: URP에서 코드 생성 투명 머티리얼이 불투명 렌더될 수 있음. 에디터에서 직접 제작 후 연결 권장.
- **DataManager SO 미수집**: RecipeData 에셋이 `Assets/Resources/Data/Recipes/`에 없으면 미인식. 해당 폴더로 이동하거나 JSON으로 전환 필요.

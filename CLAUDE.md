# CLAUDE.md

Guidance for Claude Code in this repository.

**상세 문서:** `docs/units.md` | `docs/combat.md` | `docs/jobs.md` | `docs/world.md` | `docs/ui.md` | `docs/data.md`

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Dungeon Core 시뮬레이터. 두 유닛 타입: **방랑자(Wanderer)** (직접 제어), **하수인(Minion)** (자동 AI, 던전 코어 소환).

## Development Environment

- Unity 2022+, New Input System (`UnityEngine.InputSystem`), TextMeshPro
- CLI 빌드 없음 — Unity Editor Play Mode로 테스트

## 폴더 구조 (`Assets/1_Script/`)

```
1_Script/
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
| **방랑자** | `UnitData` | Draft → **직접 명령** → Fight → Rest → Eat → Work → Idle |
| **하수인** | `MinionData` | Detect+Fight → Rest → Eat → Patrol → Work → Idle |
| **Enemy** | `UnitData` | Detect → Fight → Idle |
| **NPC** | `NpcData` | 타입별 (Villager: Flee→Wander / Guard: Fight→Patrol / EventMerchant: WalkIn→Stay→WalkOut) |

### 팩션 (세력 관계)

`FactionData` (SO) — 세력 1개. `PlayerControlled` / `DefaultStance` / `Relations[{Other, Stance}]`. **관계는 방향별**(`StanceTo`): A→B 적대여도 B→A 중립 가능. `Faction` 컴포넌트가 SO를 들고 `IsPlayer`/`IsHostileTo`/`IsAllyOf` 제공, **SO 미연결 시 레거시 `FactionType{Player,Enemy}` 폴백**(기존 씬 무영향). 반격은 관계와 무관 — `UnitBlackboard.LastAttacker`(15초). AI 역할은 `UnitBrain.UsesEnemyAi`(NPC/하수인 아님 && !IsPlayer). **적을 만났을 때의 반응 + 유휴 배회는 `AiProfileData`(SO)** — 유닛 프리팹의 `UnitBrain.aiProfile`에 연결(새 컴포넌트 부착 불필요, 프로필을 여러 프리팹이 공유). 세 축 분리: 종족(`UnitData`)=스탯 / 세력(`FactionData`)=누가 적인가 / 역할(`AiProfileData`)=어떻게 반응하는가. 비우면 타입별 기본값(일반·경비=교전+배회 / NPC 주민=도주+배회 / 상점상인=도주+제자리). 트리 조립은 `UnitBrain.AddCombatBranch` / `AddWanderBranch` 공용. 스타터 에셋은 `Assets/5_Data/AiProfile/`(4종) · `Assets/5_Data/Faction/`(4종)에 생성돼 있다. 상세 `docs/units.md`.

- `MoveController` — 방랑자만 우클릭 명령 (`!(brain.Data is MinionData)`)
- `MinionData.AllowedJobs` — 하수인 작업 설정의 **초기값**(이후 조정은 `WorkSettings`로 개체별)
- `MinionData.CanProvideWater` — 우물 없이 직접 물 공급

### Key Singletons

| Class | 주요 API |
|---|---|
| `MapManager` | 점유=`HashSet<Vector2Int>` 희소 저장(절대좌표, 핵 이동에도 유지). `TileToWorld`(Y=0, 길찾기용) / `TileToGround`(표면 스냅, 배치/시각화용) / `SurfaceHeight(tile)`(바닥 윗면 ∥ 지형 높이) / `TryGetFloorSurface` / `WorldToTile` / `SampleGroundHeight(x,z)` / `IsFlatEnough(footprint)` / `IsBuildable` / `OnTileChanged` static event / `GetForPosition(Vector3)` |
| `JobManager` | `RegisterJob` / `RequestJob(unit)`(방랑자·하수인 공통 — 폰의 `WorkSettings`로 필터) |
| `DungeonCore` | `Summon(MinionData, pos)` / `IsWithinAnyRangeWorld(Vector3)`(메인+서브 union) / `RegisterSubCore` / `RelocateTo(pos)`(핵 이전, UI 진입점=`CoreRelocator`) / `WouldBeInRangeAt(corePos, worldPos)`(이전 미리보기용 영역 판정) / `OnTerritoryChanged` static event(영역 변경 → `BuildingDormancy` 재평가) |
| `EventManager` | `checkInterval`(기본 300초) 주기 랜덤 이벤트 발동. `OnEventTriggered` static event |
| `GameTimeManager` | 낮30분+밤30분=1일. `NormalizedTime`(0-1) / `IsDay` / `CurrentDay` / `OnDayChanged` / `OnTimeChanged(float)` |
| `HierarchicalPathfinder` | 씬에 배치 필요. `ComputeCoarseRoute(from,to)`(메소/L2) + `FindLocalPath(pos,target,self)`(유닛 중심 Theta* 윈도우) / `RebakeObstacleAt`. `obstacleLayer`=Wall만. **통행 판정은 타일 단위** — 반경 부풀림은 타일보다 큰 에이전트에만. 상세 `docs/world.md` |
| `ResearchManager` | `IsUnlocked(recipe)` / `CanResearch(data)` / `Complete(data)` |
| `DataManager` | static. `GetItem` / `GetRecipe` / `GetRecipesBySkill(CraftSkill)` / `AllItems` / `AllRecipes` |
| `GameModeManager` | **화면·입력**이 필드냐 월드냐의 단일 소유자. `Current` / `IsField` / `IsWorld` / `SwitchTo(GameMode)` / `Toggle()` / `OnModeChanged`. 월드 전환 = 필드 UI·입력·카메라 off + 월드 UI/루트/카메라 on. **UI 캔버스는 하나** — 캔버스가 아니라 그 **직속 자식들**을 다루고 월드 전용 묶음(`Canvas/WorldUI`)만 반대로 켠다(월드 UI를 캔버스째 끄지 않으려고). 필드 UI는 **떠날 때 열려 있던 상태를 기억했다 복원**(`ApplyFieldUi`) — 일괄로 켜면 닫아 뒀던 패널까지 따라 열린다. **정착지 루트는 안 건드린다 — 거점 시뮬은 월드맵을 보는 동안에도 계속 돈다**(2026-07-30 결정, 07-29의 "필드 통째 정지"를 되돌림). `EventManager`도 계속 돈다(원정 중 습격 → M키로 복귀해 대응, 추상 방어 판정 불필요) |
| `OutpostManager` | **채집기지**(`WorldNodeContent.Outpost`)의 단일 소유자. `Build(node)` / `At(node)` / `LoadInto(outpost, party)` / `CanAfford` / `CostSummary` / `OnChanged`. **필드맵을 만들지 않는 추상 시설** — 매일(`OnDayChanged`) 남은 매장에서 골고루 캐 기지에 쌓고, **원정대가 들러 등짐으로 실어와야** 창고에 들어간다(본거지 순간이동 없음). 건설 비용 `buildCost`(비우면 무료)는 활성 거점 창고에서 빠진다 |
| `ExpeditionFormation` | 원정 편성의 **집결** 단계. `Begin(members)` / `Cancel()` / `IsGathering` / `ArrivedCount` / `OnChanged`. 핵 앞으로 걸어와 **전원 도착 시 일괄 출발** → `WorldMapView.AddMembers` + 월드맵 전환. 도착한 폰은 `UnitBlackboard.HoldPosition`으로 붙들어 둔다(`ManualMove`는 목적지 소진 시 자동 해제돼 못 잡는다) |
| `ExpeditionParty` | 월드맵 원정대(논리 데이터). `Members` / `CurrentNode` / **`Cargo`**(등짐) / `Capacity`(멤버 근력 합 × 5 — **필드 폰 적재와 같은 식**) / `TryAdd`(실제 받은 개수 반환) / `ClearCargo`. 자원 채집물이 여기 쌓이고 거점 진입 시 창고로 비워진다 |
| `SettlementManager` | 정착지 전환형. `Active` / `SwitchTo` / `SwitchToNode` / `RegisterAt` / `ActiveBiome`. **루트 활성화의 소유자** — 한 번에 하나만 시뮬되는 건 모드가 아니라 "활성 정착지는 하나"라는 규칙 때문. 시뮬 안 되는(= 지금 안 보는 다른) 정착지는 매일 오프라인 추상 생산 |
| `WorldMap` | 추상 바둑판 월드맵. `GetTile` / `SetContent` / `RegisterSettlement` / `Neighbors` / `Distance` / `OnNodeChanged`. **매장 자원**: 칸마다 무엇이 얼마나 묻혔는지는 `WorldDeposits.At(node, tile, seed)` — 종류는 `BiomeContentData.Deposits`(바이옴), 수량은 **칸 좌표+시드 해시로 결정적**. 값은 항상 같고 **조사(`IsSurveyed`/`MarkSurveyed`)해야 보인다**(안개 아님 — 가려지는 건 세부 수치뿐). `ResourceSite` 칸은 ×2.5. **저작한다, 절차 생성하지 않는다** — `mapData`(`WorldMapData` SO)를 연결하면 그대로 싣고 크기·시작 노드도 데이터가 이긴다. 없으면 빈 맵 + 경고. 칸이 갖는 건 **바이옴(콘텐츠 게이팅) · 내용물**뿐 — 높이·생김새는 저작한 지형 몫이다. 저작은 `DungeonColony → World Map Editor`(브러시 3종). **안개 없음**(2026-06-20 결정) |

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

### Job Categories & 배정 모델

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research

**두 갈래** — `JobCategoryInfo`가 단일 출처.
- **큐 작업**(`QueueOrder`: 건설 → 농사 → 제작 → 연구 → 운반): 건물/농지/작업대가 일감 등록 → 폰이 집어감. **배열 순서 = 전역 우선순위**(모든 폰 공통). `QueueOrder`에 없는 카테고리는 큐에서 배정되지 않는다.
- **직접 배정**(Mining/Logging = 자원 노드, Guard = 순찰 구역): 플레이어가 대상에 폰을 붙임(`Blackboard.AssignedNode`/`AssignedPatrolZone`). 큐를 타지 않으므로 on/off 대상이 아님. (`MineJob`은 현재 미사용 코드)

Job score = `JobCategoryInfo.Weight(category) × 100 + job.Priority`.

폰별 설정은 **숫자 우선순위가 아니라 on/off** — `WorkSettings`(`UnitBrain.Work`, 플레이어 세력 일꾼에만 자동 부착). UI는 `ColonyPanel`(P키). 상세 `docs/jobs.md`.

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

**씬 구성**: `ItemInfoPanel` + `MerchantUI`는 씬에 있고, 항목 행은 `MerchantStockEntry.prefab` / `SellItemEntry.prefab`을 복제해 채운다. 모양 수정은 인스펙터에서(생성기는 역할을 마쳐 제거됨 — `docs/editor_creators.md` "생성기의 수명").

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
- **바닥(floor) 시스템**: `BuildingData.IsFloor` → 무료 substrate(모델 A, 코어 무관). `MapManager._floored` 희소 저장(타일 → **바닥 윗면 Y**). 건물 배치 높이는 `SurfaceHeight` + `BuildModeManager.SpawnOnSurface` — 프리팹 루트 `localPosition.y`가 양수면 **저작 부양 높이**로 보고 그만큼 띄우고(서브코어 등), 아니면 `SnapOnSurface`가 렌더러 바닥면을 표면에 맞춘다(피벗 중앙 프리팹 파묻힘 방지, 올리기만 함). 즉시 배치/청사진/고스트/자유 배치 동일 규칙. 메인 핵은 `DungeonCore._hoverOffset`(Start에서 캡처)로 이전 후에도 부양 유지. 건물의 "바닥 위에만" 게이트 = `FloorRequired`(= `requireFloor` 플래그 ∥ 바닥 존재) — 그리드/자유 모드 공통, 던전 씬은 플래그를 켤 것(필드 씬 기본 false·무영향). 공허 조준=레이캐스트 실패 시 y=0 평면 폴백, 바닥은 좌클릭 유지 드래그 페인트 + 그리드 강제. 에셋: `Floor.prefab` + `Floor.asset`(placeable) / 씬의 `[DungeonStarterFloor]`(코어바닥 20×20 저작, `requireFloor` 켜짐). 공허 이동 차단 3중: 길찾기(L1/메소)가 바닥 밖 셀 unwalkable + `PawnMover` 낭떠러지 가드(`ledgeLookahead`) + `fallRecoveryY` 낙하 복구. AI 목적지 선정은 `MapManager.IsStandable` 검사.
- **핵 영역(크립) / 건물 휴면**: 점유는 절대좌표 희소 저장이라 핵 이동에도 유지. 영역(핵 union) 밖 건물은 `BuildingDormancy`가 휴면(작동 정지+회색 틴트, 철거 X), 복구 시 재가동. **휴면 = 전면 사용 불가**: 창고 집계 제외(`Storage.IsActive`), 침대·농지·작업대·연구대·우물 컴포넌트 `enabled=false`, 클릭해도 패널 안 열림(`SelectionManager` 게이트), 매달린 일감은 `JobBase.IsSiteUsable`로 배정 보류(취소 아님 → 복구 시 재개). 외부 판정은 `BuildingDormancy.IsDormantAt(Component)`. 런타임 배치 시 `BuildingDormancy.Ensure`로 자동 부착(씬 사전 배치 건물은 수동 부착 필요). **핵 이전**: `DungeonCoreUI`의 [핵 이전] 버튼 → `CoreRelocator`(좌클릭 확정 / 우클릭·Esc 취소, 타일 유효성 + 새 영역 원 + 휴면·재가동 예정 수 미리보기) → `RelocateTo`. 설계: `docs/design_core_creep.md` (step 1~5 완료)

### Input & Camera

| Script | 트리거 | 동작 |
|---|---|---|
| `SelectionManager` | 좌클릭/드래그 | Player 팩션 유닛 선택. 적/NPC 클릭 = `OnPawnInspected`(정보만, 명령 대상 아님) |
| `MoveController` | 우클릭 | 방랑자 이동/타겟 설정 |
| `PawnInventoryPanel` | I키 | 인벤토리/장비 패널 |
| `UnitStatsUI` | C키 | 스탯 패널 토글 |
| `ColonyPanel` | P키 | 무리 관리(작업 on/off + 전투 성향, 폰 전체) |
| `ExpeditionPanel` | G키 | 원정대 편성 — 폰 체크 + 적재량 요약. [출발] 시 **바로 사라지지 않고** 핵 앞으로 집결(`ExpeditionFormation`), 전원 도착 후 함께 떠남 |
| `WorldMenuPanel` | 월드맵에서 칸에 **도착**(빈 야생 포함) | 마운트앤블레이드식 도착 메뉴(화면 왼쪽 고정, 좌 그림 / 우 선택지). **선택지 구성은 `WorldMapView.OpenMenu`가 정한다** — `탐험한다`(매장 조사) 동작, `여기에 전초기지를 세운다`·마을·소굴은 "준비 중" |
| `BuildModeUI` | B키 | 건설 모드 |
| `CameraController` | WASD/중클/스크롤 | 카메라 |
| `CameraController` (월드) | WASD/중클/스크롤 | 월드맵 카메라 — 필드와 같은 컴포넌트. `GameModeManager`가 **월드 카메라에 붙은 것은 월드 측**으로 보고 필드와 반대로 켠다 |
| `WorldMapView` | M키 | 필드↔월드 **모드 전환**(`GameModeManager`에 위임 — 반대편은 통째로 꺼진다). **원정대는 여럿**. 나가 있는 팀마다 토큰이 서고, 없으면 토큰도 없다. **선택 조작은 필드맵과 동일** — 좌클릭=단일 선택 / 드래그=사각 다중 선택 / 빈 곳=해제. **우클릭=선택한 팀만 이동**. **내용물 있는 칸에 도착하면 `WorldMenuPanel` 자동 오픈**(M&B식 — 멀리서 클릭해선 안 열린다), Enter=한 팀만 선택했을 때 그 메뉴 재진입. 토큰에 콜라이더가 있어 `RaycastAll` 거리순으로 "토큰인 첫 충돌"(선택) / "토큰 아닌 첫 충돌"(이동)을 가른다. 월드에서 **자유 실시간 이동**(우클릭한 지점으로 직진, 지형 표면을 탄다 — 격자 스냅 없음). 지형은 `WorldMapTerrain`이 저작 격자를 재료로 **맵 전체를 메시 한 장**으로 짓는다(바이옴 성격을 이웃끼리 보간 → 칸 경계 없음). 컴포넌트를 빼면 직접 만든 지형을 쓴다. `WorldMapGrid`는 좌표 변환 + 격자선 + 오브젝트 배치 |

## Stat Grade System (`StatGrade.cs`, `CombatXp.cs`)

모든 스탯 0–130 범위, 5단위 27등급 (F- ~ SSS+). 상세 `docs/units.md` / `docs/combat.md`.

- `StatGrade` enum (27단계) + `StatName` enum (51개) — 통합 식별자
- `UnitData.DefaultMinGrade/MaxGrade` + `StatGrades` 오버라이드 리스트로 등급 정의 (Vector2 range 제거됨)
- `UnitStats.GrowStat(name, amount)` — 모든 성장 진입점. `IsLocked=true` (미니언) 시 차단
- `UnitData.StatGrowthRates` — 종족별 **특정 스탯만** 성장 속도 배율(목록에 없으면 1.0). `GrowStat`에서 `amount × GetGrowthRate(stat)` 적용
- `UnitStats.OnAnyGradeUp` static event — 등급 승급 감지 (UI 알림용)
- 켄시식 XP: `CombatXp.Calculate(my, opp, enemyCount)` = ChallengeMult × Sol × OutnumberedMult
- 전투 외 노동(`BTLeafNodes`, `CraftJob`, `ResearchJob`, `PawnMover.Athletics`)도 SOL/ChallengeMult 적용
- `ResourceNodeData.RequiredSkill` — 노동 도전 대상 레벨

## 알려진 이슈

- **BT 노드에서 `Time.deltaTime` 금지**: BT는 `tickInterval`(0.1초)마다 돌므로 프레임 델타를 누적하면 시간이 6배 이상 느려진다. `UnitBlackboard.TickDelta`를 쓸 것 (2026-07-27 전수 교체).

- **한글 폰트**: `AddComponent<TextMeshProUGUI>` 시 기본 폰트(LiberationSans SDF) 한글 미지원(□). `UIHelper.MakeLabel/MakeButton`은 `ApplyKoreanFont`로 TMP 기본 폰트 → 씬의 기존 TMP 폰트를 자동 상속하지만, **직접 `AddComponent`할 땐 수동 지정 필요**. 근본 해결은 `Project Settings → TextMesh Pro → Default Font Asset`을 `Assets/NanumGothic SDF.asset`으로 설정(이 폰트는 Resources 밖이라 `Resources.Load` 불가).
- **핵 이동 시 빌드존 리셋**: 해소됨. 점유는 `HashSet` 절대좌표 희소 저장이라 이동에도 유지되고, 영역 밖 건물은 `BuildingDormancy`가 휴면 처리(`docs/design_core_creep.md` step 1~5 완료).
- **Blueprint 반투명 머티리얼**: URP에서 코드 생성 투명 머티리얼(`BuildingBlueprint.CreateBlueprintMaterial`)이 불투명 렌더될 수 있음. 에디터에서 직접 제작 후 연결 권장.
- **DataManager**: `Resources.LoadAll<RecipeData>("Data/Recipes")` + JSON 스키마(`DataSchemas`) 경로 둘 다 지원. SO는 `Assets/Resources/Data/{Items,Recipes,Research}/`에 있어야 인식.

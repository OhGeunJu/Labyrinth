# CLAUDE.md

Guidance for Claude Code in this repository.

**상세 문서:** `docs/units.md` | `docs/combat.md` | `docs/jobs.md` | `docs/world.md` | `docs/ui.md` | `docs/data.md`

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Dungeon Core 시뮬레이터. 두 유닛 타입: **방랑자(Wanderer)** (직접 제어), **하수인(Minion)** (자동 AI, 던전 코어 소환).

## Development Environment

- Unity 2022+, New Input System (`UnityEngine.InputSystem`), TextMeshPro
- CLI 빌드 없음 — 동작 확인은 Unity Editor Play Mode
- **컴파일은 에디터 없이도 볼 수 있다**: `dotnet msbuild Assembly-CSharp.csproj -t:Build`
  (산출물은 `Temp/bin/Debug`, gitignore됨). Play Mode에 들어가기 전에 문법·시그니처 사고를
  먼저 거를 것 — "CLI 빌드 없음"은 게임을 못 돌린다는 뜻이지 컴파일도 못 본다는 뜻이 아니다.

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

**축은 넷이고 서로를 모른다.** 조합은 **프리팹이 한다** — 인간 상인 = (Human + 상인 역할), 엘프 상인 = (Elf + **같은** 상인 역할).

| 축 | 데이터 | 답하는 것 | 에셋 |
|---|---|---|---|
| 종족 | `UnitData` | 스탯·이름 풀 | `Resources/Data/Units/` |
| 세력 | `FactionData` | 누가 적인가 | `5_Data/Faction/`(4종) |
| AI 성향 | `AiProfileData` | 적을 만나면 어떻게 하는가 + 유휴 배회 | `5_Data/AiProfile/`(4종) |
| 역할 | `NpcData` | 무엇을 하는 사람인가(주민·경비·상인) | `Resources/Data/Npc/` |

- `FactionData` — `PlayerControlled` / `DefaultStance` / `Relations[{Other, Stance}]`. **관계는 방향별**(`StanceTo`): A→B 적대여도 B→A 중립 가능. `Faction` 컴포넌트가 SO를 들고 `IsPlayer`/`IsHostileTo`/`IsAllyOf` 제공. **SO는 반드시 연결할 것** — 비우면 아무와도 관계가 없어 판정이 전부 중립으로 떨어진다(`Awake`가 경고). 반격은 관계와 무관(`UnitBlackboard.LastAttacker`, 15초).
- **AI 역할은 `UnitBrain.UsesEnemyAi`(NPC 아님 && !IsPlayer)** — 에셋 클래스는 묻지 않는다. **`is MinionData`는 "하수인 종족"이지 "내 편"이 아니다**(고블린은 소환될 수도 야생에 살 수도 있다). 소속을 물어야 하는 곳(`DungeonCore` 소환 한도, `UnitHPUI` 라벨, `UnitStats.IsLocked`)은 **세력을 같이 볼 것**.
- `aiProfile`을 비우면 타입별 기본값(일반·경비=교전+배회 / NPC 주민=도주+배회 / 상점상인=도주+제자리). 트리 조립은 `AddCombatBranch`/`AddWanderBranch` 공용. 상세 `docs/units.md`.

- `MoveController` — 방랑자만 우클릭 명령 (`!(brain.Data is MinionData)`)
- `MinionData.AllowedJobs` — 하수인 작업 설정의 **초기값**(이후 조정은 `WorkSettings`로 개체별). **큐 작업만 뜻이 있다** — 채굴·벌목·경비는 직접 배정이라 여기 적어도 켜고 꺼지지 않는다. 큐 작업이 하나도 없는 목록은 빈 목록과 같게(전부 허용) 보고 경고한다
- `MinionData.CanProvideWater` — 우물 없이 직접 물 공급

### Key Singletons

| Class | 주요 API |
|---|---|
| `MapManager` | 점유=`HashSet<Vector2Int>` 희소 저장(절대좌표, 핵 이동에도 유지). `TileToWorld`(Y=0, 길찾기용) / `TileToGround`(표면 스냅, 배치/시각화용) / `SurfaceHeight(tile)`(바닥 윗면 ∥ 지형 높이) / `TryGetFloorSurface` / `WorldToTile` / `SampleGroundHeight(x,z)` / `IsFlatEnough(footprint)` / `IsBuildable` / `OnTileChanged` static event / `GetForPosition(Vector3)` |
| `JobManager` | `RegisterJob` / `RequestJob(unit)`(방랑자·하수인 공통 — 폰의 `WorkSettings`로 필터) |
| `DungeonCore` | `Summon(MinionData, pos)` / **`Minions`**(소환 목록 — 씬의 `minionEntries`가 비면 `DataManager.AllMinions`, 채우면 그게 이기는 큐레이션 오버라이드. 프리팹은 `MinionData.Prefab`, 단 **씬 배열이 이긴다** — 에셋은 씬 오브젝트를 참조할 수 없어서) / `IsWithinAnyRangeWorld(Vector3)`(메인+서브 union) / `RegisterSubCore` / `RelocateTo(pos)`(핵 이전, UI 진입점=`CoreRelocator`) / `WouldBeInRangeAt(corePos, worldPos)`(이전 미리보기용 영역 판정) / `OnTerritoryChanged` static event(영역 변경 → `BuildingDormancy` 재평가) |
| `EventManager` | `checkInterval`(기본 300초) 주기 랜덤 이벤트 발동. `OnEventTriggered` static event |
| `GameTimeManager` | **낮 20분 + 밤 10분 = 1일**(`dayDuration` 1800초 × `dayFraction` 2/3). 밤이 짧은 건 폰이 일하는 시간이 낮이라서다. **모든 지속시간 데이터의 기준 축** — 하루 길이를 바꾸면 허기·작물·연구·제작·채굴을 같이 옮겨야 한다(`docs/data.md` "시간 축"). 시계는 선형이라 낮 06:00~22:00. `NormalizedTime`(0-1) / `IsDay` / `CurrentDay` / `Speed` / `SetSpeed` / `OnDayChanged` / `OnTimeChanged(float)` |
| `HierarchicalPathfinder` | 씬에 배치 필요. `ComputeCoarseRoute(from,to)`(메소/L2) + `FindLocalPath(pos,target,self)`(유닛 중심 Theta* 윈도우) / `RebakeObstacleAt`. `obstacleLayer`=Wall만. **통행 판정은 타일 단위** — 반경 부풀림은 타일보다 큰 에이전트에만. **코스 라우트는 도착점이 아니라 복도 안내다** — 메소(2m)·L2(100m) **셀 중심**의 나열이라 마지막 점이 목적지와 어긋난다. `PawnMover.SelectSteeringTarget`이 **마지막 구간에서 `_finalGoal`을 직접 겨누지 않으면** 폰이 셀 중심에 서서 멈추고 재계획해도 같은 답이 나와 **2~3m 앞에서 영영 맴돈다**. 마지막 칸은 L1 정밀 경로 몫. 상세 `docs/world.md` |
| `ResearchManager` | `IsUnlocked(recipe)` / `GetRequiredResearch(recipe)` / `CanResearch(data)` / `Complete(data)`. 진행률은 `ResearchTable.ActiveResearch` / `ActiveProgress`(폰이 연구대에 붙어 타이머가 도는 동안만 — 큐 예약은 안 잡힘) / `PendingResearch`(폰이 안 왔어도 맡은 것 — UI가 버튼/게이지를 가르는 기준). **한 연구대는 한 번에 하나만 맡는다** — `StartResearch(data)`가 하던 것을 접고 갈아탄다(진행도는 남아 다시 고르면 이어서 한다). UI의 [연구] 버튼과 진행 게이지는 **같은 칸을 번갈아 쓴다**(`ResearchTableUI.PrepareSlot`이 런타임에 게이지를 버튼 자리로 옮긴다 — 씬 수정 불필요). **잠금은 양방향 어느 쪽으로 적어도 걸린다** — `ResearchData.UnlockedRecipes`(큐레이션, 역인덱스를 Awake에서 생성) ∥ `RecipeData.RequiredResearch`(개별 지정). 둘 다 비면 처음부터 열린 레시피 |
| `DataManager` | static. `GetItem` / `GetRecipe` / `GetRecipesBySkill(CraftSkill)` / `GetBuilding(name)` / `GetUnit(id)` / `AllItems` / `AllRecipes` / `AllResearch` / **`AllBuildings`**(Category→SortOrder→이름 순) / **`AllUnits`**(`Resources/Data/Units/` 스캔 — `MinionData`·`NpcData` 포함) / **`AllMinions`**(SortOrder→이름 순, 소환 메뉴 순서) |
| `GameModeManager` | **화면·입력**이 필드냐 월드냐의 단일 소유자. `Current` / `IsField` / `IsWorld` / `SwitchTo(GameMode)` / `Toggle()` / `OnModeChanged`. 월드 전환 = 필드 UI·입력·카메라 off + 월드 UI/루트/카메라 on. **UI 캔버스는 하나** — 캔버스가 아니라 그 **직속 자식들**을 다루고 월드 전용 묶음(`Canvas/WorldUI`)만 반대로 켠다(월드 UI를 캔버스째 끄지 않으려고). 필드 UI는 **떠날 때 열려 있던 상태를 기억했다 복원**(`ApplyFieldUi`) — 일괄로 켜면 닫아 뒀던 패널까지 따라 열린다. **정착지 루트는 안 건드린다 — 거점 시뮬은 월드맵을 보는 동안에도 계속 돈다**(2026-07-30 결정, 07-29의 "필드 통째 정지"를 되돌림). `EventManager`도 계속 돈다(원정 중 습격 → M키로 복귀해 대응, 추상 방어 판정 불필요) |
| `OutpostManager` | **채집기지**(`WorldNodeContent.Outpost`)의 단일 소유자. `Build(node)` / `At(node)` / `LoadInto(outpost, party)` / `CanAfford` / `CostSummary` / `OnChanged`. **필드맵을 만들지 않는 추상 시설** — 매일(`OnDayChanged`) 남은 매장에서 골고루 캐 기지에 쌓고, **원정대가 들러 등짐으로 실어와야** 창고에 들어간다(본거지 순간이동 없음). 건설 비용 `buildCost`(비우면 무료)는 활성 거점 창고에서 빠진다 |
| `ExpeditionFormation` | 원정 편성의 **집결** 단계. `Begin(members)` / `Cancel()` / `IsGathering` / `ArrivedCount` / `OnChanged`. 핵의 `WorkSpot`을 중심으로 한 **반지름 고정 원**에 벌려 서고(인원이 늘면 원이 커지는 게 아니라 촘촘해진다 — 집결 범위가 인원마다 달라지면 "저기 모인다"를 예측할 수 없다), 자리는 설 수 있는 칸으로 스냅한다. **도착 판정은 자리가 아니라 원이다** — 원 안에 들어오면 도착. 자리는 안내지 조건이 아니라서, 그 칸에 못 서는 폰 하나가 출발을 막으면 안 된다. 그래서 자리를 원보다 `approachInset`만큼 **안쪽**에 잡는다(판정선 위에 세우면 경계에서 아슬아슬해진다). 자리 근처 판정은 폴백으로만 남는다. **전원 도착 시 일괄 출발** → `WorldMapView.AddMembers` + 월드맵 전환. 도착한 폰은 `UnitBlackboard.HoldPosition`으로 붙들어 둔다(`ManualMove`는 목적지 소진 시 자동 해제돼 못 잡는다) |
| `ExpeditionParty` | 월드맵 원정대(논리 데이터). `Members` / `CurrentNode` / **`Cargo`**(등짐) / `Capacity`(멤버 근력 합 × 5 — **필드 폰 적재와 같은 식**) / `TryAdd`(실제 받은 개수 반환) / `ClearCargo`. 자원 채집물이 여기 쌓이고 거점 진입 시 창고로 비워진다 |
| `SettlementManager` | 정착지 전환형. `Active` / `SwitchTo` / `SwitchToNode` / `RegisterAt` / `ActiveBiome`. **루트 활성화의 소유자** — 한 번에 하나만 시뮬되는 건 모드가 아니라 "활성 정착지는 하나"라는 규칙 때문. 시뮬 안 되는(= 지금 안 보는 다른) 정착지는 매일 오프라인 추상 생산 |
| `WorldMap` | 추상 바둑판 월드맵. `GetTile` / `SetContent` / `RegisterSettlement` / `Neighbors` / `Distance` / `OnNodeChanged`. **매장 자원**: 칸마다 무엇이 얼마나 묻혔는지는 `WorldDeposits.At(node, tile, seed)` — 종류는 `BiomeContentData.Deposits`(바이옴), 수량은 **칸 좌표+시드 해시로 결정적**(같은 축으로 `TownMerchants`가 마을 상인을 고른다). 값은 항상 같고 **조사(`IsSurveyed`/`MarkSurveyed`)해야 보인다**(안개 아님 — 가려지는 건 세부 수치뿐). `ResourceSite` 칸은 ×2.5. **저작한다, 절차 생성하지 않는다** — `mapData`(`WorldMapData` SO)를 연결하면 그대로 싣고 크기·시작 노드도 데이터가 이긴다. 없으면 빈 맵 + 경고. 칸이 갖는 건 **바이옴(콘텐츠 게이팅) · 내용물**뿐 — 높이·생김새는 저작한 지형 몫이다. 저작은 `DungeonColony → World Map Editor`(브러시 3종). **안개 없음**(2026-06-20 결정) |

### 작업 자리 (`WorkSpot`) — 도착 판정은 두 갈래

**저작된 자리가 있느냐 없느냐가 두 규약을 가른다.**
- **작업대·건물** = `WorkRadius` 0.9(칸 반대각선 0.71)로 저작된 자리에 선다. 못 서면 교착이므로 **2초 동안 못 좁히면 `ReachRadius` 2.0에서라도 시작**하고, 그 판단은 **래치로 붙든다**(일을 시작하면 `ResetProgress`가 정체 타이머를 지워서, 안 붙들면 "일한다 ↔ 다시 걷는다"가 매 틱 진동. 목표 칸이 바뀌면 저절로 풀림).
- **자원 노드** = `NodeReach` 2.0으로 **둘러서서** 캔다. 저작된 자리가 없고 `MaxWorkers`만큼 붙으므로 한 칸에 모으면 서로 비켜서다 아무도 못 캔다.

일감의 목표 칸은 건물 자기 타일이 아니라 **폰이 서는 자리**다. `JobBase.SetSite(site)`가 `WorkSpot.TileFor(site)`로 한 번에 정한다 — 각 Job이 따로 계산하지 않는다. 순서: 저작된 `WorkSpot` 자식(그 칸에 설 수 있을 때) → `MapManager.FindWorkableNear(center, 2)`(대각선·반경 2까지). **던전 핵의 `WorkSpot`은 원정 집결 원의 중심**이다 — 같은 규약을 쓰므로 집결 장소가 저작 대상이 되고 건설 모드에서 표식으로 보인다(핵은 씬 오브젝트라 `Resize Buildings And Rebuild Models`가 모델과 함께 세운다). **저작은 선호지 보장이 아니다** — 나중에 막히면 자동 탐색으로 넘어간다. 표식은 **건설 모드에서만** 보여서 그 칸을 막는 배치를 눈으로 피할 수 있다. 배선 = `DungeonColony → Setup Work Spots`.

### 작업대 산출물

완성품은 **폰이 아니라 작업대에 쌓인다** — `CraftingTable.Inventory`(기본 8×3). 순서는 **산출물 → (꽉 차면) 폰 인벤 → (그것도 안 되면) 드롭**. `allowHaulOut`이 켜져 있으면 폰이 창고로 실어 간다. 운반 출처는 **`IHaulSource`**(`ResourceNode`·`CraftingTable`이 같은 `HaulJob`을 쓴다), 발행은 **`HaulDispatcher`** 공통.

**두 숫자를 헷갈리지 말 것** — `haulThreshold`는 "이만큼 쌓이면 나른다"는 발행 **조건**이고, 실어 가는 양은 **`CarryCapacity`** 가 정한다. **몫을 미리 쪼개지 않는다**: 미배정 재고를 통째로 맡긴 일감 하나를 내고, 폰이 정해지는 순간(`OnReserved`) 그 폰이 질 만큼만 남기고 **나머지를 즉시 놓아** 다음 일감이 되게 한다 — 몇 명이 붙을지 미리 맞힐 필요가 없다. 배낭에 실은 순간 맡음은 **전부** 놓는다(안 그러면 이중으로 빠진다). 주기 점검(2초)이 잔량을 다시 집는다.

연료는 **용광로만** 요구(`_requiresFuel`) — 없으면 `CraftJob.Execute`가 대기하고 UI에 게이지+보충 안내가 뜬다. `Inventory.Resize(w,h)`는 **비어 있을 때만** 통한다. 상세 `docs/jobs.md`.

### 적재 무게 — `CarryCapacity` (한도의 단일 출처)

```
한도(kg) = 10 + 근력 × 0.5      // 근력 10 → 15kg, 130 → 75kg
```

`CarryCapacity.For / Load / Free / RoomFor(brain, item) / TotalFor(members)`. 전에는 같은 것을 네 군데가 따로 계산했고 그중 둘이 3.3배 어긋나 있었다(`근력 × 5` vs `10 + 근력 × 0.5`). 근력 × 5는 130에서 650kg이라 무게가 아무것도 막지 못해 작은 쪽을 남겼다.

- **한도는 자동 운반에만 강제된다.** `HaulJob` / `GroundHaulJob`이 `RoomFor`만큼만 싣고, `CanAssignTo`로 짐이 찬 폰에겐 일감을 주지 않는다. 못 실은 잔량은 출처에 남아 다음 발행에서 새 몫이 된다.
- **플레이어가 배낭에 직접 넣는 건 막지 않는다** — 대신 넘긴 비율만큼 이동·공격 속도가 준다(`PawnMover` / `CombatHandler`의 무게 보정). 손으로 한 일을 거부하면 "왜 안 들어가지"가 되고, 자동이 한도를 무시하면 "왜 이 폰만 느리지"가 된다. `InventoryUI`가 `무게 12/15kg`을 띄우고 과적이면 붉게 물든다(액션 바 위 `CarryBar` — 씬 오브젝트, 생성기 `Create Carry Bar`).
- **짐을 지고 걸으면 근력이 자란다**(켄시식) — `PawnMover.GrowStrengthFromLoad`, 한도 대비 적재 비율로 재고 25% 미만이면 안 오른다. 근력이 커질수록 같은 짐으로는 덜 자란다.
- 원정대 등짐(`ExpeditionParty`)도 같은 한도를 쓰고 **무게로 센다**. 예전엔 kg으로 만든 수를 개수 한도로 써서 씨앗이나 철갑옷이나 한 자리였다.
- **적재를 묻는 곳은 전부 `CarryCapacity`다** — 답이 두 벌이면 반드시 어긋난다(옛 `PartyCarry`가 3.3배 어긋나 있었다).

### 재료는 `MaterialPool`로 센다 (창고만 보지 말 것)

상점 구매가 폰 배낭으로 들어가면서 **창고만 세면 손에 든 재료가 없는 것이 된다.**

```csharp
MaterialPool.Available(item, carrier)      // 창고 + 그 폰의 배낭 — 배정·소비 판정용
MaterialPool.AvailableAnywhere(item)       // 창고 + 플레이어 폰 전부 — 일감 등록 판정용
MaterialPool.Consume(item, amount, carrier)// 배낭 먼저, 그다음 창고
MaterialPool.Refund(item, amount, dropAt)  // **창고 먼저**, 못 넣은 만큼만 바닥 — 취소 환불
```

**남의 배낭은 안 센다** — B를 조작하는데 A의 짐이 줄면 설명할 수 없다. 등록 시점엔 누가 할지 모르므로 `AvailableAnywhere`로 넓게 보고, `JobBase.CanAssignTo(unit)`가 폰별로 다시 거른다(`IsReady`와 다른 축). 적용처: `SeedJob` · `CraftJob` · `ResearchJob`.

**환불은 바닥이 아니라 창고로**(`Refund`) — 취소는 사건이 아니라 **무효화**다. 바닥에 떨구면 그 짐이 다시 운반 일감이 되고, 나르는 동안 창고 집계에서 빠져 있어 같은 작업이 "재료 부족"으로 또 죽는다.

**재료를 먹는 일감에는 반드시 `IsReady` + `CanAssignTo`를 같이 둘 것.** `IsReady`가 false면 큐에서 **대기**하지만, 게이트 없이 배정되면 `OnStart`가 `Done`으로 끝내 **예약 자체가 사라진다**(눌러 둔 연구가 저절로 풀린 것처럼 보인다).

### Storage API (자주 사용)

```csharp
Storage.GetTotalByItem(ItemData)         // 집계는 아이템 기준 하나뿐 (ResourceType판은 삭제)
Storage.ConsumeByItem(ItemData, amount)
Storage.FindNearest(pos, item, amount, quality)  // **실제로 받아 줄**(필터+자리) 가장 가까운 창고
storage.HasRoomFor(item, amount, quality)        // 필터 통과 + 넣을 자리 있음 (CanAccept만으로는 부족)
Storage.FindStorageWithItem(ItemData)    // 그 아이템이 실제로 들어 있는 창고 (건설 자재 수거)
storage.CanAccept(ItemData)              // 필터 통과 여부
storage.TryAdd(item, amount)             // 필터 통과 시에만 입고
storage.AllowsEverything                 // 필터 둘 다 비었나 (= 전부 허용 정규형)
storage.AddFilter(ResourceGroup)         // 묶음 단위 허용 — 창고 패널 버튼이 다루는 것
storage.AddItemFilter(ItemData)          // 묶음과 별개로 콕 집어 허용 ("석탄 전용 창고")
```

**필터 통과 ≠ 들어갈 수 있음.** 만적인 창고도 `CanAccept`는 참이다. 운반 목적지를 고를 땐 반드시 `FindNearest`/`HasRoomFor`(자리까지 봄)를 쓸 것 — `CanAccept`만 보면 폰이 거기까지 갔다 못 넣고 짐을 떨구고, 그 짐이 다시 같은 일감이 되어 **줍고 떨구기를 무한 반복**한다. 받아 줄 곳이 없으면 **일감을 아예 만들지 않고** 출처에 쌓아 둔다. 바닥에 남은 짐은 `DroppedItem`이 5초마다 다시 시도한다(창고를 비우면 저절로 실려 간다). **단 손으로 내려놓은 것은 예외** — `DroppedItem.NoAutoHaul`(배낭에서 바닥으로 드래그한 것만 참). 일부러 놓은 것을 폰이 도로 창고에 넣으면 "내려놓기"가 곧 "창고로 보내기"가 되어 **어디에 두고 싶어도 둘 수가 없다**(짐내림을 꺼 둔 폰의 짐도 바닥을 거쳐 흘러간다 — 짐내림과 운반은 다른 축이라 끈 쪽만으로는 안 막힌다). 사건으로 떨어진 것(발견·철거 환불·작업대/수확 넘침)은 **아무도 의도하지 않은 자리**라 그대로 운반된다. 되돌리는 길은 우클릭 [창고로 옮긴다](`AllowAutoHaul`). **반대로 "가지러 갈 곳"은 `FindStorageWithItem`** — 거기에 `FindNearest`를 쓰면 가득 든 창고가 오히려 탈락한다.

**`amount`·`quality`를 빠뜨리지 말 것.** 기본값(1개·일반)으로 물으면 "한 개는 들어간다"만 확인하고 50을 지고 간다. 등급은 더 조용히 틀린다 — 스택은 **같은 등급끼리만** 합쳐지므로(`Inventory.TryAdd`), 등급을 빼먹으면 `일반 빵 30/99`가 든 만적 창고가 `희귀 빵`도 받는다고 답하고, 폰이 거기까지 갔다 못 넣어 **줍고 떨구기가 그대로 남는다**(`Inventory.CanFit`이 `TryAdd`와 같은 등급 검사를 한다). `FindNearest`는 **두 번 훑는다** — 통째로 받아 줄 곳이 먼저, 없으면 한 개라도 받는 곳. 그래서 넉넉히 물어도 일감이 사라지지 않는다.

### Job Categories & 배정 모델

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research, **Unload**(짐내림)

**두 갈래** — `JobCategoryInfo`가 단일 출처.
- **큐 작업**(`QueueOrder`: 건설 → 농사 → 제작 → 연구 → 운반 → 짐내림): 건물/농지/작업대가 일감 등록 → 폰이 집어감. **배열 순서 = 전역 우선순위**(모든 폰 공통). `QueueOrder`에 없는 카테고리는 큐에서 배정되지 않는다.
- **직접 배정**(Mining/Logging = 자원 노드, Guard = 순찰 구역): 플레이어가 대상에 폰을 붙임(`Blackboard.AssignedNode`/`AssignedPatrolZone`). 큐를 타지 않으므로 on/off 대상이 아님. (`MineJob`은 현재 미사용 코드)

Job score = `JobCategoryInfo.Weight(category) × 100 + job.Priority`.

폰별 설정은 **숫자 우선순위가 아니라 on/off** — `WorkSettings`(`UnitBrain.Work`, 플레이어 세력 일꾼에만 자동 부착). UI는 `ColonyUI`(P키). 상세 `docs/jobs.md`.

### Damage Quick Ref

```
DamageCategory: Physical / Elemental / Dark / Holy
PhysicalType:   Slash / Pierce / Blunt          (Physical일 때)
DamageElement:  None / Fire / Ice / Lightning / Poison  (비Physical일 때)
```

### 자원 — 종류는 아이템, 묶음만 enum (`ResourceGroup`)

**`ResourceType` enum은 없다**(2026-08-02 삭제). 그 22개 값은 `ResourceItemData` 에셋과 정확히 1:1이라 한 자원에 이름이 셋(에셋 Id · enum 값 · 표시명) 있는 꼴이었고, 표시명 테이블도 두 벌이었다.

- **무슨 자원인가** = `ItemData` 참조. `BuildingData.CostItem` / `MinionData.SummonCostItem` / `ResourceNodeData.DropItem` / `SettlementManager.OfflineYieldEntry.Item`.
- **어느 묶음인가** = `ResourceGroup { Stone, Ore, Gem, Ingot, Wood, Currency }` — `ResourceItemData.Group`이 직접 들고 있다. 창고 필터·UI 분류 전용이고 **콘텐츠가 아니라 화면을 나누는 축**이라 enum으로 남겼다.
- 표시명은 `ItemName` 한 곳. `ResourceGroupExt.DisplayName()`은 묶음 이름만 답한다.
- `ResourceGroupExt.ItemsIn(group)` — 그 묶음의 자원 전부를 **레지스트리에서** 훑는다(모드가 넣은 자원도 딸려 온다).
- `ResourceNodeData.IsTree` — 목재 묶음을 내놓는 노드 = 벌목(채굴 아님). 예전 `ResourceType == Wood` 검사를 대체.
- 모드가 새 자원을 넣는 법: 아이템 JSON에 `"type": "resource", "resourceGroup": "Ore"`. 옛 `resourceType`도 받아 묶음으로 옮기고 경고한다.

### CraftSkill 타입

Labor, WeaponCrafting, ArmorCrafting, Cooking, Alchemy, Research, Smelting, Stonecutting, Woodworking, Gemcutting

### ItemQuality

Common(일반) / Uncommon(고급) / Rare(희귀) / Unique(유니크) / Epic(에픽) / Legendary(전설)

`StatMultiplier()`: 1.0 / 1.2 / 1.5 / 2.0 / 2.5 / 3.0. 같은 등급끼리만 스택.
`ItemInstance.DisplayName` — `[희귀] 철검` 형태 표시명

### 상인 시스템

**골드**: `ResourceItemData` SO (`Group = Currency`, Stack=9999). Storage에 보관, `GetTotalByItem` / `ConsumeByItem`으로 집계.

**`ItemData.ResaleFactor`** — **되팔 때의 감가.** 1 = 시세대로, 낮을수록 손해. **상인이 아니라 물건이 든다** — 상인 쪽에만 두면 "이 상인은 인색하다"밖에 표현할 수 없어 그에게 광석을 팔아도 똑같이 깎인다. 값이 안 나오는 건 물건의 성질이다: 광석은 시세대로 받지만 쓰던 검은 중고다. 기본 1(자원·식량) / `WeaponData` 0.55 / `ArmorData` 0.5 → 상인 매입률과 곱해 **장비는 사는 값의 약 1/4.5, 자원은 1/2**로 팔린다. JSON 아이템도 `"type": "weapon"`이면 `WeaponData`로 만들어지므로 그대로 적용된다.

**ItemData.BaseValue**: 아이템 기준 가격(G). 인벤토리 툴팁에 "가치" 표시 + 상점 판매가 계산 기준. JSON은 `"baseValue"`. **안 적으면 0이고, 그러면 상인이 기본값 10G로 판다** — 허브도 철검도 10G가 된다.

**MerchantData** (SO, `Create → DungeonColony → MerchantData`):
- `MerchantType`: General / Weapon / Armor / **Material** / **Food**. **한 마을에 종류당 하나씩 선다**(`TownMarket`)이므로 이 enum은 곧 마을 시장의 **줄 목록**이다 — 값을 늘리면 상인이 한 명 더 설 자리가 생긴다. 값은 뒤에 붙일 것(직렬화가 정수라 중간에 끼우면 기존 에셋이 밀린다)
- `DailySlotCount`: 하루 진열 종류 수
- `ItemPool`: 후보 목록 (`Item` **또는 `ItemId`** + `BasePrice`(0이면 아이템 BaseValue) + `MinStock` + `MaxStock` + `Weight` + `Quality`). **`ItemId`는 JSON·모드 아이템용** — 그것들은 런타임 생성이라 에셋 참조로 못 가리킨다. 읽을 땐 반드시 `entry.Resolve()`(에셋 먼저 → 없으면 레지스트리 Id)
- `GoldItem`: Gold ItemData 연결
- `PriceMultiplier`: 마을 전체 구매가 배율 (기본 1.0)
- `ItemOverrides`: 아이템별 개별 배율 (특정 아이템만 비싸게/싸게)
- `SellMultiplier`: 플레이어→상인 판매가 배율 (기본 0.5)
- `CalcPrice(entry)` — 구매가 계산: `BasePrice × GetItemMultiplier(item)`
- `CalcSellPrice(item, quality)` — 판매가 계산: `BaseValue × quality배율 × GetItemMultiplier(item) × SellMultiplier × **ItemData.ResaleFactor**`. **살 때와 팔 때 같은 배율을 쓴다** — 전엔 배율이 구매가에만 붙어서 생선을 2배로 치는 마을도 매입은 남들과 같았고, 그러면 싸게 사서 비싸게 파는 교역이 성립하지 않는다

**`Shop`** (`Assets/1_Script/Event/Shop.cs`) — **상점 하나. MonoBehaviour가 아니다.** 재고(`Stock`)와 거래(`TryBuy`/`TrySell`/`GetSellPrice`/`Refresh`)를 든다. 필드맵 상인은 `Merchant`가, 월드맵 마을은 `TownMarket`이 이걸 든다 — 지도 위의 마을은 "칸"이지 씬에 있는 물건이 아니라서 폰을 세울 수 없다.

**`TradeAccount`** (`Assets/1_Script/Event/TradeAccount.cs`) — **거래의 "우리 쪽": 돈이 어디 있고 산 물건은 어디로 가는가.** 상인이 아는 것은 "무엇을 얼마에 파는가"뿐이고, 지갑이 창고냐 원정대냐는 **누가 사러 왔느냐**의 문제다. `GoldAmount` / `TakeGold` / `GiveGold` / `Receive`.
- **돈은 전액 아니면 0**(절반만 빼면 되돌릴 방법이 없다), **물건은 부분 입고 인정**(받은 개수를 돌려주면 거기까지만 값을 치른다).
- `StorageAccount` — 거점 창고. `Merchant.Account`가 이걸 준다.
- `PartyAccount` — 원정대. **멤버 배낭이 곧 지갑이고 창고다.** 등짐(`Cargo`)은 안 쓴다 — 등급을 몰라서 희귀 물건이 조용히 일반이 된다. **잠근 뭉치도 안 쓴다**(여비를 잠가 두면 그만큼은 안 나간다).
- **사는 순서는 돈 먼저.** 계정에 되돌리는 길이 있으므로, "물건을 넣은 뒤 값을 못 치르는"(= 되돌릴 곳을 모르는) 경우가 아예 안 생긴다.

**Merchant 컴포넌트** (`Assets/1_Script/Event/Merchant.cs`) — **폰에 이걸 붙이면 상인이 된다.** `Shop` / `Account`(창고 지갑) / `RefreshStock()`(매일 자정) / `OnWorldClick`. 어느 종족 프리팹이든 `Merchant` + `MerchantData`를 얹으면 그 물건을 파는 상인이다. **역할(`NpcData`)은 행동만 정한다**(자리를 지킨다 / 들렀다 떠난다) — 무엇을 파는지를 역할에만 적을 수 있으면 종류마다 역할 에셋이 필요해진다(역할 × 상품의 곱셈).

**쓰러진 상인과는 거래하지 않는다** — `Merchant.CanTrade`(기절·시체. `BecomeCorpse`는 `Merchant`를 떼지 않는다). 여는 것만 막으면 창을 띄워 둔 채 눕히면 되므로 `MerchantUI`가 매 프레임 다시 본다. **거절은 알림으로 띄우지 않는다** — `IClickGate.CanClick`으로 **클릭을 사양**하면 `SelectionManager`가 다음 분기(폰 정보 패널)로 흘려보낸다. 이벤트 알림 패널은 사건을 보는 자리라 조작 실패가 섞이면 안 되고, 쓰러진 것은 눈에 보이므로 설명이 필요 없다.

**`Shop`은 상인마다 하나다.** 같은 `MerchantData`를 쓰는 상인이 둘이어도 재고는 **따로 돈다** — `MerchantData`는 "무엇을 팔 수 있는가"(후보 목록)이고 오늘 진열대에 무엇이 몇 개 올랐는지는 그 상인의 것이다. 한쪽에서 사도 다른 쪽은 그대로고, 일일 갱신도 각자 돈다. 창 제목은 `이름 (무기 상인)` — 종류 이름만 쓰면 무기 상인이 둘일 때 어느 쪽 창인지 알 수 없다.

**`TownRecruits`** — 마을에서 **사람을 구한다**. `At(node)`. **마을엔 늘 최소 한 명이 선다**(바이옴 풀이 비면 빌려 쓰고 경고). 인원(1~3)은 후보 종류 수와 무관 — 사람을 가르는 건 종족이 아니라 이름이라 같은 종족을 여럿 세우되 이름이 안 겹치게 고른다. 후보는 `BiomeContentData.TownRecruits`에서 **칸 좌표 + 시드 + 날짜**로 결정적(여닫는다고 새로 안 뽑힌다 — 그러면 고르는 일이 아니라 돌리는 일이 된다). 후보마다 `PawnSpec`을 들고 서고(이름·등급·몸집·값이 미리 정해진다) 등급 범위는 `RecruitEntry`가 종족 기본을 덮는다. **이름을 미리 정한다**(`UnitBrain.Rename`) — 안 그러면 `Awake`가 새로 굴려 메뉴에서 본 사람과 합류한 사람이 달라진다. 고용 = `PartyAccount`로 값을 치르고 프리팹을 **꺼진 채로** 찍어 `party.Members`에 넣는다(거점 진입 시 `EnterSettlement`가 켠다).

**`TownMarket`** (`Assets/1_Script/World/TownMarket.cs`) — 월드맵 `Town` 칸의 시장. `ShopsAt(node)` / `GoldAt(node)`. **종류당 하나씩** 선다(재료·식료·무기·방어구). **종류는 바이옴 풀에 있는 것이 전부 뜬다** — 좌표가 고르는 건 "같은 종류에 후보가 여럿일 때 어느 쪽인가"뿐이다. 종류까지 추첨하면 무기 상인을 만나려고 마을을 순회하게 되고, 그건 고르는 일이 아니라 찾아다니는 일이 된다. 어느 상인이 서는지는 `BiomeContentData.TownMerchants`에서 **칸 좌표 + 시드 해시로 결정적**(`WorldDeposits`와 같은 패턴 — 배치는 저작물, 세부만 굴린다). 재고는 **하루 단위 캐시**(여닫으며 재고를 새로 뽑는 것을 막는다), 상인 자체는 안 바뀐다. 지도를 새로 지으면 `WorldMap.Generate`가 `Clear()`.

**상인 하나 만드는 법** — 셋 중 아무 길이나. 어느 쪽이든 `Merchant`가 붙고 매일 `RefreshStock()`이 돈다. **우선순위는 저작 > 주입 > 기본값.**
1. **프리팹에 직접** — 종족 프리팹에 `Merchant`를 붙이고 `MerchantData`를 지정. **이게 `HasAuthoredData`**(인스펙터에서 물린 것만 참, `Awake`가 잡는다)이고, 이벤트·스폰이 **덮지 않는다** — "이 사람은 늘 이것만 판다"가 성립해야 고정 상인을 프리팹 하나로 세울 수 있다.
2. **스폰이 정해 준다** — `merchant.Data = ...`(setter가 그 상인만의 재고를 새로 뽑는다). 이벤트·마을이 이 길.
3. **역할 기본값** — `NpcData.MerchantData`. 위 둘이 다 없을 때만.

**종족별 상품은 두 축이 짝을 이룬다 — `Races`(누가 취급하나) + `ExtraPools`(무엇을 얹나).**

- **`MerchantData.Races`**(비우면 누구나) — 스폰이 종족을 먼저 뽑고 **그 종족이 취급하는 상품만** 후보로 삼는다(하나도 없으면 역할 기본값으로 떨어진다). 종족 풀 × 상품 풀을 그냥 곱하면 인간이 고블린제 물건을 들고 오고, 조합마다 프리팹을 만들면 종족 × 종류의 곱셈이 된다. **조건은 상품 쪽이 든다** — 어느 종족이 만드는 물건인가는 그 물건의 성질이지 파는 사람의 스탯·이름 풀의 성질이 아니다.
- **`MerchantData.ExtraPools`** — 상품 묶음을 **겹쳐 쓴다**. `Merchant_Goblin_Armor`는 자기 목록에 고블린제만 적고 `Merchant_Armor`를 통째로 얹는다. 이게 없으면 종족별 에셋마다 공용 목록을 복붙해야 하고, 기본 방어구 하나를 고칠 때 종족 수만큼 고쳐야 한다 — 반드시 어긋난다. 진열은 `Data.CollectPool()`(자기 것 + 얹은 것, 중복·순환 방지)에서 뽑고, **값·매입 배율·진열 칸 수는 파는 상인의 것**을 쓴다(같은 물건도 누가 파느냐로 값이 달라지는 지역 시세와 같은 규칙).

월드맵 마을(`TownMarket`)은 파는 폰이 없으므로 `Races`를 안 본다(`ExtraPools`는 그대로 작동).

**이벤트 상인** (`NpcType.EventMerchant`): 걸어 들어와 `NpcData.StayDuration` 후 돌아가 소멸. **장사는 상주 상인과 똑같이 한다** — 2026-08-07까지 `Merchant`를 아예 안 붙여서 클릭해도 아무것도 안 열렸다. **종족과 상품을 따로 굴린다** — `GameEventData.MerchantPrefabs`(누가 오는가) × `MerchantVariants`(무엇을 파는가)를 스폰 때 조합한다. 둘을 프리팹에 묶으면 종족 × 종류만큼 프리팹이 필요해지고, 종족을 하나 늘리는 값이 종류 수만큼 곱해진다.

**UI 구조:**
- `MerchantUI` ← `Merchant.OnSelected` 이벤트로 오픈. 상인 재고(구매)만 담당.
  - 아이템 클릭/더블클릭 → 구매. 단건 구매만 지원.
- `InventoryUI` — 상인 열림 시 판매 모드 전환. 하단 액션 바에 [판매] 버튼 표시.
  - 아이템 단클릭 → 선택 + 버튼 활성, 더블클릭 → 즉시 판매, 드래그 → MerchantUI 위에 드롭 판매
- `ItemInfoUI` — 아이템 hover 시 공용 툴팁. **이름줄은 이름만**이고 아랫줄이 `등급 · 종류`다(`희귀 · 양손 검`) — 이름 앞에 등급을 또 붙이면 같은 말이 두 번이고 긴 이름이 잘린다. 종류 이름은 **`ItemData.TypeName`이 답한다**(클래스가 오버라이드) — 밖에서 `is WeaponData`로 갈라 쓰면 종류가 늘 때마다 분기를 전부 찾아 고쳐야 하고 하나 빠뜨리면 조용히 "잡화"가 된다

**`MerchantUI`는 모드 무관이다** — 거래는 거점 상인에게도 하고 월드맵 마을에서도 한다. `GameModeManager`가 `MerchantUI`·`InventoryUI`·`ItemInfoUI`의 캔버스 직속 부모를 필드 UI 스태시에서 뺀다(씬 배열이 아니라 컴포넌트에서 부모를 타고 올라가 찾는다). 스태시에서 빼는 것뿐이라 저절로 열리진 않는다. 진입점 둘 — 필드는 `Merchant.OnSelected`, 월드는 `MerchantUI.OpenFor(shop, account, traders, title)`. 모드가 바뀌면 닫는다. 원정대 거래면 **누구 배낭을 볼지는 배낭 창이 갖는다** — `InventoryUI.SetPawnCycle(members)` → 적재 바(`CarryBar`) 오른쪽 끝의 [이전] 이름 (2/4) [다음]. 무게·짐내림·잠금이 이미 그 폰의 것이므로 폰 고르기도 같은 줄에 있어야 한다. 원정 나간 폰은 GameObject가 꺼져 있어 `SelectionManager`로는 배낭을 열 길이 없다(`BindTo`가 그 우회로).

**씬 구성**: `ItemInfoUI` + `MerchantUI`는 씬에 있고, 항목 행은 `MerchantStockEntry.prefab` / `SellItemEntry.prefab`을 복제해 채운다. 모양 수정은 인스펙터에서(생성기는 역할을 마쳐 제거됨 — `docs/editor_creators.md` "생성기의 수명").

**지역 시세는 재료 상인이 든다** (`5_Data/Merchant/`, 7종):

| 에셋 | 종류 | 성격 |
|---|---|---|
| `Merchant_Plains` / `Forest` / `Cavern` / `Frontier` | Material | **지역별 가격표.** 평야는 곡물이 싸고 광물이 비싸다 / 동굴은 반대 |
| `Merchant_Food` / `Weapon` / `Armor` | Food / Weapon / Armor | 지역 무관 공용 |

지역 상인의 `ItemOverrides`는 **팔 때도 적용된다**(`CalcSellPrice`). 그래서 재료 상인이 빵을 진열하지
않아도 "동굴 마을에서는 빵을 비싸게 쳐준다"가 성립한다 — 무엇을 지고 가서 어디서 파느냐가 선택이 된다.

바이옴별 진용(`BiomeContentData.TownMerchants`)이 마을 성격을 정한다 — 평원=넷 다 / 동굴=재료·무기·방어구
(식료 없음) / 설원=재료·식료. "이 마을엔 무기 상인이 있나"가 지도를 도는 이유가 된다.

### 이벤트 시스템

**GameEventData** (SO): `EventName`, `Description`, `Icon`, `Weight`, `GameEventType`
- `Raid`: `EnemyPrefabs[]`, `RaidCount` → 맵 엣지에서 던전 코어 방향으로 이동
- `Merchant`: **`MerchantPrefabs`**(종족) × **`MerchantVariants`**(상품, 비우면 프리팹이 들고 온 것) → 맵 엣지에 NPC 스폰 (EventMerchant BT로 동작). 각각 하나씩 뽑아 조합
- `Reinforcement`: `ReinforcementPrefabs[]`, `ReinforcementCount` → 아군 지원군 스폰
- `Discovery`: `DiscoveryItems`(비우면 레지스트리 전체, **화폐 제외**), `DiscoveryMinAmount/MaxAmount` → **거점 둘레 바닥에 떨군다**(`discoveryDropRadius`). 예전엔 자원 노드를 세웠는데 노드는 영구히 남아 거점 둘레가 광맥 밭이 됐고(**발견은 사건이지 지형 변화가 아니다**), 그다음엔 창고에 바로 넣었는데 그건 **사건이 아니라 숫자가 조용히 오르는 일**이라 알림을 놓치면 무슨 일이 있었는지도 모른다. 떨구면 `DroppedItem`이 운반 일감을 걸어 폰이 가지러 간다

**스폰 자리는 지형이 아니라 맵이 정한다**(`TryFindEdgeSpawn`) — **핵에서 바깥으로 한 칸씩 걸어 나가** 설 수 있는 마지막 칸을 찾고 `spawnEdgeInset`만큼 **안쪽으로 물러선다**. 걷다가 설 수 없는 칸에서 멈추므로 **핵과 이어져 있는 것이 방식 자체로 보장된다**(좌표를 굴려 검사만 하면 바닥 밖 섬을 못 거른다). 낭떠러지 바로 위에 무리를 세우면 서로 비켜서다 밀려 떨어진다(`ExpeditionFormation.approachInset`과 같은 교훈). 흩뿌리기(`ScatterNear`)는 **이미 쓴 칸**과 **이웃이 공허인 칸**을 피한다. **자리도 프리팹도 없으면 알림 없이 건너뛴다** — 알림이 먼저 나가면 "몰려온다"고 해 놓고 아무도 안 온다. **침공대는 소환 직후 핵으로 `SetMoveTarget`** — 적 BT는 탐지·배회(12m)뿐이라 명령이 없으면 가장자리에서 서성이다 끝난다.

**EventNotificationUI**: `EventManager.OnEventTriggered` 구독 → 이벤트 발동 시 화면 상단 팝업 자동 표시. `displayDuration`(기본 5초) 후 자동 닫힘. **제목도 설명도 비면 안 띄운다** — 배경만 남아 화면에 작은 검은 상자가 뜬다(실제로 빈 이벤트 에셋 하나 때문에 그것만 보였다, 2026-08-06). 대신 무엇이 비었는지 콘솔에 남긴다. **스폰형 이벤트는 8방위를 덧붙인다**(`북동쪽에 나타났습니다`) — 어느 쪽을 막을지가 곧 대응이다. 꺼져 있는 동안(월드맵) 들어온 알림은 **데이터든 완성된 문장이든 들고 기다렸다** 돌아올 때 띄우고, 둘 다 밀렸으면 문장이 이긴다(방향이 붙어 더 자세하다).

**`MinGameTime`은 `GameTimeManager`와 같은 축**(하루 = 1800초). 크게 잡으면 초반 추첨 풀에서 빠져 다른 이벤트만 반복된다 — 습격 600·합류 1200이 그래서 한동안 안 나왔고 습격 120·합류 600으로 내렸다(2026-08-07).

**이벤트 에셋**(`5_Data/Event/`, 4종): `Ev_Raid_Goblins`(고블린 4, 가중치 2.5) · `Ev_Peddler`(행상인 방문, 2.0) · `Ev_Wanderer_Joins`(방랑자 합류, 0.8) · `Ev_Discovery`(아이템 발견, 3.0). 씬의 `EventManager.eventPool`에 배선돼 있다.

### BuildMode

- **건설 목록은 레지스트리에서 온다** — `Assets/Resources/Data/Buildings/`에 `BuildingData` 에셋을 두면 그것만으로 목록에 오른다(`DataManager.AllBuildings`, `Category`→`SortOrder`→이름 순). 씬의 `placeableBuildings`는 **비워 두는 게 기본**이고, 채우면 그게 이기는 큐레이션 오버라이드다(`ResearchTable.availableResearch`와 같은 관례). `UsesBlueprint=true` → 2단계 건설, `false` → 즉시 배치
- 설계 원칙: `BuildingData`는 배치 방법만 담당. 각 컴포넌트(Storage, SubCore, FarmPlot 등)가 동작 데이터를 프리팹 SerializeField로 직접 보유
- **씬에 손으로 놓은 건물도 자기 칸을 막는다** — `PlacedBuilding.Start()`가 `data.Size`로 발자국을 등록한다(배치로 지은 건물은 이미 불렸으므로 건너뜀). **건물 프리팹에는 `PlacedBuilding` + `data`를 미리 붙여 둘 것** — 없으면 이 등록이 아예 안 돌고, 그러면 길찾기가 아는 건 콜라이더뿐이라 모델보다 작을 때 **모델 한복판을 가로지르는 경로**가 나온다.
- **씬 배치는 격자에 맞출 것**: **홀수 크기 축은 `.5`, 짝수 축은 `.0`**(`GetFootprint`/`FootprintCenter` 규약). 어긋나면 모델이 옆 칸으로 삐져나와 **작업 자리를 먹는다**. 도구: `Snap Placed Buildings To Grid` → `Fit Building Colliders To Size`(**순서 지킬 것**).
- `OnActiveChanged` — 건설 모드 토글 시 다른 UI 자동 닫힘
- **바닥(floor) 시스템**: `BuildingData.IsFloor` → 무료 substrate(코어 무관). `MapManager._floored`가 타일 → **바닥 윗면 Y**를 희소 저장. 배치 높이는 `SurfaceHeight` + `SpawnOnSurface` — 프리팹 루트 `localPosition.y`가 양수면 **저작 부양 높이**로 보고 그만큼 띄우고(서브코어 등), 아니면 렌더러 바닥면을 표면에 맞춘다(피벗 중앙 프리팹 파묻힘 방지). 즉시 배치/청사진/고스트/자유 배치 동일. 건물의 "바닥 위에만" 게이트 = `FloorRequired`(= `requireFloor` 플래그 ∥ 바닥 존재) — 던전 씬은 플래그를 켤 것. 바닥은 좌클릭 드래그 페인트 + 그리드 강제. **공허 이동 차단 3중**: 길찾기가 바닥 밖 셀 unwalkable + `PawnMover` 낭떠러지 가드(`ledgeLookahead`) + `fallRecoveryY` 낙하 복구. AI 목적지는 `IsStandable` 검사.
- **핵 영역(크립) / 건물 휴면**: 영역(핵 union) 밖 건물은 `BuildingDormancy`가 휴면(작동 정지+회색 틴트, 철거 X). **휴면 = 전면 사용 불가** — 창고 집계 제외(`Storage.IsActive`), 컴포넌트 `enabled=false`, 클릭해도 패널 안 열림, 매달린 일감은 `JobBase.IsSiteUsable`로 **배정 보류**(취소 아님 → 복구 시 재개). 외부 판정 `BuildingDormancy.IsDormantAt`. 런타임 배치는 `Ensure`로 자동 부착(씬 사전 배치는 수동). **핵 이전** = `DungeonCoreUI` [핵 이전] → `CoreRelocator` → `RelocateTo`. 상세 `docs/design_core_creep.md`

### Input & Camera

| Script | 트리거 | 동작 |
|---|---|---|
| `SelectionManager` | 좌클릭/드래그 | Player 팩션 유닛 선택. **UI에서 시작한 클릭은 놓을 때까지 통째로 무시**(`_pressOverUI`) — 누를 때만 걸렀더니 배낭에서 바닥으로 물건을 드래그해 버릴 때 놓는 쪽이 새어, 지난 `_dragStart`와 엮인 엉뚱한 사각 선택이 **선택을 통째로 지웠다**. 바닥의 드롭 클릭은 노드·시체처럼 **선택 유지**. 적/NPC 클릭 = `OnPawnInspected`(정보만, 명령 대상 아님). **폰이면서 `IWorldClickable`이기도 하면 클릭 대상이 이긴다**(상인 = 상점이 열린다) |
| `MoveController` | 우클릭 | 방랑자 이동/타겟 설정. **적대 세력 = 즉시 공격**(뜻이 하나뿐), **중립·아군 유닛 = `PawnContextMenuUI`**(거래/따라간다/공격한다). 선택지 구성은 `MoveController`가 정한다 |
| `PawnContextMenuUI` | 우클릭(적대 유닛·맨바닥 제외) | 우클릭한 **월드 지점에 고정**되는 팝업(카메라를 움직여도 그 자리 — `LateUpdate`에서 매 프레임 화면 좌표로 푼다). 제목 바·닫기 버튼 없음(고르거나 딴 데 누르면 닫힘). 줄은 씬의 `OptionTemplate` 복제. 대상별 선택지: 유닛=거래(상인, **쓰러졌으면 안 뜬다**)/따라간다/공격, 노드=캔다·벤다(배정)/살펴본다, **드롭=착용한다(장비)/줍는다/창고로 옮긴다**, 시체=뒤진다, 핵·건물=연다, **어디든 마지막에 [여기로 이동]**. **[줍는다]**(`PickUpTarget` + `PickUpNode`, 방랑자 트리 `ManualOrderNode` 바로 아래)는 **자동 운반과 다른 축** — `GroundHaulJob`은 "창고로 가져다 놓아라"는 큐 작업이라 창고가 없거나 운반을 껐으면 안 돌고, 그러면 바닥의 물건을 집을 길이 없다. 규칙 셋: ① **들 수 있는 폰 중 가장 가까운 한 명만** 보낸다(앞사람 짐이 찼으면 다음 폰). ② **혼자 골랐으면 한도를 넘겨서라도 든다**(`PickUpIgnoreLimit`) — 배분이 아니라 "저걸 네가 들어라"라서 손으로 넣는 것과 같고 과적은 속도로 값을 치른다. **여럿일 때만 한도가 누가 갈지를 가른다.** ③ 무게와 **격자 자리를 둘 다** 본다(격자는 한도를 무시할 때도 못 넘는다). **[착용한다]는 줍기와 한 동작**(`PickUpThenEquip`, 장비 위에선 맨 위 선택지) — 원래 차던 것은 `Equipment`가 배낭으로 돌려주고, 착용은 짐으로 남지 않으므로 한도를 안 본다. **맨바닥은 창 없이 즉시 이동**(선택지가 하나뿐인 창은 클릭만 늘린다). 따라가기 = `UnitBlackboard.FollowTarget` + `FollowNode`(방랑자 트리에서 `ManualOrderNode` 바로 아래) |
| `InventoryUI` | I키 | 인벤토리/장비 패널 |
| `UnitStatsUI` | C키 | 스탯 패널 토글 |
| `ColonyUI` | P키 | 무리 관리(작업 on/off + 전투 성향, 폰 전체) |
| `ExpeditionUI` | G키 | 원정대 편성 — 폰 체크 + 적재량 요약. [출발] 시 **바로 사라지지 않고** 핵 앞으로 집결(`ExpeditionFormation`), 전원 도착 후 함께 떠남 |
| `WorldMenuUI` | 월드맵에서 칸에 **도착**(빈 야생 포함) | 마운트앤블레이드식 도착 메뉴(화면 왼쪽 고정, 좌 그림 / 우 선택지). **선택지 구성은 `WorldMapView.OpenMenu`가 정한다** — `탐험한다`(매장 조사) · `시장에 간다`(메뉴 안 메뉴 → 상인별 한 줄, `TownMarket`) · `마을을 둘러본다`(메뉴 안 메뉴 → 인력 영입, `TownRecruits`) 동작, 소굴·`직접 건설`은 "준비 중" |
| `BuildModeUI` | B키 | 건설 모드 |
| `CameraController` | WASD/중클/스크롤 | 카메라 |
| `CameraController` (월드) | WASD/중클/스크롤 | 월드맵 카메라 — 필드와 같은 컴포넌트. `GameModeManager`가 **월드 카메라에 붙은 것은 월드 측**으로 보고 필드와 반대로 켠다 |
| `WorldMapView` | M키 | 필드↔월드 **모드 전환**(`GameModeManager`에 위임 — 반대편은 통째로 꺼진다). **원정대는 여럿**. 나가 있는 팀마다 토큰이 서고, 없으면 토큰도 없다. **선택 조작은 필드맵과 동일** — 좌클릭=단일 선택 / 드래그=사각 다중 선택 / 빈 곳=해제. **우클릭=선택한 팀만 이동**. **내용물 있는 칸에 도착하면 `WorldMenuUI` 자동 오픈**(M&B식 — 멀리서 클릭해선 안 열린다), Enter=한 팀만 선택했을 때 그 메뉴 재진입. 토큰에 콜라이더가 있어 `RaycastAll` 거리순으로 "토큰인 첫 충돌"(선택) / "토큰 아닌 첫 충돌"(이동)을 가른다. 월드에서 **자유 실시간 이동**(우클릭한 지점으로 직진, 지형 표면을 탄다 — 격자 스냅 없음). 지형은 `WorldMapTerrain`이 저작 격자를 재료로 **맵 전체를 메시 한 장**으로 짓는다(바이옴 성격을 이웃끼리 보간 → 칸 경계 없음). 컴포넌트를 빼면 직접 만든 지형을 쓴다. `WorldMapGrid`는 좌표 변환 + 격자선 + 오브젝트 배치 |

### 개체 명세 — `PawnSpec` (`Assets/1_Script/unit/PawnSpec.cs`)

**"이 폰이 어떤 사람인가"를 태어나기 전에 적어 둔 것.** 이름 · 스탯 등급 51개 · 몸 크기, 그리고 그것을 만든 **시드 하나**.

`UnitBrain`은 `Awake`에서 이름과 스탯을 제 마음대로 굴린다 — 그게 기본값이다. 하지만 **보고 나서 데려오는 경우**(마을 고용)엔 메뉴에 뜬 사람과 합류한 사람이 같아야 하므로, `UnitBrain.ApplySpec(spec)`이 그 결과를 덮는다. `Spec` 프로퍼티로 남는다.

- **모든 무작위가 시드 하나에서 나온다** → 같은 시드면 몇 번을 다시 계산해도 같은 사람(세이브 없이도 일관).
- **외모 커스터마이징의 자리이기도 하다.** 굴리는 것과 사람이 정하는 것이 **같은 그릇**을 쓰므로, 나중에 머리·피부색·장식이 늘어도 `PawnSpec`에 필드가 늘 뿐 적용 통로(`ApplySpec`)는 그대로다. 지금은 `BodyScale` 하나가 그 축의 첫 칸이다.
- **수준(tier)을 먼저 뽑고 스탯은 그 언저리(±1칸)에서 굴린다.** 51개를 각각 균등하게 굴리면 평균이 중심으로 몰려(표준편차 1/√51) 누구나 한복판이 되고 "강한 방랑자"가 성립하지 않는다.
- `PriceFrom(basePrice)` — `기준가 × (1 + 평균등급 × 0.12) × 흔들림(±22%)`. **등급이 값을 정하되 같은 등급이라도 값은 다르다** — 흔들림이 없으면 값이 곧 스탯 표가 되어 굳이 볼 이유가 없다. 흔들림도 시드에서 나오므로 메뉴를 여닫아도 안 바뀐다(스탯 추첨과는 **다른 난수열**을 쓴다 — 같은 열이면 "스탯이 좋으면 흔들림도 크다"는 상관이 생긴다).

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

## 데이터 현황표

`python Tools/export_gamedata.py` → `Tools/GameData.xlsx`(Items·Recipes·Buildings·Enums, 시트마다 자동 필터) + `Tools/Items.csv`·`Tools/Recipes.csv`(git diff용).

**표는 저작물이 아니라 에셋의 그림자다** — 에셋과 코드를 직접 읽어 만든다. 값을 바꿨으면 다시 돌릴 것. **손으로 적은 표는 반드시 뒤처진다** — 적을 때만 맞고 그 뒤로는 아무도 고치지 않는다.

제작 배수(산출값 ÷ 재료비)가 **1 미만이면 붉게** 칠해진다 — 만들수록 손해인 레시피가 표에서 가장 먼저 눈에 띈다.

## 알려진 이슈

- **BT 노드에서 `Time.deltaTime` 금지**: BT는 `tickInterval`(0.1초)마다 돌므로 프레임 델타를 누적하면 시간이 6배 이상 느려진다. `UnitBlackboard.TickDelta`를 쓸 것 (2026-07-27 전수 교체).

- **BT 노드에서 델타 곱하기를 빼먹는 것도 같은 사고**: 반대 방향으로 틀린다. `ResourceNode.Work(skill)`이 틱마다 스킬 수치를 그대로 더해 채굴 속도가 **초당 skill×10**이 됐었다(2026-07-31 수정 — `Work(power, deltaTime)`로 시그니처를 바꿔 호출자가 델타를 빠뜨릴 수 없게 했다). **틱마다 누적하는 값은 전부 델타를 곱할 것** — 안 그러면 배속·프레임레이트가 게임 밸런스를 바꾼다.

- **일감을 밖에서 취소하면 들고 있는 폰에게도 놓는 길을 내야 한다**: `JobBase.Tick`은 `InProgress`일 때만 돌고 `IsComplete()`는 `Done`일 때만 참이라, **밖에서 취소된 작업은 어느 쪽에도 안 걸려 폰 손에 영원히 남는다**(도착해서 아무것도 안 하고 서 있는다). `BTLeafNodes`가 `Status == Cancelled`면 놓는다(2026-08-02, 연구 갈아타기에서 드러남).

- **새 UI 텍스트에 특수문자를 쓰지 말 것**: 한글은 `TMP Settings` 폴백 체인(NanumGothic SDF → LiberationSans SDF)으로 해결됐지만(2026-08-01), 폴백은 안전망이지 허가가 아니다 — 폰트가 섞이면 자간·기준선이 어긋난다(`docs/editor_creators.md` "폰트 체인").
- **Blueprint 반투명 머티리얼**: URP에서 코드 생성 투명 머티리얼(`BuildingBlueprint.CreateBlueprintMaterial`)이 불투명 렌더될 수 있음. 에디터에서 직접 제작 후 연결 권장.
- **DataManager**: `Resources.LoadAll<RecipeData>("Data/Recipes")` + JSON 스키마(`DataSchemas`) 경로 둘 다 지원. SO는 `Assets/Resources/Data/{Items,Recipes,Research}/`에 있어야 인식.

**SO냐 JSON이냐 — 기준은 "다른 에셋이 GUID로 가리켜야 하는가"다.** JSON 아이템은 런타임 생성이라 프리팹·SO가 **가리킬 방법이 없다**(그래서 상인 재고에 `ItemId` 문자열 경로를 따로 뒀다).
- **SO여야 하는 것**: `ResourceNodeData.DropItem` · `BuildingData.CostItem` · `MinionData.SummonCostItem` · `SeedData.HarvestItem` · SO 레시피의 재료/결과처럼 **에셋이 가리키는** 아이템.
- **JSON이 나은 것**: 레시피로만 만들고 쓰는 것(무기·방어구 대부분). 모드가 고치거나 덮어쓸 수 있다. 2026-08-02에 옛 장비 16종을 이 기준으로 JSON에 옮겼다(참조 0건 실측 후).
- **모드 자산(`ModAssets`)**: JSON의 `iconPath`/`modelPath`를 **그 콘텐츠 폴더 → 에셋 번들 → Resources** 순으로 찾는다. **1번이 본체** — 그림은 PNG/JPG, 모델은 **`.obj`(+`.mtl`+텍스처)** 를 폴더에 넣기만 하면 실린다(`ObjModel`이 직접 파싱). `.fbx`는 런타임 해석 불가 → 그렇게 알려 준다. 번들은 OBJ로 표현 못 하는 것(프리팹·뼈대·애니메이션)용 보조 경로(`DungeonColony → Build Mod AssetBundle`). 폴더 밖(`../`) 경로는 무시. 예시는 `StreamingAssets/Mods/ExampleMod/`. 상세 `docs/data.md`.

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
| **방랑자** | `UnitData` | ~~Draft~~ → **직접 명령** → 업기 → 줍기 → 따라가기 → **반격**+Fight → Rest → Eat → Work → Idle |
| **하수인** | `MinionData` | Detect+Fight → Rest → Eat → Patrol → Work → Idle |
| **Enemy** | `UnitData` | Detect → Fight → Idle |
| **NPC** | `NpcData` | 타입별 (Villager: Flee→Wander / Guard: Fight→Patrol / EventMerchant: WalkIn→Stay→WalkOut) |

**소집(Draft)은 아직 안 붙어 있다** — 분기·상태 표시·도주 억제까지 다 있는데 **`UnitBrain.Draft()`를 부르는 입력이 없다.** 그래서 그 분기는 한 번도 안 돈다(위 표에 취소선을 둔 이유). 지우지 않은 것은 자리를 이미 정해 뒀기 때문이다 — 붙일 때는 `DraftedNode`가 `Running`만 돌려 **소집 중에는 반격도 안 한다**는 것을 같이 볼 것.

### 팩션 (세력 관계)

**축은 다섯이고 서로를 모른다.** 조합은 **프리팹이 한다** — 인간 상인 = (Human + 상인 역할), 엘프 상인 = (Elf + **같은** 상인 역할).

| 축 | 데이터 | 답하는 것 | 에셋 |
|---|---|---|---|
| 종족 | `UnitData` | 스탯·이름 풀 | `Resources/Data/Units/` |
| 세력 | `FactionData` | 누가 적인가 | `5_Data/Faction/`(4종) |
| AI 성향 | `AiProfileData` | 적을 만나면 어떻게 하는가 + 유휴 배회 | `5_Data/AiProfile/`(4종) |
| 역할 | `NpcData` | 무엇을 하는 사람인가(주민·경비·상인) | `Resources/Data/Npc/` |
| 소지 | `LoadoutData` | 무엇을 지니고 무엇을 떨구는가(장비·소지품·동전) | `5_Data/Loadout/` |

- `FactionData` — `PlayerControlled` / **`AcceptsRansom`**(붙잡은 사람을 돈 받고 놓아주는가 — 끄면 쳐들어가는 수밖에 없다. **무리가 아니라 세력이 든다**: 무리마다 적으면 같은 세력인데 어떤 무리는 흥정이 되고 어떤 무리는 안 되는 꼴이 된다. 고블린은 꺼져 있다) / `DefaultStance` / `Relations[{Other, Stance}]`. **관계는 방향별**(`StanceTo`): A→B 적대여도 B→A 중립 가능. `Faction` 컴포넌트가 SO를 들고 `IsPlayer`/`IsHostileTo`/`IsAllyOf` 제공. **SO는 반드시 연결할 것** — 비우면 아무와도 관계가 없어 판정이 전부 중립으로 떨어진다(`Awake`가 경고). 반격은 관계와 무관(`UnitBlackboard.LastAttacker`, 15초).
- **방랑자는 반격만 한다 — 먼저 걸지 않는다**(`DetectNode(retaliateOnly: true)`). 하수인·적처럼 범위 안의 적대를 훑으면 지나가는 적을 보고 저 혼자 달려 나가 **직접 제어가 성립하지 않는다**(어디에 세워 둘지가 명령이 아니라 지형의 문제가 된다). 그렇다고 없으면 **맞으면서 가만히 서 있는다** — 방랑자 트리에 `DetectNode`가 아예 없어서 `bb.Target`이 저 혼자 붙는 길이 없었다(2026-08-12 수정). **노드를 새로 만들지 않고 스위치를 둔다** — 반격 대상 규칙(15초 기억 · 감지 범위 2배 · 쓰러진 놈 제외 · 기존 표적 유지)이 이미 `DetectNode` 안에 있다
- **AI 역할은 `UnitBrain.UsesEnemyAi`(NPC 아님 && !IsPlayer)** — 에셋 클래스는 묻지 않는다. **`is MinionData`는 "하수인 종족"이지 "내 편"이 아니다**(고블린은 소환될 수도 야생에 살 수도 있다). 소속을 물어야 하는 곳(`DungeonCore` 소환 한도, `InspectPanelUI` 라벨, `UnitStats.IsLocked`)은 **세력을 같이 볼 것**.
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
| `WorldBandManager` | **지도 위를 도는 무리**의 소유자 + **예고된 습격**. `Bands` / `FindContact(pos)` / `PushApart(band)` / `RemoveIfDead` / `BandFor(go)` / `TryGetRaidFrom(lairNode, …)`. **둘은 다른 축이다**(2026-08-10): 무리(`Roam`/`Caravan`/`Patrol`)는 제 볼일로 돌아다니고 **마주치는 것이 사건**이며, 작정하고 오는 습격은 **토큰 없는 사건**이라 며칠 전에 예고만 하고 도착 시 필드맵에 실제 적이 선다. 습격을 토큰으로 걸어오게 하면 며칠짜리 행군이 화면을 기어가고 "가로막을 수 있나"가 또 다른 규칙이 된다 — **막는 길은 소굴을 치는 것 하나로 족하다**(치면 무산되고, 두들겨 놓으면 그만큼 적게 온다). **포로**(`WorldBand.Captives` → 근거지에 닿으면 `WorldLair.Captives`): 진 원정대는 밀려나는 게 아니라 **붙잡혀 끌려간다**(폰은 안 지운다 — 꺼진 채로 실려 다니므로 이름·스탯·특성이 살아 있다). 되찾는 길 다섯(**몸값**(`WorldRansom`) / 그 무리를 이긴다 / 소굴을 친다 / 무리가 전멸한다 / 하루마다 굴리는 탈출)은 **전부 `WorldMapView.RestoreParty` 하나로 모은다** — 길이 넷인데 처리가 넷이면 하나가 어긋나 사람이 조용히 증발한다. **탈출 확률은 안전장치다**: 되찾을 길이 "이긴다"뿐이면 인원을 다 잃은 뒤엔 이길 수가 없어 지는 것이 손실이 아니라 막다른 길이 된다. 소굴 해방은 `WorldLairs.Clear` 안에 둔다(소굴이 없어지는 길이 여럿이라 호출처마다 챙기면 빠뜨린다). 떠돌이는 `RoamDays` 뒤 근거지로 돌아가 **병력을 복귀시킨다**(무한정 떠돌면 `maxBands`가 차서 새 무리가 못 나오고 소굴을 쳐도 이미 나온 것이 남는다). 소굴·마을이 무리를 내보내고, 실시간으로 목적지까지 걷고, **무리끼리의 조우는 여기서 조용히 결판난다**(알림 없음 — 남들 싸움에 창이 뜨면 내 원정대 알림이 묻힌다). **원정대와의 조우는 `WorldMapView`가 메뉴로 받는다**(이동을 멈추고 창을 여는 주인이 그쪽). **월드맵을 안 보고 있어도 돈다**(거점 시뮬이 계속 도는 것과 짝). 씬 배선은 `DungeonColony → Setup World Bands` |
| `WorldLairs` | 적 소굴(`EnemyLair` 칸)의 병력·출격 상태. `At(node)` / `BuildAll()`(미리 다 깨운다 — 가보지 않은 지역의 소굴도 저 혼자 내보내야 한다) / `Clear(lair)` / `RegenAll()`. 어떤 소굴인지는 `BiomeContentData.Lairs` + **칸 좌표 + 시드로 결정적**(`TownMarket` 패턴). **날짜는 안 섞는다** — 병력은 그날의 사정이 아니라 누적된 상태라 매일 굴리면 어제 친 소굴이 멀쩡해진다 |
| `WorldRansom` | **붙잡힌 사람을 돈으로 되찾는다.** `Gold`(`Wallet.Balance`) / `PriceFor(폰\|여럿)` / `Available(party)` / `TryPay`. **값 = 스탯 + 장비**이고 사람 값은 고용가(`PawnSpec.PriceBase/PricePerGrade`)와 **같은 상수**를 쓴다(값이 두 벌이면 "고용은 싼데 되찾는 건 비싸다"가 사고가 된다). 되사는 값이라 ×1.4, **흔들림 없음**(창을 여닫을 때마다 바뀌면 값이 나올 때까지 여닫게 된다). 등급 평균은 명세가 아니라 **실제 스탯**에서 낸다(굴려서 태어난 폰도 붙잡히고, 자란 만큼도 값에 실린다). 지불은 **배낭 먼저 그다음 창고**(`MaterialPool.Consume`과 같은 순서) — `TakeGold`가 전액 아니면 0이므로 **가진 만큼만 청구**한다(부족한 액수를 그대로 넣으면 한 푼도 안 나와 "둘을 합치면 되는데 못 낸다"가 된다) |
| `WorldBattle` | **지도 위의 자동 전투**(M&B의 auto-resolve). `PowerOf(BandMember/UnitBrain/멤버들)` / `Resolve(party, band)` / `ResolveBands(a, b)`. 전력 = **공격·방어·강인함·회피** 넷의 가중합(51개를 다 세면 요리 실력이 전투를 바꾼다). **장비는 양쪽 다 세되 표현이 다르다** — 원정대는 실제 무기·갑옷을 읽고, 무리는 `WorldBandData.PowerMultiplier`가 그 자리를 대신한다(한쪽만 세면 무장한 쪽이 늘 이겨 판정이 결론이 된다). 결과는 **인원 삭제가 아니라 피해** — `BodyPartSystem.TakeDamage`를 여러 번 나눠 때리고 기절/사망은 기존 체계가 정한다. 조우도 소굴 습격도 **같은 판정**(규칙이 둘이면 어느 쪽이 나은지가 계산이 아니라 눈치가 된다) |
| `WorldMap` | 추상 바둑판 월드맵. `GetTile` / `SetContent` / `RegisterSettlement` / `Neighbors` / `Distance` / `OnNodeChanged`. **매장 자원**: 칸마다 무엇이 얼마나 묻혔는지는 `WorldDeposits.At(node, tile, seed)` — 종류는 `BiomeContentData.Deposits`(바이옴), 수량은 **칸 좌표+시드 해시로 결정적**(같은 축으로 `TownMerchants`가 마을 상인을 고른다). 값은 항상 같고 **조사(`IsSurveyed`/`MarkSurveyed`)해야 보인다**(안개 아님 — 가려지는 건 세부 수치뿐). `ResourceSite` 칸은 ×2.5. **저작한다, 절차 생성하지 않는다** — `mapData`(`WorldMapData` SO)를 연결하면 그대로 싣고 크기·시작 노드도 데이터가 이긴다. 없으면 빈 맵 + 경고. 칸이 갖는 건 **바이옴(콘텐츠 게이팅) · 내용물**뿐 — 높이·생김새는 저작한 지형 몫이다. 저작은 `DungeonColony → World Map Editor`(브러시 3종). **안개 없음**(2026-06-20 결정) |

### 작업 자리 (`WorkSpot`) — 도착 판정은 두 갈래

**"거기 서서 하는 일이냐, 손만 닿으면 되는 일이냐"가 규약을 가른다** — 그리고 그건 **일감이 정한다**(`JobBase.Reach`, 기본 `StandReach`).
- **`StandReach` 0.9**(칸 반대각선 0.71) — 작업대·연구대·침대·건설·치료. 저작된 자리에 **실제로 선다**. 넓게 잡으면 폰이 두 칸 떨어져 허공에 대고 제작한다. 못 서면 교착이므로 **2초 동안 못 좁히면 `ReachRadius` 2.0에서라도 시작**하고, 그 판단은 **래치로 붙든다**(일을 시작하면 `ResetProgress`가 정체 타이머를 지워서, 안 붙들면 "일한다 ↔ 다시 걷는다"가 매 틱 진동. 목표 칸이 바뀌면 저절로 풀림).
- **`HandReach` 2.0** — 운반(`HaulJob`·`GroundHaulJob`·`UnloadJob`)·연료·수확·급수. **집고 내려놓을 뿐이라 그 자리에 설 이유가 없고**, 좁게 잡으면 같은 창고를 여럿이 쓸 때 서로 비켜서느라 오래 걸린다. `ReachRadius`와 같은 값이라 **정체 타이머를 안 기다린다**.
- **단계마다 다를 수 있어 상수가 아니라 프로퍼티다** — `ConstructionJob`·`TreatJob`은 창고에 들를 땐 손, 청사진·환자 앞에서는 자리.
- **자원 노드** = `NodeReach` 2.0으로 **둘러서서** 캔다(일감이 아니라 BT 잎이 직접 잰다). 저작된 자리가 없고 `MaxWorkers`만큼 붙으므로 한 칸에 모으면 서로 비켜서다 아무도 못 캔다.

일감의 목표 칸은 건물 자기 타일이 아니라 **폰이 서는 자리**다. `JobBase.SetSite(site)`가 `WorkSpot.TileFor(site)`로 한 번에 정한다 — 각 Job이 따로 계산하지 않는다. 순서: 저작된 `WorkSpot` 자식(그 칸에 설 수 있을 때) → `MapManager.FindWorkableNear(center, 반경, 발자국 제외)`(대각선). **건물이 깔고 앉을 칸은 후보에서 뺀다**(`IFootprintSite` — `PlacedBuilding`·`BuildingBlueprint`가 같은 `FootprintAt`으로 답한다). 다 지어진 건물은 그 칸이 이미 통행 불가라 저절로 빠지지만 **청사진은 일부러 안 막으므로**(짓는 동안 폰이 지나다녀야 한다) 따로 물어야 한다 — 안 물으면 폰이 3×2 건물 한복판에 서서 짓고 완성되는 순간 발밑이 막혀 **갇힌다**(길찾기는 출발 칸이 막힌 것을 풀어 주지 않는다). **폰이 선 자리에 건물이 들어서면 `PlacedBuilding.OccupyTiles`가 밖으로 내보낸다**(`CharacterController`를 끄고 옮기고 캡슐 절반만큼 띄운다 — 스폰 Y와 같은 함정). 즉시 배치는 폰을 전혀 안 보고(33종 중 28종이 즉시 배치, 여러 칸짜리는 전부 이쪽) 길찾기는 출발 칸이 막힌 것을 안 풀어 주므로, 이건 예외 처리가 아니라 **주 경로**다. **걷는 중인 폰은 건드리지 않는다** — 제 발로 빠져나가는데 순간이동시키면 가던 길이 끊긴다(단 "목표는 있는데 경로가 없는" 것은 걷는 게 아니라 막혀 선 것이라 내보낸다). 나갈 데가 없으면 그대로 둔다. **던전 핵의 `WorkSpot`은 원정 집결 원의 중심**이다 — 같은 규약을 쓰므로 집결 장소가 저작 대상이 되고 건설 모드에서 표식으로 보인다(핵은 씬 오브젝트라 `Resize Buildings And Rebuild Models`가 모델과 함께 세운다). **저작은 선호지 보장이 아니다** — 나중에 막히면 자동 탐색으로 넘어간다. 표식은 **건설 모드에서만** 보여서 그 칸을 막는 배치를 눈으로 피할 수 있다. 배선 = `DungeonColony → Setup Work Spots`.

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

**남의 배낭은 안 센다** — B를 조작하는데 A의 짐이 줄면 설명할 수 없다. 등록 시점엔 누가 할지 모르므로 `AvailableAnywhere`로 넓게 보고, `JobBase.CanAssignTo(unit)`가 폰별로 다시 거른다(`IsReady`와 다른 축). 적용처: `SeedJob` · `CraftJob` · `ResearchJob` · **`ConstructionJob`**.

**건설도 여기 있어야 한다**(2026-08-11에 들어왔다 — 그전엔 `Storage`만 봤다). 창고만 세면 둘이 따라온다: 상점에서 목재를 사 온 폰이 **그 목재로 집을 못 짓고**, **창고가 하나도 없으면 첫 창고를 못 짓는다**(창고도 청사진이라 자재를 가지러 갈 창고가 있어야 창고가 선다 — 새 정착지가 정확히 그 상태로 시작한다). 즉시 배치가 28종이던 동안엔 그 길로 우회됐고, 13종을 청사진으로 돌리면서 드러났다. `BuildingBlueprint`는 **배낭 변화를 이벤트로 못 듣는다**(`Storage.OnChanged`는 static인데 `Inventory.OnChanged`는 인스턴스 단위라 청사진마다 플레이어 폰 전부를 구독했다 끊어야 한다) — 2초 주기 점검으로 받는다.

**환불은 바닥이 아니라 창고로**(`Refund`) — 취소는 사건이 아니라 **무효화**다. 바닥에 떨구면 그 짐이 다시 운반 일감이 되고, 나르는 동안 창고 집계에서 빠져 있어 같은 작업이 "재료 부족"으로 또 죽는다.

**재료를 먹는 일감에는 반드시 `IsReady` + `CanAssignTo`를 같이 둘 것.** `IsReady`가 false면 큐에서 **대기**하지만, 게이트 없이 배정되면 `OnStart`가 `Done`으로 끝내 **예약 자체가 사라진다**(눌러 둔 연구가 저절로 풀린 것처럼 보인다).

### Storage API (자주 사용)

```csharp
Storage.GetTotalByItem(ItemData)         // 집계는 아이템 기준 하나뿐 (ResourceType판은 삭제)
Storage.ConsumeByItem(ItemData, amount)
Storage.FindNearest(pos, item, amount, quality)  // **실제로 받아 줄**(필터+자리) 가장 가까운 창고
Storage.Rebind(current, from, item, amount, quality)  // 목적지가 살아 있으면 그대로, 철거됐으면 다른 창고
Storage.Deliver(preferred, item, amount, quality, carrier, dropAt)  // **부린 짐이 증발하지 않게 하는 사슬**
storage.HasRoomFor(item, amount, quality)        // 필터 통과 + 넣을 자리 있음 (CanAccept만으로는 부족)
Storage.FindStorageWithItem(item, from)  // 그 아이템이 실제로 든 **가장 가까운** 창고 (자재·씨앗·약 수거)
Storage.NearestWhere(from, accepts)      // 조건에 맞는 가장 가까운 창고 — "훑기"는 여기 한 벌뿐
storage.CanAccept(ItemData)              // 필터 통과 여부
storage.TryAdd(item, amount)             // 필터 통과 시에만 입고
storage.AllowsEverything                 // 필터 둘 다 비었나 (= 전부 허용 정규형)
storage.AddFilter(ResourceGroup)         // 묶음 단위 허용 — 창고 패널 버튼이 다루는 것
storage.AddItemFilter(ItemData)          // 묶음과 별개로 콕 집어 허용 ("석탄 전용 창고")
Inventory.SplitInstance(inst, take)      // 뭉치 쪼개기 — 새 뭉치 반환(자리 없으면 null, **무변경**)
InventoryGridUI.FindCounterpart(mine, item)  // 같이 열려 있는 반대편 통(우클릭 보내기)
```

**필터 통과 ≠ 들어갈 수 있음.** 만적인 창고도 `CanAccept`는 참이다. 운반 목적지를 고를 땐 반드시 `FindNearest`/`HasRoomFor`(자리까지 봄)를 쓸 것 — `CanAccept`만 보면 폰이 거기까지 갔다 못 넣고 짐을 떨구고, 그 짐이 다시 같은 일감이 되어 **줍고 떨구기를 무한 반복**한다. 받아 줄 곳이 없으면 **일감을 아예 만들지 않고** 출처에 쌓아 둔다.

**바닥의 물건은 아무도 안 건드린다 — 체크한 것만 실려 간다**(`DroppedItem.HaulAllowed`, 기본 **꺼짐**, 2026-08-13). 켠 것에는 **초록 구슬**이 떠서 눈에 보인다. 켠 물건만 5초마다 다시 시도하므로 "창고를 비우면 저절로 실려 간다"는 그대로다.

**찍는 길이 둘** — 우클릭 한 개(**[창고로 옮긴다] ↔ [여기 둔다]**, 양방향)와 **운반 지정 모드(H)**. 모드는 **철거 모드와 같은 조작**이다(`HaulDesignator` — 클릭=단일 / 드래그=일괄 / 미리보기, 다른 건 색과 **우클릭이 지운다**는 것뿐). 우클릭만 있으면 전투 뒤나 발견 뒤에 열 몇 개를 하나씩 눌러야 한다. **끄면 가던 폰도 돌려세운다**(일감을 `Cancel` — 헛걸음을 아끼는 규약보다 "지금 그러지 마라"가 앞선다. 안 그러면 스위치가 거짓말이 된다).

전엔 **"누가 놓았나"로 갈랐다** — 손 드롭은 두고 사건 드롭(발견·철거 환불·작업대/수확 넘침)은 날랐다. 규칙 자체는 말이 됐지만 **화면에서 구별이 안 되고 스위치가 한 방향뿐이라**(켤 수는 있는데 끌 수는 없었다) "왜 이건 나르고 저건 안 나르지"가 **고칠 수 없는 것**으로 보였다. 기본을 전부 끄고 켜는 쪽만 두니 규칙이 한 줄이 된다.

**설비가 내놓는 것은 이 축과 무관하다** — 자원 노드·작업대 산출물은 바닥을 안 거치고 `HaulDispatcher`가 직접 일감을 낸다. **생산 라인은 그대로 자동이다.**

(손 드롭은 여전히 **커서가 UI를 완전히 벗어났을 때만** 떨어진다(`DraggableItem.TryWorldDrop`) — 전엔 격자만 봐서 장비 열·액션 바·패널 여백처럼 창 **안**의 격자 아닌 자리에 놓아도 바닥에 떨어졌다.) **반대로 "가지러 갈 곳"은 `FindStorageWithItem`** — 거기에 `FindNearest`를 쓰면 가득 든 창고가 오히려 탈락한다. **둘 다 거리를 본다**: 예전엔 이쪽만 위치를 안 받아 `All`에서 **처음 걸리는** 창고를 돌려줬고, 창고가 둘 이상이면 폰이 목재 한 짐·씨앗 한 줌에 맵을 가로질렀다(2026-08-12 수정). `from`은 **선택 인자가 아니다** — 기본값을 두면 부르는 쪽이 조용히 빠뜨린다(`ResourceNode.Work`에 델타를 못 빠뜨리게 한 것과 같은 이유). 기준은 **폰이 선 자리**지 일감이 매달린 건물이 아니다(걷는 것은 폰이다).

**부린 짐의 잔량 처리는 `Storage.Deliver` 하나다** — 지정 창고 → 받아 줄 다른 창고 → 배낭 → 바닥, **뒤로 갈수록 나쁜 쪽**. 배낭이 바닥보다 앞인 건 들고 있으면 짐내림이 옮겨 주지만 바닥에 떨구면 그게 다시 운반 일감이 되기 때문이다. 전엔 `HaulJob`·`GroundHaulJob`·`FarmHarvestJob`이 각자 적어 두고 **셋이 달랐다**(하나는 `FindNearest`에 수량·등급을 안 넘겼고, 하나는 `Inventory.TryAdd`로 **필터를 우회**했다).

**운반 목적지는 도중에 사라진다.** 일감을 만들 땐 있던 창고가 폰이 걸어가는 사이에 철거될 수 있다 — 안 보고 `_storage.transform`을 만지면 `MissingReferenceException`이 `UnitBrain.Update`까지 올라가 **그 폰의 BT가 통째로 멎는다**(`GroundHaulJob`·`HaulJob`이 둘 다 이랬다. 후자는 부릴 때만 봤고 목적지 칸을 정할 때는 안 봤다). `Storage.Rebind`로 갈아타고, **갈 데가 없으면 그냥 들고 있는다** — 이 일감들은 받아 줄 창고가 있어야만 만들어지므로 여기서 없다는 건 그새 철거됐다는 뜻이고, 이미 간 걸음을 버릴 이유가 없다(짐내림이 나중에 옮긴다).

**`amount`·`quality`를 빠뜨리지 말 것.** 기본값(1개·일반)으로 물으면 "한 개는 들어간다"만 확인하고 50을 지고 간다. 등급은 더 조용히 틀린다 — 스택은 **같은 등급끼리만** 합쳐지므로(`Inventory.TryAdd`), 등급을 빼먹으면 `일반 빵 30/99`가 든 만적 창고가 `희귀 빵`도 받는다고 답하고, 폰이 거기까지 갔다 못 넣어 **줍고 떨구기가 그대로 남는다**(`Inventory.CanFit`이 `TryAdd`와 같은 등급 검사를 한다). `FindNearest`는 **두 번 훑는다** — 통째로 받아 줄 곳이 먼저, 없으면 한 개라도 받는 곳. 그래서 넉넉히 물어도 일감이 사라지지 않는다.

### Job Categories & 배정 모델

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research, **Unload**(짐내림), **Medical**(치료), **Warden**(간수). **값은 뒤에 붙일 것** — 직렬화가 정수라 중간에 끼우면 `MinionData.AllowedJobs`가 통째로 밀린다.

**두 갈래** — `JobCategoryInfo`가 단일 출처.
- **큐 작업**(`QueueOrder`: **치료** → **간수** → 건설 → 농사 → 제작 → 연구 → 운반 → 짐내림 — 치료가 맨 앞인 건 피 흘리는 사람은 기다릴 수 없고, 그 일감은 다친 폰이 침대에 누웠을 때만 생겨 늘 있지도 않기 때문이다. 간수가 그다음인 건 **붙잡은 적이 깨어나면 걸어 나가기** 때문이다 — 늦으면 붙잡은 것 자체가 없던 일이 된다): 건물/농지/작업대가 일감 등록 → 폰이 집어감. **배열 순서 = 전역 우선순위**(모든 폰 공통). `QueueOrder`에 없는 카테고리는 큐에서 배정되지 않는다.
- **직접 배정**(Mining/Logging = 자원 노드, Guard = 순찰 구역): 플레이어가 대상에 폰을 붙임(`Blackboard.AssignedNode`/`AssignedPatrolZone`). 큐를 타지 않으므로 on/off 대상이 아님. **일감을 안 만든다** — `WorkNode.TickAssigned`가 배정된 노드를 직접 캔다(`MineJob`이라는 죽은 클래스가 오래 남아 있었는데, 리팩터링 때마다 같이 고쳐야 하는 값만 물어 2026-08-12에 지웠다).

Job score = `JobCategoryInfo.Weight(category) × 100 + job.Priority`.

폰별 설정은 **숫자 우선순위가 아니라 on/off** — `WorkSettings`(`UnitBrain.Work`, 플레이어 세력 일꾼에만 자동 부착). UI는 `ColonyUI`(P키). 상세 `docs/jobs.md`.

### Damage Quick Ref

```
DamageCategory: Physical / Elemental / Dark / Holy
PhysicalType:   Slash / Pierce / Blunt          (Physical일 때)
DamageElement:  None / Fire / Ice / Lightning / Poison  (비Physical일 때)
```

### 치료 — 다친 폰을 다른 폰이 돌본다

부상은 누워 있으면 저절로 낫는다(`RestNode`: 혈액 2/s · 캡 1/s · 부위 1/s). 그건 시간이 지나는 것이지 누가 무엇을 한 것이 아니라, **부상이 사건이 되지 못했다.**

- **침대는 회복을 얹는 것이지 조건이 아니다** — **폰을 직접 우클릭**해도 치료한다(`TreatJob(patient, manual)`). 전투 직후 쓰러진 사람을 그 자리에서 돌보는 것이 침대까지 끌고 가는 것보다 급하다. **자기 자신도 된다**(`[스스로 치료한다]`, `SelfPenalty` 적용) — 누운 채 저절로 도는 `SelfTend` 토글과 **다른 축**이다(토글은 "알아서 해라", 이건 "지금 해라"). **침대에 누운 환자면 침대 쪽 일감을 쓴다**(`Bed.FindOccupiedBy`) — 새로 만들면 같은 사람에게 둘이 붙고 침대가 제 일감을 지울 때 이건 안 지워진다. **기절한 폰은 남도 자기도 못 돌본다.**
- **우클릭으로 시킬 수도 있다** — `Bed.RequestTreatBy(doctor)` → `JobManager.ReserveFor`(등록 + 그 폰 앞으로 예약). `RequestJob`이 **이 폰이 예약한 일감을 먼저** 돌려주므로(`FindReserved`) 추첨을 건너뛴다. **작업 설정(on/off)도 안 본다** — 손으로 시킨 일은 "이 작업을 맡길까"의 결과가 아니라 지금 이 순간의 지시다(자원 노드 직접 배정과 같은 규칙). **새로 만들지 않고 있던 일감을 넘긴다**(침대가 주인이라 하나 더 만들면 같은 환자에게 둘이 붙는다), **이미 하고 있으면 안 뺏는다**(약만 버려진다).
- **`Bed`가 일감을 낸다**(`TreatJob`, `JobCategory.Medical`). 다친 폰 자신이 내면 "누가 등록을 지우는가"가 애매해진다 — 기절하거나 죽으면 스스로 못 지운다. 침대는 자리를 아는 유일한 물건이고, 작업대가 제작 주문을 내는 것과 같은 자리다.
- **누워 있을 때만** 생긴다. 환자가 돌아다니면 의사가 쫓아다녀야 하고, 침대 배정은 이 게임에서 이미 "쉬어라"라는 명령이다. 플레이어 세력만 돌본다.
- **한 회차 = 약 하나.** 시간을 재서 조금씩 얹는 대신 회차로 끊는 이유는 그래야 **소모품을 쓰는 자리**가 생기기 때문이다. 나을 것이 남았으면 `Bed`가 다음 회차를 다시 건다.
- **자기 자신은 못 고친다**(`CanAssignTo`) — 누운 폰이 제 일감을 집으면 침대에서 일어나 침대 옆으로 걸어간다.
- **`BodyPartSystem.Treat(partHeal, capHeal)`** — 부위 HP + **`BloodCap` 복구**(자연 회복이 못 하는 것) + **즉시 지혈**(`StopBleeding` — `LastWoundTime`을 과거로 민다). 혈액은 `degradeCap: false`로 채운다(캡을 되돌리는 일인데 깎으면서 되돌리면 제자리걸음).
- `NeedsTreatment`는 **파괴된 부위를 안 센다** — `HealBodyParts`가 건너뛰므로 세면 영원히 안 끝나는 일감이 된다(의족은 후유증 축의 일).

**의료용품은 등급(`ItemQuality`)이 아니라 별개 아이템이다**(`MedicineData`, JSON `"type": "medicine"`). 등급은 같은 물건이 잘 만들어졌느냐의 축인데, 찢은 천과 구급낭은 그 관계가 아니다. 따로 두면 레시피·재료·값이 각자 붙는다.

| ItemId | 이름 | 효능 | 속도 | 만드는 법 |
|---|---|---|---|---|
| (없음) | **맨손** | 0.4 | 0.55 | — 도구를 요구하면 물자가 끊길 때 치료가 통째로 멎는다 |
| `med_rag` | 넝마 | 0.75 | 0.8 | 천 1 → 3 (노동) |
| `med_bandage` | 붕대 | 1.0 | 1.0 | 천 2 → 2 (노동) |
| `med_poultice` | 약초 습포 | 1.6 | **0.75** | 약초 3 + 붕대 1 → 2 (연금) |
| `med_kit` | 구급낭 | 2.3 | 1.4 | 붕대 3 + 약초 2 + 정수 1 (연금) |

효능과 속도를 **따로** 둔 이유: 약초 습포처럼 **잘 듣지만 손이 많이 가는** 것이 있어야 고르는 일이 된다. **가장 좋은 것을 쓴다** — 아껴 쓰면 애써 만든 구급낭이 영영 상자에 남는다. 무엇을 쓸지는 **무엇을 쟁여 두느냐**로 이미 조절된다.

**약은 손에 든 것만 쓴다**(`Treatment.BestInBackpack`/`ConsumeFromBackpack`). 창고 것을 원격으로 소모하면 붕대가 손에 없는데 감기는 꼴이 된다. 그래서 `TreatJob`은 **2-phase**다(`ConstructionJob`과 같은 패턴): 배낭에 약이 있으면 바로 환자에게, 없으면 **창고에 들러 챙긴 뒤**(한 번에 5개 — 회차마다 왕복하면 붕대 하나 감는 데 맵을 가로지른다) 환자에게. 남은 것은 다음 회차에 그대로 쓰이고, 다 낫고 남으면 `UnloadJob`이 창고로 돌려놓는다. **배낭에 있으면 더 좋은 것을 찾아 창고에 가지 않는다** — 그동안 환자는 피를 흘린다.

**자가치유(`WorkSettings.SelfTend`, 기본 끔)는 일감이 아니라 `RestNode` 안에 있다.** BT 순서가 `Rest → … → Work`라 침대에 배정된 폰은 `RestNode`에서 Running으로 멈춰 **`WorkNode`까지 가지 않는다** — 일감으로 만들면 아무도 못 집는 일감이 된다(림월드도 자가치유는 누운 채로 한다). 누워 있는 사람은 창고에 손을 못 뻗으므로 **제 배낭에서만** 약이 나오고, `Treatment.SelfPenalty`(0.7)가 붙는다 — 한 손으로 안 보이는 곳을 아픈 채로 싸맨다. **기본이 꺼져 있는 이유는 약을 축내서다**(림월드와 같다) — 의료가 낮은 폰이 구급낭을 헐어 쓰는 것보다 의사를 기다리는 편이 대개 낫고, **켜진 줄 모르는 채 물자가 새는 것**이 가장 나쁘다. 켜는 자리를 둔 이유는 **의사가 없을 때**(혼자 다니는 방랑자, 원정에서 돌아온 폰). P키 패널에서 짐내림 옆.

**회복량·속도·약 고르기는 `Treatment`가 단일 출처다** — 치료하는 길이 둘(의사·자가)이라 양쪽에 따로 적으면 반드시 어긋난다.

`StatName.Medicine`(의료)이 회복량과 속도를 함께 정한다(`0.5 + 의료/100`) — 그전까지 이 스탯은 이름과 설명만 있고 **아무 데서도 안 읽혔다.**

### 기절한 폰은 아무도 안 때린다 — 지목한 하나만 예외

`BTTargetUtil.IsAttackable`이 기절(`IsKnockedOut`)·시체를 **모든 자동 경로**(탐지·교전·반격)에서 뺀다. **KO가 사형선고가 아니라 회복 가능한 상태**여야 하기 때문이다(켄시식) — `BodyPartSystem.UpdateKnockOutRecovery`가 최소 시간 + 코어 부위 회복 + 혈액 기준으로 일으켜 세운다.

그래서 **때려눕힌 적은 내버려 두면 일어난다.** 확실히 죽이려면 우클릭 **[공격한다]**(문구는 서 있는 적과 같지만 안에서 `FinishWith`로 갈린다) — `UnitBlackboard.FinishTarget`에 그 대상을 지목하면 `IsAttackable(go, bb)`가 그 **하나만** 통과시킨다. 규칙을 고치는 대신 예외를 뚫는 이유: 자동으로 뚫리면 KO 회복이 사실상 사라지고, 그렇다고 길이 아예 없으면 이겨도 전투가 안 끝난다.

**예외는 한쪽 방향뿐이다** — 지목은 `MoveController`(플레이어 우클릭)에서만 붙으므로 **쓰러진 우리 폰은 적에게 여전히 안 맞는다**. 지목은 대상이 죽거나(`FightNode`) 다른 명령이 오면 거둬진다.

**쓰러진 놈은 막지도 피하지도 못한다** — `CombatHandler.TryBlock`/`TryBlockRanged`/`TryDodge`가 `IsDown`이면 즉시 false. 방어도 회피도 능동적인 동작이다. 회피를 `Evasion`으로 굴리면 회피 높은 폰은 기절해서도 잘 안 맞아 **눕힌 보람이 사라진다**(빗맞음을 넣고 싶으면 방어자 스탯이 아니라 고정 확률이어야 한다).

**시체는 죽은 그 순간에서 멈춘다** — `BecomeCorpse`가 `UnitBrain`·`PawnMover`·`CombatHandler`에 더해 **`BodyPartSystem`·`StatusEffectSystem`도 끈다**(안 끄면 시체의 혈액이 계속 줄거나 차고, 계속 불탄다). **`Faction` 컴포넌트는 지우되 소속은 `Corpse.Faction`이 기억한다** — 지우는 건 자동 탐지(`OverlapSphere` + `GetComponent<Faction>`, `enabled=false`로는 못 막는다)를 끊으려는 것이지 잊자는 뜻이 아니다. 그래서 **"모르는 것은 플레이어가 아니다"** — `faction == null`을 플레이어처럼 대하면 시체가 그 구멍으로 들어온다(허기 바가 떴었다).

### 업기와 가두기 — 어깨에 사람을 멘다

**붙잡은 적을 끌고 오는 것과 동료를 옮기는 것은 같은 동작이다**(`PawnCarry`) — 규칙을 둘로 나누면 하나가 어긋난다. **우리 편은 멀쩡해도 업고**(급히 옮기는 것은 명령이고, 받는 쪽이 우리 폰이면 싫다고 할 이유가 없다), **남은 기절해야 업는다**(멀쩡한 적을 메는 건 붙잡기가 아니라 제압이고, 클릭 한 번에 데려오면 싸울 이유가 사라진다). 깨어 있는 사람은 **세워서** 내려놓는다.

**시체도 같은 동작으로 치운다**(2026-08-13). 전엔 "시체는 다른 축(뒤져서 물건만 꺼낸다)"이었는데, 그러면 싸움이 끝난 자리에 시체가 영영 쌓이고 치울 길이 없다 — 메고 걸어가 내려놓는 것은 산 사람을 옮기는 것과 **글자 그대로 같은 일**이라 규칙을 따로 만들 이유가 없다(무게도 한 손 제약도 그대로 걸린다). **늘 업을 수 있다** — 죽은 자에게 기절·소속 판정은 뜻이 없고(`Faction`은 지워졌고 `IsKnockedOut`은 죽은 방식에 따라 들쭉날쭉하다) 안 가르면 답이 흔들린다. **자리에는 안 들어간다** — 침대도 우리도 안 받는다(`Bed.Accepts`·`PrisonCage.TryPut`): 눕히는 건 회복을 얹는 일이라 뜻이 없고, 넣어 두면 그 자리를 산 사람이 못 쓴다. **간수도 안 데려간다**(`PrisonEscort.IsCaptive`가 시체를 뺀다 — 안 빼면 시체를 업은 폰이 저 혼자 우리로 걸어간다). 시체인지는 **`PawnCarry.IsCorpse`가 답한다** — `BecomeCorpse`는 `UnitBrain`을 **끄기만** 하므로 `== null`로는 산 사람과 구별되지 않는다.

**그 "끄기만 한다"가 우클릭을 죽여 놨었다** — `MoveController`의 분기가 `if (brain != null) … else if (corpse != null)` 순서라 **시체 가지가 한 번도 안 돌았다**: 시체를 우클릭하면 [뒤진다] 대신 `따라간다`·`공격한다`가 떴다(따라갈 수도 칠 수도 없는 것에게). 좌클릭(`SelectionManager`)은 시체를 먼저 봐서 멀쩡했다 — **같은 물음에 두 곳이 다른 순서로 답하고 있었다.** 죽은 것은 무엇보다 먼저 시체다.

**두 팔로 안지 않고 한쪽 어깨에 멘다.** 그래야 값이 둘 붙는다:
- **무게** — `CarryCapacity.Load`가 업은 사람을 같이 센다(`PawnCarry.BodyWeight` 60 × 몸집 + 그 폰의 짐). 한도가 `10 + 근력 × 0.5`라 **근력 100쯤에서 같아진다** — 근력 10은 속도 25%로 기어가고 130은 거의 멀쩡하다. **업고 나르는 것이 힘센 폰의 일**이 된다. `PawnMover`·`CombatHandler`가 배낭 무게를 직접 세던 것을 `CarryCapacity.Load`로 돌렸다(한 곳만 고치면 이동도 공격도 따라온다).
- **한 손** — 양손 무기(`IsLargeWeapon`)도 방패도 못 쓴다. `CombatHandler.Weapon`/`Shield` 프로퍼티 **하나로 모았다**(여덟 군데에서 묻고 있었다 — 한 군데만 빠뜨려도 "업고도 대검을 휘두르는" 구멍이 남는다). **장비를 벗기지는 않는다**(벗기면 돌려 입힐 길이 필요하고 배낭이 차 있으면 샌다).

**가두는 곳은 둘, 갇힌 상태는 하나.** 문도 방도 없으니 켄시처럼 **자리에 묶어 둔다**.
- `PrisonCage`(우리) — 가두는 것이 전부라 싸다. 한 명. 생성기 `Create Prison Cage`
- `Bed.ForPrisoners`(죄수 침대) — **건설 목록에 별개 항목이 있고**(`PrisonBed.prefab`/`.asset` — 침대 프리팹 복제 + 플래그, 값·크기 동일. 생성기 `Create Prison Bed`는 없을 때 다시 만드는 복구용) **우클릭으로 서로 바꾼다**(림월드식). 잡아 온 뒤 방을 돌리는 것과 애초에 감옥을 짓는 것은 다른 일이라 둘 다 둔다. **쓰고 있으면 안 바꾼다**. **`Bed.Update`가 갇힌 포로도 돌본다** — 이게 죄수 침대의 존재 이유다(안 돌보면 우리와 같은 물건이 된다). 지나가던 적은 여전히 안 돌본다. 눕힐 때 **점유와 배정을 같이** 한다(하나만 하면 패널이 "배정 없음"이고 `ReleaseFrom`이 거두는 짝이 어긋난다)
- **보통 침대는 붙잡은 사람을 안 받는다**(`Bed.Accepts`) — 받으면 갇힘이 안 걸린 채 자세만 눕고 다음 틱에 **누운 채로 걸어 나간다.** 붙들자니 보통 침대가 곧 죄수 침대가 되고 안 붙들자니 눕힌 것이 아무 뜻이 없다. 우클릭 선택지·도착 판정 둘 다 이걸 본다
- **용도는 매트리스 색으로 보인다** — 죄수용 바랜 주황 / 보통 바랜 초록. **매트리스 자체를 물들인다**(`tintTarget`, 비우면 이름에 `Mattress`가 든 자식 → 없으면 가장 큰 렌더러). 표식 quad를 얹어 봤다가 걷어냈다: 바닥에 깔면 침대 모델이 덮어 **한 번도 안 보이고**, 위로 올리면 침대에 스티커를 붙인 꼴이며, 자식이 늘어 `WorldBounds`가 커져 **폰이 침대 위에 떠서 잔다**(`SleepPosition` = 윗면 + `lieHeight`). **쨍하게 칠하지 않는다** — 자기를 봐 달라는 UI가 아니라 침대 천의 색이다
- **색은 코드가 아니라 에셋이 든다**(`Bld_5C8057`·`Bld_9E6133`, 프리팹에 미리 물려 있음). 런타임에 하는 일은 **둘 중 하나를 골라 끼우는 것**뿐(`sharedMaterial`). 코드가 `rend.material`로 칠하면 셋이 딸려 온다: ① **에디터에서 둘이 똑같아 보인다**(색이 게임 돌릴 때만 붙으니 프리팹을 열어도 구분이 안 된다) ② 침대마다 **머티리얼 인스턴스**가 하나씩 생겨 배칭이 깨지고 손수 `Destroy`해야 한다 ③ `OnEnable`이 **편집 중에도 불려서**(재컴파일·컴포넌트 추가) 씬에 이름 없는 머티리얼이 쌓인다 — 그래서 `Application.isPlaying` 가드를 달면 그 가드가 곧 ①이 된다. **저작하면 셋이 한꺼번에 없어진다**
- **MPB는 안 쓴다** — 철거 예정 표시가 MPB로 덮었다가 `SetPropertyBlock(null)`로 되돌릴 때 **머티리얼 본래 색**으로 가므로, MPB로 칠하면 철거 모드에 한 번 스치는 것만으로 색이 사라진다
- **`Imprisonment` + `UnitBlackboard.IsPrisoner`가 유일한 상태다** — 가두는 물건마다 BT를 따로 멈추면 한쪽에서 포로가 걸어 나간다
- **갇힌 사람도 다시 업는다**(`[끌어낸다]`) — 되돌리는 길이 [풀어 준다] 하나뿐이면 자리를 못 바꾼다. 우리·침대에서 놓아 주는 건 `PawnCarry.TryPickUp`이 한다(업기 전에 늘 거치는 길이라 호출처마다 안 챙겨도 된다)
- **우리 콜라이더는 루트에** — `hit.collider.GetComponent<IWorldClickable>()`로 묻기 때문에 자식에 있으면 눌러도 아무 일도 안 일어난다(침대도 루트에 있다). 트리거이고 기둥에는 콜라이더가 없다(막으면 문 없는 상자가 된다)
- **눕히는 길은 `PawnMover.SetSleeping` 하나다** — 우리 폰이 제 발로 눕는 것(`RestNode`)과 죄수 침대에 눕히는 것(`Bed.PlacePrisoner`)이 같은 길을 쓰고, `Imprisonment.Hold`는 `IsSleeping`이면 손을 뗀다(**우리에 선 포로만** 붙든다). `Bed.ReleaseFrom`은 짝으로 `SetSleeping(false)` — 안 풀면 침대 밖으로 나가서도 누운 채 굳는다(`PawnMover.Update`가 통째로 멈춰 중력도 조향도 안 돈다)
- **기절 회복은 "지금 누울 자리가 있는지"를 직접 묻는다** — `BodyPartSystem`이 무조건 `_preKoRotation`으로 되돌리면 침대에 눕힌 포로가 다 나은 순간 **벌떡 일어선다.** 우리 폰도 똑같이 그랬지만 다음 틱에 `RestNode`가 도로 눕혀서 한 프레임짜리 튐이 안 보였을 뿐이다 — 포로는 BT가 안 돌아 그 복구가 없다. **되돌린 뒤 누가 다시 눕혀 주기를 기대하지 않는다**(그 "누가"가 없는 경로에서 반드시 드러난다). 깨어나는 것과 일어서는 것은 다른 일이다
- **새로 업으면 갈 곳은 백지다** — `PawnCarry.TryPickUp`이 `HasPutDown`/`PutDownBed`/`PutDownCage`를 지운다. 안 지우면 `[업는다]`만 시켰는데 폰이 **저 혼자 예전 우리로 걸어간다**. 업기가 시작되는 문이 그 함수 하나뿐이라 명령 내는 쪽마다 챙기게 하면 언젠가 빠뜨린다. `MoveController.MoveTo`도 같이 거둔다(딴 데로 가라는 명령 = 옛 명령 취소, `Target`·`FollowTarget`과 같은 줄). **도착해서 자리를 다시 보는 것**(`CarryPawnNode`)은 그와 별개의 안전망 — 걸어오는 사이 남이 먼저 넣었을 수 있다

**부모 크기를 상쇄하고 붙인다** — 그냥 자식으로 달면 업힌 사람의 월드 크기가 **업은 사람 크기만큼 곱해지고**, 내려놓을 때 `SetParent(null, true)`가 그 값을 굳혀 **업을 때마다 커진다**.

**이미 붙잡은 사람은 깨어나도 업는다** — 기절을 요구하는 건 *붙잡는 순간*의 규칙이지 붙잡아 둔 사람을 옮기는 데까지 걸 이유가 없다(회복해 눈뜨면 우리에서 꺼낼 길이 사라진다). 갇힌 사람의 선택지는 **끌어내기·치료·처형** 셋뿐이다(거래·따라가기는 빠진다).

**침대에 눕힐 때는 침대가 방향을 정한다**(`Bed.SleepRotation` — `RestNode`가 제 발로 누울 때 쓰는 것과 같은 값). 안 그러면 발이 베개를 벤다.

**업힌 사람과 갇힌 사람은 아무도 안 때리고 BT도 안 돈다.** 갇힌 사람은 **쓰러진 사람과 같은 규칙** — 자동에선 빠지고 `FinishTarget`으로 지목했을 때만 통과한다(처형하는 길). 업힌 사람은 **지목해도 못 친다**(어깨 위라 사거리가 업은 폰과 같아 노린 것이 짐인지 사람인지 구별이 안 된다). 다만 **직접 명령은 BT를 안 거치므로** `PawnMover.SetMoveTarget`에서도 막는다(기절과 같은 자리) — 안 막으면 갇힌 우리 폰이 우클릭으로 걸어 나간다. `MoveController.Rouse`도 갇힌 사람은 안 건드린다(침대에서만 놓아 주면 `IsPrisoner`가 남아 **자리 없이 갇힌 폰**이 된다). `Bed.ReleaseFrom`이 죄수 침대에서 놓아 줄 때 갇힘도 함께 푸는 것이 그 안전망이다. 업힌 쪽을 때리면 업은 폰을 노리는 것과 구별이 안 되고(어깨 위라 사거리가 같다), 갇힌 쪽을 때리면 붙잡아 온 뜻이 사라진다. **깨어나도 BT는 안 돈다** — 돌리면 눈뜨는 순간 걸어 나간다.

**업은 폰이 사라지면 산 사람이 딸려 간다** — 자식으로 붙이기 때문이다. `Die`와 `OnDestroy` **양쪽에서** 내려놓는다(파괴 경로가 `Die`만은 아니다). 갇힌 채 죽으면 우리 자리도 비운다.

조작: 사람 우클릭 `[업는다 (62kg)]`(몇 kg인지 적는다 — 업을지는 얼마나 느려지느냐가 정한다. **무게로 거르지 않는다**: 못 드는 게 아니라 느려질 뿐이고, 그건 명령이 조용히 거절할 일이 아니다) / 침대·우리 `[업은 사람을 여기 눕힌다·가둔다]` / **업고 있는 폰을 우클릭 → `[내려놓는다]`**. **맨바닥에서는 안 묻는다** — 사람을 업었다고 이동이 두 번 누르기가 되면 안 된다(업고 다니는 동안에도 가장 잦은 명령은 이동이다). 내려놓기는 걸어갈 필요가 없으므로 **일감을 안 걸고 즉시** 처리한다(BT를 거치면 다음 틱까지 아무 일도 안 일어난 것처럼 보인다).

**갇힌 사람 우클릭 = [소지품을 본다] · [풀어 준다] · [끌어낸다] · [치료한다] · [공격한다].** 소지품은 원정 배낭과 **같은 통로**(`InventoryUI.BindTo` + `ForceOpen`)를 쓰고, **닫으면 손을 뗀다**(안 풀면 다음에 I를 눌렀을 때 내 폰 대신 포로 배낭이 열린다 — 원정 돌려보기 `_cycle`은 예외). **놓아 준 적은 맵을 떠난다**(`LeaveMapNode` + `CombatStance.Flee`) — 성향만 바꾸면 구석에서 서성이다 그 자리에 적이 하나 남는다. 나가는 길은 **스폰과 같은 자**(`EventManager.TryFindExit` → `WalkToEdge`; 답이 두 벌이면 한쪽이 바닥 밖을 가리킨다). **단 물러섬은 반대다** — 스폰은 `spawnEdgeInset`(3칸) 안쪽에 세우지만(낭떠러지 위에 무리를 세우면 밀려 떨어진다) 나가는 길은 `insetOverride: 0`으로 **끝까지 간다**(안쪽에 멈추면 판 한가운데서 사라진다). 도착 판정도 0.9(칸 반대각선을 조금 넘는 값). 좁힌 대신 **8초 정체 시 거기서 떠난다** — 못 닿는 지형에서 영원히 서 있지 않게. **떠나는 사람도 자동 표적에서 뺀다** — 안 빼면 푸는 순간 우리 폰이 쫓아가 죽여 놓아 주는 것 자체가 성립하지 않는다(저쪽은 도주 성향이라 일방적인 사냥이다). **푸는 길은 `Imprisonment.Release` 하나**(우리·침대·사람 셋 다 여기로) — 각자 제 것만 풀면 어디서 눌렀느냐로 결과가 갈리고 자리도 안 비워진다.

**때리면 싸운다 — 확률로 굴리지 않는다.** ① 살려 보내는 선택의 값은 "안 때리면 안 싸운다"에 있고, **무를 수 있어야 선택이 진짜다.** ② 반격은 원래 세력 관계와 무관하게 `LastAttacker`로 도는데(중립을 때려도 되받아친다) 풀어 준 포로만 예외일 이유가 없다. ③ 15초(`RetaliationMemory`)가 지나면 `HasFreshAttacker`가 꺼져 **다시 떠난다** — 확률로는 못 만드는 "때리면 돌아서고 놓아두면 다시 간다"가 공짜로 나온다.

**갇힘은 세력을 안 가린다** — 적·NPC·하수인·방랑자 전부 같은 상태다. 그래서 둘을 챙긴다: **갇힌 폰은 세력을 안 가리고 굶고**(아래 "먹이기" — 업힌 폰만 예외다), **명령 목록에서 뺀다**(안 빼면 [치료한다]·[업는다]가 고르는 "가장 가까운 한 명"이 갇힌 폰일 때 아무도 안 간다). **다만 우클릭 창은 연다** — [풀어 준다]는 침대·우리가 하는 일이라 명령 받을 폰이 없어도 떠야 한다(막으면 마지막 한 명을 가둔 순간 풀어 줄 길이 사라진다).

### 회유 — 길은 둘, 게이지는 하나

**무엇이 되는지는 종족이 정한다**(2026-08-12 결정). 하수인 종족(`MinionData`)이면 회유해도 하수인이다 — 명령 못 받고, 안 자라고, **소환 한도를 차지한다**. 사람 종족이면 방랑자가 된다. 그래서 종족에 뜻이 생긴다(*고블린은 머릿수, 인간은 사람*) — 이 결정 덕에 구조를 안 바꿔도 됐다: 종족이 이미 답을 갖고 있어 `IsLocked`·명령 제외·라벨이 저절로 맞는다.

- **회유는 우리에서만 한다.** 죄수 침대에서도 되게 했더니 **두 건물이 같은 물건**이 됐다(침대가 회복까지 얹어 주니 우리를 지을 이유가 없다). 갈랐다: **우리 = 돌려세우는 자리 / 죄수 침대 = 살려 두는 자리** — 다친 포로는 눕혀 낫게 한 **다음 우리로 옮긴다**([끌어낸다])
- **갇힌 사람은 자리가 자세를 정한다**(`UnitBlackboard.PrisonPose`) — 침대면 `SleepRotation`, 우리면 서 있는 자세. 안 들고 있으면 `BodyPartSystem`이 기절에서 깨울 때 **기절 직전 자세**로 되돌려 **침대에 눕혀 둔 포로가 벌떡 일어선다**(깨우는 쪽도 갇힌 사람이면 자세를 안 건드린다)
- **자리는 도착해서 다시 본다** — 고를 때 비어 있어도 걸어오는 사이에 찰 수 있다. 안 보면 **한 우리에 여럿이 겹쳐 서고**(등록은 하나뿐) 나머지는 갇히지도 않는다. 자리가 없으면 거기 두지 않고 발밑에 내려놓은 뒤 알린다
- **사람은 사람이 설득한다**(`PersuadeJob`, `JobCategory.Warden`) — 폰이 가서 회차마다 올린다. 우클릭 `[설득한다 (40%)]`이면 그 폰만(`_speaker`), 우리가 저 혼자 낸 것이면 간수를 켜 둔 아무나. **일감의 주인은 `PrisonCage`다**(`Bed`가 `TreatJob`을 내는 것과 같은 자리) — 누르는 사람마다 하나씩 만들면 **여럿이 동시에 붙어 게이지가 사람 수만큼 빨리 찬다**(실제로 그랬다). 자리가 하나면 일감도 하나고, 우클릭은 `RequestPersuadeBy`로 **있던 일감을 넘긴다**
- **한 회차 = 말하는 시간 + 굴림 + 쉬는 시간.** 셋이 다 있어야 한다. `PersuadeSeconds`(말하는 시간)만 있으면 설득이 **쉼 없이 이어지는 한 줄**이라 간수 하나가 우리 앞에 붙박인다 — `PersuadeCooldown`(**180초**)이 그 사이를 연다. **이 값이 회유 전체 길이의 축**이다(회차 수 × 이 값 ≈ 이틀) — 20초였을 때는 회유 한 판이 몇 분 만에 끝나 붙잡아 두는 일에 아무 무게가 없었다. 바꾸면 `PersuadeGain`도 같이 옮길 것. **쿨타임이 지난 다음에 우리가 일감을 낸다**(`IsReady`로 막으면 큐에 남아 폰이 그 앞에서 기다린다 — 쿨타임을 둔 이유가 사라진다). 쿨타임은 **설득당하는 쪽**이 든다(`UnitBlackboard.NextPersuadeTime`) — 정적 표는 죽은 폰의 항목이 남아 새고, 무엇보다 사람을 바꿔 가며 누르면 쉬지 않고 도는 것이 된다
- **말이 안 먹힐 수도 있다** — `PersuadeChance`(인지력 0이면 40%, 130이면 90%). 실패하면 게이지가 안 오른다. 여기 확률을 둔 것도 "모르게 굴리지 말자"와 안 어긋난다: 결과가 그 자리에서 글자로 뜨고(`설득 성공`/`설득 실패`, `GradeUpNotificationUI.Popup` — 등급 승급과 **같은 자리 같은 모양**), **잃는 것은 시간뿐**이다. **실패해도 인지력은 는다**(성공에만 붙이면 인지력 낮은 폰은 늘지도 않아 영영 못 한다). 굴림도 알림도 `Conversion.PersuadeRound` 하나가 한다 — 일감 안에 적으면 설득 경로가 늘 때 성공률과 문구가 두 벌이 된다
- **하수인 종족은 던전 핵이 지배한다**(`Conversion.TickCore`, `DungeonCore.Update`) — 폰이 갈 필요 없이 **핵 영역 안** 우리에 가둬 두면 저절로 찬다. 핵이 하수인을 소환하는 물건이니 같은 종족을 붙들면 그 아래로 들어오는 것이 자연스럽다
- **지배는 도박이다 — 게이지가 곧 결과가 아니다.** 다 차면 **한 번 시도할 수 있게** 되고(`TryDominate`), 성공하면 하수인, **실패하면 머리가 버티지 못해 죽는다**. 확률은 `DungeonCoreData.DominateChance`(붙잡힌 쪽의 성질이 아니라 **지배하는 핵의 힘**). 설득(사람)은 반대로 다 차면 그대로 합류한다 — 공들인 것이 결과다
- **여기 확률이 있는 건 "확률로 굴리지 말자"와 안 어긋난다** — 그때 막은 건 **모르게** 굴리는 것이었다. 이건 성공률과 "실패하면 죽는다"를 적어 두고 사람이 누른다: **굴림이 곧 결정**이다
- **핵이 채우는 속도는 누구든 같다**(`CoreSeconds` **1800초 = 하루**). 설득(≈2.2일)보다 빠른 건 폰이 갈 필요가 없어서고, 대신 다 차도 **도박 한 번**이 남는다. 4분이던 시절엔 하수인 종족을 잡아 오는 것이 사실상 즉시였다. **자리가 없으면 멈추고 그 이유를 화면에 적는다**(`CoreBlockedReason`)
- **설득은 버티는 쪽 인지력이 늦춘다** — `PersuadeGain = 0.11 / (1 + 포로인지/130)`. 머리가 좋은 사람은 좀처럼 안 넘어온다(설득하는 쪽과 **같은 스탯을 마주 세운다**). 한때 강인함으로 버티게 했다가 걷어냈었는데("저 놈은 왜 느리지"를 스탯 창을 열어야 알았다), 이제 **포로 패널이 회유·탈출 게이지를 둘 다 띄우고** 갇힌 사람도 눌러서 스탯을 볼 수 있어 그 반대 이유가 사라졌다
- **설득하는 쪽은 `StatName.Warden`(간수)으로 잰다** — 성공률(`PersuadeChance` 40~90%)과 말 붙이는 시간(`PersuadeSeconds`)에 들고, 회차마다 그 스탯이 자란다. 전엔 인지력을 빌려 썼는데 그러면 **학자가 곧 간수가 된다**(연구를 시키려고 올린 스탯이 설득까지 잘하게 하고, 반대도 마찬가지). 작업 카테고리마다 노동 스탯이 하나씩 있는데(치료=의료, 연구=연구, 채굴·벌목·요리…) **간수만 없었다**. **`StatName`은 정수로 직렬화되므로 반드시 뒤에 붙일 것**
- **한 스탯이 두 가지를 정하지 않는다** — 간수 솜씨는 "먹히나"만, 포로 인지력은 "얼마나 오르나"만. 예전엔 설득자 인지력이 둘 다 정해서 **같은 값이 두 번 곱해져** 차이가 다섯 배 넘게 벌어졌다. 실측 범위 **1.0일**(간수 130이 인지 0을 설득) ~ **4.9일**(그 반대), 중간이 **2.2일**
- 합류할 때 `Imprisonment.Release`를 **안 쓴다** — 그건 적이면 도망 성향을 씌운다. 나가는 게 아니라 들어오는 것이다

**`UnitBrain.ChangeFaction`이 세력을 바꾸는 유일한 길이다.** `IsLocked`·BT·`WorkSettings`가 전부 `Awake`에서 정해지므로, `Faction.SetData`만 부르면 **적 트리를 그대로 든 아군**이 된다.

**`DungeonCore.CurrentMinionCount`는 `Faction.All`을 훑어 셀 때마다 다시 센다** — 이벤트로 더하고 빼면 **그 문을 안 지나는 변화**를 놓친다(회유는 스폰된 적이 없어 안 세어지고, 원정 나간 하수인은 죽지 않아 남는다 — 후자는 이미 새고 있었다).

### 간수 (`JobCategory.Warden`) — 붙잡은 사람을 자리에 넣고, 말을 붙인다

간수가 하는 일은 셋: **설득 회차**(`PersuadeJob`, 위) + **붙잡은 사람을 자리로 데려가기**(`PrisonEscort`) + **먹이기**(`FeedJob`, 아래).

- **데려가기는 일감이 아니라 업은 폰이 한다.** 일감으로 만들면 "누가 업을 것인가"부터 정해야 하는데 업는 것은 이미 명령이고(`[업는다]`), 업은 다음 다른 폰이 넘겨받으려면 어깨에서 어깨로 옮기는 동작이 필요하다. 간수 설정이 정하는 건 **그 걸음을 걸을까**다 — **켜져 있으면** 붙잡은 사람을 업는 순간 자리로 데려가고(전투가 끝나면 저절로 정리된다), **꺼져 있으면 계속 업고만 있는다**
- **`EscortDest`는 자리가 아니라 종류다**(`None`/`Cage`/`Bed`, `UnitBlackboard.EscortTo`). `PutDownBed`/`PutDownCage`와 **다른 축** — 저쪽은 우리·침대를 우클릭해 고른 **그 한 자리**고 이쪽은 "우리에 넣어라"다. 종류로 두면 걸어가는 사이 그 칸이 차도 다음 칸으로 갈아타고, 여럿을 동시에 나를 때 서로 같은 칸을 노리지 않는다. `CarryPawnNode`가 **매 틱 `PrisonEscort.Resolve`로 다시 정한다**
- **자동 판단은 다쳤으면 죄수 침대, 아니면 우리** — 우리는 돌려세우는 자리고 침대는 살려 두는 자리라, 피 흘리는 사람을 우리에 세워 두면 회유가 시작되기 전에 죽는다. 원하는 쪽이 없으면 있는 쪽으로 간다(길에 눕혀 두는 것보다 늘 낫다)
- **우리 폰은 `IsCaptive`가 아니다 — 갇혀 있어도.** 안 그러면 동료를 침대로 옮기다 우리에 처넣고, [끌어낸다]로 꺼낸 우리 폰을 간수가 도로 집어넣는다. 우리 편을 가두는 건 늘 손으로 시키는 일이다
- **`EscortTo`는 업기를 거쳐도 살아남는다**(`[우리로 옮긴다]`는 업기와 데려가기가 한 동작). 그래서 `PawnCarry.TryPickUp`이 지우는 `PutDown*`과 달리 여기서 안 지운다 — 대신 **`PawnCarry.PutDown`이 지운다**(업기가 끝나는 유일한 문)
- **명령의 단일 출처는 `PrisonEscort.CanOrder`/`Order`** — 문이 둘이다(우클릭 `[우리로 옮긴다]`·`[죄수 침대로 옮긴다]` / `InspectPanel` 버튼). 각자 적으면 한쪽만 "이미 업고 있으면 그 폰에게 시킨다"를 빠뜨린다. **빈 자리가 없으면 선택지를 안 띄운다**(눌러도 아무 일이 안 일어나면 명령이 먹혔는지 알 길이 없다)
- **손으로 시킨 것은 간수 설정을 안 본다** — 자원 노드 직접 배정·`[치료한다]`와 같은 규칙
- **`InspectPanel`이 폰에게도 동작 줄을 낸다** — 이 둘뿐이다. 쓰러진 적을 좌클릭해 살펴본 다음(무엇을 지녔나, 얼마나 다쳤나) 바로 정하는 흐름이 실제로 있어서다

아직 없는 것: **필드 몸값**.

### 탈출 — 게이지 둘의 경주

**`PrisonBreak`가 규칙의 단일 출처.** 그 전까지 갇힘은 **영구**였다 — 한 번 가두면 회유가 끝날 때까지 아무 일도 안 일어나서 **붙잡는 것이 공짜였다**. 간수를 둘 이유도, 회유를 서두를 이유도 없었다.

**확률이 아니라 게이지다**(`UnitBlackboard.EscapeProgress`). 처음엔 1분마다 굴렸는데, 그러면 **어느 날 포로가 사라지고 그게 관리를 안 해서인지 운인지 알 길이 없다** — 이 저장소가 여러 번 적어 둔 "모르게 굴리지 말자"의 그 꼴이다. 게이지로 두면 셋이 풀린다: 위험이 **눈에 보이고**, 간수를 켜면 느려지는 것이 **보이며**, **`ConvertProgress`와 경주**가 된다. **관리가 그 경주를 정한다.**

**월드맵 포로의 하루 굴림과는 축이 반대다**(`WorldBandManager.RollEscapes`) — 저쪽은 **안전장치**고(원정대를 다 잃으면 되찾을 길이 막다른 길이 되니 언젠가는 돌아온다) 이쪽은 **값**이다(안 지키면 잃는다).

| 상태 | 다 차기까지 | 회유(≈2.2일)와의 승부 |
|---|---|---|
| 지키고 · 먹임 | 7.5일 | 회유 승 — 넉넉하다 |
| 지키고 · 굶김 | 3.75일 | 회유 승 — 아슬하다 |
| 안 지킴 · 먹임 | **1.5일** | **탈출 승** |
| 안 지킴 · 굶김 | **0.75일** | 탈출 승 |

- **간수는 켜고 끄는 것으로 본다**(`PrisonEscort.AnyWarden`, ×0.2). "누가 지금 우리 앞에 서 있나"는 안 잰다 — 간수는 회차마다 왔다 가는 일이라 어느 순간을 재느냐로 답이 흔들리고, 그러면 화면에 이유를 적을 수가 없다.
- **굶기면 배로 빨라진다**(×2). 굶주린 사람이 **필사적**이 되는 편이, 약해져서 못 도망치는 것보다 값이 분명하다 — 후자면 굶기는 것이 최적 전략이 되어 먹이기를 넣은 취지와 정반대가 된다.
- **기절해 있으면 안 찬다.** 속도를 깎는 것보다 규칙이라 화면에 적기 쉽다. **다친 정도는 안 본다** — 넣으면 치료가 벌칙이 된다("살려 놓으면 도망간다").
- **우리 폰은 안 도망친다**(`PrisonEscort.IsCaptive` — "붙잡은 사람"의 단일 정의). 도망칠 곳도 없고(`Release`가 우리 폰에겐 도주 성향·맵 이탈을 안 씌운다) 알림만 거짓말로 남는다.
- **자리를 옮겨도 게이지는 안 줄어든다.** 되돌리면 업었다 도로 넣기를 되풀이해 영영 안 도망가게 만들 수 있다 — 지우는 자리는 `Imprisonment.Unbind` 하나뿐이다(풀려나거나 넘어왔을 때).
- **델타는 `PrisonBreak`가 스스로 잰다**(`LastEscapeTick`). 부르는 간격을 인자로 받으면 자리마다 주기가 달라질 때 속도가 조용히 갈린다. 기절해서 안 차는 동안에도 **시각은 갱신한다** — 안 그러면 깨어나는 순간 밀린 시간이 한꺼번에 들어온다.
- **되잡는 길은 이미 있다.** 도망치는 포로는 `LeavingMap`이라 자동 표적에서 빠지지만 우클릭 [공격한다]로 지목해 때려눕힐 수 있고(`FinishTarget`), 눕히면 `PawnCarry.CanCarry`가 통과해 다시 업어 가둘 수 있다 — 규칙을 새로 만들지 않아도 고리가 닫힌다.
- **나가는 길은 `Imprisonment.Release` 하나**(`reason`만 다르다) — 자리 비우기·갇힘 풀기·도주 성향·맵 이탈이 전부 거기 있다.
- **화면에 뜬다** — 탈출 게이지 바 + **"이대로면 N일 뒤"**(`PrisonBreak.RateFor` 하나로 낸다 — 보이는 수와 실제가 갈리면 안 된다) + 팝업(`탈출!`) + 상단 알림.

**자리가 부르는 문은 `Imprisonment.TickHeld(site)` 하나다** — 먹이기·탈출을 자리마다 두 줄씩 부르게 하면 축이 늘 때 한쪽을 빠뜨린다.

### 먹이기 — 붙잡아 두는 데 값이 든다

**갇힌 사람은 제 발로 못 먹으러 간다** — 갇히면 BT가 안 돌아 `EatNode`도 안 도는데 허기는 BT 밖에서 깎인다. 그래서 한동안 **갇힌 폰은 아예 안 굶었다**(안 그러면 우리 폰이 먹을 길 없이 조용히 굶어 죽었다 — 규칙이 아니라 사고였다). 이제 남이 가져다 먹이므로(`FeedJob`) 되살렸다.

- **적 포로도 굶는다**(`Feeding.Simulates` — 플레이어 세력 ∥ **갇힌 사람은 세력을 안 가린다**). 그래야 **회유가 공짜가 아니다**: 예전엔 우리에 넣어 두기만 하면 게이지가 저절로 찼다. **업힌 동안은 아무도 안 굶는다**(남의 어깨 위에서 먹을 수도 먹일 수도 없고, 업기는 걸어가는 동안의 임시 상태다).
- **화면도 같은 답을 쓴다** — `InspectPanelUI`의 허기 바 게이트가 `Feeding.Simulates`다. 갈리면 가장 나쁜 쪽으로 나타난다: 포로는 굶는데 바가 안 떠서 **왜 죽는지 알 길이 없다**. 굶고 있으면 이유도 적는다(먹일 것이 없다 / 간수가 없다 / 오는 중).
- **일감의 주인은 자리다**(`PrisonFeeding` + `IPrisonSite` — `PrisonCage`·`Bed`가 구현). 설득은 우리에만 있어 `PrisonCage`가 직접 들어도 됐지만 **먹이기는 양쪽 다 필요하다**(우리는 돌려세우는 자리, 침대는 살려 두는 자리 — 어느 쪽이든 굶기면 안 된다). 자리마다 적으면 반드시 한쪽이 어긋난다. 걸고·넘기고·거두는 규칙 자체는 **`SiteJobSlot`**이 든다 — 치료·설득·먹이기가 같은 그릇을 쓰고, 자리가 답하는 건 **언제 필요하고 언제 걸어도 되는가**뿐이다.
- **한 끼 = 음식 하나**(`TreatJob`의 "한 회차 = 약 하나"와 같은 규약). 배낭에 있으면 바로, 없으면 **창고에 들러 3개 챙긴 뒤**(끼니마다 왕복하면 빵 하나에 맵을 가로지른다) 자리로. 아직 배고프면 자리가 다음 것을 건다. `Reach`는 `HandReach` — 창살 너머로 건넨다.
- **기절한 포로도 먹인다** — 허기는 기절 중에도 도는데(2026-07-26 결정) 안 먹이면 오래 쓰러진 포로는 반드시 굶어 죽는다.
- **설득보다 급하다**(같은 `Warden`이라 `Priority`가 가른다 — 6 vs 4). 굶어 죽으면 회유도 몸값도 없다.
- 우클릭 `[먹인다 (허기 42)]` — 간수를 꺼 뒀거나 지금 당장 먹여야 할 때. **손으로 시킨 일은 작업 설정을 안 본다**(`[치료한다]`·자원 노드 직접 배정과 같은 규칙). 일감은 **자리가 든 것을 넘긴다**(`RequestBy`).

**무엇을 먹는지는 `Feeding` 하나가 답한다** — 먹는 길이 둘이라(제 발로 `EatNode` / 남이 먹여 주는 `FeedJob`) 양쪽에 적으면 "포로만 다른 것을 먹는" 꼴이 된다. `Simulates` · `IsHungry` · `BestInBackpack` · `BestInStorage` · `ConsumeFromBackpack` · `Eat` · `HungerThreshold`(60).

**낭비가 가장 적은 것을 먹는다** — 허기는 100에서 잘리므로(`Eat`의 `Min`) 허기 80에 영양 50짜리를 먹이면 30이 사라진다. **안 넘치는 것 중 가장 큰 것**, 전부 넘치면 **가장 작은 것**. 규칙 하나가 두 가지를 한다: 낭비가 줄고 **애써 만든 요리가 저절로 아껴진다**(잡곡부터 나간다). 치료가 "가장 좋은 것을 쓴다"인 것과 반대로 보이지만 축이 다르다 — 약은 넘쳐도 버려지지 않는다. **잠근 뭉치는 안 먹는다**(`Treatment`와 같은 규칙).

**가장 가까운 창고부터 쓴다** — 밥은 모든 폰이 하루 두 끼씩 하는 일이라 그 왕복이 곧 콜로니의 시간이다. **어디로 갈지가 먼저, 무엇을 먹을지가 그다음** — 낭비는 영양 몇 점이지만 왕복은 그 폰의 시간 전부라, 아끼자고 더 먼 창고까지 걷게 하면 아낀 것보다 걸은 값이 크다. `Better`는 **고른 창고 안에서** 고르는 규칙으로 남는다. **약은 반대다**(`Treatment.BestInStorage`) — 효능 차가 크고(맨손 0.4 ↔ 구급낭 2.3) 드물게 쓰므로 **효능이 거리를 이기고**, 거리는 같은 약이 여러 창고에 있을 때만 갈린다.

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

**등급은 장비만 갖는다 — `ItemData.HasQuality`**(`WeaponData`·`ArmorData`만 참). "같은 물건이 얼마나 잘 만들어졌나"가 성립하는 것은 무기·방어구뿐이다 — 잘 만든 빵이나 잘 만든 붕대는 없고, **약의 효능 차이는 등급이 아니라 별개 아이템**이다(넝마 ↔ 구급낭). `CraftJob`이 장비에만 등급을 굴리는 것도 같은 규칙이고, 이제 그 물음에 `is WeaponData`가 아니라 **물건이 답한다**(`TypeName`과 같은 자리 — 밖에서 갈라 쓰면 종류가 늘 때 분기를 전부 찾아야 하고 하나 빠뜨리면 조용히 틀린다).

**`Inventory.TryAdd`/`CanFit`이 강제한다** — 등급 없는 물건의 등급은 `Common`으로 눌린다. 안 그러면 저작 실수 하나(상인 목록에 "희귀 붕대", 전리품 표에 "고급 빵")로 **같은 물건이 두 뭉치로 갈라진다**(스택은 같은 등급끼리만 합쳐진다) — 창고가 조각나고, 등급을 안 넘기고 꺼내는 코드에게 그 뭉치는 없는 것이 된다. **들어오는 문에서 한 번 누른다**: 읽는 쪽마다 묻게 하면 반드시 한 곳이 빠진다.

### 상인 시스템

**골드는 전역 잔고다 — 다만 동전은 세상에 남는다** (`Wallet`, 2026-08-10).

```
Wallet.Balance / Add / TrySpend(전액 아니면 0) / CanAfford / OnChanged
Wallet.Coin      // 레지스트리에서 ResourceGroup.Currency 자원 (씬 배열 금지 — 모드 아이템에서 빈다)
Wallet.IsCurrency(item)
```

**동전은 세상에 남되 "쓰는 물건"이다** — 배낭·창고·시체·작업대 어느 격자에서든 **더블클릭하면 잔고가 된다**(`InventoryUI.TryClaimCoins`, 모든 패널이 같은 `InventoryGridUI`를 쓰므로 한 곳만 고치면 다 된다). **자동으로 빨아들이지 않는 것이 요점**이다: 들어오는 족족 흡수하면 "어느 인벤토리가 우리 것인가"를 가리는 스위치가 필요해지고(적 주머니·시체는 빼야 한다) 시작 시 기존 동전을 걷는 전환도 있어야 한다. **누르는 사람이 곧 임자**로 두면 둘 다 필요 없다. 무게는 0 — 어차피 눌러 없앨 물건이라 지고 다니는 값을 물릴 이유가 없고, 무게가 있으면 전투 뒤 주운 동전으로 과적이 걸린다.

**지니는 것은 다섯째 축이다 — `LoadoutData`**(SO, `5_Data/Loadout/`, `LootEntry.cs`/`LoadoutData.cs`). 장비·소지품·동전 **전부** 여기 있고, **`UnitData`에는 아이템이 없다**(2026-08-11에 걷어냈다).

**종족에 두면 틀린다**: `Human` 하나를 산적·마을 사람·경비·상인이 같이 쓰는데 종족에 적으면 **인간이면 누구나 도끼를 차고 나온다**. 같은 `Goblin`을 쓰는 `Enemy_Goblin`(야생 도적)과 `Minion_Goblin`(내 하수인)도 가를 수 없다. **조합은 프리팹이 한다** — `UnitBrain.loadout`(비우면 아무것도 안 나온다).

한때 "몸에서 나오는 것(늑대 가죽)은 종족, 지니는 것(무기)은 역할"로 갈라 두 벌을 굴렸다. 서사로는 맞는 구분이지만 **기계적으로는 둘 다 "죽으면 나오는 아이템"**이라, "이 적이 뭘 떨구나"에 답하려면 에셋 둘을 열어 더해야 했다. **늑대 가죽도 `Loadout_Wolf`에 적는다** — 답이 두 벌이면 반드시 어긋난다(`CarryCapacity`·표시명·`MaterialPool`에서 이미 겪었다).

**시점이 `Loadout`과 `Loot`를 가른다**:
- **`Loot`(소지품) — 죽을 때** 굴려 시체 배낭에 넣는다(`DropLoot`, 동전 바로 옆). 살아 있는 동안 아무 차이를 안 만들기 때문이다. 미리 지고 다니게 하면 적재 무게·짐내림 같은 **살아 있는 폰의 규칙에 공짜로 걸린다**(적이 제 배낭이 무겁다고 느려지고, 아군이 되면 그걸 창고로 나르려 든다). `Chance` → `MinCount~MaxCount` 순으로 굴린다(개수 0을 확률로 쓰면 "10% 확률로 3~5개"를 적을 수 없다).
- **`Loadout`(장비) — 태어날 때** 입힌다(`ApplyLoadout`, `Start`에서 `OnUnitSpawned` **앞** — 구독자가 장비를 읽는다). 무기가 공격을, 갑옷이 피해 감소를 바꾸므로 살아 있는 동안 실제로 일한다 — 죽을 때 만들면 맨몸으로 싸운 적이 철갑옷을 떨군다. **배낭에 넣고 `Equipment.Equip*`으로 찬다**(바로 꽂는 길은 없고 있으면 안 된다 — 벗을 때 돌아갈 자리가 없는 장비가 생긴다). 못 찬 것은 배낭에 남아 그대로 전리품이 된다(양손 무기를 든 채 굴린 방패 등). **같은 부위를 여럿 적으면 나중 것이 이긴다**(밀려난 것도 전리품).
- **`CoinDropMin/Max`(동전)** — 등급에서 자동 산출하지 않는다. 그러면 강한 늑대가 금화를 물게 되고 저작으로 막을 길이 없다. `Loadout_GoblinRaider` 5~30.
- **`Item`(에셋) ∥ `ItemId`(문자열) + `Resolve()`** — `MerchantPoolEntry`와 같은 규약. **장비는 대부분 JSON이라 `ItemId`를 쓰게 된다**(2026-08-02 이관분은 에셋 참조로 못 가리킨다). `Item`을 직접 읽으면 JSON 아이템이 조용히 null이 된다. **`ItemId` 오타는 조용히 아무것도 안 나오는 것으로 나타난다.**
- `Equipment`는 `Bind()`로 **필요할 때 스스로 묶는다** — `Start`에만 두면 `ApplyLoadout`이 먼저 돌 때 `_inventory`가 null이라 장착이 **에러도 없이** 실패한다.
- **시체 배낭이 차면 넘친 만큼을 발밑에 내려놓는다**(`GiveOrDrop`). `TryAdd`의 반환값을 안 보면 조용히 증발해서, 굴림표를 아무리 저작해도 "가끔 안 나온다"로만 보이고 원인이 화면에 안 남는다 — `Equipment.ReturnToInventory`와 같은 관례. **차고 있는 장비는 격자를 안 먹으므로**(시체 창이 장비 슬롯을 따로 그린다) 넘치는 건 못 찬 나머지와 소지품뿐이다.
- 첫 벌은 `DungeonColony → Setup Pawn Loadouts`(`Loadout_GoblinRaider` 생성 + `Enemy_Goblin` 배선). 이후는 `Create → DungeonColony → LoadoutData`.

**`TradeAccount`는 남았지만 갈리는 것은 돈이 아니라 물건이다** — `GoldAmount`/`TakeGold`/`GiveGold`는 전부 `Wallet`(base 구현), 하위가 정하는 건 `Receive`(산 물건이 창고로 가느냐 배낭으로 가느냐)뿐이다. **`MerchantData.GoldItem`은 삭제했다**(2026-08-10): 값은 숫자고 지갑은 전역이라 상인이 "무엇이 돈인가"를 들 이유가 없고, 남겨 두면 안 채운 상인이 조용히 거래 불가가 된다(`TownMarket.GoldAt`도 같이 삭제).

**`ItemData.ResaleFactor`** — **되팔 때의 감가.** 1 = 시세대로, 낮을수록 손해. **상인이 아니라 물건이 든다** — 상인 쪽에만 두면 "이 상인은 인색하다"밖에 표현할 수 없어 그에게 광석을 팔아도 똑같이 깎인다. 값이 안 나오는 건 물건의 성질이다: 광석은 시세대로 받지만 쓰던 검은 중고다. 기본 1(자원·식량) / `WeaponData` 0.55 / `ArmorData` 0.5 → 상인 매입률과 곱해 **장비는 사는 값의 약 1/4.5, 자원은 1/2**로 팔린다. JSON 아이템도 `"type": "weapon"`이면 `WeaponData`로 만들어지므로 그대로 적용된다.

**ItemData.BaseValue**: 아이템 기준 가격(G). 인벤토리 툴팁에 "가치" 표시 + 상점 판매가 계산 기준. JSON은 `"baseValue"`. **안 적으면 0이고, 그러면 상인이 기본값 10G로 판다** — 허브도 철검도 10G가 된다.

**MerchantData** (SO, `Create → DungeonColony → MerchantData`):
- `MerchantType`: General / Weapon / Armor / **Material** / **Food**. **한 마을에 종류당 하나씩 선다**(`TownMarket`)이므로 이 enum은 곧 마을 시장의 **줄 목록**이다 — 값을 늘리면 상인이 한 명 더 설 자리가 생긴다. 값은 뒤에 붙일 것(직렬화가 정수라 중간에 끼우면 기존 에셋이 밀린다)
- `DailySlotCount`: 하루 진열 종류 수
- `ItemPool`: 후보 목록 (`Item` **또는 `ItemId`** + `BasePrice`(0이면 아이템 BaseValue) + `MinStock` + `MaxStock` + `Weight` + `Quality`). **`ItemId`는 JSON·모드 아이템용** — 그것들은 런타임 생성이라 에셋 참조로 못 가리킨다. 읽을 땐 반드시 `entry.Resolve()`(에셋 먼저 → 없으면 레지스트리 Id)
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
  - **우클릭 = 한 번에 보내기.** 뜻은 **무엇이 열려 있느냐**가 정한다 — 반대편 통이 열려 있으면 그리로 옮기고(배낭 ↔ 창고·작업대·시체, `InventoryGridUI.FindCounterpart`), 배낭만 열려 있으면 **장비를 입는다**(이미 찬 것은 `Equipment`가 배낭으로 돌려준다). 둘을 다른 조작으로 가르지 않는 이유: 우클릭으로 하려는 일은 늘 **"이걸 저기로"** 하나이고 "저기"만 상황마다 다르다. 들어간 만큼만 빠지므로 자리가 모자라면 나머지는 그대로 남는다. **창고의 검은 안 입힌다** — 어느 폰이 입는지 말할 수 없다
  - **뭉치 쪼개기 — 드래그에서 갈린다**: `Shift` = 절반, `Ctrl` = 하나, 그냥 = 전부. 쪼개는 시점이 **드래그 시작**이라 그 뒤로는 평범한 드래그다(놓을 수 있는 곳이 전부 그대로 통한다). **집어 든 쪽이 새 뭉치다** — 남는 쪽이 원래 자리를 지켜야 한다. 통이 꽉 차 놓을 칸이 없으면 **격자에 안 놓고 손에 들고**, 놓기에 실패하면 출처로 되돌린다(안 되돌리면 조용히 샌다). 알리는 것은 아이콘을 캔버스로 옮긴 **뒤**에 — 먼저 알리면 격자가 다시 그려지며 **끌던 아이콘이 파괴된다**(`InventoryGridUI`가 `DraggedInstance`를 건너뛴다)
- `EquipSlotUI` — 장비 칸 6개. **장비칸을 컨테이너처럼 다룬다**: 클릭=해제(배낭으로), 드롭=착용, **끌어서 칸끼리 교환**. 받는 곳은 어디서든 된다 — 내 배낭 · 상자 · 작업대 · **적 시체**, 그리고 **다른 장비칸**(내 것이든 시체 것이든).
  - **배낭을 안 거친다**(`Equipment.Wear*` — 인스턴스를 그대로 찬다). 원래 `Equip*`은 배낭에서 꺼내 차는 설계(`FindInInventory`+`RemoveOne`)라 밖에서 끌어온 것을 못 찾고 **조용히 실패했고**, 배낭 경유로 고쳤더니 이번엔 **배낭이 꽉 차면 빈 칸에도 못 넣었다**("칸은 비었는데 왜 안 들어가지"). 지금은 **벗겨지는 쪽만** 배낭으로 가고(자리 없으면 발밑), 들어오는 쪽은 자리를 안 본다.
  - **장착 지점을 셋으로 모았다** — `Equip*`(배낭 경로)도 전부 `Wear*`로 흐른다. 여럿이면 하나가 반드시 `SyncBlackboard`나 `OnEquipChanged`를 빠뜨린다.
  - 규칙: ① **맞는 칸인지 먼저 보고**(`Accepts`) 뺀다 — 못 찰 것을 옮겨 놓으면 시킨 적 없는 이동이 된다. ② **한 개만** 가져온다(상자에 철검이 셋이어도 차는 건 하나 — 뭉치째면 "착용"이 아니라 "가져오기"다). ③ 교환은 **양쪽 다 빼놓고 나서 채운다**(`TakeOff`는 배낭에 안 돌려준다) — 한쪽씩 하면 중간에 `ReturnToInventory`가 끼어들어 물건이 배낭·바닥으로 샌다. ④ **서로 받을 수 있을 때만** 바꾼다(한쪽만 되면 교환이 아니라 강탈이다).
  - 옮기는 것은 `ItemData`가 아니라 **`ItemInstance`** — 데이터만 넘기면 희귀 철검이 맞바꾸는 것만으로 일반이 된다.
  - 칸 자체가 그림이라 **끌 아이콘을 안 만든다**(격자의 `DraggableItem`과 두 벌이 되고 회전·프리뷰를 여기서 또 구현하게 된다). 대신 끄는 동안 칸을 흐리게 한다.
  - **장비칸 → 격자**(`TryDropToGrid`, `OnEndDrag`) — 격자 쪽은 `IDropHandler`가 없어(아이콘 단위로 `DraggableItem`이 받는다) **받아 줄 사람이 없었다**: 장비칸을 끌어 배낭에 놓아도 아무 일도 안 일어났다. **자리를 확인하고 나서 벗긴다** — 순서가 뒤집히면 격자가 꽉 찼을 때 벗긴 것이 배낭이나 발밑으로 새는데, 사용자는 **그 격자에** 넣으려 한 것이다. 끄는 동안 **`ShowPreview`로 들어갈 자리를 그린다**(장비는 2×4처럼 크고, **창고는 필터가 걸려 있으면 무기·방어구를 아예 안 받는다** — `Storage.CanAccept`. 놓아 보고 나서야 알면 "왜 안 옮겨지지"가 된다)
  - **격자 → 차고 있는 칸 = 맞바꾸기.** 벗은 것은 **온 곳으로 돌아간다**(상자에서 끌었으면 상자로). `Equipment.ReturnToInventory`에 맡기면 **폰의 배낭으로** 가서, 상자 갑옷을 입을 때마다 벗은 것이 배낭에 쌓이고 그걸 다시 상자로 옮겨야 한다 — **한 동작으로 보이는 일이 두 동작**이 된다. 못 받으면 배낭 → 발밑 순(증발 방지 순서)
- `ItemInfoUI` — 아이템 hover 시 공용 툴팁. **띄운 놈이 사라지면 스스로 닫는다**(`Show(..., owner: this)` + `LateUpdate` 감시) — `OnPointerExit`는 **오브젝트가 파괴되면 오지 않아서**, 커서를 얹은 채 격자 아이콘이나 상점 행이 없어지면(동전 더블클릭으로 잔고에 넣기·구매·판매·목록 갱신) 툴팁이 영영 남았다. **띄우는 쪽마다 `OnDestroy`를 다는 대신 여기서 지켜본다** — 부르는 곳이 넷이고 앞으로 늘 텐데 하나가 빠뜨리면 그 경로에서만 남아 원인을 찾기 어렵다. `owner`를 안 넘긴 곳은 예전처럼 스스로 `Hide()`한다(안 넘겼다고 곧바로 닫으면 그쪽이 통째로 못 뜬다). "안 넘김"과 "죽음"은 **`ReferenceEquals`로 가른다** — 유니티의 `== null`은 파괴된 것에도 참이라 둘이 같아 보인다. **이름줄은 이름만**이고 아랫줄이 `등급 · 종류`다(`희귀 · 양손 검`) — 이름 앞에 등급을 또 붙이면 같은 말이 두 번이고 긴 이름이 잘린다. 종류 이름은 **`ItemData.TypeName`이 답한다**(클래스가 오버라이드) — 밖에서 `is WeaponData`로 갈라 쓰면 종류가 늘 때마다 분기를 전부 찾아 고쳐야 하고 하나 빠뜨리면 조용히 "잡화"가 된다

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
- `Discovery`: `DiscoveryItems`(비우면 레지스트리 전체, **화폐 제외**), `DiscoveryMinAmount/MaxAmount` → **거점 둘레 바닥에 떨군다**(`discoveryDropRadius`). 예전엔 자원 노드를 세웠는데 노드는 영구히 남아 거점 둘레가 광맥 밭이 됐고(**발견은 사건이지 지형 변화가 아니다**), 그다음엔 창고에 바로 넣었는데 그건 **사건이 아니라 숫자가 조용히 오르는 일**이라 알림을 놓치면 무슨 일이 있었는지도 모른다. 떨어진 것은 우클릭 [창고로 옮긴다]로 켜야 실려 간다(2026-08-13 — 바닥 물건은 전부 기본 꺼짐). 알림이 방향까지 적으므로 어디로 가야 할지는 알 수 있다

**스폰 자리는 지형이 아니라 맵이 정한다**(`TryFindEdgeSpawn`) — **핵에서 바깥으로 한 칸씩 걸어 나가** 설 수 있는 마지막 칸을 찾고 `spawnEdgeInset`만큼 **안쪽으로 물러선다**. 걷다가 설 수 없는 칸에서 멈추므로 **핵과 이어져 있는 것이 방식 자체로 보장된다**(좌표를 굴려 검사만 하면 바닥 밖 섬을 못 거른다). 낭떠러지 바로 위에 무리를 세우면 서로 비켜서다 밀려 떨어진다(`ExpeditionFormation.approachInset`과 같은 교훈). 흩뿌리기(`ScatterNear`)는 **이미 쓴 칸**과 **이웃이 공허인 칸**을 피한다. **자리도 프리팹도 없으면 알림 없이 건너뛴다** — 알림이 먼저 나가면 "몰려온다"고 해 놓고 아무도 안 온다. **침공대는 소환 직후 핵으로 `SetMoveTarget`** — 적 BT는 탐지·배회(12m)뿐이라 명령이 없으면 가장자리에서 서성이다 끝난다.

**EventNotificationUI**: `EventManager.OnEventTriggered` 구독 → 이벤트 발동 시 화면 상단 팝업 자동 표시. `displayDuration`(기본 5초) 후 자동 닫힘. **제목도 설명도 비면 안 띄운다** — 배경만 남아 화면에 작은 검은 상자가 뜬다(실제로 빈 이벤트 에셋 하나 때문에 그것만 보였다, 2026-08-06). 대신 무엇이 비었는지 콘솔에 남긴다. **스폰형 이벤트는 8방위를 덧붙인다**(`북동쪽에 나타났습니다`) — 어느 쪽을 막을지가 곧 대응이다. 꺼져 있는 동안(월드맵) 들어온 알림은 **데이터든 완성된 문장이든 들고 기다렸다** 돌아올 때 띄우고, 둘 다 밀렸으면 문장이 이긴다(방향이 붙어 더 자세하다).

**`MinGameTime`은 `GameTimeManager`와 같은 축**(하루 = 1800초). 크게 잡으면 초반 추첨 풀에서 빠져 다른 이벤트만 반복된다 — 습격 600·합류 1200이 그래서 한동안 안 나왔고 습격 120·합류 600으로 내렸다(2026-08-07).

**이벤트 에셋**(`5_Data/Event/`, 4종): `Ev_Raid_Goblins`(고블린 4, 가중치 2.5) · `Ev_Peddler`(행상인 방문, 2.0) · `Ev_Wanderer_Joins`(방랑자 합류, 0.8) · `Ev_Discovery`(아이템 발견, 3.0). 씬의 `EventManager.eventPool`에 배선돼 있다.

### BuildMode

- **건설 목록은 레지스트리에서 온다** — `Assets/Resources/Data/Buildings/`에 `BuildingData` 에셋을 두면 그것만으로 목록에 오른다(`DataManager.AllBuildings`, `Category`→`SortOrder`→이름 순). 씬의 `placeableBuildings`는 **비워 두는 게 기본**이고, 채우면 그게 이기는 큐레이션 오버라이드다(`ResearchTable.availableResearch`와 같은 관례). `UsesBlueprint=true` → 2단계 건설, `false` → 즉시 배치
- 설계 원칙: `BuildingData`는 배치 방법만 담당. 각 컴포넌트(Storage, SubCore, FarmPlot 등)가 동작 데이터를 프리팹 SerializeField로 직접 보유
- **콜라이더 크기와 `OccupiesTile`은 다른 축이고, 짝을 맞춰야 한다.** 크기 = **어디를 눌러야 잡히나**(클릭 판정 — 늘 `Size − 0.2`), `OccupiesTile` = **길을 막나**(길찾기는 타일 점유만 보고 콜라이더는 안 본다, `obstacleLayer`는 Wall뿐). 둘을 같은 것으로 보면 *2×2 창고인데 콜라이더가 1×1이라 가운데만 눌리는* 일이 난다(실제로 그랬다 — 고치는 도구가 `OccupiesTile`로 걸러 창고만 건너뛰었다). **`OccupiesTile`이 false면 콜라이더는 트리거여야 한다** — 안 그러면 지도는 "빈 칸"인데 몸이 부딪혀 길찾기가 통과 경로를 내고 폰이 처박힌다(침대·밭이 그랬다). 크기 맞추기는 `Fit Building Colliders To Size`(바닥만 건너뛴다 — 서로 맞닿아야 한다)
- **씬에 손으로 놓은 건물도 자기 칸을 막는다** — `PlacedBuilding.Start()`가 `data.Size`로 발자국을 등록한다(배치로 지은 건물은 이미 불렸으므로 건너뜀). **건물 프리팹에는 `PlacedBuilding` + `data`를 미리 붙여 둘 것** — 없으면 이 등록이 아예 안 돌고, 그러면 길찾기가 아는 건 콜라이더뿐이라 모델보다 작을 때 **모델 한복판을 가로지르는 경로**가 나온다.
- **씬 배치는 격자에 맞출 것**: **홀수 크기 축은 `.5`, 짝수 축은 `.0`**(`GetFootprint`/`FootprintCenter` 규약). 어긋나면 모델이 옆 칸으로 삐져나와 **작업 자리를 먹는다**. 도구: `Snap Placed Buildings To Grid` → `Fit Building Colliders To Size`(**순서 지킬 것**).
- `OnActiveChanged` — 건설 모드 토글 시 다른 UI 자동 닫힘
- **청사진은 자재를 배치 때 안 낸다** — 폰이 지고 와 납품하는 것이 유일한 지불이다(`ConstructionJob`). 전엔 `Place`의 차감이 `UsesBlueprint` 분기보다 **위**에 있어 **두 번 냈다**. 그래서 **자재가 없어도 청사진은 놓인다**(계획이다 — 자재가 들어오면 저절로 재개). `BuildModeUI`의 붉은 색은 막는 표시가 아니라 "지금은 안 지어진다"는 알림
- **철거 모드(X)가 청사진도 지운다** — 클릭 단일 / 드래그 일괄 / 같은 빨간 예정 표시. **결과만 다르다: 건물 철거는 75%, 청사진 취소는 전액**(`BuildingBlueprint.Cancel` → `MaterialPool.Refund`) — 취소는 파괴가 아니라 무효화라 깎을 손실이 없고, 깎으면 계획을 세우는 일에 벌칙이 붙는다. **픽킹은 콜라이더가 아니라 타일**(`PickAt`) — 고스트는 콜라이더를 일부러 다 껐다(통행을 안 막고, 켜면 선택·이동 명령 레이캐스트에 걸린다). 조준은 배치 미리보기와 같은 `TryCursorGround`. 픽킹용 칸 `Area`는 **`Footprint`와 다른 축**(발자국은 "완성되면 막을 칸"이라 창고·침대·농지는 비어 있다). 매달린 일감은 거두되 **배낭에 실린 자재는 안 건드린다**(짐내림이 돌려놓는다)
- **청사진 취소는 건설 모드 밖에서도 된다** — 같은 `PickAt`을 좌·우클릭에도 붙였다. **우클릭 = `[건설을 취소한다]`**(`MoveController`, 아무것도 안 걸렸을 때만 — 그 위에 폰이 서 있으면 폰이 이긴다), **좌클릭 = 폰 정보 패널에 진행 표시 + 같은 버튼**. 문이 셋이어도 전부 `bp.Cancel()` 하나를 부르므로 갈라질 데가 없다 — **어긋나는 건 판단이 여러 곳에 흩어질 때**지 문이 여럿일 때가 아니다. 패널이 답하는 건 "왜 아직 안 지어졌나" 하나라 `자재 12/20` 옆에 **가진 것**(`AvailableAnywhere`)을 같이 적는다. 조사 해제는 `SelectSingle` 맨 앞 **한 곳**에서 한다(분기마다 적으면 새 분기가 하나 빠뜨린다)
- **철거·취소 환불은 전액**(`refundRatio` 1.0). 손실을 물리면 "지어 놓고 아니다 싶으면 옮긴다"가 벌칙이 된다 — 잘못 놓은 값은 자재가 아니라 **들인 시간**으로 이미 치른다. 돌아가는 곳은 **창고 먼저**(`MaterialPool.Refund`) — 바닥에 떨구면 그 짐이 다시 운반 일감이 되고, 나르는 동안 창고 집계에서 빠져 같은 자재를 기다리던 작업이 "재료 부족"으로 죽는다
- **`InspectPanel`은 "고른 것을 보는 자리"다**(2026-08-13 개명 — `UnitHP_Panel`/`UnitHPUI`였다. 폰 HP만 뜨던 창이 아니라 **고른 것 무엇이든** 뜨는 자리가 됐는데 이름이 폰에 묶여 있었다) — 폰 / 시체 / 자원 노드 / **바닥에 떨어진 물건**(무엇이·얼마나·얼마어치 + **자동 운반이 켜졌나**, 버튼으로 켜고 끈다. 초록 구슬이 안 보일 때 그것이 "안 찍었다"인지 "찍었는데 창고가 없다"인지 구별이 안 됐다 — 둘은 할 일이 다르다) / 청사진 / **다 지은 건물**(이름·**설명**·크기·휴면 + [이동한다]·[분해한다]) / **던전 핵**(`OnCoreInspected` — `PlacedBuilding`이 아니라 분해·환불이 없고 [이동한다]만). 저마다의 건물 패널(창고·침대·농지…)을 **대신하지 않고 곁들인다**: 그쪽은 그 건물로 무엇을 하는지, 여기는 **건물 자체**를 다룬다. **휴면 건물도 이 줄은 뜬다**(제 패널은 안 열려도 옮기고 분해할 수는 있어야 한다 — 오히려 그때 제일 필요하다). **바닥은 뺀다** — 바닥도 `PlacedBuilding`이라 세면 빈 땅을 누를 때마다 건물 창이 뜬다. 버튼 줄은 씬의 `ActionTemplate` 복제(생성기 `Create UnitHP Action Row`), **세로로 쌓는다**(가로면 300px 패널에서 글자가 잘린다)
- **자원 노드는 설명을 손으로 안 적는다** — 알고 싶은 것(무엇이 나오나 · 캐나 베나 · 몇 명이 붙나 · 권장 스킬)이 전부 `ResourceNodeData`에 있어 거기서 만든다(적어 두면 값을 고칠 때마다 문장이 뒤처져 **표가 아니라 거짓말이 된다**). `InspectPanelUI`가 기존 **`OnNodeSelected`를 같이 듣는다**(이벤트를 새로 만들면 둘이 어긋난다). **"배정된 폰이 없습니다"가 이 창의 값어치** — 채굴·벌목은 큐가 아니라 직접 배정이라 아무도 안 붙으면 영영 안 캐는데 그걸 말해 주는 곳이 없었다. 씬에 흩어진 나무·바위는 `PlacedBuilding`이 없어 [이동]·[분해]가 안 뜬다(건드릴 수 없는 지형)
- **`BuildingData.Description`이 "무엇을 하는 물건인가"에 답한다**(33종 전부 채움). 크기·환불액은 이미 아는 사람에게나 쓸모가 있고, `보석 연마대`·`서브코어`는 이름이 답을 안 준다. 청사진에도 띄운다 — 아직 물건이 아니니 **무엇이 될 것인지**를 알아야 취소할지 정할 수 있다
- **건물 프리팹에 `PlacedBuilding` + `data`가 실제로 붙어 있어야 한다** — 적어만 두고 안 붙어 있었다(`LargeStorage` 하나뿐). 없으면 씬에 손으로 놓은 건물이 **발자국을 등록 안 해** 모델 한복판을 가로지르는 경로가 나오고, 좌클릭해도 **어느 건물인지 몰라** 정보 패널이 안 뜬다. 도구 `Fix: Attach PlacedBuilding To Prefabs`
- **`BuildingRelocator`** — 다 지은 건물 이전. `CoreRelocator`와 **나란한 물건이지 일반화가 아니다**(핵은 한 칸이고 옮기면 영역이 따라 움직여 휴면/재가동 수를 미리 세어 준다). **값을 안 받는다**(환불이 전액이라 "분해 후 재건축"이 늘 더 싸므로 값을 받아 봐야 조작만 는다). **자기 발자국은 장애물로 안 센다**(안 그러면 한 칸 옆으로도 못 옮긴다). `PlacedBuilding.MoveTo` = `ReleaseTiles` → 이동 → `OccupyFootprint`(폰 밀어내기 포함) → **떠난 자리와 새 자리 둘 다** 재베이크 → `BuildingDormancy.Reevaluate`(영역은 그대로고 건물만 움직여 `OnTerritoryChanged`가 안 뜬다)
- **바닥(floor) 시스템**: `BuildingData.IsFloor` → 무료 substrate(코어 무관). `MapManager._floored`가 타일 → **바닥 윗면 Y**를 희소 저장. 배치 높이는 `SurfaceHeight` + `SpawnOnSurface` — 프리팹 루트 `localPosition.y`가 양수면 **저작 부양 높이**로 보고 그만큼 띄우고(서브코어 등), 아니면 렌더러 바닥면을 표면에 맞춘다(피벗 중앙 프리팹 파묻힘 방지). 즉시 배치/청사진/고스트/자유 배치 동일. 건물의 "바닥 위에만" 게이트 = `FloorRequired`(= `requireFloor` 플래그 ∥ 바닥 존재) — 던전 씬은 플래그를 켤 것. 바닥은 좌클릭 드래그 페인트 + 그리드 강제. **공허 이동 차단 3중**: 길찾기가 바닥 밖 셀 unwalkable + `PawnMover` 낭떠러지 가드(`ledgeLookahead`) + `fallRecoveryY` 낙하 복구. AI 목적지는 `IsStandable` 검사.
- **미리보기·청사진·완성품은 루트 로컬 위치를 똑같이 버려야 한다.** `Instantiate(prefab, pos, rot)`는 프리팹 루트의 `localPosition`을 **덮어쓴다**(그래서 `SpawnOnSurface`가 부양 높이를 다시 얹는다). 그런데 `SetParent(transform, false)`는 그 값을 **그대로 물려받는다** — 청사진 고스트가 이 길이었고, 루트가 0이 아닌 프리팹은 청사진과 완성품이 그만큼 어긋났다(`Bed.prefab` 루트 z = 0.5 → 침대가 반 칸 뒤에 놓임). 고스트는 `localPosition = (0, AuthoredHover, 0)`으로 못 박을 것. **씬에서 끌어내 만든 프리팹은 루트에 씬 좌표가 박혀 있다**(작업대 셋·연구대가 26m대) — 지금은 코드가 무시하므로 무해하지만, 프리팹 편집 화면에서는 엉뚱한 자리에 보인다.
- **폰은 표면 "위에" 찍는다.** `TileToGround`는 **바닥 윗면 Y**라서 그대로 `Instantiate`하면 `CharacterController`(중심 0·높이 2)의 아래 절반이 파묻히고, 유니티가 겹침을 풀며 옆으로 밀어내다 가장자리면 그대로 떨어진다. 캡슐 절반(`height/2 - center.y`)만큼 올릴 것(`EventManager.SpawnPawn`). 핵 소환이 멀쩡했던 건 `GetSpawnPosition`이 **핵의 부양 높이**를 써서 공중에서 착지했기 때문이다.
- **낙하 복구 지점도 확인한다** — `_lastGroundedPos`를 그냥 쓰면 **무한 낙하**가 된다(한 번도 땅을 밟은 적 없는 폰은 그 값이 태어난 자리 = 바닥 밖이다). `PawnMover.SafeRecoveryPoint`가 설 수 있는 칸인지 보고 아니면 가장 가까운 바닥으로 보낸다.
- **핵 영역(크립) / 건물 휴면**: 영역(핵 union) 밖 건물은 `BuildingDormancy`가 휴면(작동 정지+회색 틴트, 철거 X). **휴면 = 전면 사용 불가** — 창고 집계 제외(`Storage.IsActive`), 컴포넌트 `enabled=false`, 클릭해도 패널 안 열림, 매달린 일감은 `JobBase.IsSiteUsable`로 **배정 보류**(취소 아님 → 복구 시 재개). 외부 판정 `BuildingDormancy.IsDormantAt`. 런타임 배치는 `Ensure`로 자동 부착(씬 사전 배치는 수동). **핵 이전** = `DungeonCoreUI` [핵 이전] → `CoreRelocator` → `RelocateTo`. 상세 `docs/design_core_creep.md`

### Input & Camera

| Script | 트리거 | 동작 |
|---|---|---|
| `SelectionManager` | 좌클릭/드래그 | Player 팩션 유닛 선택. **UI에서 시작한 클릭은 놓을 때까지 통째로 무시**(`_pressOverUI`) — 누를 때만 걸렀더니 배낭에서 바닥으로 물건을 드래그해 버릴 때 놓는 쪽이 새어, 지난 `_dragStart`와 엮인 엉뚱한 사각 선택이 **선택을 통째로 지웠다**. 바닥의 드롭 클릭은 노드·시체처럼 **선택 유지**. 적/NPC 클릭 = `OnPawnInspected`(정보만, 명령 대상 아님) — **여기서도 폰 선택은 유지한다.** 적을 눌러 보는 이유는 대개 그 다음에 무언가를 시키려는 것이라(살펴보고 우클릭으로 친다), 지우면 적을 한 번 볼 때마다 폰을 다시 골라야 한다. **선택 = 명령 대상, 조사 = 보는 대상**으로 갈리고 `InspectPanelUI`가 "조사 중인 것이 있으면 그쪽"으로 가려 준다(반대 순서였던 탓에 선택을 지워야만 적이 보였다). 조사는 일회성이라 내 폰을 다시 고르거나 빈 곳을 누르면 `OnPawnInspected(null)`로 풀린다. **선택은 고정이다 — 푸는 조작이 없다**(켄시식, 2026-08-11). 빈 곳을 눌러도 패널만 닫히고 폰은 그대로다: 선택은 "지금 누구에게 명령하고 있나"라는 **지속 상태**라 빈 땅을 눌렀다고 바뀔 이유가 없고, 실제로는 카메라를 돌리다 땅을 스칠 때마다 팀이 통째로 풀려 우클릭 한 번에 매번 다시 골라야 했다. **사각 선택도 잡힌 것이 있을 때만 갈아치운다**(빈 땅을 훑어도 안 풀린다 — 사각형은 더 자주 빗나간다). 바꾸는 길(다른 폰 클릭·사각 선택·로스터)이 이미 있고 "아무도 안 고른 상태"가 필요한 곳은 없다. 대신 **`PruneSelection`이 매 프레임 걷어낸다** — 파괴된 폰과 **꺼진 폰**(원정을 떠나 GameObject 비활성). 꺼진 것은 `== null`로 안 걸려서, 안 걷어내면 우클릭 명령이 지도 밖의 누군가에게 조용히 나간다. **폰이면서 `IWorldClickable`이기도 하면 클릭 대상이 이긴다**(상인 = 상점이 열린다) |
| `MoveController` | 우클릭 | 방랑자 이동/타겟 설정. **UI 위에서 누른 우클릭은 월드로 안 샌다** — 창을 열어 둔 채 그 위를 우클릭하면 뒤의 땅으로 이동 명령이 나갔다. 좌클릭(`SelectionManager._pressOverUI`)처럼 누름/놓음을 짝지을 필요는 없다: 우클릭은 **누르는 그 프레임에 전부 끝나므로**(드래그가 없다) 누른 곳만 보면 된다. **적을 겨눴을 때의 뜻은 상황이 정한다**(`Fighting`) — **붙기 전에는 창**(칠까 / 마무리할까 / 따라붙을까 + 시간도 있다), **이미 싸우는 중이면 즉시 공격**(뜻이 "저놈을 쳐라" 하나뿐이고 표적 바꿀 때마다 창이 뜨면 조작만 느려진다). 방랑자 BT엔 `DetectNode`가 없어 `Target`이 저 혼자 안 붙으므로 이 검사는 곧 **"내가 공격 명령을 내려 뒀나"**다(맞고 있는 중 `HasFreshAttacker`도 전투). **쓰러진 놈은 싸우는 중이어도 창**(할 수 있는 게 마무리뿐인데 그건 지목해야만 된다). 중립·아군 = 늘 창. 선택지 구성은 `MoveController`가 정한다. **겨냥(`PickTarget`)은 `RaycastAll`을 거리순으로 훑으며 명령 받는 폰을 건너뛴다** — 우클릭은 고른 폰들에게 내리는 명령이라 **그들 자신은 대상이 될 수 없다**(폰 하나를 고르고 그 폰을 누르면 `따라간다`(자기를 따라감)가 떴다). 고르지 **않은** 아군은 안 거른다 — 그건 [따라간다]를 시키려고 일부러 누르는 길이다. 즉시 공격과 창이 **같은 결과를 쓴다**(따로 쏘면 규칙이 한쪽에만 걸린다). 창 제목은 **개체 이름 + 종족**(`던컨 (인간)`) — 종족만 띄우면 고블린이 셋일 때 셋 다 "고블린"이고, **데이터가 어긋나도 그럴듯해 보여 안 드러난다**(씬의 `enemy`가 `Faction_Goblin` + `Human` 데이터였는데 이름이 붙자마자 잡혔다) |
| `PawnContextMenuUI` | 우클릭(교전 중 멀쩡한 적·맨바닥 제외) | 우클릭한 **월드 지점에 고정**되는 팝업(카메라를 움직여도 그 자리 — `LateUpdate`에서 매 프레임 화면 좌표로 푼다). 제목 바·닫기 버튼 없음(고르거나 딴 데 누르면 닫힘). 줄은 씬의 `OptionTemplate` 복제. **자기 자신도 겨눌 수 있다** — 한때 `PickTarget`이 명령 받는 폰을 건너뛰었는데(자기를 누르면 `따라간다`가 떠서), **말이 안 되던 것은 겨냥이 아니라 선택지였다.** 자기를 눌렀으면 거래·따라가기·공격을 빼고 **치료만** 남긴다. 대상별 선택지: 유닛=거래(상인, **쓰러졌으면 안 뜬다**)/따라간다/**치료한다**(다쳤을 때, 우리 편만 — 침대에 없어도 된다)(**쓰러진 놈에겐 안 뜬다** — 누운 것을 따라다닐 수는 없다)/**공격 — 문구는 상태와 무관하게 [공격한다] 하나**(쓰러졌으면 안에서 `FinishWith` → `UnitBlackboard.FinishTarget`으로 갈린다. 상태마다 낱말이 다르면 [마무리한다]가 별개의 동작인 줄 알게 된다), 노드=캔다·벤다(배정)/살펴본다, **드롭=착용한다(장비)/줍는다/창고로 옮긴다 ↔ 여기 둔다**, 시체=뒤진다, 핵·건물=연다, **침대=여기 눕는다(=배정, 곧 "쉬어라")·일어난다·`이름`을(를) 치료한다**(명령을 받으면 `Rouse`가 침대에서 일으킨다 — `SetMoveTarget`이 누운 자세는 이미 풀지만 **배정·점유가 남으면** 도착하자마자 도로 눕고 그 침대를 아무도 못 쓴다(`RestNode`는 명령이 도는 동안 안 돌아 스스로 못 비운다). **기절한 폰은 건드리지 않는다** — 못 일어나는데 침대만 뺏으면 깨어나 돌아갈 자리가 없다)(누가 누워 있고 손댈 것이 남았을 때만 — 둘 다 **가장 가까운 한 명**에게 간다. 침대는 하나뿐이고 치료도 한 사람이면 족하다), **어디든 마지막에 [여기로 이동]**. **[줍는다]**(`PickUpTarget` + `PickUpNode`, 방랑자 트리 `ManualOrderNode` 바로 아래)는 **자동 운반과 다른 축** — `GroundHaulJob`은 "창고로 가져다 놓아라"는 큐 작업이라 창고가 없거나 운반을 껐으면 안 돌고, 그러면 바닥의 물건을 집을 길이 없다. 규칙 셋: ① **들 수 있는 폰 중 가장 가까운 한 명만** 보낸다(앞사람 짐이 찼으면 다음 폰). ② **혼자 골랐으면 한도를 넘겨서라도 든다**(`PickUpIgnoreLimit`) — 배분이 아니라 "저걸 네가 들어라"라서 손으로 넣는 것과 같고 과적은 속도로 값을 치른다. **여럿일 때만 한도가 누가 갈지를 가른다.** ③ 무게와 **격자 자리를 둘 다** 본다(격자는 한도를 무시할 때도 못 넘는다). **[착용한다]는 줍기와 한 동작**(`PickUpThenEquip`, 장비 위에선 맨 위 선택지) — 원래 차던 것은 `Equipment`가 배낭으로 돌려주고, 착용은 짐으로 남지 않으므로 한도를 안 본다. **맨바닥은 창 없이 즉시 이동**(선택지가 하나뿐인 창은 클릭만 늘린다). 따라가기 = `UnitBlackboard.FollowTarget` + `FollowNode`(방랑자 트리에서 `ManualOrderNode` 바로 아래) |
| `InventoryUI` | I키 | 인벤토리/장비 패널 |
| `UnitStatsUI` | C키 | 스탯 패널 토글 |
| `ColonyUI` | P키 | 무리 관리(작업 on/off + 전투 성향, 폰 전체) |
| `ExpeditionUI` | G키 | 원정대 편성 — 폰 체크 + 적재량 요약. [출발] 시 **바로 사라지지 않고** 핵 앞으로 집결(`ExpeditionFormation`), 전원 도착 후 함께 떠남 |
| `PawnContextMenuUI` (월드) | 월드맵에서 무리를 **우클릭** | 필드와 **같은 창·같은 규약** — 거래한다·말을 건다(중립만) / 공격한다 / 따라간다 / 살펴본다 / 이 자리로 이동. **고르는 순간 정해지고 가서 그대로 벌어진다**(`BandIntent`) — 다다른 뒤에 또 물으면 같은 결정을 두 번 한다. 따라가기는 중립을 따라다니는 동안 창을 안 띄운다. `GameModeManager`의 **모드 무관 UI**라 지도에서도 살아 있고, 카메라를 넘겨받는다(월드에선 `Camera.main`이 꺼진 필드 카메라다). **연 프레임의 클릭은 무시한다** — 안 그러면 창을 연 그 우클릭이 곧바로 창을 닫는다(필드에선 실행 순서가 우연히 맞아 안 드러났다) |
| `WorldMenuUI` (조우) | 지도에서 무리와 **맞닿음** | **멈춰 세우는 건 적뿐이다**(`Interrupts`) — 적은 지나칠 수 없고(싸우거나 도망치거나 항복하거나), 중립은 스쳐도 아무 일이 아니다(매번 창이 뜨면 순찰대를 지날 때마다 걸린다). 중립과 할 일이 있으면 우클릭으로 작정하고 간다. 적대=`싸운다`/`도망친다`/**`항복한다`**, 중립 상단=`거래한다`(마을 시장과 같은 창)/`습격한다`/`지나친다`. **패배와 항복은 짐이 아니라 사람에서 갈린다** — 둘 다 등짐 전부를 잃지만 패배는 크게 다치고 **포로가 되어 끌려가고**, 항복은 아무도 안 다치고 풀려난다(짐만 덜 뺏기게 하면 항복이 늘 손해라 고를 이유가 없다). **도망은 발이 정한다**(속도 차 — 비슷하면 굴린다). **전력을 숨기지 않는다**(붙을지 말지가 선택이 되려면 재료가 있어야 한다). **필드를 보는 중엔 메뉴 대신 알림**(조작 없이 판정하면 선택을 뺏고, 조용히 넘기면 왜 다쳤는지 모른다) |
| `WorldMenuUI` | 월드맵에서 칸에 **도착**(빈 야생 포함) | **글자가 길면 창이 늘어난다**(`FitToContent`) — 줄 높이도 패널 높이도 씬 고정값이라 두 줄짜리 문구(포로 안내)가 들어오면 칸에서 잘리고 목록이 패널 밖으로 넘쳤다. **문구를 줄여서 풀지 않는다**: 여기 뜨는 말은 `WorldMapView`가 정하고 앞으로도 느는데, 그때마다 글자 수를 세면 UI가 내용을 검열하게 된다. **두 번 잰다**(첫 패스엔 칸 너비가 없어 TMP가 높이를 못 낸다), **매번 저작한 높이에서 다시 잰다**(늘리기만 하면 긴 메뉴를 한 번 본 뒤로 창 크기가 지금 내용과 무관해진다). 마운트앤블레이드식 도착 메뉴(화면 왼쪽 고정, 좌 그림 / 우 선택지). **선택지 구성은 `WorldMapView.OpenMenu`가 정한다** — `탐험한다`(매장 조사) · `시장에 간다`(메뉴 안 메뉴 → 상인별 한 줄, `TownMarket`) · `마을을 둘러본다`(메뉴 안 메뉴 → 인력 영입, `TownRecruits`) 동작, 소굴·`직접 건설`은 "준비 중" |
| `BuildModeUI` | B키 | 건설 모드 |
| `HaulDesignator` | H키 | **운반 지정 모드** — 바닥 물건에 "실어 가라"를 찍는다. 클릭=단일 / 드래그=일괄 / 우클릭=해제. 철거 모드와 **같은 조작**이라 새로 배울 것이 없다. 건설 모드가 켜지면 물러난다(드래그가 겹치면 무엇이 찍히는지 알 수 없다). **씬 배선이 없다** — `RuntimeInitializeOnLoadMethod`로 스스로 선다(아무 상태도 안 들고 저작할 것도 없는 순수 입력기라 맵마다 배선을 반복할 이유가 없다) |
| `CameraController` | WASD/중클/스크롤 | 카메라. **`Time.unscaledDeltaTime`으로 움직인다**(`Dt`) — `GameTimeManager`가 `Time.timeScale`을 만지므로 그냥 `deltaTime`을 쓰면 **멈춘 동안 카메라가 얼어붙고**(멈춰 세우는 이유의 절반은 둘러보려는 것이다) 3배속에서 세 배로 튄다 |
| `CameraController` (월드) | WASD/중클/스크롤 | 월드맵 카메라 — 필드와 같은 컴포넌트. `GameModeManager`가 **월드 카메라에 붙은 것은 월드 측**으로 보고 필드와 반대로 켠다 |
| `WorldMapView` | M키 | 필드↔월드 **모드 전환**(`GameModeManager`에 위임 — 반대편은 통째로 꺼진다). **원정대는 여럿**. 나가 있는 팀마다 토큰이 서고, 없으면 토큰도 없다. **선택 조작은 필드맵과 동일** — 좌클릭=단일 선택 / 드래그=사각 다중 선택 / 빈 곳=해제. **우클릭=선택한 팀만 이동**. **내용물 있는 칸에 도착하면 `WorldMenuUI` 자동 오픈**(M&B식 — 멀리서 클릭해선 안 열린다), Enter=한 팀만 선택했을 때 그 메뉴 재진입. 토큰에 콜라이더가 있어 `RaycastAll` 거리순으로 "토큰인 첫 충돌"(선택) / "토큰 아닌 첫 충돌"(이동)을 가른다. 월드에서 **자유 실시간 이동**(우클릭한 지점으로 직진, 지형 표면을 탄다 — 격자 스냅 없음). 지형은 `WorldMapTerrain`이 저작 격자를 재료로 **맵 전체를 메시 한 장**으로 짓는다(바이옴 성격을 이웃끼리 보간 → 칸 경계 없음). 컴포넌트를 빼면 직접 만든 지형을 쓴다. `WorldMapGrid`는 좌표 변환 + 격자선 + 오브젝트 배치 |

### 개체 명세 — `PawnSpec` (`Assets/1_Script/unit/PawnSpec.cs`)

**"이 폰이 어떤 사람인가"를 태어나기 전에 적어 둔 것.** 이름 · 스탯 등급 51개 · 몸 크기, 그리고 그것을 만든 **시드 하나**.

`UnitBrain`은 `Awake`에서 이름과 스탯을 제 마음대로 굴린다 — 그게 기본값이다. 하지만 **보고 나서 데려오는 경우**(마을 고용)엔 메뉴에 뜬 사람과 합류한 사람이 같아야 하므로, `UnitBrain.ApplySpec(spec)`이 그 결과를 덮는다. `Spec` 프로퍼티로 남는다.

- **모든 무작위가 시드 하나에서 나온다** → 같은 시드면 몇 번을 다시 계산해도 같은 사람(세이브 없이도 일관).
- **외모 커스터마이징의 자리이기도 하다.** 굴리는 것과 사람이 정하는 것이 **같은 그릇**을 쓰므로, 나중에 머리·피부색·장식이 늘어도 `PawnSpec`에 필드가 늘 뿐 적용 통로(`ApplySpec`)는 그대로다. 지금은 `BodyScale` · `MaterialOverride` · `ModelOverride`가 그 축이다(`ApplyLook`이 입힌다 — 전용 모델은 기본 렌더러를 **끄고** 자식으로 붙인다. 지우면 초상화·선택 표식처럼 렌더러를 훑는 것들이 깨진다).
- **수준(tier)을 먼저 뽑고 스탯은 그 언저리(±1칸)에서 굴린다.** 51개를 각각 균등하게 굴리면 평균이 중심으로 몰려(표준편차 1/√51) 누구나 한복판이 되고 "강한 방랑자"가 성립하지 않는다.
- **저작할 수 있다 — `PawnSpecData`(SO, `Create → DungeonColony → PawnSpecData`).** 이름 있는 동료는 매번 다른 사람이면 안 되는데, **시드는 굴림의 결과를 붙들 뿐 무엇이 나올지 고를 수 없다**(원하는 조합이 나올 때까지 숫자를 바꿔 돌리는 건 저작이 아니라 뽑기다). 그래서 **적고 싶은 것만 적고 나머지는 굴린다** — `Grades`에 적은 스탯만 그 등급으로 못 박히고, 안 적은 것은 종족 범위에서 나온다. `Seed`를 비우면 **이름에서 안정 해시로 뽑아** 시드를 안 만져도 늘 같은 사람이다(`GetHashCode`는 실행마다 달라져 못 쓴다). 못 박은 등급이 평균을 바꾸므로 `RecomputeAverage`로 다시 센다 — 안 하면 저작한 S급 동료가 굴린 값 그대로 싸게 나온다.
- **굴리기가 둘이다**: `Roll(data, seed, min, max, …)`는 범위를 **덮어쓴다**(마을 후보 — 지역 등급이 종족 기본을 이긴다), `Roll(data, seed, …)`는 **종족이 스탯별로 저작한 범위를 그대로** 쓴다(고정 개체 — 그 종족다움이 남아야 한다).
- **배선은 `UnitBrain`**: `fixedName`(이름만) / `pawnSpec`(전부). 우선순위는 **저작 < 주입** — 마을 고용처럼 밖에서 `ApplySpec`을 주면 그쪽이 이긴다(상인 상품과 같은 규칙).
- `PriceFrom(basePrice)` — `기준가 × (1 + 평균등급 × 0.12) × 흔들림(±22%)`. **등급이 값을 정하되 같은 등급이라도 값은 다르다** — 흔들림이 없으면 값이 곧 스탯 표가 되어 굳이 볼 이유가 없다. 흔들림도 시드에서 나오므로 메뉴를 여닫아도 안 바뀐다(스탯 추첨과는 **다른 난수열**을 쓴다 — 같은 열이면 "스탯이 좋으면 흔들림도 크다"는 상관이 생긴다).

### 특성 — `TraitData` / `TraitPool` (`Assets/1_Script/unit/`)

**"이 폰은 어떤 사람인가"를 숫자 대신 이름으로 답하는 것.** `강인함 62`는 비교해야 뜻이 생기지만 `강인함`이라는 딱지는 그 자체로 읽힌다.

**모델 A — 특성은 스탯에 숫자를 얹을 뿐이다**(2026-07-26 결정, `docs/design_traits.md`). 기능을 특성으로 게이트하지 않는다: 그러면 전투·작업 코드가 전부 "이 폰이 그 특성을 가졌나"를 묻게 되고 특성 하나 늘 때마다 분기가 사방에 는다. 그래서 `BodyPartSystem.TryKnockOut`은 지금도 `Stats.Toughness`만 읽는다.

- `TraitData`(SO, `Create → DungeonColony → TraitData`) — `StatOffsets` · `Weight` · **`ExclusiveGroup`**(같은 묶음은 한 폰에 하나 — `용감`+`겁쟁이`는 설명이 안 되고 오프셋이 **상쇄돼** 안 붙은 것과 같아진다). 에셋은 `Resources/Data/Traits/`(10종, 배타 4쌍 + 단독 2).
- **`GrowStat`을 쓰지 않는다** — 그건 성장이라 `IsLocked`(하수인)에서 막히고 승급 알림까지 띄운다. 특성은 타고난 것이라 태어날 때의 값을 바꾼다.
- **개수도 굴린다 — 0~3개**(`TraitPool.RollForPawn`, 가중치 `{0.8, 1.8, 3, 2.6}` → 0개 10% · 1개 22% · 2개 36% · 3개 32%). 전원이 정확히 셋이면 특성은 개성이 아니라 **양식**이 된다. 다만 **저작한 특성은 굴린 개수를 이긴다**(0이 나와도 `PawnSpecData`에 적은 건 붙는다).
- **`PawnSpec.Traits`가 시드에서 나온다**(굴림 **맨 끝** — 앞 순서를 안 건드려야 기존 시드의 이름·스탯이 그대로다). 개수도 같은 난수열에서 뽑으므로 마을 후보 메뉴에서 본 특성 그대로 합류한다.
- `ApplySpec`은 **스탯을 덮어쓴 뒤에** 얹는다(순서가 뒤집히면 등급이 보정을 지운다). `SetTraits`가 이전 것을 되돌리므로 두 번 불려도 두 겹으로 안 쌓인다.
- **전부 받는다** — 방랑자·하수인·적·NPC. 명세 없는 폰은 `ApplyAuthoredSpec`이 굴린다. 하수인도 받으므로 **같은 하수인을 소환해도 다른 놈이 나온다**.
- **나중에 붙일 수 있다** — `UnitBrain.AddTrait`/`RemoveTrait`(부상 트라우마 등의 진입점). 뗄 때 0~130 클램프에 잘린 만큼은 안 돌아온다.
- 저작은 `PawnSpecData.Traits` — **적은 것만 못 박고 남는 자리는 굴린 것으로 채운다**(등급과 같은 규칙).
- 나쁜 특성도 스탯을 하나는 올린다(`곰손` = 손재주 −12, 근력 +8). 순수하게 나쁘기만 하면 개성이 아니라 벌칙이다.
- **스탯 표시명은 `StatNameExt.ToDisplay` 하나뿐**(`StatGrade.cs`). 전엔 `GradeUpNotificationUI`의 private 메서드였다. 스탯 설명은 `StatInfo.Describe`(툴팁 전용).
- **표시**: 스탯 창(C)은 맨 위에 이름+오프셋(`곰손  손재주 -12 · 근력 +8`), 체력 창은 이름만 한 줄(`특성  강인함 · 겁쟁이`). **마우스를 올리면 설명**(`HoverTooltipUI`, 씬 오브젝트 — 생성기 `Create Hover Tooltip UI`) — 스탯·특성 둘 다. TMP **`<link>`로 글자 안에서 영역을 잡으므로**(`FindIntersectingLink`) 스탯 51개마다 오브젝트를 두지 않는다. 구조·위치 계산은 **`ItemInfoUI`와 같다**(커서 툴팁이 둘인데 규칙이 다르면 안 된다): 컴포넌트는 **늘 켜져 있는 부모**에 살고 자식 `panel`만 토글한다 — 꺼지는 오브젝트에 붙이면 `Awake`가 안 돌아 `Instance`가 null로 남고 툴팁이 한 번도 안 뜬다. 배경은 `raycastTarget = false`(커서를 먹으면 깜빡인다). **링크 태그는 정렬 바깥에 감을 것** — `{F(...),-50}`처럼 rich text에 자릿수를 매겨 열을 맞추는 코드라 태그가 안쪽에 들어가면 열이 어긋난다.

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

## 답이 한 벌이어야 하는 것들 (자주 어긋난 자리)

같은 물음에 여러 곳이 각자 답하면 **반드시 어긋난다** — 이 저장소에서 실제로 겪은 것들이다.
새 코드를 쓸 때 여기 있는 것을 다시 만들지 말 것.

| 물음 | 유일한 답 | 어긋났던 흔적 |
|---|---|---|
| 얼마나 질 수 있나 | `CarryCapacity` | 네 군데가 따로 계산했고 둘이 **3.3배** 달랐다 |
| 재료가 어디 있나 | `MaterialPool` | 창고만 세어 손에 든 씨앗이 없는 것이 됐다 |
| 무엇을 먹나 · 누가 굶나 | `Feeding` | 먹는 길이 둘인데(제 발로 / 먹여 주는) 임계·고르기가 `EatNode` 안에 박혀 있었다 |
| 자리가 든 일감 하나 | `SiteJobSlot` | 침대·우리·먹이기가 `Tick`/`RequestBy`/`Drop`을 **세 벌** 갖고 있었다 |
| 무엇이 등급을 갖나 | `ItemData.HasQuality` | `CraftJob` 안에 `is WeaponData \|\| is ArmorData`로 박혀 있었다 |
| 창고에서 배낭으로 챙기기 | `Storage.FetchInto` | 셋이 **꺼낸 뒤에** 자리를 봐서, 짐이 찬 폰이 오면 물건이 창고에서 나왔다 들어갔다 했다 |
| 폰을 바닥 어디에 세우나 | `PawnPlacement` | **다섯 벌**. 함정 설명이 세 벌이었다 |
| 부린 짐의 잔량 | `Storage.Deliver` | **세 벌**, 서로 달랐고 하나는 필터를 우회했다 |
| 주변에 누가 있나 | `FactionScan` | **세 벌**, 전부 매 틱 배열을 만들었다 |
| 폰이 서는 자리 | `WorkSpot.TileFor` | Job마다 따로 계산해 통행 불가 칸을 겨눴다 |
| 얼마나 가까이 가야 하나 | `JobBase.Reach` | BT에 상수로 박혀 모든 일감이 한 자리에 붙었다 |
| 맵 가장자리가 어디인가 | `EventManager.WalkToEdge` | 나가는 길이 따로였다면 바닥 밖을 가리켰을 것 |
| 갇힘을 어떻게 푸나 | `Imprisonment.Release` | 우리·침대가 각자 풀어 결과가 갈렸다 |
| 자주 쓰는 컴포넌트 | `UnitBlackboard`(`Brain`·`Body`·`Combat`·`Faction`) | 매 틱 `GetComponent`를 다시 했다 |

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

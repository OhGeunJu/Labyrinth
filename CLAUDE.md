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
├── Campfire.cs               ← 화톳불 (저녁에 켜지고 아침에 꺼진다)
├── DayNightLighting.cs       ← 하루를 빛에 얹는다 (방향광 한 바퀴)
├── LightSource.cs            ← 불빛 레지스트리 (`Vision`이 훑는다)
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

## 에셋 폴더

`5_Data`(SO) · `2_Material` · `3_Prefab`. **`3_Prefab`은 종류로 나뉜다**(2026-08-14 — 예순 개가 한 폴더에 섞여 있었다):

```
3_Prefab/
├── Building/   ← PlacedBuilding·SubCore가 붙은 것 (BuildingData가 가리킨다)
├── Pawn/       ← UnitBrain이 붙은 것 (적·동맹·NPC·내 폰·하수인)
├── Node/       ← ResourceNode가 붙은 것
└── UI/         ← 창 부품 프리팹
```

**옮겨도 참조는 안 끊긴다** — 코드에 경로가 박힌 곳이 없고(전수 확인) 씬·에셋은 전부 guid로 가리킨다. **`.meta`를 짝지어 옮길 것**: 프리팹만 옮기면 유니티가 새 guid를 발급해 **모든 참조가 조용히 끊긴다.**

**이미 쓰이는 프리팹을 통째로 다시 지을 때는 `.meta`의 guid만으로 부족하다 — 루트의 fileID도 지켜야 한다.** 씬에 놓인 인스턴스는 루트 **GameObject·Transform·컴포넌트**의 fileID에 제 오버라이드(위치·회전)를 매달아 두므로, id가 바뀌면 유니티가 *"Prefab mismatch … data will be lost"*를 뱉는다. **자식은 없어져도 된다**(평범한 편집이라 그 오버라이드만 조용히 버려진다) — 위험한 것은 **루트**와, 옛 id를 **다른 타입으로 재사용**하는 것이다. 다시 지을 때는 옛 파일에서 루트 계열 id를 뽑아 그대로 물려줄 것(2026-08-16에 바리케이드로 겪었다).

## Architecture — Quick Reference

### Unit Types & BT 순서

| Type | Data Class | BT 순서 |
|---|---|---|
| **방랑자** | `UnitData` | **직접 명령** → 업기 → 줍기 → 따라가기 → **반격**+Fight → Rest → Eat → Work → Idle |
| **하수인** | `MinionData` | Detect+Fight → Rest → Eat → Patrol → Work → Idle |
| **Enemy** | `UnitData` | Detect → Fight → Idle |
| **NPC** | `NpcData` | 타입별 (Villager: Flee→Wander / Guard: Fight→Patrol / EventMerchant: WalkIn→Stay→WalkOut / **Drifter: Flee→Wander→Leave**) |

**소집(Draft) — `R`키로 고른 폰을 싸울 태세로 붙든다**(2026-08-20에 붙였다). **분기가 아니라 게이트다**: 한동안 트리 맨 위에 `DraftedNode`가 있었는데 그것이 `Running`만 돌려줘 **켜는 순간 아래가 통째로 멎었다**(이동 명령도 반격도 안 먹는 소집이라 부르는 입력도 안 붙어 있었다). 소집은 *무엇을 하는 상태*가 아니라 **자동 행동을 멈추고 싸울 태세로 두는 상태**라, 아래 노드들이 저마다 물어야 한다(업고 있는 것과 **같은 모양**).

뜻하는 것은 셋(`UnitBrain.Draft`):
- **자동 행동을 멈추되 배정은 남긴다** — 일감을 안 집고(`WorkNode`) 눕지 않는다(`RestNode`). **하던 일감은 `Draft()`가 `DropCurrentJob`으로 놓는다** — 게이트만 걸면 그 일감이 예약된 채 얼어붙어 **다른 폰도 못 가져간다**. **`AbandonWork`가 아니다**: 그건 자원 노드 배정까지 거두므로 소집을 풀 때마다 캐던 바위를 손으로 다시 붙여 줘야 한다. **침대도 안 건드린다** — 자던 폰을 일으키는 것은 `RestNode` 게이트의 `LeaveBed`가 이미 하고(자세와 **점유만** 풀고 **배정은 남긴다**), 여기서 `Bed.ReleaseFrom`을 부르면 배정까지 거둬 그 사이 다른 폰이 침대를 가져간다. **소집은 잠시 딴 일을 시키는 것이지 담당과 잠자리를 떼는 일이 아니다.**

**`DropCurrentJob` ↔ `AbandonWork` — 가르는 축은 "명령이냐 상태냐"다.**

- **명령은 옛 명령을 덮어쓴다** → `AbandonWork`(일감 + 배정). 이동·공격·치료·먹이기·설득·업기·줍기·호송·대화, 그리고 사망·원정 집결. 자원 노드 배정도 **플레이어가 붙인 명령**이라 새 명령이 덮는 것이 맞다(이동 명령이 `PutDownSite`·`FollowTarget`·`EscortTo`를 거두는 것과 같은 자리). 심부름 끝나고 하던 바위로 돌아가게 만들면, 시킬 때마다 그 폰이 **무엇을 하던 중이었는지 기억하고 있어야** 한다.
- **소집은 명령이 아니라 상태다**(켜고 끄는 스위치) → `DropCurrentJob`(일감만). 끄면 이전으로 돌아가는 것이 그 스위치의 뜻이라 **배정을 거둘 이유가 없다**. 손에 든 일감만 놓는 것은 게이트로 막기만 하면 그 일감이 **예약된 채 얼어붙어 다른 폰도 못 가져가기** 때문이다.
- **소집 중의 이동 명령도 배정을 안 거둔다** — 자원 노드(`MoveController`)와 **침대**(`Bed.RouseFrom`) 둘 다. 진형을 잡으려고 한 번 옮겼을 뿐이고, `WorkNode`·`RestNode`가 이미 막혀 있어 *"도착하자마자 도로 일하러/눕으러 간다"*가 성립하지 않는다.
- **밥은 그대로 먹는다**(`EatNode`는 안 막았다) — 막으면 **소집해 둔 것을 잊었을 때 굶어 죽는다**(업기가 먹는 것만 열어 둔 그 판단이다). 피로는 안 죽으므로(쓰러질 뿐이다) 막아도 사고가 안 난다.
- **먼저 친다** — 평소 방랑자는 반격만 하는데 소집 중에는 다가온 적을 저 혼자 문다. **목줄은 짧다**(`DetectNode.DraftLeash`, 공격 사거리 ×3 = 근접 4.5m) — *자리를 지키며 싸우라*는 뜻이지 표적을 찾아 나서라는 뜻이 아니다. 진형을 잡으려고 소집했는데 걸어 나가면 뜻이 사라진다. (한 번 문 표적은 목줄 밖으로 달아나도 쫓는다 — 지목한 표적을 유지하는 규칙과 갈라 쓸 수가 없어서다.)
- **도망치지 않는다** — `PlayerFleeNode`가 이미 `!bb.IsDrafted`를 본다. 상태 글자는 `PawnStatusText`가 이미 띄운다.

**명령을 받는가는 폰이 답한다** — `UnitBrain.AcceptsOrders`(하수인은 *원래* 안 받는다) / `CanObeyNow`(갇히거나 업힌 폰은 *지금* 못 받는다). **둘은 다른 물음이라** 부르는 쪽이 갈라 쓴다(우클릭은 후자를 "빠졌습니다"로 알리고 전자는 조용히 건너뛴다). 묻는 곳이 둘이라(`MoveController`·`DraftController`) 각자 적으면 **명령이 조용히 사라지는 대상**이 생긴다.

### 팩션 (세력 관계)

**축은 다섯이고 서로를 모른다.** 조합은 **프리팹이 한다** — 인간 상인 = (Human + 상인 역할), 엘프 상인 = (Elf + **같은** 상인 역할).

| 축 | 데이터 | 답하는 것 | 에셋 |
|---|---|---|---|
| 종족 | `UnitData` | 스탯·이름 풀 | `Resources/Data/Units/` |
| 세력 | `FactionData` | 누가 적인가 | `Resources/Data/Factions/`(9종 — **레지스트리가 훑어야 해서** 여기 있다. 세력 창이 만나기 전의 세력까지 나열한다) |
| AI 성향 | `AiProfileData` | 적을 만나면 어떻게 하는가 + 유휴 배회 | `5_Data/AiProfile/`(4종) |
| 역할 | `NpcData` | 무엇을 하는 사람인가(주민·경비·상인·**떠돌이**) | `Resources/Data/Npc/` |
| 소지 | `LoadoutData` | 무엇을 지니고 무엇을 떨구는가(장비·소지품·동전) | `5_Data/Loadout/` |

**세력 아홉** — 플레이어 / **마을 사람**·**변방민**(시민이 둘이다) / 산적 / 고블린 / 야수 / 망자 / 검은 교단 / **자유 용병단**(플레이어와 동맹). **시민이라고 한 세력일 이유가 없다** — 마을 사람은 플레이어와 아군이지만 변방민은 **중립에서 시작한다**(적대가 아니라 아직 아무것도 아니다). 둘 다 산적·고블린·야수·망자·교단에는 적대라 지도에서 하는 일은 비슷한데, 나를 어떻게 보느냐가 다르다. 교단과 망자는 **서로 아군**이다(해골을 누가 일으켰는지가 무리 구성에 보인다).

- `FactionData` — `PlayerControlled` / **`AcceptsRansom`**(붙잡은 사람을 돈 받고 놓아주는가 — 끄면 쳐들어가는 수밖에 없다. **무리가 아니라 세력이 든다**: 무리마다 적으면 같은 세력인데 어떤 무리는 흥정이 되고 어떤 무리는 안 되는 꼴이 된다. 고블린은 꺼져 있다) / **`RaisesDead`**(이 세력의 습격이 맵의 시체를 일으키는가 — 교단·망자) + `ExhumesGraves`(무덤도 파는가 — 교단만) + `RisenBones`(뼈에서 세우는 것) / `DefaultStance` / `Relations[{Other, Stance}]`. **관계는 방향별**(`StanceTo`): A→B 적대여도 B→A 중립 가능. `Faction` 컴포넌트가 SO를 들고 `IsPlayer`/`IsHostileTo`/`IsAllyOf` 제공. **SO는 반드시 연결할 것** — 비우면 아무와도 관계가 없어 판정이 전부 중립으로 떨어진다(`Awake`가 경고). 반격은 관계와 무관(`UnitBlackboard.LastAttacker`, 15초).
- **방랑자는 반격만 한다 — 먼저 걸지 않는다**(`DetectNode(retaliateOnly: true)`). 하수인·적처럼 범위 안의 적대를 훑으면 지나가는 적을 보고 저 혼자 달려 나가 **직접 제어가 성립하지 않는다**(어디에 세워 둘지가 명령이 아니라 지형의 문제가 된다). 그렇다고 없으면 **맞으면서 가만히 서 있는다** — 방랑자 트리에 `DetectNode`가 아예 없어서 `bb.Target`이 저 혼자 붙는 길이 없었다(2026-08-12 수정). **노드를 새로 만들지 않고 스위치를 둔다** — 반격 대상 규칙(15초 기억 · 감지 범위 2배 · 쓰러진 놈 제외 · 기존 표적 유지)이 이미 `DetectNode` 안에 있다
- **AI 역할은 `UnitBrain.UsesEnemyAi`(NPC 아님 && !IsPlayer)** — 에셋 클래스는 묻지 않는다. **`is MinionData`는 "하수인 종족"이지 "내 편"이 아니다**(고블린은 소환될 수도 야생에 살 수도 있다). 소속을 물어야 하는 곳(`DungeonCore` 소환 한도, `InspectPanelUI` 라벨, `UnitStats.IsLocked`)은 **세력을 같이 볼 것**.
- `aiProfile`을 비우면 타입별 기본값(일반·경비=교전+배회 / NPC 주민=도주+배회 / 상점상인=도주+제자리). 트리 조립은 `AddCombatBranch`/`AddWanderBranch` 공용. 상세 `docs/units.md`.

- `MoveController` — 방랑자만 우클릭 명령 (`!(brain.Data is MinionData)`)
- `MinionData.AllowedJobs` — 하수인 작업 설정의 **초기값**(이후 조정은 `WorkSettings`로 개체별). **큐 작업만 뜻이 있다** — 채굴·벌목·경비는 직접 배정이라 여기 적어도 켜고 꺼지지 않는다. 큐 작업이 하나도 없는 목록은 빈 목록과 같게(전부 허용) 보고 경고한다
- `MinionData.CanProvideWater` — 우물 없이 직접 물 공급

### Key Singletons

| Class | 주요 API |
|---|---|
| `GridOverlay` | 건설 격자. **메시로 그린다**(`MeshTopology.Lines`) — 한때 `GL.Begin`을 `endCameraRendering`에서 불러 **오버레이 UI 위에 격자가 얹혔다**(창이 반투명해 보였다). 즉시 모드로 카메라 렌더 끝에 그리는 것은 UI와 순서를 다투는 일이고 승자가 파이프라인 버전에 달린다. 메시면 세상의 물건과 같은 규칙이라 UI가 언제나 위다. **경계를 손으로 잡을 것** — 선은 얇아 자동 경계가 카메라 밖으로 판정되면 통째로 사라진다 |
| `MapManager` | 점유=`HashSet<Vector2Int>` 희소 저장(절대좌표, 핵 이동에도 유지). `TileToWorld`(Y=0, 길찾기용) / `TileToGround`(표면 스냅, 배치/시각화용) / `SurfaceHeight(tile)`(바닥 윗면 ∥ 지형 높이) / `TryGetFloorSurface` / `WorldToTile` / `SampleGroundHeight(x,z)` / `IsFlatEnough(footprint)` / `IsBuildable` / `OnTileChanged` static event / `GetForPosition(Vector3)` |
| `JobManager` | `RegisterJob` / `RequestJob(unit)`(방랑자·하수인 공통 — 폰의 `WorkSettings`로 필터) |
| `DungeonCore` | `Summon(MinionData, pos)` / **`Minions`**(소환 목록 — 씬의 `minionEntries`가 비면 `DataManager.AllMinions`, 채우면 그게 이기는 큐레이션 오버라이드. 프리팹은 `MinionData.Prefab`, 단 **씬 배열이 이긴다** — 에셋은 씬 오브젝트를 참조할 수 없어서) / `IsWithinAnyRangeWorld(Vector3)`(메인+서브 union) / `RegisterSubCore` / `RelocateTo(pos)`(핵 이전, UI 진입점=`CoreRelocator`) / `WouldBeInRangeAt(corePos, worldPos)`(이전 미리보기용 영역 판정) / `OnTerritoryChanged` static event(영역 변경 → `BuildingDormancy` 재평가) |
| `EventManager` | `checkInterval`(기본 300초) 주기 랜덤 이벤트 발동. `OnEventTriggered` static event. **습격에는 구원이 딸려 올 수 있다**(`GameEventData.RescueOnRaid`/`RescueChance`/`RescueDelay`) — 지원군이 추첨으로만 오면 **조용한 날에 와서 어슬렁거리다 정작 몰려올 때는 없다.** 습격이 벌어질 때마다 따로 굴리고, **늦게 도착한다**(같이 나타나면 구원이 아니라 그냥 편이 하나 더 있는 판이다). **먼저 예고하지 않는다** — 올지 모르는 채로 막아야 오는 것이 반가운 일이 되고, 늘 오면 습격이 안 무섭다. 후보는 **추첨 풀에서 찾는다**(씬에 필드를 하나 더 두면 배선을 빠뜨렸을 때 조용히 아무 일도 안 일어난다). **이벤트 습격과 지도에서 걸어온 습격 둘 다**에 붙는다 — 플레이어에게 그 둘은 똑같이 '적이 몰려온 것'이다 |
| `GameTimeManager` | **하루 1800초(실제 30분)이고 다섯 때로 나뉜다** — 새벽 00\~04 · 아침 05\~09 · 낮 10\~16 · 저녁 17\~20 · 밤 21\~23(`TimePhase`, 경계는 상수). **때는 시각(`Hour`)이 정한다** — 비율로 얹으면 시계와 라벨이 따로 논다. `IsDay`는 거기서 나온다(아침·낮·저녁 = 16시간 / 새벽·밤 = 8시간). **하루는 자정에 바뀐다**(`NormalizedTime` 0 = 00:00). **모든 지속시간 데이터의 기준 축** — 하루 길이를 바꾸면 허기·작물·연구·제작·채굴을 같이 옮겨야 한다(`docs/data.md` "시간 축"). **밤이 세 가지를 바꾼다**(2026-08-19) — **어두워지고**(`DayNightLighting`) · **덜 보이고**(`Vision`) · **습격이 몰린다**(`GameEventData.NightWeight`). 셋이 같이 있어야 뜻이 선다: 어둡기만 하면 불편이고, 시야만 줄면 표의 숫자고, 습격만 몰리면 왜 밤에 몰려오는지가 화면에 없다. **그리고 밤에 잔다**(2026-08-20, `Needs`의 피로) — 피로 1200초 + 회복 600초가 정확히 하루 한 번이고, 그 600초가 어두운 때(밤+새벽)와 같은 길이라 잘 굴러가는 콜로니는 저절로 *밤에 자고 낮에 일하는* 모양이 된다. **밤을 되돌리는 손잡이는 불이다**(`LightSource`·화톳불 — 어두워지는 것과 덜 보이는 것 둘에 답한다). **판은 아침에 시작한다**(`startHour` 7 — `NormalizedTime` 0은 자정이라 손대지 않으면 새 판이 캄캄하게 열린다). **`Elapsed`에는 안 얹는다** — 그건 *시작하고 얼마나 지났나*라 이벤트 게이팅이 쓰는 축이고, 더하면 판이 열리자마자 일곱 시간이 지난 것이 된다. `NormalizedTime`(0-1) / `Phase` / `PhaseAt(hour)` / `PhaseName` / `IsDay` / `Hour` / `Minute` / `CurrentDay` / `Speed` / `SetSpeed` / `OnDayChanged` / `OnTimeChanged(float)` |
| `HierarchicalPathfinder` | 씬에 배치 필요. `ComputeCoarseRoute(from,to)`(메소/L2) + `FindLocalPath(pos,target,self)`(유닛 중심 Theta* 윈도우) / `RebakeObstacleAt`. `obstacleLayer`=Wall만. **통행 판정은 타일 단위** — 반경 부풀림은 타일보다 큰 에이전트에만. **코스 라우트는 도착점이 아니라 복도 안내다** — 메소(2m)·L2(100m) **셀 중심**의 나열이라 마지막 점이 목적지와 어긋난다. `PawnMover.SelectSteeringTarget`이 **마지막 구간에서 `_finalGoal`을 직접 겨누지 않으면** 폰이 셀 중심에 서서 멈추고 재계획해도 같은 답이 나와 **2~3m 앞에서 영영 맴돈다**. 마지막 칸은 L1 정밀 경로 몫. **못 서는 칸은 목적지가 못 된다** — `PawnMover.SetMoveTarget`이 `MapManager.FindWorkableNear`로 접는다(작업 자리가 쓰는 그 함수. 부르는 곳이 스물일곱이라 **문 하나에서** 접는다). 안 접으면 건물 발자국을 겨눈 폰이 **도착 판정에 영영 안 걸리고**, 막힐 때마다 `RememberBlockedSpot`이 그 자리를 3초 막아 플래너가 건물을 빙 돌리므로 **멈추지도 포기하지도 않고 둘레를 돈다**(2026-08-22). 그때 정체 감시가 안 걸린 것은 그것이 *얼마나 움직였나*만 재기 때문 — **움직이는 것과 가까워지는 것은 다르다**. `TrackGoalGain`이 *가장 가까웠던 거리*의 기록이 8초간 안 깨지면 포기한다(기록 대신 지금 거리와 견주면 반 걸음씩 오가는 것도 "가까워졌다"가 된다). 상세 `docs/world.md` |
| `ResearchManager` | `IsUnlocked(recipe)` / `GetRequiredResearch(recipe)` / `CanResearch(data)` / `Complete(data)`. 진행률은 `ResearchTable.ActiveResearch` / `ActiveProgress`(폰이 연구대에 붙어 타이머가 도는 동안만 — 큐 예약은 안 잡힘) / `PendingResearch`(폰이 안 왔어도 맡은 것 — UI가 버튼/게이지를 가르는 기준). **한 연구대는 한 번에 하나만 맡는다** — `StartResearch(data)`가 하던 것을 접고 갈아탄다(진행도는 남아 다시 고르면 이어서 한다). UI의 [연구] 버튼과 진행 게이지는 **같은 칸을 번갈아 쓴다**(`ResearchTableUI.PrepareSlot`이 런타임에 게이지를 버튼 자리로 옮긴다 — 씬 수정 불필요). **잠금은 양방향 어느 쪽으로 적어도 걸린다** — `ResearchData.UnlockedRecipes`(큐레이션, 역인덱스를 Awake에서 생성) ∥ `RecipeData.RequiredResearch`(개별 지정). 둘 다 비면 처음부터 열린 레시피 |
| `DataManager` | static. `GetItem` / `GetRecipe` / `GetRecipesBySkill(CraftSkill)` / `GetBuilding(name)` / `GetUnit(id)` / `AllItems` / `AllRecipes` / `AllResearch` / **`AllBuildings`**(Category→SortOrder→이름 순) / **`AllUnits`**(`Resources/Data/Units/` 스캔 — `MinionData`·`NpcData` 포함) / **`AllMinions`**(SortOrder→이름 순, 소환 메뉴 순서) |
| `GameModeManager` | **화면·입력**이 필드냐 월드냐의 단일 소유자. `Current` / `IsField` / `IsWorld` / `SwitchTo(GameMode)` / `Toggle()` / `OnModeChanged`. 월드 전환 = 필드 UI·입력·카메라 off + 월드 UI/루트/카메라 on. **UI 캔버스는 하나** — 캔버스가 아니라 그 **직속 자식들**을 다루고 월드 전용 묶음(`Canvas/WorldUI`)만 반대로 켠다(월드 UI를 캔버스째 끄지 않으려고). 필드 UI는 **떠날 때 열려 있던 상태를 기억했다 복원**(`ApplyFieldUi`) — 일괄로 켜면 닫아 뒀던 패널까지 따라 열린다. **정착지 루트는 안 건드린다 — 거점 시뮬은 월드맵을 보는 동안에도 계속 돈다**(2026-07-30 결정, 07-29의 "필드 통째 정지"를 되돌림). `EventManager`도 계속 돈다(원정 중 습격 → M키로 복귀해 대응, 추상 방어 판정 불필요) |
| `OutpostManager` | **채집기지**(`WorldNodeContent.Outpost`)의 단일 소유자. `Build(node)` / `At(node)` / `LoadInto(outpost, party)` / `CanAfford` / `CostSummary` / `OnChanged`. **필드맵을 만들지 않는 추상 시설** — 매일(`OnDayChanged`) 남은 매장에서 골고루 캐 기지에 쌓고, **원정대가 들러 등짐으로 실어와야** 창고에 들어간다(본거지 순간이동 없음). 건설 비용 `buildCost`(비우면 무료)는 활성 거점 창고에서 빠진다 |
| `ExpeditionFormation` | 원정 편성의 **집결** 단계. `Begin(members)` / `Cancel()` / `IsGathering` / `ArrivedCount` / `OnChanged`. 핵의 `WorkSpot`을 중심으로 한 **반지름 고정 원**에 벌려 서고(인원이 늘면 원이 커지는 게 아니라 촘촘해진다 — 집결 범위가 인원마다 달라지면 "저기 모인다"를 예측할 수 없다), 자리는 설 수 있는 칸으로 스냅한다. **도착 판정은 자리가 아니라 원이다** — 원 안에 들어오면 도착. 자리는 안내지 조건이 아니라서, 그 칸에 못 서는 폰 하나가 출발을 막으면 안 된다. 그래서 자리를 원보다 `approachInset`만큼 **안쪽**에 잡는다(판정선 위에 세우면 경계에서 아슬아슬해진다). 자리 근처 판정은 폴백으로만 남는다. **전원 도착 시 일괄 출발** → `WorldMapView.AddMembers` + 월드맵 전환. 도착한 폰은 `UnitBlackboard.HoldPosition`으로 붙들어 둔다(`ManualMove`는 목적지 소진 시 자동 해제돼 못 잡는다) |
| `ExpeditionParty` | 월드맵 원정대(논리 데이터). `Members` / `CurrentNode` / **`Cargo`**(등짐) / `Capacity`(멤버 근력 합 × 5 — **필드 폰 적재와 같은 식**) / `TryAdd`(실제 받은 개수 반환) / `ClearCargo`. 자원 채집물이 여기 쌓이고 거점 진입 시 창고로 비워진다 |
| `SettlementManager` | 정착지 전환형. `Active` / `SwitchTo` / `SwitchToNode` / `RegisterAt` / `ActiveBiome`. **루트 활성화의 소유자** — 한 번에 하나만 시뮬되는 건 모드가 아니라 "활성 정착지는 하나"라는 규칙 때문. 시뮬 안 되는(= 지금 안 보는 다른) 정착지는 매일 오프라인 추상 생산 |
| `WorldBandManager` | **지도 위를 도는 무리**의 소유자 + **예고된 습격**. `Bands` / `FindContact(pos)` / `PushApart(band)` / `RemoveIfDead` / `BandFor(go)` / `TryGetRaidFrom(lairNode, …)`. **둘은 다른 축이다**(2026-08-10): 무리(`Roam`/`Caravan`/`Patrol`)는 제 볼일로 돌아다니고 **마주치는 것이 사건**이며, 작정하고 오는 습격은 **토큰 없는 사건**이라 며칠 전에 예고만 하고 도착 시 필드맵에 실제 적이 선다. **습격 빈도는 지도 전체가 하나로 정한다**(`raidIntervalDays`·`raidFirstDelayDays`·`maxPlannedRaids`) — 소굴마다 제 시계를 들면 **소굴이 늘어난 만큼 습격이 곱해져**, 바이옴에 소굴 종류를 얹는 것만으로 초반에 사방에서 몰려와 맞을 수밖에 없다(2026-08-14 수정). 전역 시계가 "언제 하나 온다"를 정하고 소굴은 **누가 올 차례인가**만 겨룬다(`LairEntry.RaidIntervalDays`가 절대 주기가 아니라 **가중치**로 읽힌다 — 작을수록 자주, 0이면 안 온다). 그래서 소굴을 늘리는 것은 **빈도가 아니라 다양성**을 늘린다. 습격을 토큰으로 걸어오게 하면 며칠짜리 행군이 화면을 기어가고 "가로막을 수 있나"가 또 다른 규칙이 된다 — **막는 길은 소굴을 치는 것 하나로 족하다**(치면 무산되고, 두들겨 놓으면 그만큼 적게 온다). **포로**(`WorldBand.Captives` → 근거지에 닿으면 `WorldLair.Captives`): 진 원정대는 밀려나는 게 아니라 **붙잡혀 끌려간다**(폰은 안 지운다 — 꺼진 채로 실려 다니므로 이름·스탯·특성이 살아 있다). 되찾는 길 다섯(**몸값**(`WorldRansom`) / 그 무리를 이긴다 / 소굴을 친다 / 무리가 전멸한다 / 하루마다 굴리는 탈출)은 **전부 `WorldMapView.RestoreParty` 하나로 모은다** — 길이 넷인데 처리가 넷이면 하나가 어긋나 사람이 조용히 증발한다. **탈출 확률은 안전장치다**: 되찾을 길이 "이긴다"뿐이면 인원을 다 잃은 뒤엔 이길 수가 없어 지는 것이 손실이 아니라 막다른 길이 된다. 소굴 해방은 `WorldLairs.Clear` 안에 둔다(소굴이 없어지는 길이 여럿이라 호출처마다 챙기면 빠뜨린다). 떠돌이는 `RoamDays` 뒤 근거지로 돌아가 **병력을 복귀시킨다**(무한정 떠돌면 `maxBands`가 차서 새 무리가 못 나오고 소굴을 쳐도 이미 나온 것이 남는다). 소굴·마을이 무리를 내보내고, 실시간으로 목적지까지 걷고, **무리끼리의 조우는 여기서 조용히 결판난다**(알림 없음 — 남들 싸움에 창이 뜨면 내 원정대 알림이 묻힌다). **원정대와의 조우는 `WorldMapView`가 메뉴로 받는다**(이동을 멈추고 창을 여는 주인이 그쪽). **월드맵을 안 보고 있어도 돈다**(거점 시뮬이 계속 도는 것과 짝). 씬 배선은 이미 서 있다(`WorldBandManager` + `Band_Caravan`·`Band_Goblin_Raiders`·`Band_Militia`) — **연결이 여섯 군데**라 하나만 빠져도 조용히 아무 일도 안 일어난다(세력 SO 미연결 → 전부 중립 / 바이옴 풀 비었음 → 소굴이 아무도 안 내보냄 / 씬에 매니저 없음 → 무리 자체가 없음) |
| `WorldLairs` | 적 소굴(`EnemyLair` 칸)의 병력·출격 상태. `At(node)` / `BuildAll()`(미리 다 깨운다 — 가보지 않은 지역의 소굴도 저 혼자 내보내야 한다) / `Clear(lair)` / `RegenAll()`. 어떤 소굴인지는 `BiomeContentData.Lairs` + **칸 좌표 + 시드로 결정적**(`TownMarket` 패턴). **날짜는 안 섞는다** — 병력은 그날의 사정이 아니라 누적된 상태라 매일 굴리면 어제 친 소굴이 멀쩡해진다 |
| `WorldRansom` | **붙잡힌 사람을 돈으로 되찾는다.** `Gold`(`Wallet.Balance`) / `PriceFor(폰\|여럿)` / `Available(party)` / `TryPay`. **값 = 스탯 + 장비**이고 사람 값은 고용가(`PawnSpec.PriceBase/PricePerGrade`)와 **같은 상수**를 쓴다(값이 두 벌이면 "고용은 싼데 되찾는 건 비싸다"가 사고가 된다). 되사는 값이라 ×1.4, **흔들림 없음**(창을 여닫을 때마다 바뀌면 값이 나올 때까지 여닫게 된다). 등급 평균은 명세가 아니라 **실제 스탯**에서 낸다(굴려서 태어난 폰도 붙잡히고, 자란 만큼도 값에 실린다). 지불은 **배낭 먼저 그다음 창고**(`MaterialPool.Consume`과 같은 순서) — `TakeGold`가 전액 아니면 0이므로 **가진 만큼만 청구**한다(부족한 액수를 그대로 넣으면 한 푼도 안 나와 "둘을 합치면 되는데 못 낸다"가 된다) |
| `WorldBattle` | **지도 위의 자동 전투**(M&B의 auto-resolve). `PowerOf(BandMember/UnitBrain/멤버들)` / `Resolve(party, band)` / `ResolveBands(a, b)`. 전력 = **공격·방어·강인함·회피** 넷의 가중합(전부 다 세면 요리 실력이 전투를 바꾼다). **장비는 양쪽 다 세되 표현이 다르다** — 원정대는 실제 무기·갑옷을 읽고, 무리는 `WorldBandData.PowerMultiplier`가 그 자리를 대신한다(한쪽만 세면 무장한 쪽이 늘 이겨 판정이 결론이 된다). 결과는 **인원 삭제가 아니라 피해** — `BodyPartSystem.TakeDamage`를 여러 번 나눠 때리고 기절/사망은 기존 체계가 정한다. 조우도 소굴 습격도 **같은 판정**(규칙이 둘이면 어느 쪽이 나은지가 계산이 아니라 눈치가 된다) |
| `WorldMap` | 추상 바둑판 월드맵. `GetTile` / `SetContent` / `RegisterSettlement` / `Neighbors` / `Distance` / `OnNodeChanged`. **매장 자원**: 칸마다 무엇이 얼마나 묻혔는지는 `WorldDeposits.At(node, tile, seed)` — 종류는 `BiomeContentData.Deposits`(바이옴), 수량은 **칸 좌표+시드 해시로 결정적**(같은 축으로 `TownMerchants`가 마을 상인을 고른다). 값은 항상 같고 **조사(`IsSurveyed`/`MarkSurveyed`)해야 보인다**(안개 아님 — 가려지는 건 세부 수치뿐). `ResourceSite` 칸은 ×2.5. **저작한다, 절차 생성하지 않는다** — `mapData`(`WorldMapData` SO)를 연결하면 그대로 싣고 크기·시작 노드도 데이터가 이긴다. 없으면 빈 맵 + 경고. 칸이 갖는 건 **바이옴(콘텐츠 게이팅) · 내용물**뿐 — 높이·생김새는 저작한 지형 몫이다. 저작은 `DungeonColony → World Map Editor`(브러시 3종). **안개 없음**(2026-06-20 결정) |

### 몸이 요구하는 것 둘 — `Needs` (허기 · 피로)

**둘은 기계적으로 같은 물건이다**: 0~100에서 저절로 줄고, 임계(`Threshold` **40**) 아래면 폰이 채우러 가고,
바닥에 닿으면 몸이 상한다. 갈리는 것은 **눈금과 채우는 방법**뿐이다. 허기 하나뿐이던 시절에도
그 규칙이 여섯 군데에 흩어져 있었으므로(블랙보드·감소·임계·`EatNode`·포로 급식·살펴보기 창)
축이 늘 때마다 그 여섯이 통째로 복사된다 — 그래서 `Needs` 하나로 모았다(2026-08-20).

| 축 | 100→임계(40) | 임계→가득 | 100→0 | 바닥에서 |
|---|---|---|---|---|
| 허기 | **540초**(하루 3.3끼) | 먹으면 즉시 | 900초 | 혈액 2/s → **65초**면 죽는다 |
| **피로** | **1200초**(하루의 2/3) | **침대 600초 / 바닥 1200초** | 2000초(1.11일) | **안 죽는다 — 그 자리에 쓰러져 잔다** |

**피로의 눈금은 0~100이 아니라 임계(40)~가득(100) 사이다.** 폰은 바닥이 아니라 **임계에서 자러
가고 가득 차면 일어나므로**, 0~100으로 재면 실제 주기가 그 40%로 줄어 **하루에 두세 번 눕는다**
(4분 자고 8분 일한다). 지금은 **1200 + 600 = 1800초, 정확히 하루 한 번**이고 그 600초가
어두운 때(밤+새벽)와 같은 길이다. **그래서 임계를 옮겨도 피로 주기는 안 바뀐다** — 갈리는 것은
여유뿐이다(임계 60→40으로 내리면서 쓰러지기까지가 1.67일 → 1.11일, 밤새 바닥잠이 20 → 30점).
문턱을 내린 진짜 이유는 **허기**다: 60은 절반도 안 쓰고 움직이는 값이라 **하루 다섯 번** 창고를
왕복했는데, 그건 "하루 2끼"라고 적어 둔 눈금과 너무 멀다. 허기는 이 셈이 다르다 — 굶어 죽는 자리가 실제로 있어
0까지 쓰는 눈금이고, 임계는 *먹으러 갈까*만 정한다(그래서 실제로는 더 자주 먹으러 간다).

- **눈금은 하루(`GameTimeManager.DayDuration`)에서 나온다** — 초당 감소량을 상수로 박으면 하루
  길이를 바꿨을 때 안 따라온다. "하루에 몇 번"으로 적고 거기서 나눈다.
- **피로 1200초는 하루의 2/3고, 남은 600초는 어두운 때(밤+새벽 8시간)와 정확히 같다** — 잘
  굴러가는 콜로니는 저절로 *밤에 자고 낮에 일하는* 모양이 된다. 밤이 견디는 시간이 아니라
  **자는 시간**이 되는 자리고, `DayNightLighting`이 어둡게 한 그 시간이다.
- **저장은 한 벌이다** — `UnitBlackboard`가 `float[]`을 들고 `Hunger`/`Rest`는 그것을 가리키는
  **별명**이다. 축을 도는 코드는 `bb.Need(kind)`/`SetNeed`를 쓴다.
- **누가 도는가는 `Needs.Simulates` 하나가 답한다**(플레이어 세력 + 갇힌 사람, 업힌 동안은
  아무도 안 돈다). **축마다 다시 묻는다** — **갇힌 사람은 피로가 안 돈다**: 먹이는 것은 남이
  해 줄 수 있지만(`FeedJob`) **잠은 대신 자 줄 수 없고**, 갇히면 BT가 안 돌아 `RestNode`도
  안 돈다. 채울 길 없는 바는 관리할 것이 아니라 **화면의 거짓말**이다. 살펴보기 창도 같은
  답을 쓴다(안 도는 축은 줄 자체가 안 뜬다).
- **핵 보정(`HungerRateModifier`)은 허기에만 건다** — *"이 핵의 하수인은 덜 먹는다"*는 뜻으로
  저작된 손잡이라, 피로까지 조용히 따라 움직이면 저작한 사람이 모르는 채 둘이 바뀐다.
- **한때 갈증이 셋째 축이었다**(2026-08-20에 넣었다 같은 날 걷어냈다). 걷어낸 이유는 눈금이
  아니라 **하는 일이 허기와 같아서**다: 둘 다 *창고에서 소모품을 찾아 먹기*이고 우물이 서면
  물은 사실상 무한이라, **축은 둘인데 플레이어가 내리는 결정은 늘지 않았다.** 설계(마실 것을
  `FoodData.WaterValue`로 두는 법, 우물 갈래, 간수가 떠다 먹이는 2-phase)는 그날 worklog에
  통째로 남아 있다 — **되살릴 때 잃는 것이 없으므로 코드는 지웠다.**

#### 잠 — **누가 배정했느냐가 배정의 뜻을 가른다** (`Bed.AutoClaimed`)

- **손으로 붙였으면 "가서 쉬어라"라는 명령이다** — 안 졸려도 눕고 **뗄 때까지 눕는다**(옛 규칙
  그대로). 그것이 **다친 동료를 눕히는 길**이다.
- **졸린 폰이 저 혼자 잡았으면 그냥 제 잠자리다** — 졸리면 가고 **가득 채우면 일어나** 일하러
  간다. 배정은 남아서 다음에도 그 침대로 간다.

**안 가르면 둘 중 하나가 깨진다.** 늘 눕게 두면 침대를 준 폰이 하루 종일 누워 있고, 피로만 보게
두면 **배정해도 안 눕는다** — 2026-08-20에 실제로 후자를 만들었다(*"침대에 배정하면 눕지도
않고"*). 그때 *"배정은 소유고 눕는 시점은 몸이 정한다"*고 적었는데, 그 한 문장이 **다친 폰을
눕히는 길을 통째로 없앴다.**

- **침대에서 일으킬 때 배정을 거둘지도 그 축이 정한다**(`Bed.RouseFrom` — 이동 명령이 부른다):
  손으로 붙인 것은 거두고(그 명령을 덮어쓰는 것이다), **저 혼자 잡은 것과 소집 중인 폰의 것은
  남긴다**. 점유와 자세는 늘 푼다. 그냥 `ReleaseFrom`을 부르면 **한 번 옮겼다는 이유로 잠자리를
  잃는다**(자원 노드에서 겪은 것과 같은 사고).
- **점유를 잃으면 `RestNode._bed`를 놓는다** — 그 필드가 히스테리시스의 기준이라(*"이미 누우러
  갔으면 가득 찰 때까지"*) 남겨 두면 **안 졸린 폰이 명령을 마치자마자 침대로 돌아간다**.
- **졸린데 제 침대가 없으면 빈 것을 잡는다**(`Bed.ClaimFreeFor` — 이쪽이 `AutoClaimed`를 세우는
  유일한 자리다). 사람마다 배정하게 두면 폰이 늘 때마다 잡일이 늘고, 잊으면 **그 폰만 조용히
  바닥에서 잔다**.
- **침대 600초 / 바닥 1200초**(임계→가득, `Needs.SleepPerSecond`). 바닥잠은 **밤새(600초) 자도
  40점 중 20점밖에 못 채워서** 다음 날 낮에 또 졸리고, **침대가 그 고리를 끊는다** — 그게 침대를
  짓는 이유다.
- **바닥(0)에 닿으면 자리를 안 가리고 쓰러진다** — 습격 한복판이든 광산이든. 임계에서 자러
  가는 것은 *판단*이고 이건 *한계*라, 피로는 안 죽이면서도 아프다.
- **저 혼자 잡은 잠자리는 가는 문턱과 나오는 문턱이 다르다**(`Threshold` 40 → `WakeAt` 100). **하나로 두면 문턱 위에서
  떤다** — 자러 가서 조금 차면 "이제 안 졸리다"가 되어 일어나고, 나가면 다시 졸려서 돌아온다
  (2026-08-20에 실제로 그랬다: 침대로 갔다 바로 일어나기를 되풀이했다). **누워 있을지 정하는
  자리는 `RestNode`에 하나뿐이어야 한다** — 두 곳에서 물으면 낮은 문턱이 늘 이겨 히스테리시스가
  도로 사라진다. **다친 것이 남았으면 계속 눕는다**(치료를 기다리는 폰이 잠이 깼다고 걸어
  나가면 안 된다).
- **자는 동안에는 피로가 안 준다**(`Needs.Decays`). 피로는 *깨어 있는 값*이라 그게 맞고, 안
  그러면 채우는 속도에서 주는 속도가 상쇄된다 — 침대는 두 배 느려지고 **바닥잠은 영영 안 찬다**
  (바닥 회복이 감소와 정확히 같은 값이라 net 0이다: 쓰러진 폰이 영원히 못 일어난다).
- **굶주림이 잠보다 먼저다** — 굶은 채로 자면 자는 동안 바닥을 치는데 **자는 폰은 스스로 못
  일어나 그대로 죽는다**.

### 밤 — 어두워지고, 덜 보이고, 습격이 몰린다

**하루를 다섯 때로 갈라 놓고도 그것을 읽는 곳은 시계 글자 하나뿐이었다**(2026-08-19까지). 화면은
스무 시간 내내 똑같이 밝아서 밤이 온 것을 **글자를 읽어야만** 알았고, 그러면 밤에 무엇을 걸어도
그건 밸런스 표의 숫자일 뿐이다.

**셋이 같이 있어야 뜻이 선다** — 어둡기만 하면 불편이고, 시야만 줄면 표의 숫자고, 습격만 몰리면
왜 밤에 몰려오는지가 화면에 없다.

| 축 | 답하는 것 | 저작 |
|---|---|---|
| **`DayNightLighting`** | 어두워진다 | 씬의 방향광에 붙인다. 때마다 색·세기·주변광 한 벌 |
| **`Vision`** | 덜 보인다 | `UnitData.NightVision`(0~1) |
| **`GameEventData.NightWeight`** | 습격이 몰린다 | 이벤트 에셋마다 |
| **`LightSource`**(화톳불) | **그중 둘을 되돌린다** | 건물마다 반경·세기 |

- **시계는 `GameTimeManager` 하나다.** 셋 다 때(`TimePhase`)·`IsDay`만 읽는다 — 어느 하나가 제
  축을 들면 *"화면은 저녁인데 판정은 밤"*이 생겨 어느 쪽도 못 믿게 된다.
- **광원은 하나다 — 낮에는 해, 밤에는 달.** 방향광 하나를 하루에 걸쳐 한 바퀴 돌린다(05시
  일출 → 21시 일몰 → 지평 아래 8시간, **경계는 `MorningHour`·`NightHour` 그대로**). 달을 위해
  등을 하나 더 두면 세기·색·그림자를 두 곳에서 맞춰야 하고, 겹치는 새벽·저녁에 무엇이 이기는지가
  또 규칙이 된다. **어두운 때에는 그림자를 끈다**(지평 아래라 그림자가 뒤집힌다 — 그 세기에서는
  어차피 안 보인다). 덤으로 **절차적 하늘이 광원을 따라가 저 혼자 저문다** — 스카이박스 머티리얼을
  런타임에 만지면 그 에셋이 실제로 바뀌어 플레이를 나가도 남는다.
- **색은 때가 들고 경계에서만 섞인다**(`blendHours` 1.5). 시각별 키프레임 표를 따로 두면 그 표와
  `GameTimeManager`의 경계 상수가 두 벌이 되어 반드시 어긋난다. 라벨이 `저녁`으로 바뀌는 그
  순간부터 빛이 물들기 시작한다.
- **밤은 어둡되 검지 않다** — 주변광(`ambientLight`)이 바닥을 든다. 방향광만 낮추면 그늘이 완전한
  검정이 되어 밤이 *어두운 것*이 아니라 **안 보이는 것**이 된다(분위기가 아니라 조작 불능이다).
- **시야 배율은 낮 1.0 · 저녁 0.7 · 새벽 0.5 · 밤 0.4**, 여기에 **밤눈**이 섞인다
  (`Vision.RangeFor` — 1이면 밤에도 낮처럼 본다). 해골 1.0 · 늑대 0.8 · 고블린 0.7 · 곰 0.5 ·
  사람 0. **밤을 모두에게 똑같이 걸면 세상이 흐려진 것일 뿐**이고, 저들만 보면 밤이 "불편한
  시간"이 아니라 **"저들의 시간"**이 된다.
- **묻는 곳은 교전 탐지(`DetectNode`)와 도주 탐지(`FleeNode`) 둘이고 답은 한 벌이다** — 따로 두면
  도망은 밤에도 멀쩡하고 싸움만 늦게 붙는다.
- **반격 거리는 밤에도 안 줄인다** — 맞은 것을 아는 것은 눈이 아니라 아픔이다. 여기까지 걸면
  등 뒤에서 맞고도 누가 때렸는지 모르는 채 서 있는데, 그건 밤이 위험한 게 아니라 폰이 고장 난
  것으로 보인다. **이미 도망 중일 때도 안 줄인다**(쫓기다 추격자를 놓치면 그 자리에 멈춰 잡힌다).
- **`NightWeight`는 이벤트마다 저작한다** — *"습격류는 밤에 두 배"* 하나로 묶으면 종류가 축이 되어
  밤에만 오는 것(짐승)과 낮에도 오는 것(산적)을 못 가른다. 지금: 망자·교단 3.0 · 짐승 2.5 ·
  고블린 2.2 · 산적 2.0 / 발견 1.0 / 합류·용병·떠돌이 0.6 / **행상인 0.2**(한밤중에 좌판을 펴는
  봇짐장수는 없다). **밤은 총량이 아니라 구성을 가른다** — 흔든 뒤 합을 때를 안 가린 값으로
  **되돌린다**(정규화). 그냥 곱하면 밤에 사건이 더 잦아져 *밤을 어둡게 한 값이 난이도 인상으로
  새어 나온다*(습격이 하루 0.82 → 1.15건). 우호도가 습격 총량을 안 건드리고 **누가 올 차례인가**만
  가른 것과 같은 자리다. 정규화하면 사건 빈도는 낮이나 밤이나 같고, 밤에는 **습격의 몫이 커진
  만큼 행상인·떠돌이가 밀려난다** — 밤이 무서운 것은 사건이 잦아서가 아니라 **올 것이 습격뿐**이어서다.
- **죽은 자가 일어나는 습격이 밤에 몰린다**(`Necromancy` × `NightWeight` 3.0) — 치우지 않은 시체가
  가장 안 보일 때 일어난다.
- **초상화는 조명을 안 탄다**(`PortraitCapture`) — 캡처하는 동안 **세상의 등을 전부 끄고**
  (방향광만이 아니다 — 폰은 제자리에서 찍히므로 곁의 점광원도 닿는다) 제 등 하나와 고정
  주변광으로 그린다. 안 그러면 **찍힌 시각이 그대로 굳는다**: 초상화는 한 번 찍고 영영
  캐싱되므로(`UnitBrain.EnsurePortrait`) 새벽에 로스터를 처음 연 폰은 하루 종일 어둡고, 낮에
  합류한 폰은 밝아 **같은 창에 두 사람이 다른 조명으로 나란히 뜬다**. 초상화는 *지금 몇 시인지*가
  아니라 **그 사람이 누구인지**를 알려 주는 그림이다. **제 등은 캡처하는 동안에만 켠다** —
  `cullingMask`에 기대면 안 된다: URP는 **가장 밝은 방향광을 메인 라이트로 고르고 그 선택은
  컬링 마스크를 안 봐서**, 켜 둔 등이 세상의 해를 밀어내고 낮밤이 통째로 멎을 수 있다.
- **불이 그중 둘을 되돌린다**(`LightSource` + `Campfire`, 2026-08-22). 셋을 걸어 놓고도 **플레이어가
  손댈 자리가 없으면 밤은 선택이 아니라 벌칙이다.** 불은 **어두운 것과 덜 보이는 것에만** 답한다 —
  습격은 벽·순찰·소집이 맡는다(하나가 셋을 다 되돌리면 그것을 짓는 것 말고 결정이 안 남는다).
  - **켜는 때는 `Vision.Factor < 1`이 정한다 — `IsDay`가 아니다.** 저녁은 `IsDay`가 참인데 시야는
    이미 0.7이라, 낮/밤으로 가르면 **화면이 물들고 덜 보게 된 뒤에도 불이 안 붙는다.** 시야 배율을
    손보면 켜지는 때가 저절로 따라온다.
  - **아침에 꺼지고 저녁에 켜진다 — 손이 안 간다.** 그래도 **꺼 둘 수는 있다**(우클릭
    `[불을 끈다]`): 불빛이 세력을 안 가리므로 *"이 자리는 어둡게 둔다"*가 값을 갖는다.
  - **밝기는 `LightSource.BrightestAt(자리)` 한 벌이 답하고**, 감지 범위로 바꾸는 식은
    `Vision.FactorAt`에 있다. **겹쳐도 안 밝아지고**(가장 밝은 것 하나) **가장자리에서 뚝 끊긴다**
    (부드럽게 줄이면 어디까지가 불빛인지를 눈으로 못 재 어디에 세울지가 짐작이 된다).
  - **세력을 안 가린다** — 우리 화톳불 옆에 선 산적도 그만큼 잘 본다. 그것이 거점을 밝히는 값이다.
  - **연료가 없다 — 값은 짓는 데서 한 번만 치른다**(돌 10). 목재를 태우게 했다가 **같은 날
    걷어냈다**(갈증과 같은 이유 — 눈금이 아니라 *결정이 안 생겨서*다): 목재는 벌목 몇 번이면
    남아돌아 "댈까 말까"가 성립하지 않았고, 폰이 안 나르는 설계라 **폰이 다 죽은 거점에서
    창고가 저 혼자 줄었다**. 값은 **어디에 몇 개 세우나**로만 치른다(반경이 있고 겹쳐도 안
    밝아지므로 그건 여전히 배치의 문제다). **꺼 두지 않고 지웠다** — 안 도는 분기가 남으면
    다음 사람이 그것부터 재야 한다.
  - **`Update`가 없다** — 답이 바뀌는 것은 하루 두 번뿐이라 `GameTimeManager.OnTimeChanged`에
    매달린다(`DayNightLighting`과 같은 자리).
  - **휴면에 넣는다**(`BuildingDormancy.GatherTargets`) — 안 넣으면 핵 영역 밖 화톳불이 혼자
    밝다. 휴면은 "전면 사용 불가"인데 그 하나만 예외가 되는 자리다.
  - **평소의 방랑자에게는 거의 이득이 없다** — 반격 전용이라 애초에 안 훑는다. 되돌아오는 것은
    **화면 · 하수인·경비의 탐지(4m → 10m) · 소집한 폰의 목줄 안 시야**다.
- **판은 아침에 시작한다**(`GameTimeManager.startHour` 7). `NormalizedTime` 0이 자정이라 손대지
  않으면 새 판이 **00:00에 캄캄하게** 열린다 — 처음 몇 분은 둘러보는 시간이라 거기가 밤이면
  안 보여서 못 하는 것이 된다. **`Elapsed`에는 안 얹는다**(`_clockOffset`이 따로 있다) — 그건
  *시작하고 얼마나 지났나*라 `MinGameTime` 게이팅이 쓰는 축이고, 더하면 판이 열리자마자 일곱
  시간이 지난 것이 되어 0.2일 뒤에 열리기로 한 사건이 첫 화면부터 후보에 든다. **하루가 바뀌는
  자리는 그대로 자정이다.**

### 작업 자리 (`WorkSpot`) — 도착 판정은 두 갈래

**"거기 서서 하는 일이냐, 손만 닿으면 되는 일이냐"가 규약을 가른다** — 그리고 그건 **일감이 정한다**(`JobBase.Reach`, 기본 `StandReach`).
- **`StandReach` 0.9**(칸 반대각선 0.71) — 작업대·연구대·침대·건설·치료. 저작된 자리에 **실제로 선다**. 넓게 잡으면 폰이 두 칸 떨어져 허공에 대고 제작한다. 못 서면 교착이므로 **2초 동안 못 좁히면 `ReachRadius` 2.0에서라도 시작**하고, 그 판단은 **래치로 붙든다**(일을 시작하면 `ResetProgress`가 정체 타이머를 지워서, 안 붙들면 "일한다 ↔ 다시 걷는다"가 매 틱 진동. 목표 칸이 바뀌면 저절로 풀림).
- **`HandReach` 2.0** — 운반(`HaulJob`·`GroundHaulJob`·`UnloadJob`)·연료·수확·급수. **집고 내려놓을 뿐이라 그 자리에 설 이유가 없고**, 좁게 잡으면 같은 창고를 여럿이 쓸 때 서로 비켜서느라 오래 걸린다. `ReachRadius`와 같은 값이라 **정체 타이머를 안 기다린다**.
- **단계마다 다를 수 있어 상수가 아니라 프로퍼티다** — `ConstructionJob`·`TreatJob`은 창고에 들를 땐 손, 청사진·환자 앞에서는 자리.
- **자원 노드** = `NodeReach` 2.0으로 **둘러서서** 캔다(일감이 아니라 BT 잎이 직접 잰다). 저작된 자리가 없고 `MaxWorkers`만큼 붙으므로 한 칸에 모으면 서로 비켜서다 아무도 못 캔다.

일감의 목표 칸은 건물 자기 타일이 아니라 **폰이 서는 자리**다. `JobBase.SetSite(site)`가 `WorkSpot.TileFor(site)`로 한 번에 정한다 — 각 Job이 따로 계산하지 않는다. 순서: 저작된 `WorkSpot` 자식(그 칸에 설 수 있을 때) → `MapManager.FindWorkableNear(center, 반경, 발자국 제외)`(대각선). **건물이 깔고 앉을 칸은 후보에서 뺀다**(`IFootprintSite` — `PlacedBuilding`·`BuildingBlueprint`가 같은 `FootprintAt`으로 답한다). 다 지어진 건물은 그 칸이 이미 통행 불가라 저절로 빠지지만 **청사진은 일부러 안 막으므로**(짓는 동안 폰이 지나다녀야 한다) 따로 물어야 한다 — 안 물으면 폰이 3×2 건물 한복판에 서서 짓고 완성되는 순간 발밑이 막혀 **갇힌다**(길찾기는 출발 칸이 막힌 것을 풀어 주지 않는다). **폰이 선 자리에 건물이 들어서면 `PlacedBuilding.OccupyTiles`가 밖으로 내보낸다**(`CharacterController`를 끄고 옮기고 캡슐 절반만큼 띄운다 — 스폰 Y와 같은 함정). 즉시 배치는 폰을 전혀 안 보고(41종 중 15종, 여러 칸짜리는 전부 이쪽) 길찾기는 출발 칸이 막힌 것을 안 풀어 주므로, 이건 예외 처리가 아니라 **주 경로**다. **걷는 중인 폰은 건드리지 않는다** — 제 발로 빠져나가는데 순간이동시키면 가던 길이 끊긴다(단 "목표는 있는데 경로가 없는" 것은 걷는 게 아니라 막혀 선 것이라 내보낸다). 나갈 데가 없으면 그대로 둔다. **던전 핵의 `WorkSpot`은 원정 집결 원의 중심**이다 — 같은 규약을 쓰므로 집결 장소가 저작 대상이 되고 건설 모드에서 표식으로 보인다(핵은 씬 오브젝트라 그 자식으로 직접 세워 뒀다). **저작은 선호지 보장이 아니다** — 나중에 막히면 자동 탐색으로 넘어간다. 표식은 **건설 모드에서만** 보여서 그 칸을 막는 배치를 눈으로 피할 수 있다. 자리는 **프리팹·씬에 저작돼 있다** — 옮기거나 더할 때는 프리팹에서 자식 `WorkSpot`을 직접 다룬다.

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
- **플레이어가 배낭에 직접 넣는 건 막지 않는다** — 대신 넘긴 비율만큼 이동·공격 속도가 준다(`PawnMover` / `CombatHandler`의 무게 보정). 손으로 한 일을 거부하면 "왜 안 들어가지"가 되고, 자동이 한도를 무시하면 "왜 이 폰만 느리지"가 된다. `InventoryUI`가 `무게 12/15kg`을 띄우고 과적이면 붉게 물든다(액션 바 위 `CarryBar` — 씬 오브젝트).
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

**건설도 여기 있어야 한다**(2026-08-11에 들어왔다 — 그전엔 `Storage`만 봤다). 창고만 세면 둘이 따라온다: 상점에서 목재를 사 온 폰이 **그 목재로 집을 못 짓고**, **창고가 하나도 없으면 첫 창고를 못 짓는다**(창고도 청사진이라 자재를 가지러 갈 창고가 있어야 창고가 선다 — 새 정착지가 정확히 그 상태로 시작한다). `BuildingBlueprint`는 **배낭 변화를 이벤트로 못 듣는다**(`Storage.OnChanged`는 static인데 `Inventory.OnChanged`는 인스턴스 단위라 청사진마다 플레이어 폰 전부를 구독했다 끊어야 한다) — 2초 주기 점검으로 받는다.

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
Inventory.TryPlaceOverAt(inst,x,y,rot,out pushed)  // 놓기 + **밀어내기 한 문** — 발자국에 걸리는 것이 하나면 통에서 빼서 돌려주고(손에 들린다), 둘 이상이면 아무것도 안 함
InventoryGridUI.FindCounterpart(mine, item)  // 같이 열려 있는 반대편 통(우클릭 보내기)
```

**필터 통과 ≠ 들어갈 수 있음.** 만적인 창고도 `CanAccept`는 참이다. 운반 목적지를 고를 땐 반드시 `FindNearest`/`HasRoomFor`(자리까지 봄)를 쓸 것 — `CanAccept`만 보면 폰이 거기까지 갔다 못 넣고 짐을 떨구고, 그 짐이 다시 같은 일감이 되어 **줍고 떨구기를 무한 반복**한다. 받아 줄 곳이 없으면 **일감을 아예 만들지 않고** 출처에 쌓아 둔다.

**바닥의 물건은 아무도 안 건드린다 — 체크한 것만 실려 간다**(`DroppedItem.HaulAllowed`, 기본 **꺼짐**, 2026-08-13). 켠 것에는 **초록 구슬**이 떠서 눈에 보인다. 켠 물건만 5초마다 다시 시도하므로 "창고를 비우면 저절로 실려 간다"는 그대로다.

**찍는 길이 둘** — 우클릭 한 개(**[창고로 옮긴다] ↔ [여기 둔다]**, 양방향)와 **운반 지정 모드(H)**. 모드는 **철거 모드와 같은 조작**이다(`HaulDesignator` — 클릭=단일 / 드래그=일괄 / 미리보기, 다른 건 색과 **우클릭이 지운다**는 것뿐). 우클릭만 있으면 전투 뒤나 발견 뒤에 열 몇 개를 하나씩 눌러야 한다. **끄면 가던 폰도 돌려세운다**(일감을 `Cancel` — 헛걸음을 아끼는 규약보다 "지금 그러지 마라"가 앞선다. 안 그러면 스위치가 거짓말이 된다).

전엔 **"누가 놓았나"로 갈랐다** — 손 드롭은 두고 사건 드롭(발견·철거 환불·작업대/수확 넘침)은 날랐다. 규칙 자체는 말이 됐지만 **화면에서 구별이 안 되고 스위치가 한 방향뿐이라**(켤 수는 있는데 끌 수는 없었다) "왜 이건 나르고 저건 안 나르지"가 **고칠 수 없는 것**으로 보였다. 기본을 전부 끄고 켜는 쪽만 두니 규칙이 한 줄이 된다.

**설비가 내놓는 것은 이 축과 무관하다** — 자원 노드·작업대 산출물은 바닥을 안 거치고 `HaulDispatcher`가 직접 일감을 낸다. **생산 라인은 그대로 자동이다.**

(손 드롭은 여전히 **커서가 UI를 완전히 벗어났을 때만** 떨어진다(`DraggableItem.TryWorldDrop`) — 전엔 격자만 봐서 장비 열·액션 바·패널 여백처럼 창 **안**의 격자 아닌 자리에 놓아도 바닥에 떨어졌다.) **반대로 "가지러 갈 곳"은 `FindStorageWithItem`** — 거기에 `FindNearest`를 쓰면 가득 든 창고가 오히려 탈락한다. **둘 다 거리를 본다** — 위치를 안 받으면 `All`에서 처음 걸리는 창고를 돌려줘, 창고가 둘 이상일 때 폰이 맵을 가로지른다. `from`은 **선택 인자가 아니다** — 기본값을 두면 부르는 쪽이 조용히 빠뜨린다(`ResourceNode.Work`에 델타를 못 빠뜨리게 한 것과 같은 이유). 기준은 **폰이 선 자리**지 일감이 매달린 건물이 아니다(걷는 것은 폰이다).

**부린 짐의 잔량 처리는 `Storage.Deliver` 하나다** — 지정 창고 → 받아 줄 다른 창고 → 배낭 → 바닥, **뒤로 갈수록 나쁜 쪽**. 배낭이 바닥보다 앞인 건 들고 있으면 짐내림이 옮겨 주지만 바닥에 떨구면 그게 다시 운반 일감이 되기 때문이다. 전엔 `HaulJob`·`GroundHaulJob`·`FarmHarvestJob`이 각자 적어 두고 **셋이 달랐다**(하나는 `FindNearest`에 수량·등급을 안 넘겼고, 하나는 `Inventory.TryAdd`로 **필터를 우회**했다).

**운반 목적지는 도중에 사라진다.** 일감을 만들 땐 있던 창고가 폰이 걸어가는 사이에 철거될 수 있다 — 안 보고 `_storage.transform`을 만지면 `MissingReferenceException`이 `UnitBrain.Update`까지 올라가 **그 폰의 BT가 통째로 멎는다**(`GroundHaulJob`·`HaulJob`이 둘 다 이랬다. 후자는 부릴 때만 봤고 목적지 칸을 정할 때는 안 봤다). `Storage.Rebind`로 갈아타고, **갈 데가 없으면 그냥 들고 있는다** — 이 일감들은 받아 줄 창고가 있어야만 만들어지므로 여기서 없다는 건 그새 철거됐다는 뜻이고, 이미 간 걸음을 버릴 이유가 없다(짐내림이 나중에 옮긴다).

**`amount`·`quality`를 빠뜨리지 말 것.** 기본값(1개·일반)으로 물으면 "한 개는 들어간다"만 확인하고 50을 지고 간다. 등급은 더 조용히 틀린다 — 스택은 **같은 등급끼리만** 합쳐지므로(`Inventory.TryAdd`), 등급을 빼먹으면 `일반 빵 30/99`가 든 만적 창고가 `희귀 빵`도 받는다고 답하고, 폰이 거기까지 갔다 못 넣어 **줍고 떨구기가 그대로 남는다**(`Inventory.CanFit`이 `TryAdd`와 같은 등급 검사를 한다). `FindNearest`는 **두 번 훑는다** — 통째로 받아 줄 곳이 먼저, 없으면 한 개라도 받는 곳. 그래서 넉넉히 물어도 일감이 사라지지 않는다.

### Job Categories & 배정 모델

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research, **Unload**(짐내림), **Medical**(치료), **Warden**(간수). **값은 뒤에 붙일 것** — 직렬화가 정수라 중간에 끼우면 `MinionData.AllowedJobs`가 통째로 밀린다.

**두 갈래** — `JobCategoryInfo`가 단일 출처.
- **큐 작업**(`QueueOrder`: **치료** → **간수** → 건설 → 농사 → 제작 → 연구 → 운반 → 짐내림 — 치료가 맨 앞인 건 피 흘리는 사람은 기다릴 수 없고, 그 일감은 다친 폰이 침대에 누웠을 때만 생겨 늘 있지도 않기 때문이다. 간수가 그다음인 건 **붙잡은 적이 깨어나면 걸어 나가기** 때문이다 — 늦으면 붙잡은 것 자체가 없던 일이 된다): 건물/농지/작업대가 일감 등록 → 폰이 집어감. **배열 순서 = 전역 우선순위**(모든 폰 공통). `QueueOrder`에 없는 카테고리는 큐에서 배정되지 않는다.
- **직접 배정**(Mining/Logging = 자원 노드, Guard = 순찰 구역): 플레이어가 대상에 폰을 붙임(`Blackboard.AssignedNode`/`AssignedPatrolZone`). 큐를 타지 않으므로 on/off 대상이 아님. **일감을 안 만든다** — `WorkNode.TickAssigned`가 배정된 노드를 직접 캔다.

Job score = `JobCategoryInfo.Weight(category) × 100 + job.Priority`.

폰별 설정은 **숫자 우선순위가 아니라 on/off** — `WorkSettings`(`UnitBrain.Work`, 플레이어 세력 일꾼에만 자동 부착). UI는 `ColonyUI`(P키). 상세 `docs/jobs.md`.

### Damage Quick Ref

```
DamageCategory: Physical / Elemental / Dark / Holy
PhysicalType:   Slash / Pierce / Blunt          (Physical일 때)
DamageElement:  None / Fire / Ice / Lightning / Poison  (비Physical일 때)
```

### 몸의 눈금 — 켄시 값 (부위 100 균일 · 혈액 100)

**머리·가슴·배·팔·다리 전부 100**(`BodyPartSystem.InitBodyParts`), **혈액도 100**, 파괴는 **−100 고정**(`BodyPart.IsDestroyed`) → 무력화(0)에서 **꼭 그만큼 더** 맞아야 부위가 떨어진다(1:2, 켄시 그대로).

**부위의 무게는 HP가 아니라 `HitWeight`(머리 10 · 가슴 18 · 배 16 · 팔 각 13 · 다리 각 15 = 몸통 34% · 사지 56%)와 그 부위가 무엇을 하느냐가 정한다** — 코어(머리·가슴·배)는 나가면 쓰러지고, 팔이 나가면 못 때리고 다리가 나가면 못 걷는다. HP로 또 가르면 같은 것을 두 번 표현하는 셈이고, 실제로 그러면 *"가슴은 두 배로 질긴 데다 제일 자주 맞는다"*가 되어 다른 부위가 뜻을 잃는다. **HP가 균일한 지금 부위를 가르는 축은 사실상 `HitWeight` 하나다.**

전엔 부위가 30/50/40/25였다. 눈금이 1/3이라 **팔다리가 한 방에 무력화됐고**(25 HP ↔ 강철 대검 26) 파괴 문턱만 −100 고정이라 *무력화는 한 대, 절단은 다섯 대*라는 이상한 사이가 생겼다.

- **피해는 안 올렸다** — 무기(9~26) · 방어구 평탄 감소(1~9) · 상태이상 전부 그대로다. 그래서 **전투가 약 두 배 길어진다**(의도한 것 — 켄시식 난전).
- **대신 타격당 혈액 손실을 절반으로 줄였다**(`slashBloodFactor` 0.5→**0.25** · blunt 0.1→**0.05** · other 0.25→**0.125**). 혈액이 100 그대로인데 부위만 세 배로 질겨지면 **모든 전투가 출혈 KO로 끝나고 절단을 영영 못 본다**:

  | | 출혈 KO | 가슴 무력화(기대) |
  |---|---|---|
  | 예전(가슴 50) | 7.7대 | 6.4대 |
  | 부위 100 · 계수 그대로 | **7.7대** | 12.8대 ← 출혈이 늘 먼저 |
  | 부위 100 · **계수 절반** | 15.4대 | **12.8대** ← 부위가 먼저 |

  (강철 대검 26 기준. 무력화는 피격 가중치를 감안한 **총** 타격 수.)
- **몸통 비중이 곧 전투 길이다 — 쓰러뜨리는 것은 코어뿐이라서**(`IsCorePart`). 그래서 몸통을 50%(가슴 30 + 배 20)에서 **34%로 낮췄다**: 부위 HP를 100 균일로 올리고 나니 *팔 하나 무력화 38대 : 전투 13대*가 되어 **켄시의 상징인 절단·의족을 구조적으로 못 보는** 배치였다. 지금은 총 타격 수가 **가슴 21.4 · 배 24 · 다리 25.6 · 팔 29.6 · 머리 38.5**로 어느 하나가 압도하지 않는다(다리 > 팔 — 표적도 크고, 나갔을 때 못 걷는다는 뜻도 무겁다).
- **그래서 쓰러지는 것은 대개 출혈이다**(베기 15.4대 ↔ 가슴 21.4대) — 켄시도 난전에서 눕는 이유는 대개 실혈이고 부위 파괴는 그 뒤에 오래 두들겨 맞을 때 온다. **무기가 이 축을 가른다**: 둔기는 혈액 계수 0.05라 **부위로 눕히고**(뼈 몽둥이의 *"죽이기보다 눕히기에 낫다"* — 출혈로는 59대가 걸린다), 베기는 0.25라 **피로 눕힌다**. 부위 쪽을 먼저 오게 하려면 **`slashBloodFactor`를 더 내린다**(0.25 → 0.18이면 21.4대로 가슴과 나란해진다) — `HitWeight`를 도로 올리면 사지가 다시 안 상한다.
- **혈액은 강인함을 통과한 뒤의 피해에서 뺀다**(`actualDamage * bloodFactor`) — 그래서 강인함이 높아도 부위와 혈액이 **같은 비율로** 늦춰진다.
- **부위 축만 ×3으로 같이 올린다**(평균 부위 HP 31→100): `partRegenPerSec` 0.8 · `RestNode` 부위 3 · `Treatment.BasePartHeal` 38 · `coreKoDepth` 30 · **`WorldBattle`이 주는 피해**(30~60 · 9~27 — 그건 무기 피해가 아니라 *"진 쪽이 이만큼 다친다"*는 결과라, 안 올리면 **진 원정대가 멀쩡히 걸어 나온다**).
- **혈액 축은 전부 예전 값 그대로다** — `maxBlood` 100 · 부위별 `BleedRate` 0.2~0.8(지속 출혈은 `clotSeconds` 30초가 끊으므로 혈액이 100인 이상 예전 페이싱이 맞다. 줄이면 단일 부상이 아무것도 아니게 된다) · `bloodRegenPerSec` 0.2 · `RestNode` 혈액 2·캡 1 · `Treatment.BaseCapHeal` 8 · 아사 2. `HitWeight`와 비율로 재는 것(`deathBloodRatio`·`wakeBloodRatio`·`FleeBloodRatio`)도 그대로.
- **`maxBlood`·혈액 계수·`coreKoDepth`·회복률은 `[SerializeField]`라 프리팹이 값을 들고 있다** — 코드 기본값을 고쳐도 **이미 있는 프리팹에는 아무 힘이 없다**(`EventManager.checkInterval`이 씬에 10으로 박혀 있던 그 함정). 폰 프리팹 26종은 같이 고쳤다. **부위 HP와 `BleedRate`는 런타임 생성이라**(`InitBodyParts`) 코드만 고치면 된다.

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

**언데드는 기절하지 않는다 — 쓰러지는 구간 없이 그 자리에서 부서진다**(`UnitData.IsUndead`(해골) ∥ **일으켜진 몸**(`UnitBrain.Rise`) → `UnitBrain.IsUndead`). 기절은 *의식을 잃는 것*이라 해골에게도, 이미 한 번 죽어 일어난 몸에게도 성립하지 않는다. **갈리는 자리는 `BodyPartSystem.KnockOut` 하나** — 기절로 오는 길이 셋인데(혈액 임계 · 코어 부위 깊이 · 강인함 굴림) 각자 앞에서 물으면 하나가 빠져 *가끔* 기절하는 해골이 남는다. 그래서 **언데드는 혈액 0에서 부서진다**(사람이 −30까지 버티는 구간이 통째로 없다).

**딸려 오는 것**: 붙잡기·회유·몸값이 전부 기절에서 시작하므로(`PawnCarry.CanCarry`는 남이면 기절을 요구한다) **언데드는 산 채로 못 잡는다.** 해골을 갖고 싶으면 붙잡는 것이 아니라 **뼈로 빚는다**(핵 소환 25) — 뼈로 닫히는 고리와 오히려 맞물린다. 일어난 동료를 되돌릴 길이 없다는 것도 같은 자리다: **그래서 태우는 것이 값을 갖는다.** 되돌리려면 종족 에셋의 체크 하나(`IsUndead`)를 끄면 된다.

그래서 **때려눕힌 적은 내버려 두면 일어난다.** 확실히 죽이려면 우클릭 **[공격한다]**(문구는 서 있는 적과 같지만 안에서 `FinishWith`로 갈린다) — `UnitBlackboard.FinishTarget`에 그 대상을 지목하면 `IsAttackable(go, bb)`가 그 **하나만** 통과시킨다. 규칙을 고치는 대신 예외를 뚫는 이유: 자동으로 뚫리면 KO 회복이 사실상 사라지고, 그렇다고 길이 아예 없으면 이겨도 전투가 안 끝난다.

**예외는 한쪽 방향뿐이다** — 지목은 `MoveController`(플레이어 우클릭)에서만 붙으므로 **쓰러진 우리 폰은 적에게 여전히 안 맞는다**. 지목은 대상이 죽거나(`FightNode`) 다른 명령이 오면 거둬진다.

**쓰러진 놈은 막지도 피하지도 못한다** — `CombatHandler.TryBlock`/`TryBlockRanged`/`TryDodge`가 `IsDown`이면 즉시 false. 방어도 회피도 능동적인 동작이다. 회피를 `Evasion`으로 굴리면 회피 높은 폰은 기절해서도 잘 안 맞아 **눕힌 보람이 사라진다**(빗맞음을 넣고 싶으면 방어자 스탯이 아니라 고정 확률이어야 한다).

**누가 죽든 시체로 남는다 — 내 폰도**(2026-08-15). 한때 플레이어 세력만 그 자리에서 파괴했는데, 그 경로는 `BecomeCorpse`를 안 거쳐 **입고 있던 장비와 배낭이 통째로 증발했다**(원정 나간 폰이 죽으면 지고 있던 짐까지). 죽음이 *"장비를 되찾으러 가는 일"*이 되려면 몸이 남아야 한다 — `Corpse.WasPlayer`가 원래 그걸 상정하고 있던 자리다. 갈랐던 이유였던 "내 폰 시체가 쌓인다"는 **치우는 길**로 풀 일이지 몸을 없애서 풀 일이 아니다.
- **명부·무리 관리·원정 편성에서는 저절로 빠진다** — 셋 다 `Faction.IsPlayer`로 거르는데 `BecomeCorpse`가 `Faction` 컴포넌트를 지우기 때문이다. **선택은 따로 뺀다**(`SelectionManager.PruneSelection`이 `Corpse`를 본다) — 파괴되지 않으므로 `== null`로는 안 걸리고, 안 빼면 우클릭 명령이 송장에게 나간다.
- **혈액은 0이 아니라 −30에서 죽는다**(`deathBloodRatio` −0.3 × `maxBlood` 100, 켄시식 — 기절을 건너뛴 즉사를 막는다). 그래서 쓰러진 폰은 그 사이에 치료하면 살아난다. "시체가 갑자기 사라진다"의 정체가 이것이었다 — 사라진 것은 시체가 아니라 **아직 살아서 피 흘리던 내 폰**이었고, 죽는 순간 파괴됐다.
- **시체는 삭는다 — 그러나 사라지지는 않는다**(`CorpseStage`: 신선 1일 → 썩음 3일 → **뼈(영구)**). 단계가 곧 **할 수 있는 일**이고, 그것이 "언제까지 치워야 하나"를 만든다. 시각은 `GameTimeManager.Elapsed`로 잰다(배속·일시정지가 그대로 반영된다). 단계가 바뀌면 **색으로 알린다**(MPB — 시체마다 머티리얼 인스턴스가 생기지 않게).
  - **뼈가 저절로 안 없어지는 것이 요점이다.** 그게 매장·화장이 있어야 할 이유이고, **교단·망자 습격이 오면 맵에 남은 시체가 일어난다**(`Necromancy`, 2026-08-19). 그래서 **치우는 일이 잡일이 아니라 방어**가 된다 — 새 이벤트를 안 만들고 기존 습격에 얹으므로 습격 빈도는 안 는다.
- **치우는 길은 셋이다 — 무엇이 남느냐가 다르다**(`ButcherJob` / `Grave` / `Pyre`).

  | 길 | 값 | 남는 것 |
  |---|---|---|
  | **도축** | 공짜 + 폰의 시간 | 고기·가죽·뼈 — 다만 **나올 것이 있는 몸에만** 통한다(`Butchering.HasYield`) |
  | **매장**(무덤 2×1, 돌 18) | 그 **칸을 영영 먹는다** | 몸이 남는다 — **다시 파낼 수 있고**, 나중에 교단이 파낼 수도 있다 |
  | **화장**(화장터 2×2, 돌 40 + 태울 때마다 목재 5) | 시간(60초) + 목재 | 아무것도 — **되돌릴 수 없다** |

  **사람도 가를 수 있지만, 그게 무덤을 없애지는 않는다** — 도축은 *동료의 시신에 하는 일*로는 성립하지 않고(규칙 기본값이 적대 세력만인 것도 그래서다), 무엇보다 도축은 **몸을 없애 주지만 그 대가로 뼈를 남긴다**. 무덤과 화장터가 갈리는 것은 값이 아니라 **끝맺음**이다: 무덤은 싸고 즉시지만 미루는 것이고, 화장은 값을 치르고 끝내는 것이다. **그 차이는 이제 값을 갖는다** — 묻은 몸은 **무덤을 파는 세력**(교단)만 일으키고, 태운 몸은 어느 쪽으로도 안 돌아온다(`Necromancy`). 치울 길을 **그 기능보다 먼저** 붙여 둔 이유가 그것이다(치울 길이 없는 채로 시체가 일어나면 그건 선택이 아니라 사고다).
  - **묻히면 시간이 멈춘다**(`Corpse.SetBuried` — `DiedAt`을 옮겨 나이를 얼린다. 파내면 거기서 이어 삭는다). 땅속에서도 삭게 두면 무덤이 *"보이지 않는 곳에 쌓아 두는 자리"*가 된다. 묻는 동안 렌더러·콜라이더를 끈다(**`CharacterController`는 빼고** — 그것도 `Collider`라 한꺼번에 훑으면 딸려 온다).
  - **유품은 안 태운다** — 태우는 것은 몸이지 물건이 아니고, 안 뒤지고 올려놓았다고 장비가 사라지면 조용한 손실이다(`ButcherJob`과 같은 규칙). 발밑에 떨궈 운반 축(H)에 올린다. **무덤은 같이 묻는다** — 파내는 길이 있으니 잃는 것이 아니다.
  - **유품에 답하는 것은 `Corpse.SpillBelongings` 하나다 — 배낭과 *차고 있던 장비* 둘 다 내놓는다.** 장비는 격자를 안 먹으므로(시체 창이 장비 열을 따로 그린다) **배낭만 훑으면 무기·갑옷이 시체와 함께 조용히 사라진다** — 도축도 화장도 마지막에 몸을 없애기 때문이다. 실제로 `ButcherJob`과 `Pyre`가 각자 배낭만 훑고 있었고, 화장터에는 *"장비가 사라지면 조용한 손실"*이라고 **적혀만 있었다**(2026-08-16). 답을 시체에 둔 이유: 부르는 곳이 둘이고 앞으로 늘 텐데 각자 적으면 하나가 반드시 장비를 빠뜨린다. **무덤은 안 부른다** — 같이 묻고 파낼 수 있어서 잃는 것이 아니고, 그것이 무덤과 화장을 가르는 축이다.
  - **화장터에는 폰이 안 붙는다.** 시체를 올려놓는 것 자체가 이미 사람이 업고 와서 하는 일이고, 불을 지키고 서 있는 것은 아무것도 안 정한다(핵이 우리에 가둔 하수인을 저 혼자 지배해 가는 것과 같은 자리) — 그래서 **새 일감도 새 작업 카테고리도 없다**. 연료는 **불을 붙일 때 한 번** 낸다(매 틱 조금씩 빼면 "반쯤 탄 시체"라는 상태가 생기는데 그걸 표현할 데가 없다). 모자라면 멈추고 **줄에 이유를 적는다**(`BlockedReason`).
  - **건물이 없어지면 시체가 도로 나온다**(`OnDisable` → `Exhume`/`Eject`) — 안 꺼내면 묻어 둔 몸이 건물과 함께 조용히 사라진다.
- **죽은 자가 일어난다 — `Necromancy`**(2026-08-19). **치우지 않은 시체가 습격의 재료가 된다.** 교단·망자가 쳐들어오면 맵에 널브러진 시체가 **그 자리에서** 일어난다 — 습격이 가장자리에서 걸어 들어오는 것이 아니라 **거점 한복판에서** 시작된다.
  - **일으키는 것은 세력이 답한다**(`FactionData.RaisesDead` / `ExhumesGraves` / `RisenBones` — `AcceptsRansom`·`ForHire`와 같은 자리). **습격이 오는 길이 둘**이라(이벤트 추첨 · 지도에서 걸어온 무리) 이벤트 에셋에 적으면 **지도에서 온 교단은 시체를 그냥 지나친다** — 플레이어에게 그 둘은 똑같이 '교단이 몰려온 것'이다(구원이 양쪽에 다 붙는 것과 같은 이유). 그래서 **실제로 쳐들어온 폰들의 소속을 묻는다**(`Necromancy.OnRaidArrived(arrivals)`) — 배선을 하나 빠뜨려 조용히 아무 일도 안 일어나는 자리가 안 생긴다.
  - **무엇이 되는지는 시체의 단계가 정한다.** **살점이 남았으면(신선·썩음) 그 몸이 그대로 일어난다** — 이름·스탯·특성·**차고 있던 장비까지**(`UnitBrain.Rise`). 프리팹이 필요 없고, 무엇보다 **죽은 동료가 내가 입혀 준 갑옷을 입고 걸어온다**. **뼈만 남았으면 해골이 선다**(`RisenBones`) — 그 모양은 더 이상 그 사람이 아니므로 몸을 없애고, **유품은 `Corpse.SpillBelongings`가 내놓는다**(도축·화장과 같은 규칙).
  - **`UnitBrain.Rise`는 `BecomeCorpse`의 짝이다.** 저쪽이 **끄기만 하고 아무것도 안 지운** 덕에(*"되살릴 일이 생기면 그대로 되돌릴 수 있다"*고 적혀 있던 그 자리다) 도로 켜면 그 사람이 선다. 되돌릴 것 넷: **`Corpse` 제거**(안 지우면 업기·도축·무덤이 산 사람을 시체로 본다) · **`Faction` 재부착**(`BecomeCorpse`가 컴포넌트째 지웠다) · **역할과 `Merchant` 제거**(안 지우면 일어난 상인이 제자리를 지키고 서서 **죽은 자와 거래가 된다**) · **몸**(`BodyPartSystem.Rise` — 혈액이 사망 비율 −30에 있고 부위가 **파괴**돼 있어, 켜기만 하면 다음 틱에 `CheckBloodState`가 다시 죽인다. `HealBodyParts`가 건너뛰는 파괴 부위까지 되돌리는 유일한 자리다 — 일으키는 것은 치료가 아니라 죽음을 되돌리는 일이다). BT·성장 잠금은 `ChangeFaction`과 **같은 식**이어야 한다. **그리고 `_bb.Faction` 캐시를 같이 갈아 끼운다** — `UnitBlackboard`가 `Awake`에서 한 번 잡는 참조인데 `BecomeCorpse`가 그것을 파괴하므로, 새로 붙이기만 하면 블랙보드가 **파괴된 옛 참조**를 들고 `DetectNode`가 `myFaction == null` 가지로 빠져 **일어난 것이 아무도 안 때린다**(`ChangeFaction`은 같은 컴포넌트의 값만 갈아 이 함정을 안 밟는다).
  - **일어난 몸은 물들인다**(`Corpse.Tint` — 삭을 때 색으로 알리는 것과 같은 자리). 모양이 그대로라 안 물들이면 **내 폰과 눈으로 구별되지 않는다.**
  - **사람 손에 있는 것은 안 일어난다** — 업고 가는 중 · 도축대에 눕혀 놓은 것 · **화장터에 올려 불을 붙인 것**. 그 자리들은 저마다 몸을 붙들고 있어 자세·자리·일감이 한꺼번에 어긋나고, 무엇보다 불 위에서 일어나는 것은 화장을 *끝내는 길*로 둔 취지와 정반대다.
  - **부패 눈금**: 신선 ~1일(1배속 실기 30분) · 썩음 1~3일 · **뼈 3일~영구**(`Corpse.FreshDays`/`RottenDays`, 하루 1800초). **습격 주기(1~3일)와 겹치게 잡은 값이다** — 2일·4일이던 시절엔 신선한 몸이 실기 한 시간이라 싸움 뒤에 서두를 이유가 없었다. 시험은 `DungeonColony → Debug: Age Corpses → 썩음 / 뼈`(`Corpse.SetAge` — 나이를 옮기는 문은 `SetBuried`와 여기 둘뿐이다).
  - **묻힌 것은 무덤을 파는 세력만**(교단 `ExhumesGraves`, 망자는 아니다). 그것이 무덤에 값을 준다 — 망자 습격은 막아 주지만 교단에는 안 통하고, 화장만이 어느 쪽으로도 안 돌아오게 한다.
- **도축·도축대 세부는 `docs/jobs.md` "시체를 치운다 — 도축과 도축대"로 내렸다.** 밖에서 어기기 쉬운 것만 여기 남긴다:
  - **몸에서 나오는 것은 종족이 든다**(`UnitData.ButcherYield`/`BoneYield`, **갈라야 나온다**), **지니던 것은 `LoadoutData.Loot`**(태어날 때 배낭에 들어가 죽으면 그 자리에 남는다). **시점이 그 둘을 가르는 축이다.**
  - **단계 규칙은 `Butchering` 하나가 답한다**: 신선=전부 / 썩음=**음식만 빠진다**(`is FoodData`) / 뼈=뼈만.
  - **뼈가 고리를 닫는다** — 도축이 내놓는 `Res_Bone`이 소환 비용(고블린 12 · 해골 25) · 연구(`Res_Bonecraft`) · 무기(`wpn_bone_club`)로 돌아, **치우는 것이 잡일이 아니라 생산 라인의 앞단**이 된다.
  - **`JobCategory.Butcher`가 큐를 탄다**(제작 뒤 · 운반 앞). 자동이 동의 없이 벌어지지 않는 것은 큐를 막아서가 아니라 **규칙(`ButcherBill`)을 사람이 먼저 걸기** 때문이다.
  - **산 채로 쓰러진 것도 가른다** — 판정은 `Butchering.CanButcherAlive` 하나(우클릭 메뉴와 일감이 같이 묻는다). **가는 사이에 깨어나면 그만둔다.**
  - **한 몸에 일감은 하나**(`JobManager.HasButcherJobFor`) — 문이 둘이라(우클릭 · 규칙) 각자 걸면 두 폰이 같은 곳으로 걸어간다.
  - **상판에 눕힌 몸은 `SetSleeping`으로 붙든다** — 기절은 `IsSleeping`이 아니라 회전만 눕힌 상태라, 안 재우면 `PawnMover`가 매 프레임 중력을 먹여 **상판에서 끌어내린다.**

**시체는 죽은 그 순간에서 멈춘다** — `BecomeCorpse`가 `UnitBrain`·`PawnMover`·`CombatHandler`에 더해 **`BodyPartSystem`·`StatusEffectSystem`도 끈다**(안 끄면 시체의 혈액이 계속 줄거나 차고, 계속 불탄다). **`Faction` 컴포넌트는 지우되 소속은 `Corpse.Faction`이 기억한다** — 지우는 건 자동 탐지(`OverlapSphere` + `GetComponent<Faction>`, `enabled=false`로는 못 막는다)를 끊으려는 것이지 잊자는 뜻이 아니다. 그래서 **"모르는 것은 플레이어가 아니다"** — `faction == null`을 플레이어처럼 대하면 시체가 그 구멍으로 들어온다(허기 바가 떴었다).

### 업기와 가두기 — 어깨에 사람을 멘다

**붙잡은 적을 끌고 오는 것과 동료를 옮기는 것은 같은 동작이다**(`PawnCarry`) — 규칙을 둘로 나누면 하나가 어긋난다. **우리 편은 멀쩡해도 업고**(급히 옮기는 것은 명령이고, 받는 쪽이 우리 폰이면 싫다고 할 이유가 없다), **남은 기절해야 업는다**(멀쩡한 적을 메는 건 붙잡기가 아니라 제압이고, 클릭 한 번에 데려오면 싸울 이유가 사라진다). 깨어 있는 사람은 **세워서** 내려놓는다.

**시체도 같은 동작으로 치운다.** 메고 걸어가 내려놓는 것은 산 사람을 옮기는 것과 **글자 그대로 같은 일**이라 규칙을 따로 만들 이유가 없다(무게도 한 손 제약도 그대로 걸린다). **늘 업을 수 있다** — 죽은 자에게 기절·소속 판정은 뜻이 없고(`Faction`은 지워졌고 `IsKnockedOut`은 죽은 방식에 따라 들쭉날쭉하다) 안 가르면 답이 흔들린다. **자리에는 안 들어간다** — 침대도 우리도 안 받는다(`Bed.Accepts`·`PrisonCage.TryPut`): 눕히는 건 회복을 얹는 일이라 뜻이 없고, 넣어 두면 그 자리를 산 사람이 못 쓴다. **간수도 안 데려간다**(`PrisonEscort.IsCaptive`가 시체를 뺀다 — 안 빼면 시체를 업은 폰이 저 혼자 우리로 걸어간다). 시체인지는 **`PawnCarry.IsCorpse`가 답한다** — `BecomeCorpse`는 `UnitBrain`을 **끄기만** 하므로 `== null`로는 산 사람과 구별되지 않는다.

**그 "끄기만 한다"가 우클릭을 죽여 놨었다** — `MoveController`의 분기가 `if (brain != null) … else if (corpse != null)` 순서라 **시체 가지가 한 번도 안 돌았다**: 시체를 우클릭하면 [뒤진다] 대신 `따라간다`·`공격한다`가 떴다(따라갈 수도 칠 수도 없는 것에게). 좌클릭(`SelectionManager`)은 시체를 먼저 봐서 멀쩡했다 — **같은 물음에 두 곳이 다른 순서로 답하고 있었다.** 죽은 것은 무엇보다 먼저 시체다.

**두 팔로 안지 않고 한쪽 어깨에 멘다.** 그래야 값이 둘 붙는다:
- **무게** — `CarryCapacity.Load`가 업은 사람을 같이 센다(`PawnCarry.BodyWeight` 60 × 몸집 + 그 폰의 짐). 한도가 `10 + 근력 × 0.5`라 **근력 100쯤에서 같아진다** — 근력 10은 속도 25%로 기어가고 130은 거의 멀쩡하다. **업고 나르는 것이 힘센 폰의 일**이 된다. `PawnMover`·`CombatHandler`가 배낭 무게를 직접 세던 것을 `CarryCapacity.Load`로 돌렸다(한 곳만 고치면 이동도 공격도 따라온다).
- **일을 안 한다** — 업고 있으면 `WorkNode`가 일감을 안 집는다(큐 작업도, 배정된 자원 노드도). 한 손이 사람으로 막힌 것과 **같은 축**이고, 무엇보다 어깨에 시신을 얹은 채 광맥을 캐러 맵을 가로지르는 그림이 된다 — **업기는 그 자체로 명령**이라 그동안 그 폰이 하는 일은 옮기는 것이다. **하던 일감은 업는 순간 놓는다**(`PawnCarry.TryPickUp` → `AbandonWork`) — 자리로 데려가는 동안에는 `CarryPawnNode`가 Running을 물어 `WorkNode`가 **아예 안 돌므로**, 게이트에서만 놓으면 그 일감이 예약된 채 얼어붙어 **다른 폰도 못 가져간다**. **배정된 자원 노드도 같이 거둔다** — 배정도 플레이어가 붙인 명령이라 **새 명령이 옛 명령을 덮어쓴다**. **눕지도 않는다** — `RestNode`도 같은 게이트를 둔다(사람을 어깨에 얹은 채 잠자리에 드는 그림이 된다). **배정은 안 거두고 미루기만 하므로 내려놓는 즉시 제 침대로 간다**, 그리고 자고 있던 폰이 무언가를 업으면 `LeaveBed`로 **일어난다** — 누운 자세와 점유를 안 풀면 업은 채로 침대에 굳고 그 침대를 아무도 못 쓴다. **먹는 것은 그대로 둔다**(한 손으로 되고, 막으면 업은 채 굶는다).
- **한 손** — 양손 무기(`IsLargeWeapon`)도 방패도 못 쓴다. `CombatHandler.Weapon`/`Shield` 프로퍼티 **하나로 모았다**(여덟 군데에서 묻고 있었다 — 한 군데만 빠뜨려도 "업고도 대검을 휘두르는" 구멍이 남는다). **장비를 벗기지는 않는다**(벗기면 돌려 입힐 길이 필요하고 배낭이 차 있으면 샌다).

**업은 것을 받는 자리는 `ICarrySite` 하나로 답한다** — 우리·침대·**무덤**·**화장터**. 자리마다 다른 것은 넷뿐이다(어디에 놓나 · 어느 쪽을 보게 놓나 · 이 사람을 받나 · 받고 나서 무엇이 되나). 전에는 `UnitBlackboard`에 `PutDownBed`·`PutDownCage`가 나란히 있었고 `BTLeafNodes`가 삼항으로 골랐다 — **자리가 하나 늘 때마다 필드가 늘고 지우는 자리가 늘어서**, 무덤을 붙이는 순간 그 사다리가 넷이 될 참이었다(`CarryCapacity`·`Storage.Deliver`에서 이미 겪은 모양이다). 지금은 `PutDownSite` 한 칸이다.

**가두는 곳은 둘, 갇힌 상태는 하나.** 문도 방도 없으니 켄시처럼 **자리에 묶어 둔다**.
- `PrisonCage`(우리) — 가두는 것이 전부라 싸다. 한 명. 모델은 기본 도형이라 갈아 끼울 자리다(`PrisonCage.prefab`)
- `Bed.ForPrisoners`(죄수 침대) — **건설 목록에 별개 항목이 있고**(`PrisonBed.prefab`/`.asset` — 침대 프리팹 복제 + 플래그, 값·크기 동일) **우클릭으로 서로 바꾼다**(림월드식). 잡아 온 뒤 방을 돌리는 것과 애초에 감옥을 짓는 것은 다른 일이라 둘 다 둔다. **쓰고 있으면 안 바꾼다**. **`Bed.Update`가 갇힌 포로도 돌본다** — 이게 죄수 침대의 존재 이유다(안 돌보면 우리와 같은 물건이 된다). 지나가던 적은 여전히 안 돌본다. 눕힐 때 **점유와 배정을 같이** 한다(하나만 하면 패널이 "배정 없음"이고 `ReleaseFrom`이 거두는 짝이 어긋난다)
- **보통 침대는 붙잡은 사람을 안 받는다**(`Bed.Accepts`) — 받으면 갇힘이 안 걸린 채 자세만 눕고 다음 틱에 **누운 채로 걸어 나간다.** 붙들자니 보통 침대가 곧 죄수 침대가 되고 안 붙들자니 눕힌 것이 아무 뜻이 없다. 우클릭 선택지·도착 판정 둘 다 이걸 본다
- **용도는 매트리스 색으로 보인다** — 죄수용 바랜 주황 / 보통 바랜 초록. **매트리스 자체를 물들인다**(`tintTarget`, 비우면 이름에 `Mattress`가 든 자식 → 없으면 가장 큰 렌더러). 표식 quad를 얹어 봤다가 걷어냈다: 바닥에 깔면 침대 모델이 덮어 **한 번도 안 보이고**, 위로 올리면 침대에 스티커를 붙인 꼴이며, 자식이 늘어 `WorldBounds`가 커져 **폰이 침대 위에 떠서 잔다**(`SleepPosition` = 윗면 + `lieHeight`). **쨍하게 칠하지 않는다** — 자기를 봐 달라는 UI가 아니라 침대 천의 색이다
- **색은 코드가 아니라 에셋이 든다**(`Bld_5C8057`·`Bld_9E6133`, 프리팹에 미리 물려 있음). 런타임에 하는 일은 **둘 중 하나를 골라 끼우는 것**뿐(`sharedMaterial`). 코드가 `rend.material`로 칠하면 셋이 딸려 온다: ① **에디터에서 둘이 똑같아 보인다**(색이 게임 돌릴 때만 붙으니 프리팹을 열어도 구분이 안 된다) ② 침대마다 **머티리얼 인스턴스**가 하나씩 생겨 배칭이 깨지고 손수 `Destroy`해야 한다 ③ `OnEnable`이 **편집 중에도 불려서**(재컴파일·컴포넌트 추가) 씬에 이름 없는 머티리얼이 쌓인다 — 그래서 `Application.isPlaying` 가드를 달면 그 가드가 곧 ①이 된다. **저작하면 셋이 한꺼번에 없어진다**
- **MPB는 안 쓴다** — 철거 예정 표시가 MPB로 덮었다가 `SetPropertyBlock(null)`로 되돌릴 때 **머티리얼 본래 색**으로 가므로, MPB로 칠하면 철거 모드에 한 번 스치는 것만으로 색이 사라진다
- **`Imprisonment` + `UnitBlackboard.IsPrisoner`가 유일한 상태다** — 가두는 물건마다 BT를 따로 멈추면 한쪽에서 포로가 걸어 나간다
- **갇힌 사람도 다시 업는다**(`[끌어낸다]`) — 되돌리는 길이 [풀어 준다] 하나뿐이면 자리를 못 바꾼다. 우리·침대에서 놓아 주는 건 `PawnCarry.TryPickUp`이 한다(업기 전에 늘 거치는 길이라 호출처마다 안 챙겨도 된다)
- **우리 콜라이더는 루트에** — `hit.collider.GetComponent<IWorldClickable>()`로 묻기 때문에 자식에 있으면 눌러도 아무 일도 안 일어난다(침대도 루트에 있다). 트리거이고 기둥에는 콜라이더가 없다(막으면 문 없는 상자가 된다)
- **눕히는 길은 `PawnMover.SetSleeping` 하나다** — 우리 폰이 제 발로 눕는 것(`RestNode`)과 죄수 침대에 눕히는 것(`Bed.PlacePrisoner`)이 같은 길을 쓰고, `Imprisonment.Hold`는 `IsSleeping`이면 손을 뗀다(**우리에 선 포로만** 붙든다). `Bed.ReleaseFrom`은 짝으로 `SetSleeping(false)` — 안 풀면 침대 밖으로 나가서도 누운 채 굳는다(`PawnMover.Update`가 통째로 멈춰 중력도 조향도 안 돈다)
- **기절 회복은 "지금 누울 자리가 있는지"를 직접 묻는다** — `BodyPartSystem`이 무조건 `_preKoRotation`으로 되돌리면 침대에 눕힌 포로가 다 나은 순간 **벌떡 일어선다.** 우리 폰도 똑같이 그랬지만 다음 틱에 `RestNode`가 도로 눕혀서 한 프레임짜리 튐이 안 보였을 뿐이다 — 포로는 BT가 안 돌아 그 복구가 없다. **되돌린 뒤 누가 다시 눕혀 주기를 기대하지 않는다**(그 "누가"가 없는 경로에서 반드시 드러난다). 깨어나는 것과 일어서는 것은 다른 일이다
- **새로 업으면 갈 곳은 백지다** — `PawnCarry.TryPickUp`이 `HasPutDown`/`PutDownSite`를 지운다. 안 지우면 `[업는다]`만 시켰는데 폰이 **저 혼자 예전 우리로 걸어간다**. 업기가 시작되는 문이 그 함수 하나뿐이라 명령 내는 쪽마다 챙기게 하면 언젠가 빠뜨린다. `MoveController.MoveTo`도 같이 거둔다(딴 데로 가라는 명령 = 옛 명령 취소, `Target`·`FollowTarget`과 같은 줄). **도착해서 자리를 다시 보는 것**(`CarryPawnNode`)은 그와 별개의 안전망 — 걸어오는 사이 남이 먼저 넣었을 수 있다

**부모 크기를 상쇄하고 붙인다** — 그냥 자식으로 달면 업힌 사람의 월드 크기가 **업은 사람 크기만큼 곱해지고**, 내려놓을 때 `SetParent(null, true)`가 그 값을 굳혀 **업을 때마다 커진다**.

**이미 붙잡은 사람은 깨어나도 업는다** — 기절을 요구하는 건 *붙잡는 순간*의 규칙이지 붙잡아 둔 사람을 옮기는 데까지 걸 이유가 없다(회복해 눈뜨면 우리에서 꺼낼 길이 사라진다). 갇힌 사람의 선택지는 **끌어내기·치료·처형** 셋뿐이다(거래·따라가기는 빠진다).

**침대에 눕힐 때는 침대가 방향을 정한다**(`Bed.SleepRotation` — `RestNode`가 제 발로 누울 때 쓰는 것과 같은 값). 안 그러면 발이 베개를 벤다.

**업힌 사람과 갇힌 사람은 아무도 안 때리고 BT도 안 돈다.** 갇힌 사람은 **쓰러진 사람과 같은 규칙** — 자동에선 빠지고 `FinishTarget`으로 지목했을 때만 통과한다(처형하는 길). 업힌 사람은 **지목해도 못 친다**(어깨 위라 사거리가 업은 폰과 같아 노린 것이 짐인지 사람인지 구별이 안 된다). 다만 **직접 명령은 BT를 안 거치므로** `PawnMover.SetMoveTarget`에서도 막는다(기절과 같은 자리) — 안 막으면 갇힌 우리 폰이 우클릭으로 걸어 나간다. `MoveController.Rouse`도 갇힌 사람은 안 건드린다(침대에서만 놓아 주면 `IsPrisoner`가 남아 **자리 없이 갇힌 폰**이 된다). `Bed.ReleaseFrom`이 죄수 침대에서 놓아 줄 때 갇힘도 함께 푸는 것이 그 안전망이다. 업힌 쪽을 때리면 업은 폰을 노리는 것과 구별이 안 되고(어깨 위라 사거리가 같다), 갇힌 쪽을 때리면 붙잡아 온 뜻이 사라진다. **깨어나도 BT는 안 돈다** — 돌리면 눈뜨는 순간 걸어 나간다. **자세도 안 되돌린다** — `BodyPartSystem`이 기절에서 깨울 때 `_preKoRotation`으로 세우는데, 업힌 사람에게 그대로 걸면 **어깨 위에 벌떡 선다**(업기는 90도 눕힌 자세를 부모 기준으로 잡아 두고, 내려놓기 전까지 다시 눕혀 줄 사람이 없다). 침대·우리와 같은 자리의 판단이다 — *깨어나는 것과 일어서는 것은 다른 일이다.*

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
- **설득하는 쪽은 `StatName.Social`(사교)로 잰다** — 성공률(`PersuadeChance` 40~90%)과 말 붙이는 시간(`PersuadeSeconds`)에 들고, 회차마다 그 스탯이 자란다. 전엔 인지력을 빌려 썼는데 그러면 **학자가 곧 간수가 된다**(연구를 시키려고 올린 스탯이 설득까지 잘하게 하고, 반대도 마찬가지). 작업 카테고리마다 노동 스탯이 하나씩 있는데(치료=의료, 연구=연구, 채굴·벌목·요리…) **간수만 없었다**. **`StatName`은 정수로 직렬화되므로 반드시 뒤에 붙일 것**(이름만 바꾸는 것은 자리를 안 옮기므로 안전 — 에셋에 문자열로 박힌 곳이 없음을 전수 확인했다)
  - **이름이 `Warden`이 아니라 `Social`인 것은 흥정이 같은 능력이기 때문이다**(2026-08-14 (17)). 포로를 설득하는 것과 상인과 값을 다투는 것은 둘 다 **말로 상대를 움직이는 일**이라, 따로 두면 "설득은 잘하는데 흥정은 못한다"를 설명할 길이 없다. 인지력↔간수를 가른 것과는 다른 경우다(연구와 설득은 남남이지만 설득과 흥정은 같은 재주다 — 림월드의 Social이 포로 회유와 거래가를 함께 정하는 것과 같은 자리). **작업 이름 `JobCategory.Warden`은 그대로다** — 치료 작업이 의료 스탯을 쓰듯, 작업 이름과 그 작업이 쓰는 스탯 이름은 같을 이유가 없다.
- **한 스탯이 두 가지를 정하지 않는다** — 사교는 "먹히나"만, 포로 인지력은 "얼마나 오르나"만. 예전엔 설득자 인지력이 둘 다 정해서 **같은 값이 두 번 곱해져** 차이가 다섯 배 넘게 벌어졌다. 실측 범위 **1.0일**(사교 130이 인지 0을 설득) ~ **4.9일**(그 반대), 중간이 **2.2일**
- 합류할 때 `Imprisonment.Release`를 **안 쓴다** — 그건 적이면 도망 성향을 씌운다. 나가는 게 아니라 들어오는 것이다

**`UnitBrain.ChangeFaction`이 세력을 바꾸는 유일한 길이다.** `IsLocked`·BT·`WorkSettings`가 전부 `Awake`에서 정해지므로, `Faction.SetData`만 부르면 **적 트리를 그대로 든 아군**이 된다.

**`DungeonCore.CurrentMinionCount`는 `Faction.All`을 훑어 셀 때마다 다시 센다** — 이벤트로 더하고 빼면 **그 문을 안 지나는 변화**를 놓친다(회유는 스폰된 적이 없어 안 세어지고, 원정 나간 하수인은 죽지 않아 남는다 — 후자는 이미 새고 있었다).

### 간수 (`JobCategory.Warden`) — 붙잡은 사람을 자리에 넣고, 말을 붙인다

간수가 하는 일은 셋: **설득 회차**(`PersuadeJob`, 위) + **붙잡은 사람을 자리로 데려가기**(`PrisonEscort`) + **먹이기**(`FeedJob`, 아래).

- **데려가기는 일감이 아니라 업은 폰이 한다.** 일감으로 만들면 "누가 업을 것인가"부터 정해야 하는데 업는 것은 이미 명령이고(`[업는다]`), 업은 다음 다른 폰이 넘겨받으려면 어깨에서 어깨로 옮기는 동작이 필요하다. 간수 설정이 정하는 건 **그 걸음을 걸을까**다 — **켜져 있으면** 붙잡은 사람을 업는 순간 자리로 데려가고(전투가 끝나면 저절로 정리된다), **꺼져 있으면 계속 업고만 있는다**
- **`EscortDest`는 자리가 아니라 종류다**(`None`/`Cage`/`Bed`/**`Grave`**/**`Pyre`**, `UnitBlackboard.EscortTo`). `PutDownSite`와 **다른 축** — 저쪽은 우리·침대를 우클릭해 고른 **그 한 자리**고 이쪽은 "우리에 넣어라"다. 종류로 두면 걸어가는 사이 그 칸이 차도 다음 칸으로 갈아타고, 여럿을 동시에 나를 때 서로 같은 칸을 노리지 않는다. `CarryPawnNode`가 **매 틱 `PrisonEscort.Resolve`로 다시 정한다**. **시체의 장례도 같은 축을 탄다**(우클릭 `[묻는다]`·`[태운다]`) — 붙잡은 사람을 우리로 데려가는 것과 시신을 무덤으로 옮기는 것은 *업고 걸어가 자리에 넣는* 똑같은 동작이라, 규칙을 새로 만들면 "걸어가는 사이 자리가 차면"·"자리가 없으면 업은 채로 기다린다"·"도착해서 다시 본다"를 전부 두 벌로 갖게 된다. 갈리는 것은 **누가 갈 수 있는 자리인가**뿐이고 그건 `ICarrySite.Accepts`가 이미 답한다(가둘 자리는 붙잡은 사람만, 장례 자리는 시체만 — `CanOrder`도 같은 답을 쓴다)
- **자동 판단은 다쳤으면 죄수 침대, 아니면 우리** — 우리는 돌려세우는 자리고 침대는 살려 두는 자리라, 피 흘리는 사람을 우리에 세워 두면 회유가 시작되기 전에 죽는다. 원하는 쪽이 없으면 있는 쪽으로 간다(길에 눕혀 두는 것보다 늘 낫다)
- **우리 폰은 `IsCaptive`가 아니다 — 갇혀 있어도.** 안 그러면 동료를 침대로 옮기다 우리에 처넣고, [끌어낸다]로 꺼낸 우리 폰을 간수가 도로 집어넣는다. 우리 편을 가두는 건 늘 손으로 시키는 일이다
- **`EscortTo`는 업기를 거쳐도 살아남는다**(`[우리로 옮긴다]`는 업기와 데려가기가 한 동작). 그래서 `PawnCarry.TryPickUp`이 지우는 `PutDown*`과 달리 여기서 안 지운다 — 대신 **`PawnCarry.PutDown`이 지운다**(업기가 끝나는 유일한 문)
- **명령의 단일 출처는 `PrisonEscort.CanOrder`/`Order`** — 문이 둘이다(우클릭 `[우리로 옮긴다]`·`[죄수 침대로 옮긴다]` / `InspectPanel` 버튼). 각자 적으면 한쪽만 "이미 업고 있으면 그 폰에게 시킨다"를 빠뜨린다. **빈 자리가 없으면 선택지를 안 띄운다**(눌러도 아무 일이 안 일어나면 명령이 먹혔는지 알 길이 없다)
- **손으로 시킨 것은 간수 설정을 안 본다** — 자원 노드 직접 배정·`[치료한다]`와 같은 규칙
- **`InspectPanel`이 폰에게도 동작 줄을 낸다** — 이 둘뿐이다. 쓰러진 적을 좌클릭해 살펴본 다음(무엇을 지녔나, 얼마나 다쳤나) 바로 정하는 흐름이 실제로 있어서다

**필드 몸값 — 붙잡은 사람을 그 세력에 넘기고 돈을 받는다**(`WorldRansom.CanSell`/`PayoutFor`/`Sell`, 우클릭 `[몸값을 받고 넘긴다 (1,350 G)]`). **월드맵 몸값과 같은 파일에 둔다** — 방향만 반대인 같은 값이라, 두 벌이면 "되찾는 건 비싼데 넘기는 건 더 비싸다"가 조용히 생긴다. 하는 일은 **`Imprisonment.Release`에 돈이 붙는 것**뿐이다(자리 비우기·갇힘 풀기·탈출 게이지·도주 성향·맵 이탈이 전부 거기 있다). **넘기는 값이 되찾는 값보다 훨씬 싸다**(×0.3 ↔ ×1.4) — 흥정은 아쉬운 쪽이 손해 보고, 내 일꾼은 대신할 것이 없지만 저쪽에게 졸개 하나는 얼마든지 다시 뽑는다. 그래서 **셋을 팔아야 하나를 고용한다**(1,350 ↔ 4,500 G): 포로 장사가 벌이는 되지만 최적 전략은 아니고, 회유(이틀 + 탈출 위험 + 간수 비용으로 사람 하나)와 **고르는 일**이 된다. 등급·장비가 값에 실리므로 **두목은 산 채로 잡을 값이 있다**. **말이 통해야 한다**(`AcceptsRansom`) — 고블린·야수·망자·교단은 안 된다. 그때도 **줄은 남기되 못 누르는 색으로 두고 이유는 올려 봤을 때 뜬다**(`PawnMenuOption.Blocked`): 빼 버리면 "이 세력은 원래 몸값이라는 게 없나"와 구별이 안 되고, 창에 사유를 적으면 커서 옆 작은 팝업이 부푼다. **액수도 사유도 우클릭이 든다** — 넘길지 말지는 그 줄을 누를지의 문제라 **고르는 자리에 붙어 있어야** 하고, 살펴보기 창에 적으면 창 둘을 띄워 놓고 눈을 옮기게 된다.

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

**무엇을 먹는지는 `Feeding` 하나가 답한다** — 먹는 길이 둘이라(제 발로 `EatNode` / 남이 먹여 주는 `FeedJob`) 양쪽에 적으면 "포로만 다른 것을 먹는" 꼴이 된다. `Simulates` · `IsHungry` · `BestInBackpack` · `BestInStorage` · `ConsumeFromBackpack` · `Eat`(임계는 `Needs.Threshold` 40).

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

**지니는 것은 다섯째 축이다 — `LoadoutData`**(SO, `5_Data/Loadout/`, `LootEntry.cs`/`LoadoutData.cs`). 장비·소지품·동전 **전부** 여기 있고, **`UnitData`에는 아이템이 없다**.

**종족에 두면 틀린다**: `Human` 하나를 산적·마을 사람·경비·상인이 같이 쓰는데 종족에 적으면 **인간이면 누구나 도끼를 차고 나온다**. 같은 `Goblin`을 쓰는 `Enemy_Goblin`(야생 도적)과 `Minion_Goblin`(내 하수인)도 가를 수 없다. **조합은 프리팹이 한다** — `UnitBrain.loadout`(비우면 아무것도 안 나온다).

**둘 다 태어날 때 정해진다 — 갈리는 것은 *어디에 놓이는가*뿐이다**(2026-08-16):
- **`Loot`(주머니) — 태어날 때** 굴려 **배낭에 넣는다**(`UnitBrain.FillPockets`, 동전도 같이). **죽을 때 굴리지 않는다** — 그러면 살아 있는 상대의 배낭이 늘 비어 *무엇을 지녔는지 보고 가둘지·몸값을 부를지 정한다*가 성립하지 않는다. `Chance` → `MinCount~MaxCount` 순으로 굴린다(개수 0을 확률로 쓰면 "10% 확률로 3~5개"를 적을 수 없다).
- **주머니에는 "평소 지니고 다닐 만한 것"만 적는다** — **몸에서 나오는 것은 종족이 든다**(`UnitData.ButcherYield`/`BoneYield`, **도축해야 나온다**). 짐승은 주머니가 비어야 한다(짐승이 약초를 지니고 있으면 안 된다). **시점이 그 둘을 가르는 축이다**: 지니던 것은 죽으면 그 자리에 남고, 몸은 갈라야 나온다.
- **죽는다고 물건이 떨어지지 않는다.** 주머니가 이미 배낭에 있으므로 시체 배낭이 그대로 이어받는다 — 꺼내는 것은 `Corpse.SpillBelongings` 하나뿐이다(도축·화장이 부른다). 덤으로 *"상판에서 죽이면 동전이 도마 위에 쌓인다"*가 **떨구는 시점 자체가 없어져** 사라졌다.
- **`Loadout`(장비) — 태어날 때** 입힌다(`ApplyLoadout`, `Start`에서 `OnUnitSpawned` **앞** — 구독자가 장비를 읽는다). 무기가 공격을, 갑옷이 피해 감소를 바꾸므로 살아 있는 동안 실제로 일한다 — 죽을 때 만들면 맨몸으로 싸운 적이 철갑옷을 떨군다. **배낭에 넣고 `Equipment.Equip*`으로 찬다**(바로 꽂는 길은 없고 있으면 안 된다 — 벗을 때 돌아갈 자리가 없는 장비가 생긴다). 못 찬 것은 배낭에 남아 그대로 전리품이 된다(양손 무기를 든 채 굴린 방패 등). **같은 부위를 여럿 적으면 나중 것이 이긴다**(밀려난 것도 전리품).
- **`CoinDropMin/Max`(동전)** — 등급에서 자동 산출하지 않는다. 그러면 강한 늑대가 금화를 물게 되고 저작으로 막을 길이 없다. `Loadout_GoblinRaider` 5~30. **주머니와 같이 태어날 때 들어간다** — 무게가 0이라 지고 다녀도 느려지지 않고, 살아 있는 적을 뒤지면 그 자리에 보인다.
- **`Item`(에셋) ∥ `ItemId`(문자열) + `Resolve()`** — `MerchantPoolEntry`와 같은 규약. **장비는 대부분 JSON이라 `ItemId`를 쓰게 된다**(2026-08-02 이관분은 에셋 참조로 못 가리킨다). `Item`을 직접 읽으면 JSON 아이템이 조용히 null이 된다. **`ItemId` 오타는 조용히 아무것도 안 나오는 것으로 나타난다.**
- `Equipment`는 `Bind()`로 **필요할 때 스스로 묶는다** — `Start`에만 두면 `ApplyLoadout`이 먼저 돌 때 `_inventory`가 null이라 장착이 **에러도 없이** 실패한다.
- **주머니가 배낭에 안 들어가면 콘솔에 적는다**(`Stow`) — 태어나는 자리는 대개 맵 가장자리라 **떨구면 아무도 안 가는 곳에 쌓인다**. `TryAdd`의 반환값을 안 보면 조용히 증발해서, 굴림표를 아무리 저작해도 "가끔 안 나온다"로만 보이고 원인이 화면에 안 남는다. 못 넣는 것은 대개 **장비 잔량이 격자를 다 먹은** 저작 실수다 — **주머니는 장비 다음에 채운다**(`ApplyLoadout`이 배낭을 거쳐 장비를 차므로, 먼저 채우면 그 왕복에 격자를 빼앗아 무기를 못 찬다).
- 새로 만들 때는 `Create → DungeonColony → LoadoutData`. **`ItemId` 오타는 조용히 아무것도 안 나오는 것으로 나타나므로**(`Resolve()`가 null이면 그 항목은 건너뛰어진다) 손으로 칠 때는 그 에셋을 본으로 삼을 것.

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
- **`Gold`: 상인이 가진 돈 — 이만큼까지만 사 준다**(기본 800, **0이면 무한**). 없을 때는 `TradeAccount.GiveGold`가 늘 받아서 **상인 지갑이 무한**이었고, 그러면 재고를 다 사고도 무제한으로 팔 수 있어 지역 시세로 만든 교역이 *"제일 비싸게 쳐주는 한 곳에 전부 붓기"*로 무너진다 — 어디에 파느냐가 선택이 되려면 **한 사람이 받아 줄 수 있는 양에 끝이 있어야** 한다. 저작: 재료 상인 900~1,400 / 무기·방어구 1,000 / 식료 500(아이템 값 중앙값 94G, 비싼 장비 1,000G대 기준)
- `CalcPrice(entry)` — 구매가 계산: `BasePrice × GetItemMultiplier(item)`
- `CalcSellPrice(item, quality)` — 판매가 계산: `BaseValue × quality배율 × GetItemMultiplier(item) × SellMultiplier × **ItemData.ResaleFactor**`. **살 때와 팔 때 같은 배율을 쓴다** — 구매가에만 붙으면 생선을 2배로 치는 마을도 매입은 남들과 같아 싸게 사서 비싸게 파는 교역이 성립하지 않는다

**`Shop`** (`Assets/1_Script/Event/Shop.cs`) — **상점 하나. MonoBehaviour가 아니다.** 재고(`Stock`)와 거래(`TryBuy`/`TrySell`/`GetSellPrice`/`Refresh`)를 든다. 필드맵 상인은 `Merchant`가, 월드맵 마을은 `TownMarket`이 이걸 든다 — 지도 위의 마을은 "칸"이지 씬에 있는 물건이 아니라서 폰을 세울 수 없다.

**`TradeAccount`** (`Assets/1_Script/Event/TradeAccount.cs`) — **거래의 "우리 쪽": 돈이 어디 있고 산 물건은 어디로 가는가.** 상인이 아는 것은 "무엇을 얼마에 파는가"뿐이고, 지갑이 창고냐 원정대냐는 **누가 사러 왔느냐**의 문제다. `GoldAmount` / `TakeGold` / `GiveGold` / `Receive`.
- **돈은 전액 아니면 0**(절반만 빼면 되돌릴 방법이 없다), **물건은 부분 입고 인정**(받은 개수를 돌려주면 거기까지만 값을 치른다).
- `StorageAccount` — 거점 창고. `Merchant.Account`가 이걸 준다.
- `PartyAccount` — 원정대. **멤버 배낭이 곧 지갑이고 창고다.** 등짐(`Cargo`)은 안 쓴다 — 등급을 몰라서 희귀 물건이 조용히 일반이 된다. **잠근 뭉치도 안 쓴다**(여비를 잠가 두면 그만큼은 안 나간다).
- **사는 순서는 돈 먼저.** 계정에 되돌리는 길이 있으므로, "물건을 넣은 뒤 값을 못 치르는"(= 되돌릴 곳을 모르는) 경우가 아예 안 생긴다.

**Merchant 컴포넌트** (`Assets/1_Script/Event/Merchant.cs`) — **폰에 이걸 붙이면 상인이 된다.** `Shop` / `Account`(창고 지갑) / `RefreshStock()`(매일 자정) / `OnWorldClick`. 어느 종족 프리팹이든 `Merchant` + `MerchantData`를 얹으면 그 물건을 파는 상인이다. **역할(`NpcData`)은 행동만 정한다**(자리를 지킨다 / 들렀다 떠난다) — 무엇을 파는지를 역할에만 적을 수 있으면 종류마다 역할 에셋이 필요해진다(역할 × 상품의 곱셈).

**쓰러진 상인과는 거래하지 않는다** — `Merchant.CanTrade`(기절·시체. `BecomeCorpse`는 `Merchant`를 떼지 않는다). 여는 것만 막으면 창을 띄워 둔 채 눕히면 되므로 `MerchantUI`가 매 프레임 다시 본다. **거절은 알림으로 띄우지 않는다** — `IClickGate.CanClick`으로 **클릭을 사양**하면 `SelectionManager`가 다음 분기(폰 정보 패널)로 흘려보낸다. 이벤트 알림 패널은 사건을 보는 자리라 조작 실패가 섞이면 안 되고, 쓰러진 것은 눈에 보이므로 설명이 필요 없다.

**`Shop`은 상인마다 하나다.** 같은 `MerchantData`를 쓰는 상인이 둘이어도 **재고와 주머니가 따로 돈다** — `MerchantData`는 "무엇을 팔 수 있는가"(후보 목록·가격표)이고, 오늘 진열대에 무엇이 몇 개 올랐는지와 **주머니에 얼마가 남았는지는 그 상인의 것**이다. 한쪽에 다 팔았다고 다른 쪽이 빈털터리가 되면 안 된다. 일일 갱신도 각자 돈다. 창 제목은 `이름 (무기 상인)  850 G` — 종류 이름만 쓰면 무기 상인이 둘일 때 어느 쪽 창인지 알 수 없고, **주머니를 안 적으면 한창 팔다 갑자기 안 팔리는 이유가 화면에 없다**(`MerchantUI.RefreshTitle` — 제목을 짓는 곳은 여기 하나다. 흥정하는 사람이 바뀔 때와 돈이 오갈 때 둘 다 다시 그려야 해서, 각자 적으면 한쪽이 주머니를 빠뜨려 거래 한 번에 액수가 사라진다).

**상인 돈은 `Shop.Gold`가 든다.** 팔면 줄고(`TrySell`), **사면 는다**(`TryBuy` — 대칭이라 규칙을 따로 설명할 것이 없고, 덤으로 *"빈털터리 상인에게 팔고 싶으면 그에게서 뭔가 사면 된다"*가 저절로 나온다). 하루가 지나면 **통째로 되돌린다**(`Refresh` — 남은 돈을 이월하면 며칠 안 들르는 것만으로 무한 지갑이 된다). 판매는 **개당으로 재서 바닥나면 거기서 멈춘다** — "50개 중 12개까지만 사 줬다"가 저절로 나온다(한꺼번에 계산하면 부족할 때 통째로 거절하게 되고, 그러면 얼마까지 되는지를 손으로 세어 봐야 한다). 못 파는 이유는 배낭 창 판매 줄에 뜬다(`상인의 돈이 모자란다 (120 G)`) — **값이 0인 물건(`판매 불가`)과 구별한다**: 내일 다시 오는 것과 원래 안 팔리는 것은 할 일이 다르다.

**`TownRecruits`** — 마을에서 **사람을 구한다**. `At(node)`. **마을엔 늘 최소 한 명이 선다**(바이옴 풀이 비면 빌려 쓰고 경고). 인원(1~3)은 후보 종류 수와 무관 — 사람을 가르는 건 종족이 아니라 이름이라 같은 종족을 여럿 세우되 이름이 안 겹치게 고른다. 후보는 `BiomeContentData.TownRecruits`에서 **칸 좌표 + 시드 + 날짜**로 결정적(여닫는다고 새로 안 뽑힌다 — 그러면 고르는 일이 아니라 돌리는 일이 된다). 후보마다 `PawnSpec`을 들고 서고(이름·등급·몸집·값이 미리 정해진다) 등급 범위는 `RecruitEntry`가 종족 기본을 덮는다. **이름을 미리 정한다**(`UnitBrain.Rename`) — 안 그러면 `Awake`가 새로 굴려 메뉴에서 본 사람과 합류한 사람이 달라진다. 고용 = `PartyAccount`로 값을 치르고 프리팹을 **꺼진 채로** 찍어 `party.Members`에 넣는다(거점 진입 시 `EnterSettlement`가 켠다).

**`TownMarket`** (`Assets/1_Script/World/TownMarket.cs`) — 월드맵 `Town` 칸의 시장. `ShopsAt(node)` / `GoldAt(node)`. **종류당 하나씩** 선다(재료·식료·무기·방어구). **종류는 바이옴 풀에 있는 것이 전부 뜬다** — 좌표가 고르는 건 "같은 종류에 후보가 여럿일 때 어느 쪽인가"뿐이다. 종류까지 추첨하면 무기 상인을 만나려고 마을을 순회하게 되고, 그건 고르는 일이 아니라 찾아다니는 일이 된다. 어느 상인이 서는지는 `BiomeContentData.TownMerchants`에서 **칸 좌표 + 시드 해시로 결정적**(`WorldDeposits`와 같은 패턴 — 배치는 저작물, 세부만 굴린다). 재고는 **하루 단위 캐시**(여닫으며 재고를 새로 뽑는 것을 막는다), 상인 자체는 안 바뀐다. 지도를 새로 지으면 `WorldMap.Generate`가 `Clear()`.

**상인 하나 만드는 법** — 셋 중 아무 길이나. 어느 쪽이든 `Merchant`가 붙고 매일 `RefreshStock()`이 돈다. **우선순위는 저작 > 주입 > 기본값.**
1. **프리팹에 직접** — 종족 프리팹에 `Merchant`를 붙이고 `MerchantData`를 지정. **이게 `HasAuthoredData`**(인스펙터에서 물린 것만 참, `Awake`가 잡는다)이고, 이벤트·스폰이 **덮지 않는다** — "이 사람은 늘 이것만 판다"가 성립해야 고정 상인을 프리팹 하나로 세울 수 있다.
2. **스폰이 정해 준다** — `merchant.Data = ...`(setter가 그 상인만의 재고를 새로 뽑는다). 이벤트·마을이 이 길.
3. **역할 기본값** — `NpcData.MerchantData`. 위 둘이 다 없을 때만.

**종족별 상품은 두 축이 짝을 이룬다 — `Races`(누가 취급하나) + `ExtraPools`(무엇을 얹나).**

- **`MerchantData.Races`**(비우면 누구나) — 스폰이 종족을 먼저 뽑고 **그 종족이 취급하는 상품만** 후보로 삼는다(하나도 없으면 역할 기본값으로 떨어진다). 종족 풀 × 상품 풀을 그냥 곱하면 인간이 고블린제 물건을 들고 오고, 조합마다 프리팹을 만들면 종족 × 종류의 곱셈이 된다. **조건은 상품 쪽이 든다** — 어느 종족이 만드는 물건인가는 그 물건의 성질이지 파는 사람의 스탯·이름 풀의 성질이 아니다.
- **`MerchantData.ExtraPools`** — 상품 묶음을 **겹쳐 쓴다**. 고블린제 방어구 상인을 만든다면 자기 목록에는 고블린제만 적고 `Merchant_Armor`를 통째로 얹으면 된다. 이게 없으면 종족별 에셋마다 공용 목록을 복붙해야 하고, 기본 방어구 하나를 고칠 때 종족 수만큼 고쳐야 한다 — 반드시 어긋난다. 진열은 `Data.CollectPool()`(자기 것 + 얹은 것, 중복·순환 방지)에서 뽑고, **값·매입 배율·진열 칸 수는 파는 상인의 것**을 쓴다(같은 물건도 누가 파느냐로 값이 달라지는 지역 시세와 같은 규칙).

월드맵 마을(`TownMarket`)은 파는 폰이 없으므로 `Races`를 안 본다(`ExtraPools`는 그대로 작동).

**이벤트 상인** (`NpcType.EventMerchant`): 걸어 들어와 `NpcData.StayDuration` 후 돌아가 소멸. **장사는 상주 상인과 똑같이 한다** — 2026-08-07까지 `Merchant`를 아예 안 붙여서 클릭해도 아무것도 안 열렸다. **종족과 상품을 따로 굴린다** — `GameEventData.MerchantPrefabs`(누가 오는가) × `MerchantVariants`(무엇을 파는가)를 스폰 때 조합한다. 둘을 프리팹에 묶으면 종족 × 종류만큼 프리팹이 필요해지고, 종족을 하나 늘리는 값이 종류 수만큼 곱해진다.

**UI 조작 세부는 `docs/ui.md` "상점·인벤토리 조작"으로 내렸다** — 여기 남기는 것은 **밖에서 어기기 쉬운 규칙**뿐이다.

- **격자 조작은 "한 번 눌러 집고, 한 번 눌러 놓는다"**(`DraggableItem`, 드래그 아님). **좌클릭**=집기(Shift 절반 / Ctrl 하나) ↔ 놓기(발자국에 걸리는 것이 하나면 그것이 손에 들린다) · **우클릭**=이걸 저기로(반대편 통·착용·먹기·사고팔기·동전 회수) ↔ 되돌리기 · **Q/E**=회전. **더블클릭은 없다.**
- **놓을 곳은 장비칸을 먼저 묻는다**(장비칸 → 격자 → 판매 구역 → 바닥) — 배낭 격자의 `RectTransform`이 장비 열까지 덮어서, 격자를 먼저 물으면 **착용이 아예 안 된다.**
- **손에 든 것은 통에서 빠져 있지 않다**(격자가 안 그릴 뿐) — 그래야 놓기에 실패해도 제자리에 있다. 대신 **받아 간 쪽이 덜어야** 같은 물건이 둘이 안 된다.
- **좌판은 진짜 격자이고 자리가 곧 판매 한도다**(`Shop.Stall`). **한도는 `Shop`이 든다 — 화면이 아니다**(창을 안 열고 파는 길이 생기면 규칙이 샌다).
- **자리 판정은 `CanFit`이 아니라 `TryAdd`의 결과다** — `CanFit`은 빈 칸을 넓이로만 세어 조각남을 못 보고, 그 말을 믿고 값을 치르면 **판 물건이 세상에서 사라진다.**
- **되사는 값은 판 값 그대로 얼어붙는다**(`Reprice`가 `FromPlayer`를 건너뛴다) — 안 그러면 왕복으로 **돈이 찍힌다.**
- **`MerchantUI`는 모드 무관이다** — 거래는 거점 상인에게도 하고 월드맵 마을에서도 한다(`GameModeManager`가 필드 UI 스태시에서 뺀다). 모드가 바뀌면 닫는다.

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
- `Raid`: `EnemyPrefabs[]`, `EnemyCount` → 맵 엣지에서 던전 코어 방향으로 이동
- `Merchant`: **`MerchantPrefabs`**(종족) × **`MerchantVariants`**(상품, 비우면 프리팹이 들고 온 것) → 맵 엣지에 NPC 스폰 (EventMerchant BT로 동작). 각각 하나씩 뽑아 조합
- `Reinforcement`: `ReinforcementPrefabs[]`, `ReinforcementCount` → **걸어 들어오는 사람들**. 이름은 지원군이지만 하는 일은 "가장자리에 세우고 핵 쪽으로 걷게 한다"뿐이고 **세력은 안 건드린다** — 그래서 합류하는 방랑자(`Faction_Player`)도, 곁에서 싸우는 용병(`Faction_Mercenary`)도, 지나가는 떠돌이(`Faction_Freefolk`)도 **같은 문으로 온다**. 무엇이 되는지는 프리팹의 세력이 정한다
- `Discovery`: `DiscoveryItems`(비우면 레지스트리 전체, **화폐 제외**), `DiscoveryMinAmount/MaxAmount` → **거점 둘레 바닥에 떨군다**(`discoveryDropRadius`). 예전엔 자원 노드를 세웠는데 노드는 영구히 남아 거점 둘레가 광맥 밭이 됐고(**발견은 사건이지 지형 변화가 아니다**), 그다음엔 창고에 바로 넣었는데 그건 **사건이 아니라 숫자가 조용히 오르는 일**이라 알림을 놓치면 무슨 일이 있었는지도 모른다. 떨어진 것은 우클릭 [창고로 옮긴다]로 켜야 실려 간다(2026-08-13 — 바닥 물건은 전부 기본 꺼짐). 알림이 방향까지 적으므로 어디로 가야 할지는 알 수 있다

**스폰 자리는 지형이 아니라 맵이 정한다**(`TryFindEdgeSpawn`) — **핵에서 바깥으로 한 칸씩 걸어 나가** 설 수 있는 마지막 칸을 찾고 `spawnEdgeInset`만큼 **안쪽으로 물러선다**. 걷다가 설 수 없는 칸에서 멈추므로 **핵과 이어져 있는 것이 방식 자체로 보장된다**(좌표를 굴려 검사만 하면 바닥 밖 섬을 못 거른다). 낭떠러지 바로 위에 무리를 세우면 서로 비켜서다 밀려 떨어진다(`ExpeditionFormation.approachInset`과 같은 교훈). 흩뿌리기(`ScatterNear`)는 **이미 쓴 칸**과 **이웃이 공허인 칸**을 피한다. **자리도 프리팹도 없으면 알림 없이 건너뛴다** — 알림이 먼저 나가면 "몰려온다"고 해 놓고 아무도 안 온다. **침공대는 소환 직후 핵으로 `SetMoveTarget`** — 적 BT는 탐지·배회(12m)뿐이라 명령이 없으면 가장자리에서 서성이다 끝난다.

**EventNotificationUI**: `EventManager.OnEventTriggered` 구독 → 이벤트 발동 시 화면 상단 팝업 자동 표시. `displayDuration`(기본 5초) 후 자동 닫힘. **제목도 설명도 비면 안 띄운다** — 배경만 남아 화면에 작은 검은 상자가 뜬다(실제로 빈 이벤트 에셋 하나 때문에 그것만 보였다, 2026-08-06). 대신 무엇이 비었는지 콘솔에 남긴다. **스폰형 이벤트는 8방위를 덧붙인다**(`북동쪽에 나타났습니다`) — 어느 쪽을 막을지가 곧 대응이다. 꺼져 있는 동안(월드맵) 들어온 알림은 **데이터든 완성된 문장이든 들고 기다렸다** 돌아올 때 띄우고, 둘 다 밀렸으면 문장이 이긴다(방향이 붙어 더 자세하다).

**빈도는 `quietWeight`가 정한다 — "아무 일도 없음"도 후보다**(2026-08-15). 전엔 `TryRollEvent`가 **반드시 하나를 발동시켜** 굴릴 때마다 무슨 일이 났다. 그러면 주기가 곧 빈도라 조용한 날을 만들려면 주기를 늘리는 수밖에 없고, 그건 **사건이 언제 올지가 메트로놈처럼 예측 가능해진다**는 뜻이다. 가중치로 두면 빈도를 주기와 따로 조절할 수 있고 `MinGameTime` 게이팅과 저절로 맞물린다.

> 셈: 하루 굴림 수 = `1800 / checkInterval` · 사건 확률 = `후보합 / (후보합 + quietWeight)`.
> 지금 값(주기 300 · quiet 26)이면 **하루 1.6건(초반) → 2.2건(전부 열린 뒤)**.

**`checkInterval`을 시험용으로 줄여 두지 말 것** — 씬에 `10`이 박혀 있었다(하루 **180번**). 코드 기본값(300)은 씬이 값을 들고 있으면 아무 힘이 없고, 증상은 "사건이 미친 듯이 터진다"로만 나타나 원인을 짚기 어렵다. `Awake`가 60초 미만이면 경고한다.

**`MinGameTime`은 `GameTimeManager`와 같은 축**(하루 = 1800초)이고 **난이도 곡선을 여기서 만든다**. 2026-08-07엔 반대로 내렸었다(습격 120·합류 600) — 크게 잡으면 초반 후보가 비어 다른 이벤트만 반복됐기 때문인데, **그건 "아무 일도 없음"이 없어서 생긴 증상**이었다. 조용한 날이 생긴 지금은 늦게 여는 것이 곧 **조용한 초반**이 된다.

**이벤트 에셋**(`5_Data/Event/`, 10종). 씬의 `EventManager.eventPool`에 배선돼 있다. **가중치는 흔한 것부터, 시작 시점은 약한 것부터** — 습격이 순서대로 열리는 것이 곧 난이도 곡선이다.

| 에셋 | 종류 | W | **밤** | 열림 | 규모 |
|---|---|---|---|---|---|
| `Ev_Discovery` | 발견 | 3.0 | 1.0 | 0.2일 | 4~15개 |
| `Ev_Peddler` | 상인 | 2.5 | **0.2** | 0.3일 | — |
| `Ev_Drifter_Passes` | 지원 | 2.0 | 0.6 | 0.3일 | 1 |
| `Ev_Beasts_Attack` | 습격 | 1.6 | **2.5** | **0.6일** | 3 |
| `Ev_Raid_Bandits` | 습격 | 1.4 | **2.0** | **1.5일** | 4 |
| `Ev_Mercenaries_Arrive` | 지원 | 1.0 | 0.6 | 1.0일 | 3 (구원 40%/25초) |
| `Ev_Raid_Goblins` | 습격 | 1.2 | **2.2** | **3일** | 5 |
| `Ev_Wanderer_Joins` | 지원 | 0.8 | 0.6 | 1.5일 | 1 |
| `Ev_Raid_Undead` | 습격 | 0.8 | **3.0** | **5일** | 6 |
| `Ev_Raid_Cult` | 습격 | 0.6 | **3.0** | **7일** | 5 |

실측 곡선(주기 300 · quiet 26): **0.6일에 첫 습격이 열려 3.7일 간격 → 1.5일 2.1일 → 3일 1.6일 → 7일 이후 1.2일**. **월드맵 예고 습격(3일 주기)이 여기에 더 얹힌다** — 둘은 다른 축이라 합쳐서 봐야 한다.

**밤은 총량이 아니라 구성을 가른다**(정규화 — 위 "밤" 절). 하루 여섯 굴림 중 **둘이 어두운 때**에 든다:

| | 사건이 날 확률 | 그중 습격 |
|---|---|---|
| 낮 | 36.4% | 37.6% |
| 밤 | **36.4%**(같다) | **70.2%** |

전부 열린 뒤 **하루 습격 0.82 → 1.06건(+29%)** 이고 그중 **48%가 밤에** 온다(전 33%). 사건 수는
그대로인데 습격이 늘어난 만큼 **행상인이 밀려난다**(0.37 → 0.26건/일) — 밤의 값을 *다른 것을 못
만나는 것*으로 치르는 자리다. **손잡이는 습격 에셋의 `NightWeight` 하나**: 낮춰도 밤 습격의
*비중*은 남고 총량만 내려간다.

### BuildMode

- **갈래는 여덟이고 화면 순서는 `BuildCategoryExt.Order`가 정한다**(2026-08-22) — 바닥·방어·보관·제작·거주·수용·자원(광맥 포함)·기타(서브코어·무덤·화장터). **쪼개는 값은 "고를 것이 갈리는가"다** — 광맥을 자원에서 떼고 장례를 따로 두었다가 되돌렸다(광맥과 밭·나무는 둘 다 지형에 놓는 것이라 헷갈릴 일이 없고, 장례는 둘뿐이라 제 칸이 늘 비어 있다). **값은 꼬리에 붙인다**(직렬화가 정수라 중간에 끼우면 마흔한 개가 밀린다) — 대신 `Building`은 **`Living`으로 이름만 바꿔** 죽은 값을 안 남겼다. **`Order`에 안 적은 갈래는 화면에 없다**(목록의 유일한 근거). 갈래 버튼은 **씬의 첫 자식을 본떠 찍고** **빈 갈래는 접는다**. 짓는 것은 `Start`가 아니라 **처음 열 때** — `BuildModeManager`가 목록을 실은 뒤여야 빈 갈래를 가릴 수 있다. 갈래 재배정·띠 크기는 `DungeonColony → Update Build Menu`(**이름표로 매기고 자리 값은 절대값이라 몇 번 돌려도 같다** — 상대로 옮기면 두 번째 실행이 두 번 넓힌다). **UI 기준은 1920×1080 · 1:1**(`CanvasScaler`도 `ScaleWithScreenSize` 1920×1080) — **크기는 게임 뷰 배율 1x에서 잰다**(줄여 놓고 재면 실제보다 크게 만든다). **갈래는 띠에 없다 — 왼쪽 아래에 선 세로 판이다**(256×252, 두 열 × 네 줄; 타일 줄은 그 오른쪽에 가로로, **띠 배경은 없다**): 갈래는 여덟로 끝나고 지을 것은 계속 느는데, 한 줄에 두면 **갈래를 더할 때마다 띠 높이를 다시 재야 한다**(줄 수가 곧 높이였다). 빼내는 방법은 **`LayoutElement.ignoreLayout`** — 별도 오브젝트로 꺼내면 `panel.SetActive` 한 번에 같이 꺼지던 것이 깨진다. **`BuildMode_Panel`에 `HorizontalLayoutGroup`이 있어 자식의 `sizeDelta`·`anchoredPosition`은 매번 덮인다**(폭은 `LayoutElement`, 높이는 `childForceExpandHeight`) — 좌표를 쓰기 전에 그 자리에 레이아웃 그룹이 있는지부터 볼 것. **타일은 오른쪽 끝에 닿으면 윗줄로 올라간다**(`GridLayoutGroup` + `Constraint.Flexible`, `startCorner = LowerLeft` — 아래 줄부터 채워야 한 줄뿐인 갈래가 허공에 안 뜬다). **열 수는 안 적는다** — 폭에서 나온다. **타일에는 그림과 이름만** 두고 설명·크기·`목재 12/20`·잠김 사유는 **마우스를 올렸을 때**(`HoverTooltipUI`) 뜬다(92픽셀 폭에 잠금 문구를 넣으면 잘려서 못 읽는다). **커서 툴팁의 자리 계산은 `CursorTooltip.Place` 한 벌이다** — 규칙은 **밀어 넣지 말고 반대편으로 뒤집는다**(밀면 툴팁이 커서 밑으로 기어들어온다). 그 설명은 **`Func<string>`으로 넘긴다** — 구워 두면 창고가 바뀌어도 안 따라온다. `OnPointerExit`는 파괴 시 안 오므로 `OnDisable`에서 거둔다. 목록 폭은 아무 데도 안 적고(`flexibleWidth` 1이 띠 전체를 준다) **타일이 시작하는 x는 `padding.left`로 민다**. 밑선은 갈래 판을 띠의 **왼쪽 아래** 모서리에 걸고 타일 줄을 `LowerLeft`로 붙여 맞춘다. **안 보이는 배경은 레이캐스트도 끈다** — 투명한 판이 클릭을 먹으면 "여기는 왜 클릭이 안 되지"가 된다. 아래 띠는 **피벗이 (0.5, 0)**이라 `anchoredPosition.y`가 *높이의 절반*이 아니라 **바닥에서 띄우는 값**이다. 붙일 값은 부모(`BuildMode_UI`, 화면 바닥에 가운데를 둔다) 높이의 절반이고 **부모에서 읽는다**
- **건설 목록은 레지스트리에서 온다** — `Assets/Resources/Data/Buildings/`에 `BuildingData` 에셋을 두면 그것만으로 목록에 오른다(`DataManager.AllBuildings`, `Category`→`SortOrder`→이름 순). 씬의 `placeableBuildings`는 **비워 두는 게 기본**이고, 채우면 그게 이기는 큐레이션 오버라이드다(`ResearchTable.availableResearch`와 같은 관례). `UsesBlueprint=true` → 2단계 건설, `false` → 즉시 배치
- 설계 원칙: `BuildingData`는 배치 방법만 담당. 각 컴포넌트(Storage, SubCore, FarmPlot 등)가 동작 데이터를 프리팹 SerializeField로 직접 보유
- **콜라이더 크기와 `OccupiesTile`은 다른 축이고, 짝을 맞춰야 한다.** 크기 = **어디를 눌러야 잡히나**(클릭 판정 — 늘 `Size − 0.2`), `OccupiesTile` = **길을 막나**(길찾기는 타일 점유만 보고 콜라이더는 안 본다, `obstacleLayer`는 Wall뿐). 둘을 같은 것으로 보면 *2×2 창고인데 콜라이더가 1×1이라 가운데만 눌리는* 일이 난다(실제로 그랬다 — 고치는 도구가 `OccupiesTile`로 걸러 창고만 건너뛰었다). **`OccupiesTile`이 false면 콜라이더는 트리거여야 한다** — 안 그러면 지도는 "빈 칸"인데 몸이 부딪혀 길찾기가 통과 경로를 내고 폰이 처박힌다(침대·밭이 그랬다). 콜라이더는 **바닥만 빼고** `Size − 0.2`다(바닥은 서로 맞닿아야 해서 — 0.2를 깎으면 틈으로 커서 레이가 빠진다)
- **씬에 손으로 놓은 건물도 자기 칸을 막는다** — `PlacedBuilding.Start()`가 `data.Size`로 발자국을 등록한다(배치로 지은 건물은 이미 불렸으므로 건너뜀). **건물 프리팹에는 `PlacedBuilding` + `data`를 미리 붙여 둘 것** — 없으면 이 등록이 아예 안 돌고, 그러면 길찾기가 아는 건 콜라이더뿐이라 모델보다 작을 때 **모델 한복판을 가로지르는 경로**가 나온다.
- **씬 배치는 격자에 맞출 것**: **홀수 크기 축은 `.5`, 짝수 축은 `.0`**(`GetFootprint`/`FootprintCenter` 규약). 어긋나면 모델이 옆 칸으로 삐져나와 **작업 자리를 먹는다**. **정렬을 먼저, 콜라이더를 나중에** — 어긋난 채로 콜라이더를 키우면 작업 자리를 문다.
- `OnActiveChanged` — 건설 모드 토글 시 다른 UI 자동 닫힘
- **청사진은 자재를 배치 때 안 낸다** — 폰이 지고 와 납품하는 것이 유일한 지불이다(`ConstructionJob`). 전엔 `Place`의 차감이 `UsesBlueprint` 분기보다 **위**에 있어 **두 번 냈다**. 그래서 **자재가 없어도 청사진은 놓인다**(계획이다 — 자재가 들어오면 저절로 재개). `BuildModeUI`의 붉은 색은 막는 표시가 아니라 "지금은 안 지어진다"는 알림
- **철거 모드(X)가 청사진도 지운다** — 클릭 단일 / 드래그 일괄 / 같은 빨간 예정 표시. **결과만 다르다: 건물 철거는 75%, 청사진 취소는 전액**(`BuildingBlueprint.Cancel` → `MaterialPool.Refund`) — 취소는 파괴가 아니라 무효화라 깎을 손실이 없고, 깎으면 계획을 세우는 일에 벌칙이 붙는다. **픽킹은 콜라이더가 아니라 타일**(`PickAt`) — 고스트는 콜라이더를 일부러 다 껐다(통행을 안 막고, 켜면 선택·이동 명령 레이캐스트에 걸린다). 조준은 배치 미리보기와 같은 `TryCursorGround`. 픽킹용 칸 `Area`는 **`Footprint`와 다른 축**(발자국은 "완성되면 막을 칸"이라 창고·침대·농지는 비어 있다). 매달린 일감은 거두되 **배낭에 실린 자재는 안 건드린다**(짐내림이 돌려놓는다)
- **청사진 취소는 건설 모드 밖에서도 된다** — 같은 `PickAt`을 좌·우클릭에도 붙였다. **우클릭 = `[건설을 취소한다]`**(`MoveController`, 아무것도 안 걸렸을 때만 — 그 위에 폰이 서 있으면 폰이 이긴다), **좌클릭 = 폰 정보 패널에 진행 표시 + 같은 버튼**. 문이 셋이어도 전부 `bp.Cancel()` 하나를 부르므로 갈라질 데가 없다 — **어긋나는 건 판단이 여러 곳에 흩어질 때**지 문이 여럿일 때가 아니다. 패널이 답하는 건 "왜 아직 안 지어졌나" 하나라 `자재 12/20` 옆에 **가진 것**(`AvailableAnywhere`)을 같이 적는다. 조사 해제는 `SelectSingle` 맨 앞 **한 곳**에서 한다(분기마다 적으면 새 분기가 하나 빠뜨린다)
- **철거·취소 환불은 전액**(`refundRatio` 1.0). 손실을 물리면 "지어 놓고 아니다 싶으면 옮긴다"가 벌칙이 된다 — 잘못 놓은 값은 자재가 아니라 **들인 시간**으로 이미 치른다. 돌아가는 곳은 **창고 먼저**(`MaterialPool.Refund`) — 바닥에 떨구면 그 짐이 다시 운반 일감이 되고, 나르는 동안 창고 집계에서 빠져 같은 자재를 기다리던 작업이 "재료 부족"으로 죽는다
- **`InspectPanel`은 "고른 것을 보는 자리"다** — 폰 / 시체 / 자원 노드 / **바닥에 떨어진 물건**(무엇이·얼마나·얼마어치 + **자동 운반이 켜졌나**, 버튼으로 켜고 끈다. 초록 구슬이 안 보일 때 그것이 "안 찍었다"인지 "찍었는데 창고가 없다"인지 구별이 안 됐다 — 둘은 할 일이 다르다) / 청사진 / **다 지은 건물**(이름·**설명**·크기·휴면 + [이동한다]·[분해한다]) / **던전 핵**(`OnCoreInspected` — `PlacedBuilding`이 아니라 분해·환불이 없고 [이동한다]만). 저마다의 건물 패널(창고·침대·농지…)을 **대신하지 않고 곁들인다**: 그쪽은 그 건물로 무엇을 하는지, 여기는 **건물 자체**를 다룬다. **휴면 건물도 이 줄은 뜬다**(제 패널은 안 열려도 옮기고 분해할 수는 있어야 한다 — 오히려 그때 제일 필요하다). **바닥은 뺀다** — 바닥도 `PlacedBuilding`이라 세면 빈 땅을 누를 때마다 건물 창이 뜬다. 버튼 줄은 씬의 `ActionTemplate` 복제, **세로로 쌓는다**(가로면 300px 패널에서 글자가 잘린다)
- **자원 노드는 설명을 손으로 안 적는다** — 알고 싶은 것(무엇이 나오나 · 캐나 베나 · 몇 명이 붙나 · 권장 스킬)이 전부 `ResourceNodeData`에 있어 거기서 만든다(적어 두면 값을 고칠 때마다 문장이 뒤처져 **표가 아니라 거짓말이 된다**). `InspectPanelUI`가 기존 **`OnNodeSelected`를 같이 듣는다**(이벤트를 새로 만들면 둘이 어긋난다). **"배정된 폰이 없습니다"가 이 창의 값어치** — 채굴·벌목은 큐가 아니라 직접 배정이라 아무도 안 붙으면 영영 안 캐는데 그걸 말해 주는 곳이 없었다. 씬에 흩어진 나무·바위는 `PlacedBuilding`이 없어 [이동]·[분해]가 안 뜬다(건드릴 수 없는 지형)
- **`BuildingData.Description`이 "무엇을 하는 물건인가"에 답한다**(전부 채움). 크기·환불액은 이미 아는 사람에게나 쓸모가 있고, `보석 연마대`·`서브코어`는 이름이 답을 안 준다. 청사진에도 띄운다 — 아직 물건이 아니니 **무엇이 될 것인지**를 알아야 취소할지 정할 수 있다
- **건물 프리팹에 `PlacedBuilding` + `data`가 실제로 붙어 있어야 한다** — 적어만 두고 안 붙어 있었다(`LargeStorage` 하나뿐). 없으면 씬에 손으로 놓은 건물이 **발자국을 등록 안 해** 모델 한복판을 가로지르는 경로가 나오고, 좌클릭해도 **어느 건물인지 몰라** 정보 패널이 안 뜬다. **건물 프리팹을 새로 만들 때마다 손으로 붙일 것**(전수 확인 — 41종 전부 붙어 있다)
- **`BuildingRelocator`** — 다 지은 건물 이전. `CoreRelocator`와 **나란한 물건이지 일반화가 아니다**(핵은 한 칸이고 옮기면 영역이 따라 움직여 휴면/재가동 수를 미리 세어 준다). **값을 안 받는다**(환불이 전액이라 "분해 후 재건축"이 늘 더 싸므로 값을 받아 봐야 조작만 는다). **자기 발자국은 장애물로 안 센다**(안 그러면 한 칸 옆으로도 못 옮긴다). `PlacedBuilding.MoveTo` = `ReleaseTiles` → 이동 → `OccupyFootprint`(폰 밀어내기 포함) → **떠난 자리와 새 자리 둘 다** 재베이크 → `BuildingDormancy.Reevaluate`(영역은 그대로고 건물만 움직여 `OnTerritoryChanged`가 안 뜬다)
- **바닥(floor) 시스템**: `BuildingData.IsFloor` → 무료 substrate(코어 무관). `MapManager._floored`가 타일 → **바닥 윗면 Y**를 희소 저장. 배치 높이는 `SurfaceHeight` + `SpawnOnSurface` — 프리팹 루트 `localPosition.y`가 양수면 **저작 부양 높이**로 보고 그만큼 띄우고(서브코어 등), 아니면 렌더러 바닥면을 표면에 맞춘다(피벗 중앙 프리팹 파묻힘 방지). 즉시 배치/청사진/고스트/자유 배치 동일. 건물의 "바닥 위에만" 게이트 = `FloorRequired`(= `requireFloor` 플래그 ∥ 바닥 존재) — 던전 씬은 플래그를 켤 것. 바닥은 좌클릭 드래그 페인트 + 그리드 강제. **공허 이동 차단 3중**: 길찾기가 바닥 밖 셀 unwalkable + `PawnMover` 낭떠러지 가드(`ledgeLookahead`) + `fallRecoveryY` 낙하 복구. AI 목적지는 `IsStandable` 검사.
- **미리보기·청사진·완성품은 루트 로컬 위치를 똑같이 버려야 한다.** `Instantiate(prefab, pos, rot)`는 프리팹 루트의 `localPosition`을 **덮어쓴다**(그래서 `SpawnOnSurface`가 부양 높이를 다시 얹는다). 그런데 `SetParent(transform, false)`는 그 값을 **그대로 물려받는다** — 청사진 고스트가 이 길이었고, 루트가 0이 아닌 프리팹은 청사진과 완성품이 그만큼 어긋났다(`Bed.prefab` 루트 z = 0.5 → 침대가 반 칸 뒤에 놓임). 고스트는 `localPosition = (0, AuthoredHover, 0)`으로 못 박을 것. **씬에서 끌어내 만든 프리팹은 루트에 씬 좌표가 박혀 있다**(작업대 셋·연구대가 26m대) — 지금은 코드가 무시하므로 무해하지만, 프리팹 편집 화면에서는 엉뚱한 자리에 보인다.
- **폰의 피벗은 발밑이다.** 콜라이더 바닥과 모델 발끝이 모두 **0**이고 `center.y = height/2`다. 그래서 `PawnPlacement.Lift`(루트에서 바닥까지)가 **0**이고, **씬에 놓을 때 루트 Y = 지면 높이** 그대로다 — 유니티가 피벗을 지면에 붙이는 것이 곧 "발로 선다"가 된다. 몸 한가운데에 두면 놓는 족족 **정확히 절반이 묻힌다**(2026-08-14에 그렇게 겪었다: 처음엔 발끝을 −1로 맞췄더니 네발짐승만 통째로 잠겼고, 몸을 피벗 가운데로 옮기니 이번엔 전부 절반이 묻혔다).
- **몸통 자리를 묻는 곳은 `PawnPlacement.CenterOffset`을 쓴다** — 피벗이 발밑이므로 어깨에 메거나(`PawnCarry`) 침대에 눕히거나(`PawnMover.SetSleeping`) 누운 폰의 클릭 캡슐을 놓을 때는 발이 아니라 **몸통**이 기준이라 그만큼 비켜 놓아야 한다. **상수로 박지 말 것** — 높이가 종족마다 다르다(늑대 1.0 · 곰 1.4 · 사람 2.0). `SelectionIndicator`가 발밑 원을 `-0.9`로 박아 뒀다가 짐승의 표식을 땅속에 그렸다.
- **폰의 발끝은 루트 기준 −1이다 — 몸집을 바꿔도.** 몸은 루트의 `Model` 자식으로 지어져 있고 종족·역할별 크기가 그 자식의 `localScale`이다. **그런데 자식을 키우면 콜라이더는 안 커진다** — `CharacterController`는 루트에 있고 높이 2·중심 0으로 고정이라, 몸집만 곱하면 발끝이 `−1 × 몸집`으로 옮겨 가 **큰 놈은 땅에 박히고 작은 놈은 뜬다**(곰 1.45 → 0.45 파묻힘, 고블린 0.80 → 0.20 뜸. 몸집 1.00인 것들만 멀쩡해서 "어떤 건 박히고 어떤 건 발바닥으로 선다"로 나타났다, 2026-08-14 수정). **`Model.localPosition.y = 몸집 − 1`로 되돌린다.** `PawnSpec.BodyScale`은 이 함정이 없다 — 그건 **루트**를 스케일해서 컨트롤러가 같이 커진다.
- **폰은 표면 "위에" 찍는다.** `TileToGround`는 **바닥 윗면 Y**라서 그대로 `Instantiate`하면 `CharacterController`(중심 0·높이 2)의 아래 절반이 파묻히고, 유니티가 겹침을 풀며 옆으로 밀어내다 가장자리면 그대로 떨어진다. 캡슐 절반(`height/2 - center.y`)만큼 올릴 것(`EventManager.SpawnPawn`). 핵 소환이 멀쩡했던 건 `GetSpawnPosition`이 **핵의 부양 높이**를 써서 공중에서 착지했기 때문이다.
- **낙하 복구 지점도 확인한다** — `_lastGroundedPos`를 그냥 쓰면 **무한 낙하**가 된다(한 번도 땅을 밟은 적 없는 폰은 그 값이 태어난 자리 = 바닥 밖이다). `PawnMover.SafeRecoveryPoint`가 설 수 있는 칸인지 보고 아니면 가장 가까운 바닥으로 보낸다.
- **핵 영역(크립) / 건물 휴면**: 영역(핵 union) 밖 건물은 `BuildingDormancy`가 휴면(작동 정지+회색 틴트, 철거 X). **휴면 = 전면 사용 불가** — 창고 집계 제외(`Storage.IsActive`), 컴포넌트 `enabled=false`, 클릭해도 패널 안 열림, 매달린 일감은 `JobBase.IsSiteUsable`로 **배정 보류**(취소 아님 → 복구 시 재개). 외부 판정 `BuildingDormancy.IsDormantAt`. 런타임 배치는 `Ensure`로 자동 부착(씬 사전 배치는 수동). **핵 이전** = `DungeonCoreUI` [핵 이전] → `CoreRelocator` → `RelocateTo`. 상세 `docs/design_core_creep.md`

### Input & Camera

| Script | 트리거 | 동작 |
|---|---|---|
| `SelectionManager` | 좌클릭/드래그 | Player 팩션 유닛 선택. **UI에서 시작한 클릭은 놓을 때까지 통째로 무시**(`_pressOverUI`)(누를 때만 걸러선 안 된다 — 놓는 쪽이 새어 엉뚱한 사각 선택이 된다). 바닥의 드롭 클릭은 노드·시체처럼 **선택 유지**. 적/NPC 클릭 = `OnPawnInspected`(정보만, 명령 대상 아님) — **여기서도 폰 선택은 유지한다.** 적을 눌러 보는 이유는 대개 그 다음에 무언가를 시키려는 것이라(살펴보고 우클릭으로 친다), 지우면 적을 한 번 볼 때마다 폰을 다시 골라야 한다. **선택 = 명령 대상, 조사 = 보는 대상**으로 갈리고 `InspectPanelUI`가 "조사 중인 것이 있으면 그쪽"으로 가려 준다. 조사는 일회성이라 내 폰을 다시 고르거나 빈 곳을 누르면 `OnPawnInspected(null)`로 풀린다. **선택은 고정이다 — 푸는 조작이 없다**(켄시식, 2026-08-11). 빈 곳을 눌러도 패널만 닫히고 폰은 그대로다: 선택은 "지금 누구에게 명령하고 있나"라는 **지속 상태**라 빈 땅을 눌렀다고 바뀔 이유가 없고, 안 그러면 카메라를 돌리다 땅을 스칠 때마다 팀이 통째로 풀린다. **사각 선택도 잡힌 것이 있을 때만 갈아치운다**(빈 땅을 훑어도 안 풀린다 — 사각형은 더 자주 빗나간다). 바꾸는 길(다른 폰 클릭·사각 선택·로스터)이 이미 있고 "아무도 안 고른 상태"가 필요한 곳은 없다. 대신 **`PruneSelection`이 매 프레임 걷어낸다** — 파괴된 폰과 **꺼진 폰**(원정을 떠나 GameObject 비활성). 꺼진 것은 `== null`로 안 걸려서, 안 걷어내면 우클릭 명령이 지도 밖의 누군가에게 조용히 나간다. **그리고 `EnsureSelection`이 빈 자리를 메운다**(2026-08-15) — 폰이 하나라도 있으면 **무리 첫 번째**(`PawnOrder.FirstOfBand`)를 자동으로 고른다. 판을 시작하는 순간과 마지막 폰이 죽는 순간에 빈 상태가 남으면 **우클릭이 조용히 아무 일도 안 한다**. 다 죽었거나 전부 원정을 나갔으면 아무 일도 안 한다(고를 사람이 정말로 없다). **폰이면서 `IWorldClickable`이기도 하면 클릭 대상이 이긴다**(상인 = 상점이 열린다) |
| `MoveController` | 우클릭 | 방랑자 이동/타겟 설정. **UI 위에서 누른 우클릭은 월드로 안 샌다** — 창을 열어 둔 채 그 위를 우클릭하면 뒤의 땅으로 이동 명령이 나갔다. 좌클릭(`SelectionManager._pressOverUI`)처럼 누름/놓음을 짝지을 필요는 없다: 우클릭은 **누르는 그 프레임에 전부 끝나므로**(드래그가 없다) 누른 곳만 보면 된다. **적을 겨눴을 때의 뜻은 상황이 정한다**(`Fighting`) — **붙기 전에는 창**(칠까 / 마무리할까 / 따라붙을까 + 시간도 있다), **이미 싸우는 중이면 즉시 공격**(뜻이 "저놈을 쳐라" 하나뿐이고 표적 바꿀 때마다 창이 뜨면 조작만 느려진다). 방랑자 BT엔 `DetectNode`가 없어 `Target`이 저 혼자 안 붙으므로 이 검사는 곧 **"내가 공격 명령을 내려 뒀나"**다(맞고 있는 중 `HasFreshAttacker`도 전투). **쓰러진 놈은 싸우는 중이어도 창**(할 수 있는 게 마무리뿐인데 그건 지목해야만 된다). 중립·아군 = 늘 창. 선택지 구성은 `MoveController`가 정한다. **겨냥(`PickTarget`)은 `RaycastAll`을 거리순으로 훑으며 명령 받는 폰을 건너뛴다** — 우클릭은 고른 폰들에게 내리는 명령이라 **그들 자신은 대상이 될 수 없다**(폰 하나를 고르고 그 폰을 누르면 `따라간다`(자기를 따라감)가 떴다). 고르지 **않은** 아군은 안 거른다 — 그건 [따라간다]를 시키려고 일부러 누르는 길이다. 즉시 공격과 창이 **같은 결과를 쓴다**(따로 쏘면 규칙이 한쪽에만 걸린다). 창 제목은 **개체 이름 + 종족**(`던컨 (인간)`) — 종족만 띄우면 고블린이 셋일 때 셋 다 "고블린"이고, **데이터가 어긋나도 그럴듯해 보여 안 드러난다**(씬의 `enemy`가 `Faction_Goblin` + `Human` 데이터였는데 이름이 붙자마자 잡혔다) |
| `DialogueUI` | 우클릭 `[말을 건다]` | **사람과 말을 주고받는 창 — 화면 아래에 가로로 길게 눕는다.** 대화는 **상대를 보면서** 하는 것이라 가운데에 큰 판을 띄우면 말 거는 그 사람이 가려진다(`WorldMenuUI`가 왼쪽을 통째로 쓰는 것과 갈리는 자리 — 그쪽은 *장소*에 들어설 때 뜨고 볼 것이 지도뿐이다). **좌우 여백은 대칭이 아니다**: `InspectPanel`(오른쪽 끝 400)과 `PawnRoster_Panel`(폭 340)을 비켜선 **Left 420 / Right 360**이다 — 저 둘을 옮기면 여기도 같이 고칠 것. 선택지는 **`PawnMenuOption`**(우클릭 창과 같은 그릇 — 못 고르는 줄은 흐리고 올리면 이유가 뜬다), 줄은 씬의 `OptionTemplate` 복제. **선택지 콜백이 다음 `Open`을 부르는 것이 곧 대화의 흐름**이라 트리 자료구조가 없다. **닫고 나서 콜백을 부른다**(순서가 뒤집히면 새로 연 마디를 그 자리에서 닫는다). 무엇을 띄울지는 **`Conversation`이 정한다** — 상인=물건 / 떠돌이=합류(`PawnSpec.Price`, 마을 고용가와 **같은 상수**) / 갇힌 자=설득·몸값·풀어 주기 / 그 밖 중립=인사. **하는 일은 전부 이미 주인이 있는 함수로 흐른다**(`Conversion`·`WorldRansom`·`Imprisonment`·`MerchantUI`) — 대화는 **문을 하나 더 내는 것**이지 규칙을 새로 쓰는 것이 아니다. **거래 줄은 그대로 둔다** — 물건을 사는 것은 가장 자주 하는 일이라 앞에 한 단계를 얹으면 분위기가 아니라 손해다. 기절·시체·업힌 사람·적대에는 안 뜨고(대답할 수 없거나 말을 섞을 상대가 아니다) **갇힌 사람만 예외로 통한다**(설득이 곧 그 일이다). 합류시킬 때는 `UnitBrain.ClearNpcRole()`을 `ChangeFaction`과 **짝으로** 부를 것 — 역할이 남으면 `BuildTree`가 NPC 트리를 다시 골라 **우리 편인데 명령을 안 듣고 떠돌다 맵을 나간다** |
| `PawnContextMenuUI` | 우클릭(교전 중 멀쩡한 적·맨바닥 제외) | 우클릭한 **월드 지점에 고정**되는 팝업(카메라를 움직여도 그 자리 — `LateUpdate`에서 매 프레임 화면 좌표로 푼다). 제목 바·닫기 버튼 없음(고르거나 딴 데 누르면 닫힘). 줄은 씬의 `OptionTemplate` 복제. **선택지는 `PawnMenuOption`**(`WorldMenuOption`과 같은 모양) — `(문구, 동작)` 튜플에서 **암묵 변환**된다. 동작이 `null`이면 **못 누르는 색**(채도 뺀 회색 — 투명도만 낮추면 "글자가 좀 흐리네"로 읽힌다)이고, `Note`가 있으면 **마우스를 올렸을 때** `HoverTooltipUI`가 이유를 띄운다(작은 팝업이라 줄마다 설명을 달면 정작 고를 것을 가린다). **`Button.interactable`이 꺼져도 포인터 이벤트는 온다** — 그래서 못 누르는 줄에서 오히려 설명이 되는 이 구조가 성립한다. **닫을 때 툴팁을 거둔다** — 줄이 파괴되면 `OnPointerExit`가 안 와서 설명만 화면에 남는다. 창이 모드 무관이므로 **`HoverTooltipUI`도 모드 무관**이다(아니면 지도에서 줄만 흐리고 이유는 영영 안 뜬다). **자기 자신도 겨눌 수 있다** — 자기를 눌렀으면 거래·따라가기·공격을 빼고 **치료만** 남긴다. 대상별 선택지: 유닛=거래(상인, **쓰러졌으면 안 뜬다**)/따라간다/**치료한다**(다쳤을 때, 우리 편만 — 침대에 없어도 된다)(**쓰러진 놈에겐 안 뜬다** — 누운 것을 따라다닐 수는 없다)/**공격 — 문구는 상태와 무관하게 [공격한다] 하나**(쓰러졌으면 안에서 `FinishWith` → `UnitBlackboard.FinishTarget`으로 갈린다. 상태마다 낱말이 다르면 [마무리한다]가 별개의 동작인 줄 알게 된다), 노드=캔다·벤다(배정)/살펴본다, **드롭=착용한다(장비)/줍는다/창고로 옮긴다 ↔ 여기 둔다**, 시체=뒤진다, 핵·건물=연다, **침대=여기 눕는다(=배정, 곧 "쉬어라")·일어난다·`이름`을(를) 치료한다**(명령을 받으면 `Rouse`가 침대에서 일으킨다 — `SetMoveTarget`이 누운 자세는 이미 풀지만 **배정·점유가 남으면** 도착하자마자 도로 눕고 그 침대를 아무도 못 쓴다(`RestNode`는 명령이 도는 동안 안 돌아 스스로 못 비운다). **기절한 폰은 건드리지 않는다** — 못 일어나는데 침대만 뺏으면 깨어나 돌아갈 자리가 없다)(누가 누워 있고 손댈 것이 남았을 때만 — 둘 다 **가장 가까운 한 명**에게 간다. 침대는 하나뿐이고 치료도 한 사람이면 족하다), **어디든 마지막에 [여기로 이동]**. **[줍는다]**(`PickUpTarget` + `PickUpNode`, 방랑자 트리 `ManualOrderNode` 바로 아래)는 **자동 운반과 다른 축** — `GroundHaulJob`은 "창고로 가져다 놓아라"는 큐 작업이라 창고가 없거나 운반을 껐으면 안 돌고, 그러면 바닥의 물건을 집을 길이 없다. 규칙 셋: ① **들 수 있는 폰 중 가장 가까운 한 명만** 보낸다(앞사람 짐이 찼으면 다음 폰). ② **혼자 골랐으면 한도를 넘겨서라도 든다**(`PickUpIgnoreLimit`) — 배분이 아니라 "저걸 네가 들어라"라서 손으로 넣는 것과 같고 과적은 속도로 값을 치른다. **여럿일 때만 한도가 누가 갈지를 가른다.** ③ 무게와 **격자 자리를 둘 다** 본다(격자는 한도를 무시할 때도 못 넘는다). **[착용한다]는 줍기와 한 동작**(`PickUpThenEquip`, 장비 위에선 맨 위 선택지) — 원래 차던 것은 `Equipment`가 배낭으로 돌려주고, 착용은 짐으로 남지 않으므로 한도를 안 본다. **맨바닥은 창 없이 즉시 이동**(선택지가 하나뿐인 창은 클릭만 늘린다). 따라가기 = `UnitBlackboard.FollowTarget` + `FollowNode`(방랑자 트리에서 `ManualOrderNode` 바로 아래) |
| `InventoryUI` | I키 | 인벤토리/장비 패널 |
| `UnitStatsUI` | C키 | 스탯 패널 토글 |
| `ColonyUI` | P키 | 무리 관리(작업 on/off + 전투 성향, 폰 전체) |
| `FactionPanelUI` | L키 | **세력 우호도를 한눈에.** 살펴보기 창은 **고른 폰의 세력** 하나만 알려 주고(그 폰이 눈앞에 있어야 한다) 알림은 문턱을 넘는 순간에만 뜬다 — **지금 전체가 어떤지**를 묻는 자리가 따로 있어야 한다. **가운데에서 갈라지는 막대**로 그린다(한쪽으로 차오르면 "0이 저작한 그대로"라는 것이 안 보인다). **나쁜 쪽이 위로** 정렬 — 급한 것이 위에 있어야 열자마자 읽힌다. **세력의 성질(몸값·고용 가능 여부)은 안 적는다** — 목록이 길어지면 정작 봐야 할 수치가 묻힌다. 그건 우클릭·대화에서 선택지로 드러난다. **세력은 전부 뜬다**(`DataManager.AllFactions`, 플레이어 세력만 제외) — 처음엔 만난 세력만 띄웠는데 **판을 시작하고 열면 거의 비어 있었다.** 세상에 누가 있는지를 알려 주는 것이 이 창의 절반이고, 안 만난 세력의 시작 우호도도 **저작된 값이라 볼 값이 있다**(망자 −100은 만나기 전에도 참이다). 판정은 `FactionRelations.TryOverride` → `FactionData.StanceTo`로 **실제 전투 판정과 같은 길** — 따로 재면 "표에는 적대인데 안 때린다"가 생긴다 |
| `ExpeditionUI` | G키 | 원정대 편성 — 폰 체크 + 적재량 요약. [출발] 시 **바로 사라지지 않고** 핵 앞으로 집결(`ExpeditionFormation`), 전원 도착 후 함께 떠남 |
| `PawnContextMenuUI` (월드) | 월드맵에서 무리를 **우클릭** | 필드와 **같은 창·같은 규약** — 거래한다·말을 건다(중립만) / 공격한다 / 따라간다 / 살펴본다 / 이 자리로 이동. **고르는 순간 정해지고 가서 그대로 벌어진다**(`BandIntent`) — 다다른 뒤에 또 물으면 같은 결정을 두 번 한다. 따라가기는 중립을 따라다니는 동안 창을 안 띄운다. `GameModeManager`의 **모드 무관 UI**라 지도에서도 살아 있고, 카메라를 넘겨받는다(월드에선 `Camera.main`이 꺼진 필드 카메라다). **연 프레임의 클릭은 무시한다** — 안 그러면 창을 연 그 우클릭이 곧바로 창을 닫는다(필드에선 실행 순서가 우연히 맞아 안 드러났다) |
| `WorldMenuUI` (조우) | 지도에서 무리와 **맞닿음** | **멈춰 세우는 건 적뿐이다**(`Interrupts`) — 적은 지나칠 수 없고(싸우거나 도망치거나 항복하거나), 중립은 스쳐도 아무 일이 아니다(매번 창이 뜨면 순찰대를 지날 때마다 걸린다). 중립과 할 일이 있으면 우클릭으로 작정하고 간다. 적대=`싸운다`/`도망친다`/**`항복한다`**, 중립 상단=`거래한다`(마을 시장과 같은 창)/`습격한다`/`지나친다`. **패배와 항복은 짐이 아니라 사람에서 갈린다** — 둘 다 등짐 전부를 잃지만 패배는 크게 다치고 **포로가 되어 끌려가고**, 항복은 아무도 안 다치고 풀려난다(짐만 덜 뺏기게 하면 항복이 늘 손해라 고를 이유가 없다). **도망은 발이 정한다**(속도 차 — 비슷하면 굴린다). **전력을 숨기지 않는다**(붙을지 말지가 선택이 되려면 재료가 있어야 한다). **필드를 보는 중엔 메뉴 대신 알림**(조작 없이 판정하면 선택을 뺏고, 조용히 넘기면 왜 다쳤는지 모른다) |
| `WorldMenuUI` | 월드맵에서 칸에 **도착**(빈 야생 포함) | **글자가 길면 창이 늘어난다**(`FitToContent`) — 줄 높이도 패널 높이도 씬 고정값이라 두 줄짜리 문구(포로 안내)가 들어오면 칸에서 잘리고 목록이 패널 밖으로 넘쳤다. **문구를 줄여서 풀지 않는다**: 여기 뜨는 말은 `WorldMapView`가 정하고 앞으로도 느는데, 그때마다 글자 수를 세면 UI가 내용을 검열하게 된다. **두 번 잰다**(첫 패스엔 칸 너비가 없어 TMP가 높이를 못 낸다), **매번 저작한 높이에서 다시 잰다**(늘리기만 하면 긴 메뉴를 한 번 본 뒤로 창 크기가 지금 내용과 무관해진다). 마운트앤블레이드식 도착 메뉴(화면 왼쪽 고정, 좌 그림 / 우 선택지). **선택지 구성은 `WorldMapView.OpenMenu`가 정한다** — `탐험한다`(매장 조사) · `시장에 간다`(메뉴 안 메뉴 → 상인별 한 줄, `TownMarket`) · `마을을 둘러본다`(메뉴 안 메뉴 → 인력 영입, `TownRecruits`) 동작, 소굴·`직접 건설`은 "준비 중" |
| `BuildModeUI` | B키 | 건설 모드 |
| `DraftController` | R키 | **소집 토글** — 고른 폰(명령 대상만)을 싸울 태세로 붙들거나 푼다. **전부 켜거나 전부 끈다**(개체마다 뒤집으면 셋 중 하나만 켜져 있을 때 결과를 매번 확인해야 한다). 글자를 치는 중이거나 월드맵에서는 안 먹는다. `HaulDesignator`처럼 **씬 배선이 없다** |
| `WindowRaiser` | (클릭·창 열기) | **지금 만지는 창이 앞으로 온다.** **창을 여는 문도 여기다**(`WindowRaiser.Open` — `panel.SetActive(true)`를 부르던 스물한 곳이 전부 이 문을 지난다): 상자·작업대를 누르는 것은 **월드 클릭**이라 UI 레이캐스트에 안 걸려서, 이미 열린 창을 다시 눌러도 안 올라왔다. 여는 곳마다 두 줄을 적게 하면 **나중에 창을 하나 더 만들 때 그것만 조용히 안 올라온다**. **이미 맨 위면 아무것도 안 한다**(툴팁은 `Show`가 매 프레임 불릴 수 있어 캔버스가 헛돈다). 창은 **캔버스의 직속 자식**이 단위다(`GameModeManager`가 필드/월드를 갈아 끼울 때 쓰는 것과 같은 눈금). **늘 위에 있어야 하는 것은 다시 올린다** — 우클릭 창 · 툴팁 둘 · **손에 든 아이콘**(창 뒤로 숨으면 무엇을 들었는지도, 거절 흔들림도 안 보인다). **`LateUpdate`에서 돈다** — `EventSystem`이 먼저 처리해야 집어 든 아이콘이 이미 캔버스에 나와 있다. `HaulDesignator`처럼 **씬 배선이 없다** |
| `HaulDesignator` | H키 | **운반 지정 모드** — 바닥 물건에 "실어 가라"를 찍는다. 클릭=단일 / 드래그=일괄 / 우클릭=해제. 철거 모드와 **같은 조작**이라 새로 배울 것이 없다. 건설 모드가 켜지면 물러난다(드래그가 겹치면 무엇이 찍히는지 알 수 없다). **씬 배선이 없다** — `RuntimeInitializeOnLoadMethod`로 스스로 선다(아무 상태도 안 들고 저작할 것도 없는 순수 입력기라 맵마다 배선을 반복할 이유가 없다) |
| `CameraController` | WASD/중클/스크롤 | 카메라. **`Time.unscaledDeltaTime`으로 움직인다**(`Dt`) — `GameTimeManager`가 `Time.timeScale`을 만지므로 그냥 `deltaTime`을 쓰면 **멈춘 동안 카메라가 얼어붙고**(멈춰 세우는 이유의 절반은 둘러보려는 것이다) 3배속에서 세 배로 튄다 |
| `CameraController` (월드) | WASD/중클/스크롤 | 월드맵 카메라 — 필드와 같은 컴포넌트. `GameModeManager`가 **월드 카메라에 붙은 것은 월드 측**으로 보고 필드와 반대로 켠다 |
| `WorldMapView` | M키 | 필드↔월드 **모드 전환**(`GameModeManager`에 위임 — 반대편은 통째로 꺼진다). **원정대는 여럿**. 나가 있는 팀마다 토큰이 서고, 없으면 토큰도 없다. **선택 조작은 필드맵과 동일** — 좌클릭=단일 선택 / 드래그=사각 다중 선택 / 빈 곳=해제. **우클릭=선택한 팀만 이동**. **내용물 있는 칸에 도착하면 `WorldMenuUI` 자동 오픈**(M&B식 — 멀리서 클릭해선 안 열린다), Enter=한 팀만 선택했을 때 그 메뉴 재진입. 토큰에 콜라이더가 있어 `RaycastAll` 거리순으로 "토큰인 첫 충돌"(선택) / "토큰 아닌 첫 충돌"(이동)을 가른다. 월드에서 **자유 실시간 이동**(우클릭한 지점으로 직진, 지형 표면을 탄다 — 격자 스냅 없음). 지형은 `WorldMapTerrain`이 저작 격자를 재료로 **맵 전체를 메시 한 장**으로 짓는다(바이옴 성격을 이웃끼리 보간 → 칸 경계 없음). 컴포넌트를 빼면 직접 만든 지형을 쓴다. `WorldMapGrid`는 좌표 변환 + 격자선 + 오브젝트 배치 |

### 세력 관계는 움직인다 — `FactionRelations`

**`FactionData`는 초기값이고, 우호도가 그 위에 얹힌다.** 전에는 관계표가 고정이라 세력이 아홉이어도 배경화면이었다 — **마을 사람을 몰살하고도 그 마을과 계속 거래할 수 있었다**(중립을 죽여도 반격하는 것은 `LastAttacker`가 붙은 **그 개인뿐**이었다).

- **전쟁 깃발이 아니라 수치다**(−100 ~ +100). 깃발이면 관계가 두 칸짜리라 마을 사람 하나를 벤 것과 마을을 불태운 것이 같아진다. 수치는 **쌓이고**, 되돌릴 길(몸값·풀어 주기)이 값을 갖는다.
- **시작값은 세력마다 저작한다**(`FactionData.StartingFavor`) — **적대라도 정도가 다르다**: 산적 −55(돈이 목적이라 무를 여지가 크다) / 야수 −65 / 교단 −80 / 고블린 −85(말이 안 통한다) / **망자 −100**(바닥 — 뼈와는 화해가 성립하지 않는다) / 마을 사람 +75 / 용병 +65(아군이되 장삿속) / 변방민 0(벌어야 한다). **0이면 태도에서 뽑는다**(적대 −70 / 아군 +75) — 안 적어 둔 세력이 `적대 (0)`으로 뜨는 것을 막는 폴백이다.
- **플레이어와의 태도는 우호도 하나가 정한다 — 사이 구간도 포함해서**(`HostileAt −50` / `AllyAt +60`, 사이는 중립). 처음엔 문턱을 넘을 때만 덮고 사이는 저작이 이기게 했는데, **시작값을 저작하고 나니 그게 무르는 길을 막았다**: 적대로 저작된 세력은 −40까지 올라와도 여전히 적대라, 아군(+60)에 닿기 전에는 아무 변화가 없다 — 놓아 주고 몸값을 치른 것이 **숫자만 움직이고 세상은 그대로**였다. 시작값이 이미 저작한 뜻을 담고 있으므로 그 뒤로는 우호도가 답이면 된다. **저작은 시작점이지 판결이 아니다.** (단 플레이어 세력이 아직 안 잡힌 프레임에는 `TryOverride`가 **거짓**을 돌려준다 — 거기서 중립이라고 답하면 판이 시작되는 순간 적이 안 덤빈다.)
- **얹는 자리는 `Faction.StanceTo` 하나**다. `IsHostileTo`·`IsAllyOf`·탐지·오사 회피가 전부 거길 거치므로, 다른 데서 또 물으면 "화면에는 적대인데 안 때린다"가 생긴다.
- **움직이는 것은 플레이어와의 관계뿐**이다. 세력끼리의 전쟁은 그것을 움직일 사건이 아직 없어서, 표만 만들면 무엇이 왜 변했는지 아무도 모른다.
- **무엇이 움직이나**: 죽이면 −12(`UnitBrain.Die`), 가둬 두고 죽이면 −25(손에 넣어 두고 죽인 것은 싸우다 죽인 것과 다르다 — 굶겨 죽인 것도 여기 걸린다), **중립 무리를 먼저 덮치면 −20**(`WorldMapView.Fight` — 지도 위 전투는 폰을 죽이는 게 아니라 피해를 나눠 주는 것이라 `Die`의 −12가 안 걸린다. 여기 한 번이 먼저 친 값의 전부다), 몸값 받고 돌려보내면 +8, **그냥 놓아 주면 +14**(몸값은 거래지만 이건 베푼 것이다 — "돈이냐 관계냐"가 고르는 일이 된다). **이미 적대인 상대를 죽이는 것은 안 센다** — 그건 사건이 아니라 전쟁이고, 세면 습격 한 번 막을 때마다 관계가 바닥을 친다. **적대인지는 판정보다 먼저 묻는다**(싸움이 끝난 뒤엔 −50을 넘어가 이미 적대로 바뀌어 있다).
- **한 번 놓아 준 것이 두 번 값을 받지 않게** `Imprisonment.Release`는 `reason == DefaultReleaseReason`일 때만 얹는다(몸값 경로는 제 몫을 이미 더했다).
- **눈에 보인다** — 문턱을 넘으면 알림(`관계 변화`), `InspectPanel`이 `산적  껄끄럽다 (−32)`를 적는다(우호도 0이면 안 적는다 — 늘 떠 있으면 안 읽는다).

**우호도가 무엇을 바꾸나 — 그러나 값은 안 바꾼다.** 태도(적대/중립/아군)만 정하면 우호도는 관리할 것이 못 된다: 올려 봐야 "안 덤빈다" 하나뿐이고, 그건 이미 적대가 아닌 세력에겐 아무 변화도 아니라 **마을 사람(+75)과 변방민(0)이 화면의 숫자만 다르고 하는 일이 똑같았다.**

그래서 값 배율을 붙여 봤다가 **걷어냈다**(2026-08-14 (16)→(17)). **상대별 flat %는 선택이 아니라 순위를 만든다** — 지역차(`MerchantData.ItemOverrides` 0.5~2.0, 4배)는 품목마다 방향이 갈려 "무엇을 어디서 사서 어디에 파나"를 묻지만, 상대에게 붙은 배율은 그 상인의 **모든 품목을 같은 방향으로** 민다. 오버라이드가 없는 품목(무기·방어구·약 — 대다수)에서는 그게 유일한 차이가 되어 평원·숲으로 쏠렸고, 마을 사람 +75는 **저작한 시작값**이라 그 20%가 "관계를 쌓아 얻은 것"이 아니라 *평원이 원래 싸다*로 나타났다 — 넣은 취지와 정반대다.

무역을 다루는 게임들이 대체로 같은 자리에 놓는다: **가격은 지역·수요공급이, 평판은 접근권과 위험이** 맡는다(마운트앤블레이드·Pirates!·X4·엘리트). flat %를 쓰는 쪽은 그걸 **상대가 아니라 플레이어에게** 붙인다(림월드 Social, 스카이림 화술).

| 축 | 무엇이 정하나 | 어디에 |
|---|---|---|
| **값** | **`Trading`**(폰의 `StatName.Social`) | 사교 130 → 구매 −18% / 매입 +18%. **모든 상인에 똑같이** 걸려 어디로 갈지를 안 왜곡한다 |
| 진열 종류 수 | `FactionRelations.SlotBonus` | −2 ~ +2. **쏠림을 안 만든다** — 평원에서 고를 것이 많아도 광물은 여전히 동굴에 가야 한다 |
| 습격 소굴 추첨 | `FactionRelations.RaidWeight` | −100 → ×2 / 0 → ×1 / +100 → ×0.25. **미워할수록 자주 온다** |

- **값이 오가는 자리는 넷이고 넷 다 사교를 탄다** — 상점(`Shop`) · **몸값**(`WorldRansom.PriceFor`/`PayoutFor`/`Sell`) · **마을 고용**(`WorldMapView.RecruitCost`) · **떠돌이 합류·용병 계약**(`Conversation`). 상점만 타면 흥정이 절반만 서고, 몸값은 글자 그대로 흥정이라 거기가 제일 어색했다. 곱하는 자리는 **`Trading.Haggle(기준가, 흥정하는 폰, buying)`** 하나다.
- **누가 흥정하나 — "그 일을 하러 간 폰이 있으면 그 사람, 없으면 그 자리에 있는 우리 편 중 제일 잘하는 사람"**(`Trading.HagglerFor`).

  | 어디 | 간 사람 | 누가 흥정하나 |
  |---|---|---|
  | 대화창 | 말을 건 폰(`speaker`) | **그 폰** |
  | 필드 우클릭(몸값) | 명령 받은 폰 | **그 폰** — 대화창과 **같은 식**(`Nearest(commanders, 대상)`)으로 고른다 |
  | 상점 | 없다(상인을 클릭할 뿐) | 맵 최고(`Trading.BestOnField`) |
  | 지도 위 | 없다(원정대가 통째로 갔다) | 원정대 최고(`Trading.BestOf`) |

  - `BestOnField`는 `Faction.All`을 훑는다(`MaterialPool`과 같은 통로) — 그 레지스트리가 이미 "지금 살아 있고 켜져 있는 것"을 들어서 **원정 나간 폰·시체·용병이 저절로 빠진다.** 쓰러졌거나 갇힌 사람은 따로 뺀다.
  - **한때 넷 다 "그 자리 최고"로 뭉갰다가 되돌렸다**(2026-08-14). 뭉갠 이유는 같은 포로의 값이 우클릭 창과 대화창에서 달라 보이는 것이었는데, 그러면 **맵 최고 ≥ 말을 건 폰**이 항상 참이라 대화창의 `[네 쪽에 돌려보내 주지]`가 **영영 고를 이유가 없는 줄**이 된다(값을 받으려면 창을 닫고 우클릭하는 것이 언제나 낫다). 두 값이 보이는 것보다 나쁘다. 진짜 답은 **두 문이 같은 사람을 고르게 하는 것**이었다.
  - **상점은 왜 다른가** — 거기엔 "간 사람"이 없다(폰이 상인 옆으로 걸어가지도 않는다). 그런데도 고른 폰에 매달면 값을 잘 받으려고 매번 "그 폰을 골랐던가"를 신경 써야 하고, **그건 선택이 아니라 잡일이다.**
- **상점은 배낭 주인과 흥정하는 사람이 다르다**(`MerchantUI.TraderPawn` ↔ `HagglerPawn`). 물건이 오가는 것은 **고른 폰의 배낭**이고(순환 버튼으로 갈아탄다) 값을 다투는 것은 **맵의 사교 최고**다 — *"값은 우리 협상가가 부르고 물건은 내가 지고 간다"*. 하나로 묶으면 둘 중 하나가 어긋난다: 사교 최고에 배낭까지 묶으면 **내가 보려던 배낭이 아닌 것이 열리고**, 고른 폰으로 흥정하면 몸값·고용과 규칙이 갈린다. 창에 이름이 둘 뜨는 값은 치르되 흥정하는 쪽 이름을 **재고 줄**에 적고(`Trading.Note` — 제목 바는 44px이라 두 줄이 잘린다), 왜 그런지는 **사교 스탯 툴팁**(`StatInfo`)이 답한다.
- **폰 순환(`InventoryUI.SetPawnCycle`)은 필드에서도 쓴다**(2026-08-14). 한동안 월드맵 전용이었다 — "필드는 폰을 클릭해 고르는 방식이 이미 있다"가 이유였는데 **둘은 같은 일이 아니다**: 선택은 *누구를 데리고 왔나*, 순환은 *그중 누구 배낭을 보나*다(순환 범위가 선택의 부분집합이라 겨룰 데가 없다). 없으면 여럿을 고른 채 상점을 열었을 때 **레이캐스트가 먼저 잡은 폰의 배낭에 갇힌다.** 거래하러 온 사람들은 **창을 열 때 못 박는다**(`SelectedPawns`) — 창이 떠 있는 동안 뒤의 땅을 잘못 눌러도 거래 중인 배낭이 발밑에서 갈리지 않는다.
- **`negotiator` 인자에 기본값을 안 둔다** — 안 넘기면 조용히 맹탕 값이 나오는 대신 부르는 쪽이 매번 적게 해서, **값을 띄우는 곳과 치르는 곳이 같은 사람을 넘기도록** 강제한다. 실제로 그 짝이 셋이다(우클릭 라벨↔`Sell` · 메뉴 안내문↔몸값 선택지 · 고용 목록↔`Hire`). `TownRecruit.Summary`에서 값을 뺀 것도 같은 이유 — 요약에 구워 두면 목록의 액수와 빠지는 액수가 갈린다.
- **`Trading`은 개선만 있고 벌칙은 없다**(사교 0 = 배율 1.0). 낮은 쪽에 벌칙을 주면 저작한 `BaseValue`가 "아무도 못 받는 값"이 되어 데이터의 수와 화면의 수가 늘 어긋난다.
- **흥정하면 는다**(`Trading.GrowFromTrade(폰, 오간 돈)`, `CombatXp.Sol`) — **오간 돈에 비례한다. 상한은 없다**(`GoldPerPoint` 10,000).
  - **건수도 개수도 아닌 이유**: ① 건당 지급은 스스로 막으려던 것을 부추겼다 — 화살 50개를 한 번에 사면 +1.5, **한 개씩 오십 번 사면 +47.7**(F- → C+)이었다. 총액 비례가 쪼개기에 중립인 유일한 형태다. ② 개수로 재면 화살 50개(150G)가 강철 흉갑 한 벌(1,256G)보다 쉰 배를 가르친다. 흥정은 **얼마짜리를 놓고 다퉜느냐**의 일이다.
  - **하루 상한을 뒀다가 걷어냈다**(2026-08-15). 막으려던 것은 옳았지만(*몇 분 캐면 10만G가 나오는데 그게 그대로 스탯이 되면 곤란하다*) **상한은 그것을 막는 대신 딴것을 부순다**: 그 뒤로는 돈이 아무리 많아도 못 올라가 "돈을 쏟아부어 협상가를 키운다"가 아예 불가능해지고, 왜 안 오르는지를 화면에 따로 적어 줘야 한다.
  - **곡선이 이미 그 일을 한다.** 성장이 `Sol`=(1−S/131)²에 눌리므로 누적 거래액이 닫힌 식으로 나온다 — **G(S) = 10,000 × S ÷ (1 − S/131)**, 131에서 발산한다(**상한이 아니라 값이 막는다**). 사교 5 = 5만G · 30 = 39만 · 50 = 81만 · 100 = **422만** · 120 = 1,428만. *"몇 분이면 버는 10만G"*는 **바닥에서 5점**이고 위쪽에선 거의 아무것도 아니다.
  - 주 성장로는 여전히 **간수 쪽**이다(설득은 회차당 4.0) — 장사는 밑천만 있으면 얼마든지 하지만 포로는 잡아 와야 한다.
- **흥정할 사람을 지목할 수 있다** — 재고 줄 오른쪽 **[흥정] 버튼**(씬의 `StockLabel/HagglerButton`, 보기 전환 왼쪽 → `MerchantUI.OpenHagglerPicker`). 한때 재고 줄 **글자 자체**를 눌러 열었는데 둘이 나빴다: 글자는 눌리는 것처럼 안 보여 **있는 줄도 모르는 기능**이 되고, `[바꾼다]` 같은 꼬리표를 붙이면 그 줄이 길어질수록 그게 먼저 잘린다. **기본은 자동(그 자리 사교 최고)이고 지목은 선택**이다: 자동만 있으면 값은 늘 최선이지만 **사교를 배울 사람이 영영 한 명**이고(쓰는 사람만 자라는 스탯이다) 둘째 협상가를 키울 길이 없다. 반대로 매번 고르게 하면 가장 잦은 조작(물건 사기)에 한 단계가 얹힌다.
  - **창을 새로 안 짓는다** — 우클릭 창(`PawnContextMenuUI.OpenAtScreen`)이 이미 목록 그릇이다(줄 모양·못 누르는 줄·설명 툴팁·밖을 누르면 닫힘). 새로 지으면 그 규칙이 두 벌이 되고, 덤으로 **씬을 안 건드려도 된다**(재고 줄에 `Button`을 런타임에 얹는다 — `ResearchTableUI.PrepareSlot`과 같은 자리의 판단).
  - **고르는 목록과 자동으로 뽑는 목록이 같은 함수다**(`Trading.CandidatesOnField` / `CanHaggle`) — 따로 훑으면 "목록에 없는 사람이 흥정하고 있다"가 생긴다. 줄마다 **구매 몇 %인지 적는다**: 이름과 스탯만으로는 값에서 얼마인지 몰라 고르는 것이 짐작이 된다.
  - **지목은 상점을 닫아도 남고**(가게마다 다시 고르게 하면 키우려는 뜻이 안 선다) **그 폰이 못 하게 되면 조용히 자동으로 돌아간다**(쓰러짐·갇힘·죽음·원정으로 꺼짐). 되돌리는 줄(`자동 —`)이 목록 맨 위에 늘 있다.
- **진열 수는 하나를 남긴다** — 0이 되면 사실상 거래 불가인데 그건 태도(적대)가 이미 하는 일이고, 여기서 또 하면 왜 못 사는지가 두 갈래가 된다.
- **습격은 총량을 안 건드린다** — 갈리는 건 *누가 올 차례인가*뿐이고 빈도는 전역 시계(`raidIntervalDays`)가 그대로 정한다. 우호도로 빈도까지 흔들면 소굴 수만큼 곱해지던 사고(2026-08-14 (5))가 축만 바뀌어 돌아온다. **산적 포로를 풀어 주면 산적 습격이 줄어든다**(−55 → 놓아 줄 때마다 +14)가 이 축이 만드는 고리다.
- **값을 매기는 곳은 `Shop` 하나다** — `Shop.Trader`(지금 흥정하는 폰)가 배율을 들고, 구매가는 `Reprice()`가 `ListPrice`(배율 뺀 기준가)에서 다시 내고 매입가는 `GetSellPrice`가 읽을 때마다 낸다. **`Price`를 읽는 다섯 곳에서 각자 곱하지 않는다** — 화면에 뜬 값과 실제로 빠지는 값이 갈리는 것이 가장 나쁘다.
- **가게의 세력**(`Shop.Faction`, 진열 수용): 필드 상인 = 폰의 `Faction`(**물어볼 때마다 맞춘다** — 회유·계약으로 소속이 바뀐다. **`Awake`에서 상점을 짓지 않는다** — 그 시점엔 `Faction.Player`가 아직 안 잡혀 보너스가 0으로 굳는다) / 지도 위 상단 = `WorldBandData.Faction` / 마을 시장 = **`BiomeContentData.TownFaction`(저작)**. **`TownBands`에서 끌어오지 않는다** — 한 바이옴의 그 목록에 여러 세력의 무리가 같이 들어 있어(마을 하나에 여러 무리가 드나든다) 목록 순서가 마을 세력을 정하는 꼴이 된다. 평원·숲 = 마을 사람 / 나머지 다섯 = 변방민.

### 폰을 뭐라고 부르나 — `UnitBrain.DisplayTitle`

`이름 (역할·종족 · 세력)` — `던컨 (떠돌이 · 변방민)` · `카릭 (인간 · 자유 용병단)` · `그르낙 (고블린)`.

**딸림말은 "무엇을 하는 사람인가 · 어느 편인가" 둘이다.** 역할(`NpcData.DisplayName`)이 있으면 그것, **없으면 종족이 그 자리를 대신한다**(늑대를 `(야수)`라고만 하면 곰과 구별이 안 된다). 세력은 늘 붙이되 **플레이어 세력만 뺀다**(내 폰에게 "(플레이어)"는 아무것도 안 알려 준다). **같은 말이 두 번 나오면 하나로 줄인다** — 고블린 적은 종족도 세력도 "고블린"이라 안 줄이면 `(고블린 · 고블린)`이 된다.

**창마다 만들지 말 것** — 우클릭 창(`MoveController.PawnTitle`)과 대화창(`Conversation.Title`)이 이걸 같이 쓴다. 따로 만들면 같은 폰이 우클릭에선 `(인간)`, 대화에선 `(떠돌이)`로 불린다. (`InspectPanelUI`는 예외로 제 것을 쓴다 — 거기는 `이름 [유형] (세력)`을 색까지 나눠 띄우고 **시체의 소속까지**(`Corpse.Faction`) 다루는 더 자세한 창이라 같은 물음의 중복이 아니다.)

**역할 이름은 `NpcData.RoleName`**(비우면 타입 기본값: 마을 사람·경비·상인·행상인·떠돌이). 에셋 이름(`Drifter`)을 그대로 쓰면 그게 곧 번역 안 된 UI다.

### 사람 값을 묻는 곳 — `PawnSpec.PriceOf`

**`UnitBrain.Spec`을 직접 읽지 말 것.** 그건 `PawnSpecData`를 **저작한 폰에만** 붙는다 — 굴려서 태어난 폰(적·하수인·이벤트로 온 사람)은 `null`이라 값이 0이 되고, 값으로 게이트한 선택지가 **통째로 사라진다**(2026-08-14: 떠돌이 합류·용병 계약이 정확히 그래서 안 떴다. 상인만 되던 이유가 그쪽만 값을 안 쓰기 때문이었다).

`PriceOf(폰)`은 **명세가 있으면 그 값**(마을 후보는 메뉴에서 본 값과 같아야 하니 흔들림까지 그대로), 없으면 **실제 스탯**에서 낸다(`GradeAverageOf` — 흔들림 없음. 흔들림은 시드에서 뽑는 것인데 명세가 없으면 기댈 것이 없다). `WorldRansom`이 같은 문제를 먼저 겪고 실제 스탯을 읽고 있었는데, 그 계산을 여기로 올려 **답을 한 벌로** 만들었다.

### 용병 계약 — `Hireling`

**합류와 다른 축이다.** 합류(떠돌이)는 값을 한 번 치르고 **사람이 되는 것**이라 일도 시키고 성장도 하고 영영 남는다. 계약은 **기간을 사는 것**이라 끝나면 간다 — 그래서 값도 **하루치 × 날수**(`PriceOf ÷ 14 × 5일`)라 오래 붙들수록 비싸고, 그것이 "위기에 잠깐 부른다"를 만든다(싸게 오래 사면 그냥 일꾼을 사는 것이 된다).

- **세력을 안 바꾼다**(`Conversation.Contract`) — 이게 이 축의 중심이다. 처음엔 플레이어로 바꿨는데, 그러면 명부(P)에 뜨고 좌클릭으로 골라 우클릭 명령까지 먹어 **"싸움만 한다"가 성립하지 않는다.** 안 바꾸는 편이 오히려 **더 적게 만든다**: `Faction_Mercenary`는 이미 플레이어와 아군이고 산적·고블린·망자·교단·야수에 적대라 곁에서 알아서 싸우고(역할도 없고 플레이어도 아니라 `UsesEnemyAi`가 참 → 적 트리), 명부·선택·작업 큐가 전부 "플레이어 세력인가"로 거르므로 **막을 것을 따로 안 만들어도 아무 데도 안 뜬다.** 계약이 하는 일은 **값을 치르고 며칠 머물게 하는 것**뿐이다.
- 그래서 `JobManager.RequestJob`의 `Hireling.IsHired` 게이트는 **안전망**이다 — 세력이 안 바뀌니 어차피 안 불리지만, 나중에 세력을 바꾸는 길이 생겨도 잡일이 새지 않는다.
- **만료되면 `Imprisonment.Release`를 쓰지 않는다** — 그건 도주 성향까지 씌운다. 용병은 도망치는 것이 아니라 **일이 끝나서 가는 것**이다. 나가는 길만 같은 자(`TryFindExit` → `LeaveMapNode`). **세력은 만료 때도 안 건드린다** — 애초에 안 바꿨으므로 되돌릴 것이 없다.
- **다시 부르면 연장이다**(`Sign`이 남은 기간에 더한다). 갈아 끼우면 남은 날이 사라진다. `HomeFaction`은 "어느 용병단에서 왔나"를 기억할 뿐이고, **계약 중인지는 `Hireling` 컴포넌트가 있느냐로 묻는다**(`IsHired`) — 세력이 그대로라 세력으로는 못 가른다.
- **누가 값을 받고 싸워 주는가는 세력이 답한다** — `FactionData.ForHire`(`AcceptsRansom`과 같은 자리). 표시명으로 가리면 언어를 바꾸는 순간 고용이 사라진다.

### 개체 명세 — `PawnSpec` (`Assets/1_Script/unit/PawnSpec.cs`)

**"이 폰이 어떤 사람인가"를 태어나기 전에 적어 둔 것.** 이름 · 스탯 등급 53개 · 몸 크기, 그리고 그것을 만든 **시드 하나**.

`UnitBrain`은 `Awake`에서 이름과 스탯을 제 마음대로 굴린다 — 그게 기본값이다. 하지만 **보고 나서 데려오는 경우**(마을 고용)엔 메뉴에 뜬 사람과 합류한 사람이 같아야 하므로, `UnitBrain.ApplySpec(spec)`이 그 결과를 덮는다. `Spec` 프로퍼티로 남는다.

- **모든 무작위가 시드 하나에서 나온다** → 같은 시드면 몇 번을 다시 계산해도 같은 사람(세이브 없이도 일관).
- **외모 커스터마이징의 자리이기도 하다.** 굴리는 것과 사람이 정하는 것이 **같은 그릇**을 쓰므로, 나중에 머리·피부색·장식이 늘어도 `PawnSpec`에 필드가 늘 뿐 적용 통로(`ApplySpec`)는 그대로다. 지금은 `BodyScale` · `MaterialOverride` · `ModelOverride`가 그 축이다(`ApplyLook`이 입힌다 — 전용 모델은 기본 렌더러를 **끄고** 자식으로 붙인다. 지우면 초상화·선택 표식처럼 렌더러를 훑는 것들이 깨진다).
- **수준(tier)을 먼저 뽑고 스탯은 그 언저리(±1칸)에서 굴린다.** 53개를 각각 균등하게 굴리면 평균이 중심으로 몰려(표준편차 1/√53) 누구나 한복판이 되고 "강한 방랑자"가 성립하지 않는다.
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
- **표시**: 스탯 창(C)은 맨 위에 이름+오프셋(`곰손  손재주 -12 · 근력 +8`), 체력 창은 이름만 한 줄(`특성  강인함 · 겁쟁이`). **마우스를 올리면 설명**(`HoverTooltipUI`, 씬 오브젝트) — 스탯·특성 둘 다. TMP **`<link>`로 글자 안에서 영역을 잡으므로**(`FindIntersectingLink`) 스탯 53개마다 오브젝트를 두지 않는다. 구조·위치 계산은 **`ItemInfoUI`와 같다**(커서 툴팁이 둘인데 규칙이 다르면 안 된다): 컴포넌트는 **늘 켜져 있는 부모**에 살고 자식 `panel`만 토글한다 — 꺼지는 오브젝트에 붙이면 `Awake`가 안 돌아 `Instance`가 null로 남고 툴팁이 한 번도 안 뜬다. 배경은 `raycastTarget = false`(커서를 먹으면 깜빡인다). **링크 태그는 정렬 바깥에 감을 것** — `{F(...),-50}`처럼 rich text에 자릿수를 매겨 열을 맞추는 코드라 태그가 안쪽에 들어가면 열이 어긋난다.

### 성장 배경 — `BackstoryData` / `BackstoryPool` (2026-08-19)

**유년기 하나 · 성년기 하나.** 림월드식으로 *"어떻게 자랐나"*와 *"무엇을 하며 살았나"*를 가른다 — 하나로 뭉치면 둘 다 있어야 말이 되는 사람(거리에서 자란 상단 호위)이 표현되지 않는다. 에셋은 `Resources/Data/Backstories/`(36종 — 유년기 16 · 성년기 20), 레지스트리 스캔이라 씬·프리팹 배열에 등록하지 않는다.

- **특성과 나란한 축이지 같은 축이 아니다.** 특성은 **타고난 기질**이라 개수를 굴리고(0~3) 배타 묶음으로 겹침을 막지만, 배경은 **살아온 이력**이라 칸이 정해져 있고 **반드시 하나씩** 나온다(내력이 없는 사람은 없다). 그래서 개수 굴림도 배타 묶음도 없다.
- **`PawnSpec`이 시드에서 굴린다 — 그런데 맨 끝에서.** 난수열은 부르는 횟수로 자리가 정해지므로 특성 뒤·스탯 앞에 끼우면 **스탯 굴림이 통째로 밀려 같은 시드가 다른 사람을 낳는다**(저작한 고정 개체와 마을 후보가 한꺼번에 바뀐다). 꼬리에 붙이면 앞의 것이 전부 그대로다.
- **사람이면 누구나 갖는다** — 내 폰이든 산적이든 마을 사람이든(`UnitData.UsesBackstory`, 늑대에게 유년기를 굴려 줄 이유는 없다). **붙잡은 산적을 살펴볼 때 `패거리 두목`이라고 뜨는 것**이야말로 회유할지 몸값을 받을지 정하는 재료다. 안 얹으면 세력별 이력을 저작한 것이 화면에 한 번도 안 나타난다. **작업 금지는 저절로 갈린다** — `WorkSettings`는 플레이어 세력 일꾼에만 붙으므로(`ApplyJobBlocks`가 `Work == null`이면 아무 일도 안 한다) 적에게는 걸릴 자리가 없고, 회유해 오는 순간 `ChangeFaction`이 다시 얹는다. 그래서 `무능력: 운반`은 적을 볼 때 **데려오면 어떻게 되는지**를 미리 알려 주는 줄이 된다.
- **공용이라는 것은 없다 — 모든 배경은 어느 무리의 것이다.** 한때 묶음이 빈 것을 "공용"으로 두고 늘 후보에 넣었는데, 그러면 **산적에게서 `사원의 시동`이 나온다** — 무리에서 뽑은 사람이 그 무리와 상관없는 이력을 지고 오면 세력마다 이력을 갈라 둔 뜻이 통째로 지워진다. "공용"으로 보이던 것들(농가·대장간·사원·광산)은 사실 **`civilian`**이었고, 이름을 붙이고 나니 규칙이 한 줄로 줄었다: **제 무리의 것에서 뽑는다**(유년기도 성년기도).
- **`civilian`이 폴백이다** — 묶음을 안 적은 세력(야수·망자·고블린)이나 그 무리의 배경을 아직 저작하지 않은 칸은 여기로 떨어진다. 아무 무리에도 안 걸려 배경이 **없는** 것보다 여느 사람처럼 자랐다고 보는 편이 늘 낫다. `BackstoryData.Group`을 비운 것도 `civilian`으로 친다 — 저작을 빠뜨렸다고 아무 데서도 안 나오면 **아무도 모른 채 풀만 조용히 얕아진다.**
- **그래서 무리마다 칸마다 여럿이어야 한다** — 하나뿐이면 잡아 온 산적이 하나같이 `도망친 아이 · 산적`이 되어 이력이 개성이 아니라 딱지가 된다. 지금 **36종**: 유년기 16(civilian 7 · bandit 4 · cult 3 · mercenary 2) · 성년기 20(civilian 6 · bandit 7 · cult 3 · mercenary 4).
- **적으로 태어난 사람도 굴려는 둔다 — 얹지 않을 뿐이다**(`UnitBrain._rolledChildhood/_rolledAdulthood`). 안 굴려 두면 **회유해 온 산적이 배경 없는 사람**이 된다. 데려오고 나서 굴리면 그건 이력이 아니라 합류 보너스다.
- **작업도 막는다 — 그러나 막는 길은 `WorkSettings` 하나다**(`BackstoryData.DisabledJobs` → `WorkSettings.SetBlocked`). 큐 배정이 이미 `IsEnabled` 하나를 지나가므로(`JobManager.RequestJob`) 거기서 거짓을 돌려주면 **작업·전투 코드는 성장 배경을 몰라도 된다** — 특성이 스탯만 얹는 것과 같은 취지다(모델 A: 분기를 늘리지 않는다). **끈 것(`_disabled`)과 다른 축**이라 사람이 P패널에서 켤 수 없고, 칸에 **`×`로 뜬다**(빈 칸으로 두면 "안 켠 것"과 구별이 안 돼 눌러 보고 나서야 안다).
- **직접 배정만 예외로 한 번 더 묻는다** — 채굴·벌목·경비는 큐를 안 타므로 `IsEnabled`가 걸러 주지 않는다. `ResourceNode.Assign`·`PatrolZone.Assign`이 `IsBlocked`를 보고 **배정 자체를 거절**한다(안 그러면 *"무능력인데 바위는 캔다"*).
- **무능력은 드물다** — 17종 중 셋만 막는다(귀족가의 아이=운반 · 학자=경비 · 옥살이=간수). 전부가 하나씩 막으면 개성이 아니라 세금이고, 폰을 뽑을 때마다 "이번엔 뭘 못 하나"를 확인하는 일이 된다.
- **`TraitData.StatOffset`을 빌려 쓴다** — 같은 모양을 하나 더 만들면 두 벌이 되고, 공용 타입으로 **옮기면** 유니티가 직렬화 타입을 못 찾아 **기존 특성 에셋의 값이 통째로 날아간다**.
- 표시: 스탯 창(C) `■ 성장 배경`(특성 **위** — 기질보다 내력이 먼저 읽힌다) + `<link>` 툴팁, `InspectPanel`은 이름만 한 줄. 첫 벌은 `DungeonColony → Create Backstories`가 만들었다.

## Stat Grade System (`StatGrade.cs`, `CombatXp.cs`)

모든 스탯 0–130 범위, 5단위 27등급 (F- ~ SSS+). 상세 `docs/units.md` / `docs/combat.md`.

- `StatGrade` enum (27단계) + `StatName` enum (**53개**) — 통합 식별자
- `UnitData.DefaultMinGrade/MaxGrade` + `StatGrades` 오버라이드 리스트로 등급 정의 (Vector2 range 제거됨)
- `UnitStats.GrowStat(name, amount)` — 모든 성장 진입점. `IsLocked=true` (미니언) 시 차단
- `UnitData.StatGrowthRates` — 종족별 **특정 스탯만** 성장 속도 배율(목록에 없으면 1.0). `GrowStat`에서 `amount × GetGrowthRate(stat)` 적용
- `UnitStats.OnAnyGradeUp` static event — 등급 승급 감지 (UI 알림용)
- 켄시식 XP: `CombatXp.Calculate(my, opp, enemyCount)` = ChallengeMult × Sol × OutnumberedMult
- 전투 외 노동(`BTLeafNodes`, `CraftJob`, `ResearchJob`, `PawnMover.Athletics`)도 SOL/ChallengeMult 적용
- `ResourceNodeData.RequiredSkill` — 노동 도전 대상 레벨

## 데이터 현황표

`python Tools/export_gamedata.py` → `Tools/GameData.xlsx`(Items·Recipes·Buildings·**Traits·Backstories**·Enums, 시트마다 자동 필터) + `Tools/Items.csv`·`Tools/Recipes.csv`·**`Tools/Traits.csv`·`Tools/Backstories.csv`**(git diff용).

**Items에는 `설명`이 맨 끝 칸으로 딸려 온다**(SO `Description` ∥ JSON `description`). 유일하게 긴 칸이라 가운데 두면 숫자 칸들이 화면 밖으로 밀린다. **Unity는 긴 문자열을 여러 줄로 접어 쓰므로** 뽑는 쪽이 이어 붙여야 한다 — 안 그러면 설명이 **말 중간에서 잘린다**(`text()`가 따옴표가 닫힐 때까지 잇는다. 들여쓰기 깊이로 세면 `Loot:` 같은 목록의 자식 줄까지 삼킨다).

**특성·성장 배경의 표시명은 코드에서 읽는다** — 에셋이 `StatName`·`JobCategory`를 **정수로** 직렬화하므로 표에 한글을 손으로 적으면 스탯 하나 더하는 순간 조용히 어긋난다. 함정 둘: **enum 선언과 표시명이 다른 파일에 있을 수 있고**(`JobCategory` ↔ `JobCategoryInfo`, 한쪽만 보면 영어로 나온다), **원시 타입 리스트는 YAML 목록이 아니라 16진 덩어리다**(`DisabledJobs: 0200000006000000` — 네 바이트씩 리틀엔디언. 구조체 리스트인 `StatOffsets`는 평범한 목록이라 둘을 같은 방법으로 읽으면 한쪽이 빈다).

**표는 저작물이 아니라 에셋의 그림자다** — 에셋과 코드를 직접 읽어 만든다. 값을 바꿨으면 다시 돌릴 것. **손으로 적은 표는 반드시 뒤처진다** — 적을 때만 맞고 그 뒤로는 아무도 고치지 않는다.

제작 배수(산출값 ÷ 재료비)가 **1 미만이면 붉게** 칠해진다 — 만들수록 손해인 레시피가 표에서 가장 먼저 눈에 띈다.

## 답이 한 벌이어야 하는 것들 (자주 어긋난 자리)

같은 물음에 여러 곳이 각자 답하면 **반드시 어긋난다** — 이 저장소에서 실제로 겪은 것들이다.
새 코드를 쓸 때 여기 있는 것을 다시 만들지 말 것.

| 물음 | 유일한 답 | 어긋났던 흔적 |
|---|---|---|
| 몸이 얼마나 요구하나 | `Needs` | 허기 하나가 여섯 군데에 흩어져 있었다 — 축이 늘 때마다 그 여섯이 복사된다 |
| 지금 얼마나 보이나 | `Vision` | 교전 탐지와 도주 탐지가 따로 재면 도망만 밤에 멀쩡하다 |
| 얼마나 질 수 있나 | `CarryCapacity` | 네 군데가 따로 계산했고 둘이 **3.3배** 달랐다 |
| 재료가 어디 있나 | `MaterialPool` | 창고만 세어 손에 든 씨앗이 없는 것이 됐다 |
| 무엇을 먹나 · 누가 굶나 | `Feeding` | 먹는 길이 둘인데(제 발로 / 먹여 주는) 임계·고르기가 `EatNode` 안에 박혀 있었다 |
| 자리가 든 일감 하나 | `SiteJobSlot` | 침대·우리·먹이기가 `Tick`/`RequestBy`/`Drop`을 **세 벌** 갖고 있었다 |
| 무엇이 등급을 갖나 | `ItemData.HasQuality` | `CraftJob` 안에 `is WeaponData \|\| is ArmorData`로 박혀 있었다 |
| 시체가 지녔던 것 | `Corpse.SpillBelongings` | 도축·화장이 **각자** 배낭만 훑어, **차고 있던 무기·갑옷이 몸과 함께 사라졌다** |
| 창고에서 배낭으로 챙기기 | `Storage.FetchInto` | 셋이 **꺼낸 뒤에** 자리를 봐서, 짐이 찬 폰이 오면 물건이 창고에서 나왔다 들어갔다 했다 |
| 폰을 바닥 어디에 세우나 | `PawnPlacement` | **다섯 벌**. 함정 설명이 세 벌이었다 |
| 부린 짐의 잔량 | `Storage.Deliver` | **세 벌**, 서로 달랐고 하나는 필터를 우회했다 |
| 주변에 누가 있나 | `FactionScan` | **세 벌**, 전부 매 틱 배열을 만들었다 |
| 폰이 서는 자리 | `WorkSpot.TileFor` | Job마다 따로 계산해 통행 불가 칸을 겨눴다 |
| 얼마나 가까이 가야 하나 | `JobBase.Reach` | BT에 상수로 박혀 모든 일감이 한 자리에 붙었다 |
| 맵 가장자리가 어디인가 | `EventManager.WalkToEdge` | 나가는 길이 따로였다면 바닥 밖을 가리켰을 것 |
| 갇힘을 어떻게 푸나 | `Imprisonment.Release` | 우리·침대가 각자 풀어 결과가 갈렸다 |
| 업은 것을 어디에 놓나 | `ICarrySite` | 자리 종류마다 블랙보드 필드가 하나씩 늘어, 지우는 자리도 그만큼 늘었다 |
| 무리를 늘어놓는 순서 | `PawnOrder.Compare` | 로스터·무리 관리가 **같은 비교식을 복붙**해 갖고 있었다(방랑자 먼저 → 하수인 → 이름순) |
| 자주 쓰는 컴포넌트 | `UnitBlackboard`(`Brain`·`Body`·`Combat`·`Faction`) | 매 틱 `GetComponent`를 다시 했다 |

## 알려진 이슈

- **BT 노드에서 `Time.deltaTime` 금지**: BT는 `tickInterval`(0.1초)마다 돌므로 프레임 델타를 누적하면 시간이 6배 이상 느려진다. `UnitBlackboard.TickDelta`를 쓸 것 (2026-07-27 전수 교체).

- **BT 노드에서 델타 곱하기를 빼먹는 것도 같은 사고**: 반대 방향으로 틀린다. `ResourceNode.Work(skill)`이 틱마다 스킬 수치를 그대로 더해 채굴 속도가 **초당 skill×10**이 됐었다(2026-07-31 수정 — `Work(power, deltaTime)`로 시그니처를 바꿔 호출자가 델타를 빠뜨릴 수 없게 했다). **틱마다 누적하는 값은 전부 델타를 곱할 것** — 안 그러면 배속·프레임레이트가 게임 밸런스를 바꾼다.

- **일감을 밖에서 취소하면 들고 있는 폰에게도 놓는 길을 내야 한다**: `JobBase.Tick`은 `InProgress`일 때만 돌고 `IsComplete()`는 `Done`일 때만 참이라, **밖에서 취소된 작업은 어느 쪽에도 안 걸려 폰 손에 영원히 남는다**(도착해서 아무것도 안 하고 서 있는다). `BTLeafNodes`가 `Status == Cancelled`면 놓는다(2026-08-02, 연구 갈아타기에서 드러남).

- **조사는 `을(를)`처럼 둘 다 적고, 고르는 것은 `Josa.Fix`가 한다**(2026-08-22). 화면에 내보내는 자리에서 한 번 훑는다 — 툴팁 둘·이벤트 알림·팝업·살펴보기 창·우클릭 창·대화창·지도 메뉴가 이미 거친다. **새 sink를 만들면 거기에도 걸 것**(안 걸면 그 창에서만 `사파이어이(가)`가 보인다). 문장마다 `Josa.Eul(name)`을 부르지 않는 이유: **이름을 두 번 적게 되고**(어긋나면 조사만 딴 낱말을 본다) **에셋에 저작된 글은 코드를 못 부른다**. 짝마다 어느 쪽이 받침용인지는 표가 든다(`을(를)`은 앞, **`와(과)`는 뒤**). 리치 텍스트 태그는 건너뛰고, **앞말이 한글도 숫자도 아니면 안 고른다**(짐작해서 고르면 조용히 틀린다)
- **새 UI 텍스트에 특수문자를 쓰지 말 것**: 한글은 `TMP Settings` 폴백 체인(NanumGothic SDF → LiberationSans SDF)으로 해결됐지만(2026-08-01), 폴백은 안전망이지 허가가 아니다 — 폰트가 섞이면 자간·기준선이 어긋난다(`docs/editor_creators.md` "폰트 체인").
- **Blueprint 반투명 머티리얼**: URP에서 코드 생성 투명 머티리얼(`BuildingBlueprint.CreateBlueprintMaterial`)이 불투명 렌더될 수 있음. 에디터에서 직접 제작 후 연결 권장.
- **DataManager**: `Resources.LoadAll<RecipeData>("Data/Recipes")` + JSON 스키마(`DataSchemas`) 경로 둘 다 지원. SO는 `Assets/Resources/Data/{Items,Recipes,Research}/`에 있어야 인식.

**SO냐 JSON이냐 — 기준은 "다른 에셋이 GUID로 가리켜야 하는가"다.** JSON 아이템은 런타임 생성이라 프리팹·SO가 **가리킬 방법이 없다**(그래서 상인 재고에 `ItemId` 문자열 경로를 따로 뒀다).
- **SO여야 하는 것**: `ResourceNodeData.DropItem` · `BuildingData.CostItem` · `MinionData.SummonCostItem` · `SeedData.HarvestItem` · SO 레시피의 재료/결과처럼 **에셋이 가리키는** 아이템.
- **JSON이 나은 것**: 레시피로만 만들고 쓰는 것(무기·방어구 대부분). 모드가 고치거나 덮어쓸 수 있다. 2026-08-02에 옛 장비 16종을 이 기준으로 JSON에 옮겼다(참조 0건 실측 후).
- **모드 자산(`ModAssets`)**: JSON의 `iconPath`/`modelPath`를 **그 콘텐츠 폴더 → 에셋 번들 → Resources** 순으로 찾는다. **1번이 본체** — 그림은 PNG/JPG, 모델은 **`.obj`(+`.mtl`+텍스처)** 를 폴더에 넣기만 하면 실린다(`ObjModel`이 직접 파싱). `.fbx`는 런타임 해석 불가 → 그렇게 알려 준다. 번들은 OBJ로 표현 못 하는 것(프리팹·뼈대·애니메이션)용 보조 경로(`DungeonColony → Build Mod AssetBundle`). 폴더 밖(`../`) 경로는 무시. 예시는 `StreamingAssets/Mods/ExampleMod/`. 상세 `docs/data.md`.

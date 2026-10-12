# CLAUDE.md

Guidance for Claude Code in this repository.

**UI를 만들거나 고치기 전에 `docs/editor_creators.md`를 먼저 읽는다.** 어기면 조용히 깨지는 규격:
- **뿌리는 화면 전체, 자리는 판이 든다** — `Xxx_UI`는 캔버스 전체 스트레치, 앵커·자리·크기는 자식 `Xxx_Panel`. 생성기가 다시 돌 때 물려주는 것도 판의 자리다.
- **창은 `Xxx_UI` 루트의 자식이다** — 루트는 늘 켜져 있고 창만 열리고 닫힌다. `Update`를 도는 컴포넌트도 루트에 산다.
- **헤더는 #0D0D0D 풀폭, 레이아웃 그룹 안 씀** · **닫기는 28×28 · #802626 · 글자 `X` · 공용 `CloseButton`**(패널마다 close 메서드를 만들지 말 것) · **폰트는 NanumGothic SDF** · **특수문자 금지**(글리프가 없으면 조용히 `□`).

**상세 문서 — 이 파일은 색인이다.**
`docs/units.md`(폰·스탯·Needs·소집·명세·치비) · `docs/combat.md` · `docs/jobs.md` · `docs/build.md` · `docs/world.md`(지도·길찾기·스트리밍·밤·이벤트·층·상단) · `docs/ui.md` · `docs/data.md` · `docs/singletons.md` · `docs/prisoners.md` · `docs/merchants.md` · `docs/factions.md` · `docs/gathering.md` · `docs/editor_creators.md` · `docs/timing.md`(틱·델타·배속) · `docs/rendering.md` · `docs/design_*.md`(설계 근거) · `docs/worklog.md`(날짜별 기록 · 할 일). 각 문서 끝의 *"CLAUDE.md에서 내려온 원문"* 절에 규칙의 근거가 있다 — 그 축을 만지기 전에 읽을 것.

**여기는 색인이다** — 답이 한 벌이어야 하는 자리 · 어기면 조용히 틀리는 한 줄 · 포인터만. 세부와 사연은 `docs/`로. 150,000자를 넘으면 뒤가 잘린다(글자 수: `python -c "print(len(open('CLAUDE.md',encoding='utf-8').read()))"`).

## 결정된 축 — 한 줄씩

- **습격대의 뜻은 폰 밖에 산다** — `RaidLord`(접근 → 돌격 → 후퇴)가 어디로 갈지·떠날지만 정한다 · 후퇴는 `LeavingMap` 하나 · 갈래 수는 `ProngsFor` · 머리 위 표식은 `PawnMark` 한 벌. → `docs/design_raid_lord.md`
- **금속은 넷, 층 하나에 하나씩** — 광석 → 주괴 → 장비 · 값은 재료비 × 1.65 · 광맥 모양은 `VeinArt`. → `docs/design_metals.md`
- **목재도 층을 탄다 · 건설 비용은 목록(`BuildingData.Costs`) · 작업대 등급(`BenchTier`)이 속도·레시피·연구를 한 값으로** — 등급은 같은 건물의 상태(`BenchUpgrade`) · 얼굴을 입히는 문은 둘(`BenchArtCreator`·`PropDresser`) · 연구는 목록에서 거르지 않는다(`HandlesTier`) · 올리는 재료는 다음 층의 금속 + 나무(V는 비움, `CanRaiseTo`) · Play 시작 때 `BenchTierCheck`가 대조한다. → `docs/build.md`
- **전장의 안개** — 셋 상태 · 여는 것은 플레이어 폰의 눈 · 바위가 막는다 · 화면을 한 번 덮는다 · 찍은 표시는 안개를 안 탄다. → `docs/world.md`
- **층 넷은 한 평면에 2×2**(`Floors` — 1 남서 · 2 남동 · 3 북서 · 4 북동 · 층 하나 2,048m · 사이 기반암 `IsSeam`) — 지금 층은 카메라가 답한다(`Floors.Current`) · 스트리밍은 보는 층만 · 카메라는 층 안에 묶인다 · 무거운 표(광역 그래프·안개·지도)는 층 단위 · 층을 넘는 길은 통로(`FloorPassage`)와 핵 포탈뿐 · 무리·습격·배치·절벽 생성은 층 밖으로 안 나간다. → `docs/world.md` "층 넷"
- **깊이가 곧 티어** — 층은 지도가(`WorldMapData.Floor`), 후보는 `BiomeContentData.Tier`가 답한다 · **무엇이 몇 층에 있나는 사용자가 저작한다** · 칠한 지도는 `HandPainted`가 지킨다 · 바이옴을 묻는 자리는 전부 `TerrainGrid.BiomeSample` · 머리 위 여유는 `MapManager.HasHeadroom` 하나. → `docs/design_underworld.md`
- **사방이 바위이고 캐면서 넓힌다** — 답은 `RockMap` 하나 · 테두리 세 칸은 기반암 · 바위는 `RockRenderer` 청크 메시(자르기는 `RockCut.hlsl`을 그리는 패스 셋 전부에 · 벽선은 `Wobble`, 높이마다의 굴곡은 `RockBulge`로 흔들되 콜라이더·길찾기는 칸 그대로) · 커서로 짚는 문은 `VisiblePick` 하나 · 광맥은 바이옴 풀 한 표(바닥에 쓸 때 `IsVein`으로 거른다) · 벽 조명은 `RockMap.WallLightAt`(표는 `BiomeContentData.WallLights`) · 판 굴은 이틀 뒤 메워진다(`RockRegrow.Keeps` · `RockMap.Refill`). → `docs/design_underworld.md`
- **절벽 · 다리** — 답은 `Chasm` 하나(`ChasmData` · 칠하기·자동 생성 `World → I` · 다리 만드는 문 `World → J`) · 지형에 구멍을 뚫는다(2m 덩어리) · 땅 고르기는 절벽을 거절한다 · 다리는 양 끝 높이로 비스듬히(`Bridge.SpanOf`), 윗면은 `Chasm`이 든다(등록 `Start` · 해제 `OnDestroy`). → `docs/world.md` "절벽 · 다리"
- **세계 저작은 씬에서 하고 청크로 굽는다** — 저작 씬은 `Awake`에서 스스로 꺼진다 · 청크 에셋에 쓰는 문은 `5. Bake World` 하나 · 에디터 틱에서 런타임 물건을 건드리지 않는다(`isPlaying`) · 런타임 오브젝트 플래그는 `SelfStanding.RuntimeObject`. → `docs/design_world_editor.md`
- **짐승·떠도는 적은 청크가 설 때 선다**(`WildSpawner` · 표는 `BiomeContentData.WildSpawns`) — 청크가 설 때마다 새로 굴리고 서 있는 동안은 그 답을 든다 · 떠도는 동안은 레일(`PawnMover.OnRails`) · 내 폰 40m 안과 핵 영역에는 안 선다 · 카메라와 내 폰 둘 다에서 250m 넘으면 거둔다 · 핵 영역 밖 시체는 하루 뒤 사라진다(`Corpse.CheckVanish`) · 갇힌·업힌 폰은 무리에서 빠진다. → `docs/world.md`
- **에디터 메뉴 번호는 겹치면 컴파일 직후 에러다**(`MenuOrderCheck`) · `1. Rebuild Underworld`는 메뉴를 이름으로 부르니 번호를 바꾸면 거기도 · **끝난 일회성 도구는 지운다**.
- **상단 · 포탈 · 첫 마을** — 핵은 포탈(`PortalTravel` — 걸어서 닿아야 넘어간다) · 상단은 출발 핵에 다 모였다가 포탈 너머로 빠지는 추상 여정(`Caravan` · `CaravanManager` · 창 `T` · 짐은 실제 창고에서 빠지고 든다 · 길 위 습격은 `WorldBattle.ResolveAmbush`) · 첫 마을이 상단을 연다(`TownSite` · `Caravans.Unlock`) · 세력 창은 만난 세력만(`FactionRelations.Meet`). → `docs/world.md` "상단"
- **탐험은 이어진 세계를 걸어서 한다** — 옛 3D 월드맵·원정대·화면 전환(`GameModeManager`)은 없다. 필드인지 묻지 않는다. 고용 후보 표 `BiomeContentData.TownRecruits`는 읽는 곳이 없다(마을 고용을 다시 열 때 쓴다). → `docs/world.md` 끝
- **길들이기 · 가축 · 수레** — 우리에 가둔 `UnitData.Tameable` 짐승을 조련(`Conversion.CanTame` · `TameJob`) · 가축은 운반만(`UnitBrain.IsTamedBeast`) · 수레를 단 동안만 명령을 받는다(`PawnCart` · `CartData`). → `docs/units.md`
- **벽은 둘** — 외곽벽은 칸을 먹고 이웃과 잇는다(`FortWall`) · **얇은 벽은 변을 막는다**(`EdgeWalls` — 로컬·지름길·광역이 한 표를 읽는다 · 콜라이더는 `Default` 층). → `docs/build.md`
- **미룬 것**(건물 에셋 대기) — 마을 건설 · 마을=핵 · 건물 2층(층 넷과 다른 장치 — 같은 XZ에 바닥 둘). **남은 미결** — 월드 에디터의 퀘스트.

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Palworld + Mount&Blade + Dungeon Core. 유닛은 **방랑자(Wanderer)**(직접 제어)와 **하수인(Minion)**(자동 AI, 던전 코어 소환). **루프는 탐험이 본류다** — 기지는 잠깐 들러 점검하는 곳이고, 탐험해 가져온 자원으로 장비·하수인을 만들어 더 험한 지역에 간다(`docs/design_progression.md`).

## Development Environment

- Unity 2022+, New Input System, TextMeshPro. 동작 확인은 Unity Editor Play Mode.
- **컴파일은 에디터 없이 본다** — `dotnet msbuild Assembly-CSharp.csproj -t:Build`. **에디터 코드는 `Assembly-CSharp-Editor.csproj`** — 에디터 파일을 건드렸으면 둘 다 돌릴 것.
- **`.csproj`는 유니티가 만든다** — 파일을 새로 만들거나 지운 직후에는 낡아 있다(새 파일을 안 보고 "오류 없음"이 나오거나, 지운 파일 때문에 실패한다). 유니티에 포커스를 주거나 사본 csproj의 `<Compile>`을 맞춰 확인할 것.
- **`Directory.Build.props`가 참조 해석기의 프레임워크 검사를 끈다**(DOTween 4.7.2 ↔ csproj 4.7.1). 타깃을 올리지 말 것.

## 폴더

`Assets/1_Script/` — `Combat` · `Data` · `Event` · `Input` · `Item` · `Job` · `Map` · `UI` · `World` · `core` · `unit`(`UnitBrain`은 partial — `.Wanderer`·`.Minion`·`.Enemy`·`.Npc`가 트리를 짓는다) · 최상위에 `BuildModeManager` · `GameTimeManager` · `LightSource` · `UnderworldLighting` · `RuntimeShaders` 등. 에디터 도구는 `Assets/Editor`.

에셋은 `5_Data`(SO) · `2_Material` · `3_Prefab`(`Building` · `Pawn` · `Node` · `Prop` · `UI`).
- **`.meta`를 짝지어 옮길 것** — 프리팹만 옮기면 새 guid가 발급돼 참조가 조용히 끊긴다.
- **이미 쓰이는 프리팹을 통째로 다시 지을 때는 루트의 fileID도 지킨다**(GameObject·Transform·컴포넌트) — 씬 인스턴스의 오버라이드가 거기 매달려 있다. 자식은 없어져도 된다.

## Architecture — Quick Reference

### Unit Types & BT 순서

| Type | Data Class | BT 순서 |
|---|---|---|
| **방랑자** | `UnitData` | **직접 명령** → 업기 → 줍기 → 따라가기 → **반격**+Fight → Rest → Eat → Work → Idle |
| **하수인** | `MinionData` | Detect+Fight → Rest → Eat → Patrol → Work → Idle |
| **Enemy** | `UnitData` | Detect → Fight → Idle |
| **NPC** | `NpcData` | 타입별 (Villager: Flee→Wander / Guard: Fight→Patrol / EventMerchant: WalkIn→Stay→WalkOut / Drifter: Flee→Wander→Leave) |

**소집(`R`)은 분기가 아니라 게이트다** — 자동 행동을 멈추고 싸울 태세로 두는 상태라, 아래 노드들이 저마다 묻는다(`docs/units.md` "소집").
- 일감을 안 집고 눕지 않는다 · **밥은 먹는다** · 먼저 안 친다(맞으면 되받고 `DetectNode.DraftLeash` 안까지 문다) · 도망치지 않는다.
- **`DropCurrentJob` ↔ `AbandonWork` — 명령이냐 상태냐.** 명령(공격·치료·먹이기·설득·업기·줍기·호송·대화·도축·사망)은 `AbandonWork`(일감 + 배정) — 목록에서 하나가 빠지면 그 명령만 조용히 안 먹는다 · 놓은 뒤에 일감을 다시 찾을 것. **이동은 배정을 안 거둔다**(붙들어 두려면 소집). 소집은 상태라 `DropCurrentJob`(일감만) · 침대 배정도 안 건드린다.

**명령을 받는가는 폰이 답한다** — `UnitBrain.AcceptsOrders`(원래 받나) / `CanObeyNow`(지금 받나). 묻는 곳(`MoveController`·`DraftController`)이 갈라 쓴다.

### 이어진 세계 — 청크 · 지형 · 길찾기 (`docs/world.md` 끝 · `docs/design_pathfinding_hpa.md`)

- **높이의 원본은 함수다**(`TerrainHeight`) · `SampleGroundHeight`는 콜라이더를 안 본다 · **값은 씬이 든다**(`WriteUnderworldProfiles`).
- **세계 한 변 4,096m, 손잡이는 `ChunksPerAxis`(64) 하나** · 개수는 밀도로 적는다.
- **바닥은 층층이다**(`TerrainHeight.Terrace` — 셀 6m · 단 1.0m) · 씬 뷰는 `TerrainHeight.Version`으로 다시 굽는다.
- **광역 길찾기는 `WideGraph`**(셀 안 연결 덩어리마다 노드) · 로컬이 못 뚫으면 광역이 배운다(`ReportUnreachable`) · 파낸 자리는 `RebakeArea`.
- 못 짓는 이유는 판정과 같은 코드(`WhyNotBuildable`) · 격자 위상은 지하에서 0으로 못 박힌다.
- 지형 텍스처는 바이옴 × 경사(`TerrainSplat`) · **`MaterialPropertyBlock`을 필드 초기화자에서 만들지 말 것**.
- 자잘한 것은 해시로 뿌린다(`ScatterRule` · 쓰는 문 `ScatterWiring`) · 땅이 안 선 곳은 물리 없이 걷는다(`GhostMove` — 벽은 `RockMap`).
- 세우기·버리기는 프레임당 예산 · 멀리 있는 남의 폰은 안 돈다(`SimLocality`) · 무리는 멀면 토큰(`BandMaterializer`).

### 팩션 · 세력 관계 · 용병 (`docs/factions.md`)

축은 다섯이고 서로를 모른다 — 종족(`UnitData`) · 세력(`FactionData`) · AI 성향(`AiProfileData`) · 역할(`NpcData`) · 소지(`LoadoutData`).
- **세력 SO는 반드시 연결할 것**(비우면 전부 중립) · 관계는 방향별 · 우호도를 얹는 자리는 `Faction.StanceTo` 하나.
- 먼저 거는가는 성향이 답한다(`DetectNode`) · **`is MinionData`는 "하수인 종족"이지 "내 편"이 아니다.**
- 값을 곱하는 자리는 `Trading.Haggle` 하나(`negotiator`에 기본값 금지) · 사람 값은 `PawnSpec.PriceOf` · 폰 부르는 이름은 `UnitBrain.DisplayTitle`.
- 용병은 세력을 안 바꾼다(`Hireling` · 누가 파나는 `FactionData.ForHire`).

### Key Singletons (`docs/singletons.md`)

- `MapManager` — `TileToWorld`(Y=0) ↔ `TileToGround`(표면) · **월드 → 칸은 `WorldToTile` 하나**.
- `DungeonCore` — 창고·재료는 핵 영역 단위(`All` · `Instance`는 조작 중인 거점 · 영역 판정 `AnyCovers`).
- `EventManager` · `GameTimeManager`(하루 1800초 · 모든 지속시간의 기준 축).
- `HierarchicalPathfinder` — 웨이포인트 사이는 `Densify` · 못 서는 칸은 `FindWorkableNear`.
- `ResearchManager` — 씬에 놓인 그래프가 진짜다 · `DataManager`(static) · `BiomeTier`(지역 난도 단일 출처).

### 몸 · 밤 (`docs/units.md` · `docs/world.md`)

- **Needs 넷은 같은 물건이고 몸이 축을 정한다**(`Needs.BodyOf`/`Runs` — 산 몸 허기·피로 · 골렘 에너지 · 해골 사기) · 저장은 `bb.Need(kind)` · 갇힌 사람은 허기만 · 채우는 자리는 `Recharger`.
- **잠** — 손으로 붙인 침대는 명령, 저 혼자 잡은 침대는 잠자리(`Bed.AutoClaimed`) · 누워 있을지는 `RestNode` 한 곳.
- **밤** — 시계는 `GameTimeManager` · 시야는 `Vision` 한 벌 · 불빛은 `LightSource`로(장식 `Light` 금지) · 초상화는 조명을 안 탄다.
- **굴은 불빛 밖이 검다** — 화면 밝기는 `UnderworldLighting`의 `gloom`, 시야는 `Tide`(둘은 따로다) · 폰의 최저 밝기는 전역 `_PawnFloor` · 등을 도는 커스텀 셰이더는 `LIGHT_LOOP_BEGIN`(PC는 Forward+) · 그림자를 던지는 등은 `ShadowBudget.Track` · 볕은 `Skylight`. → `docs/rendering.md`

### 일감 · 창고 · 채집 · 농사 (`docs/jobs.md` · `docs/gathering.md` · `docs/design_power.md`)

- 창고는 `Storage.FindNearest`/`Deliver`/`Rebind`/`FetchInto`, 재료는 `MaterialPool`, 적재는 `CarryCapacity` — **`amount`·`quality`·`from`을 빠뜨리지 말 것.**
- 일감이 목표를 바꾸면 진행 기록을 버린다 · 정체는 `PawnMover.RouteRemaining`으로 잰다 · 가까이 가는 거리는 `JobBase.Reach` · 서는 자리는 `WorkSpot.TileFor`.
- 재료를 먹는 일감은 `IsReady` + `CanAssignTo` · **틱마다 누적하는 값은 델타를 곱한다.**
- 바닥 물건은 체크한 것만 실려 간다(`HaulAllowed` · 품목은 `HaulPolicy`, O) · 야생은 찍고 기지는 배정한다(`IsBuilt`) · 한 번 하고 없어지는 것은 `Forageable`.
- 힘은 핵이 내고 먼저 지은 것이 먼저 받는다(`PowerGrid`/`PowerUser`).
- **저장 구역은 시체만**(`StockZones` — `V`의 4·5번 · 핵 영역 안 · 나르는 것 `CorpseHauling`) · 도축대도 영역 안의 몸만.
- **배치**(`WorkPosts` · `H`) — 폰을 건물에 1~3순위로 붙박는다 · 셋 다 없으면 논다 · on/off는 안 보고 `IsBlocked`만 · 일감의 건물은 `JobBase.Site`. 소환은 소환진(`SummonCircle` → `DungeonCoreUI`).
- 큐 순서(`QueueOrder`)가 전역 우선순위 · 폰 설정은 `WorkSettings`(P) · 밭은 붙어 있으면 한 구역(`FarmZones`).

### 전투 · 붙잡은 사람 (`docs/combat.md` · `docs/prisoners.md`)

- `DamageType`은 한 축(물리 셋 + 원소 일곱) · **정수로 직렬화되는 enum은 꼬리에만 붙인다**(`StatName`·`DamageType`·`JobCategory`·`BuildCategory`·`MerchantType`·`ModeBarUI.Mode`) · 안 맞는 것은 `CombatHandler.Measure` · 신성 ×2는 `IsUndead`.
- 표적이 없으면 싸우는 중이 아니다 · 도는 것은 `PawnMover.FaceTowards` · 능동은 장비가 준다(`AbilityUser`) · 도발은 `DetectNode` 맨 위.
- 몸은 켄시 눈금 · **프리팹이 든 `[SerializeField]` 값은 코드 기본값을 고쳐도 안 바뀐다** · 치료는 `Treatment` · 기절한 폰은 안 때린다(`IsAttackable`) · 지녔던 것은 `Corpse.SpillBelongings` 하나.
- 업기·옮기기는 `PawnCarry` · 받는 자리는 `ICarrySite` · 갇힘은 `Imprisonment`(푸는 문 `Release` 하나) · 세력을 바꾸는 길은 `UnitBrain.ChangeFaction` 하나 · 포로를 넘기는 몸값은 `WorldRansom.Sell`.

### 자원 · 상인 · 이벤트 (`docs/data.md` · `docs/merchants.md`)

- 자원 종류는 아이템, 묶음만 enum(`ResourceGroup`) · 등급은 장비만(`ItemData.HasQuality`).
- 지니는 것은 `LoadoutData` · **`ItemId` 오타는 조용히 아무것도 안 나온다 — 읽는 쪽은 `Resolve()`** · 값은 `Shop` 하나가 매긴다.
- 이벤트 빈도는 `quietWeight` · `MinGameTime`이 난이도 곡선 · **아무도 안 보는 습격은 추상으로 푼다**(`SimLocality.IsSimulated` → `WorldBattle.ResolveRaid`).

### 건설 (`docs/build.md`)

- 목록은 레지스트리(`Resources/Data/Buildings/`) · 값과 못 짓는 이유는 커서 옆.
- 건물 프리팹에는 `PlacedBuilding` + `data` · 걸어 들어가는 건물은 `BlockMask` · 겹쳐 짓기 판정은 차지한 칸 전체(`PlacedBuilding.ClaimedAt`).
- 청사진은 자재를 배치 때 안 낸다 · 철거·취소는 전액 창고로(`bp.Cancel()`) · 자리에 따라 모양이 갈리면 `IPlacementPose`.
- 바닥은 데이터(`RegisterFloor` + `FloorRenderer`) · 폰이 오르는 턱은 `PawnMover.StepUp` 하나 · 지름길은 `LosOpen`.
- **레이어가 곧 누구를 통과하나**(`Player` 7 · `Npc` 8 · `Enemy` 9 — `Default`에 폰을 두지 말 것 · 세력이 바뀌면 `ApplyLayer`) · 지붕과 페이드는 `BuildingRoof` · 핵 영역 밖은 휴면.
- 건물은 부서진다(`BuildingHealth` — 핵 제외) · 수리는 `RepairDispatcher` · 적은 막힌 길을 뚫는다(`DetectNode.Breach`).
- **모델 입히기는 `Building → 0. Dress All`** · `PropDresser`는 `Retired`에 적힌 것만 벗긴다 · 새 조립 도구는 `[Art]` 말고 제 이름을 쓸 것.
- **폰의 관절은 `PawnRig` 규격** — 치비 몸(방랑자·체형 여섯·고블린)은 스킨 메시 하나이고 관절까지 `Pawn/3`이 세운다(`DressSkinned`) · 나머지 블록 인형은 `Pawn/0`(1 → 2). 세부는 `docs/units.md` 치비 절.

### 입력 · 카메라 · 명령 (`docs/ui.md`)

| 키 | 창·모드 | 키 | 창·모드 |
|---|---|---|---|
| `I` 인벤토리 | `InventoryUI` | `B` 건설 / `X` 철거 · 열면 `1`·`3`이 이전·다음 갈래 | `BuildModeUI` |
| `U` 스탯 | `UnitStatsUI` | `C` 명령 · `V` 구역(밭 · 저장) · 열면 숫자가 도구 | `CommandMode` · `FarmZoneDesignator` |
| `P` 무리 관리 | `ColonyUI` | `R` 소집 | `DraftController` |
| `L` 세력 | `FactionPanelUI` | `F1~F5` 진형 | `FormationController` |
| `O` 자동운반 품목 | `HaulPolicyPanel` | `M` 지도 | `WorldMapPanel` |
| `H` 지휘소(배치) | `WorkPostsUI` | `T` 상단 | `CaravanUI` |
| `1~4` 능동 능력(건설·명령·구역이 켜져 있으면 양보) | `AbilityController` | 좌/우클릭 | `SelectionManager` / `MoveController` |

- **모드 바**(`ModeBarUI`, 하단 가운데) — 명령 · 구역 · 건설 · 지휘소 · 상단 · 무리 · 세력 · 지도(뒤의 다섯은 창 — 키와 같은 문을 부른다) · 칸은 `UI → Setup Command Bar`가 세운다. 거점 바(`OutpostBarUI`, 좌상단)는 거점이 둘 이상일 때만.
- **새 글자 단축키는 `InputFocus.KeysBlocked()`를 묻는다** · 씬 필드가 든 키(`kb[필드]`)까지 훑고 붙일 것(`I`·`U`).
- **땅 고르기**(`GroundLevel` · `LevelDesignator`) — 찍는 단위는 2m 덩어리, 일감은 높이 정점 · 막는 것은 기단뿐 · 위에 선 것은 `CollectRiders`가 옮긴다 · 폰은 제가 파는 땅 밖에 선다(`LevelGroundJob.StandTile`) · 다시 굽는 문은 `GroundLevel.Refresh` 하나.
- 도구 바 칸은 목록(`ModeToolBarUI.tiles`) · 모드는 한 번에 하나(`ExclusiveMode`) · 개수가 정해진 UI는 씬에 세운다 · 창을 여는 문은 `WindowRaiser.Open` · 카메라는 `unscaledDeltaTime`.
- 선택은 고정(켄시식) · UI에서 시작한 클릭은 놓을 때까지 무시 · 진형은 소집의 것(`Formation.Current`) · 못 누르는 줄은 지우지 말고 흐리게.

### 개체 명세 · 특성 · 배경 (`docs/units.md`)

`PawnSpec`은 태어나기 전에 적은 사람이고 **모든 무작위가 시드 하나에서 나온다 — 굴림은 반드시 꼬리에 붙일 것**. 특성은 스탯에 숫자만 얹는다 · 배경은 유년기·성년기 하나씩, 묶음은 세력이 든다(`FactionData.BackstoryGroup`) · 저작한 장소의 폰은 `WorldObject.Id`에서 시드를 낸다 · 작업 금지는 `WorkSettings.SetBlocked`.

## Stat Grade System (`StatGrade.cs`, `CombatXp.cs`)

모든 스탯 0–130, 5단위 27등급(F- ~ SSS+).
- `StatGrade`(27) + `StatName`(53) — 통합 식별자 · 등급 정의는 `UnitData.DefaultMinGrade/MaxGrade` + `StatGrades`.
- **모든 성장은 `UnitStats.GrowStat` 하나** — `IsLocked`(하수인)면 막힌다 · 종족 배율 `UnitData.StatGrowthRates` · 열정 배율 `Passions`.
- 켄시식 XP `CombatXp.Calculate` — 노동(`BTLeafNodes`·`CraftJob`·`ResearchJob`·`PawnMover.Athletics`)도 같은 셈 · 노동 도전 레벨은 `ResourceNodeData.RequiredSkill`.

## 데이터 현황표

`python Tools/export_gamedata.py` → `Tools/GameData.xlsx` + `Tools/*.csv`. **표는 에셋의 그림자다 — 값을 바꿨으면 다시 돌릴 것.** 함정은 `docs/data.md` "게임 데이터 Excel".

## 답이 한 벌이어야 하는 것들

같은 물음에 여러 곳이 각자 답하면 반드시 어긋난다. 새 코드를 쓸 때 여기 있는 것을 다시 만들지 말 것.

| 물음 | 유일한 답 |
|---|---|
| 몸이 얼마나 요구하나 · 무엇을 요구하는 몸인가 | `Needs` · `Needs.BodyOf`/`Runs` |
| 무엇을 먹나 · 무엇으로 채우나 | `Feeding` · `Recharge` |
| 지금 얼마나 보이나 | `Vision` |
| 얼마나 질 수 있나 | `CarryCapacity` |
| 재료가 어디 있나 | `MaterialPool` |
| 무엇을 그 재료로 쳐 주나 | `ItemData.CountsAs` → `Inventory.CountSatisfying`/`RemoveSatisfying` |
| 자리가 든 일감 하나 | `SiteJobSlot` |
| 무엇이 등급을 갖나 | `ItemData.HasQuality` |
| 시체가 지녔던 것 | `Corpse.SpillBelongings` |
| 창고에서 배낭으로 · 부린 짐의 잔량 | `Storage.FetchInto` · `Storage.Deliver` |
| 폰을 바닥 어디에 세우나 · 폰이 서는 자리 | `PawnPlacement` · `WorkSpot.TileFor` |
| 얼마나 가까이 가야 하나 | `JobBase.Reach` |
| 주변에 누가 있나 | `FactionScan` |
| 근접이 표적 둘레 어디에 서나 | `MeleeSlots`(폰은 둘레 여덟, 건물은 발자국 둘레 한 줄 · 다 차면 뒷줄에서 대기 · 예약은 틱마다 다시 적는다) |
| 진형에서 누가 어느 자리로 가나 | `Formation.Build`(앞뒤는 사거리 · 같은 사거리끼리는 `Match`가 걷는 거리로) |
| 맵 가장자리가 어디인가 | `EventManager.WalkToEdge` |
| 갇힘을 어떻게 푸나 · 업은 것을 어디에 놓나 | `Imprisonment.Release` · `ICarrySite` |
| 무리를 늘어놓는 순서 | `PawnOrder.Compare` |
| 자주 쓰는 컴포넌트 | `UnitBlackboard`(`Brain`·`Body`·`Combat`·`Faction`) |
| 월드 좌표 → 칸 | `MapManager.WorldToTile` |
| 밖에서 온 것이 설 자리 | `RockMap.TryNearestOpen` |
| 폰을 어느 프리팹으로 세우나 · 성별 | `PawnSpawn`(직접 `Instantiate` 금지) · `PawnBody` |
| 어느 스탯이 빨리 느나 | `Passions`(새 스탯은 `PoolOf` 표에) |
| 저작 도구가 보는 지형 값 | `TerrainAuthoring` |
| 몇 층인가 · 지금 보는 층 | `Floors`(`At` · `Current` · `Origin` · `IsSeam`) |
| 이 걸음이 막힌 변을 건너나 | `EdgeWalls`(`StepOpen` · `MaskAt`) |
| 화면을 언제 다시 그리나 | `UiRefresh`(박 0.15초 · `Due(ref _beat, 배수)`) — 켜고 끄는 것은 이 박자에 안 태운다 · 주기는 코드가 든다 |

## 알려진 이슈 (`docs/timing.md` · `docs/rendering.md`)

- **배속에서는 한 프레임이 커진다** — 시간을 깎지 말고 나눠서 돈다(`StepMove` 0.2m씩 · BT는 밀린 만큼 · 누적은 `UnitBlackboard.TickDelta`) · 틱 주기 0이면 조용히 멈춘다.
- **컴포넌트는 `TryGetComponent`로 찾는다 — `GetComponent<T>()` 뒤에 `?.`·`??`·`is { }`를 붙이지 말 것**(에디터의 가짜 null을 못 거른다 — 빌드에서는 멀쩡해 에디터에서만 깨진다) · `!= null`로만 묻는 자리는 그대로 둬도 된다.
- **조사는 `을(를)`처럼 둘 다 적고 `Josa.Fix`가 고른다.**
- **런타임 `Shader.Find`는 빌드에서 잘린다 — 목록은 `RuntimeShaders` 하나**(`Hidden/`은 넣지 말 것) · 투명하게 만드는 문은 `FadeMaterial` 하나(URP는 `_BaseColor`).
- **SO와 JSON을 같이 볼 것** — 감사는 `StreamingAssets/Data/`와 `Resources/Data/`를 둘 다.

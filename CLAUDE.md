# CLAUDE.md

Guidance for Claude Code in this repository.

**UI를 만들거나 고치기 전에 `docs/editor_creators.md`를 먼저 읽는다.** 거기에 **어기면 조용히 깨지는 규격**이 있다: **뿌리는 화면 전체이고 자리는 판이 든다**(2026-09-08 — `Xxx_UI`는 캔버스 전체 스트레치, 앵커·자리·크기는 자식 `Xxx_Panel`. 씬의 서른둘 중 스물하나가 이미 그랬다: **만지는 자리가 하나여야 하고** · 뿌리가 점 하나로 앵커되면 **해상도가 바뀔 때 판이 화면 모서리를 놓치며** · 커서를 따라다니는 툴팁 둘은 뿌리가 캔버스 크기여야 좌표가 맞는다. **생성기가 물려주는 것도 판의 자리다** — 뿌리만 지키면 다시 돌릴 때마다 손으로 맞춘 자리가 날아간다) · **창은 `Xxx_UI` 루트의 자식이다**(캔버스 **직속 자식**은 `GameModeManager`가 *필드 UI 묶음*으로 켜고 끄는 자리라, 거기에 창을 직접 두면 **모드가 바뀔 때 그 창만 규칙 밖에서 논다.** 루트는 늘 켜져 있고 창만 열리고 닫히므로 `Update`를 도는 컴포넌트도 **루트**에 산다) · **헤더는 #0D0D0D 풀폭이고 레이아웃 그룹을 안 쓴다** · **닫기는 28×28 · #802626 · 글자 `X` · 공용 `CloseButton` 컴포넌트**(패널마다 close 메서드를 만들지 말 것) · **폰트는 NanumGothic SDF를 물린다** · **특수문자를 쓰지 말 것**(글리프가 없으면 조용히 `□`가 된다). **2026-09-06에 안 읽고 짓다가 이 넷을 한꺼번에 어겼다** — 사용자가 *"이거 자꾸 안지키네 왜 규칙확인을 안하고 만드는거야"*라고 했다.

**상세 문서 — 이 파일은 색인이고 근거는 저기 있다:**
`docs/units.md`(폰·스탯·Needs·소집·명세/특성/배경) · `docs/combat.md`(피해·능력·몸·치료·시체) · `docs/jobs.md`(일감·창고·작업 자리·도축) · `docs/build.md`(건설·바닥·휴면·연구 저작) · `docs/world.md`(지도·길찾기·스트리밍·밤·이벤트) · `docs/ui.md`(창·입력·인벤토리 조작) · `docs/data.md`(JSON·모드·시간 축·가치 사슬) · `docs/singletons.md`(Key Singletons 항목별) · `docs/prisoners.md`(업기·가두기·회유·간수) · `docs/merchants.md`(상인·지갑·소지품) · `docs/factions.md`(우호도·흥정·용병) · `docs/design_raid_lord.md`(습격대의 뜻) · `docs/design_metals.md`(금속 사다리) · `docs/gathering.md`(찍기·캐기·채집·자동운반) · `docs/design_power.md`(전력) · `docs/editor_creators.md`(에디터 도구) · `docs/timing.md`(틱·델타·배속 — **같은 함정을 세 번 밟았다**) · `docs/rendering.md`(셰이더가 빌드에 들어가는 길·런타임 재질) · `docs/design_pathfinding_hpa.md`(광역 길찾기 — `WideGraph`) · `docs/design_underworld.md`(지하·천장·바위)

**최근 결정 — 한 줄씩. 규칙의 근거·겪은 사고·"어기면 조용히 깨지는 것" 전체는 각 문서의 *"CLAUDE.md에서 내려온 원문"* 절에 있다. 그 축을 만지기 전에 그 절을 읽을 것.**

- **습격대의 뜻은 폰 밖에 산다** — `RaidLord`(접근 → 돌격 → 후퇴)가 *어디로 갈지·떠날지*만 정하고 전투 규칙은 안 건드린다 · 후퇴는 `LeavingMap` 하나(`Routed`는 맞아도 간다) · 갈래 수는 `ProngsFor`(저작 안 함) · 이끄는 이력은 `LeaderOf`만 준다 · 머리 위 표식은 `PawnMark` 한 벌. → `docs/design_raid_lord.md`
- **금속은 넷이고 층 하나에 하나씩** — 광석 → 주괴 → 장비, 합금으로 상위 금속을 안 만든다 · 값은 재료비 × 1.65 · 광맥 모양은 `VeinArt`가 굽는다(팩 메시·아틀라스를 안 가져온다). → `docs/design_metals.md`
- **목재도 층을 탄다 · 건설 비용은 목록(`BuildingData.Costs`) · 작업대 등급(`BenchTier`)은 속도·레시피·연구를 한 값으로** — 등급은 같은 건물의 상태(`BenchUpgrade`) · 얼굴은 `[Art N]`을 켤 뿐 · 얼굴을 입히는 문은 둘(`BenchArtCreator`·`PropDresser` — 둘 다 두면 두 겹) · **연구는 목록에서 거르지 않는다**(`HandlesTier`) · 연구 배분은 순환 검사를 통과해야 한다. → `docs/build.md`
- **전장의 안개** — 셋 상태 · 여는 것은 플레이어 폰의 눈 · 바위가 막는다 · 화면을 한 번 덮는다(셰이더마다 안 넣는다) · 찍은 표시는 안개를 안 탄다. → `docs/world.md`
- **지하 · 층 넷 · 깊이가 곧 티어** — 층은 지도가(`WorldMapData.Floor`), 후보는 `BiomeContentData.Tier`가 답한다 · **무엇이 몇 층에 있나는 사용자가 저작한다** · 구역은 보로노이 + 도메인 워프 · 그린 층은 도구가 그대로 찍고 칠한 지도는 `HandPainted`가 지킨다 · 바이옴을 묻는 자리는 전부 `TerrainGrid.BiomeSample`을 지난다 · 머리 위 여유는 `MapManager.HasHeadroom` 하나. → `docs/design_underworld.md`
- **사방이 바위이고 캐면서 넓힌다** — 답은 `RockMap` 하나(함수 + 캔 칸) · 세계 밖은 바위, 테두리 세 칸은 기반암 · 바위는 `RockRenderer` 청크 메시(콜라이더 있음 · 자르기는 `RockCut.hlsl`을 그리는 패스 셋 전부에 · **벽선은 흔든다**(`Wobble` — 경계 위 반 칸 격자 점만 · 자리의 함수 · 높이를 안 탄다 · 콜라이더·길찾기는 칸 그대로)) · **커서로 짚는 문은 `VisiblePick` 하나** · 광맥은 바이옴 풀 한 표를 셋이 읽는다(벽의 풀을 바닥에 쓸 때는 `IsVein`으로 거른다) · `Mining`·`Logging`은 큐에도 있다 · **벽 조명(야광석·폐광 횃불)은 `RockMap.WallLightAt`이 답하고 `RockRenderer`가 드러난 면에만 세운다**(표는 `BiomeContentData.WallLights` · 짓는 문 `World → K`) · **판 굴은 이틀 뒤 메워진다**(2026-09-28 — 붕괴를 걷어냈다 · 핵 영역과 버팀목 둘레는 남는다 · 판정 `RockRegrow.Keeps` · 메우는 문 `RockMap.Refill` · 무엇이 있으면 미룬다). → `docs/design_underworld.md`
- **절벽 · 다리**(2026-09-26) — 그린 곳에만 서는 **밑이 안 보이는 낭떠러지**(`ChasmData` · 칠하기와 **자동 생성**은 `World → I` — **세계 한 변에서 맞은편까지 끊기지 않는 큰 협곡**이고 건너는 곳은 **다리 목**(다리로 건널 만큼 좁힌 자리)이다 · 다리 배치가 끊긴 곳을 채운다 · 다리는 **양 끝 높이로 비스듬히**(`Bridge.SpanOf`)) · 답은 `Chasm` 하나(통행은 `HasHeadroom`, 절벽 칸은 바위가 아니다) · **지형에 구멍을 뚫는다**(2m 덩어리 = 구멍 해상도) · 땅 고르기는 절벽을 거절하고 가장자리 정점을 못 움직이며 `MinLevelY` 아래로 못 내린다 · 다리는 종류가 무엇이든 **지을 수 있고 자연에도 선다**(`Bridge` — 윗면은 기단 목록이 아니라 `Chasm`이 들고, 등록은 `Start`·해제는 `OnDestroy`만) · 만드는 문 `World → J`. → `docs/world.md` 끝 "절벽 · 다리"
- **세계 저작은 씬에서 하고 청크로 굽는다** — 저작 씬은 `Awake`에서 스스로 꺼진다 · 청크 에셋에 쓰는 문은 `5. Bake World` 하나(씬이 세계 전부를 담았을 때만) · **에디터 틱에서 런타임 물건을 건드리지 않는다**(`isPlaying`) · 도메인 리로드가 목록을 비우면 `Resources.FindObjectsOfTypeAll`로 거둔다 · 런타임 오브젝트 플래그는 `SelfStanding.RuntimeObject` · 장소 배치는 자동(손으로 놓은 것은 안 건드린다). → `docs/design_world_editor.md`
- **짐승·떠도는 적은 청크가 설 때 선다**(2026-09-28, `WildSpawner`) — 소굴 무리(지도를 걷는 사건)와 다른 축 · 무엇이 사는가는 `BiomeContentData.WildSpawns`(무리 에셋 · 청크당 확률 · 다시 나는 날수, 초안 `World → L`) · **청크가 설 때마다 새로 굴린다**(서 있는 동안은 한 번 굴린 답을 든다 — 안 그러면 미룬 청크를 다시 볼 때마다 굴려 떼가 곱해진다) · 무리는 기준점 하나를 같이 쓰고 30m 안을 떼로 다닌다(기준점은 걸어서 닿는 칸만 · **떠도는 동안은 레일** — `PawnMover.OnRails`: 몸 없이 길만 따라 걷고 막힘·포기 진단을 안 찍는다, 표적이 생기면 몸으로) · 내 폰 40m 안과 핵 영역에는 안 선다 · 식구가 전부 **카메라와 내 폰 둘 다에서 250m** 넘게 떨어지면 거둔다(청크 기준은 카메라만 옮겨도 싸우던 떼가 사라졌다) · **핵 영역 밖 시체는 하루 뒤 사라진다**(`Corpse.CheckVanish` — 묻힌·업힌·내 사람 시체는 빼고) · **갇힌·업힌 폰은 무리에서 빠진다**(안 빼면 기지 청크가 내려갈 때 포로가 지워진다). → `docs/world.md` 끝
- **에디터 메뉴 번호는 겹치면 컴파일 직후 에러다**(`MenuOrderCheck` — 2026-09-26, `World/I`가 둘이라 씬을 저장해 버리는 도구가 잘못 눌렸다) · 목록을 따로 들지 않고 유니티가 아는 메뉴를 훑는다 · **`1. Rebuild Underworld`는 메뉴를 이름으로 부르니 번호를 바꾸면 거기도** · **끝난 일회성 이행은 지운다**(남겨 두면 번호만 차지하고 잘못 눌린다).
- **남은 미결은 둘** — 월드맵 `직접 건설`, 월드 에디터의 퀘스트. 적도 능동을 쓴다(`UseAbilityNode` — 가르는 축은 `UnitBrain.AbilityByHand`, `AcceptsOrders`가 아니다). **"구현 예정"이라고 적어 두면 다음에 읽는 사람이 이미 끝난 것을 다시 잰다.**

**짓는 중(미착수):** 없다.

**설계 기록(전부 구현됨 — 왜 그렇게 정했는지가 남아 있다):** `docs/design_progression.md` | `docs/design_combat_rework.md` | `docs/design_world_field.md` | `docs/design_core_creep.md` | `docs/design_traits.md`

**이 파일에는 150,000자 한도가 있다 — 넘으면 뒤가 잘려 나가고, 잘린 줄은 아무도 못 읽는다.** 그래서 **여기는 색인이다**: 답이 한 벌이어야 하는 자리 · 어기면 조용히 틀리는 한 줄 · 포인터. 한 축의 세부(눈금·저작 목록·겪은 사고의 전말)는 `docs/`에 둔다. **새 결정을 적을 때도 여기에는 한두 줄만, 전말은 docs로.** 네 번 내렸다(08-27 155,092 → 47,844 · 09-13 136,112 → 116,422 · 09-25 144,112 → 116,171 → **사용자: *"너무 안줄어들었잖아"*** → 절 전체를 원문 그대로 docs 끝 *"CLAUDE.md에서 내려온 원문 (2026-09-25)"*으로). **내릴 때는 지우지 말고 옮길 것.** 길이는 글자로 잰다 — `python -c "print(len(open('CLAUDE.md',encoding='utf-8').read()))"`(`wc`는 바이트를 센다).

## Project Overview

**DungeonColony** — RimWorld + Kenshi + Palworld + Mount&Blade + Dungeon Core. 두 유닛 타입: **방랑자(Wanderer)** (직접 제어), **하수인(Minion)** (자동 AI, 던전 코어 소환).

**루프는 탐험이 본류다**(2026-08-23 방향 확정, `docs/design_progression.md`) — 기지는 **잠깐 들러
점검하는 곳**이고, 탐험해 가져온 자원으로 기지에 광맥을 심고, 그것으로 만든 장비·하수인으로 더
험한 지역에 간다. **2026-08-23에 그 축이 전부 들어갔다** — 자율 배정 · 지역 티어 · 광맥 이식 ·
원소 1축 · 방패 스탯 · **도발과 능동 능력**(`AbilityData`, 08-23에 데이터부터 화면까지).
**그 마지막 미결(적도 능동을 쓰는가)은 2026-09-07에 닫혔다** — `UseAbilityNode`.
그릇은 처음부터 다 있었고(폰마다 `AbilityUser` · 적 로드아웃 여섯이 도발 방패를 든다)
없던 것은 판단 하나였다.

## Development Environment

- Unity 2022+, New Input System (`UnityEngine.InputSystem`), TextMeshPro
- CLI 빌드 없음 — 동작 확인은 Unity Editor Play Mode
- **컴파일은 에디터 없이도 볼 수 있다**: `dotnet msbuild Assembly-CSharp.csproj -t:Build`
  (산출물은 `Temp/bin/Debug`, gitignore됨). Play Mode에 들어가기 전에 문법·시그니처 사고를
  먼저 거를 것 — "CLI 빌드 없음"은 게임을 못 돌린다는 뜻이지 컴파일도 못 본다는 뜻이 아니다.
  - **`Assets/Editor`는 거기 안 들어간다 — 어셈블리가 둘이다**(2026-09-09에 겪었다).
    `Assembly-CSharp.csproj`는 런타임 코드뿐이고 에디터 도구·창은
    **`Assembly-CSharp-Editor.csproj`**다. 앞의 것만 돌리면 **에디터 스크립트의 문법
    오류가 통째로 안 잡히고**, 유니티로 넘어가서야 터진다. 에디터 파일을 건드렸으면
    **둘 다 돌릴 것.**
  - **`.csproj`는 유니티가 만든다 — 새 파일을 만든 직후에는 낡아 있다.** 그 상태로 돌리면
    **새 파일을 아예 안 보고 "오류 없음"이 나온다**(2026-08-29에 겪었다). 유니티에 포커스를
    한 번 주어 다시 만들게 하거나, 급하면 사본 csproj에 `<Compile>`을 손으로 넣어 확인할 것.
  - **`Directory.Build.props`가 참조 해석기의 프레임워크 검사를 끈다** — 유니티가 내놓는
    csproj는 `v4.7.1` 타깃인데 `DOTween.dll`은 `v4.7.2`라 msbuild가 참조를 거부하고
    (`MSB3274`) 플러그인 소스가 통째로 못 읽힌다(`CS0234` 191건). **유니티 컴파일러는 이
    검사를 안 하므로 게임에는 아무 문제가 없다.** 타깃을 올리면 안 된다(4.7.2 타기팅 팩이
    없는 기계에서 `MSB3644`로 죽는다). **생성물이 아니라 `.props`에 두는 이유는 유니티가
    csproj를 다시 만들어도 안 지워지기 때문이다.**

## 폴더 구조 (`Assets/1_Script/`)

```
1_Script/
├── BuildModeManager.cs       ← 빌드 모드 토글
├── Campfire.cs               ← 화톳불 (저녁에 켜지고 아침에 꺼진다)
├── LightSource.cs            ← 불빛 레지스트리 (`Vision`이 훑는다)
├── Combat/                   ← DamageInfo, DamageType(물리3+원소7), DamageResistance
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
├── Prop/       ← 손에 드는 소품 · 벽 조명 (기능 없는 모형)
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

**소집(Draft) — `R`키로 고른 폰을 싸울 태세로 붙든다.** **분기가 아니라 게이트다** — 트리 맨 위의 노드가 `Running`만 돌려주면 **켜는 순간 아래가 통째로 멎는다**(이동 명령도 반격도 안 먹는다). 소집은 *무엇을 하는 상태*가 아니라 **자동 행동을 멈추고 싸울 태세로 두는 상태**라, 아래 노드들이 저마다 물어야 한다(업고 있는 것과 **같은 모양**). 근거와 전말은 `docs/units.md` "소집".

- **자동 행동을 멈추되 배정은 남긴다** — 일감을 안 집고(`WorkNode`) 눕지 않는다(`RestNode`). **밥은 그대로 먹는다**(막으면 소집해 둔 것을 잊었을 때 굶어 죽는다). **먼저 안 친다**(소집이 성향의 *먼저 걸기*를 끈다) — 다만 맞으면 되받아치고 **짧은 목줄**(`DetectNode.DraftLeash`) 안의 적까지는 문다. **도망치지 않는다.**
- **`DropCurrentJob` ↔ `AbandonWork` — 가르는 축은 "명령이냐 상태냐"다.**
  - **명령은 옛 명령을 덮어쓴다** → **`AbandonWork`**(일감 **+ 배정**). 공격·치료·먹이기·설득·업기·줍기·호송·대화·**도축**, 그리고 사망·원정 집결. **이동은 2026-09-17에 여기서 빠졌다**(사용자 결정) — *"안 거두면 도착하자마자 일하러 돌아가 명령이 안 먹는 것처럼 보인다"*가 그 이유였는데 **그건 명령이 안 먹은 것이 아니라 먹고 끝난 것**이고, 붙들어 두고 싶으면 **소집(`R`)이 그 장치**다. 거두는 쪽은 *한 번 비켜세웠다고 캐던 바위가 떨어져 나가는* 값을 치른다. **목록에서 하나가 빠지면 그 명령만 조용히 안 먹는다** — 2026-09-11에 도축이 그랬다(예약은 걸리는데 폰이 하던 일을 마칠 때까지 안 와서, **놀고 있을 때만 되니 가끔 되는 것처럼** 보였다). **놓은 뒤에 일감을 다시 찾을 것** — 그 폰이 막 집어 든 참이었으면 `AbandonWork`가 `CancelJob`을 불러 **큐에서 빠지므로**, 미리 잡아 둔 참조에 예약을 걸면 아무도 못 찾는 일감이 된다(화면에는 똑같이 *안 온다*로 보인다). 자원 노드 배정도 **플레이어가 붙인 명령**이라 새 명령이 덮는 것이 맞다.
  - **소집은 명령이 아니라 상태다**(켜고 끄는 스위치) → **`DropCurrentJob`**(일감만). 끄면 이전으로 돌아가는 것이 그 스위치의 뜻이라 배정을 거둘 이유가 없다. 손에 든 일감만 놓는 것은 **게이트로 막기만 하면 그 일감이 예약된 채 얼어붙어 다른 폰도 못 가져가기** 때문이다.
  - **침대도 안 건드린다** — 일으키는 것은 `RestNode` 게이트의 `LeaveBed`가 이미 하고(자세와 **점유만** 풀고 **배정은 남긴다**), 여기서 `Bed.ReleaseFrom`을 부르면 그 사이 다른 폰이 침대를 가져간다. **소집은 잠시 딴 일을 시키는 것이지 담당과 잠자리를 떼는 일이 아니다.**

**명령을 받는가는 폰이 답한다** — `UnitBrain.AcceptsOrders`(하수인은 *원래* 안 받는다) / `CanObeyNow`(갇히거나 업힌 폰은 *지금* 못 받는다). **둘은 다른 물음이라** 부르는 쪽이 갈라 쓴다(우클릭은 후자를 "빠졌습니다"로 알리고 전자는 조용히 건너뛴다). **묻는 곳이 둘이라**(`MoveController`·`DraftController`) 각자 적으면 **명령이 조용히 사라지는 대상**이 생긴다.

### 이어진 세계 — 청크 스트리밍 · 지형 · 길찾기

**전체 원문은 `docs/world.md` 끝**(스트리밍·높이 함수·계단 바닥·광역 길찾기·격자 위상·지형 그리기·뿌리기·지도). 광역 길찾기 세부는 `docs/design_pathfinding_hpa.md`.

- **높이의 원본은 함수다**(`TerrainHeight` — 노이즈 + 저작 + 델타 + 런타임에 고른 땅). `SampleGroundHeight`는 콜라이더를 안 본다. **값은 씬이 들고 코드 기본값은 힘이 없다**(`WriteUnderworldProfiles`가 씬에 쓴다).
- **세계 한 변 1,024m, 손잡이는 `ChunksPerAxis`(16) 하나**(`ChunkSize`는 안 건드린다) · 개수는 밀도로 적는다 · 미터로 재는 값은 세계 크기를 안 탄다.
- **바닥은 층층이다**(`TerrainHeight.Terrace` — 셀 6m · 단 1.0m · 층 ±6, 셀 한복판으로 묻는다 · 수직 턱은 `Terrain` 구조상 안 된다) · 씬 뷰는 `TerrainHeight.Version`으로 다시 굽는다.
- **광역 길찾기는 `WideGraph` — 셀 안의 연결 덩어리마다 노드 하나**(HPA*) · 통과점은 열린 구간 한가운데 · 로컬 창도 같은 1m 표를 읽는다 · **로컬이 못 뚫으면 광역이 배운다**(`ReportUnreachable`) · 파낸 자리는 `RebakeArea`.
- **못 짓는 이유는 판정과 같은 코드가 답한다**(`WhyNotBuildable`) · 평탄 판정은 기울기 · **격자 위상은 첫 격자 조작이 정하고 지하에서는 0으로 못 박힌다**(바위가 근거다).
- **지형 텍스처는 바이옴 × 경사 함수**(`TerrainSplat`) · 레이어 목록은 세계 한 벌 · 캐시 키에 색조를 넣는다 · **`MaterialPropertyBlock`을 필드 초기화자에서 만들지 말 것**.
- **자잘한 것은 해시로 뿌린다**(`ScatterRule` — 규칙은 씬이 들고 쓰는 문은 `ScatterWiring` 하나 · 지하에서는 `RequireOpenTile` 필수 · 어느 바이옴에 나나는 `ScatterRule.Biomes`) · **땅이 안 선 곳에서는 물리 없이 걷는다**(`GhostMove` — 벽은 `RockMap`이 막는다).
- 세우기·버리기 모두 **프레임당 예산** · 멀리 있는 남의 폰은 안 돈다(`SimLocality`, 플레이어 세력은 언제나 돈다) · 무리는 멀면 토큰(`BandMaterializer`) · 지도는 굴을 그리고 안개를 따른다(M).

### 팩션 · 세력 관계 · 용병

**원문은 `docs/factions.md` 끝.** 축은 다섯이고 서로를 모른다 — 종족(`UnitData`) · 세력(`FactionData`) · AI 성향(`AiProfileData`) · 역할(`NpcData`) · 소지(`LoadoutData`). 조합은 프리팹이 한다.

- **세력 SO는 반드시 연결할 것**(비우면 전부 중립) · 관계는 방향별 · **우호도가 초기값 위에 얹히고 얹는 자리는 `Faction.StanceTo` 하나** · 이미 적대인 상대를 죽이는 것은 안 센다.
- **먼저 거는가는 성향이 답한다**(`DetectNode`가 매 틱 읽는다 — 방랑자 기본은 `반격`) · AI 역할은 `UsesEnemyAi` · **`is MinionData`는 "하수인 종족"이지 "내 편"이 아니다.**
- **값을 곱하는 자리는 `Trading.Haggle` 하나**(`negotiator`에 기본값 금지) · 흥정하는 사람은 `Trading.HagglerFor` · 사람 값은 **`PawnSpec.PriceOf`**(`UnitBrain.Spec`을 직접 읽지 말 것 — 굴려서 태어난 폰은 null) · 폰 부르는 이름은 `UnitBrain.DisplayTitle`.
- **용병은 세력을 안 바꾼다**(`Hireling` 컴포넌트로 묻는다 · 만료는 `Imprisonment.Release`가 아니다 · 누가 파나는 `FactionData.ForHire`).

### Key Singletons

**표 전체(주요 API와 어기기 쉬운 것)는 `docs/singletons.md`.** 자주 부딪히는 것만:

- `MapManager` — `TileToWorld`(Y=0) ↔ `TileToGround`(표면) · **월드 → 칸은 `WorldToTile` 하나** · 점유는 절대좌표 희소 저장.
- `DungeonCore` — 창고·재료는 **핵 영역 단위**(`All`이 레지스트리, `Instance`는 지금 조작 중인 거점).
- `EventManager` — `checkInterval` 60초 미만이면 경고(씬 값이 이긴다) · `GameTimeManager` — 하루 1800초 · **모든 지속시간의 기준 축** · 판은 아침에 시작(`Elapsed`에는 안 얹는다).
- `HierarchicalPathfinder` — 웨이포인트 사이는 로컬 창보다 좁게(`Densify`) · 코스 라우트는 복도 안내다 · 못 서는 칸은 `FindWorkableNear`로 접는다 · 길을 다시 낼 때 시계는 안 만진다.
- `ResearchManager` — 한 연구대는 하나만 · 잠금은 양방향 · **씬에 놓인 그래프가 진짜다** · `DataManager`(static) · `GameModeManager`(캔버스 직속 자식을 다룬다) · `BiomeTier`(지역 난도 단일 출처).

### 몸 · 밤

**원문은 `docs/units.md`·`docs/world.md` 끝.**

- **Needs 넷(허기·피로·에너지·사기)은 같은 물건이고 몸이 축을 정한다**(`Needs.BodyOf`/`Runs` — 산 몸은 허기·피로, 골렘은 에너지, 해골은 사기) · 눈금은 하루에서 나온다 · 저장은 한 벌(`bb.Need(kind)`) · **갇힌 사람은 허기만 돈다** · 채우는 자리는 지은 건물(`Recharger`) · 유지 시설은 그 하수인과 같은 연구로 연다.
- **잠** — 손으로 붙인 침대는 명령, 저 혼자 잡은 침대는 잠자리(`Bed.AutoClaimed`) · 가는 문턱 40 ↔ 나오는 문턱 100 · 누워 있을지는 `RestNode` 한 곳 · 자는 동안 피로가 안 준다.
- **밤** — 시계는 `GameTimeManager` 하나 · 광원은 하나 · 시야는 `Vision` 한 벌(교전·도주 탐지가 같이) · **밤은 총량이 아니라 구성을 가른다**(정규화) · 불빛은 `LightSource`로(장식 `Light` 금지) · 벽걸이 횃불은 `IPlacementPose` 모양 둘 · 초상화는 조명을 안 탄다.

### 일감 · 창고 · 채집 · 농사

**원문은 `docs/jobs.md` 끝**, 채집·운반 네 갈래는 `docs/gathering.md`, 전력은 `docs/design_power.md`.

- 창고는 `Storage.FindNearest`/`Deliver`/`Rebind`/`FetchInto`, 재료는 **`MaterialPool`**(배낭 먼저 · 환불은 창고 먼저), 적재는 **`CarryCapacity`** — **`amount`·`quality`·`from`을 빠뜨리지 말 것.**
- **일감이 목표를 바꾸면 진행 기록을 버린다** · **정체는 `PawnMover.RouteRemaining`으로 잰다**(직선이 아니다) · 길이 바뀌면 눈금도 다시(`RouteVersion`) · 가까이 가는 거리는 `JobBase.Reach` · 서는 자리는 `WorkSpot.TileFor` 하나.
- **재료를 먹는 일감은 `IsReady` + `CanAssignTo`** · 밖에서 취소된 일감은 폰이 놓게 한다 · **틱마다 누적하는 값은 델타를 곱한다.**
- **바닥 물건은 체크한 것만 실려 간다**(`HaulAllowed`) · 품목 단위는 `HaulPolicy` 하나(O) · **야생은 찍고 기지는 배정한다**(`IsBuilt` — 야생도 손으로는 붙이지만 저 혼자는 안 잡는다) · 한 번 하고 없어지는 것은 `Forageable`.
- **힘은 핵이 내고 먼저 지은 것이 먼저 받는다**(`PowerGrid`/`PowerUser` — 배선은 `World → 8`).
- **배치 — 폰을 건물에 1~3순위로 붙박는다**(`WorkPosts` · 2026-09-29, **핵을 누르면** `WorkPostsUI` · 소환은 새 건물 **소환진**(`SummonCircle` → `DungeonCoreUI`, 하수인은 소환진 곁에 선다)) — 위에서부터 할 일이 있는 첫 칸을 한다 · 셋 다 없으면 **논다**(다른 일에 안 끌려간다) · 칸이 없는 폰은 예전 그대로 · 배치는 명령이라 on/off는 안 보고 `IsBlocked`만 · 일감의 건물은 `JobBase.Site` · 노드를 캐는 중에도 3초마다 위 순위를 다시 본다 · 자기 배낭 비우기는 늘 먼저. → `docs/jobs.md` 끝
- 큐 순서(`QueueOrder`)가 전역 우선순위 · 폰 설정은 on/off(`WorkSettings`, P) · **밭은 붙어 있으면 한 구역**(`FarmZones` — 4-이웃, 테두리를 그린다) · 씨앗은 심는 순간 하나 쓴다 · 거둔 것은 그 자리에 떨어진다.

### 전투 · 붙잡은 사람

**원문은 `docs/combat.md`·`docs/prisoners.md` 끝.**

- `DamageType`은 한 축(물리 셋 + 원소 일곱) · **정수로 직렬화되는 enum은 꼬리에만 붙인다**(`StatName`·`DamageType`·`JobCategory`·`BuildCategory`·`MerchantType`) · 안 맞는 것은 `CombatHandler.Measure` 한 번에 · 신성 ×2는 `IsUndead`.
- **표적이 없으면 싸우는 중이 아니다**(`CombatHandler.Update`가 스스로 끈다) · 도는 것은 `PawnMover.FaceTowards` · 능동은 장비가 준다(`AbilityUser` · 하루에 한 번 찬다) · 도발은 `DetectNode` 맨 위.
- 몸은 켄시 눈금(부위 100 · 쓰러뜨리는 것은 코어뿐) · **프리팹이 든 `[SerializeField]` 값은 코드 기본값을 고쳐도 안 바뀐다** · 치료는 `Treatment` 하나 · 기절한 폰은 아무도 안 때린다(`IsAttackable`) · **붙들려 있는 동안 기절 시계가 안 간다** · 누가 죽든 시체로 남고 지녔던 것은 `Corpse.SpillBelongings` 하나.
- 업기·옮기기·치우기는 한 동작(`PawnCarry`) · 받는 자리는 `ICarrySite` · 갇힘은 `Imprisonment`(푸는 문 `Release` 하나) · 세력을 바꾸는 길은 `UnitBrain.ChangeFaction` 하나.

### 자원 · 품질 · 상인 · 이벤트

**원문은 `docs/data.md`·`docs/merchants.md`·`docs/world.md` 끝.**

- **자원 종류는 아이템, 묶음만 enum**(`ResourceGroup` — 꼬리에 붙인다) · 노드의 범주는 `DropItem`에서 파생 · 등급은 장비만(`ItemData.HasQuality` — `TryAdd`가 누른다) · CraftSkill은 작업대 종류(갑옷 작업대가 둘인 이유는 원문).
- 지니는 것은 `LoadoutData` · **`ItemId` 오타는 조용히 아무것도 안 나온다 — 읽는 쪽은 `Resolve()`** · 값은 `Shop` 하나가 매긴다 · `BaseValue`를 안 적으면 10G.
- **이벤트** — 빈도는 `quietWeight`(아무 일도 없음도 후보) · `MinGameTime`이 난이도 곡선 · 알림 없이 건너뛸 것을 먼저 거른다 · **아무도 안 보는 습격은 추상으로 푼다**(`SimLocality.IsSimulated` · 결과는 `WorldBattle.ResolveRaid`).

### 건설

**원문은 `docs/build.md` 끝**(갈래 · 목록 · 발자국 · 청사진 · 바닥 · 휴면 · 지붕 · 레이어 · 턱과 지름길).

- **목록은 레지스트리에서 온다**(`Resources/Data/Buildings/`) · 잠긴 건물은 안 뜬다 · 값과 못 짓는 이유는 **커서 옆**.
- **건물 프리팹에는 `PlacedBuilding` + `data`** · 콜라이더 크기와 `OccupiesTile`은 다른 축 · 씬 배치는 격자에 맞춘다 · 걸어 들어가는 건물은 `BlockMask`.
- **청사진은 자재를 배치 때 안 낸다** · 철거·취소는 전액 창고로(`bp.Cancel()` 하나) · 자리에 따라 모양이 갈리면 `IPlacementPose` · 고스트에서 스크립트 떼는 순서를 지킨다.
- **바닥은 데이터다**(`RegisterFloor` + `FloorRenderer` 청크) · 경사를 안 따르는 기단 · **폰이 오르는 턱은 `PawnMover.StepUp` 하나**(길찾기도 안다) · 지름길은 몸통 두께를 안다(`LosOpen`).
- **레이어가 곧 누구를 통과하나**(`Player` 7 · `Npc` 8 · `Enemy` 9 — `Default`에 폰을 두지 말 것 · 세력이 바뀌면 `ApplyLayer`) · 지붕과 페이드는 한 벌(`BuildingRoof`) · 핵 영역 밖은 휴면.
- **건물은 부서진다**(`BuildingHealth` — 핵 제외 · 최대치는 비용에서 · 둔기가 잘 부순다 · 부서지면 75%가 바닥에 · 철거는 전액) · 내 폰은 우클릭 [부순다] · 수리는 `RepairDispatcher`(핵 영역 · 맞은 지 10초 뒤 · 비용 25%) · **적은 막힌 길을 뚫는다**(`DetectNode.Breach` — 길 포기 `PawnMover.GaveUpAt` → 우리 영역 길막이 · 적만). → `docs/build.md` 끝
- **모델 입히기는 `Building → 0. Dress All` 한 번**(표로 입히는 `PropDresser` + 제 손으로 조립하는 도구 여덟을 차례로) · **`PropDresser`는 표에서 뺀 것을 `Retired`에 적힌 것만 벗긴다**(`[Art]`는 다른 도구도 쓰는 이름이다 — *표에 없다*로 벗기다 소환진·다리 서른이 날아갔다) · 새 조립 도구는 `[Art]` 말고 제 이름을 쓸 것.
- **폰의 관절은 `PawnRig` 규격**(짓는 문은 `Pawn/0. Rebuild Pawn Rigs And Animations`) · 치비 폰 세부는 `docs/units.md` · `PawnAnimator`는 판정을 옮길 뿐 · 공격 스타일은 무기 종류에서 파생.

### 입력 · 카메라 · 명령

**원문은 `docs/ui.md` 끝**(창마다의 규약 · 명령 모드 · 땅 고르기 · 진형 · 선택).

| 키 | 창·모드 | 키 | 창·모드 |
|---|---|---|---|
| `I` 인벤토리 | `InventoryUI` | `B` 건설 / `X` 철거 · 열면 `1`·`3`이 이전·다음 갈래 | `BuildModeUI` |
| `U` 스탯 | `UnitStatsUI` | `C` 명령 · `V` 밭 구역 · 열면 숫자가 도구(`CommandMode.Order` — 취소·채굴·채집·운반·올리기·내리기·평탄화) | `CommandMode` · `FarmZoneDesignator` |
| `P` 무리 관리 | `ColonyUI` | `R` 소집 | `DraftController` |
| `L` 세력 우호도 | `FactionPanelUI` | `F1~F5` 진형 | `FormationController` |
| `O` 자동운반 품목 | `HaulPolicyPanel` | 모드 버튼 바(키 줄 순서: 명령 · 밭 · 건설) | `ModeBarUI` |
| `G` 원정 편성 | `ExpeditionUI` | `M` 지도 | `WorldMapView` |
| `1~4` 능동 능력(건설·명령·밭 구역이 켜져 있으면 양보) | `AbilityController` | 좌/우클릭 | `SelectionManager` / `MoveController` |

- **새 글자 단축키는 `InputFocus.KeysBlocked()`를 묻는다** · **씬 필드가 든 키(`kb[필드]`)까지 훑고 붙일 것**(`G`가 그래서 원정 편성 것이다).
- **땅 고르기**(`GroundLevel` · `LevelDesignator`) — 찍는 단위는 **2m 덩어리**, 일감은 **높이 정점**(땅 높이가 2m 정점으로만 있다) · 올리기·내리기는 지금 땅에서 한 단(쌓지 않는다) · 평탄화는 누른 자리 높이 · **막는 것은 기단뿐**(윗면 높이를 데이터로 든다) · 건물·청사진은 **발자국 아래 가장 높은 땅**을, 야생 노드·채집물·바닥 물건은 제 자리 땅을 따라 옮긴다(`CollectRiders`) · 선은 할 일이 남은 덩어리에만(`BlockNeedsWork`) · **폰은 제가 파는 땅 밖에 선다**(`LevelGroundJob.StandTile` — 정점 영향 ±2m 바로 바깥, 다른 표시에도 안 걸린 땅 먼저: 발밑을 같이 내려 구덩이에 갇혔다) · 거절하면 이유를 커서 옆에(`WhyNotBlock`) · 다시 굽는 자리 셋은 `GroundLevel.Refresh` 하나.
- **도구 바 칸은 목록이다**(`ModeToolBarUI.tiles` — 칸은 `UI → Setup Command Bar`가 세운다) · **모드는 한 번에 하나**(`ExclusiveMode`) · 개수가 정해진 UI는 씬에 세운다 · 창을 여는 문은 `WindowRaiser.Open` · 카메라는 `unscaledDeltaTime`.
- 선택은 고정(켄시식) · `PruneSelection`/`EnsureSelection` · UI에서 시작한 클릭은 놓을 때까지 무시 · **진형은 소집의 것**(`Formation.Current` — 끈 선이 전면 · 자리는 칸으로 센다 · 미리보기는 진형이 낸 자리 그대로) · 못 누르는 줄은 지우지 말고 흐리게.

### 개체 명세 · 특성 · 성장 배경

**원문은 `docs/units.md` 끝.** `PawnSpec`은 태어나기 전에 적은 사람이고 **모든 무작위가 시드 하나에서 나온다** — **굴림은 반드시 꼬리에 붙일 것**(중간에 끼우면 같은 시드가 다른 사람을 낳는다). 특성은 스탯에 숫자만 얹는다(기능을 특성으로 게이트하지 않는다) · 배경은 유년기·성년기 하나씩, **모든 배경은 어느 무리의 것이다**(폴백 `civilian`) · 묶음은 세력이 든다(`FactionData.BackstoryGroup` — 소환물만 `PawnSpec.SummonedStoryGroup`) · 저작한 장소의 폰은 `WorldObject.Id`에서 시드를 낸다 · 작업 금지는 `WorkSettings.SetBlocked`.

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

`python Tools/export_gamedata.py` → `Tools/GameData.xlsx` + `Tools/*.csv`(git diff용). 뽑는 쪽의 함정은 `docs/data.md` "게임 데이터 Excel".

**표는 저작물이 아니라 에셋의 그림자다** — 에셋과 코드를 직접 읽어 만든다. **값을 바꿨으면 다시 돌릴 것.** **손으로 적은 표는 반드시 뒤처진다**(적을 때만 맞고 그 뒤로는 아무도 고치지 않는다). 제작 배수가 1 미만이면 붉게 칠해진다 — **만들수록 손해인 레시피**가 가장 먼저 눈에 띈다.

## 답이 한 벌이어야 하는 것들 (자주 어긋난 자리)

같은 물음에 여러 곳이 각자 답하면 **반드시 어긋난다** — 이 저장소에서 실제로 겪은 것들이다.
새 코드를 쓸 때 여기 있는 것을 다시 만들지 말 것.

| 물음 | 유일한 답 | 어긋났던 흔적 |
|---|---|---|
| 몸이 얼마나 요구하나 | `Needs` | 허기 하나가 여섯 군데에 흩어져 있었다 — 축이 늘 때마다 그 여섯이 복사된다 |
| 지금 얼마나 보이나 | `Vision` | 교전 탐지와 도주 탐지가 따로 재면 도망만 밤에 멀쩡하다 |
| 얼마나 질 수 있나 | `CarryCapacity` | 네 군데가 따로 계산했고 둘이 **3.3배** 달랐다 |
| 재료가 어디 있나 | `MaterialPool` | 창고만 세어 손에 든 씨앗이 없는 것이 됐다 |
| **무엇을 그 재료로 쳐 주나** | `ItemData.CountsAs` → `Inventory.CountSatisfying`/`RemoveSatisfying` | 종족마다 고기를 나누면 레시피마다 재료를 종족 수만큼 적게 된다 — 레시피는 묶음(`생고기`) 하나를 적고 물건이 *그것으로 쳐 준다*를 든다. 보통 수량은 안 따른다(상인이 늑대 고기를 빼 간다) |
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
| **월드 좌표 → 칸** | `MapManager.WorldToTile` | 반올림·`Floor`로 직접 구한 자리가 **넷**이었다(바닥 등록 · 건물 발자국 · 로컬 격자 원점 · 코스 셀 범위). 격자 위상이 세계 원점에 박혀 있던 시절의 계산이라, 08-27에 위상이 옮겨 간 뒤로 **전부 한 칸씩 밀렸다** — 오두막 문간이 벽으로 등록되고 폰이 벽으로 걸어갔다 |
| **밖에서 온 것이 설 자리** | `RockMap.TryNearestOpen` | 지하로 내려온 뒤 세계의 3분의 2가 바위인데 **세우는 코드가 하나도 그걸 안 봤다** — 저작한 마을은 **벽 속에 서서 아예 안 보이고**(갈 수도 없다) · 무리는 **벽에 낀 채 경고만 찍고**(습격이 예고돼도 아무도 안 온다) · 이벤트는 *처음* 찾은 방향을 붙들어 **상인이 기지 코앞의 벽에서 튀어나왔다**. 증상이 저마다 달라 같은 원인으로 안 보인다 |
| **무엇을 요구하는 몸인가** | `Needs.BodyOf` / `Runs` | 축을 세력 하나로 정해서 **골렘이 밥을 먹고 굶어 죽었다**. `IsConstruct`를 밖에서 다시 물으면 몸이 늘 때 그 자리가 뒤처진다 |
| **무엇으로 어디서 채우나** | `Recharge` | `Feeding`의 짝. 세는 곳과 쓰는 곳이 갈리면 폰이 걸어와 빈손으로 접고 다시 온다 |
| **폰을 어느 프리팹으로 세우나** | `PawnSpawn` | 체형이 프리팹마다 따로라(관절이 몸마다 구워진다) 방랑자는 `BodyVariants`에서 성별에 맞는 몸을 골라 세운다. 세우는 자리가 여섯인데 하나라도 `Instantiate`를 직접 부르면 **그쪽에서만 남자 몸에 여자 이름**이 선다(`ApplySpec`이 어긋남을 경고한다). **성별은 몸이 답한다**(`PawnBody`) — 명세의 성별은 아직 몸이 없을 때 몸을 고르는 값이고, **스탯과 다른 열에서 뽑는다**(같은 열이면 같은 시드가 다른 사람을 낳는다) |
| **어느 스탯이 빨리 느나(열정)** | `Passions` | 열정은 **스탯 하나하나**에 붙고 **두 풀에서 따로** 뽑는다(무기·마법 / 노동 — 한 풀이면 한쪽이 다른 쪽을 굶긴다). 속성 넷과 공통 전투에는 안 붙는다(`PoolOf`가 `null`). 배율은 **올리기만**(1 · 1.5 · 2)이고 곱하는 자리는 `UnitStats.GrowStat` 하나다. **새 스탯은 `PoolOf`의 표에 적을 것** · 굴림은 **배경 다음, 난수열 꼬리** |
| 저작 도구가 보는 지형 값 | `TerrainAuthoring` | 씬에서 프로필·특징을 읽는 세 줄이 도구마다 복사돼 **넷**이 됐다 — 하나만 뒤처져도 조각한 자리가 게임에서는 딴 데다 |
| **화면을 언제 다시 그리나** | `UiRefresh`(박 0.15초 · `Due(ref _beat, 배수)`) | 창 **여덟**이 저마다 `refreshInterval`과 타이머를 들었다. 떨어진 창은 어긋나도 아무도 모르지만 **붙어 선 두 판**(`PawnSheet_Panel` ↔ `PawnHealth_Panel`)에서는 **한 창이 두 번에 나눠 바뀌는 것**으로 보였다(주기가 서로 나눠떨어져도 **시작 시각이 달라** 영영 안 맞는다 — 배수로 세면 느린 창이 도는 순간은 언제나 빠른 창도 도는 순간이다). **축도 갈려 있었다** — `deltaTime`으로 재던 둘(무리 목록·거점 창)은 **일시정지 중에 값이 얼어붙었다**(`UiTween`의 `SetUpdate(true)`·`CameraController.Dt`와 같은 사고). **켜고 끄는 것은 이 박자에 안 태운다** — 여기가 답하는 것은 *언제 다시 그리나*이지 *보이나*가 아니다(가시성을 주기에 태우면 모드를 끄고 한 박 뒤에 판이 돌아와 다른 박자로 도는 판과 어긋난다) · **주기는 씬이 아니라 코드가 든다**(갱신 주기는 저작이 아니라 규격이다) |

## 알려진 이슈

**원문은 `docs/timing.md` 끝**(배속 세부는 같은 문서 본문, 셰이더는 `docs/rendering.md`).

- **배속에서는 한 프레임이 커진다** — 시간을 깎지 말고 나눠서 돈다(걸음은 `StepMove`가 0.2m씩 · BT는 밀린 만큼 돌고 남는 것을 넘긴다 · 누적은 `UnitBlackboard.TickDelta`) · 틱 주기 0이면 조용히 멈춘다 · 두 번 돌아도 안전한 이유는 `Time.time` 마감이다.
- **조사는 `을(를)`처럼 둘 다 적고 `Josa.Fix`가 고른다** — 새 화면 출력 자리에도 걸 것.
- **런타임 `Shader.Find`는 빌드에서 잘린다 — 목록은 `RuntimeShaders` 하나**(`Hidden/`은 넣지 말 것 · 크래시 원인은 `Editor-prev.log`) · 투명하게 만드는 문은 `FadeMaterial` 하나(`Material.color`를 쓰지 말 것 — URP는 `_BaseColor`).
- **SO와 JSON을 같이 볼 것** — 레시피·아이템 감사는 `StreamingAssets/Data/`와 `Resources/Data/`를 둘 다 · 연구 비용의 순환도 잰다 · 기준은 *다른 에셋이 GUID로 가리켜야 하나*다.

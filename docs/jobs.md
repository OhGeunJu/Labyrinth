# Job System & Buildings

## Job System (`Assets/1 Script/Job/`)

`JobManager` (singleton):
- `RegisterJob(JobBase)` — 작업 등록
- `RequestJob(unit)` — 방랑자·하수인 공통 (폰의 `WorkSettings`로 필터)

Job score = `JobCategoryInfo.Weight(category) × 100 + job.Priority`. 폰이 그 카테고리를 껐으면 배정 제외.

`JobBase.Category` — virtual `JobCategory` 프로퍼티.

## 모든 Job 타입

| Job | Category | 흐름 |
|---|---|---|
| `HaulJob` | Hauling | GoToSource(**맡은 몫**만 수거) → GoToStorage(TryAdd) |
| `GroundHaulJob` | Hauling | GoToItem(DroppedItem.PickUp) → GoToStorage |
| `UnloadJob` | Unload | 배낭의 잉여를 창고로. **주인만 할 수 있다**(`CanAssignTo`) — 짐이 그 폰 배낭에 있다. **잠근 뭉치는 뺀다**(`ItemInstance.Locked`) — 아래 참조 |

### 무엇을 남길지 — 뭉치 잠금 (`ItemInstance.Locked`)

배낭 창(I키)의 **[잠금]** 을 켜고 아이템을 누르면 그 뭉치가 자동 운반에서 빠진다. 다시 누르면 풀린다.
잠긴 뭉치에는 왼쪽 위에 `잠금`이 뜬다.

**종류로 거르지 않는다.** 한때 `WeaponData`·`ArmorData`·`FoodData`를 통째로 뺐는데 그 규칙은
반드시 어긋난다 — 만들어 쌓아 둔 검은 창고로 가야 하고 쥐어 준 예비 검은 남아야 하는데
둘 다 `WeaponData`다. 같은 종류라도 어느 뭉치를 남길지는 플레이어만 안다.

잠기면 넷이 막힌다:

| 막히는 것 | 어디서 |
|---|---|
| 자동 운반 | `UnloadJob.IsReady` · `UnitBrain.UpdateUnload` |
| **재료 집계·소비** | `MaterialPool.Available` / `AvailableAnywhere` / `Consume` |
| 판매 | `MerchantUI.ExecuteSell` (버튼·더블클릭·드래그가 모두 여기로 모인다) |
| 버리기(창 밖 드롭) | `DraggableItem.TryWorldDrop` |

- **드래그로 옮기고 장착하는 건 그대로 된다.** 막는 것은 **되돌릴 수 없는 처분**과 **자동**뿐이다 —
  손으로 옮기는 것까지 막으면 풀었다 잠그기를 반복하게 된다.
- **잠긴 뭉치도 새 물건은 받는다**(`Inventory.TryAdd`). 잠금은 자리를 비워 두지 말라는 뜻이지
  봉인이 아니다 — 안 받으면 잠글 때마다 칸이 쪼개져 배낭이 금세 조각난다. 합쳐진 것은 같이 잠긴다.
- 잠금을 지나쳐 세는 통로는 `Inventory.GetUnlockedQuantity` / `RemoveUnlocked` **한 쌍**이다.
  **세는 것과 빼는 것이 같은 기준이어야 한다** — 넓게 세고 좁게 빼면 차감이 실패하고,
  좁게 세고 넓게 빼면 잠근 것이 사라진다.
- 창고 재고는 잠글 수단이 없다(잠금은 배낭 얘기다) — 창고에 든 것은 전량이 재료다.
- **잠금은 가는 곳에 따라 따라간다.** 폰 배낭 → 폰 배낭은 유지(다른 일꾼에게 맡기는 것),
  폰 배낭 → 창고·작업대·시체는 해제(공용 자리에 내놓는 것). `DraggableItem.IsPawnInventory`.
- 툴팁이 잠긴 뭉치에 `잠김 — 자동 운반·재료·판매·버리기에서 빠집니다`를 덧붙이고,
  팔려다 거절당하면 상단 알림(`EventNotificationUI.Notify`)이 뜬다.

### 등급은 집은 그대로 내려놓는다

스택은 **등급별**이라(`Inventory.TryAdd`), 뺄 때 등급을 안 가리면 희귀를 집어 일반으로 넣게
된다 — 물건이 바뀌는 셈이다. `HaulDispatcher`의 맡음 장부가 `(아이템, 등급)` 키인 것도
이 때문이다(아이템만 세면 희귀 셋 + 일반 다섯이 한 덩어리가 된다).

`Inventory`의 제거·집계는 **잠금·등급 두 축**으로 걸러진다:
`Remove` / `Remove(…, quality)` / `RemoveUnlocked` / `RemoveUnlocked(…, quality)`,
`GetTotalQuantity` / `GetUnlockedQuantity`(각각 등급 오버로드). 속은 `RemoveCore`/`CountCore` 하나다.

**짐내림 기본값은 켜짐이다.** `WorkSettings`는 플레이어 세력 일꾼에만 붙으므로
(`UnitBrain.Awake`의 `!UsesEnemyAi && !(unitData is NpcData)`) 적·NPC는 애초에 대상이 아니다.
| `CraftJob` | Crafting | OnStart 재료 즉시 소비 → 테이블 도착 → CraftTime 카운트 → DroppedItem.Spawn |
| `FuelJob` | Crafting | 창고 이동(수거) → 용광로(AddFuel) |
| `SeedJob` | Farming | 창고에서 씨앗 소비 → FarmPlot 심기 |
| `WaterJob` | Farming | (우물 페이즈 →) FarmPlot 물 주기 |
| `FarmHarvestJob` | Farming | FarmPlot 수확 → Storage 운반 |
| `ConstructionJob` | Construction | 창고 수거 → BuildingBlueprint 납품 |
| `ResearchJob` | Research | OnStart 재료 즉시 소비 → ResearchTable 카운트 → ResearchManager.Complete |

### 운반 발행 — `HaulDispatcher` (출처 하나에 폰 여럿)

**운반 일감은 출처의 재고를 몫으로 쪼갠 것이다.** 예전엔 출처마다 `bool _haulJobPending`
하나로 "일감은 동시에 하나"였고 그 한 건이 재고를 **전량** 가져갔다 — 광맥에 100개가
쌓여 있어도 나르는 폰은 언제나 한 명이었다(폰을 늘려도 나머지는 나를 것이 없었다).

`HaulDispatcher`가 `ResourceNode`·`CraftingTable` 공통으로 발행한다.

**두 숫자를 헷갈리지 않는다.**

| | 무엇 | 누가 정하나 |
|---|---|---|
| 임계값 `haulThreshold` | 이만큼 쌓이면 나른다 — 발행 **조건** | 노드마다 저작(기본 10) |
| 실어 가는 양 | 한 번에 얼마나 지고 가나 | **폰의 `CarryCapacity`** — 폰마다 다르다 |

- **몫을 미리 쪼개지 않는다.** 미배정 재고를 통째로 맡긴 일감 하나를 내고, 폰이 정해지는
  순간(`HaulJob.OnReserved` ← `JobBase.Reserve`) 그 폰이 질 만큼만 남기고 **나머지를 즉시
  놓는다** — 놓인 만큼은 바로 다음 일감이 되어 다른 폰이 붙는다. 몇 명이 붙을지를
  미리 맞힐 필요가 없다. 예전엔 등록 시점의 재고로 몫이 못 박혀서, 임계값이 10이면
  힘센 폰도 영영 10개씩만 날랐다.
- **맡은 몫은 상한이 아니다.** 맡음(`HaulJob._claim`)은 "같은 재고에 일감이 겹쳐 등록되지
  않게" 세는 장부일 뿐이다. 실어 가는 양은 **도착해서** `min(현재 재고, 폰의 적재)`로 다시 잰다.
  등록 시점의 수량을 상한으로 쓰면 임계값이 그대로 이송량이 된다 — 임계 10에 일감이 뜨면
  27개를 질 수 있는 폰도 10개씩만 날랐다(D급 근력 35 = 27.5kg인데 돌 10~13개만 옮기던 증상).
- **배낭에 실은 순간 맡음은 전부 놓는다.** 맡음이 지키는 건 "출처에 아직 있는데 내가
  가져갈 재고"뿐이다 — 이미 빠져나간 수량을 계속 맡고 있으면 그만큼이 이중으로 빠져
  남은 재고에 일감이 안 붙는다.
- 맡은 것보다 많이 실어도 된다. 다른 일감도 도착해서 다시 재므로 있는 만큼만 가져가고,
  넘친 장부는 `HaulDispatcher`가 재고를 넘는 맡음을 깎아 맞춘다.
- 폰 배낭이 차서 덜 가져간 잔량은 다시 미배정이 되어 다음 점검에서 새 일감이 된다.
- **주기 점검**(`ResourceNode.Update` 2초 / `CraftingTable.UpdateHaulOut` 2초)이 있어야
  잔량이 방치되지 않는다. 산출 시점에만 등록하면 **다 캔 노드**에는 다음 산출이 없어
  잔량이 영영 남는다(실제로 그랬다).
- `HaulJob.Release(n)`는 **맡고 있는 양을 넘겨 놓지 않는다.** 두 번 놓으면 출처의 장부가
  음수로 새고, 그러면 있지도 않은 재고에 일감이 붙는다(플래그 시절엔 멱등이라 없던 함정).
- `ResourceNode.haulThreshold` (기본 10) — 이만큼 쌓여야 일감이 뜬다. **한 번에 몇 개나
  실려 가는지는 여기가 아니라 폰의 적재 한도가 정한다**(아래). 작업대 산출물은 임계 1.
- `UnitBrain.Die()`에서 `CurrentJob` 자동 취소

### 한 번에 얼마나 지나 — `CarryCapacity`

```
한도(kg) = 10 + 근력 × 0.5      // 근력 10 → 15kg, 130 → 75kg
```

**적재 한도의 단일 출처.** 전에는 같은 것을 네 군데가 따로 계산했고 그중 둘이 어긋나 있었다
(`PawnMover`·`CombatHandler`·`ExpeditionParty` = `근력 × 5` / 옛 `PartyCarry` = `10 + 근력 × 0.5`).
`PartyCarry`는 이때 삭제했다 — 남겨 두면 세 번째 답이 된다.
근력 × 5는 130에서 650kg이라 무게가 사실상 아무것도 막지 못한다 — 폰 하나가 광맥을 통째로
비우고 온다. 무게를 두는 이유가 "한 번에 다 못 가져간다"인데 한도가 그만큼 크면 넣으나 마나다.

- **한도는 자동 운반에만 강제된다.** `HaulJob`/`GroundHaulJob`이 `RoomFor(unit, item)`만큼만
  싣고, `CanAssignTo`가 짐이 찬 폰에게는 일감을 주지 않는다(헛걸음 방지). 못 실은 잔량은
  출처에 남아 다음 발행에서 새 몫이 되고, 땅 아이템은 그 자리에 다시 떨어져 새 일감이 걸린다.
- **손으로 넣는 건 안 막는다.** 넘긴 비율만큼 이동·공격 속도가 준다. 거부하면 "왜 안 들어가지",
  자동이 무시하면 "왜 이 폰만 느리지"가 된다 — 둘 다 설명할 수 없는 화면이다.
- `ItemData.Weight`는 이미 전부 저작돼 있다(씨앗 0.05 ~ 철갑옷 8). 무게 0인 물건은 무게로
  막지 않는다 — 배낭 격자가 막는다.
- **짐을 지고 걸으면 근력이 자란다**(`PawnMover.GrowStrengthFromLoad`). 한도 대비 적재 비율로
  재므로 근력이 커질수록 같은 짐으로는 덜 자란다. 25% 미만이면 안 오른다(빈손 배회로 크는 것 방지).
- 원정대 등짐도 같은 한도를 쓰고 **무게로 센다**(`ExpeditionParty.CarriedWeight`). 예전엔
  kg으로 만든 수를 개수 한도로 써서 씨앗이나 철갑옷이나 한 자리를 먹었다.

**WorkNode 작업 반경:** 2.0f (콜라이더에 끼어도 2.0f 이내면 실행). Logging은 `Stats.Cutting`, 그 외는 `Stats.Mining` 성장.

## 작업 배정 모델 (on/off + 전역 고정 순서)

작업은 두 갈래다.

| 갈래 | 카테고리 | 배정 방식 |
|---|---|---|
| **큐 작업** | Construction, Farming, Crafting, Research, Hauling | 건물/농지/작업대가 `JobManager`에 일감 등록 → 폰이 알아서 집어감 |
| **직접 배정** | Mining, Logging (자원 노드), Guard (순찰 구역) | 플레이어가 대상에 폰을 직접 붙임 (`Blackboard.AssignedNode` / `AssignedPatrolZone`) |

**배정 인원 제한** — `ResourceNodeData.MaxWorkers`(0이면 무제한). `ResourceNode.Assign`이
**성공 여부를 돌려준다** — 조용히 실패하면 플레이어는 명령이 먹은 줄 알고 폰이 안 움직이는
것만 본다. 여럿을 한 번에 배정하면 자리만큼만 받고 나머지는 로그로 말한다.
자리 확인을 **이전 노드에서 떼기 전에** 한다 — 순서가 반대면 꽉 찬 노드로 보내려다 실패한
폰이 원래 캐던 곳에서도 떨어져 나와 아무 데도 안 붙는다.
표시는 배정 버튼(`배정 2/3`, 꽉 차면 비활성) · 우클릭 선택지(`여기서 캔다 (2/3)`) ·
무리 관리 패널(`채굴: 석탄 2/3`). 스타터 값은 크기에 맞춰 광맥 3 · 보석 2 · 돌·나무 1.

> **직접 배정은 큐 작업을 통째로 밀어낸다.** `WorkNode.Tick`은 `AssignedNode`가 있으면
> `TickAssigned`로 끝나고 그건 **늘 `Running`** 이다 — 배정된 폰은 `RequestJob`을 아예
> 호출하지 않는다(순찰도 트리에서 작업보다 위라 마찬가지). 즉 **채굴꾼은 운반하지 않는다.**
> 광맥마다 폰을 붙여 두면 나르는 건 배정 안 된 폰뿐이라 **운반 전담을 따로 남겨야 한다.**
> 의도한 설계지만 화면에서는 안 보였다 — 배정된 폰의 P키 체크가 멀쩡히 켜져 있어서
> "운반 켠 폰이 다섯인데 왜 하나만 나르지"가 된다. `ColonyRowUI`가 그 체크를
> **흐리게** 그려 구분한다(2026-08-03).

`JobCategoryInfo` — 이 구분과 표시명의 **단일 출처**.
- `QueueOrder[]` — 큐를 타는 카테고리. **배열 순서 = 전역 우선순위**(앞이 먼저), 모든 폰 공통.
- `Weight(category)` — 점수 가중치. 큐 밖 카테고리는 0 = 큐에서 배정되지 않음.
- `IsQueued` / `DirectAssign[]` / `DisplayName(category)`
- 새 Job 클래스의 카테고리를 `QueueOrder`에 넣지 않으면 **그 일감은 아무도 집지 않는다.**

`WorkSettings` (폰 컴포넌트, `UnitBrain.Work`):
- `IsEnabled(category)` / `SetEnabled` / `Toggle` — 폰별 **한다/안 한다**만. 숫자 우선순위 없음(폰마다 순서를 다르게 줄 이유가 없고, 그리드 관리 비용만 커진다).
- `UnitBrain.Awake`가 플레이어 세력 일꾼(방랑자·하수인)에만 자동 부착. 기본값 전부 켬.
- 하수인은 `MinionData.AllowedJobs`를 **초기값**으로만 사용 → 이후 조정은 개체별(같은 SO를 공유하는 다른 하수인에 영향 없음).
- **`AllowedJobs`에는 큐 작업만 적는다.** 채굴·벌목·경비는 직접 배정이라 켜고 끌 대상이 아니다.
  큐 작업이 하나도 없는 목록은 큐에 대해 아무 말도 하지 않은 것이므로 **빈 목록과 같게(전부 허용)** 보고 경고한다.
  예전엔 `[채굴, 벌목]`만 적힌 `Goblin`이 "목록이 비지 않았다"는 이유로 **큐 작업을 전부 잃었다** —
  하수인이 운반·건설·제작을 아예 안 하고 방랑자 혼자 나르는 증상으로 나타났다(2026-08-03).

UI: `ColonyUI` — P키 토글 (`docs/ui.md`).

## Resource & Storage System

**`ResourceNode`:**
- `Awake`에서 타일을 Impassable로 설정, `OnDestroy`에서 복구
- `DropYield()` → 자신 `Inventory`에 아이템 추가 → 가장 가까운 호환 Storage에 HaulJob 등록
- `haulThreshold` — NodeInfoPanel에서 +5/-5 버튼으로 조절

**`Storage`** (씬 오브젝트):
- `BuildingName` — 표시명
- 필터: **묶음**(`ResourceGroup`)과 **아이템 콕 집기**를 따로 둔다. 둘 다 비면 전부 허용.
- API: `AddFilter(ResourceGroup)` / `AddItemFilter(ItemData)` / `AllowsEverything` / `CanAccept(ItemData)` / `HasRoomFor(item, amount, quality)`
- `TryAdd(item, amount)` — 필터 통과 시만 입고
- `Init(BuildingData)` — 빌드 모드 배치 시 초기화
- `static OnSelected` — 좌클릭 시 이벤트 → `StorageUI`가 구독
- `static OnChanged` — 재고 변경 시 이벤트 → `BuildingBlueprint`가 구독하여 건설 작업 자동 등록

## Crafting System

**`CraftingTable`** (씬 오브젝트):
- `tableType: CraftSkill` — 테이블 종류
- `Recipes` — `DataManager.GetRecipesBySkill(tableType)`로 자동 조회
- `QueueOrder(recipe)` → `JobManager.RegisterJob(new CraftJob(...))`
- `CanCraft(recipe)` — `Storage.GetTotalByItem` 기준 재료 충분 여부

**`CraftSkill` enum:** Labor, WeaponCrafting, ArmorCrafting, Cooking, Alchemy, Research, Smelting, Stonecutting, Woodworking, Gemcutting  
`CraftSkillExt.DisplayName()` — "무기 제작대" / "방어구 제작대" 등

**`RecipeData`** (ScriptableObject):
- `RecipeName`, `Ingredients: List<{ItemData, int}>`, `Result: ItemData`, `ResultAmount`, `CraftTime`
- `Category: CraftSkill`, `SkillGrowthAmount`, `RequiredResearch` (null이면 기본 개방)

**CraftJob 흐름:**
1. `OnStart`: 모든 Storage에서 재료 즉시 소비
2. `Execute`: 테이블 도착 → `CraftTime` 카운트다운
3. 완료: `DroppedItem.Spawn`으로 테이블에 드롭 → 자동 HaulJob 등록

## Fuel System (용광로)

`CraftingTable` 연료 필드 (인스펙터에서 용광로에만 체크):
- `_requiresFuel: bool`
- `_fuelBurnRate: float` — 초당 소모량 (기본 2)
- `_fuelCapacity: float` — 최대 연료량 (기본 100)
- `_acceptedFuels: List<FuelEntry>` — `{Item, FuelValue}` 쌍
- 연료 30% 이하 → `FuelJob` 자동 등록 (3초 간격 체크)
- `HasFuel` — CraftJob이 연료 없으면 대기

`FuelJob`: Phase1 창고 수거 → Phase2 용광로 `AddFuel()`

## Processing Buildings (제련/가공)

| 건물 | CraftSkill | 변환 |
|---|---|---|
| 용광로 | Smelting | 광석 → 주괴 (연료 필요) |
| 석재 가공대 | Stonecutting | Stone → CutStone |
| 제재소 | Woodworking | Wood → Lumber |
| 보석 연마대 | Gemcutting | 보석 + 촉매석 → 마법 결정 12종 |

**마법 결정 조합 (보석 연마대):**
- 원소 보석(Ruby/Sapphire/Topaz/Emerald) + MagicStone → Elemental 결정
- 원소 보석 + Etherstone → Holy 결정
- 원소 보석 + Voidstone → Dark 결정

## Farming System

**`FarmPlot`** (씬 오브젝트):
- 상태: `Empty → Planted → ReadyToHarvest`
- `Moisture` — 시간에 따라 감소, 0이면 성장 멈춤
- 2초 간격으로 필요한 Job 자동 등록 (`OnSelected` 클릭 이벤트)

**`WaterSource`** (씬 오브젝트):
- 물 무한 공급처
- `WaterSource.FindNearest(position)` 정적 유틸

**`WaterJob`:** `MinionData.CanProvideWater = true`이면 우물 페이즈 스킵

**`SeedData : ItemData`:**
- `HarvestItem`, `HarvestAmount`, `GrowthTime`, `MoistureDecay`, `WaterFillAmount`
- `MaxStack = 99`

`FarmUI` — 씨앗 선택 버튼 + 상태/성장/수분 표시. ESC로 닫기.

## Blueprint System (건설)

**`BuildingBlueprint`:**
- 반투명 파란색 고스트 시각. 자재 납품 완료 시 실제 건물 스폰 후 자폭
- `BuildingBlueprint.All` 정적 목록으로 중복 배치 방지
- `Storage.OnChanged` 구독 → 창고에 자재 들어올 때 자동으로 ConstructionJob 등록

**`ConstructionJob`** (Category: Construction):
- Phase 1: 창고 수거 → Phase 2: Blueprint에 납품

`BuildingData.UsesBlueprint=true`이면 Blueprint 2단계, `false`이면 즉시 스폰 후 컴포넌트 Init.

## Dungeon Core & SubCore

**`DungeonCore`** (singleton):
- `Summon(MinionData, position)` — 캡 확인 → `Storage.Consume` → 프리팹 스폰
- `CurrentMinionCount` / `TotalMinionCap` (BaseMinionCap + 서브 코어 합산)
- `IsWithinAnyRange(Vector2Int)` / `IsWithinAnyRangeWorld(Vector3)` — 빌드 가능 범위 판단
- `RegisterSubCore` / `UnregisterSubCore`

**`SubCore`** (빌드 모드 설치):
- 인스펙터 SerializeField: `Radius`, `MinionCapBonus`, `MoveSpeedModifier`, `DamagePerSecond`
- `Start`에서 `DungeonCore.RegisterSubCore`, `OnDestroy`에서 해제
- 범위 효과 (0.3초 간격 `Physics.OverlapSphere`):
  - 우호 유닛: `MoveSpeedModifier > 0` → Hasted / `< 0` → Slowed
  - 적 유닛: `DamagePerSecond > 0` → 매 틱 피해

## Research System

**`ResearchData`** (ScriptableObject): 선행 연구, 재료 목록, 시간, 해금 레시피 목록

**`ResearchTable`** (씬 오브젝트):
- `availableResearch: List<ResearchData>`
- `QueueResearch(data)` / `CanResearch(data)` (재료 충분 여부)

**`ResearchJob`:**
- `OnStart`: 재료 즉시 소비
- `Execute`: `ResearchTime` 카운트다운 → `ResearchManager.Complete(data)` + `Stats.Research` 성장

**`ResearchManager`**: `IsUnlocked(recipe)` / `CanResearch(data)` / `Complete(data)`

`RecipeData.RequiredResearch` — null이면 기본 개방, 지정 시 연구 완료 후 해금.

---

## 시체를 치운다 — 도축과 도축대

(CLAUDE.md의 `기절한 폰은 아무도 안 때린다`에서 옮겨 왔다 — 규칙 요약은 그쪽에, 세부는 여기.)

- **도축**(`ButcherJob` + `Butchering`) — 시체를 갈라 쓸 것을 꺼내고 **그 자리를 치운다**. 지금은 이것이 유일한 치우는 길이다.
  - **몸에서 나오는 것은 종족이 든다**(`UnitData.ButcherYield`/`BoneYield`), **지니던 것은 `LoadoutData.Loot`**. 한때 늑대 가죽도 `Loot`에 있었는데(*"둘 다 죽으면 나오는 아이템이라 답이 두 벌이면 어긋난다"*), 도축이 생기며 **시점이 갈렸다** — **지니던 것은 태어날 때부터 배낭에 있어 죽으면 그 자리에 남고, 몸은 갈라야 나온다.** 그래서 늑대·곰의 가죽·고기를 `Loot`에서 종족으로 옮겼고, **짐승의 주머니는 비운다**(곰이 약초를 지니고 있었다).
  - 단계 규칙은 `Butchering` 하나가 답한다: 신선=전부, 썩음=**음식만 빠진다**(`is FoodData` — 종족마다 "이건 고기다"를 적게 하면 저작 실수로 썩은 고기가 나온다), 뼈=뼈만. 뼈는 삭아도 남으므로 **단계를 안 가린다**.
  - **종족마다 다른 것이 나오되, 쓰임이 갈리는 것만 나눈다.** `늑대 고기`·`곰 고기`·`고블린 고기`를 영양도 값도 같게 셋으로 두면 그건 개성이 아니라 **창고 조각화**다(스택이 갈리고 요리 레시피마다 재료를 종족 수만큼 적게 된다). 실제로 갈리는 것은 셋이다:

    | 종족 | 나오는 것 | 왜 갈리나 |
    |---|---|---|
    | 늑대 | **생가죽** + 생고기 + 뼈 | 기준 |
    | 곰 | **두꺼운 생가죽** + 생고기(더) + 뼈(더) | 무두질하면 **질긴 가죽** → 강철 절반 무게로 그 절반보다 조금 더 막는 갑옷. **곰을 사냥할 이유**가 여기서 생긴다 |
    | 고블린 | **질긴 고기** + 뼈 | 영양 11(생고기 25)에 먹는 시간도 길다 — **요리가 구제한다**(질긴 고기 3 + 채소 2 → 스튜, 보통은 고기 1). 습격이 식량이 된다 |
    | 사람 | 생가죽 · 생고기 · 뼈 | **늑대와 같은 물건이 나온다** — 쓰임이 안 갈리니 나눌 이유도 없다(사람 고기를 따로 두면 창고만 조각난다). 늑대보다 살이 많다 |
    | 해골 | 뼈 | 이미 뼈다 |

  - **고기는 종족마다 이름이 따로다**(2026-09-29, 사용자: *"이름이 다 따로 늑대 고기 곰 고기 인간 고기"*) — 위 표의 "생고기"는 이제 `늑대 고기`(영양 25 · 8G) · `곰 고기`(34 · 먹는 시간 3.2 · 12G) · `인간 고기`(25 · 2G) · `개미 고기`(14 · 0.4kg · 5G)다. **창고 조각화는 레시피 쪽에서 막는다**: 고기마다 `countsAs: Food_RawMeat`를 들고, 요리 레시피는 여전히 `생고기` 하나를 적는다 — 재료를 세고 빼는 자리(`Storage.GetTotalByItem`/`ConsumeByItem` · `MaterialPool`)가 `Inventory.CountSatisfying`/`RemoveSatisfying`로 **그것으로 쳐 주는 물건까지** 센다(제 물건 먼저 뺀다). 보통 수량(`GetTotalQuantity`)은 안 건드린다 — 상인이 `생고기`를 사 가며 늑대 고기를 빼 가면 안 된다. 레시피 줄에는 `생고기(종류 무관)`으로 뜬다(`ItemData.IngredientName`). `생고기` 자체는 상인이 파는 출처 모를 고기로 남는다. 질긴 고기(고블린)는 **쳐 주지 않는다** — 스튜에 세 배가 드는 것이 그 값이다. **되돌릴 때는 뺀 것 그대로다** — `MaterialPool.Consume`이 실제로 뺀 물건을 `taken`에 적고 제작·연구·건설 배치가 그 목록으로 `Refund`한다(레시피가 적은 이름으로 돌려주면 늑대 고기를 넣고 취소했는데 `생고기`가 돌아왔다).
  - **도축은 손질하지 않은 것을 내놓는다** — 짐승에게서 나오는 것은 `가죽`이 아니라 **`생가죽`**이고, 무두질(`craft_leather`)을 거쳐야 방어구 재료가 된다. 전에는 무두질이 **생고기 2 → 가죽 1**이었는데(도축이 없어 받을 것이 고기밖에 없었다) 고기가 가죽이 되는 것은 말이 안 되고, 무엇보다 **가죽이 나오는 경로가 둘**이 되어 무두질에 할 일이 없었다.
- **뼈가 하수인이 된다 — 고리가 닫히는 자리.** 도축이 내놓는 뼈(`Res_Bone`)가 ① **소환 비용**(고블린 12 · 해골 25 — 던전 핵이 뼈로 하수인을 빚는다. 예전엔 돌 10이었는데, 그러면 도축이 내놓는 것과 핵이 먹는 것이 남남이다) ② **연구 재료**(`Res_Bonecraft` 골각 세공 — 뼈 30) ③ **무기 재료**(`wpn_bone_club` 뼈 몽둥이 — 값싼 둔기라 **때려눕혀 붙잡는** 데 쓴다)로 흘러간다. 그래서 **시체를 치우는 것이 잡일이 아니라 생산 라인의 앞단**이 된다: 치우면 뼈가 나오고, 그 뼈로 하수인을 부르고, 그 하수인이 죽으면 또 뼈를 남긴다.
  - **`MinionData.RequiredResearch`** — 연구를 마쳐야 소환 목록에 뜬다(`RecipeData.RequiredResearch`와 같은 규약, 비우면 처음부터 열림). 잠긴 것은 **목록에서 빠진다**(회색으로 안 남긴다 — 소환 메뉴는 "지금 부를 수 있는 것"의 목록이고, 못 부르는 이유를 그 좁은 줄에 적기 시작하면 건설 목록과 규칙이 갈린다). 연구가 끝나면 **열려 있는 창도 다시 그린다**(`ResearchManager.OnResearchCompleted`).
  - **해골은 종족 에셋 하나를 적과 나눠 쓴다** — `Skeleton.asset`이 `MinionData`가 됐고 `Enemy_Skeleton`·`Minion_Skeleton`이 그것을 같이 가리킨다. 갈리는 것은 **프리팹의 세력**뿐이다(고블린이 이미 그 모양이다 — *"`is MinionData`는 '하수인 종족'이지 '내 편'이 아니다"*가 이 배치의 이름이다).
  - **뼈는 SO다**(`Resources/Data/Items/Res_Bone.asset`, `Id: res_bone`). JSON으로 뒀다가 옮겼다 — **연구 재료와 소환 비용은 에셋 참조**라 런타임 생성물인 JSON 아이템을 가리킬 방법이 없다(CLAUDE.md "SO냐 JSON이냐"의 기준 그대로). `Id`를 채워 뒀으므로 `LootEntry.ItemId`로 문자열 참조하던 종족 넷은 그대로 산다.
- **도축대**(`ButcherTable` + `ButcherBill`) — **규칙을 걸어 두면 시체가 알아서 치워진다.** 싸움이 끝나고 시체가 열 구 널브러지면 우클릭을 열 번 눌러야 했다.
  - **산출물은 통에, 유품은 옆 바닥에.** 도축이 내놓는 것과 죽은 자가 지녔던 것은 다른 축이라(`Corpse.SpillBelongings`) 자리도 가른다 — 몸을 상판에 눕혀 두므로 그 자리에 떨구면 **동전과 장비가 도마 위에 쌓인다**. 유품·넘친 산출물 모두 도축대 **옆 작업 자리**(`ApproachPosition`)로 간다.
  - **상판에서 잡고 상판에서 가른다 — 산 것이든 시체든**(`LayOnTable`, 2026-08-16). 한때 시체만 올렸는데(*"바닥에서 잡고, 상판에 올려 가른다"*) 그러면 **죽이는 7초 동안 몸이 폰 발밑에 누워 있어** 도축대가 옆에 선 가구가 된다 — 같은 일감의 두 단계가 자리를 달리할 이유가 없다. 그때 갈랐던 까닭 둘은 각자 제자리에서 풀렸다:
    - **`PawnMover`가 도로 끌어내린다** → **`SetSleeping`으로 붙든다**(침대·우리가 쓰는 그것). 기절은 `IsSleeping`이 아니라 **회전만 눕힌 상태**라 `_cc`가 켜진 채고, `PawnMover.Update`가 매 프레임 중력을 먹여 상판에서 떨어뜨린다 — 그게 원인이었다. 재우면 컨트롤러가 꺼져 `Update`를 통째로 건너뛰고, 덤으로 눕는 동안의 클릭 캡슐(`_lyingPick`)이 켜져 **상판 위의 몸을 커서로 집을 수 있다**. **눕히는 보정은 `SetSleeping`이 제 몫으로 이미 한다** — 밖에서 또 빼면 **두 번 빠진다**(그래서 산 몸에는 앵커를 그대로 넘기고, 시체 쪽만 `CenterOffset`의 수평 성분을 직접 뺀다).
    - **죽을 때 전리품이 도마 위에 쌓인다** → **`UnitBrain.DeathDropPosition`**. 떨구는 자리를 아는 것은 죽는 몸이 아니라 **눕혀 놓은 쪽**이라, 몸에 "도축대인가"를 묻게 하는 대신 자리를 넣어 주는 칸을 뒀다(`ButcherJob`이 죽이기 직전 `ApproachPosition`을 넣는다 — `SpillBelongings`가 유품을 떨구는 **그 자리**다). 비우면 예전처럼 발밑이다.
  - **요리는 한 구를 마칠 때 한 번 는다**(`3 × Sol`). 틱마다 주면 29초짜리 작업이 한 구에 **+29**가 된다 — 다른 일감은 전부 완료 시 한 번이다(제작 0.2~0.7 · 치료 6 · 설득 4).
  - **멈추면 하던 것도 멈춘다** — 규칙만 끄고 진행 중인 손질을 두면 스위치가 거짓말이 된다(운반 지정을 끄면 가던 폰도 돌려세우는 것과 같은 판단).
  - 하는 일은 셋: 규칙에 맞는 시체를 찾아 **일감을 하나** 걸고(`SiteJobSlot` — 자리가 일감 하나를 든다), 폰이 그것을 **업어 와서**(`ButcherJob`의 두 단계 — `ConstructionJob`이 자재를 챙긴 뒤 청사진으로 돌아서는 것과 같은 모양) 여기서 가르고, 나온 것이 **여기 쌓여**(`Inventory` + `HaulDispatcher`) 창고로 실려 간다. 산출물 순서는 **도축대 → 폰 배낭 → 바닥**(작업대와 같다).
  - **시체를 담아 두지 않는다.** 도축대는 일하는 자리지 시체를 얹어 두는 통이 아니다 — 담아 두게 하면 "올려놨는데 아무도 안 가른다"는 상태가 하나 더 생기고 그걸 비우는 길을 또 만들어야 한다. `ICarrySite`가 아닌 이유다.
  - **규칙은 도축대가 하나 든다.** 림월드에서 주문이 여럿인 것은 한 작업대가 **여러 레시피**를 맡기 때문인데, 여기 도축대는 하는 일이 하나뿐이라 규칙이 여럿이면 갈리는 것은 조건뿐이고 그 둘은 곧바로 겨룬다(같은 시체가 둘에 걸리면 어느 쪽 횟수가 줄어드나). **규칙이 더 필요하면 도축대를 더 짓는다** — 우리·침대가 자리마다 제 몫을 갖는 것과 같고, 목록 UI도 필요 없다.
  - 손잡이 셋: **대상**(`적대 세력만` / `적대 + 중립 (아군 제외)` / `적대 + 중립 + 내 폰` — **관계를 한 칸씩 넓힌다**. 글자가 무엇을 포함하는지 말해야 한다 — *"내 편만 빼고"*로는 **동맹이 어느 쪽인지 알 수 없어** 값을 치르고 부른 용병 시신까지 갈린다. 세력으로 가르는 것은 종족에 "짐승인가" 칸을 새로 파면 저작할 것이 늘고, 정작 사람이 신경 쓰는 선은 관계이기 때문이다) · **상태**(신선한 것만 / 뼈만 남은 것 빼고 / 전부 — 뼈만 남은 것을 업어 오는 건 뼈 몇 개에 폰을 왕복시키는 일이다) · **반복**(무한 / N구). 적대인지는 `FactionRelations.StanceBetween`으로 **실제 전투 판정과 같은 길**을 쓴다(시체는 `Faction` 컴포넌트가 지워져 있어 지도 위 무리처럼 **데이터로** 묻는다).
  - **창을 새로 안 짓는다** — 우클릭 창이 이미 목록 그릇이고(흥정 상대 고르기와 같은 판단) 값이 몇 가지 안 되므로 **누르면 다음 값으로 돌아간다**. 쌓인 것을 보는 것은 **시체를 뒤지는 창을 그대로 쓴다**(`CorpseInventoryUI.ShowContainer` — 장비 열만 끈다): 하는 일이 글자 그대로 같다(저쪽 통을 띄우고 내 배낭을 열고 사이에서 옮긴다).
  - **손질하는 동안에만 고기가 걸린다**(`hangingMeat` 묶음을 `ButcherTable.Cutting`이 켜고 끈다 — `ButcherJob.AtTable`이 그 기준이다). 갈고리는 늘 있고 **거기 걸린 것이 지금 일하고 있다는 표시**다(연구대 게이지와 같은 자리). 늘 걸어 두면 장식이고, 아예 없으면 서 있는 모습만으로 무엇을 하는 물건인지 안 보인다.
- **조작하는 건물에는 제 창이 있다** — `GraveUI` · `PyreUI` · `ButcherTableUI`(좌클릭 → `OnSelected`). **`InspectPanel`은 정보만 든다**: *고른 것을 보는 자리*이지 조작하는 자리가 아니고, 거기 있는 [이동한다]·[분해한다]는 **건물 자체**를 다루는 것이지 그 건물로 무엇을 하는 일이 아니다. **침대에 창이 없는 것은 예외가 아니다** — 배정이 곧 "누워라"라 조작이랄 게 없어서고(2026-08-12에 `BedUI`를 없앤 이유는 규칙이 두 벌이어서였다), 무덤·화장터·도축대는 실제로 손볼 것이 있다(파내기 · 내리기 · 규칙 셋 · 산출물).
  - **우클릭에 남는 것은 *업고 있는 폰에게 내리는 지시*뿐이다** — `[업은 시체를 여기 묻는다]`·`[여기서 태운다]`(침대의 `[여기 눕힌다]`와 같은 자리). 파내기·내리기·규칙 손보기는 **전부 제 창으로 갔다**: 남겨 두면 조작이 두 자리가 되고, 그게 `BedUI`를 없앨 때 겪은 그것이다.
  - 창은 **`Assets/Editor/CorpseFacilityUICreator.cs`가 씬에 짓는다**(`DungeonColony → Create Corpse Facility UI`). 다 쓰면 지운다 — 산출물인 씬이 정본이고 이후 수정은 인스펙터에서 한다(`docs/editor_creators.md` "생성기의 수명").
  - **일하는 자리는 도축대 하나다 — 갈리는 것은 *누가 시켰나*뿐이다.** 우클릭 `[도축한다]`(사람이 콕 집어 시킨다, 작업 설정을 안 본다)와 도축대 규칙(`ButcherBill` — 조건에 맞는 시체를 저 혼자 찾는다). **둘 다 가장 가까운 도축대로 업어 간다**(`ButcherTable.NearestTo`) — 도축대가 **아직 없을 때만** 그 자리에서 가른다(가죽이 첫 방어구 재료라 짓기 전에도 손질할 수 있어야 한다). **한 몸에 일감은 하나**다(`JobManager.HasButcherJobFor`/`FindButcherJobFor`) — 문이 둘이라 같은 시체에 각자 걸면 두 폰이 같은 곳으로 걸어간다. 우클릭은 있던 일감을 그 폰에게 **넘기고**, 하고 있으면 안 뺏는다(`[치료한다]`와 같은 규약). **나올 것이 없으면 줄을 흐리게 남긴다**. **문구는 산 것에도 `[도축한다]` 하나다** — 상태마다 낱말을 나누면 그것이 별개의 동작인 줄 알게 된다(`[공격한다]`가 쓰러진 놈에게도 같은 낱말인 것과 같은 규칙). 되돌릴 수 없다는 것은 **일이 시작되는 순간**이 말해 준다.
  - **도축대 우클릭에는 몸을 올리고 내리는 줄만 있다** — `[X을(를) 여기서 손질한다]`(업고 있을 때)와 `[X을(를) 내린다]`(상판에 남아 있을 때). 침대의 `[업은 사람을 여기 눕힌다]`와 **같은 축**이라 조작 화면에 넣을 성질이 아니다(창을 열려고 짐을 내려놓을 수는 없다). **내리는 줄이 없으면 상판 위의 몸을 치울 길이 아예 없다** — 도축대 콜라이더가 상판을 감싸고 있어 그 위에 누운 몸은 커서로 안 집힌다(화장터의 `[내린다]`와 같은 자리).
  - **규칙은 시체만 고른다 — 쓰러진 것은 손으로 시킬 때만.** 자동으로 가르기 시작하면 **붙잡는 일이 성립하지 않는다**: 적을 때려눕히는 것은 회유·몸값을 노리는 행동인데 그 사이에 폰이 알아서 갈라 버리면 포로를 만들 길이 사라진다.
  - **죽이는 것도 일이다**(`SlaughterSeconds` 7초 → `BaseSeconds` 22초, 림월드의 도살). 올려놓자마자 죽어 버리면 그건 작업이 아니라 판정이고 **무를 시간이 0**이 된다. 죽이기를 손질 **뒤로** 미루는 것도 안 된다 — 기절은 저절로 풀리므로(`UpdateKnockOutRecovery`) *가르는 도중에 깨어난다*. 그래서 **먼저 죽이고 그다음 가른다**, 둘 다 시간을 들여서. 죽이는 동안에도 깨어나면 그만두고 내려놓는다(`CanButcherAlive`가 매 틱 돈다).
  - **6초는 너무 빨랐다** — 몸 하나가 눈 깜짝할 사이에 사라져 도축이 *일*로 안 보였다. 제작 시간(6~22초)과 같은 눈금에 뒀다.
  - **진행은 게이지로 보인다**(`ButcherTableUI` — 다른 작업대와 같은 눈금). 죽이는 중은 더 짙은 색에 글자도 갈린다. **일감이 도축대에 스스로 알린다**(`SetWork`) — `_slot.Current`만 보면 **손으로 시킨 도축을 못 본다**(우클릭 일감은 규칙의 자리를 안 거친다), 그러면 직접 시켰을 때 고기도 안 걸리고 게이지도 안 돈다.
  - **일하는 내내 몸은 상판 위에 눕는다 — 죽이는 동안에도**(`ButcherTable.LayOnTable`, 위). 발밑에 두면 폰이 바닥에 쭈그려 앉아 가르는 그림이 되고 도축대는 옆에 선 가구가 된다. **자리가 안 잡히면 먼저 `tableAnchor`를 볼 것** — 나중에 넣은 필드라 그 전에 지은 도축대 인스턴스에는 비어 있을 수 있고, 비면 `transform.position`(도축대 한가운데·바닥 높이)에 눕는다.
  - **`JobCategory.Butcher`가 큐를 탄다**(제작 뒤 · 운반 앞 — 시체는 삭으므로 늦으면 나올 것이 줄고, 사람이 걸어 둔 제작 주문보다는 덜 급하다). 한동안 안 태웠던 이유는 *"자동이면 시키지도 않았는데 동료 시신을 손질한다"*였는데, **그 걱정을 없앤 것은 자동화를 막는 것이 아니라 규칙이다**(`ButcherBill`) — 무엇을 가를지 사람이 미리 정해 두면 폰이 알아서 집어가도 동의 없이 벌어지는 일이 아니다.
  - 산출물은 **발밑에 떨군다** — 배낭은 찬 폰에게서 증발하고 창고 직행은 "어디로 갔지"가 된다. 바닥에 두면 이미 있는 축(운반 지정 H)에 올라탄다. **시체 배낭에 남은 유품도 같이 떨군다**(안 뒤지고 도축했다고 사라지면 조용한 손실이다).
  - **산 채로 쓰러진 것도 가른다**(`[산 채로 도축한다]`) — 기절은 회복 가능한 상태라 두면 일어나므로 *"때려눕혔다"*는 아직 잡은 것이 아니고, 죽여 놓고 시체가 삭기를 기다릴 이유도 없다(**가장 신선할 때** 가르는 길). **일감은 하나다** — 대상을 `GameObject`로 받고 마무리에서 산 것이면 `Die()`를 거친다. 규칙을 둘로 나누면 세력 우호도·자리 비우기·업힌 사람 내려놓기 중 하나를 반드시 빠뜨린다. **판정은 `Butchering.CanButcherAlive` 하나**(쓰러졌을 것 · 내 편이 아닐 것) — 우클릭 메뉴와 일감이 같이 물어서, 갈리면 "메뉴엔 떴는데 가서는 안 한다"가 된다. **가는 사이에 깨어나면 그만둔다**: 멀쩡한 것을 덮치는 것은 명령이 아니라 살인이고, 시켜 놓고 자리를 비운 사이에 그렇게 되면 안 된다. 문구도 가른다(`[도축한다]` ↔ `[산 채로 도축한다]`) — 되돌릴 수 없는 일은 글자로 그렇게 말해야 한다.

---

# CLAUDE.md에서 내려온 상세 (2026-08-27)

`CLAUDE.md`의 "일감 · 창고 · 적재 · 작업 자리"에 요약과 단일 출처 이름이 남아 있다.

## 작업 자리 (`WorkSpot`) — 도착 판정은 두 갈래

**"거기 서서 하는 일이냐, 손만 닿으면 되는 일이냐"가 규약을 가른다** — 그리고 그건 **일감이 정한다**(`JobBase.Reach`, 기본 `StandReach`).
- **`StandReach` 0.9**(칸 반대각선 0.71) — 작업대·연구대·침대·건설·치료. 저작된 자리에 **실제로 선다**. 넓게 잡으면 폰이 두 칸 떨어져 허공에 대고 제작한다. 못 서면 교착이므로 **2초 동안 못 좁히면 `ReachRadius` 2.0에서라도 시작**하고, 그 판단은 **래치로 붙든다**(일을 시작하면 `ResetProgress`가 정체 타이머를 지워서, 안 붙들면 "일한다 ↔ 다시 걷는다"가 매 틱 진동. 목표 칸이 바뀌면 저절로 풀림).
- **`HandReach` 2.0** — 운반(`HaulJob`·`GroundHaulJob`·`UnloadJob`)·연료·수확·급수. **집고 내려놓을 뿐이라 그 자리에 설 이유가 없고**, 좁게 잡으면 같은 창고를 여럿이 쓸 때 서로 비켜서느라 오래 걸린다. `ReachRadius`와 같은 값이라 **정체 타이머를 안 기다린다**.
- **단계마다 다를 수 있어 상수가 아니라 프로퍼티다** — `ConstructionJob`·`TreatJob`은 창고에 들를 땐 손, 청사진·환자 앞에서는 자리.
- **자원 노드** = `NodeReach` 2.0으로 **둘러서서** 캔다(일감이 아니라 BT 잎이 직접 잰다). 저작된 자리가 없고 `MaxWorkers`만큼 붙으므로 한 칸에 모으면 서로 비켜서다 아무도 못 캔다.
  **가운데 한 점(노드 자신의 자리)에서 재고, 가까워지면 캔다** — 닿는 거리 = 바위 가로 반지름 + 1.5m, 걸어갈 곳 = 가운데에서
  **폰 쪽으로** 반지름 + 0.8m(`ResourceNode.ReachFrom`/`StandPointFor`, 2026-09-19 — 사용자: *"광맥 가운데에 빈 오브젝트를 하나
  만들고 그냥 가까워지면 캐게"*). 반지름은 실제 바위의 콜라이더 경계에서 한 번 잰다(야생은 크기를 굴려 선다). 그전의 두 셈이
  둘 다 틀렸다: **한복판 칸까지 2m**는 3×3 광맥 옆에 붙어 서도 넘어서 영영 안 캤고, **차지한 칸 덩어리**는 칸에 안 맞춰 선
  야생 바위와 반 칸까지 어긋나 **한쪽 면에서만** 캤다. 배정(`WorkNode.TickAssigned`)과 찍은 일감(`GatherNodeJob` —
  `JobBase.DistanceFrom`·`StandPointFor`·`FacePoint`)이 같은 답을 쓴다. 방향은 폰이 몸을 돌려 맞춘다.

일감의 목표 칸은 건물 자기 타일이 아니라 **폰이 서는 자리**다. `JobBase.SetSite(site)`가 `WorkSpot.TileFor(site)`로 한 번에 정한다 — 각 Job이 따로 계산하지 않는다. 순서: 저작된 `WorkSpot` 자식(그 칸에 설 수 있을 때) → `MapManager.FindWorkableNear(center, 반경, 발자국 제외)`(대각선). **건물이 깔고 앉을 칸은 후보에서 뺀다**(`IFootprintSite` — `PlacedBuilding`·`BuildingBlueprint`가 같은 `FootprintAt`으로 답한다). 다 지어진 건물은 그 칸이 이미 통행 불가라 저절로 빠지지만 **청사진은 일부러 안 막으므로**(짓는 동안 폰이 지나다녀야 한다) 따로 물어야 한다 — 안 물으면 폰이 3×2 건물 한복판에 서서 짓고 완성되는 순간 발밑이 막혀 **갇힌다**(길찾기는 출발 칸이 막힌 것을 풀어 주지 않는다). **폰이 선 자리에 건물이 들어서면 `PlacedBuilding.OccupyTiles`가 밖으로 내보낸다**(`CharacterController`를 끄고 옮기고 캡슐 절반만큼 띄운다 — 스폰 Y와 같은 함정). 즉시 배치는 폰을 전혀 안 보고(41종 중 15종, 여러 칸짜리는 전부 이쪽) 길찾기는 출발 칸이 막힌 것을 안 풀어 주므로, 이건 예외 처리가 아니라 **주 경로**다. **걷는 중인 폰은 건드리지 않는다** — 제 발로 빠져나가는데 순간이동시키면 가던 길이 끊긴다(단 "목표는 있는데 경로가 없는" 것은 걷는 게 아니라 막혀 선 것이라 내보낸다). 나갈 데가 없으면 그대로 둔다. **던전 핵의 `WorkSpot`은 원정 집결 원의 중심**이다 — 같은 규약을 쓰므로 집결 장소가 저작 대상이 되고 건설 모드에서 표식으로 보인다(핵은 씬 오브젝트라 그 자식으로 직접 세워 뒀다). **저작은 선호지 보장이 아니다** — 나중에 막히면 자동 탐색으로 넘어간다. 표식은 **건설 모드에서만** 보여서 그 칸을 막는 배치를 눈으로 피할 수 있다. 자리는 **프리팹·씬에 저작돼 있다** — 옮기거나 더할 때는 프리팹에서 자식 `WorkSpot`을 직접 다룬다.

## 작업대 산출물

**제작 목록은 지금 만들 수 있는 것이 위로 온다**(`CraftingTableUI.RebuildList`, 2026-08-25) — 저작 순서대로 늘어놓으면 잠긴 것이 사이사이에 섞여 **고를 수 있는 줄을 눈으로 골라내는 일**이 된다(연구가 마흔여덟으로 늘며 잠긴 줄이 절반을 넘었다). **잠긴 줄을 빼지는 않는다** — 무엇을 만들 수 있게 되는지가 곧 연구할 이유이고, 눌러서 필요한 연구를 보는 자리도 거기다. **묶음 안에서는 저작 순서 그대로**(안정 정렬) — 다시 이름순 따위로 흔들면 연구를 하나 끝낼 때마다 목록이 통째로 뒤바뀌어 **어디에 있던 줄인지를 다시 찾아야 한다.**

완성품은 **폰이 아니라 작업대에 쌓인다** — `CraftingTable.Inventory`(기본 8×3). 순서는 **산출물 → (꽉 차면) 폰 인벤 → (그것도 안 되면) 드롭**. `allowHaulOut`이 켜져 있으면 폰이 창고로 실어 간다. 운반 출처는 **`IHaulSource`**(`ResourceNode`·`CraftingTable`이 같은 `HaulJob`을 쓴다), 발행은 **`HaulDispatcher`** 공통.

**두 숫자를 헷갈리지 말 것** — `haulThreshold`는 "이만큼 쌓이면 나른다"는 발행 **조건**이고, 실어 가는 양은 **`CarryCapacity`** 가 정한다. **임계값은 발행할 때와 집을 때 둘 다 잰다**(2026-08-25) — 한 재고에 일감이 여럿 나가므로, 발행할 때만 재면 **힘센 폰이 먼저 쓸어 간 뒤 도착한 폰이 남은 한두 개를 그냥 집어 가** 걸어 둔 숫자가 조용히 풀린다. 그래서 일감이 임계값을 들고 가서(`HaulJob._threshold`) 집는 자리에서 다시 보고, 모자라면 물러선다 — **두 곳의 답이 같아야 한다.** **몫을 미리 쪼개지 않는다**: 미배정 재고를 통째로 맡긴 일감 하나를 내고, 폰이 정해지는 순간(`OnReserved`) 그 폰이 질 만큼만 남기고 **나머지를 즉시 놓아** 다음 일감이 되게 한다 — 몇 명이 붙을지 미리 맞힐 필요가 없다. 배낭에 실은 순간 맡음은 **전부** 놓는다(안 그러면 이중으로 빠진다). 주기 점검(2초)이 잔량을 다시 집는다.

연료는 **용광로만** 요구(`_requiresFuel`) — 없으면 `CraftJob.Execute`가 대기하고 UI에 게이지+보충 안내가 뜬다. `Inventory.Resize(w,h)`는 **비어 있을 때만** 통한다. 상세 `docs/jobs.md`.

## 적재 무게 — `CarryCapacity` (한도의 단일 출처)

```
한도(kg) = 10 + 근력 × 0.5      // 근력 10 → 15kg, 130 → 75kg
```

`CarryCapacity.For / Load / Free / RoomFor(brain, item) / TotalFor(members)`. 전에는 같은 것을 네 군데가 따로 계산했고 그중 둘이 3.3배 어긋나 있었다(`근력 × 5` vs `10 + 근력 × 0.5`). 근력 × 5는 130에서 650kg이라 무게가 아무것도 막지 못해 작은 쪽을 남겼다.

- **한도는 자동 운반에만 강제된다.** `HaulJob` / `GroundHaulJob`이 `RoomFor`만큼만 싣고, `CanAssignTo`로 짐이 찬 폰에겐 일감을 주지 않는다. 못 실은 잔량은 출처에 남아 다음 발행에서 새 몫이 된다.
- **플레이어가 배낭에 직접 넣는 건 막지 않는다** — 대신 넘긴 비율만큼 이동·공격 속도가 준다(`PawnMover` / `CombatHandler`의 무게 보정). 손으로 한 일을 거부하면 "왜 안 들어가지"가 되고, 자동이 한도를 무시하면 "왜 이 폰만 느리지"가 된다. `InventoryUI`가 `무게 12/15kg`을 띄우고 과적이면 붉게 물든다(액션 바 위 `CarryBar` — 씬 오브젝트).
- **짐을 지고 걸으면 근력이 자란다**(켄시식) — `PawnMover.GrowStrengthFromLoad`, 한도 대비 적재 비율로 재고 25% 미만이면 안 오른다. 근력이 커질수록 같은 짐으로는 덜 자란다.
- 원정대 등짐(`ExpeditionParty`)도 같은 한도를 쓰고 **무게로 센다**. 예전엔 kg으로 만든 수를 개수 한도로 써서 씨앗이나 철갑옷이나 한 자리였다.
- **적재를 묻는 곳은 전부 `CarryCapacity`다** — 답이 두 벌이면 반드시 어긋난다(옛 `PartyCarry`가 3.3배 어긋나 있었다).

## 재료는 `MaterialPool`로 센다 (창고만 보지 말 것)

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

## Storage API (자주 사용)

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

## Job Categories & 배정 모델

`JobCategory` enum: None, Mining, Hauling, Farming, Logging, Construction, Guard, Crafting, Research, **Unload**(짐내림), **Medical**(치료), **Warden**(간수). **값은 뒤에 붙일 것** — 직렬화가 정수라 중간에 끼우면 `MinionData.AllowedJobs`가 통째로 밀린다.

**두 갈래** — `JobCategoryInfo`가 단일 출처.
- **큐 작업**(`QueueOrder`: **치료** → **간수** → 건설 → 농사 → 제작 → 연구 → 운반 → 짐내림 — 치료가 맨 앞인 건 피 흘리는 사람은 기다릴 수 없고, 그 일감은 다친 폰이 침대에 누웠을 때만 생겨 늘 있지도 않기 때문이다. 간수가 그다음인 건 **붙잡은 적이 깨어나면 걸어 나가기** 때문이다 — 늦으면 붙잡은 것 자체가 없던 일이 된다): 건물/농지/작업대가 일감 등록 → 폰이 집어감. **배열 순서 = 전역 우선순위**(모든 폰 공통). `QueueOrder`에 없는 카테고리는 큐에서 배정되지 않는다.
- **직접 배정**(Mining/Logging = 자원 노드, Guard = 순찰 구역): 플레이어가 대상에 폰을 붙임(`Blackboard.AssignedNode`/`AssignedPatrolZone`). **일감을 안 만든다** — `WorkNode.TickAssigned`가 배정된 노드를 직접 캔다.

**채굴·벌목은 저 혼자도 붙는다 — 자율 배정**(`ResourceNode.ClaimFreeFor`, 2026-08-23). 큐 작업 여덟 중 이 둘만 손으로 붙여야 했는데 광맥은 열셋이고, 집결할 때 `AbandonWork`가 **배정까지 거둬** 원정을 갔다 오면 하나씩 다시 붙여 줘야 했다.
- **`QueueOrder`에는 안 넣는다** — 그 둘은 일감을 만들지 않으므로 넣으면 *"큐에서 배정되는 카테고리"*라는 그 배열의 뜻이 거짓이 된다. **`WorkNode`가 `RequestJob`이 빈손일 때 부르는 것**으로 족하다: 그 자리가 곧 "우선순위 맨 끝"이라 *캐 놓은 것을 나르는 일이 캐는 일보다 앞선다*가 저절로 성립한다.
- **핵 영역 안만 본다.** 야생 나무·바위도 `ResourceNode`라, 안 묶으면 폰이 맵 전체의 나무를 베러 떠난다 — 자동화가 아니라 통제를 잃는 것이다. 심은 광맥은 영역 안에만 설 수 있으므로(건설이 핵 union을 요구한다) 이 한 줄이 *"내 거점의 것만"*을 뜻한다.
- **손으로 붙인 것이 이긴다** — 이미 배정된 폰은 여기까지 안 온다. 그리고 **손 배정은 작업 설정을 안 본다**(그건 명령이다). **저 혼자 잡은 것만** 설정을 따라 놓는다(`UnitBlackboard.NodeAutoClaimed` — 침대의 `AutoClaimed`와 같은 축이되 **자리가 아니라 폰이 든다**: 노드 하나에 `MaxWorkers`만큼 붙지만 폰은 배정을 하나만 갖는다).
- **`WorkSettings.IsEnabled`는 `IsQueued`가 아니라 `JobCategoryInfo.IsToggleable`로 묻는다.** 안 가르면 채굴·벌목이 **늘 꺼진 것으로 답해 자율 배정이 한 번도 안 돈다**(`IsQueued`가 게이트였다). **경비는 빠진다** — 순찰 구역엔 자율 배정이 없다.
- **P패널에 칸이 있어야 끌 수 있다** — `ColonyColumns.WorkColumns`가 `QueueOrder`(−짐내림) **+ 채굴·벌목**이다. 이제 그 배열과 `QueueOrder`는 다른 물음에 답한다(*무슨 일을 시킬까* ↔ *큐 배정 순서*). 칸이 없으면 자율 배정이 강제가 된다.
- **`ResourceNode`가 레지스트리를 든다**(`_all`, `OnEnable`/`OnDisable`) — 매 틱 `FindObjectsByType`을 돌 수 없고, 휴면 노드는 `OnDisable`이 알아서 뺀다(`Bed._all`과 같은 자리).

Job score = `JobCategoryInfo.Weight(category) × 100 + job.Priority`.

폰별 설정은 **숫자 우선순위가 아니라 on/off** — `WorkSettings`(`UnitBrain.Work`, 플레이어 세력 일꾼에만 자동 부착). UI는 `ColonyUI`(P키). 상세 `docs/jobs.md`.


---

# 농사 — CLAUDE.md에서 내려온 상세 (2026-09-13)

**맞닿은 밭은 한 구역이다.** 구역 규칙 · 씨앗이 도는 고리 · 화면에 테두리를 그리는 법 ·
손으로 가르는 문. **CLAUDE.md에는 규칙 한 줄씩만 남겼다.**

- **씨앗은 심는 순간 하나 준다 — 쓰는 자리는 한 곳이다**(`SeedJob`의 `Phase.GoToPlot`). 전에는 **창고에 들르는 갈래에서** 썼는데, `OnStart`가 *"배낭에 있으면 창고를 건너뛴다"*고 최적화하면서 그 갈래를 통째로 건너뛰어 **씨앗 하나로 평생 농사가 됐다**. **걸어가는 것과 쓰는 것은 다른 일이다.**
- **구역을 저작하지 않는다 — 붙어 있으면 한 덩어리다**(`FarmPlot.Zone()`, **4-이웃**). 따로 만들면 *구역을 만드는 조작*과 *밭을 놓는 조작*이 둘이 되고, 밭을 하나 잇는 순간 **"이건 어느 구역인가"를 사람이 답해야 한다.** **대각선은 안 잇는다**(모서리만 닿은 밭까지 이으면 X자로 스친 밭이 통째로 한 구역이 된다). `SetSeed`/`ClearSeed`는 **구역 전체**에 건다.
- **자라는 중이면 지금 것을 안 뽑는다** — `NextSeed`에 담고 `OnHarvested`가 갈아탄다(다 키운 작물을 버리면 바꾸는 것 자체가 값이 큰 조작이 된다). **화면이 범위를 먼저 말한다**(`밭 구역 (25칸) — 밀`) — 안 말하면 *"한 칸만 바꾸려 했는데 왜 옆 밭까지"*가 된다.
- **구역의 답은 `FarmZones` 하나다**(규칙 + 색). 갈라 두면 **패널이 세는 구역과 밭이 칠하는 색이 어긋난다.** **면을 칠하지 않고 테두리를 그린다**(`FarmZoneOverlay` — 칸의 네 변 중 **이웃이 같은 구역이 아닌 변만** 낸다. 그것만으로 이어 붙은 밭은 선이 하나로 이어지고 끊긴 곳은 갈린다). 곱셈으로 면을 칠했더니 **어디서 끊기는지가 안 보였다** — 구역 표시의 절반은 *경계*인데 면을 칠하면 경계가 제일 안 보이는 것이 된다. **끌고 있는 것은 흰 선으로 얹는다**(아직 구역이 아니라 *묶으려는 것*이다 — 범위는 놓기 전에 보여야 한다) · **색은 구역의 가장 작은 칸에서 낸다**(목록 순서에서 내면 밭 하나를 지을 때마다 세계의 모든 구역 색이 뒤바뀐다) · **`MarkDirty` 때만 다시 센다**(묻는 쪽이 매 프레임이라 그때마다 훑으면 밭 수의 제곱이 프레임에 붙는다).
- **거둔 것은 그 자리에 떨어지고 저절로 실려 간다**(2026-09-05, 사용자 결정) — 전에는 농부가 배낭에 담아 창고까지 지고 갔다(2단계 일감). 그러면 **같은 물건이 두 길로 창고에 들어간다**(캔 돌은 운반이 나르고 거둔 밀은 농부가 나른다 — 화면에서 그 둘은 구별되지 않는데 규칙만 달랐다). 딸려 온 것 셋: **일감이 한 단계** · **배낭이 찼는지를 안 본다** · **거두는 사람과 나르는 사람이 갈린다.**
- **거두면 씨앗도 1~2알 나온다**(`Farming.SeedReturn`) — 심을 때 한 알을 쓰므로 밭이 **저절로 이어지고 천천히 넓어진다.** **원예를 안 태운다**(늘어나야 하는 것은 수확량이고 씨앗은 이어 가는 것이다 — 둘 다 스탯을 타면 밭의 크기가 스탯 하나로 정해진다) · **작물마다 저작하지 않는다**(`SeedData`에 필드를 더하면 이미 있는 에셋은 전부 0이라 *"고쳤는데 씨앗이 안 나온다"*가 된다).
- **밭은 공짜다**(`FarmPlot.asset` 비용 없음) — **값은 씨앗이 치른다.** 놓는 것에까지 물리면 같은 것을 두 번 받는다(온실은 구조물이라 그대로다).
- **모드 안의 도구는 모드 바에 안 넣는다**(`FarmZoneBarUI` — 확장/삭제) — 모드 바는 *어느 모드인가*를, 도구 바는 *그 모드 안에서 무엇을 하는가*를 답한다. 섞으면 **누르면 모드가 바뀌는 것과 도구가 바뀌는 것이 구별되지 않는다**(건설 모드가 건물 목록을 따로 띄우는 것과 같은 자리). **버튼 이름이 지금 하는 일을 말한다** — 고른 구역이 없으면 `확장`이 아니라 `새 구역`이다(안 그러면 *다 묶인 세계에서 새 구역을 만들 길이 없는 것처럼* 보인다).
- **손으로 가르는 문은 `FarmZoneDesignator`(모드 바 버튼 — 키 없음)** — 드래그로 묶고 **우클릭으로 푼다**(푸는 길이 없으면 잘못 묶은 밭은 헐어야만 되돌린다). **번호는 안 쓰는 것을 고른다**(재사용하면 남아 있던 밭과 조용히 합쳐진다) · **콜라이더가 아니라 자리로 잰다**(밭은 납작해서 비스듬한 시점에서 뒤쪽이 안 잡힌다).
- **큰 밭(2×2·4×4)을 만들면 칸 수만큼 수확도 수분 감소도 곱할 것** — 안 곱하면 4×4가 1×1 열여섯보다 엄격히 나쁘거나(수확 같음) 공짜로 좋다(물 한 번). **고르는 이유는 편의여야지 정답이면 안 된다.**

## 작업대 등급 — 속도·레시피·연구를 한 축이 답한다 (2026-09-17)

**CLAUDE.md에서 내려온 상세.** 사용자 결정 넷: *"연구대1(로마숫자) 연구대2 345 이렇게"* ·
*"새로 설치할 필요 없이 원래 쓰던 거에서 바로 업글"* · *"연구 속도가 빨라지고 상위 레시피를
열어"* · *"하위 연구대에서는 상위 연구대의 연구들이 안 보여야 해"*.

### 축이 하나여야 하는 이유

속도 배율과 *무엇을 만들 수 있나*를 **따로 저작하면 반드시 어긋난다** — 빠른데 상위
레시피는 안 열리는 작업대가 조용히 생기고, **그게 어느 쪽 실수인지 화면에 안 적힌다.**

```csharp
public static class BenchTier
{
    public const int   Min = 1, Max = 5;
    public const float SpeedStep = 0.25f;                 // V가 2배
    public static float SpeedOf(int tier) => 1f + SpeedStep * (Clamp(tier) - Min);
    public static string Roman(int tier)  => "I".."V";
    public static string NameOf(string baseName, int tier) => $"{baseName} {Roman(tier)}";
}
```

**정수로 둔다** — `RequiredBenchTier`가 *"이 등급 이상"*이라는 **부등호로 묻는 값**이라
순서가 뜻을 갖는다. enum이면 사이에 하나 끼우는 날 값이 밀린다(정수 직렬화).

**+25%인 이유**: 크게 잡으면 아래 등급을 쓸 이유가 사라지고(다음 등급이 나올 때까지
기다리는 편이 낫다), 10%쯤이면 **올리고도 달라진 것을 못 느낀다.**

### 등급은 같은 건물의 상태다 — 헐지 않는다

처음엔 `개량 연구대`를 **별도 `BuildingData`**로 냈는데 사용자가 뒤집었다. 별도 건물이면
올리는 일이 **헐고 다시 짓는 일**이 되어 자리·산출물 칸·지정한 제작자·쌓아 둔 주문이
통째로 날아가고, **건설 목록이 등급 수만큼 부푼다**(작업대 열하나 × 다섯 = 쉰다섯 줄).

```csharp
public interface IUpgradableBench { int Tier { get; } void SetTier(int t); string BenchName { get; } }
```

`CraftingTable`·`ResearchTable`이 구현하고, 올리는 문은 **`BenchUpgrade` 하나**다.

- **자재는 폰이 날라 온다**(사용자 결정) — 건설과 같은 규칙이라 *재료가 모자랄 때 클릭이
  그냥 씹히는* 일이 없다. 누르면 **예정**이 되고 폰이 지고 온다.
- **`ConstructionJob`을 그대로 쓴다** — 받는 타입만 `IConstructionSite`로 벌렸다.
  그 안에 *남이 먼저 가져갔을 때 · 배낭에 다 못 넣었을 때 · 도중에 짐을 내려놓았을 때*가
  들어 있어서 **복사하면 그중 하나가 반드시 뒤처진다.**
- **올리는 동안에도 작업대는 쓸 수 있다** — 막으면 재료를 모으는 동안 기지가 멎는다.
  등급이 오르는 것은 **다 모인 순간 한 번**이다.
- **취소도 철거도 전액 환불**(`OnDestroy`가 돌려준다) — 안 돌려주면 철거가 조용히 자재를 먹는다.
- **`Ensure`에서 `_bench`를 다시 대입하지 않는다** — `Awake`가 `GetComponent`로 이미
  채우는데, `PlacedBuilding`을 넘겼을 때 `as IUpgradableBench`가 null이라 **방금 찾은
  작업대를 덮어쓴다.**
- **화면은 살펴보기 창 하나다** — 작업대를 클릭하면 `SelectionManager`가
  `OnBuildingInspected`도 같이 쏘므로, **씬을 안 건드리고** 동작 줄(`SetActions`)에 붙는다.

### 어기면 조용히 깨지는 것

- **`Clamp`로 받는다**(`Tier`) — 새 필드는 0에서 시작하므로, 안 그러면 **이미 놓인 작업대
  열하나가 등급 0이 되어 조용히 느려진다.** 화면에는 아무 표시도 안 난다.
- **저장하는 진행도는 표준 초다** — `WorkedThisSession = (Time.time - _startTime) × 배율`.
  앉은 초로 저장하면 **III에서 반쯤 만들다 I로 옮긴 순간 진행도가 조용히 부풀거나 줄어든다**
  (`SavePartial` · `SetProgressSeconds`가 그 값을 받는다). 표준 초로 환산해 돌려주면
  **게이지도 이어 하기도 한 줄도 안 바뀐다** — 그 둘이 재는 것은 *얼마나 남았나*이지
  *얼마나 앉아 있었나*가 아니다.
- **마감은 `(남은 표준 초 / 배율)`** — `Time.time` 마감이라 매 틱 누적이 아니다
  (`docs/timing.md`의 배속 규칙 그대로).
- **연구는 목록에서 거르면 안 된다**(2026-09-17에 그렇게 했다가 되돌렸다).
  `ResearchTable.AllResearch`가 답하는 물음은 **"이 그래프에 무엇이 있나"**이지
  *"지금 무엇을 할 수 있나"*가 아니다. 거기서 거르니 **씬에 구워 둔 노드 마흔넷과 목록
  열다섯이 어긋나** `ResearchTableUI`가 *"구운 것이 낡았다"*며 매번 런타임에 그래프를
  다시 찍었다 — **저작한 자리가 통째로 버려지고, 다시 구워도 안 고쳐진다**(구울 때는
  마흔넷, 열 때는 열다섯이라 영원히 안 맞는다).

  답은 **`ResearchTable.HandlesTier` 하나**이고 `CanResearch`와 화면이 같이 읽는다.
  화면은 노드를 **감추되 구운 것은 그대로 둔다**(`SetActive(false)`) — 감춘 노드로 가는
  **선도 안 긋는다**(안 그러면 선이 빈 곳으로 뻗어 없는 노드가 있는 것처럼 보인다).

  **레시피 쪽은 목록에서 걸러도 된다**(`CraftingTable.Recipes`는 매번 새로 만드는 목록이라
  저작된 자리가 없다). **갈리는 축은 "그 목록이 저작물인가"다.**
- **`RequiredBenchTier`는 0이 기본이고 그건 "아무 데서나"다** — 이미 저작된 레시피
  백예순여덟과 연구 마흔넷이 전부 그대로 열려 있다. JSON도 같다(`requiredBenchTier`).
- **연구와 레시피는 다른 축이다** — 연구는 *배웠는가*, 등급은 *설비가 되는가*다.
  둘 다 잠그면 이중 잠금이므로 레시피 쪽은 **상위 금속 장비 일곱에만** 건다.

### 순환을 반드시 확인할 것

등급을 올리는 재료를 **그 등급에서만 열리는 연구**가 쥐고 있으면 영영 못 올린다.

| 올리는 등급 | 드는 재료 | 그 재료를 여는 연구 | 그 연구의 등급 |
|---|---|---|---|
| II  | 판자 · 돌 | 목공술 | **I** |
| III | 단단한 판자 · 강철 | 제련술 | **II** |
| IV  | 단단한 판자 · 티타늄 | 제련술(티타늄 주괴도 제련술이다) | **II** |
| V   | 단단한 판자 · 미스릴 | 야금학 | **III** |

**재료 표(`BenchTier.CostTo`)를 고치면 이 검사를 다시 할 것.** 지금 값은 사용자가
*"이름만 정하고 재료는 나중에"*라고 한 대로 **돌려 보라고 넣어 둔 임시값**이고, 그 표가
곧 손잡이다 — **비워 두지는 않았다**: 값이 0이면 누르는 즉시 완성되어 **업그레이드 절차
자체를 화면에서 못 본다.**

### 연구 배분 — 앞이 넓고 뒤가 빈다

`World → H. Wire Research Tiers`가 표를 쓴다. **15 / 13 / 10 / 6 / 0**(사용자:
*"앞을 넣고 뒤를 비운다"*).

- **V가 빈 것은 실수가 아니다** — 다섯째 등급은 있는데 거기서만 배우는 것이 아직 없다.
  넣을 자리가 준비돼 있다는 뜻이고, **없는 연구를 지어내 채우면 그건 저작이 아니라 벽지다**
  (`CraftResearchCreator`가 같은 말을 한다).
- **표로 적는다 — 선행 깊이에서 계산하지 않는다.** 초안은 그래프의 열에서 냈지만 값은
  표에 있다. 계산으로 두면 `GraphColumn`(화면의 것)이 규칙을 정하게 되어 **노드를 보기
  좋게 옮기는 날 등급이 바뀐다** — 연구 자리를 `ResearchGraphPositions`로 따로 뺀 것과
  같은 이유다.
- **표에 없는 연구를 센다** — 새 연구를 만들고 안 적으면 조용히 등급 0이 되어 **가장 낮은
  연구대에서도 보인다.** 그건 저작이 아니라 빠뜨린 것이라, 도구가 그 수를 찍는다.

### 등급이 얼굴을 바꾼다 (2026-09-17)

사용자: *"작업대나 연구대 등급에 따라 모델링이 달라지면 좋을 거 같긴해 업그레이드하는
느낌이 들어야지"* → 방식도 사용자가 정했다: *"부품이 붙은걸 모델을 통째로 갈아끼우는게
맞겠지? 그리고 크기도 변화가 되도 괜찮을 거 같"*.

**부품은 에디터에서 누적하고 런타임은 한 벌을 켤 뿐이다.** 런타임에 소품을 하나씩 붙이면
*무엇이 붙어 있나*가 상태가 되어 저장·복원·다시 짓기마다 그 목록이 따라다닌다. 등급마다
한 벌(`[Art 1]`…`[Art 5]`)을 통째로 켜면 **등급 하나가 곧 얼굴 하나**이고, 보이는 결과는
부품이 붙는 것과 같다 — `[Art 3]`은 `[Art 2]`에 소품 하나가 더 얹힌 모습이다.

- 굽는 것: `DungeonColony/Building → 3. Create Bench Tier Art` (열두 작업대 × 다섯 등급)
- 켜는 것: `BenchVisual.Apply()` — `SetTier`가 부른다
- 되돌리기: `4. Remove Bench Tier Art`

### 얼굴의 답은 한 벌이다

**침대의 눕는 자리는 입힌 모델이 정한다**(2026-09-20, 사용자: *"또 이상하게 누워"*) — `Bed`는 눕는 자리를 옛 도형의 약속(긴 축 = 정면 Z · 높이 = 렌더러 전체 꼭대기)으로 냈는데, 팩 침대는 발자국(3×1)에 맞춰 **90° 돌아 X로 길고** 꼭대기가 **머리판**이라 폰이 **가로로 누워 기둥 높이에 떴다.** `PropDresser`가 모델을 입힐 때 메시에서 **긴 축 · 더 높은 끝(머리판 = 베개 쪽) · 가운데 토막의 윗면**을 재서 `[Art Sleep]`을 `sleepAnchor`에 박는다(되돌리면 원래 `SleepAnchor`로). `pillowYaw`를 고쳐 맞추지 않는 이유: **다른 모델을 입히는 날 또 틀린다.** 딸려 고친 것: `Bed.WorldBounds`가 **꺼진 렌더러(옛 도형)까지 세던 것**.

`PropDresser`도 `[Art]`를 얹으므로 **둘 다 돌리면 두 겹으로 선다.** 그래서 등급을 가진
작업대 열둘을 저쪽 표에서 뺐다 — 화톳불이 `CampfireUpgrader`로 옮겨 가면서 빠진 것과
**정확히 같은 자리**다. `BenchArtCreator`는 굽기 전에 `[Art]`와 `[Art *]`를 둘 다 걷는다.

### 어기면 조용히 깨지는 것

- **세트가 하나도 없으면 아무 일도 안 한다** — 얼굴을 안 입힌 작업대가 아직 있을 수 있고,
  거기서 옛 렌더러를 끄면 **그 작업대만 통째로 안 보인다.**
- **없는 등급은 있는 것 중 가장 높은 것으로 내려앉는다** — IV까지만 굽고 V로 올리면
  **아무것도 안 켜져 투명해진다.**
- **프리팹은 I만 켠 채로 굽는다** — `BenchVisual`이 붙기 전 한 프레임 동안 다섯이 겹쳐 보인다.
- **맞추는 것은 바탕 소품뿐이다** — 세트 전체에 맞추면 뒤에 책장을 세우는 순간 그 높이에
  눌려 **탁자가 등급이 오를수록 작아진다**(올린다는 뜻과 정반대가 된다).
- **얹는 것은 바탕보다 작아야 한다**(너비의 0.4가 상한) — 책 하나가 탁자만 하면 무엇이
  바탕인지 안 읽힌다.
- **소품 콜라이더는 뗀다** — 길찾기가 보는 것은 타일 점유지 콜라이더가 아니라서, 남겨 두면
  지도는 *빈 칸*이라 하는데 몸이 부딪혀 폰이 처박힌다.
- **옛 메시는 끄기만 한다** — 지우면 같은 오브젝트에 붙은 콜라이더까지 사라져 클릭도 몸도
  안 걸린다(`PropDresser`와 같은 규칙).
- **로컬 경계로 잰다** — `Renderer.bounds`는 월드 경계라, 자식에 스케일이 걸려 있으면
  답이 갈린다. 메시 경계의 여덟 꼭짓점을 직접 옮겨 감싼다(회전이 걸려 있으면 중심+크기만으로는
  틀린다).

### 차이는 크게 잡는다

처음엔 **등급당 소품 하나에 크기 5%**였는데 사용자가 뒤집었다(*"업그레이드마다 차이가 좀
컸으면 좋겠는데"*) — 나란히 놓고 봐야 겨우 보였다. 셋을 한꺼번에 키웠다:

- **등급마다 소품 둘** (V에서는 여덟 개가 쌓인다 — I은 휑한 탁자, V는 물건이 가득한 공방)
- **크기 0.72 → 0.98** (I과 V가 36% 차이)
- **놓는 자리 셋** — `Top`(바탕 윗면) · `Behind`(발자국 뒤) · `Side`(발자국 옆, 좌우 번갈아)

**0.98을 안 넘는다** — 꽉 채우면 옆 칸 건물과 모서리가 닿아 서로 파고든 것처럼 보인다.
**더 키우고 싶으면 크기가 아니라 뒤에 세우는 것을 늘릴 것**: 바탕은 칸에 묶여 못 키우지만
**뒤엣것은 높이에 제약이 없어 실루엣을 그쪽이 만든다.** (작업 속도 쪽 손잡이는 별개로
`BenchTier.SpeedStep`이다.)

- **자리마다 따로 센다** — 한 셈으로 흩으면 위에 둘·뒤에 하나일 때 뒤엣것이 셋째 자리로
  밀려 **아무것도 없는 데로 비켜난다.**
- **여럿이면 좌우로 갈라 세운다** — 겹쳐 세우면 뒤엣것이 앞엣것에 가려 **없는 것과 같다.**

**무엇이 붙는지는 작업대의 성격이 정한다** — 연금술대에 뼈가 쌓이면 도축대와 구별이 안 되고,
그러면 **등급은 읽히는데 무슨 작업대인지가 흐려진다.** 바탕은 등급이 올라도 안 바꾼다
(같은 이유다 — 실루엣이 통째로 달라지면 *자란 것*이 아니라 *다른 물건*으로 읽힌다).


# 일감 · 창고 · 적재 · 작업 자리 — CLAUDE.md에서 내려온 원문 (2026-09-25)

CLAUDE.md를 절반 아래로 줄이며 옮겼다(사용자). **원문 그대로다** — CLAUDE.md에는 절마다 몇 줄과 포인터만 남았다.

### 일감 · 창고 · 적재 · 작업 자리

**세부는 `docs/jobs.md`**(작업 자리 두 갈래의 근거 · 작업대 산출물과 운반 발행 · `CarryCapacity` 눈금 · `MaterialPool` · Storage API 전부 · 작업 배정 모델). 여기 남기는 것은 밖에서 어기기 쉬운 것뿐이다.

```csharp
// 창고 — 필터 통과 ≠ 들어갈 수 있음. 목적지를 고를 땐 자리까지 보는 쪽을 쓸 것
Storage.FindNearest(pos, item, amount, quality)   // 실제로 받아 줄 가장 가까운 창고
Storage.FindStorageWithItem(item, from)           // 반대 물음: 그것이 실제로 든 창고
Storage.Deliver(preferred, item, amount, quality, carrier, dropAt)  // 부린 짐이 증발하지 않게
Storage.Rebind(current, from, item, amount, quality)                // 목적지가 철거됐을 때
Storage.FetchInto(...)   storage.HasRoomFor(item, amount, quality)
// 재료 — 창고만 보지 말 것 (상점 구매가 배낭으로 들어온다)
MaterialPool.Available(item, carrier) / AvailableNear(item, near) / AvailableHere(item)
MaterialPool.Consume(item, n, carrier)   // 배낭 먼저, 그다음 창고
MaterialPool.Refund(item, n, dropAt)     // **창고 먼저** — 취소는 사건이 아니라 무효화다
// 적재 — 한도의 단일 출처. 10 + 근력 × 0.5
CarryCapacity.For / Load / Free / RoomFor(brain, item) / TotalFor(members)
```

- **한 일감이 목표를 바꾸면 진행 기록을 버릴 것**(`WorkNode._progressFor`, 2026-09-05) — 운반은 **한 일감 안에서** 목표가 바뀐다(짐을 집으면 `TileCoord`가 창고로 간다). 기록을 안 지우면 **물건 앞에서 0에 가까워진 기록**이 남아 *50m 밖 창고*로 목표가 바뀐 뒤 **어떤 값도 그보다 작을 수 없고**, 6초 뒤 반드시 *"갈 수 없다"*가 된다 — 놓는 것은 **취소**라 짐을 바닥에 떨구므로 짐꾼이 **집었다 떨구고 다시 집기**를 되풀이한다. `_settled`가 같은 이유로 `_settledFor`에 칸을 들고 있는데 **그 짝을 진행 기록에 안 달아 둔 것**이 사고의 전부였다(눈금과 무관하게 터진다).
- **일감이 이동을 두 번 판단하지 않는다 — 정체는 "움직이는가"로 잰다**(`WorkNode`, 2026-09-05). 전에는 *일감까지의 **직선거리**가 6초 안에 줄지 않으면* 놓았는데, **굴에서는 틀린 눈금이다**: 폰과 목적지 사이가 통째로 바위면 경로가 수십 미터를 **돌아서** 가고 그동안 직선거리는 줄지 않는다 — **멀쩡히 걷는 폰이 6초마다 일감을 놓았다**(진단에 `로컬경로 있음` + `앞을 막은 것: 없음`이 같이 뜨면 못 가는 것이 아니라 **재는 쪽이 틀린 것**이다). **답은 `PawnMover.RouteRemaining` 하나다**(경로를 따라 남은 거리 — 라우트를 든 것이 그쪽이라 밖에서 다시 그으면 답이 두 벌이 된다). *"움직이기만 하면 통과"*로 두지 않는 이유: **제자리에서 맴도는 폰**을 못 잡는다(남은 경로는 맴돌면 안 준다). **라우트가 없는 짧은 이동에서는 직선으로 떨어진다**(돌아갈 것이 없으니 직선이 곧 그은 선이다) · **길이 바뀌면 눈금도 다시 낸다**(2026-09-06에 바로잡았다 — 전에는 *"다시 낸 길이 더 길어도 괜찮다, 걷는 동안 저절로 줄어든다"*고 적혀 있었는데 **줄기는 하는데 기록까지는 못 내려간다**: `ReportUnreachable`이 광역 덩어리를 20초 닫으면 다음 라우트가 통째로 우회로 나와 **56m가 110m가 되고**, 그 순간 옛 기록은 **다시는 못 깨는 값**이라 **재계획 한 번이 6초 뒤의 포기를 예약한다**. 열여섯 폰이 한꺼번에 놓은 것이 그 그림이었다 — 닫는 것은 **공유 그래프**다. 그래서 `PawnMover.RouteVersion`이 오르면 기록과 시계를 **지금 값에서** 다시 시작한다. **`_progressFor`(목표 칸이 바뀌면 버린다)와 같은 모양이고 그 규칙의 나머지 절반이다.** `PawnMover`는 제 시계에 이 짝을 이미 달아 두고 있었다 — `TrackGoalGain`의 *"새 길에는 새 시계를 준다"*) · **라우트를 갈아 끼우는 문은 하나다**(`PawnMover.SetCoarseRoute` — 여섯 군데였다. 판 올리기를 한 갈래에서만 빠뜨리면 그 갈래에 걸린 폰만 옛 눈금으로 재고, 화면에는 *"가끔 포기한다"*로만 보인다) · **목표가 없는 틱의 값은 안 믿는다**(그때 0인데 그걸 최고 기록으로 삼으면 **다시 목표를 준 뒤 어떤 값도 그보다 작을 수 없어** 걷고 있어도 반드시 놓는다).
- **얼마나 가까이 가야 하나는 일감이 정한다**(`JobBase.Reach`) — **`StandReach` 0.9**(거기 서서 하는 일: 작업대·연구대·침대·건설·치료) ↔ **`HandReach` 2.0**(집고 내려놓을 뿐인 일: 운반·연료·수확·급수). 상수로 박으면 모든 일감이 한 자리에 붙는다. 자원 노드는 `NodeReach` 2.0으로 **둘러서서** 캔다.
- **폰이 서는 자리는 `WorkSpot.TileFor(site)` 하나가 정한다** — Job마다 따로 계산하면 통행 불가 칸을 겨눈다. **건물이 깔고 앉을 칸은 후보에서 뺀다**(`IFootprintSite`) — **청사진은 일부러 안 막으므로 따로 물어야 한다**: 안 물으면 폰이 3×2 건물 한복판에서 짓고 완성되는 순간 **갇힌다**(길찾기는 출발 칸이 막힌 것을 안 풀어 준다). **폰이 선 자리에 건물이 들어서면 `PlacedBuilding.OccupyTiles`가 밖으로 내보낸다**(주 경로다 — 즉시 배치는 폰을 전혀 안 본다).
- **완성품은 폰이 아니라 작업대에 쌓인다**(산출물 → 폰 인벤 → 드롭). 운반 출처는 `IHaulSource`, 발행은 `HaulDispatcher` 공통. **`haulThreshold`(발행 조건)와 `CarryCapacity`(싣는 양)를 헷갈리지 말 것**, 그리고 **임계값은 발행할 때와 집을 때 둘 다 잰다**(한 재고에 일감이 여럿 나가므로 발행 때만 재면 걸어 둔 숫자가 조용히 풀린다). **몫을 미리 쪼개지 않는다** — 통째로 맡긴 일감 하나를 내고 폰이 정해지는 순간 나머지를 즉시 놓는다.
- **한도는 자동 운반에만 강제된다** — 플레이어가 배낭에 직접 넣는 것은 막지 않고 **속도로 값을 치른다**(손으로 한 일을 거부하면 "왜 안 들어가지"가 되고, 자동이 한도를 무시하면 "왜 이 폰만 느리지"가 된다).
- **`amount`·`quality`를 빠뜨리지 말 것** — 스택은 **같은 등급끼리만** 합쳐지므로, 등급을 빼먹으면 만적 창고가 받는다고 답하고 폰이 **줍고 떨구기를 무한 반복**한다. **`from`은 선택 인자가 아니다**(기본값을 두면 부르는 쪽이 조용히 빠뜨려 폰이 맵을 가로지른다).
- **운반 목적지는 도중에 사라진다** — 안 보고 `_storage.transform`을 만지면 `MissingReferenceException`이 올라가 **그 폰의 BT가 통째로 멎는다**. `Rebind`로 갈아타고, 갈 데가 없으면 그냥 들고 있는다.
- **세계의 것을 창고로 가져오는 길은 네 갈래이고 갈리는 축은 둘뿐이다 — 사람이 찍나 ↔ 저절로 가나 · 한 번 하고 없어지나 ↔ 마르지 않나**(**세부는 `docs/gathering.md`**). **바닥의 물건은 아무도 안 건드린다 — 체크한 것만 실려 간다**(`DroppedItem.HaulAllowed`, 기본 **꺼짐**, 초록 구슬 · 우클릭 한 개와 **가져오기 지정 모드(H)**). **채집물도 같은 모드가 찍는다**(`IGatherTarget` — `Forageable`도 기본 꺼짐이다: **채집물만 저 혼자 켜져 있으면 같은 화면의 같은 조작이 물건마다 다르게 돈다**). **채집 모드를 따로 내지 않는다** — 조작이 한 글자도 안 다르고, 플레이어에게 그 둘은 **같은 것**이다(*저기 있는 가져올 것*). 갈리는 것은 **찍힌 뒤에 무슨 일감이 나가느냐**뿐이고 그건 각자가 안다. **표식도 같은 초록 구슬이다**(따로 만들면 *"이건 찍힌 건가"*를 종류마다 다시 배운다) · **무르면 일감도 거둔다.** **설비가 내놓는 것은 이 축과 무관하다**(자원 노드·작업대 산출물은 바닥을 안 거친다) · **캔 바위도 그렇다**(벽에서 나온 돌은 **전리품이 아니라 생산물**이라 `MineJob`이 떨구면서 켠다).
- **어떤 품목을 자동으로 나르나 — `HaulPolicy` 하나가 답한다**(2026-09-06, 사용자 결정). **스위치는 물건이 아니라 품목을 켠다** — 갓버섯 하나를 눌러 켜면 **세계의 갓버섯 전부**가, 그리고 **앞으로 떨어지는 것도** 따라온다. **그래서 "어느 쪽이 이기나"를 정할 일이 없다**(창이 둘이어도 **쓰는 자리가 하나**다). 개별 지정과도 안 싸운다 — `Hauls = 이 한 개가 찍혔다 ‖ 이 품목이 켜졌다`(둘 다 *"나른다"*만 말하므로 **합집합**). **알림은 버스다**(`OnChanged` — 훑는 쪽을 정책이 알면 **정책이 화면을 아는 꼴**이라 창이 늘 때마다 거기가 는다: **듣는 쪽이 제 것만 다시 맞춘다**) · **키는 `ItemData`가 아니라 `Id`다**(JSON 아이템은 런타임 생성이라 참조로 잡으면 조용히 사라진다) · **`IGatherTarget.Designated`는 여전히 이 한 개를 묻는다**(품목이 켜졌다고 참을 주면 지정 모드가 *무를 것이 없다*고 답한다) · **세는 것은 `Hauls`다 — `HaulAllowed`가 아니다**(품목으로 켜진 것은 개별로 안 찍혀 있어 **켜 놓은 물건을 "지정 안 됨"이라고 적는다**). **정하는 창이 둘이고 답은 하나다** — 살펴보기 창의 버튼과 **품목 창(`O`)**: 살펴보기 창은 *눈앞의 그 물건*에서만 열려 **아직 못 본 물건을 켤 수가 없다**(광맥을 만나기 전에 *"철광석은 자동으로"*를 말할 수 없다). **스위치는 동작 줄의 버튼 하나다**(바닥 물건·채집물·광맥이 **같은 자리에 같은 버튼**) — 오른쪽 위의 작은 버튼이던 시절엔 **아무도 못 찾았고 바닥 물건에만 떴다**: **찾을 수 없는 스위치는 없는 스위치다.** **벽도 살펴볼 수 있다**(`RockRenderer.IsRockCollider`가 답한다 — 콜라이더에 컴포넌트가 없고 레이어로 가르면 지형과 섞인다. **맞은 면 안쪽으로 5cm 밀어 넣는다**).
- **자연은 찍고, 기지는 배정한다 — 다만 야생도 *손으로는* 붙일 수 있다**(2026-09-06 · **배정 금지는 2026-09-17에 풀었다**, 사용자: *"이미 캐라고 지정을 해놨는데 조금 움직였다고 아예 안 캐는건 말이 안되잖아"*). 막았던 이유는 *"붙일 수 있으면 밖에서 무한히 캔다"*였는데 **그 전제가 그 사이에 없어졌다** — 야생은 `TotalYields`만큼 내고 사라진다(유한한 것에 사람을 붙이는 일은 무한 채굴이 아니라 **그냥 시키는 일**이다). 이식 광맥이 사는 이유는 그대로다: **마르지 않고 · 기지 안에 있고 · 힘을 먹는다.** **다만 저 혼자 잡는 것은 지은 것뿐이다**(`ClaimFreeFor`, 2026-09-18 사용자: *"자동으로 게임시작했을 때 자동으로 채굴이 잡히는 걸 원하는게 아니라 내가 자동 스폰된 자원들은 내가 폰을 배정했을 때만 채굴을 해야하는거야"*) — **`Assign`과 다른 물음이다**: 앞의 것은 *사람이 시킨 일*이고 뒤의 것은 *아무도 안 시켰을 때 알아서 가나*라, 야생까지 잡으면 **판이 열리자마자 굴에 뿌려진 것을 전부 캐러 흩어진다.** **핵이 없으면 거리(200m)로 자른다**(전에는 `core == null`이면 통째로 건너뛰어 **핵을 꽂기 전에는 자율 배정이 죽어 있었다** — 화면에는 *"폰이 논다"*로만 보인다). 야생 자원 노드는 찍어서도 가져오고 `TotalYields`만큼 내고 사라진다. **지은 것**(`PlacedBuilding`이 붙은 것)만 배정을 받고 마르지 않는다. **그 축이 안 갈려 있으면 굴에서 캐 온 것으로 기지에 광맥을 이식할 이유가 없다**(밖에서 무한히 캐면 된다 — 이식이라는 축이 있는데 규칙이 쓸 이유를 안 만들던 자리). **가르는 것은 `IsBuilt` 하나다 — 저작으로 가르지 않는다**(노드가 늘 때마다 *"이건 야생인가"*를 적게 되고, 하나 빠뜨리면 **밖에서 무한히 캐지는 광맥**이 조용히 생긴다) · **야생 매장량은 규칙에서 낸다**(`World → 9. Set Wild Yields` — 손으로 적으면 **새 노드를 만드는 날 그것만 무한으로 남는다**) · **새 필드는 늘 0에서 시작하므로 축을 넣으면 데이터를 같이 채울 것**(하나만 적고 열여섯을 비워 둬서 **손으로 놓은 나무·돌이 여전히 무한**이었다) · **거절을 알리는 자리도 같이 고칠 것**(거절하는 코드만 넣었더니 우클릭·배정 창·로그가 **자리 탓을 하는 거짓말**을 했다) · **`IGatherTarget.CanDesignate`가 필요하다**(없으면 지은 광맥에도 예정 표식이 뜨고 눌러도 아무 일이 안 난다) · **한 틱 캐는 답은 `NodeWork.Tick` 하나다**(배정 BT와 일감이 각자 적으면 *"찍어서 캘 때가 더 빠르다"*가 된다) · **다 캐고 바로 안 지운다**(마지막 몫이 인벤토리에 있다).
- **채굴 일감은 겨누는 자리가 다르다 — 그래서 닿을 수 있는지를 따로 물어야 한다**(2026-09-05). 다른 일감은 *폰이 설 수 있는 칸*을 겨누지만(`WorkSpot.TileFor`) 채굴은 **바위 칸 그 자체**를 겨누고 2m 안에 서면 된다. 그래서 덩이를 넓게 찍으면 **속에 있는 칸**은 닿을 방법이 없는데도 큐에 올라가, 폰이 수십 미터를 걸어가 못 닿고 놓기를 되풀이했다(10초에 포기 711건 · 정작 **겉면은 아무도 안 팠다**). **`MineJob.IsReady`가 "둘러설 자리가 있나"를 같이 묻는다** — 겉면부터 파면 속이 저절로 겉면이 되므로 **파 들어가는 순서가 규칙에서 나온다**(우선순위를 저작할 필요가 없다).
- **한 번 하고 없어지는 것은 노드가 아니다 — `Forageable` + `ForageJob`이다**(2026-09-06, 갓버섯). **작은 것을 큰 것의 축소판으로 만들면 작아지지 않는다**: 자원 노드로 뒀더니 축 셋이 다 어긋났다 — 폰이 **배정되어 눌러앉고**(노드는 배정, 채집은 큐) · **게이지를 되풀이해 채우고**(한 번이면 끝나야 한다) · **다 따도 그 자리에 남았다.** 채집은 **큐 작업**이라 지나가다 집는다. 따면 **바닥에 떨군다**(`FarmHarvestJob`과 같은 규칙 — 배낭에 바로 넣으면 같은 물건이 두 길로 창고에 들어간다) · **파괴가 곧 "다시 날 때까지 없다"**이다(`ScatterTag`가 순번과 날수를 남긴다 — 여기서 또 적으면 답이 두 벌).
- **노드의 이름·설명은 저작할 수 있다**(`ResourceNodeData.DisplayName`/`Description`, 비우면 예전처럼 `{나오는 것} 광맥/나무`) — **나오는 것이 정체가 아닌 물건**이 있다(거대버섯이 목재를 내놓는다고 *"목재 나무"*가 아니다). **프리팹의 `ResourceNode.data`를 반드시 물릴 것** — 비면 살펴보기 창이 **날 프리팹 이름**을 띄우고 몸이 텅 빈다(둘이 서로를 가리키므로 **데이터를 먼저 만들고 프리팹에 물린 뒤 프리팹을 되꽂는다**).
- **재료를 먹는 일감에는 `IsReady` + `CanAssignTo`를 같이 둘 것** — 게이트 없이 배정되면 `OnStart`가 `Done`으로 끝내 **예약 자체가 사라진다**(눌러 둔 연구가 저절로 풀린 것처럼 보인다).
- **숙성 축은 넣었다 걷어냈다**(2026-09-06) — *전력으로 익고 폰이 캔다*는 밭과 같은 모양이라 설계로는 맞아 보였는데, **손에 쥐어 보니 값이 아니라 지연이었다**(사용자: *"억지로 시간늘리기 같은 느낌"*). **그 축이 하려던 일을 전력이 이미 하고 있었다** — 거기 *기다리는 시간*을 얹으면 **값이 두 벌이 되면서 둘 다 흐려진다.** **필드째 지웠다**(0으로 남기면 다음에 읽는 사람이 *"이건 왜 0이지"*를 잰다). **설계가 맞아 보여도 손에 쥐어 보기 전에는 모른다** — 작게 넣고 빨리 물어보는 편이 맞다(`docs/design_power.md`).
- **핵이 내는 힘을 건물들이 나눠 쓴다**(`PowerGrid` + `PowerUser`, 2026-09-06 사용자 결정. **세부는 `docs/design_power.md`**) — **힘의 총량이 곧 기지의 크기**이고 늘리는 길은 부핵뿐이라, *탐험이 본류*라는 축을 기지 쪽에서 붙잡는다(힘이 무한하면 굴에 나갈 이유가 초반 한 번으로 끝난다). **무엇이 먹나 = 가만히 있으면서 계속 내놓는 것**(이식 광맥·충전대·제단) — 벽·바닥·침대·작업대는 안 먹는다: **힘은 사람 없이 도는 것의 값이다.** **배분은 순서다 — 저작한 우선순위가 아니다**(먼저 지은 것이 먼저. 우선순위를 저작하면 그 숫자가 **화면에 안 보여** *"왜 저건 도는데 이건 안 도나"*가 뽑기가 된다. 바꾸는 길은 **끄는 것**이고 **끈 것은 아예 안 센다**) · **영역 단위다**(전부 더하면 16km 밖 부핵의 힘으로 여기 광맥이 돈다) · **못 받으면 멈추되 사라지지 않는다**(휴면과 **다른 상태**다: 힘 부족은 *핵을 키우면 되는 일*, 영역 밖은 *옮겨야 하는 일* — 화면에서 갈라 보여야 한다) · **`PowerUser`가 없으면 예전 그대로다**(안 그러면 옛 건물이 전부 멈춘다) · **배선은 도구가 한다**(`World → 8. Wire Power Users` — 프리팹 하나만 빠뜨리면 **그것만 공짜로 도는 건물**이 되고 화면에 표시가 안 난다. **폴더가 둘이고**(`3_Prefab/Node`·`3_Prefab/Building`) 하나만 훑어 **광맥 열두 개가 통째로 빠진** 적이 있다 — **붙일 근거는 이름이 아니라 물건이다**).
- **작업 배정은 두 갈래다**(`JobCategoryInfo`가 단일 출처) — **큐 작업**(`QueueOrder`: 치료 → 간수 → 채굴 → 건설 → 농사 → **채집** → 제작 → 도축 → 연구 → 운반 → 짐내림, **배열 순서 = 전역 우선순위**) ↔ **직접 배정**(채굴·벌목=자원 노드, 경비=순찰 구역 — **일감을 안 만든다**). **채굴·벌목은 저 혼자도 붙는다**(`ResourceNode.ClaimFreeFor`) — **`QueueOrder`에는 안 넣고** `WorkNode`가 `RequestJob`이 빈손일 때 부른다(그 자리가 곧 "우선순위 맨 끝"이다). **핵 영역 안만 본다**(안 묶으면 맵 전체의 나무를 베러 떠난다). **손으로 붙인 것이 이기고 작업 설정을 안 본다**(그건 명령이다).
- 폰별 설정은 숫자 우선순위가 아니라 **on/off**(`WorkSettings`, P키). Job score = `Weight(category) × 100 + job.Priority`.



# 농사 — CLAUDE.md에서 내려온 원문 (2026-09-25)

CLAUDE.md를 절반 아래로 줄이며 옮겼다(사용자). **원문 그대로다** — CLAUDE.md에는 절마다 몇 줄과 포인터만 남았다.

### 농사 — 맞닿은 밭은 한 구역이다
**세부는 `docs/jobs.md` "농사"**(씨앗 고리의 눈금 · 테두리 그리기 · 구역 도구 · 큰 밭 곱하기).

- **구역을 저작하지 않는다 — 붙어 있으면 한 덩어리다**(`FarmPlot.Zone()`, **4-이웃**). 따로 만들면 *구역을 만드는 조작*과 *밭을 놓는 조작*이 둘이 되고, 밭을 하나 잇는 순간 **"이건 어느 구역인가"를 사람이 답해야 한다.** **대각선은 안 잇는다**(모서리만 닿은 밭까지 이으면 X자로 스친 밭이 통째로 한 구역이 된다). `SetSeed`/`ClearSeed`는 **구역 전체**에 건다. **구역의 답은 `FarmZones` 하나다**(규칙 + 색) — 갈라 두면 **패널이 세는 구역과 밭이 칠하는 색이 어긋난다.** **화면이 범위를 먼저 말한다**(`밭 구역 (25칸) — 밀`): 안 말하면 *"한 칸만 바꾸려 했는데 왜 옆 밭까지"*가 된다.
- **면을 칠하지 않고 테두리를 그린다**(`FarmZoneOverlay` — 칸의 네 변 중 **이웃이 같은 구역이 아닌 변만** 낸다). 면을 칠했더니 **어디서 끊기는지가 안 보였다** — **구역 표시의 절반은 *경계*인데 면을 칠하면 경계가 제일 안 보이는 것이 된다.**
- **씨앗은 심는 순간 하나 준다 — 쓰는 자리는 한 곳이다**(`SeedJob`의 `Phase.GoToPlot`). 전에는 **창고에 들르는 갈래에서** 썼는데, `OnStart`가 *"배낭에 있으면 창고를 건너뛴다"*고 최적화하면서 그 갈래를 통째로 건너뛰어 **씨앗 하나로 평생 농사가 됐다.** **걸어가는 것과 쓰는 것은 다른 일이다.** **거두면 씨앗도 1~2알 나온다**(`Farming.SeedReturn`) — 심을 때 한 알을 쓰므로 밭이 **저절로 이어지고 천천히 넓어진다**(**원예를 안 태운다**: 늘어나야 하는 것은 수확량이고 씨앗은 이어 가는 것이다).
- **거둔 것은 그 자리에 떨어지고 저절로 실려 간다**(2026-09-05, 사용자 결정) — 전에는 농부가 배낭에 담아 창고까지 지고 갔는데, 그러면 **같은 물건이 두 길로 창고에 들어간다**(캔 돌은 운반이 나르고 거둔 밀은 농부가 나른다 — 화면에서 그 둘은 구별되지 않는데 규칙만 달랐다). **자라는 중이면 지금 것을 안 뽑는다**(`NextSeed`에 담고 `OnHarvested`가 갈아탄다 — 다 키운 작물을 버리면 바꾸는 것 자체가 값이 큰 조작이 된다).
- **밭은 공짜다 — 값은 씨앗이 치른다**(놓는 것에까지 물리면 같은 것을 두 번 받는다. 온실은 구조물이라 그대로다). **큰 밭(2×2·4×4)을 만들면 칸 수만큼 수확도 수분 감소도 곱할 것** — 안 곱하면 4×4가 1×1 열여섯보다 엄격히 나쁘거나(수확 같음) 공짜로 좋다(물 한 번). **고르는 이유는 편의여야지 정답이면 안 된다.**
- **모드 안의 도구는 모드 바에 안 넣는다**(`FarmZoneBarUI` — 확장/삭제) — 모드 바는 *어느 모드인가*를, 도구 바는 *그 모드 안에서 무엇을 하는가*를 답한다. 섞으면 **누르면 모드가 바뀌는 것과 도구가 바뀌는 것이 구별되지 않는다.** **버튼 이름이 지금 하는 일을 말한다**(고른 구역이 없으면 `확장`이 아니라 `새 구역`이다 — 안 그러면 *다 묶인 세계에서 새 구역을 만들 길이 없는 것처럼* 보인다). **손으로 가르는 문은 `FarmZoneDesignator`**(드래그로 묶고 **우클릭으로 푼다** — 푸는 길이 없으면 잘못 묶은 밭은 헐어야만 되돌린다).

- **`BuildModeManager.ToggleBuildMode`는 건설 모드를 켜고 끈다**(`B`와 같은 문) · **`TogglePlacementMode`가 격자/자유를 뒤집는다.** 2026-09-02까지 앞의 이름이 뒤의 일을 하고 있었고, 모드 바의 `건설` 버튼이 그걸 불러 **눌러도 아무 일도 안 일어났다.** **이름이 물음에 답해야 한다.**

## 배치 — 1~3순위 (2026-09-29)

사용자: *"팰월드는 건물에 배치하는게 하나여서 그 하나를 더 못하는 상황이 오면 그냥 놀잖아 … 3순위까지 만들어서 하나를 끝내면 2순위를 하고 그것도 끝내면 3순위를"* · *"배정이 안되어있는 폰은 지금처럼"*.

- **답은 `WorkPosts` 하나**(폰 컴포넌트 · 칸 셋). 칸 종류 넷 — `Node`(지은 자원 노드) · `Station`(작업대 · 연구대 · 도축대 — 그 대의 일감만) · `Farm`(넣은 밭과 붙은 구역 전체 — 구역에 번호가 없어 `FarmZones.Of`로 맞춘다) · `Haul`(운반 그 자체 — 창고가 아니다, 사용자).
- **일감이 제 건물을 답한다** — `JobBase.Site`(제작 · 연료 · 연구 · 도축은 대, 밭 일감은 밭). 건물에 매이지 않은 일은 `null`.
- **일꾼**(`WorkNode.TickPosts`): 들고 있는 일감이 없을 때 **1순위부터 할 일이 있는 첫 칸**. 노드 칸은 노드를 직접 캔다(`ResourceNode.Assign`) — **캐는 중에도 3초마다 위를 다시 본다**(노드는 끝이 없는 일이라 안 보면 1순위에 일이 생겨도 영영 안 돌아간다). **셋 다 없으면 논다**(`Failure` → 대기 · 저 혼자 광맥 잡기도 안 한다 — 사용자 결정). **자기 배낭 비우기(`Unload`)는 늘 먼저.**
- **배치는 명령이다** — 일감 요청에 거르는 조건을 넘기면(`JobManager.RequestJob(unit, only)`) 켜고 끄기(`IsEnabled`) 대신 **못 하는 일**(`IsBlocked` — 배경 · 종족)만 막는다. 손으로 맡긴 일감(`FindReserved`)은 그대로 먼저다.
- **창**(`WorkPostsUI` · 줄 `WorkPostRowUI`): **핵을 누르면 열린다**(사용자: *"살펴보기에 있으면 안되고 … 배치를 핵을 누르면"*). 전에 핵이 열던 **소환 창은 새 건물 소환진으로 옮겼다**(`SummonCircle` · `Building → B` — 목록·한도는 그 자리를 덮는 핵이 들고, 하수인은 소환진 곁에 선다). 줄마다 드롭다운 셋 — 0번 *비어 있음* + `WorkPosts.Candidates`(핵 영역 안 · 같은 이름은 핵에서 가까운 순으로 번호). **지금 칸의 건물이 목록에 없으면 끝에 붙여 보여 준다**(안 그러면 여는 것만으로 비어 보인다). 열 때마다 다시 짓는다. 씬은 `UI → Create Work Posts Window`가 세운다.
- **아직 안 한 것**: 우클릭으로 노드에 붙이는 옛 길은 배치가 있는 폰에게는 3초 뒤 배치가 덮는다(배치가 이긴다).


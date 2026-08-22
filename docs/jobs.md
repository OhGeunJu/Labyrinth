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

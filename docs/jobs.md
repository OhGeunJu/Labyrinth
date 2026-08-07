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
| `MineJob` | Mining | **현재 미사용** — 어디서도 생성되지 않는다. 채굴은 노드 직접 배정(`WorkNode.TickAssigned`) 경로로만 돌아간다 |
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

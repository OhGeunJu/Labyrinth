# Job System & Buildings

## Job System (`Assets/1 Script/Job/`)

`JobManager` (singleton):
- `RegisterJob(JobBase)` — 작업 등록
- `RequestJob(unit)` — 방랑자용 (필터 없음)
- `RequestJobFiltered(unit, allowedCategories)` — 하수인용

Job score = `globalPriority × 100 + job.Priority`. `PriorityManager.Get(category)` 0이면 비활성.

`JobBase.Category` — virtual `JobCategory` 프로퍼티.

## 모든 Job 타입

| Job | Category | 흐름 |
|---|---|---|
| `MineJob` | Mining | ResourceNode 도착 → Work 틱마다 `ResourceNode.Work` |
| `HaulJob` | Hauling | GoToSource(노드 재고 전량 수거) → GoToStorage(TryAdd) |
| `GroundHaulJob` | Hauling | GoToItem(DroppedItem.PickUp) → GoToStorage |
| `CraftJob` | Crafting | OnStart 재료 즉시 소비 → 테이블 도착 → CraftTime 카운트 → DroppedItem.Spawn |
| `FuelJob` | Crafting | 창고 이동(수거) → 용광로(AddFuel) |
| `SeedJob` | Farming | 창고에서 씨앗 소비 → FarmPlot 심기 |
| `WaterJob` | Farming | (우물 페이즈 →) FarmPlot 물 주기 |
| `FarmHarvestJob` | Farming | FarmPlot 수확 → Storage 운반 |
| `ConstructionJob` | Construction | 창고 수거 → BuildingBlueprint 납품 |
| `ResearchJob` | Research | OnStart 재료 즉시 소비 → ResearchTable 카운트 → ResearchManager.Complete |

**HaulJob 개선 사항:**
- `ResourceNode._haulJobPending` 플래그 — 중복 등록 방지. 완료/취소 시만 새 등록
- GoToSource에서 `_amount` 고정값 아닌 노드 현재 재고 전량 수거
- `HaulJob(onDone: Action)` — 완료/취소 시 pending 플래그 자동 해제
- `ResourceNode.haulThreshold` (기본 10) — 이 수량 이상일 때만 HaulJob 등록
- `UnitBrain.Die()`에서 `CurrentJob` 자동 취소

**WorkNode 작업 반경:** 2.0f (콜라이더에 끼어도 2.0f 이내면 실행). Logging은 `Stats.Cutting`, 그 외는 `Stats.Mining` 성장.

## Priority System

`PriorityManager` (singleton):
- `Get(category)` / `Set(category, value)` / `Increase` / `Decrease` — 0~5
- `IsEnabled(category)` — 0이면 false

`PriorityPanel` — P키 토글.

## Resource & Storage System

**`ResourceNode`:**
- `Awake`에서 타일을 Impassable로 설정, `OnDestroy`에서 복구
- `DropYield()` → 자신 `Inventory`에 아이템 추가 → 가장 가까운 호환 Storage에 HaulJob 등록
- `haulThreshold` — NodeInfoPanel에서 +5/-5 버튼으로 조절

**`Storage`** (씬 오브젝트):
- `BuildingName` — 표시명
- `allowedTypes: List<ResourceType>` — empty = 전체 허용
- 필터 API: `SetAllowAll()` / `AddFilter(type)` / `RemoveFilter(type)` / `HasFilter(type)` / `CanAccept(ItemData)`
- `TryAdd(item, amount)` — 필터 통과 시만 입고
- `Init(BuildingData)` — 빌드 모드 배치 시 초기화
- `static OnSelected` — 좌클릭 시 이벤트 → `StorageBuildingUI`가 구독
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

`FarmPlotUI` — 씨앗 선택 버튼 + 상태/성장/수분 표시. ESC로 닫기.

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

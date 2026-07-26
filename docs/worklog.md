# 작업 로그

날짜별 주요 작업 기록. 컨벤션 상세는 `docs/editor_creators.md` 등 해당 문서 참조.

---

## 2026-07-26 (5) — 화상 DoT 방어구 무효화 임시 수정: 방어구 우회 플래그 (버그 백로그 처리)

버그 백로그 "화상 DoT 무효화" 처리. `ApplyBurningDoT`가 매 프레임 `strength × deltaTime`(≈0.08) 미세 피해를 `BodyPartSystem.TakeDamage` 정식 파이프라인에 보내는데, 방어구 **고정 감소**(`armor.DamageReduction`)가 매 틱 그보다 큰 값을 빼 → 방어구 1개만 있어도 화상 피해 상시 0.

### 수정 (임시 — 화상/마법 전면 개편 전까지)
- **`DamageInfo.IgnoreArmor` 플래그** 추가. true면 `BodyPartSystem.TakeDamage`의 방어구 고정 감소 블록을 건너뜀(강인함 저항·혈액 손실·부위 피해 파이프라인은 유지).
- **`ApplyBurningDoT`**: `IgnoreArmor = true` 설정 → 화상이 방어구와 무관하게 매 틱 피해. (틱 누적 대신 우회 선택 — 사용자 요청: 화상/마법은 추후 전면 재작성 예정이라 최소 수정.)
- 매 프레임 파이프라인으로 가는 DoT는 화상뿐(독은 lump/부패 게이지라 무관) — 우회 범위 화상 한정.

### 검증 필요 (Play Mode)
1. 방어구 착용 유닛에 화상 부여 → 부위 HP/혈액이 지속 감소(이전엔 0). 무방비 유닛과 화상 피해가 비슷한지.

---

## 2026-07-26 (4) — 빌드 고스트 부수효과 차단: 비활성 인스턴스화 + 스크립트 제거 (버그 백로그 처리)

버그 백로그 "빌드 고스트 부수효과" 처리. `EnsureGhost`가 **활성 프리팹을 `Instantiate`** → 모든 컴포넌트의 `Awake`/`OnEnable`이 즉시 실행된 뒤에야 `enabled=false`로 끔. `enabled=false`는 `OnEnable/Update`만 막을 뿐, 이미 실행된 부수효과(창고/침대/농지 레지스트리 `All.Add`, 이벤트 구독 등)는 되돌리지 않음 → 배치도 안 한 미리보기 고스트가 레지스트리에 유령 등록. OnEnable 등록 컴포넌트를 새로 붙이면 바로 함정.

### 수정
- **비활성 컨테이너 밑에서 인스턴스화**: `new GameObject` 컨테이너를 `SetActive(false)` 후 그 자식으로 프리팹 인스턴스화 → Unity가 비활성 계층 스크립트를 깨우지 않아 `Awake`/`OnEnable` 자체가 유예됨.
- **깨어나기 전에 스크립트 제거**: 비활성 상태(=아직 Awake 전)에서 자식의 모든 `MonoBehaviour`를 `DestroyImmediate` → 이후 `ShowGhost`가 컨테이너를 활성화해도 스크립트가 없어 부수효과 전무. `Collider`는 `enabled=false`(비활성화). **렌더러/메시는 남아 미리보기 정상 표시.** 배치 대상 건물은 상호 `RequireComponent`가 없어 제거 순서 무관(확인함).
- 위치/회전은 컨테이너가 받고(`ShowGhost`의 `SetPositionAndRotation`) 모델은 로컬 0으로 정렬 → 기존 배치 미리보기 동작과 동일. 실제 배치(`Place`)는 별도 `Instantiate`라 무영향.

### 검증 필요 (Play Mode)
1. 건설 모드에서 창고/침대/농지 고스트를 띄우고 이리저리 움직인 뒤 취소 → `Storage.All`/`Bed`/`FarmPlot` 레지스트리에 유령 항목 없어야(배치 전엔 창고 집계·운반 대상에 안 잡힘).
2. 고스트 미리보기 시각(메시)은 이전처럼 정상 표시.
3. 실제 배치 시 건물 정상 동작(레지스트리 등록·작업).

---

## 2026-07-26 (3) — 신체 부위 균등 타격 → 가중치 추첨 (버그 백로그 처리)

버그 백로그 "신체 부위 균등 타격" 처리(의도 확인 → 가중치 부여로 결정). `GetRandomBodyPart`가 `Enum.GetValues` 7부위 **균등 추첨**(각 14.3%)이라 머리가 몸통과 똑같이 자주 맞아 비현실적 + 코어 부위(즉사/기절) 판정이 과하게 잦았음.

### 수정
- **`BodyPart.HitWeight`** 필드 추가(생성자 마지막 인자, 기본 1). 부위 크기·노출도 기반 피격 가중치.
- **`BodyPartSystem.InitBodyParts`**: 가중치 부여 — 가슴 30 / 복부 20 / 머리 10 / 사지 각 10 (합 100). **몸통 50%, 머리 10%**(기존 14%). 튜닝 지점이 부위 정의와 한곳에 모임.
- **`GetRandomBodyPart`**: `HitWeight` 합에 대한 가중치 추첨으로 교체(누적 롤). 총합 0/부동소수 오차 시 `Chest` 폴백.

### 검증 필요 (Play Mode)
1. 다수 전투 반복 → 몸통(가슴/복부) 피격이 확연히 잦고 머리 피격은 드문지(로그/부위 HP 감소 분포).
2. 머리 즉사·기절 빈도가 이전보다 낮아졌는지(체감).

---

## 2026-07-26 (2) — 클릭 레이캐스트 일원화: 건물 개별 레이캐스트 → SelectionManager 단일 라우팅 (버그 백로그 처리)

버그 백로그 "클릭 레이캐스트 분산" 처리. `Storage`/`Bed`/`FarmPlot`/`Merchant`/`CraftingTable`/`ResearchTable`/`PatrolZone` 7종이 **각자 `Update`에서 좌클릭마다 전체 씬 `Physics.Raycast`** → ① 건물 수만큼 중복 레이캐스트, ② UI 관통(패널 위 클릭도 건물 열림 — `IsPointerOverGameObject` 미검사), ③ 빌드모드 미게이트, ④ `SelectionManager`(유닛/노드/코어/시체 선택)와 우선순위 미조정.

### 수정 — 라우팅 일원화
- **`IWorldClickable` 인터페이스 신설**(`Assets/1_Script/Input/`): `void OnWorldClick()`. 클릭 선택 가능한 건물이 구현.
- **7개 건물**: 각자의 `Update` 클릭 레이캐스트 블록(+`_cam` 필드) 제거 → `OnWorldClick() => OnSelected?.Invoke(this)`로 대체. `FarmPlot`(성장/작업 등록)·`CraftingTable`(연료)는 `Update`의 나머지 로직 유지, 클릭 부분만 제거. 각 `OnSelected` static 이벤트·구독 UI는 그대로.
- **`SelectionManager.SelectSingle`**: 기존 단일 레이캐스트(Core→Node→Corpse→Pawn) 뒤에 `hit.collider.GetComponent<IWorldClickable>()` 분기 추가 → 유닛/노드/코어 선택 해제 후 `OnWorldClick()` 발동. **클릭당 레이캐스트 1회**로 통일.

### 부수 동작 변화(개선)
- 건물 클릭이 이제 **UI 블로킹**(패널 위 클릭 시 건물 안 열림) + **빌드모드 게이트**(`BuildModeManager.IsActive` 시 무시) 적용 — `SelectionManager` 경로 공유로 자동. 유닛 선택 해제 동작은 기존과 동일(건물 클릭 = 유닛 선택 해제).

### 검증 필요 (Play Mode)
1. 각 건물(창고/침대/농지/상인/작업대/연구대/순찰구역) 클릭 → 해당 패널이 이전처럼 정상 오픈.
2. UI 패널 위에서 클릭 → 뒤 건물이 안 열림(관통 방지).
3. 빌드모드 중 건물 클릭 → 패널 안 열림(배치에 집중).
4. 건물 클릭 시 선택 중이던 유닛 해제되는지(기존 동작 유지).

---

## 2026-07-26 — 아사(허기) 피해를 숨은 폴백 HP → 혈액 파이프라인으로 (버그 백로그 처리)

버그 백로그 "허기 피해가 숨겨진 폴백 HP로" 처리. `UnitBrain.UpdateHunger`가 허기 0일 때 `TakeDamage(float)`로 `_hp`(BodyPartSystem 없는 유닛용 폴백)를 깎았음 → 신체 부위 시스템을 가진 유닛(대부분의 방랑자/하수인)은 **UI에 안 보이는 제2 체력**으로 아사, 혈액/신체 부위와 무관하게 즉사.

### 수정
- **`BodyPartSystem.Starve(amount)`** 신설: 무작위 부위/갑옷과 무관하게 혈액을 직접 깎고 `CheckBloodState` 호출 → 기존 혈액 파이프라인(기절 0% → 사망 -30%)으로 전신 쇠약. `UnitHPUI`가 `Blood`를 표시하므로 아사 진행이 눈에 보임.
- **`UnitBrain`**: `_body`(BodyPartSystem) 캐시 추가. `UpdateHunger`의 아사 분기를 `_body != null → Starve` / 없으면 `_hp` 폴백으로 분리. 아사 속도 상수화(`StarveBloodPerSec = 2f`).
- **아사 진행을 기절 게이트 밖으로**: `Update`의 `UpdateHunger` 호출을 `IsKnockedOut` 조기 반환 **위로** 이동 → 굶어 쓰러진 뒤에도 혈액이 계속 빠져 실제 아사(死)에 도달. (안 옮기면 기절 중 허기 정지 + 혈액 자연회복으로 기상↔재기절 무한 진동, 영원히 안 죽음.)

### 비틀거림(Stagger) 시스템 제거 + 기절 임계 20% → 0%
혈액 30% 임계 비틀거림(짧은 행동 불능)은 불필요하다고 판단 → 전면 제거. `BodyPartSystem`의 `OnStaggered`/`staggerBloodThreshold`/`baseStaggerDuration`/`_bloodStaggerTriggered`/`TryStagger`/`Stagger`/`UpdateStagger` + `UnitBlackboard.IsStaggered`/`StaggerTimer` + `UnitBrain.Update`의 stagger 게이트 삭제.
- **기절 임계 `koBloodThreshold` 0.2 → 0**: 비틀거림 제거로 중간 약화가 없어졌으니, "피가 바닥나면(0% 미만) 기절"이 직관적. 기상 임계(`wakeBloodRatio` 25%)와의 간격이 5%→25%로 넓어져 기절이 더 의미 있어짐. **코드 기본값 + Scene1 직렬화 값 5곳 모두 0으로 변경**(직렬화 값이 코드 기본값을 덮으므로 둘 다 필요). 코어 부위 -10 이하 강제 기절(`coreKoDepth`)은 그대로 유지.
- 혈액 상태는 이제 기절(0% 미만)/사망(-30%) 2단. Scene1의 stagger 직렬화 잔여값(`staggerBloodThreshold`/`baseStaggerDuration`)은 없는 필드라 Unity 다음 저장 시 자동 폐기(무해). 문서(combat/units/README) 동기화.

### 검증 필요 (Play Mode)
1. 방랑자를 굶김(음식 없음) → 허기 0 후 **혈액 바가 눈에 띄게 감소**(숨은 죽음 아님) → 혈액 바닥(0%)서 기절 → 계속 굶으면 사망.
2. 기절한 굶주린 유닛이 기상↔재기절 무한 반복 없이 결국 사망하는지.
3. 굶는 도중 음식 공급 → 허기 회복 후 혈액 자연회복으로 정상 복귀(재기절 안 함).

---

## 2026-07-21 (7) — 건물 타일 점유 비대칭: 건물별 점유 플래그(대칭 등록/해제) + 중복 배치 금지

버그 백로그 "건물 타일 점유 비대칭" 처리(설계 결정: 건물별 플래그 = RimWorld식). 기존엔 `Place`가 `_occupied` 등록을 안 하는데 `Demolish`는 center 타일을 무조건 해제 → ① 같은 타일 중복 배치, ② 건물이 노드 타일에 겹치면 철거가 노드 점유까지 해제. (`_occupied`는 ResourceNode만 등록, 건설 판정+길찾기 공유 축.)

### 정책: 건물별 점유 플래그 (통행 차단 = 중복 차단, 단 중복 금지는 전 건물 공통)
- **`BuildingData.OccupiesTile`** 플래그 추가(기본 false). 벽·작업대 등 실체 건물=true(통행 불가), 창고·침대·농지=false(유닛이 밟음). 작업은 인접 타일에서(`GetAdjacentPassable`) 하므로 점유해도 작업 가능.
- **`PlacedBuilding`**: `OccupyTiles(tiles)`로 footprint를 `_occupied` 등록 + 소유 목록 보관. `Demolish`는 **자기가 등록한 타일만** 해제 → 노드 등 타 점유 안 건드림(비대칭 해소).
- **`BuildModeManager.Place` / `BuildingBlueprint.Complete`**: `OccupiesTile`이면 배치 시 `OccupyTiles` 호출(즉시 배치·2단계 건설 양쪽). 점유는 ResourceNode와 동일한 `SetTile`→`OnTileChanged` 경로라 메소/L1 재베이크 자동 반영.
- **중복 배치 금지(전 건물)**: `IsBuildableGrid`/`IsBuildableFree`에 `PlacedBuilding.All` 검사 추가 — 점유 플래그와 무관하게 완성 건물 위에 겹쳐 배치 불가(창고/침대도). 바닥(`IsFloor`)은 밑에 깔리므로 제외.

### 후속 (에디터 데이터 작업 — 사용자)
- 벽/작업대 등 통행 차단할 `BuildingData` 에셋의 `OccupiesTile`을 체크. 창고·침대·농지는 false 유지(기본값).

### 검증 필요 (Play Mode)
1. 같은 타일에 건물 2개 배치 시도 → 두 번째 빨강/거부(창고·침대 포함).
2. `OccupiesTile` 건물(벽) 배치 → 유닛이 그 타일 우회, 철거 시 통행 복구.
3. 자원노드 옆/위에 건물 짓고 철거 → 노드 점유 유지(노드가 계속 통행 차단).
4. 작업대(OccupiesTile=true) → 유닛이 인접에서 제작 정상.

---

## 2026-07-21 (6) — static 이벤트 구독 해제 누락(람다) 수정

버그 백로그 "static 이벤트 구독 해제 점검" 처리. 전 static 이벤트(약 35개)의 `+=`/`-=` 짝을 전수 조사 → 대부분 정상, **람다 구독 4곳**만 결함. 람다는 참조가 매번 달라 `-=`로 못 떼거나 아예 해제 코드가 없어, static 이벤트가 파괴된 UI를 계속 참조 → 이후 이벤트 발동 시 `MissingReferenceException` + 리스너 누수.

### 수정 (람다 → 명명 메서드)
- **`CorpseInventoryPanel`**: `BuildModeManager.OnActiveChanged += b => {…}` 해제 없음 → `OnBuildModeChanged` 메서드 + OnDestroy 해제.
- **`PatrolZoneUI`**: 동일(OnActiveChanged 람다 해제 없음) → 명명 메서드 + OnDestroy 해제.
- **`MerchantUI`**: 동일 → 명명 메서드 + OnDestroy 해제.
- **`ResearchTableUI`**: `ResearchManager.OnResearchCompleted += _ => RebuildList();` 와 `-= _ => RebuildList();`가 **서로 다른 람다**라 해제가 no-op → `HandleResearchCompleted` 명명 메서드로 교체(정상 해제).

### 정상 확인(수정 불필요)
- 나머지 UI/코어 구독은 `OnEnable/Awake` ↔ `OnDisable/OnDestroy`로 짝 정상.
- 인스턴스 이벤트 람다(`entry.OnSelected += …`, `Inventory.OnChanged += …`)는 퍼블리셔가 구독자와 같이 파괴돼 누수 아님 — 제외.

### 검증 필요 (Play Mode)
- 상점/시체/순찰/연구 패널을 연 상태로 건설 모드 토글 반복 + 해당 건물 철거(패널 파괴) 후 다시 건설 모드 토글 → 콘솔에 MissingReferenceException 없어야 함.

---

## 2026-07-21 (5) — Storage.TryAdd 부분 입고 시 잔량 증발 해소

버그 백로그 "Storage.TryAdd 부분 입고에도 true" 처리. `Storage.TryAdd`가 `Inventory.TryAdd(...) > 0`(bool) 기준이라, 격자에 일부만 들어가도 성공(true)으로 취급 → 호출자는 전량 입고로 오인하고 나머지를 방치 → **잔량 증발**(유닛은 이미 인벤에서 뺀 뒤).

### 수정
- **`Storage.TryAdd` 반환 bool → int(실제 입고 수량)**: 0=거부/만적, amount 미만=부분 입고. 계약 명시.
- **`GroundHaulJob`**: 부분 입고 잔량을 다른 창고 → 인벤 복귀(방금 뺀 공간이라 반드시 수용) 순으로 라우팅. 증발 방지.
- **`FarmHarvestJob`**: 잔량을 다른 창고 → 땅 드롭 순으로 라우팅.
- 호출자는 이 2개 파일 4곳뿐(그 외 `.TryAdd`는 전부 `Inventory.TryAdd` int 버전이라 무영향). 상인 `dest.Inventory.TryAdd`도 Inventory 직접 호출이라 무관.

### 검증 필요 (Play Mode)
1. 거의 찬 창고로 다량 운반 → 들어갈 만큼만 입고, 나머지는 다른 창고/인벤으로 (사라지지 않음).
2. 수확물 운반도 동일 — 잔량이 땅에 드롭되어 재운반 가능.

---

## 2026-07-21 (4) — DroppedItem 등급(quality) 보존: 장비 드롭 시 등급 소실 해소

버그 백로그 "DroppedItem 품질 미보존" 처리. `DroppedItem`이 `Item`/`Amount`만 들고 quality가 없어, 등급 있는 장비가 땅 드롭을 거치면 Common으로 소실됐음(플레이어가 인벤에서 버림, 제작 넘침 드롭, 픽업 잔량 재드롭 등).

### 수정
- **`DroppedItem`**: `Quality` 필드 + `Spawn(item, amount, pos, quality = Common)` 오버로드. `PickUp`이 `Inventory.TryAdd(Item, Amount, Quality)`로 등급 유지 + 잔량 재드롭도 등급 보존.
- **`Storage.TryAdd(item, amount, quality)` 오버로드**: 하부 `Inventory`가 `ItemInstance.Quality`를 보유하므로 등급째 입고 가능(기존 무인자 버전은 Common 위임).
- **`GroundHaulJob`**: 드롭 아이템의 `Quality`를 잡에 저장 → 창고/폴백 입고를 등급 인지 `TryAdd`로 처리(땅 장비를 창고로 운반해도 등급 유지).
- **등급 보존 드롭 지점**: 플레이어 월드 드롭(`DraggableItem`), 제작 결과물 넘침 드롭(`CraftJob`), 픽업 잔량 재드롭(`DroppedItem`).
- **Common 유지(의도)**: 자원 노드 운반(`HaulJob`), 수확물(`FarmHarvestJob`), 건물 환불·재료 환불(`PlacedBuilding`/`CraftJob` 재료) — 자원/식량은 등급 개념 없음.

### 검증 필요 (Play Mode)
1. 희귀 등급 무기를 인벤에서 땅으로 드래그 드롭 → 다시 주우면 [희귀] 유지(Common 아님).
2. 스킬 높은 유닛이 장비 제작 → 인벤 꽉 차 넘침 드롭된 것도 등급 유지.
3. 등급 장비 땅 드롭 → 운반되어 창고 입고 후에도 등급 유지.

---

## 2026-07-21 (3) — 제작 주문 재료 부족 시 소멸 → 대기/재개 (RimWorld식)

버그 백로그 "CraftJob 재료 부족 시 조용히 소멸" 처리. 재료가 없으면 유닛이 주문을 잡아 `OnStart`에서 `Status=Done`으로 없애버려, 플레이어 주문이 통보 없이 사라지고 헛걸음까지 하던 문제.

### 정책: 없애지 말고 큐에서 대기 → 재료 입고 시 자동 재개 (알림 없음, 상태로만)
- **`JobBase.IsReady`(virtual, 기본 true)**: 작업이 지금 수행 가능한지 게이트. `JobManager.RequestJobInternal`이 `!IsReady` 잡은 배정 스킵(큐엔 유지) → 조건 충족 시 자동 재개.
- **`CraftJob.IsReady`** = `_table.CanCraft(_recipe)`(재료 충분). 재료 없는 제작 주문은 배정 안 되고 대기.
- **`CraftJob.OnStart` 레이스 처리**: IsReady로 걸러도 예약~시작 사이 타 제작자가 재료를 소진할 수 있음(주문 N개 vs 재료 1인분). 이때 `Status=Done`으로 없애는 대신 `_table.QueueOrder(_recipe)`로 **동등 주문 재등록** → 주문 보존, 재료 도착 시 재개. 재등록분은 IsReady=false라 대기(루프 없음).
- **`WorkNode`**: `job.Start()` 직후 `IsComplete()`면(재큐 등 즉시 종료) 헛걸음 없이 바로 놓음.

### 검증 필요 (Play Mode)
1. 재료 없이 제작 주문 → 유닛이 안 잡고 대기(주문 유지). 재료를 창고에 넣으면 자동으로 제작 시작.
2. 재료 1인분에 주문 2개 → 하나만 제작되고 나머지는 대기(소멸 X), 재료 보충 시 재개.
3. 정상 제작 회귀 없음(재료 충분 시 기존대로 즉시 제작).

---

## 2026-07-21 (2) — 방어/회피 확률 밸런스: 지수 대립 굴림 + 공격자 우위

D랭크끼리 시험 중 방어·회피 성공률이 과함. 원인 2중: ① 방어(Block)와 회피(Dodge)를 **순차 2회 굴림**해 곱산됨(파리티 50%×2 → 75% 무효). ② 선형 대립 굴림 `def/(atk+def)`이 스킬 격차를 확률에 약하게 반영(최대 공격자도 D 방어자에 38% 흘림).

### 수정 (`CombatHandler`)
- 세 굴림(근접 방어/원거리 방어/회피)을 공통 `OpposedChance(def, atk, k, bias)` = `def^k / (atk^k × bias + def^k)`로 통합.
- **지수 `defenseExponent`(k=1.5)**: 스킬 격차를 확률에 급격히 반영(B안).
- **공격자 우위 `attackerBias`(=4)**: 파리티 기본 확률을 `1/(1+bias)`로 끌어내림(50→20%). "기본 확률이 높다" 해소.
- 둘 다 `SerializeField` — Play Mode 튜닝. shieldBonus는 지수 전 방어 base에 가산, `DefenseChanceMod`는 사후 배율로 유지.
- **F- 값 바닥 1** (`StatGrade.ToValue` = `Mathf.Max(1, idx×5)`): F-(값 0)이면 방어=0이라 `OpposedChance` 가드로 무방비였음. 값을 만드는 유일한 소스인 `ToValue`에서 바닥을 깔아 F-도 동급 파리티 확률(20%)을 받음(하류 `SetStat` 클램프 아님 — 등급 스케일 차원의 정직한 수정). 값 1도 `FromValue`상 F- 표시 유지.

### 결과 (per-roll → 합산 무효율, k=1.5 bias=4 기준)
| 공격 vs 방어 | 기존(선형) | 변경 |
|---|---|---|
| 동급 파리티 (F ~ SSS+) | 75% | **36%** |
| 70 vs 35 (C+ vs D) | 55% | **약 16%** |
| 130 vs 35 (max vs D) | 38% | **약 7%** |
| 방어 2배 우위 | — | 여전히 잘 막음 |

### 검증 필요 (Play Mode)
- 동급 전투에서 적중률 체감 상승 확인. bias/exponent 인스펙터 튜닝으로 원하는 곡선 조정.

---

## 2026-07-21 — 기절 중 이동 버그 수정: PawnMover에 KO 게이트

07-19(2) 백로그 "기절 중에도 이동함" 처리. 기절 게이트가 `UnitBrain.Update`(BT)에만 있어 BT 밖 이동 명령(`MoveController` 우클릭 → `SetMoveTarget`)이 살아 있던 구멍.

### 수정 (`PawnMover`)
- `UnitBrain` 참조 저장(`SetStats`에서 캐시) + `IsKnockedOut` 프로퍼티(`_brain.Blackboard.IsKnockedOut` 안전 접근).
- **`SetMoveTarget` 거부**: 기절 중 호출 시 즉시 return — 우클릭 등 BT 밖 명령 차단. 기상하면 `IsKnockedOut`이 풀려 복귀 이동은 정상.
- **`Update` 조향 게이트**: 기절 상태면 남은 목표를 `StopMovement`로 정리하고 조향 스킵(KO 직전 타이밍에 걸린 목표 방어). 중력·낙하 안전망은 그대로 동작.
- 적 클릭 타겟팅(`Blackboard.Target` 직접 세팅)은 combat 경로라 BT 게이트(`UnitBrain.Update` line 106)가 이미 차단 — 손대지 않음.

### 검증 필요 (Play Mode)
- 방랑자 기절 → 우클릭 이동 명령해도 안 움직임(눕는 포즈 유지) → 기상 후 우클릭 이동 정상 복귀.

---

## 2026-07-19 (2) — 기절 회복 시스템 (켄시식): 영구 기절 버그 해소

07-11 백로그 "기절 영구" 처리. 기절은 걸리기만 하고 해제 코드가 없었음 + 출혈 페이싱(기존 1~5/s, 20~100초 사망)이 너무 빨랐음.

### 설계 (켄시식 상태 기반)
- **기상 조건** (`BodyPartSystem.UpdateKnockOutRecovery`): 최소 기절 시간(`minKnockOutSeconds` 20s) 경과 + 코어 부위(머리/가슴/복부) 전부 HP > 0 + 혈액 ≥ `wakeBloodRatio`(25%). BloodCap이 기준 밑까지 깎였으면 캡까지 찼을 때 기상 허용(기절 중엔 침대 못 가는 교착 방지). `OnRecovered` static event 추가.
- **자연 지혈**: 부상 후 `clotSeconds`(30s)에 걸쳐 출혈 선형 감쇠, 재피격 시 리셋(`BodyPart.LastWoundTime`). 단일 부상은 출혈만으론 안 죽고 연타당할 때만 위험. BleedRate도 하향: 머리 3→0.5 / 가슴 5→0.8 / 복부 2→0.4 / 팔다리 1→0.2.
- **자연 회복** (`UpdateRegen`): 혈액 0.2/s(BloodCap까지) + 부위 HP 0.25/s(파괴 부위 제외 — 파괴는 영구). 부위 회복이 기상 조건과 절뚝임 해제를 성립시킴.
- **기절 부수효과 차단** (`KnockOut`): BT 정지로 못 끄던 `CombatHandler.ExitCombat()` + `PawnMover.StopMovement()` 직접 호출 — 쓰러진 채 매 프레임 공격하던 구멍 해소.
- **침대 연동** (`RestNode`): 부위 HP 회복 추가(`PartHealRate` 1/s, 자연 회복에 얹힘), 휴식 트리거/퇴장 조건에 `HasRecoverableDisabledPart`(파괴 제외 기능상실 부위) 반영.
- **피격 혈액 손실 (2차 — 켄시 축 완성)**: 베기만 혈액을 깎던 것 → **모든 피해가 혈액을 깎음**(베기 0.5× / 둔기 0.1× / 그 외 0.25×). 혈액 20% 미만 → **과다출혈 기절**(강인함 무관, `CheckBloodState` 공통 판정 — 피격/출혈 양쪽). 기상 기준(25%)이 기절 임계(20%)보다 높아 기상 직후 재기절 없음.
- **기절 재판정 + 확정 기절**: 이미 기능 상실한 코어 부위(머리/가슴/복부)를 또 맞으면 `TryKnockOut` 재굴림(기존엔 0을 처음 넘는 순간 한 번만 판정). 코어 부위 HP가 `coreKoDepth`(-10) 이하로 깊어지면 **무조건 기절** — 굴림은 0 근처에서 버티는 용도로만. 강인함 저항은 최대 90% 캡(스탯 0~130이라 100+에서 기절/비틀거림 완전 면역이던 것 수정).
- 수치는 전부 `BodyPartSystem` 인스펙터 SerializeField — Play Mode 튜닝 가능.

### 3차 — Play Mode 테스트 피드백 반영 ("기절을 못 봄" 원인 3중)
- **혈액 음수 허용 + 사망 분리**: 강한 타격이 기절 임계(20%)를 건너뛰고 혈액 0 → 즉사하던 것 수정. 켄시식 — 혈액은 음수로 내려가며 `deathBloodRatio`(-30%)에 닿아야 사망. 기절(20% 미만)→빈사(음수)→사망(-30%) 구간이 생김.
- **기절 유닛 타겟팅 제외** (`BTTargetUtil.IsAttackable`): 적이 쓰러진 유닛을 계속 때려 KO가 수 초 만에 사망으로 이어지던 것 → 기절 유닛은 자동 타겟에서 제외(다른 대상으로 전환). KO가 사형선고가 아니라 회복 가능한 상태가 됨. 부작용: 플레이어 지정 공격도 기절 대상은 못 잡음(확인 사항).
- **눕는 포즈**: 기절 시 Z축 90도 회전, 기상 시 복구 — 기절이 눈에 보이게 됨(CharacterController 캡슐은 회전 무시라 충돌 무영향). 애니메이션 도입 전 임시.
- **`UnitHPUI` MissingReferenceException**: 선택 중이던 유닛이 사망(Destroy)하면 파괴된 `PawnMover` 참조로 매 프레임 예외 → null 가드 + 패널 닫기.

### 검증 필요 (Play Mode)
1. 유닛 기절시키기(코어 부위 기능상실 or 혈액 20% 미만) → 20초+ 후 조건 회복되면 스스로 기상, 기상 후 BT 정상 재개(작업/전투 복귀)
2. 기절 중 공격 안 함 / 이동 정지
3. 출혈: 한 대 크게 맞고 방치 → 30초쯤 뒤 출혈 멎고 혈액 서서히 회복
4. 다리 기능상실 절뚝임 → 시간 지나면(또는 침대) 정상 보행 복귀
5. 팔다리 파괴(-100)는 회복 안 됨(영구) + 그 상태로 침대에서 나옴(교착 없음)
6. 지속 전투에서 혈액이 매 타격 감소 → 20% 밑에서 쓰러짐(사망 아님), 죽는 건 혈액 0 도달 시만

---

## 2026-07-19 — 공허 경로 차단 검증 중 발견: 좁은 다리에서 유닛이 우회 안 하고 밀어붙임

07-18(2) 검증에서 항목 2·3 통과, 항목 1(좁은 다리 우회) 실패 — 유닛이 다리 근처에서 공허 방향으로만 밀며 전진 못 함. 원인 2중.

### 원인·수정
- **도착 셀 unwalkable 시 즉시 null** (`PathGrid.FindPath`): 메소 웨이포인트(2m 셀 중심)가 좁은 다리 옆 공허 타일에 앉으면 L1 도착 셀이 unwalkable → 경로 실패 → `ReplanLocal` 직선 폴백이 공허로 직진 → 낭떠러지 가드 정지 → BT 재명령 무한 반복. → `FindNearestWalkable`(링 탐색, 반경 4셀)로 도착점을 근처 walkable 셀에 스냅 후 계속 탐색. 벽 부풀림으로 막힌 도착점(벽 옆 작업 대상)도 같이 개선됨.
- **낭떠러지 가드 오발** (`PawnMover`): 유효한 로컬 경로를 따라 좁은 다리를 걸을 때도 0.6m 전방 샘플이 대각 공허 타일을 찍으면 `StopMovement`로 이동 전체가 죽음. → 가드를 설계 의도대로 **직선 폴백일 때만**(`_localPath == null`) 발동. 로컬 경로는 이미 바닥 게이트를 통과했으므로 이중 검사 불필요.

### 검증 (✅ 전부 통과)
Play Mode: 좁은 다리 우회 / ㄱ자 다리 페인트 직후 코스 즉시 반영(기즈모로 메소 라우트 확인) / 바닥 철거 후 다른 모양 재설치도 반영 / 필드 씬 이동 회귀 없음. **07-18(2) 검증 5항목 전부 완료 — 공허 경로 차단 작업 마무리.**

---

## 2026-07-18 (2) — 공허 경로 차단: 길찾기·AI가 바닥 밖을 못 밟게

floor 3차의 남은 한계("길찾기는 공허를 막힌 타일로 안 봄") 해소. 필드 맵(FloorRequired=false)은 전부 무영향.

### 수정/추가
- **`MapManager.IsStandable(tile/world)`**: 공허 게이트 헬퍼 — `FloorRequired`면 바닥 필수, 아니면 항상 true. 점유(IsPassable)와 별개 축.
- **L1 로컬 윈도우** (`LocalGridManager`): ① 윈도우 원점을 정수 좌표에 스냅 — 1m 셀이 맵 타일과 정확히 겹쳐 게이트가 타일 단위로 정확. ② 바닥 없는 셀 unwalkable → Theta* 경로가 바닥 위로만 나옴 (바닥 다리 따라 우회).
- **메소 격자** (`HierarchicalPathfinder.BakeCell`): `floorGate` 파라미터 — 2m 셀이 덮는 타일에 바닥이 하나도 없으면 unwalkable. 관대한 판정(하나라도 있으면 통과)으로 좁은 바닥 다리 코스 유지, 정밀 차단은 L1 담당. L2(100m)는 미적용(타일 스캔 비용 무의미). `RebakeObstacleAt`도 floorGate 유지(건물 재베이크가 게이트를 풀지 않게).
- **바닥 변경 반영**: `MapManager.OnFloorChanged` 구독 → 해당 타일이 속한 메소 셀 1개만 재베이크 (바닥은 반경 부풀림 없음 — 스타터 400타일 일괄 등록도 감당). Floor는 OnEnable(-45) 등록이라 파인더 Start 초기 베이크보다 항상 먼저.
- **`PawnMover` 낭떠러지 가드**: 이동 프레임마다 `ledgeLookahead`(0.6m) 앞 타일이 바닥 밖이면 `StopMovement` — **경로 실패 시 직선 폴백**(`ReplanLocal`)이 공허로 직진하던 최종 구멍을 막는 예방선. 현재 타일이 바닥일 때만 발동(바닥 밖 유닛의 복귀는 안 막음). 낙하 안전망(fallRecoveryY)은 최후 보루로 유지.
- **AI 목적지 선정**: `NpcWanderNode` 배회 목적지가 공허면 재추첨, `BTLeafNodes.GetAdjacentPassable` 작업 접근 타일에 standable 검사 추가.

### 검증 필요 (Play Mode)
1. 바닥 두 섬을 좁은 다리로 연결 → 유닛이 다리 따라 우회(직선 아님)
2. 연결 안 된 섬으로 이동 명령 → 가장자리에서 정지 (낙하 X)
3. NPC/배회 유닛이 공허로 안 걸어감
4. 바닥 페인트 직후 그 위로 경로 즉시 생김 (OnFloorChanged 재베이크)
5. 필드 씬(Scene 지형) 이동 회귀 없음

---

## 2026-07-18 — floor 3차 검증 완료: "바닥 없어도 건설됨" 원인 = 씬 설정

07-17 재테스트 실패의 원인 진단·해결. **코드 문제 아님.**

### 원인
Scene1을 지형/유닛 비활성화로 공허 상태로 만들었으나, ① MapManager `requireFloor`가 꺼져 있었고(07-17 플래그 추가 전에 저장된 씬) ② `Create Dungeon Starter Floor`를 실행하지 않아 Floor 0장 → `FloorRequired == false`로 바닥 게이트가 통째로 비활성. 배치/판정 코드는 전부 정상이었음.

### 조치
- Scene1.unity `requireFloor: 0 → 1` (파일 직접 수정).
- 에디터에서 `DungeonColony → Create Dungeon Starter Floor` 실행(사용자).

### 검증 (✅ 통과)
Play Mode: 공허에 건물 배치 거부(빨간 프리뷰), 스타터 바닥 위 건물 배치 정상.

### 교훈
씬 직렬화 플래그를 추가하면 **기존 씬은 기본값으로 남는다** — 코드 완료 ≠ 씬 반영. 던전형 씬 셋업 체크리스트: requireFloor 켬 + 스타터 바닥 실행.

---

## 2026-07-17 — 던전 건설 완결 (floor 3차): 공허 씬 지원

07-10 floor 1~2차의 마무리. "공허(지형 없는 던전 씬)에서 바닥 깔기 → 그 위 건설"이 실제로 돌게 하는 조각들.

### 수정/추가
- **공허 배치 조준** (`BuildModeManager.UpdatePreview`): 물리 레이캐스트 실패 시 y=0 평면 교차로 폴백 — 콜라이더 없는 공허에도 바닥 미리보기/배치 가능. 지형 씬은 레이캐스트가 항상 먼저 잡히므로 무영향(하늘 조준 시에만 폴백).
- **바닥 드래그 페인트** (`UpdateGridPreview`): `IsFloor` 선택 시 좌클릭 유지로 연속 배치(림월드식). 이미 깔린 타일은 `CanPlaceFloor`가 걸러 중복 없음. 건물은 기존 단클릭 유지.
- **공허 낙하 안전망** (`PawnMover`): 접지 시 위치 기억, `fallRecoveryY`(-10) 아래로 떨어지면 마지막 접지 위치로 복귀 + 이동 중지. 경로가 바닥 사이 공허를 가로지르거나 배회 AI가 공허 지점을 골라도 유닛 소실 없음.
- **`Editor/DungeonStarterCreator.cs`** (`DungeonColony → Create Dungeon Starter Floor`): 코어(없으면 선택/원점) 주변 20×20타일 코어바닥 슬래브 생성. 단일 슬래브 + `Floor`(autoFromRenderer)로 400타일 등록, BoxCollider가 유닛 접지 + 배치/이동 레이캐스트 대상. 중심을 정수 좌표에 스냅해 타일 경계 정렬.

### 후속 수정 — "바닥 없어도 건설됨" 구멍 2곳 (Play Mode 테스트 발견)
- **자유 배치(Tab) 바닥 미검사**: `IsBuildableFree`에 바닥 게이트가 없어 그리드만 막히고 자유 모드는 공허에 지어짐 → `FloorRequired && !IsFloored` 검사 추가.
- **바닥 0장 씬 게이트 꺼짐**: 바닥 요구가 `_floored.Count > 0` 자동 스위치뿐이라 빈 공허 씬은 어디든 지어짐(평면 폴백으로 조준도 됨) → **`MapManager.requireFloor`** 직렬화 플래그 추가. `FloorRequired = requireFloor || 바닥 존재`. 던전 씬은 인스펙터에서 켜기(`Create Dungeon Starter Floor`가 자동으로 켜줌). 기존 필드 씬은 기본 false — 무영향.
- **바닥은 그리드 강제**: 자유 모드에서 바닥 선택 시 회전·비정렬 배치로 타일 등록이 어긋남 → `IsFloor`면 자유 모드여도 그리드 배치 경로로 강제.

### 결정
- **바닥 전용 렌더러(OnFloorChanged 구독) 폐기** — 바닥이 렌더러 있는 슬래브 프리팹이라 전용 렌더가 이중 표시. `OnFloorChanged` 이벤트는 유지(후속 소비자용).
- 이동 명령은 공허로 안 나감(기존 `MoveController`가 콜라이더 히트 요구 — 의도적으로 평면 폴백 안 넣음). 스타터 바닥은 `PlacedBuilding` 없음 → 철거 불가(메인 코어와 동일 정책).

### 한계
- 길찾기는 공허를 막힌 타일로 안 봄 — 떨어진 바닥 섬 사이 경로가 공허를 가로지를 수 있음(낙하 안전망이 복구는 함). 바닥 섬은 이어 붙이는 걸 전제. floored 기반 경로 차단은 후속.

### 검증
1차 Play Mode 테스트에서 "바닥 없어도 건설됨" 발견 → 구멍 2곳 수정(위 후속 수정). 재테스트에서도 실패 — 07-18 진단 결과 코드 문제 아님(씬 설정 누락, 아래 07-18 엔트리 참조). 설정 후 검증 통과.

---

## 2026-07-11 — 전체 코드 리뷰: 버그 수정 1차

핵심 시스템(전투/AI/작업/창고/건설/상인) 전수 리뷰. 명백한 버그는 즉시 수정(아래), 설계 판단이 필요한 항목은 백로그로.

### 수정 완료 — 자원/아이템 증발 계열
- **`Storage.Consume` 부분 차감**: 총량 확인 없이 창고를 돌며 차감 → 부족하면 이미 뺀 자원 증발. `ConsumeByItem`처럼 `GetTotal` 선확인 추가 (소환/건설 실패 시 자원 손실 해결).
- **`BuildModeManager.Place` 순서**: 자원 소비 후 프리팹 null 체크 → 프리팹 미연결 시 자원만 소실. 프리팹 확인을 소비보다 먼저로 이동. + `Keyboard.current` null 가드.
- **`HaulJob` 아이템 소실 2곳**: 수거 시 인벤에 못 들어간 잔량을 노드에 반환(안 되면 드롭), 납품 시 `removed == _amount` 전량 일치 요구 폐지 → 실제 꺼낸 수량 기준 창고→폴백창고→인벤복귀→드롭 순 보존.
- **`Merchant.TryBuy`**: ① `entry.Quality` 미전달로 구매 장비가 일반 등급으로 입고되던 것 수정, ② 아이템 입고를 먼저 확정한 뒤 골드 차감(창고 만석 시 골드 손실 방지). **`TrySell`**: 골드 입고 선확정 → 실패 시 원복 후 아이템 제거 (창고 만석 시 아이템 증발 방지).
- **`Equipment` 인스턴스 불일치**: `FindInInventory`(앞에서 탐색) + `Remove(data,1)`(뒤에서 차감) 조합으로 표시 등급과 실제 소모 아이템이 어긋나던 것 → `RemoveOne(inst)`로 통일 (무기/오프핸드/갑옷 3곳).
- **`EatNode`**: 창고에서 꺼낸 음식이 인벤 만석 시 증발 → 창고 반환 후 포기.

### 수정 완료 — 전투/AI
- **시체 무한 타겟팅**: `BecomeCorpse`에서 `Faction` 제거 + `BTTargetUtil.IsAttackable`(파괴/비활성/시체 제외) 신설, `DetectNode`/`FightNode` 타겟 유효성에 적용. 시체 대상 무한 공격·XP 파밍 차단. `DetectNode` myFaction null 가드도 추가.
- **원거리 NRE**: `CombatHandler.Update`가 BT 틱 사이 파괴된 타겟으로 `TryFireRanged` 호출 → null 가드 2중 추가.
- **신성 번개 `ExtraHit` 이중 피해**: BodyPartSystem과 UnitBrain 폴백 HP 양쪽에 피해 적용되던 것 → if/else로 수정.

### 수정 완료 — 기타
- **`ResourceNode` 점유 잔류**: Awake에서 막은 타일을 `OnDestroy`에서 해제 (노드 소멸 자리 영구 건설 불가 방지).
- **`SelectionManager`/`MoveController`**: 선택 중 파괴된 유닛 참조로 MissingReferenceException 나던 것 가드. `SelectByRect` 카메라 뒤 유닛(z≤0) 선택 제외.
- **`CraftJob` 타이머**: `OnStart`(수주 시점)가 아닌 작업대 도착 후 첫 `Execute`에서 시작 — 이동 시간이 제작 시간을 공짜로 깎던 것 수정.
- **주석 인코딩 복구**: `JobBase.cs` / `BTLeafNodes.cs` / `PawnMover.cs`의 CP949 깨짐(`����`) 주석을 UTF-8 한글로 재작성. CLAUDE.md의 `1 Script` → `1_Script` 경로 오기 수정.

### 버그 백로그 (설계 판단 필요 — 논의 후 처리)
- (전 항목 처리 완료 — 2026-07-21·2026-07-26 엔트리 참조. 화상 DoT는 방어구 우회로 임시 수정, 화상/마법 전면 개편은 추후.)

---

## 2026-07-10 — 월드맵 백엔드: 정착지 등록 배선 + 오프라인 생산 + 바이옴 게이팅

설계 `docs/design_world_field.md` step 4(일부)/5/7의 **코드/데이터 부분**(UI 제외) 구현.

### step 4 배선 — 정착지 ↔ 월드맵 동기화 (`SettlementManager`)
- `Start`에서 `SyncToWorldMap()` — 사전 배치 정착지들을 `WorldMap.RegisterSettlement(node)`로 등록(이전엔 월드맵이 startNode 하나만 인지).
- `Register(Settlement)`가 월드맵 노드에도 즉시 등록. 런타임 코어 박기용 `RegisterAt(node, root, name)` 편의 진입점 추가.
- `ActiveBiome` 프로퍼티 — 활성 정착지 노드 → `WorldMap` 바이옴 (step 7 게이팅용).
- ※ "도착→멤버 스폰"은 기존 `WorldMapView.EnterSettlement`에 이미 구현됨.

### step 5 오프라인 생산 틱 (`SettlementManager`)
- `GameTimeManager.OnDayChanged` 구독 → 비활성 정착지마다 `ProduceOffline`.
- 데이터 지정 `OfflineYieldEntry{ResourceType, PerDay}`(정착지별 인스펙터 리스트)를 그 정착지 창고에 누적. 비활성 루트라 `Storage.All`엔 없으므로 `GetComponentsInChildren<Storage>(true)`로 직접 수집, 필터(`CanAccept`)/격자 여유만큼만 입고. 활성 정착지는 실제 시뮬되므로 제외.

### step 7 바이옴 콘텐츠 게이팅 (완결)
- **`BiomeContentData.cs`** (SO, `Create → DungeonColony → BiomeContentData`): 바이옴별 `ResourceNodes` / `SummonableMinions` / `UnlockableResearch` 풀.
- **`BiomeContent.cs`** (static 레지스트리, DataManager 패턴): `Resources/Data/Biomes/`에서 자동 로드, Biome별 색인. `Get` / `ResourceNodes` / `SummonableMinions` / `UnlockableResearch` / `IsMinionAllowed`. **데이터 없으면 제한 없음** → 기존 씬 무영향.
- **소비처 배선 (실동작):**
  - 하수인 소환 — `DungeonCore.Summon`이 `SettlementManager.ActiveBiome`로 소환 불가 하수인 차단.
  - 자원 노드 — `EventManager.SpawnDiscovery`가 활성 바이옴 자원 풀(`BiomeContent.ResourceNodes`)에서 노드를 뽑아 `Prefab` 스폰 + `ResourceNode.Init(data)`. 풀 없으면 이벤트 기본 프리팹 폴백.
  - 연구 해금 — **보류**: 연구 *실행* 시스템(`ResearchJob`)이 미구현이라 소비처 없음. `UnlockableResearch` 필드만 유지, 배선은 ResearchJob 구현 시.
- **`Editor/BiomeContentCreator.cs`** (`DungeonColony → Create Biome Content Assets`): `Resources/Data/Biomes/`에 바이옴별 SO 스캐폴드 생성(Unknown 제외). 인스펙터에서 풀만 채우면 즉시 게이팅 동작.

**테스트 순서**: 위 메뉴로 에셋 생성 → 예: `Biome_Lava`에 루비 ResourceNodeData + 화염 하수인 채움 → 용암 바이옴 정착지에서 Discovery 시 루비 노드만 나오고, 타 바이옴 하수인 소환 차단되는지 Play Mode 확인.

### 던전/필드 건설 모델 A 확정 + 배선 정리
설계 결정(메모리 `project_dungeon_field_build`): **바닥=코어 무관 자유배치 substrate / 건물=바닥 위 배치, 코어 union 밖이면 휴면 / 코어=작동영역+하수인캡(건설 차단 아님).** 시작 거점=손으로 만든 첫 맵(코어+바닥 사전 배치, 절차생성 아님).
- 설계 논의 중 방향 전환: 초안은 "코어 union 건설 게이트"였으나, 바닥을 코어와 분리(모델 A)하기로 확정 → **union 건설 차단 제거**.
- **`MapManager.IsBuildable`**: 존 클램프 + 점유 + 평탄만. 코어 union 게이트 없음(union 밖은 `BuildingDormancy` 휴면이 담당). `buildZoneSize` 기본 50→200(최대 경계 클램프).
- **`BuildModeManager.IsBuildableFree`**: 자체 코어 union 체크 제거(모델 A). `IsBuildableGrid`는 `IsBuildable`에 위임(중복 정리됨).
- ※ 코어 union 건설 게이트는 원래 `BuildModeManager`에 있었으나 모델 A로 제거됨.
- **씬 주의**: `Scene1`의 `MapManager.buildZoneSize` 직렬화 값은 인스펙터에서 200으로 직접 설정(코드 기본값은 기존 직렬화 값에 반영 안 됨).

### floor(바닥) 시스템 1차 — 데이터 + 컴포넌트 + 건설 게이트
모델 A의 substrate. 바닥 배치는 **무료**(자원 비용 없음).
- **`MapManager`**: `_floored` HashSet(절대좌표 희소, 점유 패턴 미러링) + `IsFloored` / `RegisterFloor` / `UnregisterFloor` / `HasFloorSystem` + `OnFloorChanged` 이벤트(바닥 렌더링용, 후속).
- **`Map/Floor.cs`** (신규): 자기 footprint 타일을 `_floored`에 자동 등록(OnEnable)/해제(OnDisable). Renderer 바운드로 덮는 타일 자동 산출(`autoFromRenderer`) 또는 `sizeTiles`. 사전배치·런타임 공용. 바닥은 통행 안 막음(점유 X).
- **`IsBuildable` 바닥 게이트**: `HasFloorSystem`(바닥 1개 이상 존재)일 때만 "건물은 floored 위에만" 강제. **바닥 없는 필드/지형 맵·현 Scene1은 무영향**(자동 스위치).
### floor 시스템 2차 — 바닥 placeable (직접 바닥 까기)
- **`BuildingData.IsFloor`** 플래그 추가: 바닥 substrate 표시(코어 무관, 바닥요구 우회, 휴면/장애물 처리 안 함).
- **`MapManager.CanPlaceFloor`**: 바닥 배치 판정(존 + 아직 바닥 아님 + 점유 + 평탄). 건물과 달리 "바닥 위에만" 요구 없음.
- **`BuildModeManager`**: `IsBuildableGrid`가 `IsFloor`면 `CanPlaceFloor`로 분기. `Place`에 바닥 분기 — Instantiate + `Floor` 컴포넌트 보장(안전망) + `PlacedBuilding.Attach`(철거 지원, 환불 0). 휴면(`BuildingDormancy`)·`RebakeObstacle` 안 함(바닥은 코어 무관·통행 허용).
- **`Editor/FloorCreator.cs`** (`DungeonColony → Create Floor Prefab + Data`): 바닥 프리팹(1×1 슬래브 + `Floor` + BoxCollider) + `FloorMat.mat` + `BuildingData`(IsFloor·무료) 자동 생성 후 씬 `BuildModeManager.placeableBuildings`에 추가. → 메뉴 1회 실행이면 빌드모드에서 바로 바닥 선택·배치 가능.
- **남은 (floor 3차)**: 바닥 지오메트리 렌더(공허에 바닥 보이게, `OnFloorChanged` 구독). 첫 던전 맵은 씬에 `Floor` 사전 배치(저작).

**검증**: (1) 바닥 placeable로 공허에 바닥 깖 → 그 위에만 건물 배치되고 바닥 밖은 빨강. (2) 바닥끼리 겹쳐 못 깖. (3) 바닥 없는 기존 씬 건설 그대로(회귀 없음). (4) 바닥 철거 시 그 타일 위 건물 배치 불가로 돌아가는지.

### 남은 작업
- **step 4 런타임 플로우**: 빈 노드 진입 → 임시 원정 필드맵 생성 → 코어 박기 → 정착지 승격. 필드맵 런타임 생성/`SettlementSnapshot`(B vs C 결정) 필요 — 설계 `design_world_field.md` 2026-06-20 노트 참조. 현재는 사전 배치 정착지 등록 훅만.
- **step 5 확장**: 식량 소비/추상 방어 판정(식량은 `FoodData`라 `ResourceType` 경로 밖 — 별도 플러밍 필요).
- **데이터 채우기**: `Resources/Data/Biomes/`에 바이옴별 `BiomeContentData` 에셋, 정착지별 `OfflineYield` 인스펙터 설정.
- **UI**: 파티 편성 패널, 등짐 A/B 뷰 토글(step 6), 월드맵 3D 지형 뷰.
- **Play Mode 검증**: OnDayChanged 오프라인 누적 동작, 바이옴 게이팅으로 소환 차단, 정착지 전환 시 ActiveBiome 갱신.

---

## 2026-06-18 — 월드맵+필드맵+다중 정착지 토대 (설계 + 코드 1차)

설계: `docs/design_world_field.md`. 켄시식 단일 심리스 맵 폐기 → 림월드식 월드맵(추상 바둑판) + 필드맵(200×200 바운디드) + 다중 정착지(전환형). **거점 중심 모험 게임**.

### 신규 파일 (`Assets/1_Script/World/`, 전부 additive — 기존 코드 무영향)
- **`Biome.cs`** — Biome enum(평원/숲/사막/설원/용암/동굴/늪) + `DisplayName`.
- **`WorldMap.cs`** — 추상 바둑판 데이터. `WorldTile{Biome, Content, Discovered}` 2D 배열, 안개(Discover), 내 정착지 목록, 4방 인접/맨해튼 거리, `OnNodeChanged`. 시작 노드 = 내 정착지 + 주변 공개.
- **`ExpeditionParty.cs`** — 원정 파티 모델(멤버=UnitBrain 리스트, 현재 노드). 노드 기반(일수 폐기).
- **`PartyCarry.cs`** — 등짐 적재 로직. 멤버/파티 무게 집계(`Inventory.GetTotalWeight` 재활용), 용량=Strength 파생(켄시식), B뷰 공용풀 자동분배(`TryAddToParty`).
- **`SettlementManager.cs`** — 정착지 전환형. 루트 GameObject 활성 토글(인메모리 보존, 직렬화 불필요), 전환 시 싱글톤 일치(MapManager/DungeonCore), `OnActiveChanged`.
- **`WorldMapView.cs`** — **마운트앤블레이드식 실시간 월드맵**(추상 UI 격자 폐기). **지도면 1장**(노드별 바이옴 색을 Texture2D에 칠해 Bilinear 블렌딩 → 지도 느낌, 바둑판 아님; 안개=어두움, 발견 시 텍셀 갱신으로 드러남) + 정착지/POI 큐브 마커. 파티 토큰이 노드 그래프 BFS로 실시간 이동(지도 클릭→그 노드까지), **같은 씬 카메라/루트 토글**(M키), 정착지 정박 중 Enter 진입.
- **`Editor/WorldMapUICreator.cs`** — `DungeonColony → Create World Map View`. 탑다운 월드 카메라 + mapRoot(먼 곳) + WorldMapView 연결 + 매니저 GO 자동 생성.

### 기존 코드 변경
- **`DungeonCore`**: `RelocateTo`에서 그리드 recenter 제거(필드맵 고정 의미) + `SetAsActive()` 추가(전환 시 활성 코어 재지정).
- **`Storage`**: `All` 레지스트리를 `Awake/OnDestroy` → `OnEnable/OnDisable`로. 비활성 정착지(루트 SetActive false) 창고를 집계/탐색에서 자동 격리. 항상 활성이면 기존과 동일.

### 남은 작업 (Editor/Play Mode 필요 — 코드만으로 불가)
- 월드맵 바둑판 **뷰 UI**, 파티 편성 **패널**, 등짐 A/B 뷰 토글 **UI** (프리팹/씬).
- `MapManager.buildZoneSize` 인스펙터 200 설정. 정착지 루트 GameObject 구성.
- **Play Mode 검증**: Storage 레지스트리 변경 회귀(창고 집계/소비/운반 동일한지) 우선 확인.
- (step 4 정착지 등록 배선 / step 5 오프라인 생산 / step 7 바이옴 게이팅 코드 → 2026-07-10 완료)

---

## 2026-06-07 — 지형 샘플 회귀 수정 + 건물 철거(데몰리시) 기능

### 1. step 2 회귀 수정 (빨간 미리보기 + 건물 바닥 박힘)
- 평지(y=0) 씬에서 `SampleGroundHeight`가 `groundMask=Everything(~0)`로 아래를 레이캐스트 → 건물/코어/유닛 콜라이더를 지면으로 오인 → 코너 높이 들쭉날쭉(평탄도 빨강) + 배치 Y 엉뚱(박힘).
- 수정: **Terrain.activeTerrain 우선**(지면만 샘플, 안전) → groundMask가 **특정 레이어**일 때만 레이캐스트 → Everything/Nothing이면 평지(y=0) 처리. 기본값 `groundMask=0(Nothing)`. 평면 바닥 씬은 비워두면 원래 y=0 동작 복원.

### 2. 건물 철거 (빌드모드 내) — step A
- **`PlacedBuilding.cs` 신규** (`core/`): 배치 시 원본 `BuildingData` 보관(`Attach`), `Demolish()` = 점유 타일 해제 + `RebakeObstacleAt` + **건설비 75% 환불**(`DataManager.GetResourceItem`로 ResourceType→ItemData, `DroppedItem.Spawn`으로 철거 위치 드랍→자동 운반) + 파괴. `All` 레지스트리 + 철거 예정 빨간 틴트(`SetPendingHighlight`, MaterialPropertyBlock).
- `BuildModeManager`: **철거 서브모드**(`X` 토글). 클릭=커서 위 단일, 드래그=화면 박스 내 일괄. 호버/박스 대상에 빨간 예정 표시, 릴리스 시 철거. OnGUI 라벨 + 빨간 드래그 박스.
- 배치 훅(`BuildModeManager.Place` / `BuildingBlueprint.Complete`)에 `PlacedBuilding.Attach` 연결. `DataManager.GetResourceItem(ResourceType)` 추가.
- 메인 던전코어는 `PlacedBuilding` 없음 → 철거 불가(안전). 서브코어는 철거 시 현재 75% 자원 환불(코어 아이템 드랍은 step C).

### 3. 드랍 아이템 모델
- `ItemData.WorldModel`(GameObject 프리팹) 필드 추가. `DroppedItem.Spawn`이 있으면 해당 모델 인스턴스, 없으면 기존 0.25 큐브 폴백. 모델 콜라이더는 제거(픽업은 부모 SphereCollider 트리거가 담당).
- **TODO:** 각 `ItemData` 에셋의 `WorldModel`에 모델 프리팹 연결(비면 큐브).

### 한계 / 남은 작업
- **씬 사전 배치 건물**은 `PlacedBuilding`/`BuildingDormancy`가 없어 철거/휴면 미적용 → 프리팹/씬에 컴포넌트 추가 필요(런타임 배치는 자동).
- **step B (UI [철거] 버튼)**: 건물 선택 패널(StorageBuildingUI 등 다수)에 버튼 필요. 버튼 배치는 Unity 프리팹/에디터 작업이라 코드만으로 불가 → `pb.Demolish()` 호출 연결 + 에디터 스크립트 또는 수동 배치 필요.
- **step C (서브코어 코어 아이템화)**: 철거 시 자원 대신 "코어 아이템" 드랍 → 인벤토리/배치로 재설치. 별도 ItemData + 아이템→빌드모드 배치 플로우 설계 필요.

### 검증 (미완 — Unity Editor Play Mode 필요)
1. 컴파일 통과
2. 평지 씬: 건설 미리보기 초록 정상 + 건물 안 박힘(회귀 해소)
3. 빌드모드 `X` → 철거 모드, 건물 호버 시 빨강, 클릭/드래그 철거 + 75% 자원 드랍 확인
4. 벽 철거 후 길찾기 재개통(메소 재베이크), 서브코어 철거 시 영역 축소 + 밖 건물 휴면

---

## 2026-06-06 — 핵 영역(크립) 시스템 step 1~2: 점유 희소 저장 + 온디맨드 지형 샘플

설계: `docs/design_core_creep.md`. "핵 이동 시 빌드존 리셋"과 경사지 부유 버그의 근본 원인 제거.

### step 1 — 점유 희소 저장 (그리드/영역 분리)
- `MapManager`: `TileData[50,50] _tiles` 배열 + `ClearTiles()` 제거 → **`HashSet<Vector2Int> _occupied`** (절대 월드 타일좌표) 희소 저장으로 교체. 막힌 타일만 등록, 비면 통행 가능이 기본값.
  - `GetTile`/`SetTile`을 점유 집합 기반으로 재작성(빌드존 경계와 무관, 절대좌표). `SetTile`은 실제 변경 시에만 `OnTileChanged` 발동.
  - `RecenterAt`: **`ClearTiles` wipe 제거** — origin(라우팅용 중심)만 이동, 점유는 유지. (설계 결정 3)
  - `TileToWorld`: `_tiles[].Height`(항상 0) 의존 제거 → Y=0 평면 반환(길찾기용, 고빈도).
- `GridRenderer`: `OnZoneRecentered` 구독 제거 → 핵 이동 시 Blocked 오버레이를 더 이상 wipe하지 않음(점유가 절대좌표로 유지되므로 시각도 유지).

### step 2 — 온디맨드 지형 평탄도 샘플 + 경사지 부유 버그 수정
- `MapManager` 지형 샘플 헬퍼 추가(거대 맵에 절대 그리드 저장 없이 필요 시만 샘플):
  - `SampleGroundHeight(x,z)` — 레이캐스트(`groundMask`) 우선 → Terrain → 0 폴백.
  - `TileToGround(tile)` — 타일 중심을 지면에 스냅한 월드 좌표(배치/시각화용, 길찾기용 `TileToWorld`(Y=0)와 분리).
  - `IsFlatEnough(footprint)` — footprint 바운드 4코너 지면 높이차 ≤ `buildHeightTolerance` 판정(울퉁불퉁 지형 자동 반영). `IsBuildable`이 점유/존 + 이 평탄도까지 검사.
- `BuildModeManager`: 그리드 고스트/배치를 `TileToGround`로 지면 스냅(이전엔 Y=0 부유). 자유 배치는 기존 레이캐스트 Y 유지.
- `GridRenderer`: Blocked/Preview 오버레이 Y를 `SampleGroundHeight`로 산출 → 경사지 부유/잠김 해결.
- 잔여: `TileData.Height`/`.Normal` 필드는 이제 미참조(미사용). 구조체 자체는 변경 최소화 위해 유지.

### step 3 — 핵 변경 이벤트 → 고아 건물 휴면/재가동 (시각 표시 step 4 포함)
- `DungeonCore.OnTerritoryChanged` static event + `RaiseTerritoryChanged()`. 발동: `Start`(초기 영역 확정), `RegisterSubCore`/`UnregisterSubCore`(실제 변경 시에만). 핵 이동 액션은 step 5에서 연결.
- **`BuildingDormancy.cs` 신규** (`core/`): 영역(union) 밖이면 휴면. `OnTerritoryChanged`+`OnEnable`에 재평가, `IsWithinAnyRangeWorld(transform.position)`로 판정. 핵 없으면 가동 유지(보수적).
  - 작동 정지: `FarmPlot`/`CraftingTable`/`WaterSource`(+인스펙터 `suspendOnDormant` 목록, 예: Bed)를 `enabled=false` → Update 일감 등록·레지스트리 자동 해제, 재가동 시 복원(가역).
  - 시각: 모든 Renderer를 `MaterialPropertyBlock`(_BaseColor/_Color)으로 회색 틴트 → 머티리얼 인스턴스 누수 없음, `SetPropertyBlock(null)`로 복원.
  - `BuildingDormancy.Ensure(go)` — 런타임 배치 건물(`BuildModeManager.Place` / `BuildingBlueprint.Complete`)에 자동 부착.
- `Storage`: 정적 `All` 집계라 enabled로 못 막음 → **`IsActive`(=`!_dormancy.IsDormant`)** 추가, `GetTotal`/`Consume`/`GetTotalByItem`/`ConsumeByItem`/`FindStorageWithType`/`FindNearest`가 휴면 창고 제외. (설계 미결 "비활성 창고 집계 제외"를 기본값으로 채택)

### 정책 채택/한계
- 비활성 건물 = 철거 X, 일감 제공 정지 + 회색 틴트. **씬에 미리 배치된 건물**은 `BuildingDormancy`가 없으면 휴면 미적용 → 프리팹/씬에 컴포넌트 추가 필요(런타임 배치는 자동).
- 휴면 전 이미 등록된 일감은 유지될 수 있음(JobManager 측 취소 로직은 후속). `CraftingTable.QueueOrder` 등 직접 호출 경로는 enabled와 무관(UI 게이팅 후속).

### 남은 단계 (design_core_creep.md)
- step 5: 메인 핵 이전(relocate) 액션 — 이동 후 `RaiseTerritoryChanged` 호출 지점만 연결하면 휴면 재평가 자동.

### 검증 (미완 — Unity Editor Play Mode 필요)
1. 컴파일 통과
2. 자원노드 점유 표시 정상, 핵 이동(RecenterAt) 후에도 점유/오버레이 유지
3. 경사지에서 건설 고스트/오버레이가 지면에 붙는지, 코너 높이차 큰 곳에서 빨강(불가) 뜨는지. `MapManager.groundMask`를 지형 레이어로 설정해야 정확
4. 서브코어 파괴/등록 시 범위 밖 건물이 회색 틴트 + 작동 정지, 범위 복구 시 재가동되는지

---

## 2026-06-04 — 종족 성장 속도 / Theta* 길찾기 전면 개편 / 장애물 회피

### 1. 종족별 스탯 성장 속도 차별화
- `UnitData.StatGrowthRates` (List<StatGrowthEntry>) 추가 — **특정 스탯만** 개별 배율(목록에 없으면 1.0). 종족 전체 일괄 배율이 아님.
- `UnitStats.SourceData`(소속 UnitData)를 `FromData`에서 세팅, 단일 성장 진입점 `GrowStat`에서 `amount *= SourceData.GetGrowthRate(stat)` 적용 → 전투/노동/마법 모든 성장에 자동 반영. 배율 0 = 성장 차단. 미니언은 `IsLocked`라 영향 없음.
- 마이너스 성장(퇴화)은 미도입(의도).
- **TODO:** 종족 에셋(예: Human.asset) `StatGrowthRates`에 실제 배율 채우기.

### 2. 길찾기: 평범한 A* → Theta*(any-angle) + 이진 힙
- `PathGrid.FindPath`를 8방향 격자 A*에서 **Theta***로 교체(`LineOfSight` 가시선 평활화 → 계단식 아닌 직선 경로). 휴리스틱 octile→유클리드(admissible), g/h 월드 단위 통일(L2/L3 greedy 버그 수정).
- open set을 선형 스캔 → **decrease-key 인덱스 이진 최소 힙**(`_heap`/`_heapPos`). O(N²)→O(N log N).

### 3. "장애물을 안 피함" 원인 2가지 수정
- **씬 `obstacleLayer` 448→128**: Player+Wall+Enemy → **Wall만**. 유닛이 자기 셀을 막아 경로가 null→직선되던 버그 해결.
- 런타임 벽이 그리드에 등록 안 되던 문제 → 아래 4의 윈도우/메소가 매번 `CheckBox(Wall)`로 직접 감지.

### 4. 유닛 중심 로컬 윈도우 + 코스 라우트 (계층 재설계)
- `PawnMover` 이동을 단일 경로 → **코스 라우트(L2/메소) 추종 + 유닛 중심 윈도우 재계획**으로 재작성. (`SetMoveTarget`/`StopMovement` API 유지 — 호출부 무수정)
- `HierarchicalPathfinder.FindLocalPath` → `LocalGridManager.FindWindowPath`: 유닛 중심 40m 1m격자를 즉석 베이크해 Theta*. 코스 웨이포인트 추월 판정(뒤로 안 감), 막다른 길 정지, 같은 목표 재지정 시 재베이크 생략(`goalEpsilon`).

### 5. 메소 격자(중간 해상도) — 긴 벽 코스 회피
- `HierarchicalPathfinder._meso`: 던전 코어 중심 `mesoAreaSize`(160m)를 `mesoTileSize`(2m)로 덮는 격자. `ComputeCoarseRoute`가 베이스 안이면 메소로 라우팅 → **윈도우(40m)보다 긴 벽도 우회**. from만 메소 안이면 경계(게이트)까지 메소 후 to 직선.
- 건물 배치(`BuildModeManager.Place`/`BuildingBlueprint.Complete`)·타일 변경 시 `RebakeObstacleAt`로 해당 영역만 재베이크. **L2 과다차단(벽1칸→100m셀) 폐기.**

### 6. 에이전트 반경 클리어런스 — "틈은 있는데 못 지나가 끼임" 수정
- `agentRadius`(0.5m)만큼 베이크 시 장애물 부풀림(CheckBox halfBox += 반경). 메소·윈도우 양쪽 적용 → 좁은 틈 자동 차단(우회), 넓은 길은 벽에서 거리 둬 콜라이더 끼임 방지.
- `PathGrid.FindPath` 시작 셀이 막혀도 탈출 허용(벽 붙은 유닛 재이동), `PawnMover`는 로컬 막힘 시 코스 전역 재계산.

### 7. 성능 / 버그 / 정리
- **윈도우 풀링**: `LocalGridManager._windowGrid` 1개 재사용(`PathGrid.Rebase`) → 재계획마다 큰 배열 할당 제거(GC).
- **절뚝임 버그 수정**: `PawnMover._mobilityMult`(SetLimping 0.5/SetCrawling 0.2)를 effectiveSpeed에 곱. 이전엔 `moveSpeed`만 바꿔 `_stats` 유닛에 무효였음.
- **레거시 제거**: `Pathfinder.FindPath`, `HierarchicalPathfinder.FindPath`, L3 격자, `LocalGridManager` 공유·클러스터 그리드 일체. `LocalGridManager`는 윈도우 전용으로 축소.
- TMP obsolete 경고: `enableWordWrapping` → `textWrappingMode = TextWrappingModes.NoWrap` (`PawnRosterCreator`, `ResourceNodePanelCreator`).

### 튜닝(HierarchicalPathfinder/PawnMover 인스펙터)
- `agentRadius`를 **유닛 CharacterController 반경에 맞출 것**(핵심). `mesoTileSize`=코스 인식 최소 문 너비(≥2m). `mesoAreaSize`=긴 벽 처리 범위. `localWindowSize`/`replanDistance`/`coarseReachDistance`/`goalEpsilon`.

### 남은 한계
- 메소 셀(2m)보다 좁은 문은 코스에서 막힘. 오목/U자 벽은 추월 판정이 코스를 건너뛸 수 있음(로컬+stuck-recompute로 자가복구). 메소 영역(160m) 밖 긴 벽은 L2(거침). 건물 철거 시 메소 복구 로직 없음(철거 기능과 함께 `RebakeObstacleAt` 호출 필요).

### 검증 (미완 — Unity Editor Play Mode 필요)
1. 컴파일 통과
2. 벽 뒤로 이동 명령 → 우회 / 좁은 틈 회피 / 벽 모서리 안 끼임 / 긴 벽 우회 확인. 기즈모: 노랑=코스(메소), 청록=로컬 윈도우
3. 유닛 많을 때 끊김(GC) 줄었는지, 절뚝일 때 느려지는지

### 기타
- **이벤트 시스템 완료 확인** — `EventManager`가 Raid/Merchant/Reinforcement/Discovery 4종 전부 구현(가중치 추첨 + `MinGameTime` 게이팅 + `OnEventTriggered` + 맵 엣지 스폰). 더 이상 미완 아님.
- **CraftJob 취소 환불 수정** — 완성 전 취소(유닛 사망 등)되면 차감된 재료를 작업대에 드롭 환불(`OnCancelled` 구현 + `_consumed` 플래그). `UnitBrain.Die`→`CancelJob`→`Cancel`→`OnCancelled` 경로로 발동. CLAUDE.md 알려진 이슈에서 제거.

---

## 2026-05-31 — ResourceNode 패널 / 개체 이름 / 초상화 / PawnRoster

### 1. NodeInfoPanel → ResourceNodePanel 리네이밍
- `Assets/1_Script/UI/ResourceNodePanel.cs` (파일·클래스명 변경, `.meta` GUID 보존)
- 씬 `Scene1.unity`의 `m_EditorClassIdentifier`도 갱신. 기능 변경 없음.

### 2. 개체 이름 시스템
- `UnitData.NamePool` (List<string>) 추가 — 소환 시 랜덤 1개를 `UnitBrain.UnitName`에 부여, 비면 `RaceName` → `gameObject.name` 폴백.
- `MinionData`/`NpcData`는 `UnitData` 상속이라 자동 적용.
- 시체(`Corpse`)·`PawnEntryUI`·로스터가 `UnitName` 사용.
- **TODO:** 각 `UnitData` 에셋의 `NamePool`을 채워야 실제 개체명이 보임(비면 종족명).

### 3. 초상화 스냅샷 (1회 캐싱)
- `Assets/1_Script/unit/PortraitCapture.cs` 신규 — 전용 **"Portrait" 레이어**로 대상 렌더러를 임시 이동 → 단색(투명) 배경 카메라로 RenderTexture 렌더 → `Sprite` 캐싱.
- `UnitBrain.EnsurePortrait()` — 로스터/배정 카드 표시 시점에 최초 1회만 캡처(안 보이는 유닛은 비용 0).
- **⚠️ 함정:** "Portrait" 레이어가 없으면 캡처가 조용히 null 반환 → `Icon` 폴백. `Create Pawn Roster UI` 에디터 스크립트가 레이어를 자동 생성하므로 먼저 실행할 것.

### 4. PawnRosterPanel 개편
- 0.5초 전체 Destroy/Instantiate 제거 → `Dictionary<UnitBrain, PawnRosterEntryUI>` 기반 **증분 갱신**(섹션 변경 시에만 reparent).
- `PawnRosterEntryUI.cs` 신규(초상화+개체명+선택 버튼).
- 레이아웃: **하단 가로 바**(좌우 스트레치), 대기중/작업중 **2행 그리드 카드**(`GridLayoutGroup`, cellSize 110×132, 초상화 96).

### 5. 자원노드 배정 목록
- `PawnEntryUI.cs`에 `portraitImage` 추가 → 초상화+개체명+해제 버튼.
- 배정 목록을 **정사각 카드 격자**로 변경(`GridLayoutGroup` 3열, cellSize 78×88, 초상화 60). 해제 "X"는 카드 우상단 코너 오버레이.

### 6. 에디터 생성 스크립트 + 컨벤션
- `Assets/Editor/ResourceNodePanelCreator.cs` (`DungeonColony/Create Resource Node Panel`)
- `Assets/Editor/PawnRosterCreator.cs` (`DungeonColony/Create Pawn Roster UI`, "Portrait" 레이어 보장 + 엔트리 프리팹 저장)
- `docs/editor_creators.md`에 컨벤션 확정:
  - 헤더 색 **#0D0D0D** 고정 + 상단 풀폭(레이아웃 그룹 밖 수동 앵커)
  - 닫기 버튼: 공용 `CloseButton.cs` 부착 + `target`=패널, 텍스트 **"X"**, 배경색 **#802626** 고정, 헤더 안에서 LayoutGroup 금지(수동 앵커)

### 검증 (미완 — Unity Editor Play Mode 필요)
1. 컴파일 통과 확인
2. `Create Pawn Roster UI` **먼저** 실행(→ "Portrait" 레이어 생성) → `Create Resource Node Panel`
3. 폰 소환 → 로스터/배정 카드에 개체명 + 얼굴 스냅샷, 자원 노드 클릭 시 패널 정상 동작

### 남은 후속
- `UnitHPUI.cs` / `UnitStatsUI.cs` 선택 폰 헤더도 `UnitName`으로 통일 검토
- `UnitData` 에셋 `NamePool` 채우기
- 배정/로스터 격자가 칸을 넘칠 때 스크롤 추가 여부

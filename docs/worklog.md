# 작업 로그

날짜별 주요 작업 기록. 컨벤션 상세는 `docs/editor_creators.md` 등 해당 문서 참조.

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
- **건물 타일 점유 비대칭**: `BuildModeManager.Place`는 점유(`SetTile`)를 등록하지 않는데 `PlacedBuilding.Demolish`는 해제함 → 같은 타일에 건물 중복 배치 가능 + 철거가 자원노드 점유까지 풀 수 있음. 건물이 타일을 점유해야 하는지(벽만? 전부?) 설계 결정 필요.
- **기절 영구**: `Blackboard.IsKnockedOut = true`를 해제하는 코드가 없음 → 기절 유닛은 영원히 정지. 회복(시간 경과/치료) 시스템 미구현.
- **화상 DoT 무효화**: `StatusEffectSystem.ApplyBurningDoT`가 매 프레임 미세 피해를 정식 파이프라인으로 보내는데, 방어구 **고정 감소**(`BodyPartSystem.TakeDamage`)가 매번 차감 → 방어구 1개만 있어도 화상 피해 0. DoT는 틱 누적 or 방어구 우회 필요.
- **허기 피해가 숨겨진 폴백 HP로**: `UnitBrain.UpdateHunger` → `TakeDamage(float)`는 `_hp`(BodyPartSystem 없는 유닛용)를 깎음. 신체 부위와 무관한 제2 체력으로 아사 — UI에 안 보임.
- **CraftJob 재료 부족 시 조용히 소멸**: `OnStart`에서 `Status = Done` 처리 → 제작 주문이 통보 없이 사라짐. 대기/재시도/알림 정책 결정 필요.
- **DroppedItem 품질 미보존**: `DroppedItem`에 quality 필드 없음 → 장비가 드롭을 거치면 등급 소실 (CraftJob 넘침 드롭, 환불 등).
- **Storage.TryAdd 부분 입고에도 true**: `Inventory.TryAdd(...) > 0` 기준이라 일부만 들어가도 성공 취급 — 나머지는 호출자가 모름. 의미 재정의 필요.
- **클릭 레이캐스트 분산**: `Storage`/`Bed`/`FarmPlot`/`Merchant`가 각자 Update에서 클릭마다 레이캐스트 → 건물 수만큼 중복 + SelectionManager와 우선순위 미조정. 클릭 라우팅 일원화 리팩터링.
- **신체 부위 균등 타격**: `GetRandomBodyPart` 7부위 균등(머리 14%) — 의도 확인.
- **static 이벤트 구독 해제 점검**: `UnitStats.OnAnyGradeUp` 등 static 이벤트를 UI가 구독 — 씬 전환 시 해제 누락하면 파괴된 객체 참조.
- **빌드 고스트 부수효과**: `EnsureGhost`가 Instantiate 후 컴포넌트를 끄지만 Awake/OnEnable 부수효과는 한 프레임 실행됨. OnEnable 등록 컴포넌트 추가 시 함정.

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

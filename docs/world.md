# World & Map System

## Map System (`Assets/1 Script/Map/`)

Unity Terrain 기반. 개발 중 단일 Terrain 5,000×5,000, 최종 목표 10,000×10,000.

**`MapManager`:**
- `TileData[,]` 배열 소유. `TileData` — `TileType` (Passable/Impassable) + `MovePenalty`
- `static OnTileChanged` — `SetTile` 시 발행. 리스너가 시각 갱신
- `TileToWorld(Vector2Int)` / `WorldToTile(Vector3)` 좌표 변환
- 다중 존 지원: `static List<MapManager> _allZones` 레지스트리. `GetForPosition(Vector3)` — 해당 좌표 존 반환, 없으면 Instance 폴백
- `SetAsPlayerZone()` — DungeonCore가 플레이어 던전존 명시 지정용

**`GridRenderer`:** `OnTileChanged` 구독 → Impassable 타일마다 색상 Plane 오버레이 스폰

**`ResourceNode`:**
- `Awake`에서 타일 Impassable 설정, `OnDestroy`에서 Passable 복구
- `DropYield()` → `Inventory`에 아이템 추가 → HaulJob 등록

## Pathfinding — 3단계 계층 A*

NavMesh 제거 (건물 배치 시 재베이크 불필요).

| 단계 | 클래스 | 셀 크기 | 용도 |
|---|---|---|---|
| L1 | `LocalGridManager` | 1×1 | 유닛 주변 정밀 이동 |
| L2 | `PathGrid` (in `HierarchicalPathfinder`) | 100×100 | 지역 경로 |
| L3 | `PathGrid` (in `HierarchicalPathfinder`) | 1000×1000 | 광역 라우팅 |

**`PathGrid`** — 범용 A* 격자. L1/L2/L3 공용. Octile 거리 휴리스틱, 8방향 이동, 이동 비용(cost) 지원.

**`HierarchicalPathfinder`** (MonoBehaviour 싱글톤, **씬에 배치 필요**):
- 게임 시작 시 Terrain 경사도 샘플링으로 L2/L3 자동 베이킹
- `obstacleLayer` — Inspector에서 건물/벽 레이어 지정 (Terrain 레이어 제외)
- `MapManager.OnTileChanged` 구독 → 타일 변경 시 L1/L2/L3 해당 셀만 즉시 갱신
- `FindPath(from, to)`: 단거리(L1 범위 내)는 L1 A*, 장거리는 L2 A* + 첫 구간 L1 정밀화

**`LocalGridManager`** — L1 그리드 관리:
- **공유 그리드**: DungeonCore 기준 80×80 타일. DungeonCore 이동 시 재중심.
- **클러스터 그리드**: 공유 범위 밖 유닛을 20타일 반경 기준으로 그룹화, 그룹 평균 위치 기준 40×40 그리드. 3초 주기 갱신.
- `Physics.CheckBox`로 장애물 감지 시 `obstacleLayer`만 검사.

**`Pathfinder`** (static) — `PawnMover`의 진입점. `HierarchicalPathfinder.Instance.FindPath()`에 위임. Instance 없으면 직선 폴백.

**이동 비용**: Terrain 경사도 기반 (`GetSteepness`). 45° 이상 불가, 이하는 경사도 × `slopeCostMult` 비용 증가.

## Event System (`Assets/1 Script/Event/`)

**`GameEventType`** enum: Raid, Merchant, Reinforcement, Discovery

**`GameEventData`** (ScriptableObject, `DungeonColony/GameEventData`):
- 공통: `EventName`, `Description`, `Icon`, `Weight`, `MinGameTime`, `EventType`
- Raid: `EnemyPrefabs: List<GameObject>`, `EnemyCount`
- Merchant: `MerchantPrefab: GameObject`
- Reinforcement: `ReinforcementPrefabs: List<GameObject>`, `ReinforcementCount`
- Discovery: `ResourceNodePrefab`, `DiscoveryMinDist`, `DiscoveryMaxDist`

**`EventManager`** (MonoBehaviour 싱글톤, **씬에 배치 필요**):
- `checkInterval` (기본 300초) 주기로 가중치 기반 랜덤 이벤트 발동
- 에디터 우클릭 → "Force Roll Event"로 즉시 테스트 가능

**이벤트별 동작:**

| 타입 | 동작 |
|---|---|
| `Raid` | 맵 가장자리에 EnemyPrefabs 스폰. Enemy BT로 자동 공격 |
| `Merchant` | 맵 가장자리에 상인 NpcData(EventMerchant) 스폰. 걸어오고 머물다 귀환 소멸 |
| `Reinforcement` | 맵 가장자리에 ReinforcementPrefabs 스폰. 던전 코어 방향으로 초기 이동 후 일반 방랑자로 합류 |
| `Discovery` | DungeonCore 기준 MinDist~MaxDist 범위 랜덤 위치에 자원 노드 스폰 |

**`EventNotificationUI`** — 이벤트 발동 시 화면 상단 팝업 (5초 자동 닫힘 + 닫기 버튼)

에디터 메뉴: `DungeonColony → Create Event Notification UI`

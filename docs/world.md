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

## Pathfinding — 코스 라우트 + 유닛 중심 로컬 윈도우 (Theta*)

NavMesh 제거. 켄시급 대형 맵이라 전체를 1m 격자로 못 덮으므로, **장거리는 거친 코스 라우트로, 눈앞은 유닛 따라다니는 정밀 윈도우로** 처리하는 2단 구조.

| 층 | 클래스 | 셀 크기 | 범위 | 용도 |
|---|---|---|---|---|
| 로컬 윈도우 | `LocalGridManager` | 1m | 유닛 주변 40m | 눈앞 장애물 정밀 회피 |
| 메소 | `PathGrid` (`_meso`) | 2m | 코어 주변 160m | 긴 벽 코스 회피 |
| L2 | `PathGrid` (`_l2`) | 100m | 맵 전체 | 장거리 라우팅 |

**`PathGrid`** — 범용 **Theta*(any-angle)** 격자. `LineOfSight` 가시선으로 부모를 건너뛰어 계단식 아닌 직선 경로 생성. 유클리드 휴리스틱(admissible), 8방향 확장, 이동 비용 지원, **decrease-key 인덱스 이진 최소 힙**(O(N log N)). 시작 셀이 막혀도(반경 부풀림) 탈출 허용. `Rebase`로 윈도우 풀링.

**`HierarchicalPathfinder`** (MonoBehaviour 싱글톤, **씬에 배치 필요**):
- `ComputeCoarseRoute(from, to)`: from이 메소(베이스) 안이면 메소로 라우팅(긴 벽 우회), to가 메소 밖이면 경계(게이트)까지 메소 후 to 직선. 그 외 장거리는 L2.
- `FindLocalPath(unitPos, target)`: 유닛 중심 윈도우를 즉석 베이크해 Theta*.
- `RebakeObstacleAt(worldPos)`: 건물 배치/타일 변경 시 메소의 해당 영역만 `CheckBox` 재베이크.
- `obstacleLayer` — **Wall 레이어만** 지정(유닛/지형 제외). 유닛 포함 시 자기 셀을 막아 경로 실패.
- `agentRadius` — 유닛 반경. 장애물을 이만큼 부풀려 좁은 틈 차단 + 벽 끼임 방지. CharacterController 반경에 맞출 것.
- 메소는 게임 시작 + 코어 이동 시(`MesoRecenterLoop`, 3초 점검) 베이크.

**`LocalGridManager`** — 로컬 윈도우 전용. `FindWindowPath`가 풀링된 윈도우 그리드 1개를 유닛 위치로 재배치·재베이크(매번 `CheckBox(Wall)`이라 런타임 벽도 항상 반영). 길찾기는 동기라 전 유닛 공용.

**`Pathfinder`** (static) — `PawnMover`의 진입점. `ComputeCoarseRoute`/`FindLocalPath`에 위임.

**`PawnMover`** — 코스 라우트를 추종하며 윈도우 소진/이탈 시 재계획. 코스 웨이포인트 추월 판정(뒤로 안 감), 로컬 막힘 시 코스 전역 재계산, 막다른 길 정지.

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

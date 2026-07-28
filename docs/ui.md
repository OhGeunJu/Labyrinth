# UI System

## 패널 목록 (`Assets/1 Script/UI/`)

| 컴포넌트 | 트리거 | 설명 |
|---|---|---|
| `UnitHPUI` | 유닛 선택 시 항상 | 왼쪽 하단. HP/혈액/신체부위/장착/상태이상 |
| `UnitStatsUI` | C키 토글 | 속성/전투/마법/노동 스킬 — **등급 + 진행도%** 표시. 미니언은 "[고정]" 태그 |
| `GradeUpNotificationUI` | 등급 승급 시 | **월드 공간** 텍스트가 유닛 머리 위에 떠올라 페이드 (2.5초). 카메라 빌보드 + 등급 색상 |
| `PawnInventoryPanel` | I키 | 인벤토리 그리드 + 장착 슬롯 6개 + 하단 액션 바. 상인 열림 → 판매 모드(액션 바 [판매] 버튼), 시체 열림 → 옮기기 모드 |
| `CorpseInventoryPanel` | 시체 클릭 | `SelectionManager.OnCorpseSelected` 구독. 인벤토리 그리드 + 장착 슬롯 6개. PawnInventoryPanel 강제 열기 + 루팅 모드 진입 |
| `MerchantUI` | `Merchant.OnSelected` 이벤트 | 상인 재고 목록(구매 전용). 아이템 클릭/더블클릭 → 구매. 아이템 드롭 존으로 판매도 가능 |
| `ItemInfoPanel` | 아이템 hover | 공용 툴팁. 등급 컬러, 스탯, 설명, 상점가/판매가/가치. `ItemInfoPanel.Show(item, quality)` / `Hide()` |
| `StorageBuildingUI` | Storage 좌클릭 | 타이틀바 + InventoryGridUI 그리드. CanAccept 필터 적용 |
| `CraftingTableUI` | CraftingTable 좌클릭 | 마스터-디테일. 왼쪽 RecipeEntryUI 목록, 오른쪽 디테일. ESC로 닫기 |
| `ResearchTableUI` | ResearchTable 좌클릭 | 마스터-디테일. 선행연구/재료/시간 표시 |
| `FarmPlotUI` | FarmPlot 좌클릭 | 씨앗 선택 + 상태/성장/수분 표시. ESC로 닫기 |
| `BedUI` | Bed 좌클릭 | 선택 유닛 배정/해제 버튼 |
| `PatrolZoneUI` | PatrolZone 좌클릭 | 모드 전환 버튼 3개 + 배정 유닛 목록 |
| `ColonyPanel` | P키 | **무리 관리** — 폰 전체를 한 표에서. 행=폰, 열=작업 on/off + 현재 배정 + 전투 성향 |
| `BuildModeUI` | B키 | 화면 하단 건설 바. 왼쪽 카테고리, 오른쪽 건물 목록 |
| `NodeInfoPanel` | ResourceNode 클릭 | 자원 노드 정보. haulThreshold +5/-5 버튼 |
| `PawnRosterPanel` | 항상 표시 | **우측 하단 세로 명부**(340×760, 모서리에 딱 붙임). 한 줄 = 초상화+이름+현재 상태(`PawnStatusText`). 초상화·글자는 `rowHeight`(52)에 비례. 넘치면 스크롤, 행 클릭 = 선택(선택 행 배경 강조). 건설 모드에선 숨김 |
| `EventNotificationUI` | 이벤트 발동 | 화면 상단 팝업. 5초 자동 닫힘 |
| `DungeonCoreUI` | 코어 선택 | 던전 코어 정보/소환 |

**공통 패턴:**
- `BuildModeManager.OnActiveChanged` — 건설 모드 열릴 때 대부분 UI 자동 닫힘
- `CloseButton` — `Target` RectTransform 연결 시 클릭으로 비활성화
- ESC 키 — 대부분 패널이 `Update()`에서 `escapeKey.wasPressedThisFrame` 체크로 닫힘
- 마우스 휠 — 커서가 UI 위면 `CameraController`가 줌을 건너뛴다(패널 스크롤 우선). 클릭 게이트와 같은 `EventSystem.IsPointerOverGameObject` 방식

## InventoryGridUI

그리드 시각화. 다중 컨테이너 드래그-드롭 지원.

- `_activeGrids` 정적 목록 — 열린 모든 그리드 추적
- `ReceiveFilter: Func<ItemData, bool>` — 외부 필터 주입 (Storage는 `CanAccept` 연결)
- `FindUnderCursor(screenPos, cam)` — 현재 커서 아래 그리드 반환
- `ShowPreview(inst, x, y, rotated, isExternal)` — 초록/빨강 오버레이 색 피드백
- `ClearPreview()` — 프리뷰 초기화
- `ScreenToGrid(screenPos)` — `_itemsRoot` (pivot 0,1) 기준 그리드 좌표 변환

## DraggableItem

아이콘 자동 추가. 드래그/드롭/회전 처리.

- R키 — 90° 회전 (`localRotation = Euler(0,0,-90°)` + 셀 점유 변경)
- 같은 그리드 내 드롭 → `MoveItem`
- 다른 그리드 드롭 → `TryAddAt` + `RemoveInstance`
- `EquipSlotUI` 위에 드롭 → 장착 (`IDropHandler`). `DropHandled` 플래그로 월드 드롭 방지
- `MerchantUI.SellZoneRect` 위에 드롭 → 판매
- UI 밖 드롭 → `_sourceGrid.Inventory.transform.position`에 `DroppedItem.Spawn`
- 단클릭 → `PawnInventoryPanel.NotifyItemSelected`, 더블클릭 → `NotifyItemDoubleAction`
- `CanReceiveItem` 체크로 ReceiveFilter 적용. 색 피드백: 초록(가능)/빨강(불가)

## PawnInventoryPanel 액션 모드

`ActionMode` enum: `None` / `Sell` / `Loot`

| 모드 | 진입 | 액션 바 버튼 | 더블클릭 |
|---|---|---|---|
| Sell | `MerchantUI.Open()` → `SetSellMode(merchant)` | 판매 | 즉시 판매 |
| Loot | `CorpseInventoryPanel.OnCorpseSelected` → `SetLootMode(inventory)` | 옮기기 | 즉시 이동 |
| None | 상인/시체 패널 닫힘 | 숨김 | — |

- 상인 열릴 때 루팅 중이면 `_prevMode`/`_prevLootInventory` 백업 → 상인 닫히면 루팅 모드 복원 (`ClearSellMode`)
- `ForceOpen()` / `ForceClose()` — 외부에서 패널 강제 열기/닫기. 호출한 쪽이 `_didForceOpenInventory`로 추적해 닫을 때 복원

## EquipSlotUI

장착 슬롯 1칸. `Equipment.OnEquipChanged` 이벤트를 내부에서 직접 구독하는 자기관리형 컴포넌트.

- `Init(SlotKind)` — 슬롯 종류(MainHand/OffHand/Head/Chest/Hands/Legs) 설정
- `Bind(Equipment)` — Equipment 교체 시 이벤트 재구독 + Refresh
- 클릭 → 해제, 드롭(`IDropHandler`) → 장착. `DraggableItem.DropHandled` 플래그로 월드 드롭 방지
- 호버 → `ItemInfoPanel` 툴팁

`PawnInventoryPanel`, `CorpseInventoryPanel` 모두 6개 `EquipSlotUI` 필드를 직접 보유. 래퍼 컴포넌트 없음.

## DraggablePanel

헤더 GameObject에 부착. `Target`에 이동할 패널 RectTransform 연결 → 드래그로 패널 이동.

## RecipeEntryUI / ResearchEntryUI / BuildingItemUI

- `RecipeEntryUI` — `[RequireComponent(typeof(LayoutElement))]`. 재료 부족 시 빨간색. 잠긴 레시피는 `[잠김]` 접두사 + 회색 + 버튼 비활성
- `ResearchEntryUI` — 완료(초록) / 잠김(회색) / 재료부족(빨강) 색상 구분
- `BuildingItemUI` — background/icon/nameLabel/costLabel SerializeField. 자원 부족 시 빨간색, 선택 중 파란색

## 에디터 자동 생성 메뉴 (`DungeonColony →`)

| 메뉴 | 생성 대상 |
|---|---|
| Create Bed UI | BedUI 패널 Canvas 자동 생성 및 연결 |
| Create PatrolZone UI | PatrolZoneUI 패널 자동 생성 |
| Create Event Notification UI | EventNotificationUI 팝업 자동 생성 |
| Create Unit Stats UI | UnitStatsUI 패널 자동 생성 (우측 상단, 스크롤 뷰 + 한글 폰트) |
| Create Grade Up Notification UI | GradeUpNotificationUI GameObject 생성 + 폰트 연결 |
| Create FarmPlot Prefab | 갈색 납작 큐브 + FarmPlot 컴포넌트 |
| Create WaterSource Prefab | 원통 우물 외형 + WaterSource 컴포넌트 |
| Create FarmPlot UI | FarmPlotUI 패널 자동 생성 |
| Create Tree Prefab | 줄기+수관 나무 + ResourceNode(Wood) |
| Create Merchant UI | MerchantUI + ItemInfoPanel 씬 생성, MerchantStockEntry/SellItemEntry 프리팹 저장 |
| Create Pawn Inventory UI | PawnInventoryPanel 전체 UI 재생성 (그리드+장착슬롯+액션바) |
| Add Corpse Equip Row | CorpseInventoryPanel에 장착 슬롯 행 추가 |
| Create Colony Panel | ColonyPanel(무리 관리, P키) 생성 + 구버전 Priority_UI/PriorityManager 오브젝트 제거 |

## Inventory 클래스

`Inventory` — 그리드 기반:
- `TryAdd(item, amount, quality)` / `TryAddAt(item, x, y, rotated, quality)`
- `MoveItem(inst, x, y, rotated)` / `Remove(type, amount)` / `RemoveInstance(inst)`
- `CanPlaceItem(item, x, y, rotated)` / `CanPlaceExtern(item, x, y, rotated)` — 외부 아이템 색 피드백용
- `GetTotalQuantity(type)` / `GetTotalByType(type)` / `GetTotalWeight()` / `GetAt(x, y)`

`ItemInstance` — 런타임 객체: `Data`, `Quantity`, `GridX`, `GridY`, `Rotated`, `EffectiveWidth/Height`, `DisplayName` (등급 접두사 포함)

`DroppedItem.Spawn(item, amount, position)` — 작은 큐브 시각 + SphereCollider + GroundHaulJob 자동 등록

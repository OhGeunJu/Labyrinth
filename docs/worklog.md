# 작업 로그

날짜별 주요 작업 기록. 컨벤션 상세는 `docs/editor_creators.md` 등 해당 문서 참조.

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
- 종족별 성장 속도 차별화 (등급 시스템 후속)
- 배정/로스터 격자가 칸을 넘칠 때 스크롤 추가 여부

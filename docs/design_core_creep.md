# 핵 영역(크립) 시스템 설계 — 2026-06-05 결정, 구현 예정(2026-06-06)

저그 *Creep Tumor*식 **다중 핵 + 영역 합집합** 모델. 핵 이동/제거 시 "빌드존 리셋" 이슈를 근본 해결한다.

## 컨셉

- **영역(크립) = 살아있는 모든 핵의 반경 합집합.** 핵 = 메인 `DungeonCore` + 다수 `SubCore`(이미 종양 역할: radius/캡보너스/범위효과, `RegisterSubCore`로 등록).
- 메인 핵은 이전(relocate) 가능, 서브코어로 영역을 사방에 확장.
- 이미 `DungeonCore.IsWithinAnyRange`가 메인+서브 합집합을 검사 → 영역 모델은 구조적으로 존재.

## 결정 사항

1. **고아 건물 = 비활성 방치** (철거 X). 어떤 핵 범위에도 안 드는 건물은 작동 정지 + 표시. 범위가 복구되면 재가동.
2. **영역 판정과 타일 그리드 분리.** 영역은 반경 합집합으로만 판정(그리드 중심과 무관) → 50×50 단일 존의 한계/리셋 문제 소멸.
3. **`RecenterAt`의 `ClearTiles` wipe 제거.** 핵 변경이 건물/점유 데이터를 날리지 않게.

## 지형/좌표 분석 (절대좌표 가능한가? — 검토 결과)

질문: 지형이 울퉁불퉁한데 절대좌표 그리드가 되나? 지형에 투영돼야 하지 않나?

- 그리드는 **top-down XZ 격자**다. 높이(Y)는 그 (x,z)에서 **지형을 샘플**해 얻으면 된다 → 절대좌표와 지형 투영은 충돌하지 않음.
- **현 코드 실태:** `TileData.Height`는 지형에서 한 번도 안 채워짐(`MakePassable`이 0). 그래서
  - `IsBuildable`의 높이분산 평탄도 검사는 전부 0이라 **항상 통과(무의미)**.
  - 실제 배치 높이는 `BuildModeManager`가 지면 **레이캐스트**(`groundMask`)로 별도 산출 중.
  - `GridRenderer` 오버레이는 `tile.Height(=0)+오프셋`이라 경사지에서 **떠/잠김**.
- **결론:** 거대 절대 그리드를 저장하지 말 것(켄시급 맵 메모리 폭발). 대신
  - **지형은 배치 시 온디맨드 샘플**(레이캐스트 / `Terrain.SampleHeight` / `terrainData.GetSteepness`). footprint 코너 높이차로 평탄도 판정 → 울퉁불퉁 지형 자동 반영.
  - **점유만 희소 저장**: 전역 `HashSet<Vector2Int>`(절대 타일좌표) 또는 건물이 자기 footprint 보유. 핵 변경에도 안 지워짐.
  - 영역 = 반경 합집합(저장 불필요).

## 변경 대상

- **`MapManager`**: `_tiles[50,50]` 배열 제거 → ① 점유 `HashSet<Vector2Int>`(절대좌표) ② 온디맨드 지형 샘플 헬퍼(평탄도/높이). `RecenterAt` wipe 제거(또는 폐기). `IsInBuildZone`는 영역 판정에서 분리.
- **`BuildModeManager`**: `IsBuildable` 재작성 = 영역(union, `IsWithinAnyRange`) + 온디맨드 평탄도 레이캐스트 + 점유(HashSet) 검사.
- **`DungeonCore`/`SubCore`**: 영역 변경(이동/파괴/배치) 시 이벤트(`OnTerritoryChanged`?) → 건물 재평가 트리거.
- **건물(Storage/Bed/FarmPlot/…)**: 영역 이탈 시 비활성/재가동 인터페이스(`IDormantable`?), 배치/철거 시 점유 등록·해제.
- **`GridRenderer`**: 점유/영역/비활성 시각화를 온디맨드 높이로 갱신(경사지 부유 버그도 같이 해결).
- **길찾기**: 메소 격자가 이미 코어를 따라다님 — 정합. 추가 작업 없음.

## 구현 단계 (제안)

1. ✅ (2026-06-06) 영역/그리드 분리 + 점유 희소 저장 (`MapManager` 리팩터). `_tiles[50,50]`→`HashSet<Vector2Int>` 절대좌표, `RecenterAt` wipe 제거, `GridRenderer` 오버레이 유지. 상세: `docs/worklog.md`.
2. ✅ (2026-06-06) 온디맨드 지형 평탄도 샘플. `MapManager.SampleGroundHeight`/`TileToGround`/`IsFlatEnough`, `IsBuildable`이 코너 높이차 검사, `BuildModeManager`·`GridRenderer` 지면 스냅(경사지 부유 버그 동시 해결).
3. ✅ (2026-06-06) 핵 변경 이벤트(`DungeonCore.OnTerritoryChanged`) → 고아 건물 **휴면/재가동**(`BuildingDormancy`). FarmPlot/CraftingTable/WaterSource enabled 토글 + 회색 틴트, `Storage.IsActive`로 집계 제외. 상세: `docs/worklog.md`.
4. ✅ (2026-06-06, step 2와 함께) 경사지 높이 버그 수정 + 비활성 회색 틴트 표시. (전용 영역 오버레이는 후속)
5. 메인 핵 이전 액션(정책 C) — 이동 지점에서 `MapManager.RecenterAt` + `DungeonCore.RaiseTerritoryChanged` 호출만 연결하면 휴면 재평가 자동.

## 미결/주의

- 비활성 건물의 작업/창고/길찾기 상호작용 정의 필요(예: 비활성 창고는 집계 제외? 비활성 침대 사용 불가?).
- 평탄도 기준(`buildHeightTolerance`)을 온디맨드 레이캐스트로 어떻게 계산할지 확정.
- 1타일 = 1월드유닛 절대좌표(현행 유지).

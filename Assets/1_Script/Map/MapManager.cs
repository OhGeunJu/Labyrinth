using System;
using System.Collections.Generic;
using UnityEngine;

// 정착지 단위 타일 그리드. 씬에 여러 개 공존 가능 (플레이어 던전 + NPC 도시 등).
// - 1 tile = 1 world unit, 절대 월드 좌표 기준 (tile.x = floor(world.x), tile.y = floor(world.z))
// - Instance = 플레이어 던전존 (DungeonCore가 RecenterAt 호출)
// - GetForPosition(worldPos) = 해당 좌표를 포함하는 존 반환 → 미래 다중 정착지 길찾기 진입점
[DefaultExecutionOrder(-50)]
public class MapManager : MonoBehaviour
{
    // 플레이어 던전존. DungeonCore 전용 API(RecenterAt 등)에 사용.
    public static MapManager Instance { get; private set; }

    // 모든 활성 존 레지스트리 (플레이어 + NPC 정착지)
    static readonly List<MapManager> _allZones = new();

    public static event Action<Vector2Int> OnTileChanged;
    public static event Action OnZoneRecentered;
    // 바닥 타일 등록/해제 통보 (바닥 지오메트리 렌더링용 — 후속).
    public static event Action<Vector2Int> OnFloorChanged;

    [Header("빌드존 크기 (타일 단위) — 최대 바깥 경계 클램프. 실제 건설 게이트는 코어 union.")]
    [SerializeField] int buildZoneSize = 200;

    [Header("건설")]
    [SerializeField] float buildHeightTolerance = 0.5f;

    [Header("바닥 (던전형 맵)")]
    [Tooltip("건물을 바닥(floored) 위에만 배치하도록 강제. 공허 던전 씬은 켤 것 — " +
             "바닥이 하나도 없는 상태에서도 게이트가 걸린다. 꺼도 바닥이 1개 이상 깔리면 자동으로 강제됨.")]
    [SerializeField] bool requireFloor = false;

    [Header("지형 샘플")]
    [Tooltip("지면 높이 레이캐스트 마스크. Terrain이 있으면 불필요. 평면 바닥 씬은 바닥 레이어만 지정. " +
             "Everything/Nothing이면 레이캐스트 생략(평지 y=0 처리) — 건물/유닛 오인 방지.")]
    [SerializeField] LayerMask groundMask = 0;

    // 점유 타일 희소 저장 (절대 월드 타일 좌표). 핵 이동(RecenterAt)에도 살아남는다.
    // ResourceNode 등 막힌 타일만 등록 — 비면 통행 가능이 기본값.
    readonly HashSet<Vector2Int> _occupied = new();

    // 바닥 타일 희소 저장 (절대 월드 타일 좌표, 모델 A). 코어와 무관하게 깔리는 substrate.
    // 비면 = 바닥 시스템 미사용(필드/지형 맵) → 건물이 바닥을 요구하지 않음.
    // 하나라도 있으면 = 던전형 맵 → 건물은 바닥 위(floored)에만 배치 가능.
    // 값 = 바닥 윗면 월드 Y (건물이 이 높이 위에 얹힌다). 슬래브 두께를 반영하려고 높이까지 들고 있음.
    readonly Dictionary<Vector2Int, float> _floored = new();

    Vector2Int _zoneOriginTile; // 빌드존 좌하단 타일 (절대 월드 타일 좌표)

    public int BuildZoneSize => buildZoneSize;
    public int Width  => buildZoneSize;
    public int Height => buildZoneSize;
    public float BuildHeightTolerance => buildHeightTolerance;
    public Vector2Int ZoneOriginTile => _zoneOriginTile;

    void Awake()
    {
        _allZones.Add(this);
        if (Instance == null) Instance = this;
    }

    void OnDestroy()
    {
        _allZones.Remove(this);
        if (Instance == this) Instance = null;
    }

    // 월드 좌표를 포함하는 존 반환. 어느 존에도 없으면 Instance 폴백.
    public static MapManager GetForPosition(Vector3 worldPos)
    {
        foreach (var zone in _allZones)
            if (zone.IsInBuildZone(worldPos)) return zone;
        return Instance;
    }

    // DungeonCore가 호출 — 이 존을 플레이어 던전존으로 지정.
    public void SetAsPlayerZone() => Instance = this;

    // 미궁핵 이동 시 호출. 빌드존 중심(라우팅용 origin)만 옮긴다.
    // 점유(_occupied)는 절대좌표 희소 저장이라 wipe하지 않음 → "이동 시 빌드존 리셋" 방지.
    public void RecenterAt(Vector3 worldCenter)
    {
        var centerTile = WorldToTile(worldCenter);
        _zoneOriginTile = centerTile - new Vector2Int(buildZoneSize / 2, buildZoneSize / 2);
        OnZoneRecentered?.Invoke();
    }

    // ── 좌표 변환 (절대 월드 타일) ────────────────────────────────────────────

    public Vector2Int WorldToTile(Vector3 world) =>
        new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));

    public Vector3 TileToWorld(Vector2Int tile)
    {
        // Y는 0(평면) 기준. 지형 높이는 배치 시 온디맨드 샘플(step 2)에서 처리.
        return new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
    }

    public Vector3 WorldOrigin => new Vector3(_zoneOriginTile.x, 0f, _zoneOriginTile.y);

    // ── 빌드존 판정 ────────────────────────────────────────────────────────────

    public bool IsInBuildZone(Vector2Int tile) =>
        TryToLocal(tile, out _, out _);

    public bool IsInBuildZone(Vector3 world) => IsInBuildZone(WorldToTile(world));

    // 하위 호환: 기존 코드의 IsInBounds 호출은 빌드존 검사로 매핑
    public bool IsInBounds(Vector2Int tile) => IsInBuildZone(tile);
    public bool IsInBounds(int x, int y)    => IsInBuildZone(new Vector2Int(x, y));

    bool TryToLocal(Vector2Int tile, out int lx, out int ly)
    {
        lx = tile.x - _zoneOriginTile.x;
        ly = tile.y - _zoneOriginTile.y;
        return lx >= 0 && lx < buildZoneSize && ly >= 0 && ly < buildZoneSize;
    }

    // ── 타일 접근 ─────────────────────────────────────────────────────────────

    // 점유 집합에 있으면 Impassable, 아니면 Passable. 빌드존 경계와 무관(절대좌표).
    public TileData GetTile(Vector2Int tile) =>
        _occupied.Contains(tile) ? TileData.MakeImpassable() : TileData.MakePassable();

    public TileData GetTile(int x, int y) => GetTile(new Vector2Int(x, y));

    // 통행 가능 여부만 희소 저장에 반영. 핵 이동에도 유지됨.
    public void SetTile(Vector2Int tile, TileData data)
    {
        bool changed = data.IsPassable ? _occupied.Remove(tile) : _occupied.Add(tile);
        if (changed) OnTileChanged?.Invoke(tile);
    }

    public void SetTile(int x, int y, TileData data) => SetTile(new Vector2Int(x, y), data);

    // ── 바닥(floor) ────────────────────────────────────────────────────────────
    // 코어와 무관한 substrate. Floor 컴포넌트가 자기 footprint를 등록/해제한다.

    public bool HasFloorSystem => _floored.Count > 0;
    // 건물의 "바닥 위에만" 강제 여부: 명시 플래그(공허 씬, 바닥 0장이어도 게이트) 또는 바닥 존재 시 자동.
    public bool FloorRequired  => requireFloor || _floored.Count > 0;
    public bool IsFloored(Vector2Int tile) => _floored.ContainsKey(tile);
    public bool IsFloored(Vector3 world)   => IsFloored(WorldToTile(world));

    // 바닥 윗면 Y (바닥 없으면 false). 배치 높이 계산용.
    public bool TryGetFloorSurface(Vector2Int tile, out float surfaceY) => _floored.TryGetValue(tile, out surfaceY);

    // 유닛이 설 수 있는 타일인가 — 공허 게이트. 던전형 맵(FloorRequired)에서만 바닥을 요구하고
    // 필드/지형 맵은 항상 true. 점유(IsPassable)와는 별개 축이므로 필요 시 둘 다 검사할 것.
    public bool IsStandable(Vector2Int tile) => !FloorRequired || _floored.ContainsKey(tile);
    public bool IsStandable(Vector3 world)   => IsStandable(WorldToTile(world));

    // 폰이 **실제로 설 수 있는** 칸인가 — 통행 가능 + 바닥 있음.
    public bool IsWorkable(Vector2Int tile) => GetTile(tile.x, tile.y).IsPassable && IsStandable(tile);

    // `center` 주변에서 설 수 있는 가장 가까운 칸을 찾는다. 못 찾으면 `center` 자신.
    //
    // **왜 반경을 넓혀야 하나**: 예전엔 상하좌우 4칸만 봤다. 건물·자원 노드가 몰려 있으면
    // 네 칸이 전부 막히는 일이 흔한데, 그때 막힌 칸 자신을 목적지로 돌려주면 폰은
    // **영영 도착 판정에 못 걸린다**(6초 뒤 일감을 놓고, 다시 집고, 또 놓는다).
    // 대각선과 반경 2까지 보면 대부분 살아난다.
    //
    // `exclude`는 **지금은 지나갈 수 있지만 서면 안 되는 칸**이다 — 청사진 발자국이 그렇다.
    // 통행 판정만으로는 못 거른다(청사진은 일부러 칸을 안 막는다).
    public Vector2Int FindWorkableNear(Vector2Int center, int maxRadius = 2,
                                       ICollection<Vector2Int> exclude = null)
    {
        bool Allowed(Vector2Int t) => IsWorkable(t) && (exclude == null || !exclude.Contains(t));

        if (Allowed(center)) return center;

        for (int r = 1; r <= maxRadius; r++)
        {
            Vector2Int best = center;
            float bestDist = float.MaxValue;

            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                // 이번 고리(껍질)만 본다 — 안쪽은 앞선 반경에서 이미 봤다.
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;

                var t = new Vector2Int(center.x + dx, center.y + dy);
                if (!Allowed(t)) continue;

                float d = dx * dx + dy * dy;      // 같은 고리 안에서는 대각선보다 직선이 가깝다
                if (d < bestDist) { bestDist = d; best = t; }
            }

            if (bestDist < float.MaxValue) return best;
        }
        return center;
    }

    // surfaceY = 바닥 윗면 월드 Y. 같은 타일에 여러 바닥이 겹치면 더 높은 면을 채택(건물이 위에 얹히도록).
    public void RegisterFloor(Vector2Int tile, float surfaceY = 0f)
    {
        if (_floored.TryGetValue(tile, out float prev))
        {
            if (surfaceY > prev) _floored[tile] = surfaceY;
            return;                       // 이미 바닥 — OnFloorChanged 중복 발동 안 함
        }
        _floored[tile] = surfaceY;
        OnFloorChanged?.Invoke(tile);
    }

    public void UnregisterFloor(Vector2Int tile)
    {
        if (_floored.Remove(tile)) OnFloorChanged?.Invoke(tile);
    }

    // ── 온디맨드 지형 샘플 ──────────────────────────────────────────────────────

    // 월드 (x,z)의 지면 높이.
    //  1) Terrain.activeTerrain → 지면만 샘플(건물/유닛에 안 걸림). 가장 안전.
    //  2) groundMask가 "특정 레이어"로 설정된 경우에만 레이캐스트. Everything(~0)/Nothing(0)이면
    //     엉뚱한 콜라이더(건물·코어·유닛)를 지면으로 오인할 수 있어 건너뛰고 평지(0)로 처리.
    //  3) 그 외 → 0 (평지 가정). 평면 바닥 씬은 groundMask를 바닥 레이어로 지정하면 됨.
    public float SampleGroundHeight(float worldX, float worldZ)
    {
        var t = Terrain.activeTerrain;
        if (t != null) return t.SampleHeight(new Vector3(worldX, 0f, worldZ)) + t.transform.position.y;

        int mask = groundMask.value;
        if (mask != 0 && mask != ~0)
        {
            var origin = new Vector3(worldX, 500f, worldZ);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1000f, mask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
        }
        return 0f;
    }

    public float SampleGroundHeight(Vector3 worldPos) => SampleGroundHeight(worldPos.x, worldPos.z);

    // 타일의 표면 높이 — 바닥이 깔려 있으면 그 윗면, 아니면 지형/평지 높이.
    // 건물은 이 높이 "위에" 얹혀야 한다(바닥 슬래브 두께만큼 파묻히지 않도록).
    public float SurfaceHeight(Vector2Int tile)
    {
        if (_floored.TryGetValue(tile, out float floorY)) return floorY;
        return SampleGroundHeight(tile.x + 0.5f, tile.y + 0.5f);
    }

    // 타일 중심을 표면에 스냅한 월드 좌표 (배치/시각화용). 길찾기용 TileToWorld(Y=0)와 분리.
    public Vector3 TileToGround(Vector2Int tile)
    {
        return new Vector3(tile.x + 0.5f, SurfaceHeight(tile), tile.y + 0.5f);
    }

    // footprint 바운드의 4코너 지면 높이차가 tolerance 이내인지 (울퉁불퉁 지형 자동 반영).
    public bool IsFlatEnough(IEnumerable<Vector2Int> footprint)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in footprint)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
        if (minX > maxX) return false; // 빈 footprint

        // 월드 코너 = 타일 경계 (minTile 좌하단 ~ maxTile+1 우상단)
        float h00 = SampleGroundHeight(minX,     minY);
        float h10 = SampleGroundHeight(maxX + 1, minY);
        float h01 = SampleGroundHeight(minX,     maxY + 1);
        float h11 = SampleGroundHeight(maxX + 1, maxY + 1);
        float lo = Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11));
        float hi = Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11));
        return (hi - lo) <= buildHeightTolerance;
    }

    // ── 건설 가능 판정 ─────────────────────────────────────────────────────────

    // 건설 판정(모델 A): 존 클램프 + 바닥 위 + 점유 안 됨 + (지형 있으면) 평탄.
    //  - 코어 union은 건설을 차단하지 않는다(union 밖 건물은 BuildingDormancy가 휴면 처리).
    //  - 존(buildZoneSize=200)은 최대 바깥 경계 클램프.
    //  - 바닥 시스템이 활성(HasFloorSystem)일 때만 "바닥(floored) 위에만" 강제 → 바닥 없는 필드/지형 맵은 무영향.
    //  - 바닥 자체를 까는 배치는 이 검사를 우회해야 함(공허에 바닥을 놓는 것) — 후속 floor placeable에서 처리.
    public bool IsBuildable(IEnumerable<Vector2Int> footprint)
    {
        bool floorRequired = FloorRequired;
        foreach (var pos in footprint)
        {
            if (!IsInBuildZone(pos)) return false;              // 최대 경계 클램프
            if (floorRequired && !_floored.ContainsKey(pos)) return false; // 바닥 위에만
            if (!GetTile(pos).IsPassable) return false;        // 점유
        }
        return IsFlatEnough(footprint);
    }

    // 바닥 배치 판정: 존 클램프 + 아직 바닥 아님 + 점유 안 됨 + (지형시) 평탄.
    // 건물과 달리 "바닥 위에만" 요구를 받지 않음 — 바닥 자체를 공허에 까는 것이므로.
    public bool CanPlaceFloor(IEnumerable<Vector2Int> footprint)
    {
        foreach (var pos in footprint)
        {
            if (!IsInBuildZone(pos)) return false;      // 최대 경계 클램프
            if (_floored.ContainsKey(pos)) return false;   // 이미 바닥
            if (!GetTile(pos).IsPassable) return false; // 점유
        }
        return IsFlatEnough(footprint);
    }
}

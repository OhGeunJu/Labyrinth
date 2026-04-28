using System;
using System.Collections.Generic;
using UnityEngine;

// 빌드존 한정 타일 그리드.
// - 1 tile = 1 world unit, 절대 월드 좌표 기준 (tile.x = floor(world.x), tile.y = floor(world.z))
// - 빌드존(기본 50×50)은 미궁핵 위치를 따라 이동 (RecenterAt)
// - 빌드존 밖 타일은 항상 Passable 기본값 반환 — 이동/항해는 NavMesh가 담당
[DefaultExecutionOrder(-50)]
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }
    public static event Action<Vector2Int> OnTileChanged;
    public static event Action OnZoneRecentered;

    [Header("빌드존 크기 (타일 단위)")]
    [SerializeField] int buildZoneSize = 50;

    [Header("건설")]
    [SerializeField] float buildHeightTolerance = 0.5f;

    TileData[,] _tiles;
    Vector2Int _zoneOriginTile; // 빌드존 좌하단 타일 (절대 월드 타일 좌표)

    public int BuildZoneSize => buildZoneSize;
    public int Width  => buildZoneSize;
    public int Height => buildZoneSize;
    public float BuildHeightTolerance => buildHeightTolerance;
    public Vector2Int ZoneOriginTile => _zoneOriginTile;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _tiles = new TileData[buildZoneSize, buildZoneSize];
        ClearTiles();
    }

    // 미궁핵 이동 시 호출. 빌드존을 새 중심으로 옮기고 타일 데이터 초기화.
    public void RecenterAt(Vector3 worldCenter)
    {
        var centerTile = WorldToTile(worldCenter);
        _zoneOriginTile = centerTile - new Vector2Int(buildZoneSize / 2, buildZoneSize / 2);
        ClearTiles();
        OnZoneRecentered?.Invoke();
    }

    void ClearTiles()
    {
        for (int x = 0; x < buildZoneSize; x++)
            for (int y = 0; y < buildZoneSize; y++)
                _tiles[x, y] = TileData.MakePassable();
    }

    // ── 좌표 변환 (절대 월드 타일) ────────────────────────────────────────────

    public Vector2Int WorldToTile(Vector3 world) =>
        new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));

    public Vector3 TileToWorld(Vector2Int tile)
    {
        float wx = tile.x + 0.5f;
        float wz = tile.y + 0.5f;
        float wy = TryToLocal(tile, out var lx, out var ly) ? _tiles[lx, ly].Height : 0f;
        return new Vector3(wx, wy, wz);
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

    // 빌드존 밖 타일은 Passable 기본값 반환. NavMesh가 진짜 장애물 처리.
    public TileData GetTile(Vector2Int tile)
    {
        if (TryToLocal(tile, out var lx, out var ly)) return _tiles[lx, ly];
        return TileData.MakePassable();
    }

    public TileData GetTile(int x, int y) => GetTile(new Vector2Int(x, y));

    public void SetTile(Vector2Int tile, TileData data)
    {
        if (!TryToLocal(tile, out var lx, out var ly)) return;
        _tiles[lx, ly] = data;
        OnTileChanged?.Invoke(tile);
    }

    public void SetTile(int x, int y, TileData data) => SetTile(new Vector2Int(x, y), data);

    // ── 건설 가능 판정 ─────────────────────────────────────────────────────────

    public bool IsBuildable(IEnumerable<Vector2Int> footprint)
    {
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var pos in footprint)
        {
            if (!IsInBuildZone(pos)) return false;
            var tile = GetTile(pos);
            if (!tile.IsPassable) return false;
            if (tile.Height < minH) minH = tile.Height;
            if (tile.Height > maxH) maxH = tile.Height;
        }
        return minH <= maxH && (maxH - minH) <= buildHeightTolerance;
    }
}

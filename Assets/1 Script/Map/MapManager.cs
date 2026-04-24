using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }
    public static event Action<Vector2Int> OnTileChanged;

    [Header("맵 크기")]
    [SerializeField] int width  = 100;
    [SerializeField] int height = 100;

    [Header("지형 높이 샘플링")]
    [Tooltip("지형만 포함하는 레이어 마스크. 유닛/오브젝트 레이어는 제외할 것.")]
    [SerializeField] LayerMask terrainMask = ~0;
    [SerializeField] float sampleOriginY  = 500f;
    [SerializeField] float maxSlopeAngle  = 35f;   // 초과하면 논리적 Impassable
    [SerializeField] float buildHeightTolerance = 0.5f; // 건설 허용 높이차

    TileData[,] _tiles;

    public int Width  => width;
    public int Height => height;
    public float BuildHeightTolerance => buildHeightTolerance;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        GenerateMap();
    }

    // Start에서 샘플링: Awake에서 ResourceNode 등이 먼저 Impassable 설정을 마친 뒤 실행
    void Start() => SampleHeights();

    void GenerateMap()
    {
        _tiles = new TileData[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _tiles[x, y] = TileData.MakePassable();
    }

    // 지형 레이캐스트로 각 타일의 Height·Normal 수집.
    // 급경사 타일은 논리적으로만 Impassable 처리하고 OnTileChanged를 발행하지 않음.
    void SampleHeights()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float wx = x + OriginX + 0.5f;
                float wz = y + OriginZ + 0.5f;
                var origin = new Vector3(wx, sampleOriginY, wz);

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, sampleOriginY * 2f, terrainMask))
                {
                    var t = _tiles[x, y];
                    t.Height = hit.point.y;
                    t.Normal = hit.normal;

                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > maxSlopeAngle && t.Type == TileType.Passable)
                        t.Type = TileType.Impassable;

                    _tiles[x, y] = t;
                }
            }
        }
    }

    // ── 타일 접근 ─────────────────────────────────────────────────────────────

    public TileData GetTile(int x, int y) =>
        IsInBounds(x, y) ? _tiles[x, y] : default;

    public TileData GetTile(Vector2Int pos) => GetTile(pos.x, pos.y);

    public void SetTile(int x, int y, TileData tile)
    {
        if (!IsInBounds(x, y)) return;
        _tiles[x, y] = tile;
        OnTileChanged?.Invoke(new Vector2Int(x, y));
    }

    public void SetTile(Vector2Int pos, TileData tile) => SetTile(pos.x, pos.y, tile);

    public bool IsInBounds(int x, int y) =>
        (uint)x < (uint)width && (uint)y < (uint)height;

    public bool IsInBounds(Vector2Int pos) => IsInBounds(pos.x, pos.y);

    // ── 좌표 변환 ──────────────────────────────────────────────────────────────

    float OriginX => -width  * 0.5f;
    float OriginZ => -height * 0.5f;

    // 지형 Height 포함 월드 좌표 반환
    public Vector3 TileToWorld(Vector2Int pos)
    {
        float wx = pos.x + OriginX + 0.5f;
        float wz = pos.y + OriginZ + 0.5f;
        float wy = IsInBounds(pos) ? _tiles[pos.x, pos.y].Height : 0f;
        return new Vector3(wx, wy, wz);
    }

    public Vector2Int WorldToTile(Vector3 world) =>
        new Vector2Int(Mathf.FloorToInt(world.x - OriginX), Mathf.FloorToInt(world.z - OriginZ));

    public Vector3 WorldOrigin => new Vector3(OriginX, 0f, OriginZ);

    // ── 건설 가능 판정 ─────────────────────────────────────────────────────────

    // footprint 전체가 Passable이고 최대 높이차가 buildHeightTolerance 이하일 때 true
    public bool IsBuildable(IEnumerable<Vector2Int> footprint)
    {
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var pos in footprint)
        {
            if (!IsInBounds(pos)) return false;
            var tile = GetTile(pos);
            if (!tile.IsPassable) return false;
            if (tile.Height < minH) minH = tile.Height;
            if (tile.Height > maxH) maxH = tile.Height;
        }
        return minH <= maxH && (maxH - minH) <= buildHeightTolerance;
    }
}

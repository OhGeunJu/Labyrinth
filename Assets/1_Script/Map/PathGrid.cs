using System.Collections.Generic;
using UnityEngine;

// 단일 A* 격자. L1(tileSize=1), L2(tileSize=100), L3(tileSize=1000) 공용.
public class PathGrid
{
    public readonly int     Width;
    public readonly int     Height;
    public readonly float   TileSize;
    public readonly Vector3 Origin;   // 셀(0,0) 좌하단 월드 좌표

    readonly float[] _cost;       // 이동 비용 배율 (1.0 = 기본)
    readonly bool[]  _walkable;

    // 경로 계산 시 재사용 버퍼 (GC 절약)
    float[] _g, _f;
    int[]   _parent;
    bool[]  _inOpen, _inClose;

    static readonly (int dx, int dy, float w)[] Dirs8 =
    {
        ( 1, 0, 1f),    (-1, 0, 1f),    ( 0, 1, 1f),    ( 0,-1, 1f),
        ( 1, 1, 1.414f),( 1,-1, 1.414f),(-1, 1, 1.414f),(-1,-1, 1.414f),
    };

    public PathGrid(int width, int height, float tileSize, Vector3 origin)
    {
        Width = width; Height = height; TileSize = tileSize; Origin = origin;
        int n    = width * height;
        _cost    = new float[n];
        _walkable = new bool[n];
        _g       = new float[n];
        _f       = new float[n];
        _parent  = new int[n];
        _inOpen  = new bool[n];
        _inClose = new bool[n];
        for (int i = 0; i < n; i++) { _cost[i] = 1f; _walkable[i] = true; }
    }

    // ── 좌표 변환 ──────────────────────────────────────────────────────
    public Vector2Int WorldToCell(Vector3 w) =>
        new(Mathf.Clamp(Mathf.FloorToInt((w.x - Origin.x) / TileSize), 0, Width  - 1),
            Mathf.Clamp(Mathf.FloorToInt((w.z - Origin.z) / TileSize), 0, Height - 1));

    public Vector3 CellToWorld(Vector2Int c) =>
        new(Origin.x + (c.x + 0.5f) * TileSize,
            Origin.y,
            Origin.z + (c.y + 0.5f) * TileSize);

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    // ── 타일 데이터 ─────────────────────────────────────────────────────
    public void  SetWalkable(int x, int y, bool w)     { if (InBounds(x,y)) _walkable[y*Width+x] = w; }
    public void  SetCost    (int x, int y, float cost) { if (InBounds(x,y)) _cost    [y*Width+x] = Mathf.Max(0.1f, cost); }
    public bool  IsWalkable (int x, int y) => InBounds(x,y) && _walkable[y*Width+x];
    public float GetCost    (int x, int y) => InBounds(x,y) ? _cost[y*Width+x] : float.MaxValue;

    // ── A* ─────────────────────────────────────────────────────────────
    // 반환: 월드 좌표 웨이포인트 리스트 (시작점 제외, toWorld 포함). 경로 없으면 null.
    public List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld)
    {
        var start = WorldToCell(fromWorld);
        var end   = WorldToCell(toWorld);
        if (start == end) return new List<Vector3> { toWorld };
        if (!IsWalkable(start.x, start.y) || !IsWalkable(end.x, end.y)) return null;

        int n = Width * Height;

        // 버퍼 초기화
        for (int i = 0; i < n; i++)
        {
            _g[i] = float.MaxValue; _parent[i] = -1;
            _inOpen[i] = false; _inClose[i] = false;
        }

        int si = start.y * Width + start.x;
        int ei = end.y   * Width + end.x;

        _g[si] = 0f;
        _f[si] = Heuristic(start, end);
        _inOpen[si] = true;

        var open = new List<int>(256) { si };

        while (open.Count > 0)
        {
            // 최소 F 노드 선택
            int bi = 0;
            for (int i = 1; i < open.Count; i++)
                if (_f[open[i]] < _f[open[bi]]) bi = i;

            int cur = open[bi];
            open[bi] = open[open.Count - 1];
            open.RemoveAt(open.Count - 1);
            _inOpen[cur] = false;
            _inClose[cur] = true;

            if (cur == ei) return BuildPath(ei, toWorld);

            int cx = cur % Width, cy = cur / Width;

            foreach (var (dx, dy, dw) in Dirs8)
            {
                int nx = cx + dx, ny = cy + dy;
                if (!InBounds(nx, ny)) continue;
                int ni = ny * Width + nx;
                if (!_walkable[ni] || _inClose[ni]) continue;

                float ng = _g[cur] + dw * _cost[ni];
                if (ng >= _g[ni]) continue;

                _g[ni]      = ng;
                _f[ni]      = ng + Heuristic(new Vector2Int(nx, ny), end);
                _parent[ni] = cur;
                if (!_inOpen[ni]) { open.Add(ni); _inOpen[ni] = true; }
            }
        }
        return null;
    }

    List<Vector3> BuildPath(int endIdx, Vector3 exactEnd)
    {
        var result = new List<Vector3>();
        int cur = endIdx;
        while (_parent[cur] != -1)
        {
            result.Add(CellToWorld(new Vector2Int(cur % Width, cur / Width)));
            int p = _parent[cur];
            if (p == cur) break;
            cur = p;
        }
        result.Reverse();
        if (result.Count > 0) result[result.Count - 1] = exactEnd;
        else result.Add(exactEnd);
        return result;
    }

    float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        // Octile distance
        return (dx + dy + (1.414f - 2f) * Mathf.Min(dx, dy)) * TileSize;
    }
}

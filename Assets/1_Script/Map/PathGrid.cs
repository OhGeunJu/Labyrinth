using System.Collections.Generic;
using UnityEngine;

// 단일 A* 격자. L1(tileSize=1), L2(tileSize=100), L3(tileSize=1000) 공용.
public class PathGrid
{
    public readonly int     Width;
    public readonly int     Height;
    public readonly float   TileSize;
    public Vector3 Origin { get; private set; }   // 셀(0,0) 좌하단 월드 좌표

    // 같은 크기 격자를 다른 위치로 재사용(윈도우 풀링용). 이후 재베이크 필요.
    public void Rebase(Vector3 origin) => Origin = origin;

    readonly float[] _cost;       // 이동 비용 배율 (1.0 = 기본)
    readonly bool[]  _walkable;

    // 경로 계산 시 재사용 버퍼 (GC 절약)
    float[] _g, _f;
    int[]   _parent;
    bool[]  _inClose;

    // 이진 최소 힙 (open set). decrease-key 지원 인덱스 힙.
    int[] _heap;      // 힙 슬롯 → 노드 인덱스
    int[] _heapPos;   // 노드 인덱스 → 힙 슬롯 (-1 = 힙에 없음)
    int   _heapCount;

    static readonly (int dx, int dy)[] Dirs8 =
    {
        ( 1, 0), (-1, 0), ( 0, 1), ( 0,-1),
        ( 1, 1), ( 1,-1), (-1, 1), (-1,-1),
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
        _inClose = new bool[n];
        _heap    = new int[n];
        _heapPos = new int[n];
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

    public bool InWorldBounds(Vector3 w) =>
        w.x >= Origin.x && w.x < Origin.x + Width  * TileSize &&
        w.z >= Origin.z && w.z < Origin.z + Height * TileSize;

    // ── 타일 데이터 ─────────────────────────────────────────────────────
    public void  SetWalkable(int x, int y, bool w)     { if (InBounds(x,y)) _walkable[y*Width+x] = w; }
    public void  SetCost    (int x, int y, float cost) { if (InBounds(x,y)) _cost    [y*Width+x] = Mathf.Max(0.1f, cost); }
    public bool  IsWalkable (int x, int y) => InBounds(x,y) && _walkable[y*Width+x];
    public float GetCost    (int x, int y) => InBounds(x,y) ? _cost[y*Width+x] : float.MaxValue;

    // ── Theta* (any-angle) ─────────────────────────────────────────────
    // 표준 A*와 달리 가시선(LineOfSight)이 확보되면 부모를 건너뛰어 직선 경로를
    // 만든다. 결과는 계단식이 아닌 매끄러운 any-angle 웨이포인트.
    // 반환: 월드 좌표 웨이포인트 리스트 (시작점 제외, toWorld 포함). 경로 없으면 null.
    public List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld)
    {
        var start = WorldToCell(fromWorld);
        var end   = WorldToCell(toWorld);
        if (start == end) return new List<Vector3> { toWorld };
        // 시작 셀이 막혀도(반경 부풀림으로 벽에 붙은 경우 등) 탈출은 허용. 도착 셀만 검사.
        if (!IsWalkable(end.x, end.y)) return null;

        int n = Width * Height;

        // 버퍼 초기화
        for (int i = 0; i < n; i++)
        {
            _g[i] = float.MaxValue; _parent[i] = -1;
            _heapPos[i] = -1; _inClose[i] = false;
        }
        _heapCount = 0;

        int si = start.y * Width + start.x;
        int ei = end.y   * Width + end.x;

        _g[si]      = 0f;
        _f[si]      = Heuristic(start, end);
        _parent[si] = si;            // 시작점의 부모는 자기 자신
        HeapPush(si);

        while (_heapCount > 0)
        {
            int cur = HeapPopMin();
            _inClose[cur] = true;

            if (cur == ei) return BuildPath(ei, toWorld);

            int cx = cur % Width, cy = cur / Width;

            foreach (var (dx, dy) in Dirs8)
            {
                int nx = cx + dx, ny = cy + dy;
                if (!InBounds(nx, ny)) continue;
                int ni = ny * Width + nx;
                if (!_walkable[ni] || _inClose[ni]) continue;

                UpdateVertex(cur, ni, nx, ny, end);
            }
        }
        return null;
    }

    // Theta* 정점 완화. cur의 부모에서 ni가 보이면 부모를 직접 연결(Path 2),
    // 아니면 표준 A* 간선(Path 1).
    void UpdateVertex(int cur, int ni, int nx, int ny, Vector2Int end)
    {
        int par  = _parent[cur];
        int px   = par % Width, py = par / Width;

        float ng;
        int   nparent;

        // Path 2: 부모 → ni 직선 가시선 확보 시
        if (LineOfSight(px, py, nx, ny))
        {
            ng      = _g[par] + EuclidWorld(px, py, nx, ny) * _cost[ni];
            nparent = par;
        }
        else // Path 1: 표준 격자 간선
        {
            int cx = cur % Width, cy = cur / Width;
            ng      = _g[cur] + EuclidWorld(cx, cy, nx, ny) * _cost[ni];
            nparent = cur;
        }

        if (ng >= _g[ni]) return;

        _g[ni]      = ng;
        _f[ni]      = ng + Heuristic(new Vector2Int(nx, ny), end);
        _parent[ni] = nparent;

        // g가 감소했으므로 힙에 있으면 sift-up(decrease-key), 없으면 새로 push.
        if (_heapPos[ni] >= 0) HeapSiftUp(_heapPos[ni]);
        else                   HeapPush(ni);
    }

    // ── 이진 최소 힙 (키 = _f) ──────────────────────────────────────────
    void HeapPush(int node)
    {
        int i = _heapCount++;
        _heap[i] = node;
        _heapPos[node] = i;
        HeapSiftUp(i);
    }

    int HeapPopMin()
    {
        int root = _heap[0];
        _heapPos[root] = -1;
        int last = --_heapCount;
        if (last > 0)
        {
            int moved = _heap[last];
            _heap[0] = moved;
            _heapPos[moved] = 0;
            HeapSiftDown(0);
        }
        return root;
    }

    void HeapSiftUp(int i)
    {
        int node = _heap[i];
        float f  = _f[node];
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            int pNode  = _heap[parent];
            if (_f[pNode] <= f) break;
            _heap[i] = pNode; _heapPos[pNode] = i;
            i = parent;
        }
        _heap[i] = node; _heapPos[node] = i;
    }

    void HeapSiftDown(int i)
    {
        int node = _heap[i];
        float f  = _f[node];
        int half = _heapCount >> 1;   // 마지막 내부 노드까지
        while (i < half)
        {
            int child  = (i << 1) + 1;
            int right  = child + 1;
            int cNode  = _heap[child];
            if (right < _heapCount && _f[_heap[right]] < _f[cNode])
            { child = right; cNode = _heap[right]; }
            if (_f[cNode] >= f) break;
            _heap[i] = cNode; _heapPos[cNode] = i;
            i = child;
        }
        _heap[i] = node; _heapPos[node] = i;
    }

    // 두 셀 중심 사이 월드 거리.
    float EuclidWorld(int ax, int ay, int bx, int by)
    {
        float dx = (ax - bx) * TileSize;
        float dy = (ay - by) * TileSize;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Bresenham 기반 가시선 검사. 두 셀을 잇는 선이 통과하는 모든 셀이
    // 이동 가능하면 true. (Theta* 표준 LineOfSight)
    bool LineOfSight(int x0, int y0, int x1, int y1)
    {
        int dx = x1 - x0, dy = y1 - y0;
        int sx, sy, f = 0;
        if (dy < 0) { dy = -dy; sy = -1; } else sy = 1;
        if (dx < 0) { dx = -dx; sx = -1; } else sx = 1;

        int ox = (sx - 1) / 2;   // sx=1→0, sx=-1→-1
        int oy = (sy - 1) / 2;

        if (dx >= dy)
        {
            while (x0 != x1)
            {
                f += dy;
                if (f >= dx)
                {
                    if (!IsWalkable(x0 + ox, y0 + oy)) return false;
                    y0 += sy;
                    f  -= dx;
                }
                if (f != 0 && !IsWalkable(x0 + ox, y0 + oy)) return false;
                if (dy == 0 && !IsWalkable(x0 + ox, y0) && !IsWalkable(x0 + ox, y0 - 1)) return false;
                x0 += sx;
            }
        }
        else
        {
            while (y0 != y1)
            {
                f += dx;
                if (f >= dy)
                {
                    if (!IsWalkable(x0 + ox, y0 + oy)) return false;
                    x0 += sx;
                    f  -= dy;
                }
                if (f != 0 && !IsWalkable(x0 + ox, y0 + oy)) return false;
                if (dx == 0 && !IsWalkable(x0, y0 + oy) && !IsWalkable(x0 - 1, y0 + oy)) return false;
                y0 += sy;
            }
        }
        return true;
    }

    List<Vector3> BuildPath(int endIdx, Vector3 exactEnd)
    {
        var result = new List<Vector3>();
        int cur = endIdx;
        // 시작점은 부모가 자기 자신. 시작점에 도달하면 멈춰 시작점은 제외한다.
        while (cur != _parent[cur])
        {
            result.Add(CellToWorld(new Vector2Int(cur % Width, cur / Width)));
            cur = _parent[cur];
        }
        result.Reverse();
        if (result.Count > 0) result[result.Count - 1] = exactEnd;
        else result.Add(exactEnd);
        return result;
    }

    float Heuristic(Vector2Int a, Vector2Int b)
    {
        // any-angle(Theta*)에서는 직선거리가 최단이므로 유클리드가 admissible.
        float dx = (a.x - b.x) * TileSize;
        float dy = (a.y - b.y) * TileSize;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}

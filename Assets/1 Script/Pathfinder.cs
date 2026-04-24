using System.Collections.Generic;
using UnityEngine;

public static class Pathfinder
{
    sealed class Node
    {
        public Vector2Int Pos;
        public Node Parent;
        public float G = float.MaxValue;
        public float F;
        public int HeapIndex = -1;
    }

    sealed class OpenHeap
    {
        readonly List<Node> _h = new();
        public int Count => _h.Count;

        public void Push(Node n)
        {
            n.HeapIndex = _h.Count;
            _h.Add(n);
            BubbleUp(n.HeapIndex);
        }

        public Node Pop()
        {
            var top = _h[0];
            top.HeapIndex = -1;
            int last = _h.Count - 1;
            if (last > 0)
            {
                _h[0] = _h[last];
                _h[0].HeapIndex = 0;
                _h.RemoveAt(last);
                SiftDown(0);
            }
            else _h.RemoveAt(0);
            return top;
        }

        // F가 감소한 노드를 히프 위로 올림 (감소 키 연산)
        public void DecreasedKey(Node n) => BubbleUp(n.HeapIndex);

        void BubbleUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_h[p].F <= _h[i].F) break;
                Swap(i, p);
                i = p;
            }
        }

        void SiftDown(int i)
        {
            int n = _h.Count;
            while (true)
            {
                int l = (i << 1) | 1, r = l + 1, s = i;
                if (l < n && _h[l].F < _h[s].F) s = l;
                if (r < n && _h[r].F < _h[s].F) s = r;
                if (s == i) break;
                Swap(i, s);
                i = s;
            }
        }

        void Swap(int a, int b)
        {
            (_h[a], _h[b]) = (_h[b], _h[a]);
            _h[a].HeapIndex = a;
            _h[b].HeapIndex = b;
        }
    }

    static readonly Vector2Int[] Dirs = {
        new( 1,  0), new(-1,  0), new( 0,  1), new( 0, -1),
        new( 1,  1), new( 1, -1), new(-1,  1), new(-1, -1),
    };

    /// <summary>
    /// Theta* 경로 탐색. 월드 좌표 반환 (타일 중심 + 마지막은 toWorld 그대로).
    /// 경로 없으면 null.
    /// </summary>
    public static List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld)
    {
        var map = MapManager.Instance;
        if (map == null) return null;

        var start = map.WorldToTile(fromWorld);
        var goal  = map.WorldToTile(toWorld);

        if (!map.IsInBounds(start) || !map.IsInBounds(goal)) return null;
        if (!map.GetTile(goal).IsPassable) return null;
        if (start == goal) return new List<Vector3> { toWorld };

        var nodes  = new Dictionary<Vector2Int, Node>();
        var open   = new OpenHeap();
        var closed = new HashSet<Vector2Int>();

        var s = Fetch(nodes, start);
        s.G      = 0f;
        s.Parent = s;
        s.F      = Heuristic(start, goal);
        open.Push(s);

        while (open.Count > 0)
        {
            var cur = open.Pop();
            if (closed.Contains(cur.Pos)) continue;
            if (cur.Pos == goal) return Reconstruct(cur, map, toWorld);
            closed.Add(cur.Pos);

            foreach (var dir in Dirs)
            {
                var np = cur.Pos + dir;
                if (!map.IsInBounds(np)) continue;
                if (!map.GetTile(np).IsPassable) continue;
                if (closed.Contains(np)) continue;

                // 대각선 이동 시 코너 컷 방지
                if (dir.x != 0 && dir.y != 0)
                    if (!map.GetTile(cur.Pos.x + dir.x, cur.Pos.y).IsPassable ||
                        !map.GetTile(cur.Pos.x, cur.Pos.y + dir.y).IsPassable)
                        continue;

                var nb = Fetch(nodes, np);

                // Theta*: 조부모→이웃 시야 확보 시 중간 노드 건너뜀
                Node  newParent;
                float newG;
                var   gp = cur.Parent;
                if (LineOfSight(gp.Pos, np))
                {
                    newG      = gp.G + Vector2.Distance(gp.Pos, np);
                    newParent = gp;
                }
                else
                {
                    newG      = cur.G + Vector2.Distance(cur.Pos, np);
                    newParent = cur;
                }

                if (newG < nb.G)
                {
                    nb.G      = newG;
                    nb.Parent = newParent;
                    nb.F      = newG + Heuristic(np, goal);

                    if (nb.HeapIndex >= 0) open.DecreasedKey(nb);
                    else                   open.Push(nb);
                }
            }
        }

        return null;
    }

    // 타일 중심 노드 기반 LOS: Bresenham 슈퍼커버 방식으로 선분이 지나는 모든 타일 검사
    static bool LineOfSight(Vector2Int a, Vector2Int b)
    {
        var map = MapManager.Instance;
        int x = a.x, y = a.y;
        int dx = Mathf.Abs(b.x - x), dy = Mathf.Abs(b.y - y);
        int sx = b.x > x ? 1 : -1, sy = b.y > y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (!Passable(map, x, y)) return false;
            if (x == b.x && y == b.y) return true;

            int e2 = 2 * err;

            if (e2 > -dy && e2 < dx)
            {
                // 대각 이동: 양쪽 직교 타일도 차단이면 코너 컷 불가
                if (!Passable(map, x + sx, y) || !Passable(map, x, y + sy)) return false;
                err -= dy; x += sx;
                err += dx; y += sy;
            }
            else if (e2 > -dy)
            {
                err -= dy; x += sx;
            }
            else
            {
                err += dx; y += sy;
            }
        }
    }

    static bool Passable(MapManager map, int x, int y) =>
        map.IsInBounds(x, y) && map.GetTile(x, y).IsPassable;

    static float Heuristic(Vector2Int a, Vector2Int b) =>
        Vector2.Distance(a, b);

    static Node Fetch(Dictionary<Vector2Int, Node> nodes, Vector2Int pos)
    {
        if (!nodes.TryGetValue(pos, out var n))
            nodes[pos] = n = new Node { Pos = pos };
        return n;
    }

    static List<Vector3> Reconstruct(Node node, MapManager map, Vector3 exactGoal)
    {
        var path = new List<Vector3>();
        var cur  = node;
        while (cur.Parent != cur)
        {
            path.Add(map.TileToWorld(cur.Pos));
            cur = cur.Parent;
        }
        path.Add(map.TileToWorld(cur.Pos));
        path.Reverse();
        path[path.Count - 1] = exactGoal; // 마지막 웨이포인트는 정확한 목표 지점
        return path;
    }
}

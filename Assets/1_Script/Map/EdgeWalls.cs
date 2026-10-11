using System;
using System.Collections.Generic;
using UnityEngine;

// **칸과 칸 사이를 막는 벽 — 칸이 아니라 변이 막힌다**(2026-10-01, 사용자: *"건물 벽은 얇고 한쪽에 딱 붙는
// 느낌으로"*).
//
// 지금까지 길을 막는 것은 전부 **칸**이었다(`MapManager.SetOccupied`) — 벽도, 집 껍데기의 벽 줄도 한 칸을 통째로
// 먹었다. 그러면 방 안쪽이 벽 두께만큼 늘 한 칸씩 줄고, 7칸짜리 집의 안이 5칸이다. **얇은 벽은 변에 선다**:
// 칸은 비어 있고(침대·작업대를 벽에 붙여 놓을 수 있다) 지나갈 수 없는 것은 그 변 하나다.
//
// ## 답은 여기 하나다
//
// 묻는 곳이 셋이다 — 로컬 창(`PathGrid.Enterable`·지름길 `_tight`) · 광역 그래프(`WideGraph`의 덩어리 나누기와
// 경계 통과점) · 배치(벽이 가로지르는 발자국). 셋이 각자 표를 들면 *"광역은 지나간다는데 로컬은 막혔다"*가 생기고
// 폰이 벽 앞에서 왕복한다(광역 격자 시절에 이미 겪은 모양이다).
//
// ## 변의 이름
//
// 변은 **아래·왼쪽 칸에 매단다** — `(x, z, 0)`은 칸 (x,z)의 **북쪽** 변, `(x, z, 1)`은 **동쪽** 변이다. 그래서
// 칸 (x,z)의 남쪽 변은 `(x, z−1, 0)`이고 서쪽 변은 `(x−1, z, 1)`이다. 이렇게 두면 한 변에 이름이 하나뿐이라
// 양쪽 칸에 두 번 적다가 한쪽만 지우는 일이 없다.
//
// **세는 것은 수다**(있다/없다가 아니다) — 같은 변에 벽 둘이 겹쳐 서는 것은 배치가 막지만, 철거 순서가 꼬여도
// 먼저 지운 하나가 남은 하나의 막음을 풀지 않게.
public static class EdgeWalls
{
    // 방위 — `FortWall`과 같은 순서(북 동 남 서).
    public const int North = 0, East = 1, South = 2, West = 3;

    static readonly Dictionary<Vector3Int, int> _count = new();

    // 변이 막히거나 풀렸다 — 길찾기가 듣는다. 인자는 변에 닿은 두 칸 중 아래·왼쪽 칸.
    public static event Action<Vector2Int> OnChanged;

    public static int Count => _count.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _count.Clear(); OnChanged = null; }

    static Vector3Int Key(Vector2Int tile, int dir) => dir switch
    {
        North => new Vector3Int(tile.x,     tile.y,     0),
        East  => new Vector3Int(tile.x,     tile.y,     1),
        South => new Vector3Int(tile.x,     tile.y - 1, 0),
        _     => new Vector3Int(tile.x - 1, tile.y,     1),
    };

    // 회전(도)에서 방위 — 건물의 앞(+Z)이 가리키는 쪽이 벽이 서는 변이다.
    public static int DirFromYaw(float yaw) => Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) & 3;

    public static Vector2Int Step(int dir) => dir switch
    {
        North => Vector2Int.up, East => Vector2Int.right, South => Vector2Int.down, _ => Vector2Int.left,
    };

    public static void Add(Vector2Int tile, int dir)
    {
        var k = Key(tile, dir);
        _count.TryGetValue(k, out int n);
        _count[k] = n + 1;
        if (n == 0) OnChanged?.Invoke(new Vector2Int(k.x, k.y));
    }

    public static void Remove(Vector2Int tile, int dir)
    {
        var k = Key(tile, dir);
        if (!_count.TryGetValue(k, out int n)) return;
        if (n <= 1) { _count.Remove(k); OnChanged?.Invoke(new Vector2Int(k.x, k.y)); }
        else _count[k] = n - 1;
    }

    public static bool Has(Vector2Int tile, int dir) => _count.Count > 0 && _count.ContainsKey(Key(tile, dir));

    // 칸의 네 변 중 막힌 것(비트 — 1 북 · 2 동 · 4 남 · 8 서).
    public static int MaskAt(Vector2Int tile)
    {
        if (_count.Count == 0) return 0;
        int m = 0;
        for (int d = 0; d < 4; d++) if (_count.ContainsKey(Key(tile, d))) m |= 1 << d;
        return m;
    }

    // **한 걸음이 지나갈 수 있나** — 직교는 그 변 하나, 대각선은 **모퉁이를 도는 두 길이 다 트였을 때만**
    // (`PathGrid.Enterable`의 *"대각선은 직교 이웃 둘이 다 열렸을 때만"*과 같은 규칙 — 벽 끝 모서리를 비스듬히
    // 스쳐 지나가면 몸이 벽 끝에 걸린다).
    public static bool StepOpen(int ax, int az, int bx, int bz)
    {
        if (_count.Count == 0) return true;

        int dx = bx - ax, dz = bz - az;
        if (dx == 0 && dz == 0) return true;

        if (dx == 0 || dz == 0)
        {
            int dir = dz > 0 ? North : dz < 0 ? South : dx > 0 ? East : West;
            return !_count.ContainsKey(Key(new Vector2Int(ax, az), dir));
        }

        int h = dx > 0 ? East : West, v = dz > 0 ? North : South;
        var a = new Vector2Int(ax, az);
        var side1 = new Vector2Int(bx, az);   // 옆으로 먼저
        var side2 = new Vector2Int(ax, bz);   // 위아래로 먼저
        return !_count.ContainsKey(Key(a, h)) && !_count.ContainsKey(Key(side1, v))
            && !_count.ContainsKey(Key(a, v)) && !_count.ContainsKey(Key(side2, h));
    }

    // **발자국 안을 벽이 가로지르나** — 작업대를 벽 위에 걸쳐 놓지 못하게(`BuildModeManager`).
    public static bool CrossesFootprint(ICollection<Vector2Int> footprint)
    {
        if (_count.Count == 0) return false;
        foreach (var t in footprint)
        {
            if (footprint.Contains(t + Vector2Int.up)    && Has(t, North)) return true;
            if (footprint.Contains(t + Vector2Int.right) && Has(t, East))  return true;
        }
        return false;
    }
}

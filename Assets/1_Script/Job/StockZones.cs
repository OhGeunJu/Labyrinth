using System.Collections.Generic;
using UnityEngine;

// **저장 구역 — 땅에 칠해 두면 시체가 거기로 모인다**(2026-10-06, 사용자: *"저장공간을 설정하는 것도 만들어야할 거
// 같아 림월드처럼"*).
//
// **지금은 시체만 받는다**(사용자 결정: *"시체부터"*). 물건은 창고 건물의 칸 인벤토리가 받고(`Storage`), 시체만
// 바닥에 몸으로 남는다 — 그 몸이 *기지 어디에나 널브러져 있는 것*을 *한자리에 모아 두는 것*으로 바꾸는 것이 이
// 구역의 일이다. 물건을 받게 되는 날에는 세는 곳(`MaterialPool`)·운반 목적지(`Storage.FindNearest`)가 구역을
// 알아야 한다 — 그것은 다른 축이라 여기 미리 섞지 않는다.
//
// ## 어기면 조용히 깨지는 것
//
// - **핵 영역 안에만 칠한다**(사용자 결정 — 밭·건설과 같은 선). 영역 밖은 휴면이라 그 구역을 아무도 돌보지 않는다.
//   **영역이 줄면 칸은 남되 안 쓴다**(`Contains`가 그때마다 묻는다) — 지우면 핵을 옮겼다 되돌릴 때 칠한 것이 사라진다.
// - **칸 하나에 몸 하나**(`TryClaimFree`) — 겹쳐 쌓으면 커서로 하나만 집히고 나머지는 그 밑에 묻힌다. 걸어오는
//   중인 폰도 자리를 쥔다(`_claims`) — 안 쥐면 둘이 같은 칸으로 와서 하나가 그 위에 내려놓는다.
// - **바위·절벽이 된 칸은 안 쓴다** — 판 굴은 이틀 뒤 메워진다(`RockRegrow`). 칠한 채 남은 칸이 벽 속을 가리키면
//   폰이 벽까지 메고 걸어간다. 그래서 칠할 때만이 아니라 **쓸 때마다** 묻는다(`Usable`).
public static class StockZones
{
    static readonly HashSet<Vector2Int>              _tiles  = new();
    static readonly Dictionary<Vector2Int, JobBase> _claims = new();

    // 칸이 바뀐 횟수 — 테두리(`StockZoneOverlay`)가 *다시 지을 때*를 이것으로 안다.
    public static int Version { get; private set; }

    public static IReadOnlyCollection<Vector2Int> Tiles => _tiles;

    // 칠했나(쓰이는지와 별개) — 테두리가 이웃 변을 가를 때 묻는다.
    public static bool IsPainted(Vector2Int tile) => _tiles.Contains(tile);

    // 칠할 수 있나 — 못 하면 왜인지(커서 옆에 띄운다). 판정과 같은 코드가 답한다(`WhyNotBuildable`과 같은 규약).
    public static string WhyNot(Vector2Int tile)
    {
        var map = MapManager.Instance;
        if (map == null) return "지도가 없다";
        if (!TerrainGrid.TileInWorld(tile)) return "세계 밖이다";
        if (!map.HasHeadroom(tile)) return "바위나 절벽이다";
        if (!DungeonCore.AnyCovers(map.TileToWorld(tile))) return "핵 영역 밖이다";
        return null;
    }

    // 사각형(두 모서리 칸)을 칠한다 — 칠할 수 없는 칸은 건너뛴다. 새로 칠한 칸 수를 돌려준다.
    public static int Paint(Vector2Int a, Vector2Int b)
    {
        int n = 0;
        ForRect(a, b, t => { if (WhyNot(t) == null && _tiles.Add(t)) n++; });
        if (n > 0) Version++;
        return n;
    }

    public static int Erase(Vector2Int a, Vector2Int b)
    {
        int n = 0;
        ForRect(a, b, t => { if (_tiles.Remove(t)) n++; });
        if (n > 0) Version++;
        return n;
    }

    static void ForRect(Vector2Int a, Vector2Int b, System.Action<Vector2Int> each)
    {
        int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            each(new Vector2Int(x, y));
    }

    // **지금 쓰이는 구역 칸인가** — 칠했고, 아직 영역 안이고, 바위가 안 됐다.
    public static bool Contains(Vector2Int tile) => _tiles.Contains(tile) && Usable(tile);

    public static bool Contains(Vector3 world)
    {
        var map = MapManager.Instance;
        return map != null && _tiles.Count > 0 && Contains(map.WorldToTile(world));
    }

    static bool Usable(Vector2Int tile)
    {
        var map = MapManager.Instance;
        return map != null && map.HasHeadroom(tile) && DungeonCore.AnyCovers(map.TileToWorld(tile));
    }

    // 쓰이는 칸이 하나라도 있나 — 운반을 걸기 전에 묻는다(없으면 시체를 훑을 것도 없다).
    public static bool Any()
    {
        foreach (var t in _tiles) if (Usable(t)) return true;
        return false;
    }

    // ── 자리 잡기 ───────────────────────────────────────────────────────────

    // **`near`에서 가장 가까운 빈 칸을 이 일감 몫으로 쥔다.** 빈 칸 = 몸이 안 누워 있고 남이 안 쥔 칸.
    // 같은 층만 본다 — 층을 넘는 길은 통로·포탈뿐이라 옆 층 칸은 걸어서 못 간다.
    public static bool TryClaimFree(Vector3 near, JobBase job, out Vector2Int tile)
    {
        tile = default;
        var map = MapManager.Instance;
        if (map == null || job == null || _tiles.Count == 0) return false;

        PruneClaims();
        var lying = LyingTiles(map);
        int floor = Floors.At(near);

        bool found = false;
        float best = float.MaxValue;
        foreach (var t in _tiles)
        {
            if (_claims.ContainsKey(t) || lying.Contains(t) || !Usable(t)) continue;
            var w = map.TileToWorld(t);
            if (Floors.At(w) != floor) continue;
            float d = (new Vector2(w.x - near.x, w.z - near.z)).sqrMagnitude;
            if (d >= best) continue;
            best = d; tile = t; found = true;
        }
        if (found) _claims[tile] = job;
        return found;
    }

    public static void Release(JobBase job)
    {
        if (job == null || _claims.Count == 0) return;
        _gone.Clear();
        foreach (var kv in _claims) if (kv.Value == job) _gone.Add(kv.Key);
        foreach (var t in _gone) _claims.Remove(t);
    }

    static readonly List<Vector2Int> _gone = new();

    // 끝났거나 취소된 일감이 쥔 자리를 놓는다 — 놓는 길(`Release`)을 빠뜨린 경로가 있어도 자리가 영영 묶이지 않게.
    static void PruneClaims()
    {
        _gone.Clear();
        foreach (var kv in _claims)
            if (kv.Value == null || kv.Value.Status == JobStatus.Done || kv.Value.Status == JobStatus.Cancelled)
                _gone.Add(kv.Key);
        foreach (var t in _gone) _claims.Remove(t);
    }

    // 몸이 누워 있는 칸 — 묻혔거나 어딘가에 놓인 몸(업힘·화장터·무덤 — 남의 자식)은 바닥에 없다.
    static HashSet<Vector2Int> LyingTiles(MapManager map)
    {
        var set = new HashSet<Vector2Int>();
        foreach (var c in Corpse.All)
            if (c != null && !c.Buried && c.transform.parent == null)
                set.Add(map.WorldToTile(c.transform.position));
        return set;
    }
}

using UnityEngine;

// **미궁의 층 — 한 평면에 2×2로 나란히 놓는다**(2026-10-01, 사용자 결정: *"층 하나 2km로 2×2 붙여서"* ·
// 배치 *"1층 남서 · 2층 남동 · 3층 북서 · 4층 북동"*).
//
// ```
//   z ↑   ┌────────┬────────┐
//         │  3층   │  4층   │
//         ├────────┼────────┤   한 층 = 한 사분면(2,048m)
//         │  1층   │  2층   │   사이는 기반암(`Seam`)
//         └────────┴────────┘ → x
// ```
//
// ## 왜 쌓지 않고 나란히인가
//
// 이 저장소는 **"한 XZ에 바닥이 하나"** 위에 서 있다(높이 함수 · `PathGrid` · 점유 · 안개 · 지도). 층을 같은 XZ에
// 쌓으면 그 넷이 전부 깨진다. 나란히 두면 층마다 **좌표 자체가 달라** 아무것도 안 깨지고, *층을 바꾼다*는 것은
// **카메라가 다른 사분면으로 가는 것**이 된다. 층 사이는 기반암이라 걸어서 못 넘고, 저작한 통로(`FloorPassage`)와
// 포탈만 잇는다 — 설계 문서의 *"한 번에 한 층만 선다 · 층 사이는 저작한 고정 통로"* 그대로다.
//
// ## 답은 여기 하나다
//
// *"이 자리는 몇 층인가"* · *"이 층의 경계는 어디인가"* · *"지금 보고 있는 층은"*. 길찾기·안개·지도·카메라가 층
// 단위로 갈라 들면서 이 물음이 여럿에 흩어진다 — 각자 사분면을 셈하면 그중 하나가 반드시 반 칸 어긋난다.
public static class Floors
{
    public const int PerAxis = 2;
    public const int Count   = PerAxis * PerAxis;   // 넷 — 사용자가 정한 층 수(2026-09-15)

    // 층 사이 기반암의 반폭(칸). 경계 양쪽으로 이만큼 — 세계 테두리의 기반암(3칸)과 같은 두께.
    public const int Seam = 3;

    // 층 한 변(m = 칸).
    public static int Size => TerrainGrid.WorldSize / PerAxis;

    // ── 층 번호 ↔ 사분면 ─────────────────────────────────────────────────────

    // 1 남서 · 2 남동 · 3 북서 · 4 북동.
    public static Vector2Int Quadrant(int floor)
    {
        int i = Mathf.Clamp(floor, 1, Count) - 1;
        return new Vector2Int(i % PerAxis, i / PerAxis);
    }

    public static int OfQuadrant(int qx, int qz) =>
        1 + Mathf.Clamp(qx, 0, PerAxis - 1) + Mathf.Clamp(qz, 0, PerAxis - 1) * PerAxis;

    public static int At(float worldX, float worldZ) =>
        OfQuadrant(Mathf.FloorToInt(worldX / Size), Mathf.FloorToInt(worldZ / Size));

    public static int At(Vector3 world) => At(world.x, world.z);

    public static int At(Vector2Int tile) => At(tile.x + 0.5f, tile.y + 0.5f);

    public static bool Same(Vector3 a, Vector3 b) => At(a) == At(b);

    // 층의 남서 모서리(월드 m).
    public static Vector2 Origin(int floor)
    {
        var q = Quadrant(floor);
        return new Vector2(q.x * Size, q.y * Size);
    }

    public static Vector2Int TileOrigin(int floor)
    {
        var q = Quadrant(floor);
        return new Vector2Int(q.x * Size, q.y * Size);
    }

    public static Vector3 Center(int floor)
    {
        var o = Origin(floor);
        return new Vector3(o.x + Size * 0.5f, 0f, o.y + Size * 0.5f);
    }

    // 층 안의 좌표(0 ~ Size).
    public static Vector2 Local(float worldX, float worldZ)
    {
        var o = Origin(At(worldX, worldZ));
        return new Vector2(worldX - o.x, worldZ - o.y);
    }

    // **층 사이 기반암인가** — 사분면 경계 양쪽 `Seam` 칸. 세계 바깥 테두리는 `RockMap`이 따로 막는다.
    public static bool IsSeam(int tx, int tz)
    {
        int s = Size;
        int lx = ((tx % s) + s) % s, lz = ((tz % s) + s) % s;
        bool nearX = (lx < Seam && tx >= s) || (lx >= s - Seam && tx < TerrainGrid.WorldSize - s);
        bool nearZ = (lz < Seam && tz >= s) || (lz >= s - Seam && tz < TerrainGrid.WorldSize - s);
        return nearX || nearZ;
    }

    // ── 지금 보는 층 ─────────────────────────────────────────────────────────

    // **카메라가 답한다** — 층 전환은 카메라가 다른 사분면으로 가는 것이다(거점 바 · 지도 층 탭 · 통로).
    // 카메라가 없으면(에디터 · 판 시작 전) 조작 중인 핵 · 그도 없으면 1층.
    public static int Current
    {
        get
        {
            var cam = CameraController.Active;
            if (cam != null) return At(cam.transform.position);
            var core = DungeonCore.Instance;
            return core != null ? At(core.transform.position) : 1;
        }
    }

    // 층이 바뀌었다 — 지도·안개처럼 층마다 다른 것을 드는 쪽이 듣는다. `CameraController`가 알린다.
    public static event System.Action<int> OnCurrentChanged;

    static int _lastCurrent;

    // 매 프레임 카메라가 부른다 — 바뀐 프레임에만 알린다.
    public static void Poll()
    {
        int now = Current;
        if (now == _lastCurrent) return;
        _lastCurrent = now;
        OnCurrentChanged?.Invoke(now);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _lastCurrent = 0; OnCurrentChanged = null; }
}

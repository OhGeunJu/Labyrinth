using System.Collections.Generic;
using UnityEngine;

// **이 칸이 바위인가 — 답은 여기 하나다**(2026-08-29).
//
// 지하는 **사방이 바위이고 캐면서 넓혀 나가는 곳**이다(사용자 결정 — 원래 던전 코어 기획).
// 그러니 세계의 기본 상태는 *열린 굴*이 아니라 **꽉 찬 돌**이고, 플레이어가 하는 일의
// 절반이 그 돌을 파내는 것이다.
//
// ── 바닥·천장과 무엇이 다른가 ──────────────────────────────────────────────
//
//   `TerrainHeight`   바닥 높이  — 함수(노이즈 + 델타). **2m 정점 격자.**
//   `TerrainCeiling`  천장 높이  — 함수(여유). **4m 정점 격자.** 파낸 공간 위의 뚜껑.
//   `RockMap`(여기)   바위인가   — **1m 칸.** 캘 수 있고, 캐면 그 칸만 열린다.
//
// **칸 단위여야 하는 것이 요점이다.** 천장 메시는 4m 간격이라 한 칸을 팔 수가 없다 —
// 그래서 바위는 천장이 아니라 **바닥과 같은 물건**이다: 칸 데이터이고 청크 메시로 그린다
// (`MapManager._floored`와 `FloorRenderer`가 이미 그 모양이다).
//
// ── 무엇이 바위인가 ────────────────────────────────────────────────────────
//
// **천장의 여유를 그대로 쓴다.** 여유가 폰 키보다 작은 자리는 어차피 못 지나가는 곳이고,
// 그게 곧 돌이다. 함수를 하나 더 만들면 *"천장은 열렸는데 바위가 서 있는"* 칸이 생긴다.
//
//     여유 < 폰 키   →  바위       (기본값이 음수라 대부분이 여기다)
//     여유 ≥ 폰 키   →  뚫린 굴    (드물다 — 저절로 난 동굴)
//
// **캔 칸은 절대좌표로 기억한다**(`_mined`) — 핵을 옮겨도, 청크가 내려갔다 올라와도 유지된다.
// 바닥 점유가 쓰는 규칙 그대로다.
public static class RockMap
{
    // **천장을 따라간다** — 지하가 아니면 바위도 없다. 축을 따로 두면 *"천장은 껐는데 바위가
    // 서 있는"* 씬이 생긴다.
    public static bool Enabled => TerrainCeiling.Enabled;

    // 바위 한 칸의 높이(m). **딛는 면에서 이만큼 솟는다.**
    //
    // 폰 키(1.8m)보다 넉넉히 높아야 **벽으로 읽힌다** — 낮으면 넘어 다닐 수 있을 것처럼
    // 보이는데 실제로는 못 넘어서, 화면과 판정이 어긋난다.
    // **2026-08-29에 3.2 → 9.6m로 올렸다**(사용자). 3.2m는 폰 키의 두 배가 채 안 돼서
    // 위에서 내려다보면 **벽이 낮은 턱**처럼 보였다 — 참고 화면의 그 깊은 굴이 안 나온다.
    // 9.6m면 폰의 다섯 배라 **파낸 방이 우물처럼 깊게** 읽힌다.
    //
    // **높여도 판정은 안 바뀐다** — 지나갈 수 있나는 여유(`TerrainCeiling`)가 답하고
    // 이 값은 **그리는 높이일 뿐**이다. 다만 옆면이 그만큼 커져 정점이 는다.
    public const float Height = 9.6f;

    // ── 캔 칸 ───────────────────────────────────────────────────────────────

    static readonly HashSet<Vector2Int> _mined = new();

    // 캘 것으로 찍어 둔 칸. **바위인지와 다른 축이다** — 찍어 놓고 아직 안 판 상태가 있다.
    static readonly HashSet<Vector2Int> _marked = new();

    // 그리는 쪽이 듣는다. **바닥과 같은 이름·같은 모양**(`MapManager.OnFloorChanged`).
    public static event System.Action<Vector2Int> OnChanged;

    public static IEnumerable<Vector2Int> MinedTiles => _mined;
    public static IEnumerable<Vector2Int> MarkedTiles => _marked;

    public static int MinedCount  => _mined.Count;
    public static int MarkedCount => _marked.Count;

    // ── 묻기 ────────────────────────────────────────────────────────────────

    public static bool IsRock(Vector2Int tile)
    {
        if (!Enabled) return false;
        if (_mined.Contains(tile)) return false;

        // **칸 한가운데에서 잰다** — 모서리에서 재면 이웃 칸과 답이 갈려 벽이 한 줄 어긋난다.
        var map = MapManager.Instance;
        Vector3 w = map != null ? map.TileToWorld(tile)
                                : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);

        return TerrainCeiling.ClearanceAt(w.x, w.z) < PawnMover.BodyHeight;
    }

    public static bool IsRock(Vector3 world)
    {
        var map = MapManager.Instance;
        return IsRock(map != null ? map.WorldToTile(world)
                                  : new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z)));
    }

    public static bool IsMarked(Vector2Int tile) => _marked.Contains(tile);

    // 바위 윗면 높이(m) — 그리는 쪽과 커서가 쓴다.
    public static float TopOf(Vector2Int tile)
    {
        var map = MapManager.Instance;
        return (map != null ? map.SurfaceHeight(tile) : 0f) + Height;
    }

    // ── 바꾸기 ──────────────────────────────────────────────────────────────

    // 캘 것으로 찍는다 / 무른다. **바위가 아닌 칸은 안 찍는다** — 찍어 놓고 아무 일도 안
    // 일어나면 지정이 먹었는지를 알 수 없다.
    public static bool Mark(Vector2Int tile, bool on)
    {
        if (on && !IsRock(tile)) return false;
        bool changed = on ? _marked.Add(tile) : _marked.Remove(tile);
        if (changed) OnChanged?.Invoke(tile);
        return changed;
    }

    // 실제로 판다. **찍힌 것도 같이 지운다** — 판 자리에 지정이 남으면 다음 일감이 또 난다.
    public static bool Mine(Vector2Int tile)
    {
        if (!IsRock(tile)) return false;

        _mined.Add(tile);
        _marked.Remove(tile);

        // **길찾기에 알린다.** 안 알리면 격자가 옛 상태로 남아 **판 자리를 폰이 계속 벽으로
        // 본다.** 점유는 안 건드리고 소식만 보낸다 — 바위는 `_occupied`가 아니라 여기가
        // 들고 있으므로, `SetTile`로 알리면 **판 자리를 통행 가능으로 덮어써** 원래 막혀
        // 있던 건물 점유까지 풀린다.
        var map = MapManager.Instance;
        if (map != null) map.SetTile(tile, map.GetTile(tile));

        OnChanged?.Invoke(tile);
        return true;
    }

    // **시작 자리를 열어 둔다**(2026-08-29).
    //
    // 세계가 통째로 바위라 **폰이 바위 속에서 시작하면 아무것도 못 한다** — 걸을 수도, 핵을
    // 놓을 수도, 캘 자리를 고를 수도 없다(캐려면 먼저 그 칸에 커서가 닿아야 하는데 사방이
    // 벽이면 어디를 눌러도 같은 벽이다). **저절로 난 굴에 떨어지기를 바랄 수는 없다.**
    //
    // **둥글게 연다** — 네모로 열면 시작 자리가 *누가 판 방*처럼 보인다. 가장자리를 조금
    // 흔들어 자연스럽게 둔다(완전한 원도 사람이 판 것처럼 보인다).
    public static void OpenAround(Vector3 center, float radius)
    {
        if (!Enabled) return;

        var map = MapManager.Instance;
        var c   = map != null ? map.WorldToTile(center)
                              : new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));

        int r = Mathf.CeilToInt(radius);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            // 가장자리를 흔든다 — 좌표 해시라 다시 열어도 같은 모양이다.
            float wobble = Mathf.PerlinNoise((c.x + dx) * 0.18f, (c.y + dy) * 0.18f) * 2.4f - 1.2f;
            if (d > radius + wobble) continue;

            var t = new Vector2Int(c.x + dx, c.y + dy);
            if (!IsRock(t)) continue;

            _mined.Add(t);
            _marked.Remove(t);
            OnChanged?.Invoke(t);
        }

        var m = MapManager.Instance;
        if (m != null)
        {
            // 길찾기에 한 번만 알린다 — 칸마다 부르면 격자를 반경 제곱만큼 다시 굽는다.
            var t = m.WorldToTile(center);
            m.SetTile(t, m.GetTile(t));
        }
    }

    // 판을 새로 열 때. **`_marked`도 비운다** — 안 비우면 지난 판의 지정이 남는다.
    public static void Clear()
    {
        _mined.Clear();
        _marked.Clear();
        OnChanged?.Invoke(Vector2Int.zero);
    }

    // ── 광맥 ────────────────────────────────────────────────────────────────
    //
    // **좌표 해시로 결정적으로 심는다** — 나무·바위를 뿌리는 것과 같은 방식이라 저장할 것이
    // 없고, 캤다가 다시 와도(그럴 일은 없지만) 같은 자리에 같은 것이 있다.
    //
    // **무엇이 나오는지는 바이옴이 정한다** — `BiomeContentData.ResourceNodes`가 이미 그
    // 목록이다(*"이 바이옴에서 등장하는 자원 노드 풀"*). 여기서 새 표를 만들면 저작이 두 벌이 된다.
    //
    // **덩어리로 심는다.** 칸마다 따로 굴리면 광맥이 소금처럼 흩뿌려져 *"광맥을 따라 판다"*가
    // 성립하지 않는다 — 큰 주기 노이즈로 덩어리를 만들고 그 안에서만 굴린다.
    public const float VeinPeriod = 22f;    // 덩어리 크기(m)

    [Range(0f, 1f)] public static float VeinDensity = 0.16f;

    // **광맥이 없는 맨바위를 캐면 무엇이 나오나.** 씬이 든다(`RockRenderer`가 밀어 넣는다).
    //
    // 전에는 `ResourceGroup.Stone` 묶음의 **첫 번째**를 집었는데, 그 묶음에 **뼈**가 같이
    // 들어 있어서 **벽을 파면 뼈가 나왔다.** 묶음은 *"창고 필터와 화면 분류"*의 축이지
    // *"이 자원이 어디서 나오나"*의 축이 아니다 — 거기서 골라 쓴 것이 잘못이었다.
    //
    // **`ResourceNodeData`를 그대로 쓴다** — 광맥이 이미 그 타입이라 파는 쪽이 **한 가지
    // 모양만** 다루면 된다(나오는 것 · 개수 · 얼마나 오래 걸리나가 다 그 안에 있다).
    // `5_Data/Resource/Stone.asset`이 그 답을 이미 들고 있었다(돌 2~4개 · 게이지 60).
    public static ResourceNodeData PlainRock;

    public static ResourceNodeData VeinAt(Vector2Int tile)
    {
        if (!Enabled) return null;

        // ① 덩어리 안인가
        float n = Mathf.PerlinNoise((tile.x + 0.5f) / VeinPeriod + 91.7f,
                                    (tile.y + 0.5f) / VeinPeriod + 43.1f);
        if (n < 1f - VeinDensity) return null;

        // ② 이 바이옴의 풀에서 하나 — **`string.GetHashCode`를 안 쓴다**(실행마다 달라진다).
        var map = MapManager.Instance;
        Vector3 w = map != null ? map.TileToWorld(tile)
                                : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);

        var content = BiomeContent.Get(WorldMap.BiomeAtWorld(new Vector2(w.x, w.z)));
        var pool    = content != null ? content.ResourceNodes : null;
        if (pool == null || pool.Count == 0) return null;

        int h = tile.x * 73856093 ^ tile.y * 19349663;
        return pool[Mathf.Abs(h) % pool.Count];
    }
}

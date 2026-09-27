using System.Collections.Generic;
using UnityEngine;

// **절벽(낭떠러지)의 답은 여기 하나다**(2026-09-26, 사용자 결정 — 세부는 `docs/world.md` "절벽").
//
// 사용자가 그린 자리에만 서고(`ChasmData`), **밑이 안 보이는 깊은 골짜기**다. 규칙은 넷이고 전부 여기서 나온다:
//
//   · **못 지나간다** — `MapManager.HasHeadroom`이 `Blocks`를 묻는다(바위와 같은 자리라 길찾기·건설·일감이
//     통째로 따라온다). **다리가 깔린 칸은 지나간다**(`TryDeck`).
//   · **못 고친다** — 땅 고르기는 절벽 덩어리를 거절하고 **가장자리 정점을 제자리에 둔다**(`IsRimVertex`).
//     움직이면 절벽 가장자리가 비탈로 무너진다. **채굴도 없다** — 절벽 칸은 바위가 아니다(`RockMap.IsRock`).
//   · **못 파 내려간다** — 땅 고르기의 최저 높이는 `MinLevelY`(바닥 + 여유)다. 파서 절벽 밑과 이어지면 안 된다.
//   · **밑이 안 보인다** — 지형에 구멍을 뚫고(`ApplyHoles`) 벽과 안개는 `ChasmRenderer`가 그린다.
//
// ── 왜 지형에 구멍을 뚫나 ─────────────────────────────────────────────────
//
// 유니티 `Terrain`은 2m 정점을 비스듬히 이어 그리므로 **높이 차로는 수직 면이 안 나온다**(계단 바닥이
// 2m 폭 비탈이 되는 것과 같은 사실). 구멍은 **정점 사이 한 칸(=2m 덩어리)** 단위로 땅을 통째로 지워서
// 비탈이 생길 땅 자체가 없어진다. 가장자리 정점은 바깥 땅의 것이라 평평하게 남는다.
public static class Chasm
{
    static int S => TerrainGrid.VertexSpacing;

    // ── 데이터 ──────────────────────────────────────────────────────────────

    const string ResourcePath = "World/Chasms";

    static ChasmData _data;
    static bool      _loaded;
    static readonly HashSet<Vector2Int> _blocks = new();

    // **바뀐 횟수** — 그리는 쪽이 *다시 지을까*를 이 번호로 가른다.
    public static int Version { get; private set; }

    public static ChasmData Data { get { Ensure(); return _data; } }

    static void Ensure()
    {
        if (_loaded) return;
        _loaded = true;
        _data = Resources.Load<ChasmData>(ResourcePath);
        _blocks.Clear();
        if (_data != null) foreach (var b in _data.Blocks) _blocks.Add(b);
    }

    // **저작 도구가 칠한 뒤 부른다** — 판 안에서도 부를 수 있지만 절벽은 저작물이라 보통 에디터에서다.
    public static void Reload()
    {
        _loaded = false;
        Ensure();
        Version++;
    }

    public static bool Any { get { Ensure(); return _blocks.Count > 0; } }

    public static IEnumerable<Vector2Int> AllBlocks { get { Ensure(); return _blocks; } }

    public static float BottomY    => Data != null ? _data.BottomY : -24f;
    public static float MinLevelY  => BottomY + (Data != null ? _data.LevelMargin : 2f);
    public static float FogStart   => Data != null ? _data.FogStartDepth : 1.5f;

    // ── 판정 ────────────────────────────────────────────────────────────────

    public static bool IsChasmBlock(Vector2Int block) { Ensure(); return _blocks.Count > 0 && _blocks.Contains(block); }

    // **뜨거운 자리다**(`RockMap.IsRock`이 부른다) — 절벽이 없는 판에서는 좌표 계산도 안 한다.
    public static bool IsChasm(Vector2Int tile)
    {
        if (!_loaded) Ensure();
        if (_blocks.Count == 0) return false;
        return _blocks.Contains(new Vector2Int(FloorDiv(tile.x), FloorDiv(tile.y)));
    }

    static int FloorDiv(int v) => v >= 0 ? v / TerrainGrid.VertexSpacing : -((-v + TerrainGrid.VertexSpacing - 1) / TerrainGrid.VertexSpacing);

    // **못 지나가나** — 절벽 칸이고 다리가 없다.
    public static bool Blocks(Vector2Int tile) => IsChasm(tile) && !_decks.ContainsKey(tile);

    // **절벽 가장자리 정점인가** — 둘레 네 덩어리 중 하나라도 절벽이면 그 정점은 못 움직인다.
    public static bool IsRimVertex(Vector2Int v)
    {
        Ensure();
        if (_blocks.Count == 0) return false;
        for (int dz = -1; dz <= 0; dz++)
        for (int dx = -1; dx <= 0; dx++)
            if (_blocks.Contains(new Vector2Int(v.x + dx, v.y + dz))) return true;
        return false;
    }

    // ── 다리 윗면 ───────────────────────────────────────────────────────────
    //
    // **기단(`MapManager._floored`)에 넣지 않는다** — 넣으면 `FloorRenderer`가 슬래브를 그린다. 다리 윗면은
    // 그림이 아니라 판정이라 여기 따로 든다. 높이를 답하는 문(`MapManager.SurfaceHeight`·`ClimbLimitAt`)이
    // 이것을 먼저 읽는다 — 그래서 폰이 다리 위를 다리 높이로 걷고, 오르는 턱은 기단과 같은 `StepUp`이다.
    static readonly Dictionary<Vector2Int, float> _decks = new();

    public static event System.Action<Vector2Int> OnDeckChanged;

    public static bool TryDeck(Vector2Int tile, out float y) => _decks.TryGetValue(tile, out y);

    public static bool HasDeck(Vector2Int tile) => _decks.ContainsKey(tile);

    public static void RegisterDeck(Vector2Int tile, float y)
    {
        _decks[tile] = y;
        OnDeckChanged?.Invoke(tile);
    }

    public static void UnregisterDeck(Vector2Int tile)
    {
        if (_decks.Remove(tile)) OnDeckChanged?.Invoke(tile);
    }

    // **판을 새로 열 때 비운다** — static이라 플레이를 나갔다 들어와도 살아남는다(`TerrainStreamer.Awake`).
    public static void ClearRuntime()
    {
        _decks.Clear();
        _loaded = false;
    }

    // ── 지형 구멍 ───────────────────────────────────────────────────────────
    //
    // 구멍 해상도는 `heightmapResolution - 1` = 청크당 32칸 = **2m 덩어리와 정확히 같다.** 배열은 `[z, x]`이고
    // **참이 땅이다**(거짓이 구멍).
    //
    // **청크 그릇을 돌려 쓰므로 구멍도 지워야 한다**(`TerrainStreamer`의 풀) — 절벽이 있던 청크의 그릇이
    // 절벽 없는 자리로 옮겨 가면 옛 구멍이 그대로 남는다. 그래서 구멍을 뚫은 적이 있는 그릇은 기억해 둔다.
    static readonly HashSet<TerrainData> _holed = new();
    static bool[,] _holes;

    public static void ApplyHoles(TerrainData data, Vector2Int chunk)
    {
        if (data == null) return;

        int res = data.holesResolution;   // 32
        if (res <= 0) return;

        bool any = false;
        if (Any)
        {
            var origin = TerrainGrid.OriginOf(chunk);
            int bx0 = Mathf.FloorToInt(origin.x / S), bz0 = Mathf.FloorToInt(origin.z / S);
            for (int z = 0; z < res && !any; z++)
            for (int x = 0; x < res && !any; x++)
                if (_blocks.Contains(new Vector2Int(bx0 + x, bz0 + z))) any = true;

            if (any)
            {
                if (_holes == null || _holes.GetLength(0) != res) _holes = new bool[res, res];
                for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    _holes[z, x] = !_blocks.Contains(new Vector2Int(bx0 + x, bz0 + z));
                data.SetHoles(0, 0, _holes);
                _holed.Add(data);
                return;
            }
        }

        // 절벽이 없는 청크 — **예전에 뚫었던 그릇만** 메운다(안 뚫은 그릇에 구멍 텍스처를 새로 만들 이유가 없다).
        if (_holed.Remove(data))
        {
            if (_holes == null || _holes.GetLength(0) != res) _holes = new bool[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                _holes[z, x] = true;
            data.SetHoles(0, 0, _holes);
        }
    }

    // **지형 재질이 구멍을 자르게 한다** — 우리 지형 셰이더는 `_ALPHATEST_ON`일 때만 `ClipHoles`를 부른다.
    // 재질은 청크가 한 벌을 나눠 쓰므로 한 번 켜면 전부에 걸리고, 구멍 없는 청크는 흰 텍스처라 아무것도 안 잘린다.
    public static void PrepareMaterial(Material m)
    {
        if (m == null || !Any) return;
        if (!m.IsKeywordEnabled("_ALPHATEST_ON")) m.EnableKeyword("_ALPHATEST_ON");
    }
}

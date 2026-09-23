using System.Collections.Generic;
using UnityEngine;

// **땅을 목표 높이로 고른다 — 깎거나 메운다**(2026-09-24, 사용자 결정).
//
// 바닥에 계단이 생기면서 필요해졌다: 단이 1m라 통로 한복판에 층이 지고, 그러면 *"여기를
// 평평하게"*가 실제 결정이 된다.
//
// ── 기단과 무엇이 다른가 ───────────────────────────────────────────────────
//
// 둘 다 *"평평한 자리를 만든다"*에 답하지만 **가르는 축은 물건이냐 땅이냐**다:
//
//   · **기단**(`BuildingData.LevelFloor`) — 슬래브를 **얹는다**. 자재를 먹고, 철거하면
//     환불되고, 바닥 재질이 보인다. `MapManager._floored`가 든다.
//   · **땅 고르기**(여기) — 흙을 **덜거나 붓는다**. 지형 높이 자체가 바뀌어 되돌릴 근거가
//     없고, 화면에는 그냥 땅이다.
//
// **코드에서 답이 두 벌이 되지 않는다** — `MapManager.SurfaceHeight`가 *"바닥이 깔려 있으면
// 그 윗면, 아니면 지형"*을 이미 한 곳에서 답하고, 여기서 바꾸는 것은 그 뒷항이다.
//
// ── 어기면 조용히 깨지는 것 ────────────────────────────────────────────────
//
// - **높이는 2m 정점이고 지정은 1m 칸이다.** 그래서 **한 칸만 고르는 것은 애초에 안 된다** —
//   칸 하나를 덮는 정점이 최대 3×3이라 **주변 6m가 같이 움직인다**(계단이 수직이 안 되는
//   것과 같은 사실이다). 그걸 숨기지 않는다: 이미 목표에 닿은 칸은 표시가 저절로 풀리므로
//   (`IsSettled`) **구역을 찍으면 실제 일감은 훨씬 적게 난다.**
// - **다시 굽는 자리가 셋이다** — 지형 메시(`TerrainStreamer.Refill`) · 바위 벽 밑면
//   (`RockMap.SurfaceOf`가 바닥을 따르므로) · 길찾기(`SlopeRise`로 통행이 갈린다).
//   하나라도 빠지면 *"평평해 보이는데 폰이 안 지나간다"* 또는 *"벽 밑이 뚫려 보인다"*가 된다.
// - **못 고르는 자리를 먼저 거른다** — 바위(거기는 채굴이 답한다) · 건물·기단이 있는 칸
//   (깎으면 공중에 뜬다) · 세계 테두리. 안 거르면 **집이 떠 있는 것**을 되돌릴 방법이 없다.
public static class GroundLevel
{
    // ── 찍힌 칸과 목표 높이 ─────────────────────────────────────────────────
    //
    // **칸마다 목표를 든다** — 구역 하나가 한 목표를 갖지만 구역이 여럿이면 목표도 여럿이다.
    // 한 값으로 두면 두 번째 구역을 찍는 순간 첫 구역의 목표가 바뀐다.
    static readonly Dictionary<Vector2Int, float> _target = new();

    // 바뀐 칸. `RockMap.OnChanged`와 같은 규약 — 듣는 쪽이 제 것만 다시 굽는다.
    public static event System.Action<Vector2Int> OnChanged;

    // **표시가 바뀐 횟수** — 테두리를 그리는 쪽이 *다시 지을까*를 이 번호로 가른다
    // (`FarmZones.Version`과 같은 자리: 매 프레임 정점을 쌓으면 찍힌 칸 수만큼이 붙는다).
    public static int Version { get; private set; }

    static void Bump(Vector2Int tile)
    {
        Version++;
        OnChanged?.Invoke(tile);
    }

    public static IEnumerable<Vector2Int> MarkedTiles => _target.Keys;
    public static int MarkedCount => _target.Count;

    public static bool IsMarked(Vector2Int tile) => _target.ContainsKey(tile);

    public static bool TryTarget(Vector2Int tile, out float y) => _target.TryGetValue(tile, out y);

    // 찍는다 / 무른다. **바뀌었으면 참** — 부르는 쪽이 미리보기 되돌리기를 그 답으로 가른다
    // (`RockMap.Mark`와 같은 규약: 참은 *"내가 이번에 바꿨다"*는 뜻이라, 이미 찍혀 있던 칸을
    // 드래그가 스쳐도 남의 지시를 안 무른다).
    public static bool Mark(Vector2Int tile, float targetY)
    {
        if (!CanLevel(tile)) return false;

        if (_target.TryGetValue(tile, out float cur) && Mathf.Abs(cur - targetY) < 0.001f) return false;

        _target[tile] = targetY;
        Bump(tile);
        return true;
    }

    public static bool Unmark(Vector2Int tile)
    {
        if (!_target.Remove(tile)) return false;
        Bump(tile);
        return true;
    }

    public static void Clear()
    {
        if (_target.Count == 0) return;
        _tmp.Clear();
        foreach (var t in _target.Keys) _tmp.Add(t);
        _target.Clear();
        foreach (var t in _tmp) Bump(t);
    }

    static readonly List<Vector2Int> _tmp = new();

    // ── 고를 수 있는 자리인가 ───────────────────────────────────────────────
    //
    // **`MapManager`가 없으면 참** — 격자 없는 씬에서 통째로 막히지 않게(다른 폴백과 같은 규칙).
    public static bool CanLevel(Vector2Int tile)
    {
        if (!TerrainGrid.TileInWorld(tile)) return false;

        // **바위는 채굴이 답한다** — 여기서도 받으면 같은 칸에 두 일감이 붙고, 무엇이 먼저
        // 도는지가 화면에 안 적힌다.
        if (RockMap.Enabled && RockMap.IsRock(tile)) return false;

        var map = MapManager.Instance;
        if (map == null) return true;

        // **건물·기단이 있으면 안 된다** — 땅만 내려가고 물건은 그 자리에 남아 **공중에 뜬다.**
        // 되돌릴 방법이 없으니(고르기는 환불이 없다) 아예 안 받는다.
        if (map.IsOccupied(tile)) return false;
        if (map.IsFloored(tile))  return false;

        return true;
    }

    // ── 칸을 덮는 정점 ──────────────────────────────────────────────────────
    //
    // 칸 `[x, x+1]`의 높이는 `TerrainHeight.At`의 이중선형이 내므로, 그 구간에 걸치는 정점
    // **전부**가 목표여야 칸이 평평해진다. `fx = x / 간격`이고 보간이 `floor(fx)`와 그 다음을
    // 쓰므로 범위는 `floor(x/간격) ~ floor((x+1)/간격) + 1`이다.
    //
    // **`+1`을 빠뜨리면 칸의 한쪽 끝이 안 따라와** 고른 자리마다 마지막 줄이 기울어 남는다.
    public static void VertexRange(Vector2Int tile, out int gxLo, out int gxHi, out int gzLo, out int gzHi)
    {
        float s = TerrainGrid.VertexSpacing;
        gxLo = Mathf.FloorToInt(tile.x / s);
        gxHi = Mathf.FloorToInt((tile.x + 1) / s) + 1;
        gzLo = Mathf.FloorToInt(tile.y / s);
        gzHi = Mathf.FloorToInt((tile.y + 1) / s) + 1;
    }

    // 목표까지 남은 높이차(m, 부호 없음). **가장 먼 정점으로 잰다** — 평균으로 재면 반쯤
    // 고른 칸이 *다 됐다*고 답한다.
    public static float Remaining(Vector2Int tile, float targetY)
    {
        VertexRange(tile, out int gxLo, out int gxHi, out int gzLo, out int gzHi);

        float worst = 0f;
        for (int gz = gzLo; gz <= gzHi; gz++)
        for (int gx = gxLo; gx <= gxHi; gx++)
            worst = Mathf.Max(worst, Mathf.Abs(TerrainHeight.VertexHeightGlobal(gx, gz) - targetY));

        return worst;
    }

    // **이미 맞았나.** 1cm 안이면 맞은 것으로 본다 — 부동소수라 딱 같기를 바라면 영영 안 끝난다.
    public static bool IsSettled(Vector2Int tile, float targetY) => Remaining(tile, targetY) < 0.01f;

    // 깎는 쪽인가(양수) 메우는 쪽인가(음수). **한 칸 안에서 둘이 섞일 수 있다** — 가장 먼
    // 정점의 방향을 답한다(그쪽이 값을 정하는 자리다).
    public static float SignedRemaining(Vector2Int tile, float targetY)
    {
        VertexRange(tile, out int gxLo, out int gxHi, out int gzLo, out int gzHi);

        float worst = 0f;
        for (int gz = gzLo; gz <= gzHi; gz++)
        for (int gx = gxLo; gx <= gxHi; gx++)
        {
            float d = TerrainHeight.VertexHeightGlobal(gx, gz) - targetY;   // + = 깎아야 한다
            if (Mathf.Abs(d) > Mathf.Abs(worst)) worst = d;
        }
        return worst;
    }

    // ── 고른다 ──────────────────────────────────────────────────────────────
    //
    // 칸을 덮는 정점을 전부 목표로 못 박고 **다시 굽는 자리 셋에 알린다.** 실제로 바뀐 것이
    // 없으면 아무 일도 안 한다(같은 칸에 두 폰이 붙었을 때).
    public static bool Apply(Vector2Int tile, float targetY)
    {
        VertexRange(tile, out int gxLo, out int gxHi, out int gzLo, out int gzHi);

        bool changed = false;
        for (int gz = gzLo; gz <= gzHi; gz++)
        for (int gx = gxLo; gx <= gxHi; gx++)
            changed |= TerrainHeight.SetLeveled(gx, gz, targetY);

        _target.Remove(tile);

        if (!changed)
        {
            Bump(tile);   // 표시만 지운다
            return false;
        }

        Refresh(gxLo, gxHi, gzLo, gzHi, tile);
        return true;
    }

    // **다시 굽는 자리는 셋이고 여기 하나로 모은다** — 부르는 쪽마다 적으면 하나가 빠지고,
    // 그 하나가 무엇이냐에 따라 증상이 전부 다르게 보인다.
    static void Refresh(int gxLo, int gxHi, int gzLo, int gzHi, Vector2Int tile)
    {
        // ① 지형 메시 — **정점이 걸친 청크 전부**다. 청크 경계의 정점은 두 청크가 각자
        //    그리므로 한쪽만 고치면 **이음매에 벽이 선다.**
        _chunks.Clear();
        for (int gz = gzLo; gz <= gzHi; gz++)
        for (int gx = gxLo; gx <= gxHi; gx++)
        {
            // 정점 → 청크. `TerrainHeight.OwnedPerChunk`가 소유권의 단일 출처다.
            int cx = gx / TerrainHeight.OwnedPerChunk;
            int cz = gz / TerrainHeight.OwnedPerChunk;
            _chunks.Add(new Vector2Int(cx, cz));

            // 가장자리 정점은 이웃 청크의 0번이기도 하다 — 그쪽도 다시 굽는다.
            if (gx % TerrainHeight.OwnedPerChunk == 0 && cx > 0) _chunks.Add(new Vector2Int(cx - 1, cz));
            if (gz % TerrainHeight.OwnedPerChunk == 0 && cz > 0) _chunks.Add(new Vector2Int(cx, cz - 1));
        }
        foreach (var c in _chunks) TerrainStreamer.Refill(c);

        // ② 바위 벽 — 밑면이 바닥을 따라 서므로(`RockRenderer.Side`가 이웃 바닥까지 내려
        //    세운다) 고른 칸 둘레의 벽을 다시 굽는다. **안 굽으면 벽 밑이 뚫려 보인다.**
        //    **직접 안 부른다** — 듣는 쪽이 늘 수 있고, 그때마다 여기가 늘면 안 된다.
        TerrainHeight.NotifyHeightChanged(tile);

        // ③ 길찾기 — 높이가 바뀌면 오를 수 있는지가 바뀐다(`PathGrid`의 `SlopeRise`).
        //    **격자가 스스로 알 방법은 없다** — `RockMap.Mine`이 남긴 그 규칙이다.
        //    반경은 정점이 걸치는 6m보다 넉넉하게(경계 칸의 간선까지 다시 잰다).
        var map = MapManager.Instance;
        if (map != null) HierarchicalPathfinder.Instance?.RebakeArea(map.TileToWorld(tile), 8f);

        Bump(tile);
    }

    static readonly HashSet<Vector2Int> _chunks = new();

    // ── 값 ──────────────────────────────────────────────────────────────────
    //
    // **깎으면 돌이 나오고 메우면 돌을 먹는다.** 대칭이라 *"높은 데를 깎아 낮은 데를 메운다"*가
    // 저절로 성립한다(깎아서 모은 돌로 메운다).
    //
    // 나오는 것은 **`RockMap.PlainRock`이 답한다** — 벽을 팠을 때 나오는 그 돌이다. 여기서
    // 아이템을 따로 적으면 *"벽에서 나온 돌"*과 *"땅에서 나온 돌"*이 다른 물건이 될 수 있다.
    public static ItemData Spoil => RockMap.PlainRock != null ? RockMap.PlainRock.DropItem : null;

    // 1m를 고르면 돌 몇 개인가. 칸이 1m²이므로 이것이 곧 m³당 개수다.
    const float StonePerMeter = 2f;

    public static int SpoilFor(float meters) =>
        Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(meters) * StonePerMeter));

    // 1m를 고르는 데 드는 작업량. **채굴 눈금에 맞춘다** — 벽 한 칸(`GaugeRequired`)이
    // 대개 60이고, 땅을 1m 깎는 것이 그보다 쉬워야 *파는 것보다 고르는 것이 싸다*가 된다.
    public const float WorkPerMeter = 25f;
}

using System.Collections.Generic;
using UnityEngine;

// **땅을 한 단씩 올리고 내린다**(2026-09-24, 사용자 결정 · 09-25에 올리기/내리기로 · 같은 날 2m 덩어리로).
//
// 바닥에 계단이 생기면서 필요해졌다: 단이 1m라 통로 한복판에 층이 지고, 그러면 *"여기를
// 올리자 · 내리자"*가 실제 결정이 된다.
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
// ── 찍는 단위는 1m 칸이 아니라 2m 덩어리다 ─────────────────────────────────
//
// **땅 높이는 2m 간격 정점으로만 있다**(`TerrainGrid.VertexSpacing`) — 그 사이는 이중선형으로
// 잇는다. 처음(09-24)에는 1m 칸을 찍고 그 칸에 걸친 정점을 전부 움직였는데, 칸 하나에 걸친
// 정점이 한 줄에 2~3개라 **한 칸을 찍으면 4m 안팎이 움직이고 둘레 2m가 비탈이 되어** 테두리
// 선과 실제로 움직인 땅이 안 맞았다(사용자: *"판정이 좀 잘 안맞는데"*). 이건 값으로 못 고친다
// — 계단에 수직 턱이 안 서는 것과 같은 사실이다.
//
// 그래서 **찍는 단위는 정점 네 개가 둘러싼 2m 덩어리(`Block`)이고, 일감은 그 귀퉁이 정점**이다.
// 덩어리를 찍으면 그 네 귀퉁이가 움직이고, **선 안은 정확히 새 높이**가 되며 **둘레 2m만 비탈**이다.
// 그 비탈은 없앨 수 없지만 **선과 결과는 맞는다.** 덩어리 둘이 맞닿으면 귀퉁이를 나눠 쓰므로
// 넓게 찍은 구역은 통째로 한 판이 된다.
//
// **찍은 덩어리와 찍은 정점을 따로 든다** — 건물에 닿는 귀퉁이는 **못 움직이는 정점**(`CanMoveVertex`)
// 이라 덩어리는 찍혀도 그 귀퉁이는 안 찍힌다. 정점만 들면 그 덩어리가 *찍혔나*를 답할 수 없어 선이
// 안 그려진다. 덩어리는 **남은 귀퉁이 일감이 하나도 없으면 저절로 풀린다**(`Unmark`).
//
// ── 어기면 조용히 깨지는 것 ────────────────────────────────────────────────
//
// - **정점이 움직이면 둘레 네 칸 × 네 칸이 같이 움직인다**(정점에서 ±2m). 그 위의 것은 **따라 옮긴다**
//   (`CollectRiders` — 야생 노드·채집물·바닥 물건은 제 자리의 땅만큼, **건물·청사진은 발자국 아래 땅의
//   가장 높은 곳만큼**: 묻히지 않게). 처음엔 건물 둘레를 통째로 막았고(09-25 오전) 다음엔 건물 칸만
//   막았는데 사용자가 *"건물 점유된 곳도 바꿀 수 있게"*를 원했다 — 막을 이유가 *물건이 뜬다*뿐이었으니
//   **물건을 옮기면 막을 이유가 사라진다.**
// - **기단(바닥 슬래브)은 여전히 막는다** — 기단은 **윗면 높이를 데이터로 든다**(`MapManager._floored`)라
//   땅만 움직이면 흙이 슬래브를 뚫고 올라오거나 슬래브가 뜬다. 옮기려면 그 데이터를 다시 적어야 하고
//   그건 기단의 규칙(이웃 기단 윗면을 물려받는다)과 싸운다.
// - **다시 굽는 자리가 셋이다** — 지형 메시(`TerrainStreamer.Refill`) · 바위 벽 밑면
//   (`RockMap.SurfaceOf`가 바닥을 따르므로) · 길찾기(`SlopeRise`로 통행이 갈린다).
//   하나라도 빠지면 *"평평해 보이는데 폰이 안 지나간다"* 또는 *"벽 밑이 뚫려 보인다"*가 된다.
public static class GroundLevel
{
    static int S => TerrainGrid.VertexSpacing;

    // ── 덩어리와 정점 ───────────────────────────────────────────────────────
    //
    // 덩어리 `(bx, bz)`는 월드 `[bx·2, bx·2+2]`이고 귀퉁이는 정점 `(bx, bz)`~`(bx+1, bz+1)`이다.
    public static Vector2Int BlockOf(Vector2Int tile) =>
        new(Mathf.FloorToInt(tile.x / (float)S), Mathf.FloorToInt(tile.y / (float)S));

    public static Vector2Int BlockOfWorld(Vector3 world) =>
        new(Mathf.FloorToInt(world.x / S), Mathf.FloorToInt(world.z / S));

    static readonly Vector2Int[] Corner = { new(0, 0), new(1, 0), new(0, 1), new(1, 1) };

    public static IEnumerable<Vector2Int> CornersOf(Vector2Int block)
    {
        foreach (var c in Corner) yield return block + c;
    }

    // 정점의 월드 자리(높이 제외). 정점은 격자 위상을 안 탄다 — 지형이 세계 원점에 박혀 있다.
    public static Vector3 VertexWorld(Vector2Int v) => new(v.x * S, 0f, v.y * S);

    public static float Height(Vector2Int v) => TerrainHeight.VertexHeightGlobal(v.x, v.y);

    // ── 찍힌 덩어리 · 찍힌 정점과 목표 높이 ─────────────────────────────────
    static readonly HashSet<Vector2Int>             _blocks = new();
    static readonly Dictionary<Vector2Int, float> _target = new();

    // 바뀐 정점. 듣는 쪽이 제 것만 다시 굽는다.
    public static event System.Action<Vector2Int> OnChanged;

    // **표시가 바뀐 횟수** — 테두리를 그리는 쪽이 *다시 지을까*를 이 번호로 가른다.
    public static int Version { get; private set; }

    static void Bump(Vector2Int v)
    {
        Version++;
        OnChanged?.Invoke(v);
    }

    public static IEnumerable<Vector2Int> MarkedVertices => _target.Keys;
    public static IEnumerable<Vector2Int> MarkedBlocks   => _blocks;
    public static int MarkedCount => _target.Count;

    public static bool IsMarked(Vector2Int v) => _target.ContainsKey(v);

    public static bool TryTarget(Vector2Int v, out float y) => _target.TryGetValue(v, out y);

    public static bool IsBlockMarked(Vector2Int block) => _blocks.Contains(block);

    // **그 덩어리에 아직 할 일이 있나 — 목표에 안 닿은 귀퉁이가 하나라도 있나.** 선을 그릴지·덩어리를
    // 남길지를 이것 하나가 정한다(2026-09-25). *"찍혔나"*로 가르던 동안에는 이미 그 높이인 덩어리가
    // 목록에 들어가 **움직일 귀퉁이가 없으니 풀리는 계기도 영영 안 와서** 선이 남았다(사용자: *"변동사항이
    // 없는 곳은 그 선이 안남으면 좋겠어"* — 평탄화의 시작점과 이미 같은 높이인 곳이 그랬다).
    public static bool BlockNeedsWork(Vector2Int block)
    {
        foreach (var c in Corner)
            if (_target.TryGetValue(block + c, out float t) && !IsSettled(block + c, t)) return true;
        return false;
    }

    // 덩어리를 찍는다 / 무른다. **바뀌었으면 참.** 귀퉁이 정점은 부르는 쪽이 따로 찍는다
    // (목표를 정하는 것은 지정기다).
    public static bool MarkBlock(Vector2Int block)
    {
        if (!CanLevelBlock(block) || !BlockNeedsWork(block) || !_blocks.Add(block)) return false;
        Version++;
        return true;
    }

    public static bool UnmarkBlock(Vector2Int block)
    {
        if (!_blocks.Remove(block)) return false;
        Version++;
        return true;
    }

    // 정점을 찍는다 / 무른다. **바뀌었으면 참**(`RockMap.Mark`와 같은 규약).
    public static bool Mark(Vector2Int v, float targetY)
    {
        if (!CanMoveVertex(v)) return false;
        targetY = ClampTarget(targetY);
        if (_target.TryGetValue(v, out float cur) && Mathf.Abs(cur - targetY) < 0.001f) return false;

        _target[v] = targetY;
        Bump(v);
        return true;
    }

    public static bool Unmark(Vector2Int v)
    {
        if (!_target.Remove(v)) return false;
        PruneAround(v);
        Bump(v);
        return true;
    }

    // 그 정점을 귀퉁이로 쓰는 덩어리 가운데 **할 일이 남지 않은 것**을 푼다 — 다 끝났거나 다
    // 물렀다(`BlockNeedsWork`). 안 풀면 끝난 자리에 선이 영영 남는다.
    static void PruneAround(Vector2Int v)
    {
        for (int dz = -1; dz <= 0; dz++)
        for (int dx = -1; dx <= 0; dx++)
        {
            var blk = new Vector2Int(v.x + dx, v.y + dz);
            if (_blocks.Contains(blk) && !BlockNeedsWork(blk)) _blocks.Remove(blk);
        }
    }

    public static void Clear()
    {
        _blocks.Clear();
        if (_target.Count == 0) return;
        _tmp.Clear();
        foreach (var v in _target.Keys) _tmp.Add(v);
        _target.Clear();
        foreach (var v in _tmp) Bump(v);
    }

    static readonly List<Vector2Int> _tmp = new();

    // ── 고를 수 있는 자리인가 ───────────────────────────────────────────────
    //
    // **덩어리는 제 칸 넷만 본다** — 건물·기단이 든 덩어리는 안 된다(그 칸을 고르는 것이 곧
    // 건물 밑을 파는 일이다). 네 칸이 **전부** 바위여도 안 된다(보이는 땅이 없다 — 채굴이 먼저다).
    // 그리고 움직일 수 있는 귀퉁이가 하나는 있어야 한다(없으면 찍어도 아무 일이 없다).
    //
    // **바위는 막지 않는다** — 벽 밑면은 바닥을 따라 다시 굽는다(`Refresh` ②).
    //
    // **거절하면 이유를 말한다**(`WhyNotBlock`) — 참/거짓만 돌려주면 *"상자 칸만 막혀야 하는데 왜
    // 저렇게 넓게 막히나"*를 풀 수가 없다(2026-09-25에 실제로 그랬다: 범인은 어두워서 안 보이던
    // **바닥의 야생 자원 노드**가 점유로 잡힌 것이었다). `MapManager.WhyNotBuildable`과 같은 규칙.
    public static bool CanLevelBlock(Vector2Int block) => WhyNotBlock(block) == null;

    public static string WhyNotBlock(Vector2Int block)
    {
        // **절벽은 못 고친다**(2026-09-26, 사용자) — 메우지도 깎지도 않는다.
        if (Chasm.IsChasmBlock(block)) return "절벽입니다";

        var map = MapManager.Instance;
        bool anyOpen = !RockMap.Enabled;

        for (int dz = 0; dz < S; dz++)
        for (int dx = 0; dx < S; dx++)
        {
            var t = new Vector2Int(block.x * S + dx, block.y * S + dz);
            if (!TerrainGrid.TileInWorld(t)) return "세계 끝입니다";
            if (map != null && map.IsFloored(t)) return "바닥(기단)이 깔린 칸입니다";
            if (!anyOpen && !RockMap.IsRock(t)) anyOpen = true;
        }
        if (!anyOpen) return "전부 벽입니다 (채굴이 먼저입니다)";

        foreach (var c in Corner) if (CanMoveVertex(block + c)) return null;
        return "네 모서리가 전부 기단이나 절벽에 닿아 있습니다";
    }

    // **막는 칸인가 — 기단뿐이다.** 건물·청사진·야생 노드는 땅을 따라 옮긴다(`CollectRiders`).
    static bool Blocks(MapManager map, Vector2Int t) => map.IsFloored(t);

    // **정점을 움직여도 되나** — 정점 하나는 둘레 ±2m(칸 4×4)의 높이를 바꾼다. 그 안에 기단이 있으면
    // **그 정점만** 제자리다(덩어리는 막지 않는다 — 근거는 머리 주석).
    //
    // **`MapManager`가 없으면 세계 범위만 본다** — 격자 없는 씬에서 통째로 막히지 않게.
    public static bool CanMoveVertex(Vector2Int v)
    {
        if (!TerrainGrid.TileInWorld(new Vector2Int(v.x * S - 1, v.y * S - 1))) return false;
        if (!TerrainGrid.TileInWorld(new Vector2Int(v.x * S,     v.y * S)))     return false;

        // **절벽 가장자리는 제자리다** — 움직이면 절벽 가장자리가 비탈로 무너진다(`Chasm.IsRimVertex`).
        if (Chasm.IsRimVertex(v)) return false;

        var map = MapManager.Instance;
        if (map == null) return true;

        int x0 = v.x * S - S, z0 = v.y * S - S;
        for (int z = z0; z < z0 + 2 * S; z++)
        for (int x = x0; x < x0 + 2 * S; x++)
        {
            if (Blocks(map, new Vector2Int(x, z))) return false;
        }
        return true;
    }

    // **내릴 수 있는 한계** — 절벽이 있는 판이면 절벽 바닥 + 여유 아래로는 못 내린다(파서 절벽 밑과
    // 이어지면 안 된다 — 사용자). 목표를 정하는 쪽(`LevelDesignator`)과 찍는 문(`Mark`) 둘 다 이걸 지난다.
    public static float ClampTarget(float targetY) => Chasm.Any ? Mathf.Max(targetY, Chasm.MinLevelY) : targetY;

    // 목표까지 남은 높이차(m, 부호 있음 — 양수면 내려야 한다).
    public static float SignedRemaining(Vector2Int v, float targetY) => Height(v) - targetY;

    // **이미 맞았나.** 1cm 안이면 맞은 것으로 본다.
    public static bool IsSettled(Vector2Int v, float targetY) => Mathf.Abs(SignedRemaining(v, targetY)) < 0.01f;

    // ── 고른다 ──────────────────────────────────────────────────────────────
    public static bool Apply(Vector2Int v, float targetY)
    {
        CollectRiders(v);
        bool changed = TerrainHeight.SetLeveled(v.x, v.y, targetY);
        if (changed) MoveRiders();
        _target.Remove(v);
        PruneAround(v);   // 마지막 할 일이었으면 덩어리 선도 거둔다(높이를 바꾼 **뒤에** 잰다)

        if (!changed)
        {
            Bump(v);   // 표시만 지운다
            return false;
        }

        Refresh(v);
        return true;
    }

    // ── 땅 위의 것이 따라온다 ───────────────────────────────────────────────
    //
    // 땅 위의 물건은 **자리를 높이로 안 들고 한 번 앉은 그대로**라, 땅만 움직이면 **공중에 뜨거나 묻힌다.**
    // 그래서 정점이 움직이는 넓이(±2m) 안의 것을 **땅이 움직인 만큼** 같이 옮긴다(새 땅에 다시 붙이지
    // 않는다 — 일부러 띄운 것까지 붙이면 모양이 바뀐다).
    //
    // **재는 자리가 둘이다** — 야생 노드·채집물·바닥 물건은 **제 자리 한 점**의 땅, 건물·청사진은
    // **발자국 아래 땅의 가장 높은 곳**(`IFootprintSite`). 건물을 한 점으로 재면 한복판은 안 움직이고
    // 가장자리만 오른 7×6 오두막에서 **흙이 바닥을 뚫고 올라온다.** 가장 높은 곳이면 묻히지는 않는다
    // (낮은 쪽 가장자리가 조금 뜨는 것은 기단이 원래 답하는 자리다).
    static readonly List<(Transform t, float ground, IFootprintSite site)> _riders = new();
    static readonly Collider[] _hits = new Collider[256];

    static void CollectRiders(Vector2Int v)
    {
        _riders.Clear();
        var c = VertexWorld(v);
        int n = Physics.OverlapBoxNonAlloc(new Vector3(c.x, 0f, c.z), new Vector3(S, 1000f, S), _hits,
                                           Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i];
            Transform      who  = null;
            IFootprintSite site = col.GetComponentInParent<IFootprintSite>();

            if (site is Component sc) who = sc.transform;
            else if (col.GetComponentInParent<ResourceNode>() is { } node) who = node.transform;
            else if (col.GetComponentInParent<Forageable>()   is { } f)    who = f.transform;
            else if (col.GetComponentInParent<DroppedItem>()  is { } d)    who = d.transform;
            if (who == null) continue;

            bool seen = false;
            foreach (var r in _riders) if (r.t == who) { seen = true; break; }
            if (!seen) _riders.Add((who, GroundUnder(who, site), site));
        }
    }

    static void MoveRiders()
    {
        foreach (var (t, before, site) in _riders)
        {
            if (t == null) continue;
            float d = GroundUnder(t, site) - before;
            if (Mathf.Abs(d) > 0.001f) t.position += new Vector3(0f, d, 0f);
        }
        _riders.Clear();
    }

    static float GroundUnder(Transform t, IFootprintSite site)
    {
        if (site == null) return TerrainHeight.At(t.position);

        var map = MapManager.Instance;
        float max = float.MinValue;
        foreach (var tile in site.Footprint)
        {
            Vector3 w = map != null ? map.TileToWorld(tile) : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            max = Mathf.Max(max, TerrainHeight.At(w));
        }
        return max == float.MinValue ? TerrainHeight.At(t.position) : max;
    }

    // **다시 굽는 자리는 셋이고 여기 하나로 모은다.**
    static void Refresh(Vector2Int v)
    {
        // ① 지형 메시 — 정점을 소유한 청크, 그리고 **가장자리 정점이면 이웃 청크도**(경계 정점은
        //    두 청크가 각자 그리므로 한쪽만 고치면 **이음매에 벽이 선다**).
        int own = TerrainHeight.OwnedPerChunk;
        int cx = v.x / own, cz = v.y / own;
        TerrainStreamer.Refill(new Vector2Int(cx, cz));
        bool ex = v.x % own == 0 && cx > 0, ez = v.y % own == 0 && cz > 0;
        if (ex)       TerrainStreamer.Refill(new Vector2Int(cx - 1, cz));
        if (ez)       TerrainStreamer.Refill(new Vector2Int(cx, cz - 1));
        if (ex && ez) TerrainStreamer.Refill(new Vector2Int(cx - 1, cz - 1));

        // ② 바위 벽 — 영향 범위의 네 모서리 칸을 알린다(청크 경계를 넘어도 빠짐없이).
        //    **직접 안 부른다** — 듣는 쪽이 늘 수 있다.
        int x0 = v.x * S - S, z0 = v.y * S - S, x1 = v.x * S + S - 1, z1 = v.y * S + S - 1;
        TerrainHeight.NotifyHeightChanged(new Vector2Int(x0, z0));
        TerrainHeight.NotifyHeightChanged(new Vector2Int(x1, z0));
        TerrainHeight.NotifyHeightChanged(new Vector2Int(x0, z1));
        TerrainHeight.NotifyHeightChanged(new Vector2Int(x1, z1));

        // ③ 길찾기 — 높이가 바뀌면 오를 수 있는지가 바뀐다. 반경은 영향 범위(±2m)보다 넉넉하게.
        HierarchicalPathfinder.Instance?.RebakeArea(VertexWorld(v), 5f);

        Bump(v);
    }

    // ── 값 ──────────────────────────────────────────────────────────────────
    //
    // **내리면 돌이 나오고 올리면 돌을 먹는다.** 대칭이라 *"높은 데를 내려 낮은 데를 올린다"*가
    // 저절로 성립한다. 나오는 것은 **`RockMap.PlainRock`이 답한다** — 벽을 팠을 때 나오는 그 돌이다.
    public static ItemData Spoil => RockMap.PlainRock != null ? RockMap.PlainRock.DropItem : null;

    // 정점 하나가 맡는 넓이(m²) — 넓게 찍으면 덩어리 하나당 정점 하나꼴이다.
    static float VertexArea => S * S;

    // 1m²를 1m 움직이면 돌 몇 개인가.
    const float StonePerCubicMeter = 2f;

    public static int SpoilFor(float meters) =>
        Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(meters) * VertexArea * StonePerCubicMeter));

    // 1m²를 1m 움직이는 작업량. **채굴 눈금에 맞춘다** — 벽 한 칸(`GaugeRequired`)이 대개 60이고,
    // 땅을 고르는 것이 그보다 싸야 *파는 것보다 고르는 것이 싸다*가 된다.
    const float WorkPerCubicMeter = 25f;

    public static float WorkFor(float meters) => Mathf.Max(1f, Mathf.Abs(meters) * VertexArea * WorkPerCubicMeter);
}

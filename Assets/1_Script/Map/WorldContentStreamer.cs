using System.Collections.Generic;
using UnityEngine;

// **세계에 물건을 세운다** — 지형 청크를 따라(2026-08-26).
//
// 둘을 든다:
//   · **뿌리는 것**(`ScatterRule`) — 나무·바위. 29km에 손으로 놓을 수 없으니 **좌표 해시로
//     결정적으로** 계산한다. 저장하는 것이 없고, 떠났다 돌아와도 같은 자리에 선다.
//   · **저작한 장소**(`WorldSite`) — 마을·주둔지. **프리팹을 손으로 짓고 좌표를 적는다.**
//
// **청크 목록은 `TerrainStreamer`가 든다** — 여기서 반경을 다시 재면 둘의 답이 어긋나
// 지형은 있는데 나무가 없는 칸이 생긴다. 소식만 듣는다.
public class WorldContentStreamer : MonoBehaviour
{
    [Header("뿌리는 것 — 나무 · 바위")]
    [SerializeField] ScatterRule[] scatter = System.Array.Empty<ScatterRule>();

    // **옛 목록 — 이행 대기.** 아무도 안 읽는다(`DungeonColony → World → 1. Migrate Sites`가
    // 청크 에셋으로 옮기고 여기를 비운다). **두 벌을 남기지 않으려고** 읽는 쪽을 아예 끊었다 —
    // 남겨 두면 *어느 쪽이 이기는가*가 또 규칙이 되고, 그 규칙은 반드시 어긋난다.
    [Header("옛 목록 — 이행 대기 (Migrate 실행 후 비워진다)")]
    [SerializeField] WorldSite[] sites = System.Array.Empty<WorldSite>();

    public WorldSite[] LegacySites => sites;   // 이행 도구만 읽는다

    // ── 저작한 지도에서 세울 것들 ───────────────────────────────────────────
    //
    // 마을·소굴은 **이미 `WorldMapData`(16×16)에 찍혀 있다.** 이어진 세계에서 그 격자는
    // *"이동하는 칸"*이 아니라 **1,024m짜리 지역**이고, 그 칸에 찍힌 내용물은 **그 지역
    // 어딘가에 실제로 선다**는 뜻이 된다.
    //
    // **좌표를 손으로 옮겨 적지 않는다** — 저작한 지도가 답이므로, 도구
    // (`5. Place Sites From World Map`)가 읽어서 `sites`를 채운다. 지도를 고치고 다시 돌리면
    // 그대로 따라온다. 손으로 적으면 지도와 세계가 **두 벌**이 되어 반드시 어긋난다.
    [Header("지도에서 세울 프리팹 (도구가 쓴다)")]
    [SerializeField] GameObject townPrefab;
    [SerializeField] GameObject lairPrefab;
    [SerializeField] GameObject resourceSitePrefab;

    public GameObject TownPrefab         => townPrefab;
    public GameObject LairPrefab         => lairPrefab;
    public GameObject ResourceSitePrefab => resourceSitePrefab;

    [Tooltip("같은 시드면 언제나 같은 숲이 선다. 지형 시드와 따로 두는 이유는 " +
             "**지형은 그대로 두고 나무만 다시 뿌리고 싶을 때**가 있어서다.")]
    [SerializeField] int scatterSeed = 20260826;

    readonly Dictionary<Vector2Int, List<GameObject>> _live = new();

    // **뿌린 것을 이 컴포넌트의 자식으로 붙이지 않는다** — `TerrainStreamer`와 같은 이유다.
    // 붙이면 이 오브젝트의 회전·스케일이 나무에 곱해져 **숲이 통째로 기울거나 커진다.**
    // 그래야 "지형 스트리머와 같은 오브젝트에 붙여도 된다"가 참이 된다.
    Transform _root;

    Transform Root
    {
        get
        {
            if (_root == null)
            {
                _root = new GameObject("WorldContent").transform;
                _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _root.localScale = Vector3.one;
            }
            return _root;
        }
    }

    // **캔 것을 기억한다.** 청크를 떠났다 돌아오면 결정적 계산이 그것을 **또** 세우므로,
    // 없어진 것을 기억해야 한다. 좌표가 아니라 **뿌린 순번(id)**으로 기억한다 — 자리는
    // 계산으로 나오지만 *"몇 번째 것"*은 안 바뀐다.
    //
    // **담는 값은 "다시 날 게임일"이다**(2026-09-06). 전에는 집합이라 *영원히 없어진 것*밖에
    // 못 적었는데, 버섯은 다시 나야 한다(`ScatterRule.RegrowDays`). 나무·바위는
    // `PositiveInfinity`가 들어가 예전과 **한 톨도 안 달라진다.**
    //
    // **자리를 안 기억해도 되는 이유**가 뿌리기가 결정적이라는 것이다 — 잊기만 하면 같은
    // 자리에 같은 것이 다시 선다. *"저장하는 것이 없다"*는 이 파일의 전제가 그대로 유지된다.
    static readonly Dictionary<long, float> _taken = new();

    static readonly List<Vector2Int> _placed = new();   // 간격 검사용 임시

    // **정적 캐시는 판이 열릴 때 비운다.**
    //
    // 이 프로젝트는 "Enter Play Mode Options"가 켜져 있다(`m_EnterPlayModeOptionsEnabled: 1`).
    // 지금은 아무 옵션도 안 골라 둬서(`0`) 도메인 리로드가 그대로 돌지만, **거기서 리로드를
    // 끄는 순간 이 값들이 지난 판의 것을 그대로 들고 시작한다** — `_placedUp`이 남아 있으면
    // 안 내리는 마을이 **두 번째 판부터 아예 안 선다**(이미 세웠다고 기억하므로). 체크박스
    // 하나에 조용히 깨지는 것을 남겨 둘 이유가 없다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _placedUp.Clear();
        _content.Clear();
        _taken.Clear();
    }

    void OnEnable()
    {
        // **조용히 안 세우는 것이 가장 나쁘다** — 옛 목록이 남아 있으면 마을이 통째로
        // 사라진 것처럼 보이는데, 원인이 "안 옮겼다"라는 것을 알 길이 없다.
        if (sites != null && sites.Length > 0)
            Debug.LogError($"[월드콘텐츠] 옛 목록에 {sites.Length}개가 남아 있다 — 아무도 안 읽는다. " +
                           "`DungeonColony → World → 1. Migrate Sites To Chunk Assets`를 실행할 것.");

        TerrainStreamer.ChunkLoaded   += Load;
        TerrainStreamer.ChunkUnloaded += Unload;

        // 이미 서 있는 청크를 따라잡는다 — 이 컴포넌트가 나중에 켜졌을 수 있다.
        foreach (var c in new List<Vector2Int>(TerrainStreamer.LiveChunks)) Load(c);
    }

    void OnDisable()
    {
        TerrainStreamer.ChunkLoaded   -= Load;
        TerrainStreamer.ChunkUnloaded -= Unload;
        foreach (var c in new List<Vector2Int>(_live.Keys)) Unload(c);
        if (_root != null) Destroy(_root.gameObject);
    }

    // ── 청크 하나 ───────────────────────────────────────────────────────────

    void Load(Vector2Int chunk)
    {
        if (_live.ContainsKey(chunk)) return;
        var spawned = new List<GameObject>();
        _live[chunk] = spawned;

        Scatter(chunk, spawned);
        Placed(chunk, spawned);
    }

    void Unload(Vector2Int chunk)
    {
        if (!_live.TryGetValue(chunk, out var list)) return;
        _live.Remove(chunk);
        foreach (var go in list)
        {
            if (go == null) continue;

            // **내리는 것은 없어진 것이 아니다.** 표식을 먼저 세워야 `OnDestroy`가 이걸
            // *"캔 것"*으로 기억하지 않는다 — 안 세우면 청크를 한 번 벗어나는 것만으로
            // **세계의 나무가 영영 사라진다.**
            var tag = go.GetComponent<ScatterTag>();
            if (tag != null) tag.Unloading = true;

            Destroy(go);
        }
    }

    // ── 뿌리기 ──────────────────────────────────────────────────────────────

    void Scatter(Vector2Int chunk, List<GameObject> into)
    {
        _placed.Clear();

        for (int r = 0; r < scatter.Length; r++)
        {
            var rule = scatter[r];
            if (rule == null || rule.Prefab == null || rule.PerChunk <= 0f) continue;

            // **개수도 결정적이어야 한다** — 소수 부분을 확률로 쓰면 청크마다 20개냐 21개냐가
            // 갈리는데, 그것도 해시에서 나와야 다시 왔을 때 같은 숲이 선다.
            float f = rule.PerChunk;
            int count = Mathf.FloorToInt(f);
            if (Rand(chunk, r, -1, 0) < f - count) count++;

            for (int i = 0; i < count; i++)
            {
                float lx = Rand(chunk, r, i, 1) * TerrainGrid.ChunkSize;
                float lz = Rand(chunk, r, i, 2) * TerrainGrid.ChunkSize;
                float wx = chunk.x * TerrainGrid.ChunkSize + lx;
                float wz = chunk.y * TerrainGrid.ChunkSize + lz;

                float h = TerrainHeight.At(wx, wz);
                if (h < rule.MinHeight || h > rule.MaxHeight) continue;
                if (SlopeDeg(wx, wz) > rule.MaxSlope) continue;

                // **바위 속에는 안 세운다.** 경사·고도는 지상의 물음이고, 지하에서 *설 수
                // 있나*에 답하는 것은 `IsWorkable` 하나다(바위 + 머리 여유). 청크가 로드
                // 안 돼 있어도 답이 나오므로 여기서 물어도 된다.
                if (rule.RequireOpenTile)
                {
                    var map = MapManager.Instance;
                    if (map == null || !map.IsWorkable(map.WorldToTile(new Vector3(wx, 0f, wz))))
                        continue;
                }

                // **바이옴이 다르면 안 난다** — 목재(거대버섯)를 균사림에만 두는 길이 이것이다.
                // 저작한 지도가 답하므로(`WorldMap.BiomeAtWorld`) 청크가 로드 안 돼 있어도
                // 계산된다 — 이 파일의 다른 판정과 같은 성질이다.
                if (rule.Biomes != null && rule.Biomes.Length > 0 && !InBiome(rule, wx, wz))
                    continue;

                if (rule.MinSpacing > 0.01f && TooClose(wx, wz, rule.MinSpacing)) continue;

                // ── 한 자리에 여럿 ────────────────────────────────────────────
                //
                // **간격(`MinSpacing`)은 씨점끼리만 잰다** — 무리 안에서까지 재면 무리가
                // 성립하지 않는다(그게 이 축의 전부다).
                int lo = Mathf.Max(1, rule.ClusterMin);
                int hi = Mathf.Max(lo, rule.ClusterMax);
                int k  = lo + Mathf.FloorToInt(Rand(chunk, r, i, 5) * (hi - lo + 1));
                k = Mathf.Clamp(k, lo, Mathf.Min(hi, ClusterStride));

                // **자리를 먼저 고르고 나서 세운다.** 칸 목록을 미리 고르는 이유는
                // *"같은 칸에 둘"*을 막기 위해서다 — 자리를 그때그때 굴리면 겹친다.
                var spots = ClusterSpots(chunk, r, i, wx, wz, k, rule);

                for (int j = 0; j < spots.Count; j++)
                {
                    // **식구마다 제 순번을 갖는다** — `_taken`이 그 단위이기 때문이다.
                    // 하나로 묶으면 **하나만 따도 무리째 사라진다**(그리고 통째로 다시 난다).
                    //
                    // **보폭은 상수다** — 규칙의 `ClusterMax`로 곱하면 저작을 손볼 때마다
                    // 이미 딴 것의 순번이 다른 것을 가리킨다.
                    long mid = Id(chunk, r, i * ClusterStride + j);
                    if (StillTaken(mid)) continue;

                    float mx = spots[j].x, mz = spots[j].y;
                    float mh = TerrainHeight.At(mx, mz);

                    var go = Instantiate(rule.Prefab, new Vector3(mx, mh, mz),
                                         Quaternion.Euler(0f, Rand(chunk, r, i * ClusterStride + j, 3) * 360f, 0f),
                                         Root);

                    float sc = Mathf.Lerp(rule.ScaleRange.x, rule.ScaleRange.y,
                                          Rand(chunk, r, i * ClusterStride + j, 4));
                    go.transform.localScale *= sc;

                    // **캐면 기억한다** — 프리팹에 붙은 표식이 파괴될 때 순번을 남긴다.
                    // **다시 날 때까지의 날수도 표식이 든다** — 규칙 배열은 저작이라 순번으로
                    // 되찾을 수 있지만, **저작이 바뀌면 그 순번이 다른 규칙을 가리킨다.**
                    var tag = go.AddComponent<ScatterTag>();
                    tag.Id         = mid;
                    tag.RegrowDays = rule.RegrowDays;

                    into.Add(go);
                }

                _placed.Add(new Vector2Int(Mathf.RoundToInt(wx), Mathf.RoundToInt(wz)));
            }
        }
    }

    // ── 무리는 **맞닿은 칸**에 앉는다 ───────────────────────────────────────
    //
    // 반경 안에 무작위로 흩뿌리면 서넛이 *같은 동네*에 서기는 해도 **서로 떨어져 선다** —
    // 원 안의 점을 고르는 일에는 *"붙어 있어라"*라고 적을 자리가 없다. 반경을 줄이면
    // 이번엔 **한 점에 겹친다.** 둘 사이에 답이 없다.
    //
    // **칸을 고르면 간격이 규칙에서 나온다.** 칸이 1m라 이웃 칸을 고르는 순간 정확히 1m가
    // 되고, 같은 칸을 두 번 안 고르므로 겹치지도 않는다. *따닥따닥*이 곧 *맞닿은 칸*이다.
    //
    // **가까운 고리부터 채운다** — 제 칸, 그다음 맞닿은 여덟, 그다음 그 바깥. 전부 섞어서
    // 고르면 `ClusterSpread`를 2로 올리는 순간 **무리가 도넛처럼 벌어진다**(바깥 고리가
    // 칸 수가 많아 뽑힐 확률이 높다).
    static readonly List<Vector2> _spots = new();
    static readonly List<Vector2Int> _ring = new();

    List<Vector2> ClusterSpots(Vector2Int chunk, int rule, int seed,
                               float wx, float wz, int want, ScatterRule cfg)
    {
        _spots.Clear();

        var map = MapManager.Instance;
        if (map == null || cfg.ClusterSpread <= 0)
        {
            // 격자가 없으면 예전처럼 한 점이다(무리를 못 짓는다).
            for (int j = 0; j < want; j++) _spots.Add(new Vector2(wx, wz));
            return _spots;
        }

        var origin = map.WorldToTile(new Vector3(wx, 0f, wz));

        for (int ring = 0; ring <= cfg.ClusterSpread && _spots.Count < want; ring++)
        {
            _ring.Clear();
            if (ring == 0) _ring.Add(origin);
            else
                for (int dz = -ring; dz <= ring; dz++)
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;   // 고리의 테두리만
                    _ring.Add(new Vector2Int(origin.x + dx, origin.y + dz));
                }

            // **고리 안에서는 섞는다** — 안 섞으면 무리가 늘 같은 방향으로 자란다
            // (왼쪽 아래부터 채워져 ㄱ자로 선다). 결정적 셔플이라 다시 와도 같은 그림이다.
            for (int a = _ring.Count - 1; a > 0; a--)
            {
                int b = Mathf.Min(a, Mathf.FloorToInt(Rand(chunk, rule, seed * 31 + ring, 8 + a) * (a + 1)));
                (_ring[a], _ring[b]) = (_ring[b], _ring[a]);
            }

            foreach (var t in _ring)
            {
                if (_spots.Count >= want) break;
                if (cfg.RequireOpenTile && !map.IsWorkable(t)) continue;   // 벽에는 안 난다

                // **칸 한복판에 앉힌다** — 칸을 고른 뜻이 그것이다. 안 그러면 다시 흩어진다.
                var c = map.TileToWorld(t);
                _spots.Add(new Vector2(c.x, c.z));
            }
        }

        return _spots;
    }

    // 한 씨점이 가질 수 있는 식구 수의 천장. **저작값이 아니라 순번 인코딩의 보폭**이라
    // 상수여야 한다 — `Id`가 순번을 16비트에 담으므로 청크당 씨점 × 이 값이 65,536을
    // 넘으면 안 된다(지금은 열댓 × 여덟이라 한참 아래다).
    const int ClusterStride = 8;

    static bool InBiome(ScatterRule rule, float x, float z)
    {
        var here = WorldMap.BiomeAtWorld(new Vector2(x, z));
        for (int i = 0; i < rule.Biomes.Length; i++)
            if (rule.Biomes[i] == here) return true;
        return false;
    }

    static bool TooClose(float x, float z, float spacing)
    {
        float s2 = spacing * spacing;
        for (int i = 0; i < _placed.Count; i++)
        {
            float dx = _placed[i].x - x, dz = _placed[i].y - z;
            if (dx * dx + dz * dz < s2) return true;
        }
        return false;
    }

    // 경사(도) — 두 방향의 높이차로 잰다. **콜라이더를 안 본다**(로드 전에도 계산된다).
    static float SlopeDeg(float x, float z)
    {
        const float d = 2f;
        float hx = TerrainHeight.At(x + d, z) - TerrainHeight.At(x - d, z);
        float hz = TerrainHeight.At(x, z + d) - TerrainHeight.At(x, z - d);
        float grad = Mathf.Sqrt(hx * hx + hz * hz) / (2f * d);
        return Mathf.Atan(grad) * Mathf.Rad2Deg;
    }

    // ── 저작한 장소 ─────────────────────────────────────────────────────────

    // **`Id`가 키다 — 인덱스가 아니다.** 전에는 배열 순번이었는데, 저작으로 배열이 재정렬되면
    // 그 순번이 가리키던 곳이 달라졌다. 신원은 저작이 바뀌어도 안 변하는 것이어야 한다.
    static readonly HashSet<string> _placedUp = new();   // Persistent인 것은 한 번만 세운다

    // **청크 단위로 게으르게 읽는다** — 대부분의 청크에는 에셋이 없다. `Resources.LoadAll`로
    // 통째로 읽으면 없는 것까지 훑으므로, 물어볼 때 하나씩 읽고 **없다는 사실도 기억한다**
    // (안 그러면 빈 청크를 밟을 때마다 다시 찾는다 — `TerrainHeight.DeltaOf`와 같은 자리).
    static readonly Dictionary<Vector2Int, WorldChunkContent> _content = new();

    static WorldChunkContent ContentOf(Vector2Int chunk)
    {
        if (_content.TryGetValue(chunk, out var c)) return c;
        c = Resources.Load<WorldChunkContent>(WorldChunkContent.ResourcePathOf(chunk));
        _content[chunk] = c;    // **null도 넣는다** — "없음"을 기억하는 것이 요점이다
        return c;
    }

    // 저작 도구가 에셋을 다시 구운 뒤 부른다. 안 비우면 **에디터가 옛 목록을 계속 쓴다.**
    public static void ForgetContent() => _content.Clear();

    void Placed(Vector2Int chunk, List<GameObject> into)
    {
        var content = ContentOf(chunk);
        if (content == null || content.Objects == null) return;

        foreach (var o in content.Objects)
        {
            if (o == null || o.Prefab == null) continue;

            // 신원이 없으면 자리로 대신한다 — **없는 채로 두면 `Persistent` 추적이
            // 통째로 어긋나** 마을이 청크를 밟을 때마다 하나씩 더 선다.
            string id = string.IsNullOrEmpty(o.Id)
                      ? TerrainGrid.KeyOf(chunk) + ":" + o.Position.x + "," + o.Position.y
                      : o.Id;

            if (o.Persistent && _placedUp.Contains(id)) continue;

            float h = TerrainHeight.At(o.Position.x, o.Position.y) + o.YOffset;
            var go = Instantiate(o.Prefab,
                                 new Vector3(o.Position.x, h, o.Position.y),
                                 Quaternion.Euler(0f, o.Yaw, 0f),
                                 o.Persistent ? null : Root);
            go.name = o.Prefab.name + " (" + o.Name + ")";

            // **없으면 안 건드린다** — 프리팹이 든 크기를 그대로 쓴다(`WorldObject.Scale` 참고).
            if (o.Scale != Vector3.zero) go.transform.localScale = o.Scale;

            // **저작한 명세가 있으면 덮는다**(2026-08-27, 월드 에디터 4단계).
            //
            // `Instantiate`가 `Awake`를 그 자리에서 돌리므로 이 시점에는 **프리팹에 적어 둔
            // 명세가 이미 얹혀 있다**(`UnitBrain.ApplyAuthoredSpec`). 그 위에 덮는 것이
            // 맞다 — **저작(프리팹) < 주입(그 자리)** 이 이 저장소의 우선순위다(상인 상품과
            // 같은 규칙). 그래서 *"같은 산적 프리팹인데 이 소굴의 두목만 특정 인물"*이 된다.
            var pawn = go.GetComponent<UnitBrain>();

            if (o.Spec != null)
            {
                if (pawn != null) pawn.ApplySpec(o.Spec.Build(pawn.Data));
            }
            // **저작한 명세가 없어도 같은 사람이 선다**(2026-09-08, 사용자 결정 — 마을을
            // 군데군데 두고 그 안에서 퀘스트를 진행할 것이라, *"고정 NPC의 배경이 매번
            // 바뀌는 건 이상하다"*).
            //
            // 그전에는 `UnitBrain.Awake`가 `UnityEngine.Random`으로 굴렸다. 그래서
            // **판을 새로 열 때마다 마을 사람이 딴사람**이었고, `Persistent`가 아닌 물건은
            // **청크가 다시 올라올 때마다** 바뀌었다(갈 때마다 딴사람이다).
            //
            // **재료는 이미 다 있었다** — `id`가 안정된 신원이고(`Persistent` 추적이 쓰는
            // 그 값) `PawnSpec.Roll`은 시드를 받는다. 둘을 잇기만 하면 **마을 사람마다
            // 명세를 저작하지 않고도** 판마다 같은 사람이 선다.
            //
            // **`string.GetHashCode`를 안 쓴다** — 실행마다 달라져서 시드로는 못 쓴다
            // (`PawnSpecData`가 먼저 겪은 함정이고 여기도 같다).
            //
            // **세력의 이력 묶음을 넘긴다** — 안 넘기면 `civilian` 폴백이라 마을 사람과
            // 산적이 같은 이력을 산다.
            else if (pawn != null && pawn.Data != null)
            {
                var faction = go.GetComponent<Faction>();
                pawn.ApplySpec(PawnSpec.Roll(pawn.Data, StableSeed(id), null,
                                             faction != null && faction.Data != null
                                                 ? faction.Data.BackstoryGroup : null));
            }

            if (o.Persistent) _placedUp.Add(id);   // 안 내린다 — 안의 상태가 처음으로 돌아가면 안 된다
            else into.Add(go);
        }
    }

    // 신원 문자열에서 시드를 낸다. **실행마다 같아야 한다.**
    static int StableSeed(string id)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
            return h != 0 ? h : 1;   // 0은 "안 정해짐"이라 다른 뜻이다(`PawnSpecData.Seed`)
        }
    }

    // ── 결정적 난수 ─────────────────────────────────────────────────────────

    // `i`는 **씨점 순번 × `ClusterStride` + 식구 번호**다(부르는 쪽이 합친다).
    long Id(Vector2Int c, int rule, int i) =>
        (((long)c.x & 0xFFFF) << 40) | (((long)c.y & 0xFFFF) << 24) | ((long)(rule & 0xFF) << 16) | (uint)(i & 0xFFFF);

    // 해시 → 0~1. **`Random`을 쓰지 않는다** — 전역 상태라 부르는 순서가 바뀌면 숲이 달라진다.
    float Rand(Vector2Int c, int rule, int i, int salt)
    {
        unchecked
        {
            uint h = (uint)scatterSeed;
            h = h * 2166136261u ^ (uint)c.x;      h *= 16777619u;
            h = h ^ (uint)c.y;                    h *= 16777619u;
            h = h ^ (uint)rule;                   h *= 16777619u;
            h = h ^ (uint)i;                      h *= 16777619u;
            h = h ^ (uint)salt;                   h *= 16777619u;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // 캔 나무를 기억한다.
    // `regrowDays`가 0 이하면 영영 안 난다(나무·바위).
    //
    // **이미 적힌 것보다 늦게 덮어쓰지 않는다** — 같은 id가 두 번 오는 일은 없어야 하지만,
    // 온다면 그건 *두 번 캤다*는 뜻이라 나중 것이 맞다. 그냥 덮어쓴다.
    public static void MarkTaken(long id, float regrowDays)
        => _taken[id] = regrowDays > 0f
                      ? GameTimeManager.CurrentDay + regrowDays
                      : float.PositiveInfinity;

    // 아직 안 났나. **여기서 지운다** — 훑는 김에 만료된 것을 걷어내지 않으면 딸 때마다
    // 사전이 자라기만 한다(한 판 내내 딴 버섯이 전부 남는다).
    static bool StillTaken(long id)
    {
        if (!_taken.TryGetValue(id, out float back)) return false;
        if (GameTimeManager.CurrentDay < back) return true;
        _taken.Remove(id);
        return false;
    }
}

// 뿌려진 것에 붙는 표식. **파괴될 때 순번을 남긴다** — 안 남기면 청크를 다시 밟을 때
// 캔 나무가 되살아난다.
public class ScatterTag : MonoBehaviour
{
    [HideInInspector] public long Id;

    // 딴 뒤 며칠 만에 다시 나나(0이면 안 난다). **뿌릴 때 규칙에서 받아 든다** —
    // 파괴되는 시점에 규칙 배열을 되짚으면 그 사이 저작이 바뀌었을 때 엉뚱한 값을 쓴다.
    [HideInInspector] public float RegrowDays;

    // **청크가 내려갈 때는 남기면 안 된다.** 그건 없어진 것이 아니라 잠깐 치운 것이다.
    // 그래서 스트리머가 내릴 때는 이 플래그를 세우고 지운다.
    [HideInInspector] public bool Unloading;

    void OnDestroy()
    {
        if (!Unloading && Application.isPlaying) WorldContentStreamer.MarkTaken(Id, RegrowDays);
    }
}

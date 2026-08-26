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

    [Header("저작한 장소 — 마을 · 주둔지")]
    [SerializeField] WorldSite[] sites = System.Array.Empty<WorldSite>();

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

    // **캔 나무는 다시 안 자란다.** 청크를 떠났다 돌아오면 결정적 계산이 그 나무를 **또**
    // 세우므로, 없어진 것을 기억해야 한다. 좌표가 아니라 **뿌린 순번(id)**으로 기억한다 —
    // 자리는 계산으로 나오지만 *"몇 번째 것"*은 안 바뀐다.
    static readonly HashSet<long> _taken = new();

    static readonly List<Vector2Int> _placed = new();   // 간격 검사용 임시

    void OnEnable()
    {
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
        Sites(chunk, spawned);
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
                long id = Id(chunk, r, i);
                if (_taken.Contains(id)) continue;   // 캔 것은 다시 안 선다

                float lx = Rand(chunk, r, i, 1) * TerrainGrid.ChunkSize;
                float lz = Rand(chunk, r, i, 2) * TerrainGrid.ChunkSize;
                float wx = chunk.x * TerrainGrid.ChunkSize + lx;
                float wz = chunk.y * TerrainGrid.ChunkSize + lz;

                float h = TerrainHeight.At(wx, wz);
                if (h < rule.MinHeight || h > rule.MaxHeight) continue;
                if (SlopeDeg(wx, wz) > rule.MaxSlope) continue;

                if (rule.MinSpacing > 0.01f && TooClose(wx, wz, rule.MinSpacing)) continue;

                var go = Instantiate(rule.Prefab, new Vector3(wx, h, wz),
                                     Quaternion.Euler(0f, Rand(chunk, r, i, 3) * 360f, 0f), Root);

                float sc = Mathf.Lerp(rule.ScaleRange.x, rule.ScaleRange.y, Rand(chunk, r, i, 4));
                go.transform.localScale *= sc;

                // **캐면 기억한다** — 프리팹에 붙은 표식이 파괴될 때 순번을 남긴다.
                go.AddComponent<ScatterTag>().Id = id;

                into.Add(go);
                _placed.Add(new Vector2Int(Mathf.RoundToInt(wx), Mathf.RoundToInt(wz)));
            }
        }
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

    static readonly HashSet<int> _sitesUp = new();   // Persistent인 것은 한 번만 세운다

    void Sites(Vector2Int chunk, List<GameObject> into)
    {
        for (int i = 0; i < sites.Length; i++)
        {
            var s = sites[i];
            if (s == null || s.Prefab == null) continue;
            if (TerrainGrid.ChunkAt(s.Position.x, s.Position.y) != chunk) continue;
            if (s.Persistent && _sitesUp.Contains(i)) continue;

            float h = TerrainHeight.At(s.Position.x, s.Position.y) + s.YOffset;
            var go = Instantiate(s.Prefab,
                                 new Vector3(s.Position.x, h, s.Position.y),
                                 Quaternion.Euler(0f, s.Rotation, 0f),
                                 s.Persistent ? null : Root);
            go.name = s.Prefab.name + " (" + s.Name + ")";

            if (s.Persistent) _sitesUp.Add(i);   // 안 내린다 — 안의 상태가 처음으로 돌아가면 안 된다
            else into.Add(go);
        }
    }

    // ── 결정적 난수 ─────────────────────────────────────────────────────────

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
    public static void MarkTaken(long id) => _taken.Add(id);
}

// 뿌려진 것에 붙는 표식. **파괴될 때 순번을 남긴다** — 안 남기면 청크를 다시 밟을 때
// 캔 나무가 되살아난다.
public class ScatterTag : MonoBehaviour
{
    [HideInInspector] public long Id;

    // **청크가 내려갈 때는 남기면 안 된다.** 그건 없어진 것이 아니라 잠깐 치운 것이다.
    // 그래서 스트리머가 내릴 때는 이 플래그를 세우고 지운다.
    [HideInInspector] public bool Unloading;

    void OnDestroy()
    {
        if (!Unloading && Application.isPlaying) WorldContentStreamer.MarkTaken(Id);
    }
}

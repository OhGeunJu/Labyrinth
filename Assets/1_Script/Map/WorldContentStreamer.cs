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

    public GameObject TownPrefab => townPrefab;
    public GameObject LairPrefab => lairPrefab;

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

    // ── 뿌린 것이 없으면 왜 없는지가 화면에 남아야 한다 (2026-09-15) ────────
    //
    // 이 자리에서 두 번 당했다. **버섯**은 배선 도구가 규칙을 매번 지워서 통째로 안 났고,
    // **바닥 광맥**은 밀도가 낮아 *"안 나온다"*로 보였다. 둘 다 **에셋도 프리팹도 멀쩡해서**
    // 화면에 남는 단서가 아무것도 없었고, 원인이 코드인지 배선인지 눈금인지 가릴 수가 없었다.
    //
    // 첫 스물다섯 청크를 세어 **한 줄로 찍는다** — 그 뒤로는 조용하다. 규칙마다 *몇 번
    // 찍었고 어디서 버려졌나*가 나오므로, 0이 어디서 나오는지가 한눈에 보인다.
    struct Census
    {
        public int     Tried, Steep, Rock, Biome, NoPrefab, Close, Made;

        // **칸은 열렸는데 발자국이 안 들어간 것**(2026-09-15). `Rock`과 갈라 센다 —
        // 합쳐 두면 *바위가 많아서 안 선다*와 *덩치가 커서 안 선다*가 한 숫자가 되고,
        // 밀도를 얼마나 올려야 하는지를 그 숫자로는 못 낸다.
        public int     Tight;

        // **지은 자리라 안 선 것**(2026-09-28) — 바위와 갈라 센다. 건물을 부수면 다시 난다.
        public int     Built;

        // **어디에 섰는지도 적는다** — *"몇 개 섰다"*만으로는 *"안 보인다"*를 못 푼다.
        // 숫자가 0이 아닌데 화면에 없으면 다음 물음은 **어디에**이고, 그 답이 없으면
        // 찾으러 다니는 수밖에 없다. 처음 선 것 하나면 족하다(거기 가 보면 끝난다).
        public string  FirstName;
        public Vector3 FirstAt;

        // **"몇 개 섰다"는 "보인다"가 아니다**(2026-09-15). 서른네 개가 섰다는 결산을
        // 받고도 화면에는 아무것도 없었고, 에셋을 하루 종일 뒤졌지만 프리팹도 메시도
        // 재질도 멀쩡했다 — **세어 놓은 숫자와 눈에 보이는 것 사이에 잰 것이 없었다.**
        //
        // 그래서 처음 선 것 하나를 **세우자마자 재 둔다**: 붙은 렌더러 · 실제로 그려지는
        // 렌더러 · 재질이 빈 것 · 월드 경계 · 스케일. 다섯이 서로 다른 원인을 가리킨다.
        //
        // **`GetComponentsInChildren<Renderer>(true)`의 개수로는 못 푼다** — 꺼진 것과
        // 비활성까지 세므로 *"렌더러가 있다"*가 *"그려진다"*를 뜻하지 않는다. 처음 낸
        // 진단이 그것이었고, **에러가 안 뜬 것이 아무것도 증명하지 않았다.**
        public int     FirstAttached, FirstLive, FirstNoMat;
        public Vector3 FirstSize;
        public float   FirstScale;
    }

    Census[] _census;
    int      _censusChunks;
    const int CensusChunks = 25;      // 스트리밍 반경 2면 5x5 = 25장이 첫 판이다

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
        _regrowAt.Clear();
        _liveIds.Clear();
        _foot.Clear();
        _underBuilt.Clear();
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
        _underBuilt.Remove(chunk);   // 다시 세울 때(`Load`) 새로 적힌다
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

    // `regrowOnly` — **이미 선 청크에 다시 날 것만 더한다**(`RegrowLive`). 자리 판정은 처음과 똑같이 다시 돈다
    // (결정적이라 같은 답이 나온다) — 지금 서 있는 것만 건너뛴다(`_liveIds`). 결산은 안 센다.
    void Scatter(Vector2Int chunk, List<GameObject> into, bool regrowOnly = false)
    {
        _placed.Clear();
        CollectBuilt(chunk, _builtTiles);

        if (_census == null || _census.Length != scatter.Length) _census = new Census[scatter.Length];
        bool census = !regrowOnly && _censusChunks < CensusChunks;

        for (int r = 0; r < scatter.Length; r++)
        {
            var rule = scatter[r];
            if (rule == null || rule.PerChunk <= 0f) continue;
            if (!rule.FromBiomePool && rule.Prefab == null) continue;

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

                if (census) _census[r].Tried++;

                float h = TerrainHeight.At(wx, wz);
                if (h < rule.MinHeight || h > rule.MaxHeight) { if (census) _census[r].Steep++; continue; }
                if (SlopeDeg(wx, wz) > rule.MaxSlope)         { if (census) _census[r].Steep++; continue; }

                // **무엇이 설지를 여기서 먼저 고른다** — 아래의 자리 판정이 **그 물건의
                // 발자국**을 알아야 하기 때문이다(3x3 광맥과 1x1 버섯은 다른 자리를 묻는다).
                var prefab = rule.FromBiomePool ? PoolPrefab(chunk, r, i, wx, wz) : rule.Prefab;
                if (prefab == null) { if (census) _census[r].NoPrefab++; continue; }

                var foot = FootOf(prefab);

                // **지은 자리에는 안 난다 — 바위 판정보다 먼저 묻는다.** 칸을 막는 건물은 아래
                // `IsWorkable`도 거절하지만, 거기서 걸리면 *바위라서*와 갈리지 않아 **부순 뒤에
                // 다시 날 자리로 적히지 않는다**(`_underBuilt`).
                if (BuiltOn(new Vector3(wx, 0f, wz), foot, chunk))
                { if (census) _census[r].Built++; continue; }

                // **바위 속에는 안 세운다 — 그리고 *발자국*으로 묻는다**(2026-09-15).
                //
                // 경사·고도는 지상의 물음이고, 지하에서 *설 수 있나*에 답하는 것은
                // `IsWorkable` 하나다(바위 + 머리 여유). 여기까지는 전부터 맞았다.
                //
                // **틀린 것은 무엇을 물었느냐가 아니라 *어디를* 물었느냐였다.** 칸 하나만
                // 봤는데 광맥은 `size: 3x3`에 덩치가 2.7m다 — 세계의 76%가 바위인 굴에서
                // 한 칸만 열려 있으면 **나머지 여덟 칸은 24m 벽 속**이고, 광맥은 그 벽에
                // 파묻힌 채 선다. 그리는 것은 멀쩡하다(결산이 *렌더러 34개가 전부 그려진다*고
                // 답했다) — **벽이 앞을 가릴 뿐이다.**
                //
                // 그래서 증상이 **크기에 따라 갈렸다**: 바닥 돌·버섯은 1x1이라 멀쩡히 보이고
                // **광맥만 안 보였다.** 사흘 동안 렌더링과 밀도와 시작 칸을 뒤졌는데, 원인은
                // *점으로 물어 놓고 덩어리를 놓은 것*이었다.
                //
                // 칸 목록은 `ResourceNode.FootprintAt`이 답한다 — **세우고 나서 막는 칸과
                // 같은 함수**라, 짝수 크기의 한 칸 어긋남까지 저절로 따라온다.
                if (rule.RequireOpenTile)
                {
                    var map = MapManager.Instance;
                    if (map == null) { if (census) _census[r].Rock++; continue; }

                    if (!map.IsWorkable(map.WorldToTile(new Vector3(wx, 0f, wz))))
                    { if (census) _census[r].Rock++; continue; }

                    if (!Fits(map, new Vector3(wx, 0f, wz), foot))
                    { if (census) _census[r].Tight++; continue; }
                }

                // **바이옴이 다르면 안 난다** — 목재(거대버섯)를 균사림에만 두는 길이 이것이다.
                // 저작한 지도가 답하므로(`WorldMap.BiomeAtWorld`) 청크가 로드 안 돼 있어도
                // 계산된다 — 이 파일의 다른 판정과 같은 성질이다.
                if (!rule.FromBiomePool &&
                    rule.Biomes != null && rule.Biomes.Length > 0 && !InBiome(rule, wx, wz))
                { if (census) _census[r].Biome++; continue; }

                // (무엇이 설지는 위에서 골랐다 — 자리 판정이 그 발자국을 알아야 한다.
                //  **씨점마다 한 번만 고른다** — 식구마다 다시 고르면 한 무리에 구리와
                //  철이 섞여 서서 *"광맥 하나"*로 안 읽힌다.)

                if (rule.MinSpacing > 0.01f && TooClose(wx, wz, rule.MinSpacing))
                { if (census) _census[r].Close++; continue; }

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
                var spots = ClusterSpots(chunk, r, i, wx, wz, k, rule, foot);

                for (int j = 0; j < spots.Count; j++)
                {
                    // **식구마다 제 순번을 갖는다** — `_taken`이 그 단위이기 때문이다.
                    // 하나로 묶으면 **하나만 따도 무리째 사라진다**(그리고 통째로 다시 난다).
                    //
                    // **보폭은 상수다** — 규칙의 `ClusterMax`로 곱하면 저작을 손볼 때마다
                    // 이미 딴 것의 순번이 다른 것을 가리킨다.
                    long mid = Id(chunk, r, i * ClusterStride + j);
                    if (StillTaken(mid)) continue;
                    if (_liveIds.Contains(mid)) continue;   // 지금 서 있다(다시 뿌릴 때)

                    float mx = spots[j].x, mz = spots[j].y;

                    // 식구도 묻는다 — 씨점은 비었어도 무리의 한 칸에 지었을 수 있다.
                    if (BuiltOn(new Vector3(mx, 0f, mz), foot, chunk)) continue;

                    float mh = TerrainHeight.At(mx, mz);

                    var rot = Quaternion.Euler(0f, Rand(chunk, r, i * ClusterStride + j, 3) * 360f, 0f);
                    var at  = new Vector3(mx, mh, mz);

                    // **바이옴 풀에서 나온 것은 야생이다** — 노드 프리팹은 이식 광맥이라
                    // 그대로 세우면 굴 한복판의 광맥이 배정을 받고 마르지 않는다.
                    var go = rule.FromBiomePool
                           ? ResourceNode.SpawnWild(prefab, at, rot, Root)
                           : Instantiate(prefab, at, rot, Root);
                    if (go == null) continue;

                    float sc = Mathf.Lerp(rule.ScaleRange.x, rule.ScaleRange.y,
                                          Rand(chunk, r, i * ClusterStride + j, 4));
                    go.transform.localScale *= sc;

                    // **크기를 곱한 뒤에 잰다** — 앞에서 재면 경계가 프리팹의 것이라
                    // *뿌리기가 0으로 눌러 버린 것*을 못 잡는다(재는 자리가 한 프레임이
                    // 아니라 **한 줄** 어긋나도 답이 달라진다).
                    if (census)
                    {
                        if (_census[r].Made == 0) Measure(ref _census[r], go);
                        _census[r].Made++;
                    }

                    // **캐면 기억한다** — 프리팹에 붙은 표식이 파괴될 때 순번을 남긴다.
                    // **다시 날 때까지의 날수도 표식이 든다** — 규칙 배열은 저작이라 순번으로
                    // 되찾을 수 있지만, **저작이 바뀌면 그 순번이 다른 규칙을 가리킨다.**
                    var tag = go.AddComponent<ScatterTag>();
                    tag.Id         = mid;
                    tag.Chunk      = chunk;
                    tag.RegrowDays = rule.RegrowDays;
                    _liveIds.Add(mid);

                    into.Add(go);
                }

                _placed.Add(new Vector2Int(Mathf.RoundToInt(wx), Mathf.RoundToInt(wz)));
            }
        }

        if (!census) return;
        if (++_censusChunks == CensusChunks) ReportCensus();
    }

    // 처음 선 것 하나를 잰다 — **답이 한 벌이라 여기 하나다.** 세우는 자리가 둘이고
    // (`SpawnWild` · 그냥 `Instantiate`) 저마다 재게 두면 **야생 광맥만 진단이 있고
    // 버섯은 없는** 꼴이 된다 — 실제로 *"자원 뭐 뜨는 것도 없어"*는 둘 다를 가리켰다.
    static void Measure(ref Census c, GameObject go)
    {
        c.FirstName  = go.name;
        c.FirstAt    = go.transform.position;
        c.FirstScale = go.transform.localScale.x;

        var all = go.GetComponentsInChildren<Renderer>(true);
        c.FirstAttached = all.Length;

        // **경계는 그려지는 것만 모은다** — 꺼진 렌더러의 경계까지 더하면
        // *"크기는 멀쩡한데 안 보인다"*가 되어 눈금이 거짓말을 한다.
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in all)
        {
            if (r.sharedMaterial == null) c.FirstNoMat++;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            c.FirstLive++;
            b.Encapsulate(r.bounds);
        }
        c.FirstSize = c.FirstLive > 0 ? b.size : Vector3.zero;
    }

    // **한 줄로 찍는다.** 규칙마다 *찍은 수 → 어디서 버려졌나 → 세운 수*.
    // **0을 강조한다** — 아무것도 안 난 규칙이 이 로그의 존재 이유다.
    void ReportCensus()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[뿌리기] 첫 {CensusChunks}청크({TerrainGrid.ChunkSize}m) 결산 — ");
        sb.Append("규칙마다 `찍음 → 버림(경사/바위/좁음/바이옴/풀없음/간격/건물) → 세움`\n");

        for (int r = 0; r < scatter.Length; r++)
        {
            var rule = scatter[r];
            var c    = _census[r];
            string name = rule != null && !string.IsNullOrEmpty(rule.Name) ? rule.Name : $"규칙{r}";

            sb.Append($"  · {name}: {c.Tried} → " +
                      $"경사 {c.Steep} · 바위 {c.Rock} · 좁음 {c.Tight} · 바이옴 {c.Biome} · " +
                      $"풀없음 {c.NoPrefab} · 간격 {c.Close} · 건물 {c.Built}" +
                      $" → **{c.Made}개**");

            if (c.Made == 0)
                sb.Append("  ← **하나도 안 섰다.** 위 숫자에서 어디로 다 빠졌는지 보라 " +
                          "(전부 0이면 규칙 자체가 안 돈 것 — `PerChunk`·프리팹을 볼 것)");
            else
            {
                sb.Append($"  첫 자리 {c.FirstName} @ ({c.FirstAt.x:F0}, {c.FirstAt.y:F1}, {c.FirstAt.z:F0})" +
                          $" · 렌더러 {c.FirstAttached}개 중 **그려지는 것 {c.FirstLive}개**" +
                          $" · 재질 없음 {c.FirstNoMat}개" +
                          $" · 경계 {c.FirstSize.x:F2}x{c.FirstSize.y:F2}x{c.FirstSize.z:F2}" +
                          $" · 크기 {c.FirstScale:F2}");

                if (c.FirstAttached == 0)
                    sb.Append("  ← **모양이 없다**(프리팹이 비었거나 세우는 길에서 잃었다)");
                else if (c.FirstLive == 0)
                    sb.Append("  ← **꺼졌거나 비활성이다**(그려지는 렌더러가 0이다)");
                else if (c.FirstNoMat > 0)
                    sb.Append("  ← **재질이 빈 렌더러가 있다**");
                else if (c.FirstSize.y < 0.05f)
                    sb.Append("  ← **경계가 0이다**(크기가 눌렸다)");
            }

            sb.Append('\n');
        }

        sb.Append("센 것이 0이 아닌데 화면에 없으면 **첫 자리로 가 볼 것** — " +
                  "Play 중 하이어라키의 `WorldContent` 아래에 그대로 있다.\n" +
                  "밀도를 고치는 문은 씬이 아니라 도구다 — 버섯·바닥 돌은 " +
                  "`World → 7. Create Mushrooms`, 바닥 광맥은 `World → B. Create Metal Ladder`.");
        Debug.Log(sb.ToString());
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
                               float wx, float wz, int want, ScatterRule cfg, Vector2Int foot)
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
                // **식구도 발자국으로 묻는다** — 씨점만 재고 식구를 칸으로 재면
                // 무리 가장자리의 광맥만 벽에 파묻힌다(원인이 반쪽만 고쳐진 모양이다).
                if (cfg.RequireOpenTile && !Fits(map, map.TileToWorld(t), foot)) continue;

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

    // **바이옴의 자원 노드 풀에서 하나.** 벽 속 광맥과 **같은 표**를 읽는다
    // (`RockMap.VeinAt`) — 여기서 새 목록을 들면 *"이 층에서 무엇이 나나"*의 답이 두 벌이
    // 되고, 금속 사다리를 손볼 때 한쪽이 뒤처진다.
    //
    // **다만 표를 그대로 쓰면 안 된다 — 벽의 물음과 바닥의 물음이 한 칸 다르다**
    // (2026-09-15, 사용자: *"지금 있는거는 그냥 돌이겠지"*).
    //
    // 풀에는 `Stone`이 들어 있고 **벽에서는 그게 맞다** — *"이 층은 광맥이 드물다"*는
    // 희석이다(석회굴은 다섯 중 둘이 `Stone`이다). 그런데 **바닥에 세우면 그냥 돌덩이**이고,
    // 돌덩이는 `Node_Pebbles`가 이미 청크당 스물씩 깔고 있다. 그래서 굴 바닥에 선 것의
    // 3분의 1이 **자원이 아닌 돌**이었고, 화면에는 *"광맥이 없다"*로 보였다.
    //
    // **가르는 것은 `IsVein` 하나다** — *박힌 것이 있나*. 벽이 알갱이를 그릴지 정하는 그
    // 사실이고, 여기서 또 목록을 들면 **`Stone`을 뺀 표가 두 벌**이 된다.
    //
    // **`Prefab`이 빈 노드도 건너뛴다** — 뽑고 조용히 아무것도 안 세우면 **그 씨점이 통째로
    // 사라져** 바이옴마다 밀도가 달라진다. 하나도 못 고르면 `null`이고, 그건 결산의
    // *풀없음*으로 세어진다(조용히 없어지지 않는다).
    GameObject PoolPrefab(Vector2Int chunk, int rule, int seed, float x, float z)
    {
        var pool = BiomeContent.ResourceNodes(WorldMap.BiomeAtWorld(new Vector2(x, z)));
        if (pool == null || pool.Count == 0) return null;

        int start = Mathf.Clamp(Mathf.FloorToInt(Rand(chunk, rule, seed, 6) * pool.Count),
                                0, pool.Count - 1);

        for (int k = 0; k < pool.Count; k++)
        {
            var n = pool[(start + k) % pool.Count];
            if (n != null && n.Prefab != null && n.IsVein) return n.Prefab;
        }
        return null;
    }

    // ── 덩치가 들어가나 ────────────────────────────────────────────────────
    //
    // **점이 아니라 발자국을 묻는다.** `IsWorkable`은 *이 칸에 설 수 있나*에 답하는데,
    // 세우는 것은 칸 하나가 아니라 물건이다. 3x3짜리를 한 칸만 보고 놓으면 나머지
    // 여덟 칸이 바위여도 통과하고, 그 물건은 **벽 속에 박힌 채 선다.**
    static bool Fits(MapManager map, Vector3 at, Vector2Int foot)
    {
        if (foot.x <= 1 && foot.y <= 1) return true;      // 한 칸짜리는 이미 위에서 쟀다

        foreach (var t in ResourceNode.FootprintAt(at, foot))
            if (!map.IsWorkable(t)) return false;
        return true;
    }

    // 프리팹의 칸 수. **`ResourceNode`가 없으면 한 칸이다** — 채집물(`Forageable`)과
    // 바닥 돌이 그렇고, 그것들은 실제로 1m라 예전과 한 줄도 안 달라진다.
    //
    // **캐싱한다** — 청크마다 수백 번 묻는데 `GetComponent`는 그때마다 계층을 훑는다.
    static readonly Dictionary<GameObject, Vector2Int> _foot = new();

    static Vector2Int FootOf(GameObject prefab)
    {
        if (_foot.TryGetValue(prefab, out var f)) return f;

        var node = prefab.GetComponent<ResourceNode>();
        f = node != null ? node.Size : Vector2Int.one;
        _foot[prefab] = f;
        return f;
    }

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

            // **폰이면 명세를 세우기 전에 낸다** — 명세의 성별이 **어느 몸을 세울지** 고르므로(`PawnSpawn`)
            // 세운 뒤에 굴리면 여자 이름에 남자 몸이 선다. 프리팹 에셋에서도 종족·세력은 읽힌다(직렬화된 값).
            var spec = SpecFor(o, id);
            var go = PawnSpawn.Instantiate(o.Prefab,
                                           new Vector3(o.Position.x, h, o.Position.y),
                                           Quaternion.Euler(0f, o.Yaw, 0f),
                                           o.Persistent ? null : Root, spec);
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
            if (pawn != null && spec != null) pawn.ApplySpec(spec);

            if (o.Persistent) _placedUp.Add(id);   // 안 내린다 — 안의 상태가 처음으로 돌아가면 안 된다
            else into.Add(go);
        }
    }

    // 이 자리에 설 폰의 명세 — 저작한 것이 있으면 그것, 없으면 신원에서 굴린다. 폰이 아니면 null.
    static PawnSpec SpecFor(WorldObject o, string id)
    {
        var brain = o.Prefab.GetComponent<UnitBrain>();
        if (brain == null) return null;

        if (o.Spec != null) return o.Spec.Build(brain.Data);
        if (brain.Data == null) return null;

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
        var faction = o.Prefab.GetComponent<Faction>();
        return PawnSpec.Roll(brain.Data, StableSeed(id), null,
                             faction != null && faction.Data != null ? faction.Data.BackstoryGroup : null);
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
    public static void MarkTaken(long id, float regrowDays, Vector2Int chunk)
    {
        float back = regrowDays > 0f ? GameTimeManager.CurrentDay + regrowDays : float.PositiveInfinity;
        _taken[id] = back;
        if (regrowDays > 0f) _regrowAt[id] = (back, chunk);
    }

    // ── 선 자리에서 다시 난다 (2026-09-28) ──────────────────────────────────
    //
    // 사용자: *"버섯, 나무 캐고나면 나중에 다시 자라게"*. 버섯은 09-06부터 `RegrowDays`가 있었는데 **확인하는 자리가
    // 청크를 새로 세울 때(`Load`) 하나뿐이었다** — 기지처럼 늘 로드돼 있는 곳에서는 날짜가 지나도 **영영 안 났다.**
    // 딴 자리 바로 옆에 사는 것이 이 게임의 기지인데, 정확히 거기서만 안 자랐다.
    //
    // 그래서 **서 있는 청크도 가끔 훑는다.** 날짜가 된 것이 있는 청크만 다시 뿌린다(`Scatter(…, regrowOnly)`) —
    // 뿌리기가 결정적이므로 **같은 자리에 같은 것**이 선다. 막혔으면(그 자리에 무언가 지었으면) 판정이 거절한다.
    //
    // **다시 날 것만 따로 든다**(`_regrowAt`) — `_taken`에는 영영 안 나는 것(바닥 돌 등)이 판 내내 쌓이므로 그걸 훑지 않는다.
    static readonly Dictionary<long, (float day, Vector2Int chunk)> _regrowAt = new();

    // 지금 세계에 서 있는 뿌린 것의 순번 — 다시 뿌릴 때 **두 벌** 세우지 않으려고.
    static readonly HashSet<long> _liveIds = new();

    public static void Forget(long id) => _liveIds.Remove(id);

    const float RegrowCheckSeconds = 3f;   // 실시간 — 날짜는 하루에 한 번 바뀌므로 촘촘할 필요가 없다
    float _nextRegrowCheck;
    static readonly HashSet<Vector2Int> _dueChunks = new();
    static readonly List<long>          _dueIds    = new();

    void Update()
    {
        if (Time.unscaledTime < _nextRegrowCheck) return;
        if (_regrowAt.Count == 0 && _underBuilt.Count == 0) return;
        _nextRegrowCheck = Time.unscaledTime + RegrowCheckSeconds;
        RegrowLive();
        RegrowUnbuilt();
    }

    // ── 지은 자리에는 안 난다 · 부수면 다시 난다 (2026-09-28) ─────────────────
    //
    // 사용자: *"광맥은 캐야지 사라지고 그 사라진 상태에서 위에 건물을 설치하면 리스폰이 안되는거고
    // 건물을 부시면 다시 리스폰이 되야해"*.
    //
    // **지은 것은 칸 점유로 묻지 않는다** — 점유는 *길을 막나*의 답이라 창고·침대·밭처럼 안 막는
    // 건물과 **청사진**(짓는 동안 폰이 지나다녀야 한다)이 빠진다. 그 위에 버섯이 솟는다.
    // 발자국 전체(`PlacedBuilding.FootprintAt`)와 바닥(`IsFloored`)을 본다.
    //
    // **부순 것을 알리는 문을 따로 안 둔다.** 철거 · 이전 · 청사진 취소 · 바닥 걷기가 저마다
    // 알려야 하면 그중 하나는 반드시 빠진다. 대신 **막혔던 칸을 적어 두고 가끔 다시 본다** —
    // 칸이 풀렸으면 그 청크를 다시 뿌린다(뿌리기가 결정적이라 같은 자리에 같은 것이 선다).
    // 아직 날 날이 안 된 것은 `StillTaken`이 그대로 거른다.
    static readonly HashSet<Vector2Int> _builtTiles = new();
    static readonly HashSet<Vector2Int> _builtProbe = new();
    static readonly Dictionary<Vector2Int, HashSet<Vector2Int>> _underBuilt = new();
    static readonly List<Vector2Int> _freed = new();

    // 청크 경계 밖에 선 건물도 발자국이 넘어올 수 있다 — 가장 큰 건물보다 넉넉히.
    const float BuiltMargin = 8f;

    // 이 청크에 걸친 지은 칸을 모은다. **뿌리기 한 번에 한 번** — 자리마다 건물 목록을 훑지 않는다.
    static void CollectBuilt(Vector2Int chunk, HashSet<Vector2Int> into)
    {
        into.Clear();
        float s  = TerrainGrid.ChunkSize;
        float x0 = chunk.x * s - BuiltMargin, x1 = (chunk.x + 1) * s + BuiltMargin;
        float z0 = chunk.y * s - BuiltMargin, z1 = (chunk.y + 1) * s + BuiltMargin;

        foreach (var pb in PlacedBuilding.All)
        {
            if (pb == null || pb.Data == null) continue;
            var p = pb.transform.position;
            if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue;
            foreach (var t in PlacedBuilding.FootprintAt(p, pb.Data.Size, pb.transform.rotation)) into.Add(t);
        }

        foreach (var bp in BuildingBlueprint.All)
        {
            if (bp == null) continue;
            var p = bp.transform.position;
            if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue;
            foreach (var t in bp.Area) into.Add(t);
        }
    }

    static bool IsBuilt(MapManager map, HashSet<Vector2Int> built, Vector2Int t) =>
        built.Contains(t) || (map != null && map.IsFloored(t));

    // 이 자리에 지은 것이 있나. 있으면 **그 칸을 적어 둔다** — 부순 뒤에 다시 날 자리다.
    static bool BuiltOn(Vector3 at, Vector2Int foot, Vector2Int chunk)
    {
        var map = MapManager.Instance;
        bool hit = false;

        foreach (var t in ResourceNode.FootprintAt(at, foot))
            if (IsBuilt(map, _builtTiles, t)) { hit = true; break; }
        if (!hit) return false;

        if (!_underBuilt.TryGetValue(chunk, out var tiles)) _underBuilt[chunk] = tiles = new HashSet<Vector2Int>();
        foreach (var t in ResourceNode.FootprintAt(at, foot)) tiles.Add(t);
        return true;
    }

    void RegrowUnbuilt()
    {
        if (_underBuilt.Count == 0) return;
        var map = MapManager.Instance;

        _freed.Clear();
        foreach (var kv in _underBuilt)
        {
            if (!_live.ContainsKey(kv.Key)) continue;
            CollectBuilt(kv.Key, _builtProbe);
            foreach (var t in kv.Value)
                if (!IsBuilt(map, _builtProbe, t)) { _freed.Add(kv.Key); break; }
        }

        // **적어 둔 것을 비우고 다시 뿌린다** — 아직 지어져 있는 칸은 뿌리기가 다시 적는다.
        foreach (var c in _freed)
        {
            _underBuilt.Remove(c);
            if (_live.TryGetValue(c, out var list)) Scatter(c, list, regrowOnly: true);
        }
    }

    void RegrowLive()
    {
        _dueChunks.Clear();
        _dueIds.Clear();

        int today = GameTimeManager.CurrentDay;
        foreach (var kv in _regrowAt)
        {
            if (today < kv.Value.day) continue;
            _dueIds.Add(kv.Key);
            if (_live.ContainsKey(kv.Value.chunk)) _dueChunks.Add(kv.Value.chunk);
            // 안 선 청크는 **세워질 때** `Load`가 알아서 다시 낸다 — 여기서는 목록에서만 뺀다.
        }
        foreach (var id in _dueIds) _regrowAt.Remove(id);

        foreach (var c in _dueChunks)
            if (_live.TryGetValue(c, out var list)) Scatter(c, list, regrowOnly: true);
    }

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
    [HideInInspector] public Vector2Int Chunk;   // 다시 날 때 어느 청크를 훑을지

    // 딴 뒤 며칠 만에 다시 나나(0이면 안 난다). **뿌릴 때 규칙에서 받아 든다** —
    // 파괴되는 시점에 규칙 배열을 되짚으면 그 사이 저작이 바뀌었을 때 엉뚱한 값을 쓴다.
    [HideInInspector] public float RegrowDays;

    // **청크가 내려갈 때는 남기면 안 된다.** 그건 없어진 것이 아니라 잠깐 치운 것이다.
    // 그래서 스트리머가 내릴 때는 이 플래그를 세우고 지운다.
    [HideInInspector] public bool Unloading;

    void OnDestroy()
    {
        WorldContentStreamer.Forget(Id);
        if (!Unloading && Application.isPlaying) WorldContentStreamer.MarkTaken(Id, RegrowDays, Chunk);
    }
}

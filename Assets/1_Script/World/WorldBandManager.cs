using System.Collections.Generic;
using UnityEngine;

// **지도를 살아 있게 만드는 것** — 소굴과 마을이 무리를 내보내고, 그 무리가 실시간으로
// 지도를 가로지른다.
//
// 여기가 답하는 것은 셋뿐이다:
//   1) **어디서 나오는가** — 소굴은 약탈대를, 마을은 상단과 순찰대를 내보낸다.
//      출처가 없으면 무리를 만들지 않는다: 지도 아무 데서나 적이 솟으면 소굴을 칠 이유가 없다.
//   2) **어디로 가는가** — 행동(`WorldBandBehavior`)이 목적지를 정하고, 도착하면 무슨 일이
//      벌어진다. 목적지 없이 도는 점은 장식이지 사건이 아니다.
//   3) **마주치면 어떻게 되는가** — 무리끼리는 여기서 조용히 결판나고,
//      **원정대와 마주친 것은 `WorldMapView`가 메뉴로 받는다**(조작이 개입하는 것은 그쪽 몫).
//
// 토큰도 여기서 그린다. `WorldMapView`가 원정대 토큰을 그리는 것과 같은 방식이지만
// 목록이 다르므로 나눠 뒀다 — 그쪽은 이미 1000줄이고, 무리는 선택·명령의 대상이 아니다.
[DefaultExecutionOrder(-35)]   // WorldMapView(-40)가 grid를 준비한 뒤에 붙는다
public class WorldBandManager : MonoBehaviour
{
    public static WorldBandManager Instance { get; private set; }

    [Header("토큰")]
    [Tooltip("비우면 WorldMapView의 mapRoot 아래에 붙는다.")]
    [SerializeField] Transform bandRoot;
    [SerializeField] float tokenHover = 0.4f;
    [Tooltip("칸 크기 대비 토큰 크기. 원정대 토큰(0.35~)보다 작게 둬야 내 것이 눈에 띈다.")]
    [SerializeField] float tokenScale = 0.28f;

    [Header("조우")]
    [Tooltip("이만큼 가까워지면 맞닿은 것으로 본다(월드유닛). 칸 크기(기본 4)의 절반쯤이 " +
             "적당하다 — 너무 좁으면 옆을 스쳐 지나가도 아무 일이 안 일어난다.")]
    [SerializeField] float contactRadius = 2.4f;
    [Tooltip("조우가 끝난 뒤 다시 붙기까지의 유예(초). 없으면 도망친 다음 프레임에 또 잡힌다.")]
    [SerializeField] float recontactDelay = 8f;
    [Tooltip("적대가 아닌 무리는 원정대가 이 안에 들어오면 **멈춰 기다린다**. " +
             "속도가 비슷하면 쫓아가서 따라잡을 수가 없어, 없으면 상단에게 말을 걸 길이 없다.")]
    [SerializeField] float waitRadius = 7f;

    [Header("한도")]
    [Tooltip("지도에 동시에 서 있을 수 있는 무리 수. 넘으면 새로 안 내보낸다 — " +
             "소굴이 여럿인 지도에서 토큰이 수십 개가 되면 무엇이 위험한지 안 읽힌다.")]
    [SerializeField] int maxBands = 20;

    [Header("포로")]
    [Tooltip("끌려다니는 포로가 **하루에** 스스로 빠져나올 확률. 0이면 영영 못 나온다 — " +
             "되찾을 길이 '그 무리를 이긴다'뿐이면 인원을 다 잃은 뒤엔 막다른 길이 된다.")]
    [Range(0f, 1f)] [SerializeField] float escapeChancePerDay = 0.2f;
    [Tooltip("소굴에 갇힌 뒤의 하루 탈출 확률. 끌려다닐 때보다 낮게 — 소굴로 들어간 뒤에는 " +
             "쳐들어가는 것이 정공법이어야 한다.")]
    [Range(0f, 1f)] [SerializeField] float lairEscapeChancePerDay = 0.08f;

    [Header("소굴")]
    [Tooltip("떠돌이 무리가 근거지에서 벗어나는 최대 거리(칸). 넓을수록 상단·순찰대와 " +
             "마주칠 일이 늘고, 좁으면 소굴 둘레만 맴돌아 지도의 나머지가 조용해진다.")]
    [Min(1)] [SerializeField] int roamRadius = 5;

    [Header("마을")]
    [Tooltip("마을이 상단을 내보내는 간격(일). 하루 길이를 축으로 쓴다.")]
    [Min(0.05f)] [SerializeField] float caravanIntervalDays = 1f;
    [Tooltip("순찰대가 마을에서 벗어나는 최대 거리(칸).")]
    [Min(1)] [SerializeField] int patrolRadius = 3;

    [Header("첫 등장")]
    [Tooltip("소굴·마을이 **처음** 무리를 내보내기까지의 대기(게임 초). 이후는 각자의 간격을 따른다.\n" +
             "간격을 그대로 첫 대기로 쓰면 반나절(게임 900초 = 실시간 15분) 동안 지도에 아무것도 " +
             "안 나와서, 시스템이 있는 줄도 모른 채 '안 나온다'가 된다.")]
    [Min(0f)] [SerializeField] float firstSpawnDelay = 20f;

    // ── 습격 총량 ─────────────────────────────────────────────────────────────
    //
    // **습격이 오는 빈도는 지도 전체가 하나로 정한다.** 예전에는 소굴마다 제 시계를 들고
    // `RaidIntervalDays`마다 따로 계획했다 — 그러면 **소굴이 늘어난 만큼 습격이 곱해진다.**
    // 바이옴마다 소굴 종류를 얹은 순간 초반에 사방에서 동시에 몰려와 맞을 수밖에 없었다.
    //
    // 소굴을 늘리는 것은 **"어디에 무엇이 사는가"를 늘리는 일**이지 "얼마나 자주 맞는가"를
    // 늘리는 일이 아니다. 그래서 시계를 하나로 올리고, 소굴은 **누가 올 차례인가**만 정한다.
    [Header("습격 (전역)")]
    [Tooltip("습격을 **하나** 계획하는 간격(일). 지도에 소굴이 몇이든 총량은 이 값이 정한다.")]
    [Min(0.1f)] [SerializeField] float raidIntervalDays = 3f;

    [Tooltip("첫 습격까지의 대기(일). **초반을 지켜 준다** — 아무것도 못 지은 채로 맞으면 " +
             "대비의 결과가 아니라 그냥 재해다.")]
    [Min(0f)] [SerializeField] float raidFirstDelayDays = 4f;

    [Tooltip("동시에 예고돼 있을 수 있는 습격 수. 넘으면 시계가 돌아도 새로 잡지 않는다 — " +
             "예고가 셋씩 쌓이면 어느 것을 막을지가 계산이 아니라 포기가 된다.")]
    [Min(1)] [SerializeField] int maxPlannedRaids = 2;

    // 다음 습격을 계획할 시각. 음수면 아직 첫 계산 전이다.
    float _nextRaidAt = -1f;

    readonly List<WorldBand> _bands = new();
    readonly Dictionary<WorldBand, GameObject> _tokens = new();
    readonly Dictionary<GameObject, WorldBand> _byGo  = new();   // 레이캐스트 → 무리

    // 마을별 다음 상단 출발 시각(게임 초). 순찰대는 "살아 있나"만 보면 되므로 시각이 필요 없다.
    // 신원을 키로 — 마을을 옮겨도 그 마을의 상단 시계는 그대로다.
    readonly Dictionary<string, float> _nextCaravanAt = new();

    // 자리로 든다 — 칸이 아니라. 색인(`WorldSites`)이 답하므로 격자를 전수 훑지 않는다.
    readonly List<WorldSites.Site> _towns = new();

    WorldMapGrid _grid;
    Transform    _root;
    int          _spawnCounter;

    public IReadOnlyList<WorldBand> Bands => _bands;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameTimeManager.OnDayChanged -= OnDayChanged;
    }

    void Start()
    {
        // **옛 3D 월드맵을 안 찾는다**(2026-08-27). 전에는 `WorldMapView`가 없으면 스스로 꺼졌다 —
        // 그 화면의 진입로를 끊은 뒤로 **무리 체계가 통째로 죽어 있었다.** 이제 지도가 아니라
        // 세계 좌표 위에서 도므로 그 화면이 없어도 돌아야 한다.
        var view = WorldMapView.Instance;
        _grid = view != null ? view.Grid : null;
        _root = bandRoot != null ? bandRoot
              : (view != null && view.MapRoot != null ? view.MapRoot
              : new GameObject("WorldBands").transform);

        // **소굴은 미리 다 깨워 둔다.** 가보지 않은 지역의 소굴도 저 혼자 무리를 내보내야
        // 한다 — 열어 봐야 생기는 것으로 두면 지도의 절반은 영원히 조용하다.
        WorldLairs.BuildAll();
        CollectTowns();
        StaggerFirstSpawns();

        GameTimeManager.OnDayChanged += OnDayChanged;
    }

    // 첫 등장을 짧게, 그리고 **서로 어긋나게**. 전부 같은 순간에 나오면 지도에 무리가
    // 한꺼번에 솟았다가 한동안 아무것도 없는 파도가 생긴다.
    //
    // **여기서 진단도 남긴다.** 지도에 소굴 칸이나 마을이 없으면 코드는 멀쩡히 도는데 화면엔
    // 영영 아무것도 안 나온다 — 그건 "안 나온다"로만 보이므로 원인을 짚을 데가 없다.
    void StaggerFirstSpawns()
    {
        int lairs = 0;
        foreach (var lair in WorldLairs.All)
        {
            lair.NextSortieAt = GameTimeManager.Elapsed + Random.Range(0f, firstSpawnDelay);
            // **습격은 소굴이 안 든다** — 언제 하나 오는지는 전역 시계(`raidIntervalDays`)가
            // 정하고 소굴은 차례만 다툰다. 여기서 소굴마다 시각을 흩뿌리던 것이 곧 습격이
            // 소굴 수만큼 곱해지던 자리였다.
            lairs++;
        }

        float now = GameTimeManager.Elapsed;
        foreach (var town in _towns)
            _nextCaravanAt[town.Id] = now + Random.Range(0f, firstSpawnDelay);

        Debug.Log($"[WorldBandManager] 소굴 {lairs}곳 · 마을 {_towns.Count}곳에서 무리가 나옵니다.");

        if (lairs == 0)
            Debug.LogWarning("[WorldBandManager] 지도에 **소굴 칸이 없거나** 그 바이옴의 " +
                             "`BiomeContentData.Lairs`가 비어 있습니다 — 약탈대가 나올 데가 없습니다. " +
                             "`World Map Editor`로 '적' 칸을 찍고 `Setup World Bands`를 돌리세요.");

        if (_towns.Count == 0)
            Debug.LogWarning("[WorldBandManager] 지도에 **마을 칸이 없어** 상단도 순찰대도 나오지 않습니다. " +
                             "`World Map Editor`로 '마' 칸을 찍으세요.");
        else if (_towns.Count < 2)
            Debug.Log($"[WorldBandManager] 마을이 {_towns.Count}곳뿐이라 상단은 **내 정착지를 오갑니다**. " +
                      "마을을 더 찍으면 마을끼리의 교역로가 생깁니다.");
    }

    void OnDayChanged()
    {
        WorldLairs.RegenAll();
        RollEscapes();
        RefreshBandShops();
    }

    // **상단도 하루가 지나면 물건과 돈이 채워진다.**
    //
    // 필드 상인은 `Merchant.RefreshStock`이, 마을 시장은 `TownMarket.ShopsAt`이 매일 갱신하는데
    // **지도 위의 상단만 아무도 안 불렀다** — 만들 때 한 번 뽑고 끝이었다. 재고만 있을 때는
    // "털면 그만큼 준다"로 읽혀 넘어갔지만, 주머니(`Shop.Gold`)가 생기면서 **한 번 비운 상단은
    // 그 판이 끝날 때까지 빈털터리**가 된다(`MerchantData.Gold` 툴팁의 "하루가 지나면 회복"과
    // 어긋난다).
    void RefreshBandShops()
    {
        foreach (var band in _bands)
            band?.Shop?.Refresh();
    }

    // **붙잡힌 사람은 스스로 빠져나올 수 있다.**
    //
    // 없으면 안 되는 안전장치다: 원정대를 통째로 잃은 뒤 되찾을 길이 "그 무리를 이긴다"뿐이면,
    // 남은 인원이 없어서 못 이기고 사람은 영영 안 돌아온다 — 지는 것이 손실이 아니라 막다른
    // 길이 된다. 하루마다 굴리므로 늦더라도 언젠가는 돌아온다.
    //
    // **소굴에 갇히면 확률이 낮다.** 끌려다니는 동안이 되찾기 제일 쉬울 때고, 소굴로 들어간
    // 뒤에는 쳐들어가는 것이 정공법이어야 한다.
    void RollEscapes()
    {
        for (int i = _bands.Count - 1; i >= 0; i--)
        {
            var band = _bands[i];
            if (band.Captives.Count == 0) continue;
            if (Random.value > escapeChancePerDay) continue;

            var freed = new List<UnitBrain>(band.Captives);
            band.Captives.Clear();
            WorldMapView.Instance?.RestoreParty(freed, band.Position, "탈출",
                $"{freed.Count}명이 {band.Name}에게서 도망쳐 나왔습니다.");
        }

        var grid = WorldMapView.Instance != null ? WorldMapView.Instance.Grid : null;
        foreach (var lair in WorldLairs.All)
        {
            if (lair.Captives.Count == 0) continue;
            if (Random.value > lairEscapeChancePerDay) continue;

            var freed = new List<UnitBrain>(lair.Captives);
            lair.Captives.Clear();
            var at = grid != null ? grid.SurfacePoint(lair.Node) : Vector3.zero;
            WorldMapView.Instance?.RestoreParty(freed, at, "탈출",
                $"{freed.Count}명이 {lair.Name}에서 빠져나왔습니다.");
        }
    }

    void CollectTowns()
    {
        _towns.Clear();
        foreach (var t in WorldSites.Of(WorldNodeContent.Town)) _towns.Add(t);
    }

    // ── 매 프레임 ─────────────────────────────────────────────────────────────
    //
    // **월드맵을 안 보고 있어도 돈다.** 거점 시뮬이 월드맵을 보는 동안 계속 도는 것과 짝이다
    // (2026-07-30 결정) — 원정을 나가지 않았다고 지도가 멈추면, 돌아왔을 때 소굴이 여전히
    // 아무것도 안 하고 있다. 토큰만 반대편 루트가 꺼져 안 보일 뿐이다.

    void Update()
    {
        if (WorldMap.Instance == null) return;

        TrySorties();
        TryTownBands();
        UpdateRaids();
        MoveBands();
        CheckBandContacts();
    }

    // ── 출격 (떠돌이) ─────────────────────────────────────────────────────────

    // **저작한 반지름은 지역 수다** — 이동이 미터라 바꿔 쓴다. 눈금을 상수로 안 박는 이유:
    // 지역 크기를 바꾸면 거리감도 같이 따라와야 한다.
    float RoamMeters   => roamRadius   * TerrainGrid.RegionSize;
    float PatrolMeters => patrolRadius * TerrainGrid.RegionSize;

    // 자리 하나를 지면에 올린다. **지형은 함수다** — 예전에는 옛 지도 격자의 메시를 짚었는데,
    // 그러면 그 화면이 없을 때 무리가 서지를 못한다.
    static Vector3 Surface(Vector2 xz) => new(xz.x, TerrainHeight.At(xz.x, xz.y), xz.y);

    void TrySorties()
    {
        float now = GameTimeManager.Elapsed;

        foreach (var lair in WorldLairs.All)
        {
            if (lair.Entry == null || lair.Strength <= 0 || now < lair.NextSortieAt) continue;

            // 시각은 **한도에 걸려 못 내보냈어도** 미룬다. 안 그러면 한도가 풀리는 순간
            // 밀린 출격이 한꺼번에 터져 나온다.
            lair.NextSortieAt = now + lair.Entry.SortieDays * GameTimeManager.DayDuration;
            if (_bands.Count >= maxBands) continue;

            var band = WorldBand.Roll(lair.Band, lair.Position, lair.Id, NextSeed());
            if (band == null) continue;

            // **소굴 병력만큼만 나간다.** 무리 명세의 정원이 소굴에 남은 것보다 많으면
            // 줄여서 내보낸다 — 안 그러면 병력 1인 소굴이 다섯을 계속 뽑아낸다.
            if (band.Count > lair.Strength) band.TakeLosses(band.Count - lair.Strength);
            if (!band.IsAlive) continue;

            lair.Strength -= band.Count;
            // **0으로 저작되는 사고를 막는다.** 직렬화된 목록을 도구로 늘리면 C# 필드 기본값이
            // 안 들어갈 수 있는데, 0이면 나오자마자 돌아서서 문 앞을 왕복하기만 한다.
            band.ExpiresAt = now + Mathf.Max(0.1f, lair.Entry.RoamDays) * GameTimeManager.DayDuration;
            band.Goal      = Surface(RandomNear(lair.Position, RoamMeters));
            Add(band, Surface(lair.Position));
        }
    }

    // 습격이 노릴 곳 — 가장 가까운 내 정착지나 채집기지.
    // 습격이 노릴 곳 — 가장 가까운 **내 거점이나 채집기지**.
    //
    // 전에는 저작 격자 4,096칸을 전수 훑어 `MySettlement`/`Outpost` 칸을 찾았다. 그것들은
    // **플레이 중에 생기는 것**이라 저작물에 있을 이유가 없었고(`OutpostManager.Build`가
    // 실제로 격자를 고치고 있었다), 이제 각자의 레지스트리가 답한다.
    Vector2? NearestMine(Vector2 from)
    {
        Vector2 best = default;
        float bestSqr = float.MaxValue;

        void Consider(Vector2 p)
        {
            float d = (p - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p; }
        }

        var cores = DungeonCore.All;
        if (cores != null)
            foreach (var c in cores)
                if (c != null) Consider(new Vector2(c.transform.position.x, c.transform.position.z));

        var outposts = OutpostManager.Instance;
        if (outposts != null)
            foreach (var o in outposts.All) Consider(o.Position);

        return bestSqr == float.MaxValue ? (Vector2?)null : best;
    }

    // ── 마을이 내보내는 무리 ──────────────────────────────────────────────────

    void TryTownBands()
    {
        if (_towns.Count == 0) return;
        float now = GameTimeManager.Elapsed;

        foreach (var town in _towns)
        {
            if (_bands.Count >= maxBands) return;

            // 순찰대는 **마을당 하나**를 유지한다. 죽으면 다음 점검에서 새로 선다 —
            // 마을 방비가 영영 사라지면 소굴이 지도를 독차지한다.
            if (!HasBandFrom(town.Id, WorldBandBehavior.Patrol))
                SpawnTownBand(town, WorldBandBehavior.Patrol);

            // 첫 출발 시각은 `StaggerFirstSpawns`가 미리 넣어 둔다. 여기서 늦게 채우면
            // 그 마을만 한 박자 밀린다.
            if (_nextCaravanAt.TryGetValue(town.Id, out float due) && now < due) continue;
            _nextCaravanAt[town.Id] = now + caravanIntervalDays * GameTimeManager.DayDuration;
            SpawnTownBand(town, WorldBandBehavior.Caravan);
        }
    }

    bool HasBandFrom(string homeId, WorldBandBehavior behavior)
    {
        foreach (var b in _bands)
            if (b.HomeId == homeId && b.Behavior == behavior) return true;
        return false;
    }

    void SpawnTownBand(WorldSites.Site town, WorldBandBehavior behavior)
    {
        var data = PickTownBand(town, behavior);
        if (data == null) return;

        var band = WorldBand.Roll(data, town.Position, town.Id, NextSeed());
        if (band == null) return;

        if (behavior == WorldBandBehavior.Caravan)
        {
            var dest = CaravanDestination(town.Id);
            // 갈 데가 없는 상단은 내보내지 않는다 — 제자리를 맴돌 뿐이다.
            if (!dest.HasValue) return;
            band.Goal = Surface(dest.Value);
        }
        else band.Goal = Surface(RandomNear(town.Position, PatrolMeters));

        Add(band, Surface(town.Position));
    }

    // 그 마을이 내보내는 무리 종류. **행동당 하나씩** — `TownMarket`이 상인 종류당 하나를
    // 세우는 것과 같은 규칙이다. 후보가 여럿일 때만 좌표가 고른다.
    static WorldBandData PickTownBand(WorldSites.Site town, WorldBandBehavior behavior)
    {
        var data = BiomeContent.Get(WorldMap.BiomeAtWorld(town.Position));
        if (data == null || data.TownBands == null) return null;

        var pool = new List<WorldBandData>();
        foreach (var b in data.TownBands)
            if (b != null && b.IsUsable && b.Behavior == behavior) pool.Add(b);
        if (pool.Count == 0) return null;

        // **신원을 섬는다** — 좌표를 쓰면 마을을 옮기는 것만으로 나오는 무리가 바뀐다.
        int h = 0;
        if (town.Id != null) foreach (char c in town.Id) h = unchecked(h * 31 + c);
        var rng = new System.Random(unchecked(h * 24571 + (int)behavior * 104729));
        return pool[rng.Next(pool.Count)];
    }

    // 상단이 갈 곳. **다른 마을이 먼저, 없으면 내 정착지.**
    //
    // 마을이 하나뿐인 지도에서 "다른 마을"만 찾으면 상단이 영영 안 나온다 — 실제로 저작된
    // 16×16 지도가 그랬다(마을 1곳). 그렇다고 안 내보내면 지도의 절반이 죽는데, 내 거점은
    // 상단이 갈 만한 곳이다: 사람이 살고 물건이 돈다. **교역로가 저절로 생긴다** —
    // 마을에서 오는 상단을 길에서 만나 사거나, 우리 문 앞까지 오게 두거나.
    Vector2? CaravanDestination(string fromId)
    {
        if (_towns.Count >= 2)
        {
            for (int guard = 0; guard < 8; guard++)
            {
                var pick = _towns[Random.Range(0, _towns.Count)];
                if (pick.Id != fromId) return pick.Position;
            }
        }

        // 마을이 하나뿐이면 **내 거점**이 갈 만한 곳이다 — 사람이 살고 물건이 돈다.
        // 안 내보내면 지도의 절반이 죽는다.
        var cores = DungeonCore.All;
        if (cores != null && cores.Count > 0)
        {
            var c = cores[Random.Range(0, cores.Count)];
            if (c != null) return new Vector2(c.transform.position.x, c.transform.position.z);
        }
        return null;
    }

    // 근방의 다음 지점(월드). **세계 밖으로는 안 나간다.**
    static Vector2 RandomNear(Vector2 center, float radius)
    {
        float w = TerrainGrid.WorldSize;
        for (int guard = 0; guard < 12; guard++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value) * radius;
            var p = new Vector2(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r);
            if (p.x >= 0f && p.y >= 0f && p.x < w && p.y < w) return p;
        }
        return center;
    }

    // ── 이동 ─────────────────────────────────────────────────────────────────

    void MoveBands()
    {
        var view = WorldMapView.Instance;

        for (int i = _bands.Count - 1; i >= 0; i--)
        {
            var band = _bands[i];
            if (!band.IsAlive) { Remove(band); continue; }

            UpdateChase(band, view);
            if (band.Waiting) continue;   // 멈춰 서서 원정대를 기다리는 중

            var to = band.Chasing
                   ? Flat(band.ChaseAt)
                   : Flat(band.Goal);

            var from = Flat(band.Position);
            var next = Vector3.MoveTowards(from, to, band.Data.Speed * Time.deltaTime);

            band.Position = Ground(next, band);
            if (_tokens.TryGetValue(band, out var go) && go != null) go.transform.position = band.Position;

            // 쫓는 중엔 "도착"이 없다 — 상대를 잡거나 놓치는 것뿐이다.
            if (band.Chasing) continue;
            if ((next - to).sqrMagnitude >= 0.04f) continue;

            OnArrived(band);
        }
    }

    // 쫓을 대상 고르기. **원정대가 먼저다** — 남의 싸움보다 내 일이 지도에서 읽혀야 한다.
    void UpdateChase(WorldBand band, WorldMapView view)
    {
        band.Chasing = false;
        band.Waiting = false;

        var myFaction = band.Faction;
        if (myFaction == null) return;

        var playerFaction = Faction.Player != null ? Faction.Player.Data : null;
        // 우호도를 태운다(`StanceBetween`) — 관계를 끌어올린 무리는 쫓아오지 않아야 한다.
        bool hostileToPlayer = playerFaction != null &&
                               (FactionRelations.IsHostileBetween(myFaction, playerFaction)
                             || FactionRelations.IsHostileBetween(playerFaction, myFaction));

        // **적대가 아니면 멈춰 기다린다.** 도망칠 이유가 없는데 계속 걸어가 버리면, 우리와
        // 속도가 비슷한 상단·순찰대는 영영 못 만난다 — 지도에 보이는데 말은 못 거는 그림이 된다.
        if (!hostileToPlayer && playerFaction != null && view != null &&
            view.TryNearestParty(band.Position, waitRadius, out _))
        {
            band.Waiting = true;
            return;
        }

        float r = band.Data.ChaseRadius;
        if (r <= 0f) return;

        if (hostileToPlayer && view != null &&
            view.TryNearestParty(band.Position, r, out var partyPos))
        {
            band.ChaseAt = partyPos;
            band.Chasing = true;
            return;
        }

        float best = r * r;
        foreach (var other in _bands)
        {
            if (other == band || !other.IsAlive) continue;
            if (other.Faction == null || !myFaction.IsHostileTo(other.Faction)) continue;

            float d = (other.Position - band.Position).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            band.ChaseAt = other.Position;
            band.Chasing = true;
        }
    }

    // 목적지에 닿았다 — 행동별로 다른 일이 벌어진다.
    void OnArrived(WorldBand band)
    {
        switch (band.Behavior)
        {
            case WorldBandBehavior.Roam:
                RoamArrived(band);
                break;

            case WorldBandBehavior.Caravan:
                // 다음 마을로 계속 간다. 도착할 때마다 사라졌다 다시 나오면 지도 위의
                // 토큰이 깜빡이기만 하고 "어디서 어디로 오간다"가 안 보인다.
                // **제 근거지에서 출발한 상단이다** — 다음 마을을 신원으로 고른다.
                var next = CaravanDestination(band.HomeId) ?? band.Home;
                band.Goal = Surface(next);
                break;

            case WorldBandBehavior.Patrol:
                band.Goal = Surface(RandomNear(band.Home, PatrolMeters));
                break;
        }
    }

    // ── 떠돌이가 한 바퀴 돌았다 ───────────────────────────────────────────────

    // **도적처럼 근방을 떠돈다.** 목적지에 닿으면 근방의 다음 지점을 고르고, 밖에 있을 기간이
    // 끝나면 근거지로 돌아간다. 돌아가 닿으면 병력이 소굴로 복귀하고 무리는 사라진다 —
    // 그래야 소굴 병력이 순환하고, 소굴을 치면 그 순환이 끊긴다.
    void RoamArrived(WorldBand band)
    {
        if (band.Returning)
        {
            var lair = WorldLairs.At(band.HomeId);
            if (lair != null)
            {
                lair.Strength = Mathf.Min(lair.MaxStrength, lair.Strength + band.Count);

                // **끌고 온 포로는 소굴에 갇힌다.** 여기부터는 지도에서 쫓아가 되찾을 수 없고
                // 소굴을 쳐야 풀려난다 — 늦게 갈수록 되찾기 어려워진다.
                if (band.Captives.Count > 0)
                {
                    lair.Captives.AddRange(band.Captives);
                    EventNotificationUI.Notify("포로",
                        $"붙잡힌 {band.Captives.Count}명이 {lair.Name}에 갇혔습니다.", holdWhenHidden: true);
                    band.Captives.Clear();
                }
            }
            Remove(band);
            return;
        }

        if (GameTimeManager.Elapsed >= band.ExpiresAt)
        {
            band.Returning = true;
            band.Goal      = band.Home;
            return;
        }

        band.Goal = Surface(RandomNear(band.Home, RoamMeters));
    }

    // ── 예고된 습격 ───────────────────────────────────────────────────────────
    //
    // **떠돌이와 다른 축이다.** 떠돌이는 마주쳐야 사건이 되지만 습격은 작정하고 온다 —
    // 대신 **며칠 전에 알려 준다.** 예고가 없으면 대비할 수 없어 그냥 재해가 되고,
    // 예고가 있으면 "그 전에 소굴을 친다"가 선택이 된다.
    //
    // **지도에 토큰으로 걸어오지 않는다.** 토큰으로 만들면 며칠짜리 행군이 화면 위를 기어가고,
    // 그걸 원정대로 가로막을 수 있느냐 없느냐가 또 다른 규칙이 된다. 습격은 사건이지 무리가
    // 아니다 — 막는 길은 하나로 족하다(소굴을 친다).
    class RaidPlan
    {
        public WorldLair  Lair;
        // **월드 좌표다** — 노릴 곳(거점·채집기지)이 격자에서 레지스트리로 내려오면서
        // 칸으로는 가리킬 수가 없어졌다(한 지역에 둘 이상 설 수 있다).
        public Vector2 Target;
        public int        Count;
        public float      ArrivesAt;
        public bool       Reminded;   // 하루 남았을 때 한 번 더 알렸나
    }

    readonly List<RaidPlan> _raids = new();

    // 그 소굴이 계획 중인 습격(없으면 null). 소굴 메뉴가 "며칠 뒤 습격"을 적을 때 묻는다 —
    // 알림은 사라지므로 **어딘가에 계속 남아 있어야** 대비할 수 있다.
    public bool TryGetRaidFrom(Vector2Int lairNode, out int count, out float secondsLeft)
    {
        count = 0;
        secondsLeft = 0f;

        foreach (var r in _raids)
        {
            if (r.Lair == null || r.Lair.Node != lairNode) continue;
            count       = r.Count;
            secondsLeft = Mathf.Max(0f, r.ArrivesAt - GameTimeManager.Elapsed);
            return true;
        }
        return false;
    }

    void UpdateRaids()
    {
        SchedulePlans();

        float now = GameTimeManager.Elapsed;

        for (int i = _raids.Count - 1; i >= 0; i--)
        {
            var plan = _raids[i];

            // **소굴을 치면 습격이 무산된다.** 이게 예고의 요점이다 — 알려 주기만 하고 막을
            // 길이 없으면 예고가 아니라 통보다.
            if (plan.Lair == null || plan.Lair.IsCleared)
            {
                _raids.RemoveAt(i);
                EventNotificationUI.Notify("습격 무산",
                    "소굴이 무너져 예정됐던 습격이 없던 일이 되었습니다.", holdWhenHidden: true);
                continue;
            }

            if (!plan.Reminded && plan.ArrivesAt - now <= GameTimeManager.DayDuration)
            {
                plan.Reminded = true;
                EventNotificationUI.Notify("습격 임박",
                    $"{plan.Lair.Name}의 습격이 하루 앞으로 다가왔습니다.", holdWhenHidden: true);
            }

            if (now < plan.ArrivesAt) continue;

            _raids.RemoveAt(i);
            ExecuteRaid(plan);
        }
    }

    // **시계는 하나, 소굴은 차례를 다툰다.**
    //
    // 예전에는 소굴마다 제 시계를 돌려 각자 습격을 계획했다. 그래서 소굴이 N개면 습격도 N배였고,
    // 바이옴마다 소굴을 얹은 순간 초반에 사방에서 동시에 몰려왔다 — **막을 수 있느냐가 아니라
    // 맞을 수밖에 없느냐**의 문제가 됐다.
    //
    // 이제 전역 시계가 "언제 하나 온다"를 정하고, 소굴은 **누가 올 차례인가**만 겨룬다.
    // 소굴을 늘리는 것은 그래서 **빈도가 아니라 다양성**을 늘린다 — 어디서 오는지, 무엇이
    // 오는지가 달라질 뿐 맞는 횟수는 그대로다.
    void SchedulePlans()
    {
        float now = GameTimeManager.Elapsed;

        // 첫 계산 — **초반을 지켜 준다.** 아무것도 못 지은 채로 맞으면 대비의 결과가 아니라 재해다.
        if (_nextRaidAt < 0f)
        {
            _nextRaidAt = now + raidFirstDelayDays * GameTimeManager.DayDuration;
            return;
        }

        if (now < _nextRaidAt) return;

        // **시각은 뽑든 못 뽑든 미룬다.** 안 그러면 칠 곳이 없거나 예고가 꽉 찬 동안 밀린
        // 습격이 조건이 풀리는 순간 한꺼번에 터진다(`TrySorties`가 한도에서 하는 것과 같은 처리).
        _nextRaidAt = now + raidIntervalDays * GameTimeManager.DayDuration;

        if (_raids.Count >= maxPlannedRaids) return;

        var lair = PickRaidingLair(now);
        if (lair == null) return;

        var target = NearestMine(lair.Position);
        if (target == null) return;   // 칠 곳이 없으면 계획도 없다

        var e = lair.Entry;
        int count = Mathf.Max(1, Mathf.RoundToInt(lair.Strength * Mathf.Clamp01(e.RaidPortion)));
        float warnDays = Mathf.Max(0.1f, e.RaidWarningDays);
        float lead     = warnDays * GameTimeManager.DayDuration;

        _raids.Add(new RaidPlan
        {
            Lair      = lair,
            Target    = target.Value,
            Count     = count,
            ArrivesAt = now + lead,
        });

        int days = Mathf.Max(1, Mathf.CeilToInt(warnDays));
        EventNotificationUI.Notify("습격 예고",
            $"{lair.Name}({lair.Node.x}, {lair.Node.y})에서 {count}명이 " +
            $"{days}일 뒤 ({target.Value.x}, {target.Value.y})를 습격합니다.",
            holdWhenHidden: true);
    }

    // 이번 차례는 어느 소굴인가. **`RaidIntervalDays`가 이제 가중치로 읽힌다** —
    // 값이 작을수록(자주 오려는 소굴일수록) 잘 뽑히고, **0이면 습격을 아예 안 온다**(그대로다).
    //
    // 절대 주기였던 것을 상대 빈도로 바꾼 것뿐이라 저작한 값은 그대로 뜻이 통한다:
    // "3일마다 오려는 소굴"과 "6일마다 오려는 소굴"이 있으면 앞엣것이 두 배로 자주 뽑힌다.
    // 다만 **총량은 전역 시계가 정하므로** 둘 다 있다고 습격이 두 배가 되지는 않는다.
    WorldLair PickRaidingLair(float now)
    {
        float total = 0f;
        foreach (var lair in WorldLairs.All)
            total += RaidWeight(lair);

        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (var lair in WorldLairs.All)
        {
            float w = RaidWeight(lair);
            if (w <= 0f) continue;
            roll -= w;
            if (roll <= 0f) return lair;
        }
        return null;
    }

    float RaidWeight(WorldLair lair)
    {
        var e = lair?.Entry;
        if (e == null || e.RaidIntervalDays <= 0f) return 0f;
        if (lair.Strength <= 0) return 0f;

        // 이미 예고된 소굴은 빼 둔다 — 예고가 둘이면 어느 것이 언제 오는지 셈이 안 된다.
        if (HasPlanFrom(lair)) return 0f;

        // **미워하는 세력이 더 자주 온다**(`FactionRelations.RaidWeight`, −100이면 두 배).
        //
        // 우호도를 떨어뜨리는 데 값이 붙는 자리다 — 죽이고 다니는 것이 화면의 숫자만
        // 움직이면 그 숫자를 볼 이유가 없다. **총량은 안 건드린다**: 여기서 갈리는 건
        // 누가 올 차례인가뿐이고 습격 빈도는 전역 시계가 그대로 정한다(소굴 수만큼
        // 곱해지던 사고를 축만 바꿔 되풀이하지 않으려고).
        return FactionRelations.RaidWeight(lair.Band?.Faction) / e.RaidIntervalDays;
    }

    bool HasPlanFrom(WorldLair lair)
    {
        foreach (var r in _raids) if (r.Lair == lair) return true;
        return false;
    }

    void ExecuteRaid(RaidPlan plan)
    {
        var lair = plan.Lair;

        // **계획한 수가 아니라 지금 남은 병력만큼 온다.** 예고 뒤에 소굴을 두들겨 놨으면
        // 그만큼 적게 와야 한다 — 안 그러면 치나 마나가 된다.
        int count = Mathf.Min(plan.Count, lair.Strength);
        if (count <= 0) return;

        var band = WorldBand.Roll(lair.Band, lair.Node, NextSeed());
        if (band == null) return;

        for (int guard = 0; guard < 40 && band.Count < count; guard++)
        {
            var more = WorldBand.Roll(lair.Band, lair.Node, NextSeed());
            if (more == null) break;
            band.Members.AddRange(more.Members);
        }
        if (band.Count > count) band.TakeLosses(band.Count - count);
        if (!band.IsAlive) return;

        lair.Strength -= band.Count;
        if (lair.IsCleared) WorldLairs.Clear(lair);

        DeliverRaid(band, plan.Target);
    }

    void DeliverRaid(WorldBand band, Vector2 at)
    {
        var active  = SettlementManager.Instance?.Active;
        var outpost = OutpostManager.Instance?.At(at);
        var node    = TerrainGrid.RegionAt(at.x, at.y);

        // ① **지금 보고 있는 거점** — 진짜 적이 필드맵에 선다. 여기가 이 기능의 요점이다:
        //    예고됐던 것이 실제로 문 앞에 나타난다.
        if (active != null && active.WorldNode == node && EventManager.Instance != null &&
            EventManager.Instance.SpawnArrivingBand(band, out var edge))
        {
            string dir  = EventManager.CompassFromCore(edge);
            string line = dir != null ? $"{dir}쪽에 들이닥쳤습니다." : "거점에 들이닥쳤습니다.";
            EventNotificationUI.Notify(band.Name, $"{band.Count}명이 {line}", holdWhenHidden: true);
        }
        // ② **채집기지** — 사람이 살지 않으므로 싸울 것이 없다. 쌓아 둔 것을 털어간다.
        //    "들르지 않아도 캐 준다"의 대가가 여기 있다.
        // **레지스트리가 답한다** — 전에는 저작 격자의 칸 내용물을 봤는데, 채집기지는
        // 플레이 중에 생기는 것이라 거기 있을 이유가 없었다.
        else if (outpost != null)
        {
            RaidOutpost(band, outpost);
        }
        // ③ 지금 안 보는 다른 정착지 — 필드맵이 안 돌고 있으므로 알림만 남긴다.
        //    (오프라인 정착지 피해는 별도 축이라 여기서 지어내지 않는다.)
        else
        {
            EventNotificationUI.Notify(band.Name,
                $"({node.x}, {node.y})의 거점이 습격당했습니다.", holdWhenHidden: true);
        }
    }

    void RaidOutpost(WorldBand band, WorldOutpost outpost)
    {
        var node = outpost.Node;
        if (outpost.Stock.Count == 0)
        {
            EventNotificationUI.Notify(band.Name,
                $"채집기지 ({node.x}, {node.y})가 털렸지만 가져갈 것이 없었습니다.", holdWhenHidden: true);
            return;
        }

        int taken = 0;
        var keys = new List<ItemData>(outpost.Stock.Keys);
        foreach (var item in keys)
        {
            int had  = outpost.Stock[item];
            int take = Mathf.Max(1, Mathf.RoundToInt(had * Random.Range(0.5f, 1f)));
            take = Mathf.Min(take, had);

            outpost.Stock[item] = had - take;
            if (outpost.Stock[item] <= 0) outpost.Stock.Remove(item);
            taken += take;
        }

        EventNotificationUI.Notify(band.Name,
            $"채집기지 ({node.x}, {node.y})에서 {taken}개를 빼앗겼습니다.", holdWhenHidden: true);
    }

    // ── 무리끼리의 조우 ───────────────────────────────────────────────────────

    void CheckBandContacts()
    {
        float r2 = contactRadius * contactRadius;

        for (int i = 0; i < _bands.Count; i++)
        for (int j = i + 1; j < _bands.Count; j++)
        {
            var a = _bands[i];
            var b = _bands[j];
            if (!a.IsAlive || !b.IsAlive) continue;
            if (a.Faction == null || b.Faction == null) continue;

            // **어느 한쪽이라도 적대면 싸운다.** 방향별 관계이므로 한쪽만 적대일 수 있는데,
            // 덤비는 쪽이 있으면 맞는 쪽도 싸우게 된다(반격은 관계와 무관하다는 규약과 같다).
            if (!a.Faction.IsHostileTo(b.Faction) && !b.Faction.IsHostileTo(a.Faction)) continue;
            if ((a.Position - b.Position).sqrMagnitude > r2) continue;

            WorldBattle.ResolveBands(a, b);

            if (!a.IsAlive) Remove(a);
            if (!b.IsAlive) Remove(b);
            return;   // 목록이 바뀌었다 — 나머지는 다음 프레임에 본다
        }
    }

    // ── 원정대와의 조우 (판정은 `WorldMapView`가 한다) ─────────────────────────

    // 그 지점에 맞닿은 무리 하나. 조우 직후의 유예(`NoContactUntil`)를 여기서 걸러 주므로
    // 부르는 쪽은 "가까운가"만 물으면 된다.
    //
    // `accept`는 **관심 있는 무리만 걸러 준다.** 부르는 쪽에서 받아 놓고 버리면 안 된다 —
    // 하나만 돌려주므로, 그냥 지나칠 중립 무리를 집어 버리면 그 뒤에 붙어 있던 적을 놓친다.
    public WorldBand FindContact(Vector3 pos, System.Predicate<WorldBand> accept = null)
    {
        float r2  = contactRadius * contactRadius;
        float now = Time.time;

        foreach (var b in _bands)
        {
            if (!b.IsAlive || now < b.NoContactUntil) continue;
            if ((b.Position - pos).sqrMagnitude > r2) continue;
            if (accept != null && !accept(b)) continue;
            return b;
        }
        return null;
    }

    // 조우가 끝났다 — 잠시 서로를 못 본 것으로 한다.
    // 안 그러면 [도망친다]를 누른 다음 프레임에 같은 무리가 다시 말을 건다.
    // `seconds`를 주면 그만큼(항복처럼 **얻을 것을 다 얻은** 무리는 한동안 우리를 안 노린다).
    public void PushApart(WorldBand band, float seconds = -1f) =>
        band.NoContactUntil = Time.time + (seconds > 0f ? seconds : recontactDelay);

    public void RemoveIfDead(WorldBand band)
    {
        if (band != null && !band.IsAlive) Remove(band);
    }

    // ── 살림살이 ──────────────────────────────────────────────────────────────

    void Add(WorldBand band, Vector3 at)
    {
        band.Position = at;

        var token = BuildToken(band);
        _bands.Add(band);
        _tokens[band] = token;
        _byGo[token]  = band;

        band.Position = Ground(Flat(at), band);
        token.transform.position = band.Position;
    }

    void Remove(WorldBand band)
    {
        // **지키던 자들이 없어지면 포로는 풀린다.** 무리가 저희끼리 싸우다 전멸했을 수도 있고
        // 우리가 친 것일 수도 있는데, 어느 쪽이든 붙잡힌 사람이 같이 증발하면 안 된다.
        if (band.Captives.Count > 0)
        {
            var freed = new List<UnitBrain>(band.Captives);
            band.Captives.Clear();
            WorldMapView.Instance?.RestoreParty(freed, band.Position, "탈출",
                $"{band.Name}이(가) 무너진 틈에 {freed.Count}명이 빠져나왔습니다.");
        }

        _bands.Remove(band);
        if (_tokens.TryGetValue(band, out var go) && go != null)
        {
            _byGo.Remove(go);
            Destroy(go);
        }
        _tokens.Remove(band);
    }

    // 아직 지도 위에 있는 무리인가. 쫓아가던 원정대가 "놓쳤다"를 아는 유일한 길이다 —
    // 돌아가거나 죽은 무리를 계속 겨누면 사라진 자리로 걸어간다.
    public bool Has(WorldBand band) => band != null && _bands.Contains(band);

    // 그 콜라이더가 어느 무리의 토큰인가. `WorldMapView`의 클릭 라우팅이 묻는다.
    public WorldBand BandFor(GameObject go) =>
        go != null && _byGo.TryGetValue(go, out var b) ? b : null;

    GameObject BuildToken(WorldBand band)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Band_{band.Name}";
        go.transform.SetParent(_root, false);
        go.transform.localScale = Vector3.one * TokenSize();

        // **콜라이더는 남기되 트리거로** 둔다 — 원정대 토큰과 같은 이유다.
        //   남기는 이유: 좌클릭으로 무리를 들여다볼 수 있어야 한다. 처음엔 지웠는데,
        //   그러면 지도 위의 무리가 **눌러도 아무 반응이 없는 그림**이 된다.
        //   트리거인 이유: 격자의 표면 레이캐스트는 위에서 아래로 쏘는데 솔리드면 토큰이
        //   제 머리를 맞아 매 프레임 위로 올라가고, `WorldMapView.TryPickTerrain`이
        //   트리거를 건너뛰므로 이동 명령도 막지 않는다.
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var rend = go.GetComponent<Renderer>();
        rend.material = UnlitMat(band.Data.TokenColor);
        return go;
    }

    float TokenSize() => (_grid != null ? _grid.NodeSpacing : 4f) * tokenScale;

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    // **지면은 함수가 답한다**(2026-08-27) — 옛 지도 격자의 메시를 짚으면 그 화면이 없을 때
    // `NullReference`로 무리 이동이 통째로 멎는다. `TerrainHeight`는 로드 여부와 무관하게
    // 답하므로 지도 반대편의 무리도 제 높이를 안다.
    Vector3 Ground(Vector3 worldXZ, WorldBand band)
    {
        float y = TerrainHeight.At(worldXZ.x, worldXZ.z);
        return new Vector3(worldXZ.x, y + tokenHover + TokenSize(), worldXZ.z);
    }

    // 무리마다 다른 시드. 같은 프레임에 둘이 나가도 같은 사람들이 되지 않게.
    int NextSeed() => unchecked(WorldMap.Instance.DepositSeed * 31 + (++_spawnCounter) * 65599
                              + GameTimeManager.CurrentDay * 7919);

    static Material UnlitMat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }
}

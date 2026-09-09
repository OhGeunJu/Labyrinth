using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// 씬에 하나 배치. EventData 목록을 인스펙터에서 연결.
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }
    public static event Action<GameEventData> OnEventTriggered;

    [SerializeField] List<GameEventData> eventPool = new();
    [Tooltip("이벤트 롤 주기 (초). 하루 = 1800초이므로 300이면 하루 6번 굴린다.")]
    [SerializeField] float checkInterval = 300f;

    // **아무 일도 없는 것도 후보다.**
    //
    // 예전엔 `TryRollEvent`가 **반드시 하나를 발동시켰다** — 굴릴 때마다 무슨 일이 났다.
    // 그러면 주기가 곧 사건 빈도라, 조용한 날을 만들려면 주기를 늘리는 수밖에 없고 그건
    // **사건이 언제 올지가 메트로놈처럼 예측 가능해진다**는 뜻이다.
    //
    // 가중치로 두면 둘이 공짜로 풀린다: 빈도를 주기와 따로 조절할 수 있고, `MinGameTime`
    // 게이팅과 저절로 맞물린다 — **초반엔 후보가 적어 조용하고**(가중치 합이 작으니 이쪽이
    // 더 자주 이긴다) 세상이 열릴수록 시끄러워진다.
    //
    // 셈: 하루 굴림 수 = 1800 / `checkInterval`. 사건 확률 = 후보합 / (후보합 + 이 값).
    // 지금 저작으로는 **0.2일 0.6건 → 0.6일 1.6건 → 3일 2.1건 → 7일 이후 2.2건**이다
    // (맨 처음엔 `Ev_Discovery` 하나만 열려 있어 후보합이 3이라 특히 조용하다).
    [Tooltip("**아무 일도 안 일어남**의 가중치. 크면 조용해진다.\n" +
             "사건 확률 = 이벤트 가중치 합 / (합 + 이 값).\n" +
             "지금 저작·주기 300초 기준으로 하루 0.6건(첫날) → 2.2건(7일 이후).")]
    [Min(0f)] [SerializeField] float quietWeight = 26f;
    [Tooltip("지형 가장자리에서 안쪽으로 남길 여백 (m)")]
    [SerializeField] float mapEdgeOffset = 10f;
    [Tooltip("핵에서 바깥으로 훑어 볼 최대 거리 (타일)")]
    [SerializeField] float maxSpawnRadius = 150f;
    [Tooltip("핵에서 이만큼은 떨어뜨린다 — 못 채우면 찾은 칸 중 가장 먼 곳")]
    [SerializeField] float minSpawnDistance = 25f;
    [Tooltip("가장자리를 찾을 때 굴려 볼 방향 수")]
    [SerializeField] int spawnDirections = 16;
    [Tooltip("찾은 가장자리에서 안쪽으로 물러설 칸 수 — 낭떠러지 바로 위에 세우지 않는다")]
    [SerializeField] int spawnEdgeInset = 3;
    [Tooltip("발견한 물건을 떨굴 거리(핵 기준, m)")]
    [SerializeField] float discoveryDropRadius = 8f;

    float _timer;
    float _elapsed;

    void Awake()
    {
        Instance = this;

        // **디버그 값이 조용히 남지 않게.** 씬에 `checkInterval: 10`이 박혀 있었다 —
        // 시험하려고 줄여 둔 것인데, 하루가 1800초라 **하루 180번** 굴리게 된다.
        // 코드의 기본값(300)은 씬이 이미 값을 들고 있으면 아무 힘이 없고, 증상은
        // "사건이 미친 듯이 터진다"로만 나타나 원인을 짚기 어렵다.
        if (checkInterval < 60f)
            Debug.LogWarning($"[EventManager] 이벤트 주기가 {checkInterval}초입니다 — " +
                             $"하루({GameTimeManager.DayDuration:0}초)에 " +
                             $"{GameTimeManager.DayDuration / Mathf.Max(1f, checkInterval):0}번 굴립니다. " +
                             "시험용 값이 남은 것이 아닌지 확인하세요(권장 300).");
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        _timer   += Time.deltaTime;
        if (_timer >= checkInterval)
        {
            _timer = 0f;
            TryRollEvent();
        }

        TickRescue();
    }

    // ── 습격에 딸려 오는 구원 ────────────────────────────────────────────────
    //
    // **지원군이 혼자 굴러서는 도움이 안 된다.** 추첨으로만 오면 조용한 날에 와서 어슬렁거리다
    // 정작 몰려올 때는 없다 — 맞고 있을 때 와야 뜻이 생긴다.
    //
    // 그래서 **습격이 벌어질 때마다 따로 굴린다.** 추첨 풀과 별개 축이라, 같은 에셋이
    // 평시에는 추첨으로도 오고 습격 때는 구원으로도 온다.
    GameEventData _rescue;      // 도착 대기 중인 지원군 (없으면 null)
    float         _rescueAt;    // 도착 시각(`_elapsed` 축)

    void TickRescue()
    {
        if (_rescue == null || _elapsed < _rescueAt) return;

        var data = _rescue;
        _rescue = null;

        // 자리를 못 찾으면 조용히 접는다 — **알림이 먼저 나가면** "지원군이 온다"고 해 놓고
        // 아무도 안 온다(`TryRollEvent`가 지키는 것과 같은 순서).
        // 구원은 **한 무리가 걸어 들어오는 것**이라 갈래가 하나다.
        _edgeBuf.Clear();
        if (TryFindEdgeSpawns(1, _edgeBuf) == 0) return;

        SpawnReinforcement(data, _edgeBuf[0]);
        OnEventTriggered?.Invoke(data);
        NotifyDirection(data, _edgeBuf);
    }

    // 습격이 벌어졌다 — 구원이 올지 굴린다. **먼저 예고하지 않는다**: 올지 모르는 채로
    // 막아야 오는 것이 반가운 일이 되고, 늘 온다면 습격이 무섭지 않다.
    void TryScheduleRescue()
    {
        if (_rescue != null) return;   // 이미 오는 중이면 겹쳐 부르지 않는다

        // 후보는 **추첨 풀에서 찾는다.** 씬에 필드를 하나 더 두면 배선을 빠뜨렸을 때
        // 조용히 아무 일도 안 일어난다 — 이미 풀에 있는 것을 쓰면 그 구멍이 없다.
        var pool = new List<GameEventData>();
        foreach (var e in eventPool)
        {
            if (e == null || !e.RescueOnRaid) continue;
            if (e.EventType != GameEventType.Reinforcement) continue;
            if (e.ReinforcementPrefabs == null || e.ReinforcementPrefabs.Count == 0) continue;
            if (_elapsed < e.MinGameTime) continue;   // 아직 이르면 구원도 없다
            pool.Add(e);
        }
        if (pool.Count == 0) return;

        var data = pool[Random.Range(0, pool.Count)];
        if (Random.value > data.RescueChance) return;

        _rescue   = data;
        _rescueAt = _elapsed + Mathf.Max(0f, data.RescueDelay);
    }

    // **칠 것이 있어야 습격이 온다**(2026-09-08, 사용자: *"던전핵이 없으면 습격을 안 오는 게
    // 맞지 않아?"*). 맞다 — 판이 열릴 때 핵을 안 주기로 했으므로(첫 결정이 *어디에 세울까*다)
    // 그 전에 오는 습격은 **덮칠 것이 없는 습격**이다.
    //
    // 그리고 조용히 이상해진다: 기준점이 없으니 스폰 자리를 빌드존 한복판에서 짐작하게 되어
    // **눈앞에 튀어나오고**, `RaidLord`는 목적지가 스폰 자리와 같아져 **접근도 돌격도 안 뜬다**
    // (사용자가 본 그림이 그것이다).
    //
    // **묻는 곳이 둘이라 여기 한 벌로 둔다** — 추첨 후보에서 빼고(안 빼면 습격이 **굴림을
    // 먹고** 아무 일도 안 일어난다), 발동 문에서도 막는다(도구가 직접 부를 수 있다).
    static bool CanRaid => DungeonCore.Instance != null;

    void TryRollEvent()
    {
        var candidates = new List<GameEventData>();
        var weights    = new List<float>();
        float open  = 0f;   // 때를 안 가린 가중치 합 — 정규화 기준
        float tinted = 0f;  // 때가 흔든 뒤의 합
        foreach (var e in eventPool)
        {
            if (e == null || _elapsed < e.MinGameTime) continue;
            if (e.EventType == GameEventType.Raid && !CanRaid) continue;   // 칠 것이 없으면 안 온다
            float w = WeightOf(e);
            open += e.Weight;
            if (w <= 0f) continue;   // 이 때에는 아예 안 오는 것(밤 배율 0)
            candidates.Add(e);
            weights.Add(w);
            tinted += w;
        }
        if (candidates.Count == 0) return;

        // **때는 총량이 아니라 구성을 가른다** — 밤 배율을 그냥 곱하면 사건이 **더 자주** 나서,
        // 밤을 어둡게 한 값이 난이도 인상으로 새어 나온다(습격이 하루 0.82건에서 1.15건으로
        // 뛰었다). 이 저장소가 우호도에서 이미 내린 판단과 **같은 자리**다: *"습격은 총량을
        // 안 건드린다 — 갈리는 건 누가 올 차례인가뿐"*.
        //
        // 그래서 합을 **때를 안 가린 값으로 되돌린다**. 사건이 나는 빈도는 낮이나 밤이나 같고,
        // 밤에는 **그중 습격의 몫이 커진 만큼 행상인·떠돌이가 밀려난다** — 늘어난 위험의 값을
        // 플레이어가 *다른 것을 못 만나는 것*으로 치른다. 이게 곱하기보다 나은 이유: 밤이
        // 무서운 것은 사건이 잦아서가 아니라 **올 것이 습격뿐**이어서다.
        float scale = tinted > 0f ? open / tinted : 1f;
        float totalWeight = 0f;
        for (int i = 0; i < weights.Count; i++) { weights[i] *= scale; totalWeight += weights[i]; }

        // **조용한 날**(위 `quietWeight` 주석). 후보에 섞어 굴리므로 별도 분기가 없다.
        totalWeight += quietWeight;

        float roll = Random.value * totalWeight;
        if (roll > totalWeight - quietWeight) return;   // 아무 일도 없다

        float acc  = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            acc += weights[i];
            if (roll <= acc) { TriggerEvent(candidates[i]); return; }
        }
        TriggerEvent(candidates[candidates.Count - 1]);
    }

    // **때가 가중치를 흔든다**(`GameEventData.NightWeight`) — 습격은 밤에 잦고 장사꾼은 뜸하다.
    // 흔든 뒤 합은 위에서 되돌리므로 여기 배율은 **서로 겨루는 몫**이지 빈도가 아니다.
    //
    // 밝고 어두움은 `GameTimeManager.IsDay` 하나로 묻는다. 여기서 시각을 다시 재면 화면이
    // 저문 것과 습격이 몰리는 것이 어긋나, 밤을 준비한다는 말이 성립하지 않는다.
    static float WeightOf(GameEventData e) =>
        GameTimeManager.IsDay ? e.Weight : e.Weight * e.NightWeight;

    // ── 밖에서 부르는 문 (디버그) ────────────────────────────────────────────
    //
    // **추첨을 기다리지 않고 지금 이걸 발동시킨다.** 습격 하나를 확인하려면 원래
    // 이벤트가 뽑힐 때까지 기다려야 하고(`Force Roll Event`는 **아무거나** 뽑는다),
    // 습격은 하루 두 번 남짓이라 사실상 못 본다.
    //
    // **`TriggerEvent`를 그대로 부른다** — 자리 찾기·알림·추상 판정·`RaidLord`까지 전부
    // 진짜 습격과 **같은 길**을 지난다. 여기서 지름길을 내면 *도구로는 되는데 게임에서는
    // 안 되는* 것을 만들게 된다.
    public void TriggerNow(GameEventData data)
    {
        if (data != null) TriggerEvent(data);
    }

    // 저작된 이벤트 목록. 도구가 **여기서** 고른다 — 에셋 폴더를 따로 훑으면 씬이
    // 안 들고 있는 이벤트까지 부르게 되어, 실제로는 안 나오는 습격을 시험하게 된다.
    public IReadOnlyList<GameEventData> Pool => eventPool;

    void TriggerEvent(GameEventData data)
    {
        // **설 자리를 먼저 찾고 알린다.** 알림이 앞서면 "고블린이 몰려온다"고 해 놓고
        // 아무도 오지 않는(공허에 떨어져 길을 못 찾는) 일이 생긴다.
        if (data.EventType == GameEventType.Raid && !CanRaid)
        {
            Debug.Log($"[이벤트] '{data.EventName}' — 던전 핵이 없어 건너뜁니다(덮칠 거점이 없다).");
            return;
        }

        var edges = new List<Vector3>();
        bool needsSpawn = data.EventType is GameEventType.Raid
                                         or GameEventType.Merchant
                                         or GameEventType.Reinforcement;
        if (needsSpawn && !HasSpawnContent(data))
        {
            Debug.LogWarning($"[이벤트] '{data.EventName}' — 부를 프리팹이 비어 있어 건너뜁니다.");
            return;
        }

        // **습격만 여러 갈래를 묻는다**(2026-09-09 — 포위). 상인도 지원군도 *한 무리가
        // 걸어 들어오는 것*이라 나눌 뜻이 없다 — 나누면 상인 하나가 두 방향에서 온다.
        // **갈래 수는 `RaidLord`가 답한다**(여기서 또 정하면 규칙이 두 벌이 된다).
        if (needsSpawn && TryFindEdgeSpawns(
                data.EventType == GameEventType.Raid ? RaidLord.ProngsFor(data.EnemyCount) : 1,
                edges) == 0)
        {
            Debug.LogWarning($"[이벤트] '{data.EventName}' — 유닛이 설 수 있는 맵 가장자리를 " +
                             "못 찾아 건너뜁니다(바닥이 너무 좁거나 전부 막혔습니다).");
            return;
        }
        Vector3 edge = edges.Count > 0 ? edges[0] : Vector3.zero;

        OnEventTriggered?.Invoke(data);

        // **실체로 섰나** — 안 보는 곳의 습격은 그 자리에서 추상으로 끝나므로 방향을 알릴
        // 것이 없다(*"서쪽에 나타났습니다"* 해 놓고 아무것도 없으면 찾으러 가게 된다).
        bool stood = true;
        switch (data.EventType)
        {
            case GameEventType.Raid:          stood = SpawnRaid(data, edges); break;
            case GameEventType.Merchant:      SpawnMerchant(data, edge);      break;
            case GameEventType.Reinforcement: SpawnReinforcement(data, edge); break;
            case GameEventType.Discovery:     SpawnDiscovery(data);           break;
        }

        // **어디에 나타났는지 알린다.** 방향이 없으면 "고블린이 몰려온다"를 읽고도 화면을
        // 훑어 찾아야 한다 — 습격은 어느 쪽을 막을지가 곧 대응이다.
        // `Notify`는 같은 창을 다시 쓰므로 위에서 뜬 설명 위에 방향까지 얹힌다
        // (`SpawnDiscovery`가 얻은 물건을 알리는 것과 같은 길).
        if (needsSpawn && stood) NotifyDirection(data, edges);
    }

    void NotifyDirection(GameEventData data, List<Vector3> edges)
    {
        var core = DungeonCore.Instance;
        if (core == null) return;

        // **갈래가 여럿이면 여럿을 말한다**(2026-09-09) — 포위인데 한 방향만 알리면
        // 플레이어는 그쪽만 막고, 나머지 갈래는 **알림이 거짓말을 한 것**이 된다.
        string dir = DirectionsFrom(core.transform.position, edges);
        if (dir == null) return;

        // 상인은 **무엇을 파는지**가 갈 이유를 만든다 — 무기 상인이면 가고 재료 상인이면
        // 만다. 종류 이름은 전부 "…상인"으로 끝나므로 조사는 "이"로 고정이다.
        string line = data.EventType == GameEventType.Merchant && _lastMerchant != null
            ? $"{_lastMerchant.MerchantType.DisplayName()}이 {dir}쪽에 도착했습니다."
            : $"{dir}쪽에 나타났습니다.";

        string desc = string.IsNullOrWhiteSpace(data.Description)
            ? line
            : $"{data.Description}\n{line}";
        // 원정 중이라 필드 UI가 꺼져 있어도 들고 기다린다 — 돌아와서야 알게 되더라도
        // 어느 쪽에서 들어왔는지는 남아야 한다.
        EventNotificationUI.Notify(data.EventName, desc, holdWhenHidden: true);
    }

    static readonly string[] Compass = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };

    // 여러 자리를 하나의 말로 — **같은 방위는 한 번만 적는다**(갈래 둘이 같은 방위로
    // 떨어지면 *"북동·북동쪽"*이 된다). 순서는 나침반 순서로 고정한다 — 뽑힌 순서대로
    // 적으면 같은 습격을 다시 봐도 말이 달라진다.
    static string DirectionsFrom(Vector3 origin, List<Vector3> points)
    {
        if (points == null || points.Count == 0) return null;

        var seen = new List<string>();
        foreach (var name in Compass)
            foreach (var p in points)
                if (DirectionFrom(origin, p) == name) { seen.Add(name); break; }

        return seen.Count == 0 ? null : string.Join("·", seen);
    }

    // 핵에서 본 8방위. +Z가 북, 시계 방향.
    static string DirectionFrom(Vector3 origin, Vector3 point)
    {
        Vector3 d = point - origin;
        d.y = 0f;
        if (d.sqrMagnitude < 1f) return null;   // 핵 위 — 방향이라 할 것이 없다

        float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        return Compass[Mathf.RoundToInt(ang / 45f) % 8];
    }

    // ── 침공 ─────────────────────────────────────────────────────────────────

    // **참을 돌려주면 진짜로 섰다는 뜻이다.** 거짓이면 그 자리에서 추상으로 끝났다 —
    // 부르는 쪽이 방향 알림을 건너뛴다.
    bool SpawnRaid(GameEventData data, List<Vector3> edges)
    {
        if (data.EnemyPrefabs == null || data.EnemyPrefabs.Count == 0) return false;
        if (edges == null || edges.Count == 0) return false;

        var core     = DungeonCore.Instance;
        var used     = new HashSet<Vector2Int>();   // 한 칸에 둘을 낳지 않는다
        var arrivals = new List<GameObject>();

        // **갈래마다 한 무리씩 세운다**(2026-09-09 — 포위).
        //
        // **돌아가며 넣는다** — 앞에서부터 뭉텅이로 자르면 프리팹 목록의 앞쪽이 한 갈래에
        // 몰려 **한 갈래는 전부 궁수, 다른 갈래는 전부 근접**이 된다. 그러면 갈래마다
        // 삼각형을 세워도 뾰족한 쪽에 설 근접이 아예 없는 무리가 생긴다.
        var prongs = new List<RaidLord.Prong>(edges.Count);
        foreach (var e in edges) prongs.Add(new RaidLord.Prong(e));

        for (int i = 0; i < data.EnemyCount; i++)
        {
            var prefab = data.EnemyPrefabs[Random.Range(0, data.EnemyPrefabs.Count)];
            if (prefab == null) continue;

            var prong = prongs[i % prongs.Count];
            var go = SpawnPawn(prefab, ScatterNear(prong.Edge, 3f, used));
            arrivals.Add(go);
            prong.Members.Add(go);
        }

        // **어디로 갈지는 이제 `RaidLord`가 든다**(2026-09-07 — `docs/design_raid_lord.md`).
        // 전에는 여기서 폰마다 핵으로 `SetMoveTarget` 한 번을 걸고 끝이었는데, 그러면 무리의
        // 뜻이 **스폰하는 순간에만** 있어서 습격이 **줄줄이 와서 한 명씩 썰리고**, 한 명 남아도
        // 끝까지 걸어와 죽었다. 명령을 여기 남겨 두면 **Lord와 두 벌**이 된다.

        // **아무도 안 보고 있으면 여기서 끝낸다**(2026-08-31, 사용자 결정: 추상으로 푼다).
        //
        // 카메라가 멀면 `SimLocality.ShouldTick`이 거짓이라 **적의 BT가 안 돈다** — 그런데
        // 내 폰은 `IsPlayer`라 거리를 안 보고 계속 도니, 그대로 두면 문 앞에 선 석상을
        // 우리 폰들만 두들기는 반쪽 시뮬이 된다. 지도에서 걸어온 습격
        // (`WorldBandManager.DeliverRaid`)과 **같은 문**으로 결과를 낸다.
        //
        // **세운 뒤에 묻는 이유는 전력 때문이다** — 스탯은 `Awake`에서 굴려지므로
        // 프리팹만으로는 얼마나 센 무리인지 잴 방법이 없다.
        //
        // **구원군도 시체 일으키기도 안 붙인다** — 둘 다 *현장에서 벌어지는 일*이라
        // 아무도 안 보는 판정에 얹으면 결과에 안 나타나고 알림만 늘어난다.
        var home = core != null ? core.transform.position : edges[0];
        if (!SimLocality.IsSimulated(home))
        {
            var brains = new List<UnitBrain>();
            foreach (var go in arrivals)
            {
                var b = go != null ? go.GetComponent<UnitBrain>() : null;
                if (b != null) brains.Add(b);
            }

            var r = WorldBattle.ResolveRaid(brains, home);
            foreach (var go in arrivals) if (go != null) Destroy(go);

            EventNotificationUI.Notify(data.EventName,
                $"{brains.Count}명이 거점을 덮쳤습니다.\n{r.Summary}", holdWhenHidden: true);
            return false;
        }

        // **치우지 않은 시체가 여기서 값을 치른다** — 교단·망자가 왔으면 맵에 남은 시체가
        // 그 자리에서 일어난다(`Necromancy`). 습격이 가장자리가 아니라 **안쪽에서** 시작된다.
        Necromancy.OnRaidArrived(arrivals);

        // **추상으로 푸는 갈래를 지나온 뒤다** — 아무도 안 보는 습격은 위에서 이미 결과를 내고
        // 폰을 파괴했으므로 지휘할 무리가 없다.
        RaidLords.Lead(prongs, core != null ? core.transform.position : edges[0]);

        TryScheduleRescue();
        return true;
    }

    // ── 지도에서 걸어온 무리 ──────────────────────────────────────────────────

    // **월드맵의 약탈대가 거점에 닿았다** — 여기서 진짜 적이 된다.
    //
    // `SpawnRaid`(이벤트 추첨으로 나오는 습격)와 같은 자리·같은 명령을 쓴다. 다른 것은
    // 하나뿐이다: 무리는 **누가 오는지가 이미 정해져 있다**(`PawnSpec`). 지도에서 두들겨
    // 반쯤 줄여 놓은 무리가 필드에서는 멀쩡한 정원으로 나오면, 지도 위에서 싸운 것이
    // 아무 뜻도 갖지 못한다.
    //
    // 자리를 못 찾으면 **거짓을 돌려준다** — 호출자가 그때는 알림 대신 다른 처리를 한다
    // (알림만 띄우고 아무도 안 오면 "왜 아무 일도 없지"가 된다).
    public bool SpawnArrivingBand(WorldBand band, List<Vector3> edges)
    {
        if (edges == null) return false;
        edges.Clear();
        if (band == null || !band.IsAlive) return false;

        // **지도에서 걸어온 무리도 같다** — 한쪽만 막으면 *핵도 없는데 지도에서만 쳐들어온다*.
        if (!CanRaid) return false;

        // **포위도 양쪽에 다 붙인다**(2026-09-09) — 한쪽만 붙이면 *이벤트 습격만 갈라져
        // 온다*가 되는데, 플레이어에게 그 둘은 똑같이 '적이 몰려온 것'이다(구원·시체
        // 일으키기를 양쪽에 붙인 것과 같은 이유).
        if (TryFindEdgeSpawns(RaidLord.ProngsFor(band.Count), edges) == 0) return false;

        var core     = DungeonCore.Instance;
        var used     = new HashSet<Vector2Int>();
        var arrivals = new List<GameObject>();
        int spawned  = 0;

        var prongs = new List<RaidLord.Prong>(edges.Count);
        foreach (var e in edges) prongs.Add(new RaidLord.Prong(e));

        foreach (var m in band.Members)
        {
            if (m == null || m.Prefab == null) continue;

            var prong = prongs[spawned % prongs.Count];
            var go = SpawnPawn(m.Prefab, ScatterNear(prong.Edge, 3f, used));
            arrivals.Add(go);
            prong.Members.Add(go);
            spawned++;

            // 지도에서 본 그 사람을 그대로 세운다(이름·스탯·특성).
            var brain = go.GetComponent<UnitBrain>();
            if (brain != null && m.Spec != null) brain.ApplySpec(m.Spec);
        }

        // **여기도 `RaidLord`가 든다.** 이벤트로 온 습격에만 붙이면 *지도에서 걸어온 무리만
        // 줄줄이 와서 안 물러난다* — 플레이어에게 그 둘은 똑같이 '적이 몰려온 것'이다
        // (구원과 시체 일으키기를 양쪽에 다 붙인 것과 같은 이유).

        // **한 명도 못 세웠으면 실패다.** 무리 데이터에 프리팹이 안 물려 있으면 여기까지
        // 조용히 통과해서, 부르는 쪽이 "N명이 들이닥쳤습니다"라고 알리고는 **아무도 안 왔다.**
        // 자리를 못 찾았을 때 이미 `false`를 돌려주는 것과 같은 규칙이다
        // (알림이 먼저 나가면 "몰려온다"고 해 놓고 아무 일도 안 일어난다).
        if (spawned == 0)
            Debug.LogWarning($"[Event] {band.Name}: 세울 프리팹이 없어 도착을 건너뜁니다 " +
                             "(WorldBandData.Members의 Prefab을 확인하세요).");

        // **여기도 시체를 일으킨다.** 이벤트 추첨으로 나온 습격에만 걸면 지도에서 걸어온
        // 교단은 시체를 그냥 지나친다 — 플레이어에게 그 둘은 똑같이 '교단이 몰려온 것'이다
        // (구원이 양쪽에 다 붙는 것과 같은 이유).
        if (spawned > 0) Necromancy.OnRaidArrived(arrivals);
        if (spawned > 0) RaidLords.Lead(prongs, core != null ? core.transform.position : edges[0]);

        // **지도에서 걸어온 습격에도 구원이 온다.** 여기를 빠뜨리면 "이벤트로 온 습격에만
        // 용병이 온다"가 되는데, 플레이어에게 그 둘은 똑같이 '적이 몰려온 것'이다.
        if (spawned > 0) TryScheduleRescue();

        return spawned > 0;
    }

    // 핵에서 본 8방위 — 지도에서 온 습격도 어느 쪽에서 들어왔는지 같은 말로 알린다.
    public static string CompassFromCore(Vector3 point)
    {
        var core = DungeonCore.Instance;
        return core != null ? DirectionFrom(core.transform.position, point) : null;
    }

    // **갈래가 여럿이면 여럿을 말한다** — 이벤트 습격이 쓰는 것과 같은 말이라야
    // *"북동·남서"*가 어느 문으로 왔는지에 따라 달라지지 않는다.
    public static string CompassFromCore(List<Vector3> points)
    {
        var core = DungeonCore.Instance;
        return core != null ? DirectionsFrom(core.transform.position, points) : null;
    }

    // ── 지원군 ───────────────────────────────────────────────────────────────

    void SpawnReinforcement(GameEventData data, Vector3 edge)
    {
        if (data.ReinforcementPrefabs == null || data.ReinforcementPrefabs.Count == 0) return;

        var core = DungeonCore.Instance;
        Vector3 rally = core != null ? core.transform.position : edge;
        var used = new HashSet<Vector2Int>();

        for (int i = 0; i < data.ReinforcementCount; i++)
        {
            var prefab = data.ReinforcementPrefabs[Random.Range(0, data.ReinforcementPrefabs.Count)];
            if (prefab == null) continue;

            var go = SpawnPawn(prefab, ScatterNear(edge, 3f, used));

            // 던전 코어 방향으로 초기 이동 (랜덤 오프셋으로 뭉치지 않게)
            var mover = go.GetComponent<PawnMover>();
            if (mover != null) mover.SetMoveTarget(ScatterNear(rally, 5f));
        }
    }

    // ── 상인 ─────────────────────────────────────────────────────────────────

    // 이번에 온 상인이 무엇을 파는가. 알림에 종류를 적으려고 밖에서도 읽는다.
    MerchantData _lastMerchant;

    // 사람과 등짐을 따로 뽑아 여기서 합친다 — 종족 풀 × 상품 풀.
    void SpawnMerchant(GameEventData data, Vector3 edge)
    {
        _lastMerchant = null;

        var prefab = PickOne(data.MerchantPrefabs);
        if (prefab == null) return;

        var go       = SpawnPawn(prefab, edge);
        var merchant = go.GetComponent<Merchant>();

        // **저작한 상인은 건드리지 않는다.** 프리팹에 손으로 상품을 물려 뒀다면 그건
        // "이 사람은 늘 이것만 판다"는 뜻이다 — 이벤트가 덮으면 고정 상인을 세울 길이 없어진다.
        if (merchant != null && merchant.HasAuthoredData)
        {
            _lastMerchant = merchant.Data;
            return;
        }

        // **상품은 그 종족이 취급하는 것 중에서 뽑는다.** 그냥 곱하면 인간이 고블린제 물건을
        // 들고 오는 조합이 나온다(`MerchantData.Races`). 뽑을 것이 없으면 — 이 종족을 받아
        // 주는 상품이 목록에 없으면 — 프리팹이 들고 온 것(역할 기본값)을 그대로 판다.
        var race    = go.GetComponent<UnitBrain>()?.Data;
        var variant = PickVariantFor(data, race);
        if (variant == null) { _lastMerchant = merchant?.Data; return; }

        merchant ??= go.AddComponent<Merchant>();
        merchant.Data = variant;      // setter가 그 상인만의 재고를 새로 뽑는다
        _lastMerchant = variant;
    }

    static MerchantData PickVariantFor(GameEventData data, UnitData race)
    {
        var pool = data.MerchantVariants;
        if (pool == null || pool.Count == 0) return null;

        var eligible = new List<MerchantData>();
        foreach (var m in pool)
            if (m != null && m.SoldBy(race)) eligible.Add(m);

        return PickOne(eligible);
    }

    // 목록에서 하나. 균등 추첨이고, 비운 칸(null)이 섞여 있어도 건너뛴다.
    // 누가 더 자주 오는가는 아직 저작할 축이 아니다 — 필요해지면 가중치를 붙이면 된다.
    static T PickOne<T>(List<T> pool) where T : UnityEngine.Object
    {
        if (pool == null || pool.Count == 0) return null;
        for (int i = 0; i < pool.Count; i++)
        {
            var pick = pool[Random.Range(0, pool.Count)];
            if (pick != null) return pick;
        }
        return null;
    }

    // 부를 프리팹이 아예 없는 이벤트는 발동해 봐야 알림만 뜨고 아무도 안 온다.
    static bool HasSpawnContent(GameEventData data) => data.EventType switch
    {
        GameEventType.Raid          => PickOne(data.EnemyPrefabs)         != null,
        GameEventType.Merchant      => PickOne(data.MerchantPrefabs)      != null,
        GameEventType.Reinforcement => PickOne(data.ReinforcementPrefabs) != null,
        _                           => true,
    };

    // ── 발견 ─────────────────────────────────────────────────────────────────

    // **물건을 줍는다.** 예전엔 거점 근처에 광맥을 세웠는데, 광맥은 자리를 차지하고 영구히
    // 남는 것이라 이벤트가 몇 번 돌면 거점 둘레가 광맥 밭이 되고 건설할 땅이 사라졌다.
    // 발견은 한 번의 사건이지 지형 변화가 아니다.
    //
    // **바닥에 떨군다 — 창고로 순간이동시키지 않는다.** 창고에 바로 넣으면 사건이 아니라
    // 숫자가 조용히 오르는 일이 되고, 알림을 놓치면 무슨 일이 있었는지도 모른다.
    // 떨어뜨려 두면 `DroppedItem`이 운반 일감을 걸어 폰이 가지러 가고, 받을 창고가 없으면
    // 그 자리에 남아 5초마다 다시 시도한다(창고를 비우면 저절로 실려 온다).
    void SpawnDiscovery(GameEventData data)
    {
        var item = PickDiscoveryItem(data);
        if (item == null)
        {
            Debug.LogWarning("[이벤트] 발견할 아이템이 없습니다 — DiscoveryItems를 채우거나 " +
                             "아이템 레지스트리를 확인하세요.");
            return;
        }

        int lo = Mathf.Min(data.DiscoveryMinAmount, data.DiscoveryMaxAmount);
        int hi = Mathf.Max(data.DiscoveryMinAmount, data.DiscoveryMaxAmount);
        int amount = Random.Range(lo, hi + 1);

        var core   = DungeonCore.Instance;
        var origin = core != null ? core.transform.position : Vector3.zero;

        // 거점 둘레에 떨군다 — 가지러 갈 만하되 발밑은 아닌 거리.
        var spot = ScatterNear(origin, discoveryDropRadius);
        DroppedItem.Spawn(item, amount, spot);

        string dir  = DirectionFrom(origin, spot);
        string line = dir != null ? $" {dir}쪽에 떨어져 있습니다." : "";
        EventNotificationUI.Notify("발견", $"{item.ItemName} {amount}개를 찾았습니다.{line}",
                                   holdWhenHidden: true);
    }

    // 후보가 비면 **레지스트리 전체**에서 고른다 — 모드가 넣은 아이템도 딸려 온다.
    // 화폐는 뺀다: 골드가 하늘에서 떨어지면 경제가 무너진다.
    static ItemData PickDiscoveryItem(GameEventData data)
    {
        if (data.DiscoveryItems != null && data.DiscoveryItems.Count > 0)
        {
            var picked = data.DiscoveryItems[Random.Range(0, data.DiscoveryItems.Count)];
            if (picked != null) return picked;
        }

        var pool = new List<ItemData>();
        foreach (var kv in DataManager.AllItems)
        {
            var it = kv.Value;
            if (it == null || it.BaseValue <= 0) continue;
            if (it is ResourceItemData r && r.Group == ResourceGroup.Currency) continue;
            pool.Add(it);
        }
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    // ── 스폰 자리 ────────────────────────────────────────────────────────────

    // **가장자리는 지형이 아니라 맵이 정한다.** 예전엔 `Terrain` 크기로만 가장자리를 잡고
    // 높이도 `Terrain.SampleHeight`만 봐서 `MapManager`의 바닥(`_floored`)·점유를 전혀 몰랐다 —
    // 던전형 맵에서는 공허 한복판에 유닛이 떨어져 길찾기가 아예 안 나왔다(2026-08-06 실측).
    //
    // 그래서 **핵에서 바깥으로 걸어 나가며** 설 수 있는 마지막 칸을 가장자리로 삼는다.
    // 설 수 없는 칸을 만나면 거기서 멈추므로 고른 칸은 **핵과 이어져 있는 것이 보장된다** —
    // 좌표를 굴린 뒤 유효성만 검사하는 방식엔 이 보장이 없다(바닥 밖 섬도 통과한다).
    // **나가는 길도 같은 자를 쓴다.** 풀려난 포로가 맵을 떠날 때(`LeaveMapNode`) 어디가
    // 가장자리인지는 스폰이 쓰던 것과 같은 답이어야 한다 — 답이 두 벌이면 한쪽은 바닥 밖을
    // 가리키고 폰이 낭떠러지로 걸어간다.
    //
    // `from`에서 **핵 반대 방향**으로 걸어 나간다. 그쪽이 그 폰이 이미 밖에 가까운 쪽이다.
    public static bool TryFindExit(Vector3 from, out Vector3 exit)
    {
        exit = from;

        var mgr = Instance;
        var map = MapManager.GetForPosition(from) ?? MapManager.Instance;
        if (mgr == null || map == null) return false;

        var core = DungeonCore.Instance;
        var away = core != null ? from - core.transform.position : Vector3.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
        away.Normalize();

        // 바로 뒤가 막혔을 수 있으니 몇 방향 돌려 본다 — 스폰이 방향을 굴리는 것과 같은 이유.
        for (int i = 0; i < 8; i++)
        {
            var dir = Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * 45f * ((i + 1) / 2), 0f) * away;
            // **끝까지 간다**(`inset: 0`) — 스폰은 낭떠러지 앞에서 물러서야 하지만 나가는
            // 것은 반대다. 세 칸 안쪽(`spawnEdgeInset`)에 멈추면 판이 작을수록 맵 한가운데서
            // 사라지는 꼴이 된다.
            if (!mgr.WalkToEdge(map, from, dir, out var tile, out _, insetOverride: 0)) continue;
            exit = map.TileToGround(tile);
            return true;
        }
        return false;
    }

    readonly List<Vector3> _edgeBuf = new();

    // **밖에서 오는 것이 설 자리 — 답은 여기 한 벌이다**(2026-09-09에 여러 방향을 받도록
    // 넓혔다). 상인도 지원군도 구원도 습격도 같은 문을 지난다: 갈라 두면 *상인이 나오는
    // 자리*와 *습격이 나오는 자리*가 두 벌이 되어, 한쪽만 고치는 날이 반드시 온다.
    //
    // **몇 갈래로 나눠 들어올지는 무리의 크기가 정한다** — 규칙은 `RaidLord.ProngsFor`가
    // 답하고 여기는 자리만 찾는다.
    //
    // **모자라면 있는 만큼만 돌려준다.** 굴이 좁아 갈 수 있는 방위가 하나뿐이면 갈래도
    // 하나다 — 여기서 포기하면 *좁은 굴에서는 습격이 안 온다*가 되는데, 그건 한쪽에서만
    // 오는 것보다 훨씬 나쁘다.
    int TryFindEdgeSpawns(int want, List<Vector3> into)
    {
        into.Clear();
        want = Mathf.Max(1, want);

        var core = DungeonCore.Instance;
        var map  = core != null ? MapManager.GetForPosition(core.transform.position)
                                : MapManager.Instance;
        if (map == null)
        {
            into.Add(TerrainEdgeFallback());   // 맵이 없으면 지형밖에 볼 것이 없다
            return 1;
        }

        // 기준점은 핵. 핵이 없으면 빌드존 한복판에서 나간다.
        Vector3 origin = core != null
            ? core.transform.position
            : map.TileToGround(map.ZoneOriginTile + new Vector2Int(map.Width / 2, map.Height / 2));

        // **굴을 따라 가장 먼 곳을 먼저 본다**(2026-08-30).
        //
        // 아래의 방향 굴리기는 **직선**으로 걸어 가장자리를 찾는다 — 지상에서는 어느 방향이든
        // 뚫려 있어 맞았지만 지하에서는 **모퉁이를 못 돌아** 대부분의 방향이 몇 m 앞에서
        // 끝난다. 그래서 상인이 기지 코앞의 벽에서 나왔다.
        //
        // 폰이 실제로 걷는 격자를 따라 **갈 수 있는 먼 칸**을 찾는다 — 그것이 지하에서의
        // *"맵 가장자리"*다. **못 찾으면 직선 방식으로 떨어진다**(지상 씬·격자 밖·좁은 던전).
        //
        // **가장 먼 칸이 아니라 충분히 먼 칸 중 하나다**(2026-09-08). 가장 먼 곳은 자리가
        // 하나라 **습격이 늘 같은 방향에서 왔다**(사용자: *"적이 항상 북동쪽에서만 나와"*).
        //
        // **여러 갈래를 물으면 서로 먼 방위에서 하나씩 준다**(2026-09-09) — 그것이 포위다.
        var hp = HierarchicalPathfinder.Instance;
        if (hp != null && hp.PickReachableSpread(origin, maxSpawnRadius, minSpawnDistance,
                                                 want, _spreadBuf) > 0)
        {
            var flatOrigin = new Vector3(origin.x, 0f, origin.z);
            foreach (var far in _spreadBuf)
            {
                var t = map.WorldToTile(far);
                if (!map.IsWorkable(t)) continue;
                if (Vector3.Distance(flatOrigin, new Vector3(far.x, 0f, far.z)) < minSpawnDistance)
                    continue;
                into.Add(map.TileToGround(t));
            }
            if (into.Count > 0) return into.Count;
        }

        // 방향을 고르게 나눠 굴린다 — 한쪽이 막혀도 반대쪽엔 길이 있다.
        int tries = Mathf.Max(1, spawnDirections);
        float baseAngle = Random.value * 360f;

        Vector2Int fallback = default;
        bool  hasFallback  = false;
        float bestDist     = -1f;

        for (int i = 0; i < tries; i++)
        {
            float rad = (baseAngle + 360f * i / tries) * Mathf.Deg2Rad;
            var dir   = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            if (!WalkToEdge(map, origin, dir, out var tile, out float dist)) continue;

            if (dist >= minSpawnDistance) { into.Add(map.TileToGround(tile)); return 1; }

            // **가장 멀리 걸은 방향을 남긴다**(2026-08-30). 전에는 *처음* 찾은 것을 붙들었는데,
            // 지하에서는 대부분의 방향이 몇 m 앞에서 바위에 막혀 **상인과 습격이 기지 코앞의
            // 벽에서 튀어나왔다.** 지상에서는 어느 방향이든 가장자리까지 뚫려 있어 티가 안 났다.
            // **길이 이어진 것만 후보다**(`WalkToEdge`가 막히면 멈춘다) — 그래서 가장 먼 것을
            // 골라도 걸어올 수 있는 자리다: 지하에서 가장자리는 **가장 긴 굴의 끝**이다.
            if (dist > bestDist) { bestDist = dist; fallback = tile; hasFallback = true; }
        }

        // 최소 거리를 채우는 방향이 하나도 없으면(좁은 던전·사방이 바위) 가장 먼 칸이라도 쓴다 —
        // 여기서 포기하면 작은 맵에서는 습격이 영영 일어나지 않는다.
        if (hasFallback) { into.Add(map.TileToGround(fallback)); return 1; }
        return 0;
    }

    readonly List<Vector3> _spreadBuf = new();

    // origin에서 dir 방향으로 한 칸씩 나아가며 **설 수 있는 마지막 칸**을 찾는다.
    // 설 수 없는 칸(공허)이나 지형 밖을 만나면 멈춘다. 건물에 점유된 칸은 지나치기만 한다 —
    // 벽 하나가 그 방향을 통째로 지우면 안 되고, 우회는 길찾기 몫이다.
    // `insetOverride`가 음수면 스폰용 기본값(`spawnEdgeInset`)을 쓴다.
    //
    // **들어올 때와 나갈 때의 물러섬이 다르다.** 스폰은 가장자리에서 몇 칸 안쪽에 세워야
    // 한다(아래 주석) — 하지만 **나가는 것은 반대다.** 안쪽에 멈추면 맵 한가운데서 사람이
    // 사라지고, 판이 작을수록 더 이상해진다. 나갈 때는 끝까지 간다(`inset = 0`).
    bool WalkToEdge(MapManager map, Vector3 origin, Vector3 dir, out Vector2Int found, out float dist,
                    int insetOverride = -1)
    {
        found = default;
        dist  = 0f;

        // **가장자리 위가 아니라 그 안쪽에 세운다.** 마지막 칸은 낭떠러지 바로 위다 —
        // 거기에 넷을 흩어 놓으면 서로 비켜서다 한둘이 공허로 밀려난다.
        // (`ExpeditionFormation.approachInset`이 집결 원에서 배운 것과 같다:
        //  판정선 위에 세우면 경계에서 아슬아슬해진다.)
        // 그래서 걸어 나가며 **최근 몇 칸을 기억**했다가 그중 가장 안쪽 것을 쓴다.
        int inset = Mathf.Max(0, insetOverride >= 0 ? insetOverride : spawnEdgeInset);
        var recent = new List<(Vector2Int tile, float dist)>();
        var last   = map.WorldToTile(origin);

        for (float d = 1f; d <= maxSpawnRadius; d += 1f)
        {
            Vector3 point = origin + dir * d;
            if (!InsideTerrain(point)) break;               // 지형 밖 — 여기가 가장자리

            var tile = map.WorldToTile(point);
            if (tile == last) continue;                     // 같은 칸을 두 번 보지 않는다
            last = tile;

            if (!map.IsStandable(tile)) break;              // 공허 — 여기가 가장자리
            if (map.IsOccupied(tile)) continue;             // 건물 — 지나친다

            recent.Add((tile, d));
            if (recent.Count > inset + 1) recent.RemoveAt(0);
        }

        if (recent.Count == 0) return false;

        // 물러설 만큼 못 걸었으면(좁은 바닥) 있는 것 중 가장 안쪽.
        var pick = recent[0];
        found = pick.tile;
        dist  = pick.dist;
        return true;
    }

    // 폰을 표면 **위에** 세워서 찍는다.
    //
    // `ScatterNear`가 주는 것은 `TileToGround` — **바닥 윗면 Y**다. 거기에 그대로 찍으면
    // `CharacterController`(중심 0, 높이 2)의 **아래 절반이 바닥에 파묻힌다.** 유니티가 겹침을
    // 풀면서 옆으로 밀어내는데, 그 자리가 바닥 가장자리면 그대로 밖으로 나가 떨어진다
    // ("바닥보다 살짝 밖에서 나와서 계속 떨어진다", 2026-08-08).
    //
    // 핵이 하수인을 소환할 때 멀쩡했던 건 `GetSpawnPosition`이 **핵의 높이**(부양)를 쓰기 때문이다
    // — 공중에서 떨어져 착지하므로 파묻힐 일이 없었다.
    static GameObject SpawnPawn(GameObject prefab, Vector3 groundPos)
    {
        var go = Instantiate(prefab, groundPos, Quaternion.identity);
        go.transform.position = PawnPlacement.StandingPos(go, groundPos);
        return go;
    }

    const int ScatterTries = 12;

    // 한 점 둘레에 흩되 **설 수 있는 칸으로 스냅**하고, 두 가지를 더 피한다:
    //  ① 이미 이번에 쓴 칸 — 같은 칸에 둘을 낳으면 겹친 채 태어나 서로 밀어내며 갈라지고,
    //     그게 바닥 끝이면 밀려난 쪽이 떨어진다.
    //  ② 이웃이 공허인 칸 — 설 수는 있어도 낭떠러지 끝이라 비켜서는 한 걸음이 곧 추락이다.
    // 예전엔 ±3m를 좌표에 그냥 더해서, 자리를 제대로 골라도 무리의 절반이 공허로 새어 나갔다.
    static Vector3 ScatterNear(Vector3 center, float radius, HashSet<Vector2Int> used = null)
    {
        var map = MapManager.GetForPosition(center);
        if (map == null)
        {
            var p = center + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
            p.y = SampleHeight(p);
            return p;
        }

        Vector2Int fallback = map.WorldToTile(center);
        bool hasFallback = false;

        for (int i = 0; i < ScatterTries; i++)
        {
            var offset = new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
            var tile   = map.FindWorkableNear(map.WorldToTile(center + offset), Mathf.CeilToInt(radius));
            if (!map.IsWorkable(tile)) continue;

            if (!hasFallback) { fallback = tile; hasFallback = true; }

            if (used != null && used.Contains(tile)) continue;
            if (!SurroundedByFloor(map, tile)) continue;

            used?.Add(tile);
            return map.TileToGround(tile);
        }

        // 까다롭게 굴 자리가 없으면 설 수 있기만 하면 된다 — 아예 못 나오는 것보다 낫다.
        used?.Add(fallback);
        return map.TileToGround(fallback);
    }

    // 이웃 네 칸이 다 설 수 있는가 — 낭떠러지 끝 칸을 걸러낸다.
    static bool SurroundedByFloor(MapManager map, Vector2Int t) =>
        map.IsStandable(t + Vector2Int.up)   && map.IsStandable(t + Vector2Int.down) &&
        map.IsStandable(t + Vector2Int.left) && map.IsStandable(t + Vector2Int.right);

    // 지형이 있으면 그 안쪽(여백 `mapEdgeOffset`)인가. 지형이 없는 씬은 항상 참 —
    // 그때는 바닥(`IsStandable`)과 `maxSpawnRadius`가 경계를 대신한다.
    bool InsideTerrain(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        if (t == null) return true;

        var o = t.transform.position;
        var s = t.terrainData.size;
        return p.x >= o.x + mapEdgeOffset && p.x <= o.x + s.x - mapEdgeOffset
            && p.z >= o.z + mapEdgeOffset && p.z <= o.z + s.z - mapEdgeOffset;
    }

    // MapManager가 아예 없을 때만 쓰는 옛 경로.
    Vector3 TerrainEdgeFallback()
    {
        Terrain terrain = Terrain.activeTerrain;
        float halfW, halfH;
        Vector3 origin = Vector3.zero;
        if (terrain != null)
        {
            var size = terrain.terrainData.size;
            halfW  = size.x * 0.5f - mapEdgeOffset;
            halfH  = size.z * 0.5f - mapEdgeOffset;
            origin = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        }
        else
        {
            halfW = halfH = 100f;
        }

        int side = Random.Range(0, 4);
        Vector3 pos = side switch
        {
            0 => origin + new Vector3(Random.Range(-halfW, halfW),  0f,  halfH),
            1 => origin + new Vector3(Random.Range(-halfW, halfW),  0f, -halfH),
            2 => origin + new Vector3( halfW, 0f, Random.Range(-halfH, halfH)),
            _ => origin + new Vector3(-halfW, 0f, Random.Range(-halfH, halfH)),
        };
        pos.y = SampleHeight(pos);
        return pos;
    }

    static float SampleHeight(Vector3 pos)
    {
        Terrain t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(pos) : 0f;
    }

    // 디버그용 — 인스펙터 버튼 대용
    [ContextMenu("Force Roll Event")]
    void ForceRoll() => TryRollEvent();
}

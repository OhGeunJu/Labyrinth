using System.Collections.Generic;
using UnityEngine;

// **짐승과 떠도는 적은 그 지역에 원래 산다 — 청크가 설 때 선다**
// (2026-09-28, 사용자: *"동물 + 적 무리 자연스폰"* → 청크에 뿌리기 · 늑대·곰부터 · 초안은 도구가).
//
// ## 소굴과 무엇이 다른가
//
// 소굴 무리(`WorldBandManager`)는 **어디서 왔나가 있는 사건**이다 — 지도를 걸어 오고, 습격에 오르고,
// 소굴을 치면 끊긴다. 이것은 **그 자리에 사는 것**이다 — 멀리서는 없고(토큰도 없다), 가까이 가면 있다.
// 버섯·나무가 청크를 따라 서는 것(`WorldContentStreamer`)과 같은 모양이고, 다른 점은 **움직이고 죽는다**는 것뿐이다.
//
// ## 규칙
//
// - **무엇이 사는가는 바이옴 표가 답한다**(`BiomeContentData.WildSpawns` — 무리 에셋 · 청크당 확률 · 다시 나는 날수).
//   **청크가 설 때마다 새로 굴린다**(2026-09-29, 사용자: *"매번 똑같은 위치에서 나오는 건 안돼"*) — 떠났다 돌아오면
//   다른 곳에 다른 떼가 있다. 처음엔 청크 좌표 해시로 굴려 늘 같은 굴에 같은 떼가 섰다.
//   **다만 청크가 서 있는 동안은 한 번 굴린 것을 든다**(`_plans`) — 폰 곁이라 미룬 청크는 몇 초마다 다시 보는데,
//   볼 때마다 새로 굴리면 결국 **거의 모든 청크에 떼가 선다**(확률이 곱해진다). 잊는 때는 셋: 청크가 내려갈 때 ·
//   떼를 거둘 때 · 떼가 다 쓰러졌을 때.
// - **눈앞에서 솟지 않는다** — 내 폰에서 `SafeDistance` 안이거나 던전 핵 영역 안이면 세우지 않고, 나중에 다시 본다.
// - **멀어지면 거둔다** — 살아 있는 식구가 전부 카메라와 내 폰 둘 다에서 `DespawnDistance`(250m) 넘게 떨어지면 없앤다(죽인 것이 아니라 잠깐 치운 것).
// - **다 쓰러뜨리면 `RespawnDays` 뒤에 다시 난다.** 시체는 하루 남는다(도축할 수 있다 — 핵 영역 밖 시체는 `Corpse.CheckVanish`가 하루 뒤 없앤다). 붙잡아 세력이 바뀐
//   폰은 이 무리에서 빠진다 — 거둘 때도 안 건드린다.
// - **세우는 길은 무리와 같다**(`WorldBand.Roll` → `PawnSpawn` → `PawnPlacement` → `ApplySpec`) — 여기서 따로
//   찍으면 명세·몸·땅 위에 서는 규칙이 한쪽에만 고쳐진다.
public class WildSpawner : MonoBehaviour
{
    // 내 폰에서 이만큼 안에는 안 세운다(m). 눈앞에서 솟는 것은 스폰이 아니라 사고로 보인다.
    const float SafeDistance = 40f;

    // 청크 안에서 설 자리를 찾는 반경(m) — 세계의 3분의 2가 바위라 찍은 점은 대개 벽 속이다.
    const float FindOpenRadius = 24f;

    const float TickSeconds = 2f;    // 실시간
    const int   SpawnPerTick = 2;    // 무리 하나가 폰 여럿이라 한꺼번에 세우면 프레임이 튄다


    class Group
    {
        public long             Id;
        public Vector2Int       Chunk;
        public FactionData      Faction;
        public float            RespawnDays;
        public string           Name;
        public readonly List<UnitBrain> Pawns = new();

        // **떼로 다닌다**(2026-09-29, 사용자: *"왜 무리로 안돌아다녀?"*) — 식구마다 제 자리를 기준으로 12m씩
        // 서성이면 흩어진다. 무리가 **기준점 하나를 같이 쓰고**(`Anchor`), 그 점이 가끔 태어난 자리(`Home`)
        // 둘레의 새 곳으로 옮겨 가며 식구가 같이 걸어간다. 식구는 그 점 둘레에서만 서성인다(`bb.WanderRadius`).
        public Vector3          Home, Anchor;
        public float            NextMoveAt;   // 게임 초
    }

    const float RoamRadius   = 30f;          // 태어난 자리에서 이만큼 안을 떠돈다(m)
    const float HerdRadius   = 3.5f;         // 기준점 둘레에서 서성이는 반경(m)
    const float MoveEveryMin = 20f, MoveEveryMax = 45f;   // 기준점을 옮기는 간격(게임 초)

    readonly Dictionary<long, Group> _live   = new();
    readonly Dictionary<long, float> _deadTo = new();   // 다 쓰러뜨린 무리 — 다시 날 게임 시각

    // 이번에 청크가 서 있는 동안 굴린 답 — 사는가 · 어디에(위 머리글).
    struct Plan { public Vector2Int Chunk; public bool Present; public Vector3 At; }
    readonly Dictionary<long, Plan> _plans = new();
    static readonly List<long> _forget = new();
    readonly Queue<Vector2Int>       _queue  = new();
    readonly HashSet<Vector2Int>     _queued = new();
    readonly HashSet<Vector2Int>     _liveChunks = new();
    float _next;
    bool  _saidInactive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<WildSpawner>("WildSpawner");

    void OnEnable()
    {
        TerrainStreamer.ChunkLoaded   += OnLoaded;
        TerrainStreamer.ChunkUnloaded += OnUnloaded;
        foreach (var c in TerrainStreamer.LiveChunks) OnLoaded(c);
    }

    void OnDisable()
    {
        TerrainStreamer.ChunkLoaded   -= OnLoaded;
        TerrainStreamer.ChunkUnloaded -= OnUnloaded;
    }

    void OnLoaded(Vector2Int c)
    {
        _liveChunks.Add(c);
        if (_queued.Add(c)) _queue.Enqueue(c);
    }

    void OnUnloaded(Vector2Int c)
    {
        _liveChunks.Remove(c);

        // **서 있는 떼의 계획은 남긴다** — 떼가 아직 가까이 있다(거둘 때 `Reap`이 잊는다).
        _forget.Clear();
        foreach (var kv in _plans)
            if (kv.Value.Chunk == c && !_live.ContainsKey(kv.Key)) _forget.Add(kv.Key);
        foreach (var id in _forget) _plans.Remove(id);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!SimLocality.Active)
        {
            // **아예 안 도는 것도 말한다** — 이어진 세계가 아닌 씬이면 여기서 조용히 멈춘다.
            if (!_saidInactive && Time.unscaledTime > 10f)
            { _saidInactive = true; Debug.Log("[야생] 이어진 세계(`SimLocality`)가 꺼져 있어 저절로 나는 무리를 안 세웁니다."); }
            return;
        }
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + TickSeconds;

        Reap();
        Roam();

        // 서 있는 청크를 **돌아가며** 다시 본다 — 미뤄 둔 것(폰이 가까웠다 · 다시 날 때가 됐다)이 여기서 선다.
        if (_queue.Count == 0)
            foreach (var c in _liveChunks)
                if (_queued.Add(c)) _queue.Enqueue(c);

        int budget = SpawnPerTick;
        int scan   = _queue.Count;
        while (budget > 0 && scan-- > 0 && _queue.Count > 0)
        {
            var c = _queue.Dequeue();
            _queued.Remove(c);
            if (!_liveChunks.Contains(c)) continue;
            budget -= TrySpawnChunk(c);
        }
    }

    // ── 거두기 · 쓰러뜨림 ───────────────────────────────────────────────────

    static readonly List<long>      _gone  = new();
    static readonly List<UnitBrain> _alive = new();

    void Reap()
    {
        _gone.Clear();
        float now = GameTimeManager.Elapsed;

        foreach (var g in _live.Values)
        {
            _alive.Clear();
            foreach (var b in g.Pawns)
                if (Ours(b, g)) _alive.Add(b);

            if (_alive.Count == 0)
            {
                _deadTo[g.Id] = now + g.RespawnDays * GameTimeManager.DayDuration;
                _plans.Remove(g.Id);     // 다시 날 때는 새로 굴린다
                _gone.Add(g.Id);
                continue;
            }

            // **식구가 전부 멀 때만** — 하나라도 가까이 있으면(쫓아오는 중일 수 있다) 둔다.
            //
            // **거리로 잰다 — 청크가 아니다**(2026-09-29, 사용자: *"멀어지는 기준을 좀 크게"*). 청크는 카메라 둘레
            // 5×5라 130~160m에서 떼가 사라졌고, **카메라만 옮겨도** 내 폰과 싸우던 떼가 통째로 없어졌다.
            // 이제 **카메라와 내 폰 둘 다**에서 `DespawnDistance` 넘게 떨어져야 거둔다. 시간으로 거두는 규칙은 두지 않는다.
            bool allAway = true;
            foreach (var b in _alive)
                if (!FarFromEyes(b.transform.position)) { allAway = false; break; }
            if (!allAway) continue;

            foreach (var b in _alive) Destroy(b.gameObject);
            _plans.Remove(g.Id);         // 다시 오면 새로 굴린다 — 같은 자리에 같은 떼가 서지 않게
            _gone.Add(g.Id);
        }

        foreach (var id in _gone) _live.Remove(id);
    }

    // ── 떼로 다니기 ─────────────────────────────────────────────────────────

    void Roam()
    {
        float now = GameTimeManager.Elapsed;
        foreach (var g in _live.Values)
        {
            if (now < g.NextMoveAt) continue;
            g.NextMoveAt = now + Random.Range(MoveEveryMin, MoveEveryMax);

            // **싸우는 중이면 안 옮긴다** — 하나라도 붙어 있으면 무리가 거기 있는 것이다.
            bool fighting = false;
            foreach (var b in g.Pawns)
            {
                if (!Ours(b, g)) continue;
                var c = b.GetComponent<CombatHandler>();
                if (c != null && c.IsInCombat) { fighting = true; break; }
            }
            if (fighting) continue;

            // **걸어서 닿는 곳만 고른다**(2026-09-29) — 둘레의 빈칸을 아무거나 집으면 벽 너머 딴 굴이 걸려,
            // 식구가 벽 앞에서 서거나 광역 길찾기가 *부분 경로* 경고를 찍었다. 태어난 자리에서 굴을 따라
            // 훑어 닿는 칸 중에서 뽑는다(식구는 그 굴에서 태어나 그 굴 길로만 걷는다).
            if (!PickRoamSpot(g, out var at)) continue;

            g.Anchor = at;
            SetAnchor(g, walk: true);
        }
    }

    static readonly List<Vector3> _roamPick = new();

    static bool PickRoamSpot(Group g, out Vector3 at)
    {
        at = default;
        var pf = HierarchicalPathfinder.Instance;
        if (pf == null)
        {
            var r = Random.insideUnitCircle * RoamRadius;
            return RockMap.TryNearestOpen(new Vector3(g.Home.x + r.x, 0f, g.Home.z + r.y), 8f, out at);
        }
        if (pf.PickReachableSpread(g.Home, RoamRadius, RoamRadius * 0.25f, 1, _roamPick) == 0) return false;
        at = _roamPick[0];
        return true;
    }

    // 식구 모두의 기준점을 무리의 것으로 — `walk`면 싸우지 않는 식구를 그리로 걸어가게 한다.
    static void SetAnchor(Group g, bool walk)
    {
        for (int i = 0; i < g.Pawns.Count; i++)
        {
            var b = g.Pawns[i];
            if (!Ours(b, g)) continue;
            var bb = b.Blackboard;
            if (bb == null) continue;

            // 한 점에 모이면 서로 밀어낸다 — 식구마다 기준점 둘레의 제 자리를 준다(원 위로 고르게).
            float ang = i / Mathf.Max(1f, g.Pawns.Count) * Mathf.PI * 2f;
            var spot = g.Anchor + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.2f;

            bb.HomePosition = spot;
            bb.HomeSet      = true;
            bb.WanderRadius = HerdRadius;

            if (!walk || bb.Mover == null || bb.IsKnockedOut) continue;
            var c = b.GetComponent<CombatHandler>();
            if (c != null && c.IsInCombat) continue;
            bb.Mover.SetDestination(spot);
        }
    }

    // 아직 이 무리의 산 식구인가 — 파괴 · 시체 · 갇힘 · 업혀 가는 중 · 세력이 바뀐 것은 빠진다.
    // **갇힌 것을 빼는 것이 요점이다** — 세력은 그대로라 안 빼면, 원정을 나가 기지 청크가 내려가는 순간
    // *멀어졌다*로 읽혀 **우리 속 포로가 지워진다.** 빠진 폰은 거둘 때도 안 건드린다.
    static bool Ours(UnitBrain b, Group g)
    {
        if (b == null) return false;
        if (b.GetComponent<Corpse>() != null) return false;
        var bb = b.Blackboard;
        if (bb != null && bb.IsPrisoner) return false;
        if (PawnCarry.IsCarried(b)) return false;
        var f = b.GetComponent<Faction>();
        return g.Faction == null || (f != null && f.Data == g.Faction);
    }

    // ── 세우기 ──────────────────────────────────────────────────────────────

    // ── 왜 안 섰나 (2026-09-29) ─────────────────────────────────────────────
    //
    // 사용자: *"개미 폰이 근데 저절로 안보여"*. 안 서는 길이 다섯이라(표가 빔 · 확률 · 설 자리 · 내 폰 곁 · 핵 영역)
    // 화면으로는 못 가린다 — 처음 `CensusChunks`개 청크를 **까닭별로 세어 한 줄로** 찍는다(뿌리기 결산과 같은 모양).
    const int CensusChunks = 40;
    int _censusDone;
    int _cNoTable, _cRolledOut, _cNoOpen, _cNearPlayer, _cMade;
    readonly HashSet<Biome> _cEmptyBiomes = new();
    readonly HashSet<Vector2Int> _cSeen = new();

    void CensusTick(Vector2Int chunk)
    {
        if (_censusDone < 0 || !_cSeen.Add(chunk)) return;
        if (++_censusDone < CensusChunks) return;

        string empty = _cEmptyBiomes.Count > 0 ? $" (표가 빈 바이옴: {string.Join(", ", _cEmptyBiomes)})" : "";
        Debug.Log($"[야생] 첫 {CensusChunks}청크 결산 — 표가 빔 {_cNoTable}{empty} · 확률에서 빠짐 {_cRolledOut} · " +
                  $"설 자리 없음 {_cNoOpen} · 내 폰 곁/핵 영역 {_cNearPlayer}(나중에 다시 본다) → **선 무리 {_cMade}**" +
                  (_cMade == 0 && _cNoTable == CensusChunks
                      ? "  ← **표가 전부 비었다** — `World → L`(초안)과 `Pawn → 7`(개미)을 돌렸는지 볼 것." : ""));
        _censusDone = -1;
    }

    // 세운 무리 수를 돌려준다.
    int TrySpawnChunk(Vector2Int chunk)
    {
        float size = TerrainGrid.ChunkSize;
        var   mid  = new Vector2((chunk.x + 0.5f) * size, (chunk.y + 0.5f) * size);
        var   biome = WorldMap.BiomeAtWorld(mid);
        var   list = BiomeContent.WildSpawns(biome);
        bool  first = _censusDone >= 0 && !_cSeen.Contains(chunk);
        if (list.Count == 0)
        {
            if (first) { _cNoTable++; _cEmptyBiomes.Add(biome); }
            CensusTick(chunk);
            return 0;
        }
        int made = TrySpawnEntries(chunk, list, first);
        CensusTick(chunk);
        return made;
    }

    int TrySpawnEntries(Vector2Int chunk, IReadOnlyList<BiomeContentData.WildSpawnEntry> list, bool first)
    {
        float size = TerrainGrid.ChunkSize;

        int made = 0;
        float now = GameTimeManager.Elapsed;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.Band == null || !e.Band.IsUsable) continue;

            long id = Id(chunk, i);
            if (_live.ContainsKey(id)) continue;
            if (_deadTo.TryGetValue(id, out float back)) { if (now < back) continue; _deadTo.Remove(id); }

            // **이 청크에 사는가 · 어디에** — 청크가 설 때 한 번 굴리고, 서 있는 동안 그 답을 든다.
            if (!_plans.TryGetValue(id, out var plan))
            {
                plan = new Plan { Chunk = chunk, Present = Random.value < e.ChancePerChunk };
                if (!plan.Present) { if (first) _cRolledOut++; }
                else
                {
                    var pick = new Vector3(chunk.x * size + Random.value * size, 0f, chunk.y * size + Random.value * size);
                    if (RockMap.TryNearestOpen(pick, FindOpenRadius, out var open)) plan.At = open;
                    else { plan.Present = false; if (first) _cNoOpen++; }
                }
                _plans[id] = plan;
            }
            if (!plan.Present) continue;

            var at = plan.At;
            if (!FarFromPlayer(at)) { if (first) _cNearPlayer++; continue; }   // 나중에 다시 본다(같은 자리로)

            if (Spawn(e, id, chunk, at)) { made++; if (first) _cMade++; }
        }
        return made;
    }

    bool Spawn(BiomeContentData.WildSpawnEntry e, long id, Vector2Int chunk, Vector3 at)
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);   // 설 때마다 다른 개체(이름 · 등급)
        var band = WorldBand.Roll(e.Band, new Vector2(at.x, at.z), null, seed);
        if (band == null) return false;

        var g = new Group { Id = id, Chunk = chunk, Faction = e.Band.Faction,
                            RespawnDays = e.RespawnDays, Name = e.Band.BandName };

        var map = MapManager.Instance;
        for (int i = 0; i < band.Members.Count; i++)
        {
            var m = band.Members[i];
            if (m?.Prefab == null) continue;

            // 한 점에 세우면 서로 밀어내다 흩어진다 — 처음부터 벌려 세운다(`BandMaterializer`와 같은 벌림).
            float ang = i / Mathf.Max(1f, band.Members.Count) * Mathf.PI * 2f;
            float rad = 1.5f + i * 0.4f;
            var p = new Vector3(at.x + Mathf.Cos(ang) * rad, 0f, at.z + Mathf.Sin(ang) * rad);
            if (RockMap.TryNearestOpen(p, FindOpenRadius, out var open)) p = open;
            p.y = map != null ? map.SampleGroundHeight(p.x, p.z) : TerrainHeight.At(p.x, p.z);

            var go = PawnSpawn.Instantiate(m.Prefab, m.Spec);
            PawnPlacement.PlaceOnGround(go, p);

            var brain = go.GetComponent<UnitBrain>();
            if (brain == null) continue;
            if (m.Spec != null) brain.ApplySpec(m.Spec);

            // **떠도는 동안은 길만 따라 걷는다**(`PawnMover.OnRails`) — 상대가 생기면 몸으로 돌아간다.
            var mover = go.GetComponent<PawnMover>();
            if (mover != null) mover.OnRails = true;

            g.Pawns.Add(brain);
        }

        if (g.Pawns.Count == 0) return false;

        // 떼의 기준점 — 태어난 자리에서 시작해 첫 이동까지 조금 쉰다.
        g.Home = g.Anchor = at;
        g.NextMoveAt = GameTimeManager.Elapsed + Random.Range(MoveEveryMin * 0.5f, MoveEveryMax * 0.5f);
        SetAnchor(g, walk: false);

        _live[id] = g;
        Debug.Log($"[야생] {g.Name} {g.Pawns.Count} — 청크 {chunk} ({at.x:F0}, {at.z:F0})");
        return true;
    }

    // 이만큼 떨어지면 거둔다(m) — 청크가 서는 반경(130~160m)보다 넉넉히.
    const float DespawnDistance = 250f;

    // 카메라와 내 폰 **모두**에서 먼가.
    static bool FarFromEyes(Vector3 p)
    {
        const float r2 = DespawnDistance * DespawnDistance;
        var c = SimLocality.Center;
        if (new Vector2(p.x - c.x, p.z - c.z).sqrMagnitude < r2) return false;

        foreach (var b in UnitBrain.All)
        {
            if (b == null) continue;
            var f = b.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;
            var q = b.transform.position;
            if (new Vector2(p.x - q.x, p.z - q.z).sqrMagnitude < r2) return false;
        }
        return true;
    }

    // 내 폰 가까이 · 핵 영역 안에는 안 세운다.
    static bool FarFromPlayer(Vector3 at)
    {
        foreach (var core in DungeonCore.All)
            if (core != null && core.IsWithinAnyRangeWorld(at)) return false;

        foreach (var b in UnitBrain.All)
        {
            if (b == null) continue;
            var f = b.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;
            var p = b.transform.position;
            if (new Vector2(p.x - at.x, p.z - at.z).sqrMagnitude < SafeDistance * SafeDistance) return false;
        }
        return true;
    }

    // ── 셈 ─────────────────────────────────────────────────────────────────

    static long Id(Vector2Int c, int entry) =>
        (((long)c.x & 0xFFFF) << 32) | (((long)c.y & 0xFFFF) << 16) | (uint)(entry & 0xFFFF);
}

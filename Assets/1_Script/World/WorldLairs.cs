using System.Collections.Generic;
using UnityEngine;

// 지도 위의 적 소굴 하나.
//
// **소굴은 칸이지 씬 오브젝트가 아니다** — 마을(`TownMarket`)과 같다. 그래서 병력과 출격
// 시각을 이 객체가 들고, 지도에 서는 것은 `WorldMapGrid`가 그리는 표식뿐이다.
public class WorldLair
{
    public Vector2Int Node;
    public BiomeContentData.LairEntry Entry;

    public int Strength;        // 남은 병력. 무리를 내보내면 줄고, 다 떨어지면 소굴이 사라진다.
    public int MaxStrength;

    public float NextSortieAt;  // 떠돌이 무리를 내보낼 시각. 게임 초(`GameTimeManager.Elapsed` 축).
    // 습격 시각은 **여기 없다.** 소굴마다 제 시계를 들면 습격이 소굴 수만큼 곱해진다 —
    // 언제 하나 오는지는 `WorldBandManager.raidIntervalDays`(전역)가 정하고, 소굴은
    // `RaidIntervalDays`를 가중치로 삼아 **차례만 다툰다**.
    public int   LastRegenDay;

    public WorldBandData Band => Entry != null ? Entry.Band : null;

    public string Name =>
        Entry != null && !string.IsNullOrEmpty(Entry.LairName) ? Entry.LairName
        : (Band != null ? $"{Band.BandName} 소굴" : "소굴");

    // **소굴에 갇힌 포로.** 끌고 다니던 무리가 돌아오면 여기로 넘어온다 — 그때부터는
    // 지도에서 쫓아가 되찾을 수가 없고, **소굴을 쳐야** 풀려난다.
    // 소굴을 치는 이유가 하나 더 생기는 자리다.
    public readonly List<UnitBrain> Captives = new();

    public bool IsCleared => Strength <= 0;

    // 눈으로 재는 위험도. 정확한 숫자를 감추는 것이 목적이 아니라, **정찰하지 않고도**
    // "붙을 만한가"를 대략 읽을 수 있게 하는 것이 목적이다.
    public string ThreatLabel => Strength switch
    {
        <= 0  => "비어 있다",
        <= 5  => "한 줌",
        <= 12 => "제법 모여 있다",
        <= 24 => "많다",
        _     => "떼를 이루고 있다",
    };
}

// 소굴 레지스트리 — **칸 좌표 + 시드로 결정적**.
//
// `TownMarket`·`TownRecruits`·`WorldDeposits`와 같은 축이다: **배치(어디가 소굴인가)는
// 저작물**이고, 그 칸의 세부(누가 사는가, 얼마나 있나)만 좌표에서 굴린다. 소굴 200개를
// 손으로 채우지 않아도 지역마다 다른 것이 나오고, 같은 소굴에 다시 가면 같은 것이 있다.
//
// **날짜를 안 섞는다.** 마을 후보·진열과 달리 소굴의 병력은 그날의 사정이 아니라 **누적된
// 상태**다 — 매일 다시 굴리면 어제 친 소굴이 오늘 멀쩡해진다.
public static class WorldLairs
{
    static readonly Dictionary<Vector2Int, WorldLair> _byNode = new();

    public static IEnumerable<WorldLair> All => _byNode.Values;

    // 그 칸의 소굴. 소굴 칸이 아니거나 바이옴에 소굴 풀이 없으면 null.
    public static WorldLair At(Vector2Int node)
    {
        var wm = WorldMap.Instance;
        if (wm == null) return null;

        var tile = wm.GetTile(node);
        if (tile.Content != WorldNodeContent.EnemyLair) return null;

        if (_byNode.TryGetValue(node, out var lair)) return lair;

        lair = Build(node, tile, wm.DepositSeed);
        if (lair == null) return null;

        _byNode[node] = lair;
        return lair;
    }

    // 씬을 다시 시작해도 static은 살아남는다(도메인 리로드를 끈 경우).
    // 지도를 새로 지으면 옛 좌표의 소굴이 엉뚱한 칸에 붙으므로 비운다.
    public static void Clear() => _byNode.Clear();

    // 지도에 저작된 소굴 칸을 전부 깨워 둔다. **미리 만들어 두는 이유**: 소굴은
    // 플레이어가 들여다볼 때가 아니라 **저 혼자 무리를 내보낼 때** 살아 있어야 한다.
    // 마을 시장처럼 "열어 봐야 생기는" 것으로 두면, 가보지 않은 지역의 소굴은 영영
    // 아무도 안 내보낸다.
    public static void BuildAll()
    {
        var wm = WorldMap.Instance;
        if (wm == null) return;

        for (int x = 0; x < wm.Width;  x++)
        for (int y = 0; y < wm.Height; y++)
            At(new Vector2Int(x, y));
    }

    // 소굴을 없앤다 — 병력이 다 떨어졌거나 습격으로 쓸어버렸을 때.
    // **칸 내용물까지 바꾼다**: 표식만 남으면 "친 소굴"과 "안 친 소굴"이 지도에서 같아 보인다.
    public static void Clear(WorldLair lair)
    {
        if (lair == null) return;

        // **갇혀 있던 사람들을 풀어 준다 — 여기서 한다.** 소굴이 없어지는 길이 여럿이라
        // (병력 고갈 · 습격으로 함락) 호출하는 쪽마다 챙기게 두면 반드시 하나를 빠뜨리고,
        // 그러면 사람이 조용히 증발한다.
        if (lair.Captives.Count > 0)
        {
            var freed = new List<UnitBrain>(lair.Captives);
            lair.Captives.Clear();

            var view = WorldMapView.Instance;
            var at   = view != null && view.Grid != null
                     ? view.Grid.SurfacePoint(lair.Node)
                     : Vector3.zero;
            view?.RestoreParty(freed, at, "석방",
                $"{lair.Name}이(가) 무너지며 갇혀 있던 {freed.Count}명이 풀려났습니다.");
        }

        _byNode.Remove(lair.Node);
        WorldMap.Instance?.SetContent(lair.Node, WorldNodeContent.Empty);
    }

    // 하루가 지나면 조금씩 다시 찬다. **회복은 상한까지만** — 넘어가면 안 친 소굴이 시간이
    // 지날수록 무한히 강해져서, 늦게 가는 것이 곧 손해가 된다(그건 위험도 게이팅이 아니라 시한이다).
    public static void RegenAll()
    {
        int today = GameTimeManager.CurrentDay;
        foreach (var lair in _byNode.Values)
        {
            if (lair.Entry == null || lair.LastRegenDay == today) continue;
            lair.LastRegenDay = today;
            lair.Strength = Mathf.Min(lair.MaxStrength, lair.Strength + lair.Entry.RegenPerDay);
        }
    }

    static WorldLair Build(Vector2Int node, WorldTile tile, int seed)
    {
        var data = BiomeContent.Get(tile.Biome);
        if (data == null || data.Lairs == null || data.Lairs.Count == 0) return null;

        // 쓸 수 없는 항목은 먼저 거른다 — 빈 칸이 뽑히면 "아무도 안 나오는 소굴"이
        // 무작위로 생겨 원인을 짚기 어렵다.
        var pool = new List<BiomeContentData.LairEntry>();
        foreach (var e in data.Lairs)
            if (e != null && e.Band != null && e.Band.IsUsable) pool.Add(e);
        if (pool.Count == 0) return null;

        // `TownMarket`·`TownRecruits`와 **다른 상수**를 쓴다: 같은 열을 쓰면 매장이 두터운
        // 칸에 늘 같은 소굴이 서는 식으로 값들이 붙어 다닌다.
        var rng   = new System.Random(Hash(node, seed));
        var entry = PickWeighted(pool, rng);

        int lo = Mathf.Min(entry.MinStrength, entry.MaxStrength);
        int hi = Mathf.Max(entry.MinStrength, entry.MaxStrength);

        return new WorldLair
        {
            Node        = node,
            Entry       = entry,
            Strength    = lo + rng.Next(hi - lo + 1),
            MaxStrength = hi,
            // **바로 내보낼 수 있는 상태로 태어난다.** 첫 출격을 언제로 미룰지는 여기가 아니라
            // `WorldBandManager.firstSpawnDelay`가 정한다 — 간격(`SortieDays`)을 그대로 첫
            // 대기로 쓰면 지도에 나선 첫 반나절 동안 아무것도 안 나와서 소굴이 있다는 것조차
            // 모른다. 정책은 조절값이 있는 쪽에 둔다.
            NextSortieAt = GameTimeManager.Elapsed,
            LastRegenDay = GameTimeManager.CurrentDay,
        };
    }

    static BiomeContentData.LairEntry PickWeighted(List<BiomeContentData.LairEntry> pool,
                                                   System.Random rng)
    {
        float total = 0f;
        foreach (var e in pool) total += Mathf.Max(0f, e.Weight);
        if (total <= 0f) return pool[rng.Next(pool.Count)];

        double roll = rng.NextDouble() * total;
        float  acc  = 0f;
        foreach (var e in pool)
        {
            acc += Mathf.Max(0f, e.Weight);
            if (roll <= acc) return e;
        }
        return pool[pool.Count - 1];
    }

    static int Hash(Vector2Int node, int seed)
    {
        unchecked
        {
            int h = 67;
            h = h * 31 + node.x * 83492791;
            h = h * 31 + node.y * 19349663;
            h = h * 31 + seed   * 374761393;
            return h;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

// 지도 위의 적 소굴 하나.
//
// **소굴은 칸이지 씬 오브젝트가 아니다** — 마을(`TownMarket`)과 같다. 그래서 병력과 출격
// 시각을 이 객체가 들고, 지도에 서는 것은 지도 창(`WorldMapPanel`)이 그리는 표식뿐이다.
public class WorldLair
{
    // **신원은 저작한 장소의 `Id`다**(2026-08-27). 전에는 칸 좌표가 주소였는데, 그러면
    // **소굴을 200m 옮기는 것만으로 사는 놀이와 병력이 통째로 달라졌다**(좌표를 해시해 굴렸다).
    // 이제 자리를 다듬어도 그 소굴은 그 소굴다.
    public string  Id;
    public Vector2 Position;     // 월드 XZ

    // **칸은 자리에서 나온다 — 따로 들지 않는다.** 둘을 각자 들면 소굴을 옮겼을 때 한쪽만
    // 따라온다. 무리를 내보낼 때 칸으로 굴린다(`WorldBand.Roll`).
    public Vector2Int Node => TerrainGrid.RegionAt(Position.x, Position.y);
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
    static readonly Dictionary<string, WorldLair> _byId = new();

    // **친 소굴은 런타임 상태다.** 전에는 저작 격자를 고쳤는데(`SetContent(Empty)`), 자리가
    // 굽힌 에셋으로 오면서 그럴 수 없게 됐다 — **저작물에 플레이 중의 일을 쓰면 안 된다.**
    // 신원만 기억한다 — 지도도 이걸 보고 친 소굴을 안 그린다.
    static readonly HashSet<string> _gone = new();

    public static IEnumerable<WorldLair> All => _byId.Values;

    public static bool IsGone(string id) => !string.IsNullOrEmpty(id) && _gone.Contains(id);

    // 그 칸의 소굴. 소굴 칸이 아니거나 바이옴에 소굴 풀이 없으면 null.
    // 그 장소의 소굴. 바이옴에 소굴 풀이 없거나 이미 친 소굴이면 null.
    public static WorldLair At(string id)
    {
        if (string.IsNullOrEmpty(id) || _gone.Contains(id)) return null;
        if (_byId.TryGetValue(id, out var lair)) return lair;

        var site = WorldSites.ById(id);
        if (!site.IsValid || site.Kind != WorldNodeContent.EnemyLair) return null;

        lair = Build(site, Seed());
        if (lair == null) return null;

        _byId[id] = lair;
        return lair;
    }

    static int Seed() => WorldMap.Instance != null ? WorldMap.Instance.DepositSeed : 0;

    // 씬을 다시 시작해도 static은 살아남는다(도메인 리로드를 끈 경우).
    // 지도를 새로 지으면 옛 좌표의 소굴이 엉뚱한 칸에 붙으므로 비운다.
    public static void Clear() { _byId.Clear(); _gone.Clear(); }

    // 지도에 저작된 소굴 칸을 전부 깨워 둔다. **미리 만들어 두는 이유**: 소굴은
    // 플레이어가 들여다볼 때가 아니라 **저 혼자 무리를 내보낼 때** 살아 있어야 한다.
    // 마을 시장처럼 "열어 봐야 생기는" 것으로 두면, 가보지 않은 지역의 소굴은 영영
    // 아무도 안 내보낸다.
    public static void BuildAll()
    {
        foreach (var site in WorldSites.Of(WorldNodeContent.EnemyLair)) At(site.Id);
    }

    // 소굴을 없앤다 — 병력이 다 떨어졌거나 습격으로 쓸어버렸을 때.
    // **칸 내용물까지 바꾼다**: 표식만 남으면 "친 소굴"과 "안 친 소굴"이 지도에서 같아 보인다.
    public static void Clear(WorldLair lair)
    {
        if (lair == null) return;

        _byId.Remove(lair.Id);
        _gone.Add(lair.Id);   // 저작물은 그대로 두고 **신원만 기억한다**
    }

    // 하루가 지나면 조금씩 다시 찬다. **회복은 상한까지만** — 넘어가면 안 친 소굴이 시간이
    // 지날수록 무한히 강해져서, 늦게 가는 것이 곧 손해가 된다(그건 위험도 게이팅이 아니라 시한이다).
    public static void RegenAll()
    {
        int today = GameTimeManager.CurrentDay;
        foreach (var lair in _byId.Values)
        {
            if (lair.Entry == null || lair.LastRegenDay == today) continue;
            lair.LastRegenDay = today;
            lair.Strength = Mathf.Min(lair.MaxStrength, lair.Strength + lair.Entry.RegenPerDay);
        }
    }

    static WorldLair Build(WorldSites.Site site, int seed)
    {
        var data = BiomeContent.Get(WorldMap.BiomeAtWorld(site.Position));
        if (data == null || data.Lairs == null || data.Lairs.Count == 0) return null;

        // 쓸 수 없는 항목은 먼저 거른다 — 빈 칸이 뽑히면 "아무도 안 나오는 소굴"이
        // 무작위로 생겨 원인을 짚기 어렵다.
        var pool = new List<BiomeContentData.LairEntry>();
        foreach (var e in data.Lairs)
            if (e != null && e.Band != null && e.Band.IsUsable) pool.Add(e);
        if (pool.Count == 0) return null;

        // `TownMarket`·`TownRecruits`와 **다른 상수**를 쓴다: 같은 열을 쓰면 매장이 두터운
        // 칸에 늘 같은 소굴이 서는 식으로 값들이 붙어 다닌다.
        var rng   = new System.Random(Hash(site.Id, seed));
        var entry = PickWeighted(pool, rng);

        int lo = Mathf.Min(entry.MinStrength, entry.MaxStrength);
        int hi = Mathf.Max(entry.MinStrength, entry.MaxStrength);

        return new WorldLair
        {
            Id          = site.Id,
            Position    = site.Position,
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

    // **신원을 섬는다 — 좌표가 아니라.** 좌표를 쓰면 소굴을 조금 옮긴 것만으로 사는 놀이와
    // 병력이 통째로 달라진다. **`string.GetHashCode`는 실행마다 달라져 못 쓴다** —
    // `PawnSpecData`가 먼저 겪은 함정이다. 직접 섬는다.
    static int Hash(string id, int seed)
    {
        unchecked
        {
            int h = 67;
            if (id != null) foreach (char c in id) h = h * 31 + c;
            h = h * 31 + seed * 374761393;
            return h;
        }
    }
}

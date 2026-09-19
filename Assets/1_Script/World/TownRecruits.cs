using System.Collections.Generic;
using UnityEngine;

// 마을에 서 있는 사람 하나. 고용하면 `Prefab`을 찍어 `Spec`을 입히고 원정대에 넣는다.
//
// **누구인지는 태어나기 전에 다 정해져 있다**(`PawnSpec`) — 메뉴에서 본 이름·등급·몸집 그대로
// 합류한다. 그러지 않으면 보고 고르는 일이 성립하지 않는다.
public class TownRecruit
{
    public GameObject Prefab;
    public PawnSpec   Spec;
    public string     RaceName;
    public int        Cost;
    public bool       Hired;      // 그날 그 마을에서 이미 데려갔다

    public string Name => Spec != null ? Spec.Name : "떠돌이";

    // 목록에 한 줄로 적을 요약. 등급은 대표값 하나만 보인다 —
    // 51개를 늘어놓으면 고르는 게 아니라 읽는 일이 된다.
    // **값은 여기 안 적는다.** `Cost`는 그 사람이 부르는 값이고, 실제로 치를 값은 흥정이
    // 정한다(`Trading.Haggle` — 원정대에서 제일 말 잘하는 사람이 깎는다). 요약에 구워 두면
    // 목록에 뜬 액수와 빠지는 액수가 갈리므로, 값은 그것을 아는 쪽(`WorldMapView`)이 붙인다.
    public string Summary => $"{RaceName} · {Spec?.AverageGrade.ToDisplay() ?? "?"}급";
}

// 마을에서 사람을 구한다 — **칸 좌표 + 날짜**로 결정적.
//
// `TownMarket`과 같은 규칙이다: 마을은 씬 오브젝트가 아니라 칸이므로 NPC를 세워 두지 않고,
// 누가 서 있는지를 좌표·시드·날짜에서 뽑는다. 같은 날 다시 들르면 같은 사람이 있고,
// 하루가 지나면 사람이 바뀐다 — **여닫는다고 새로 뽑히지 않는다**(원하는 사람이 나올 때까지
// 메뉴를 여닫게 되면 고르는 일이 아니라 돌리는 일이 된다).
//
// **이름을 미리 정한다.** `UnitBrain`은 `Awake`에서 `NamePool`에서 아무거나 하나 고르는데,
// 그러면 메뉴에서 본 사람과 합류한 사람의 이름이 다르다. 여기서 정하고 고용할 때 넘긴다.
public static class TownRecruits
{
    // **마을엔 늘 누군가 서 있다.** 사람이 사는 곳이니 "오늘은 아무도 없다"가 기본값이면
    // 마을에 들르는 일 자체가 도박이 된다. 상한은 셋 — 많으면 고르는 게 아니라 목록을 읽는 일이 된다.
    const int MinPerTown = 1;
    const int MaxPerTown = 3;

    static readonly Dictionary<string, List<TownRecruit>> _byId = new();
    static readonly Dictionary<string, int>               _rolledDay = new();

    public static IReadOnlyList<TownRecruit> At(string siteId)
    {
        var site = WorldSites.ById(siteId);
        if (!site.IsValid || site.Kind != WorldNodeContent.Town) return System.Array.Empty<TownRecruit>();

        int today = GameTimeManager.CurrentDay;
        if (_byId.TryGetValue(siteId, out var cached) &&
            _rolledDay.TryGetValue(siteId, out int day) && day == today)
            return cached;

        var rolled = Roll(site, WorldMap.Instance != null ? WorldMap.Instance.DepositSeed : 0, today);
        _byId[siteId]      = rolled;
        _rolledDay[siteId] = today;
        return rolled;
    }

    // **옛 화면용 다리** — `WorldMapView`가 아직 칸으로 묻는다.
    public static IReadOnlyList<TownRecruit> At(Vector2Int node) =>
        At(WorldSites.InRegion(WorldNodeContent.Town, node).Id);

    // 씬을 다시 시작해도 static은 살아남는다(도메인 리로드를 끈 경우).
    // 지도를 새로 지으면 옛 좌표의 사람이 엉뚱한 마을에 서 있게 되므로 비운다.
    public static void Clear()
    {
        _byId.Clear();
        _rolledDay.Clear();
    }

    static List<TownRecruit> Roll(WorldSites.Site site, int seed, int day)
    {
        var result = new List<TownRecruit>();

        var pool = PoolFor(WorldMap.BiomeAtWorld(site.Position));
        if (pool.Count == 0) return result;

        // **날짜를 섞는다** — 안 섞으면 그 마을엔 영원히 같은 사람이 서 있다.
        //
        // 인원은 후보 **종류 수와 무관하다**. 전엔 뽑은 후보를 풀에서 빼서 "같은 종류가 둘"을
        // 막았는데, 그러면 종족이 하나뿐인 지금은 마을마다 영원히 한 명이다. 같은 종족 셋이
        // 서 있는 건 이상하지 않다 — **사람을 가르는 건 종족이 아니라 이름**이다.
        var rng   = new System.Random(Hash(site.Id, seed, day));
        int count = Mathf.Clamp(1 + rng.Next(MaxPerTown), MinPerTown, MaxPerTown);

        var usedNames = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            var picked = PickWeighted(pool, rng);
            if (picked == null) break;

            var unit = UnitOf(picked.Prefab);

            // 이름은 **한 마을 안에서 안 겹치게** 미리 고르고, 그것만 명세에 넘긴다.
            // 명세가 제 마음대로 고르게 두면 같은 이름 둘이 설 수 있다.
            // **개체 시드**는 마을·날짜·자리에서 나온다 → 같은 날 다시 열어도 같은 사람이다.
            // 성별을 먼저 낸다 — 이름이 성별을 따르고, 합류할 때 **그 성별의 몸**이 선다(`PawnSpawn`).
            int     specSeed = Hash(site.Id, seed, day * 97 + i);
            Gender? gender   = PawnSpec.OfGender(unit) ? PawnSpec.GenderFor(specSeed) : (Gender?)null;

            string name = UniqueName(unit, gender, rng, usedNames);
            if (name == null) break;   // 이름 풀이 동난 마을 — 있는 만큼만 세운다

            var spec = PawnSpec.Roll(unit, specSeed,
                                     picked.MinGrade, picked.MaxGrade, new[] { name }, null, gender);

            result.Add(new TownRecruit
            {
                Prefab   = picked.Prefab,
                Spec     = spec,
                RaceName = unit != null && !string.IsNullOrEmpty(unit.RaceName) ? unit.RaceName : "떠돌이",
                Cost     = spec.Price,
            });
        }
        return result;
    }

    // 그 바이옴에서 구할 수 있는 사람들.
    //
    // **비어 있으면 다른 바이옴 것이라도 쓴다.** 마을은 사람이 사는 곳인데 바이옴 하나를
    // 저작하다 빠뜨렸다고 그 지역 마을이 통째로 무인 마을이 되면, 원인을 짚기 전에는
    // "이 마을은 원래 그런가" 싶어진다. 대신 **경고를 한 번 남겨** 빠진 것을 알 수 있게 한다.
    static readonly HashSet<Biome> _warned = new();

    static List<BiomeContentData.RecruitEntry> PoolFor(Biome biome)
    {
        var pool = Collect(BiomeContent.Get(biome));
        if (pool.Count > 0) return pool;

        foreach (var data in BiomeContent.All)
        {
            pool = Collect(data);
            if (pool.Count == 0) continue;

            if (_warned.Add(biome))
                Debug.LogWarning($"[TownRecruits] '{biome}' 바이옴에 인력 풀이 없어 " +
                                 $"'{data.Biome}' 것을 빌려 씁니다 — " +
                                 "`DungeonColony → Setup Town Recruits`로 채우세요.");
            return pool;
        }
        return pool;
    }

    // 못 쓰는 후보를 거른다 — 빈 줄이 뽑히면 "고용할 수 없는 사람"이 서 있게 된다.
    static List<BiomeContentData.RecruitEntry> Collect(BiomeContentData data)
    {
        var pool = new List<BiomeContentData.RecruitEntry>();
        if (data == null || data.TownRecruits == null) return pool;

        foreach (var e in data.TownRecruits)
            if (e != null && e.Prefab != null) pool.Add(e);
        return pool;
    }

    static BiomeContentData.RecruitEntry PickWeighted(List<BiomeContentData.RecruitEntry> pool,
                                                      System.Random rng)
    {
        float total = 0f;
        foreach (var e in pool) total += Mathf.Max(0f, e.Weight);

        // 가중치를 전부 0으로 적어 둔 경우 — 균등하게 하나 고른다(아무도 안 뽑히는 것보다 낫다).
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

    // 프리팹에 배선된 종족 데이터. 프리팹 에셋에서도 읽힌다(직렬화된 값이라).
    static UnitData UnitOf(GameObject prefab)
    {
        var brain = prefab != null ? prefab.GetComponent<UnitBrain>() : null;
        return brain != null ? brain.Data : null;
    }

    // 한 마을 안에서 이름이 겹치지 않게 고른다 — **사람을 가르는 건 이름**이라,
    // 같은 이름 둘이 서 있으면 누구를 고용했는지 알 수 없다.
    // 이름 풀이 동나면 null(있는 만큼만 세운다).
    static string UniqueName(UnitData unit, Gender? gender, System.Random rng, HashSet<string> used)
    {
        var names = unit != null ? unit.NamesFor(gender) : null;
        if (names == null || names.Count == 0)
        {
            string fallback = unit != null && !string.IsNullOrEmpty(unit.RaceName)
                            ? unit.RaceName : "떠돌이";
            return used.Add(fallback) ? fallback : null;
        }

        // 풀이 작을 때 무한정 굴리지 않는다 — 한 바퀴 훑고도 없으면 정말 동난 것이다.
        int start = rng.Next(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            string n = names[(start + i) % names.Count];
            if (used.Add(n)) return n;
        }
        return null;
    }

    // 좌표·시드·날짜를 섞어 안정적인 정수 하나로. unchecked — 넘침은 해시에서 정상이다.
    // `TownMarket`·`WorldDeposits`와 다른 상수를 쓴다: 같은 열을 쓰면 매장이 두터운 칸에 늘
    // 같은 사람이 서는 식으로 값들이 붙어 다닌다.
    // **신원을 섬는다 — 좌표가 아니라**(2026-08-27). 자리를 다듬었다고 마을의 사람이
    // 통째로 바뀌면, 저작하는 손이 결과를 예측할 수 없게 된다.
    static int Hash(string id, int seed, int day)
    {
        unchecked
        {
            int h = 41;
            if (id != null) foreach (char c in id) h = h * 31 + c * 6151;
            h = h * 31 + seed   * 1572869;
            h = h * 31 + day    * 12289;
            return h;
        }
    }
}

using System.Collections.Generic;

// **열정 — 무엇을 좋아하나가 무엇이 빨리 느나를 정한다**(2026-09-20, 사용자: *"림월드처럼 열정을 추가해서 스탯
// 렙업을 더 빨리 할 수 있게"*).
//
// ## 열정은 **스탯 하나하나**에 붙고, **두 풀에서 따로** 뽑는다
// 처음엔 스탯을 열두 갈래로 묶어 갈래에 붙였는데, 사용자가 되돌렸다: *"근접이랑 마법 이런 것들은 다 따로 열정을
// 굴리면 좋겠고 무기랑 마법류에서만 따로 열정 또 다른 노동류끼리 열정을 뽑아줘"*. 그래서
// - **무기·마법 풀** — 무기 열한(검·도끼·둔기·장병기·활·석궁·투창·방패) + 마법 일곱. 큰 0~2 · 작은 1~4.
// - **노동 풀** — 작업(채굴·재배·벌목·조련·의술·연구·사교) + 제작(요리·무기/갑주 제작·연금·제련·석공·목공·보석·노동). 큰 0~2 · 작은 2~4.
// 풀을 가르는 이유는 **한쪽이 다른 쪽을 굶기지 않게**다 — 한 풀에서 뽑으면 어떤 사람은 열정이 전부 무기에 붙어
// 일꾼으로서 좋아하는 것이 하나도 없다.
// - **열정이 안 붙는 것** — 속성 넷(근력·강인함·손재주·인지력: 타고난 몸이다)과 공통 전투(근접공격·방어·정밀사격·
//   격투·회피·운동·수영: 무엇을 들든 쓰는 솜씨라 *좋아하는 무기*와 다른 물음이다).
//
// ## 배율은 **올리기만 한다** — 없음 1배 · 작은 1.5배 · 큰 2배
// 림월드는 열정 없는 기술을 0.35배로 누르지만 그러면 나머지가 거의 안 자란다(사용자: *"0.35배면 렙업이 안될 거 같은데"*).
//
// ## 어기면 조용히 깨지는 것
// - **풀 표는 여기 하나다**(`PoolOf`) — 성장(`UnitStats.GrowStat`)·굴림·스탯 창이 전부 이것을 읽는다.
// - **새 스탯을 만들면 여기에 적을 것** — 안 적으면 조용히 *열정이 안 붙는 스탯*이 된다.
// - **값은 꼬리에 붙인다**(정수 직렬화 — `Passion`·`PassionPool`).
public enum Passion { None, Minor, Major }
public enum PassionPool { CombatArts, Labor }

public static class Passions
{
    static readonly StatName[] CombatArts =
    {
        StatName.OneHandSword, StatName.OneHandAxe, StatName.OneHandMace,
        StatName.TwoHandSword, StatName.TwoHandAxe, StatName.TwoHandMace,
        StatName.Polearm, StatName.Bow, StatName.Crossbow, StatName.Javelin, StatName.Shield,
        StatName.FireMagic, StatName.WaterMagic, StatName.EarthMagic, StatName.LightningMagic,
        StatName.ArcaneMagic, StatName.DarkMagic, StatName.HolyMagic,
    };

    static readonly StatName[] Labor =
    {
        StatName.Mining, StatName.Growing, StatName.Cutting, StatName.Taming,
        StatName.Medicine, StatName.Research, StatName.Social,
        StatName.Cooking, StatName.WeaponCrafting, StatName.ArmorCrafting, StatName.Alchemy,
        StatName.Smelting, StatName.Stonecutting, StatName.Woodworking, StatName.Gemcutting, StatName.Labor,
    };

    // 풀마다 (큰 열정 최소·최대, 작은 열정 최소·최대). **최대는 풀마다 큰 2 · 작은 4**(2026-09-20, 사용자 결정 —
    // 처음엔 큰 1 · 작은 2~3이었다). 최소는 그대로라 적게 나오는 사람도 있다.
    static (int majMin, int majMax, int minMin, int minMax) CountOf(PassionPool p) => p switch
    {
        PassionPool.CombatArts => (0, 2, 1, 4),
        _                      => (0, 2, 2, 4),
    };

    public static IReadOnlyList<StatName> StatsOf(PassionPool p) => p == PassionPool.CombatArts ? CombatArts : Labor;

    // 이 스탯은 어느 풀인가 — 열정이 안 붙는 스탯은 `null`.
    public static PassionPool? PoolOf(StatName s)
    {
        if (System.Array.IndexOf(CombatArts, s) >= 0) return PassionPool.CombatArts;
        if (System.Array.IndexOf(Labor, s) >= 0)      return PassionPool.Labor;
        return null;
    }

    public static float Rate(Passion p) => p switch
    {
        Passion.Major => 2f,
        Passion.Minor => 1.5f,
        _             => 1f,
    };

    public static string DisplayName(this Passion p) => p switch
    {
        // **화면 이름은 "재능"이다**(2026-09-20, 사용자: *"열정? 이것도 용어를 바꿔서"*) — 하는 일이 *더 빨리 는다*라
        // 좋아한다는 말보다 타고난 소질에 가깝다. 식별자(`Passion`)는 그대로 둔다(코드와 문서가 인용한다).
        Passion.Major => "큰 재능",
        Passion.Minor => "작은 재능",
        _             => "재능 없음",
    };

    // 스탯 이름 옆 표식 — **글자는 ASCII만**(불꽃 같은 기호는 폰트에 없어 조용히 네모가 된다).
    public static string Mark(Passion p) => p switch
    {
        Passion.Major => "<color=#FF8A2A>++</color>",
        Passion.Minor => "<color=#E8A33D>+</color>",
        _             => "",
    };

    // ── 굴림 ────────────────────────────────────────────────────────────────
    //
    // 풀마다 따로 뽑는다. **성장 배경이 올려 주는 스탯은 세 배로 잘 뽑힌다** — 대장장이 출신이면 대장일 쪽에 잘 붙는다
    // (이력과 열정이 따로 놀면 *"대장장이였는데 쇠는 싫어한다"*가 흔해진다).
    // `rng`는 부르는 쪽의 난수열 **꼬리**다(`PawnSpec` — 굴리는 순서가 곧 호환성이다).
    public const float StoryWeight = 3f;

    public static Dictionary<StatName, Passion> Roll(System.Random rng,
                                                     BackstoryData childhood, BackstoryData adulthood)
    {
        var boosted = new HashSet<StatName>();
        foreach (var story in new[] { childhood, adulthood })
        {
            if (story == null || story.StatOffsets == null) continue;
            foreach (var o in story.StatOffsets)
                if (o != null && o.Delta > 0f) boosted.Add(o.Stat);
        }

        var result = new Dictionary<StatName, Passion>();
        foreach (PassionPool pool in System.Enum.GetValues(typeof(PassionPool)))
        {
            var weight = new Dictionary<StatName, float>();
            foreach (var s in StatsOf(pool)) weight[s] = boosted.Contains(s) ? StoryWeight : 1f;

            var (majMin, majMax, minMin, minMax) = CountOf(pool);
            int major = rng.Next(majMin, majMax + 1);
            int minor = rng.Next(minMin, minMax + 1);
            for (int i = 0; i < major; i++) { var s = Draw(rng, weight, pool); if (s.HasValue) result[s.Value] = Passion.Major; }
            for (int i = 0; i < minor; i++) { var s = Draw(rng, weight, pool); if (s.HasValue) result[s.Value] = Passion.Minor; }
        }
        return result;
    }

    // 가중치로 하나 뽑고 **다시 안 뽑히게 뺀다**(한 스탯에 두 번 붙으면 개수가 조용히 준다).
    // 순서는 풀의 표 순서로 훑는다 — `Dictionary`의 순서에 기대면 같은 씨앗이 다른 답을 낼 수 있다.
    static StatName? Draw(System.Random rng, Dictionary<StatName, float> weight, PassionPool pool)
    {
        float sum = 0f;
        foreach (var kv in weight) sum += kv.Value;
        if (sum <= 0f) return null;
        double r = rng.NextDouble() * sum;
        StatName? last = null;
        foreach (var s in StatsOf(pool))
        {
            if (!weight.TryGetValue(s, out float w)) continue;
            last = s;
            r -= w;
            if (r <= 0) { weight.Remove(s); return s; }
        }
        if (last.HasValue) weight.Remove(last.Value);   // 부동소수 끝자락
        return last;
    }
}

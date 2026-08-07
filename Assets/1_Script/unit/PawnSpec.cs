using System;
using System.Collections.Generic;
using UnityEngine;

// **개체 하나의 명세** — 이 폰이 어떤 사람인가를 태어나기 전에 적어 둔 것.
//
// 왜 필요한가: 마을에서 사람을 고용할 때 **보기 전에 정해져 있어야 한다.** 이름·스탯·값이
// 메뉴에 뜨고, 그걸 보고 고른 뒤에야 폰이 생긴다. 그런데 `UnitBrain`은 `Awake`에서 이름과
// 스탯을 **제 마음대로 굴린다** — 그대로 두면 메뉴에서 본 사람과 합류한 사람이 다르다.
//
// **모든 무작위는 시드 하나에서 나온다.** 그래서
//   ① 같은 시드면 몇 번을 다시 계산해도 같은 사람이 나오고(세이브 없이도 일관),
//   ② 나중에 **외모·몸 크기를 커스터마이징**하게 되면 굴리는 대신 값을 넣기만 하면 된다 —
//      적용 통로(`UnitBrain.ApplySpec`)는 그대로다. 무작위 생성과 사람이 정하는 것이
//      **같은 그릇**을 쓰는 것이 요점이다.
//
// 늘어날 자리: 머리·피부색·장식 같은 것은 여기 필드로 붙는다. 붙는 순간 마을 후보도,
// 던전 코어 소환도, 나중의 캐릭터 생성 화면도 같은 값을 다룬다.
public class PawnSpec
{
    public int    Seed;
    public string Name;

    // 몸 크기 배율. 지금은 이것 하나지만 **외모 축의 첫 칸**이다 —
    // 여기 값이 있다는 것 자체가 "생김새는 명세가 든다"는 규약을 세운다.
    public float BodyScale = 1f;

    // 굴린 등급. 값이 아니라 **등급**을 든다 — 화면에 뜨는 것도, 값을 매기는 것도 등급이다.
    public readonly Dictionary<StatName, StatGrade> Grades = new();

    // 이 사람이 얼마나 쓸 만한가 — 등급 평균(0 = F-). 값 매김·표시의 단일 기준.
    public float GradeAverage { get; private set; }

    public StatGrade AverageGrade =>
        (StatGrade)Mathf.Clamp(Mathf.RoundToInt(GradeAverage), 0, (int)StatGrade.SSSPlus);

    // ── 굴리기 ────────────────────────────────────────────────────────────────

    // 종족 데이터의 등급 범위를 **덮어쓴다**(min/max). 마을에서 구하는 사람은 종족 기본값이
    // 아니라 "떠도는 이"의 수준이어야 해서다 — 같은 인간이라도 어디서 만났느냐로 갈린다.
    //
    // 몸 크기는 좁게 흔든다. 크게 흔들면 같은 종족이 아니라 다른 종족처럼 보인다.
    public static PawnSpec Roll(UnitData data, int seed, StatGrade min, StatGrade max,
                                IReadOnlyList<string> nameCandidates = null)
    {
        var spec = new PawnSpec { Seed = seed };
        var rng  = new System.Random(seed);

        int lo = Mathf.Min((int)min, (int)max);
        int hi = Mathf.Max((int)min, (int)max);
        // **먼저 이 사람의 수준(tier)을 정하고, 스탯은 그 언저리에서 굴린다.**
        //
        // 스탯 51개를 각각 균등하게 굴리면 **평균이 거의 안 흔들린다** — 51번을 평균 내면
        // 중심으로 몰려서(표준편차가 1/√51로 줄어든다) 누구나 한복판 등급이 되고,
        // "강한 방랑자"라는 것이 성립하지 않는다. 값도 스탯을 못 따라간다.
        //
        // 수준을 먼저 뽑으면 **사람 사이의 차이**가 생기고, 언저리에서 굴리는 것이
        // 같은 수준 안의 들쭉날쭉함(무엇을 잘하나)을 만든다.
        int tier = rng.Next(lo, hi + 1);

        int sum = 0, count = 0;
        foreach (StatName stat in Enum.GetValues(typeof(StatName)))
        {
            int g = Mathf.Clamp(tier + rng.Next(-1, 2), lo, hi);   // 수준 ±1칸
            spec.Grades[stat] = (StatGrade)g;
            sum += g;
            count++;
        }
        spec.GradeAverage = count > 0 ? sum / (float)count : 0f;

        spec.BodyScale = 0.94f + (float)rng.NextDouble() * 0.12f;   // 0.94 ~ 1.06
        spec.Name      = PickName(data, rng, nameCandidates);

        return spec;
    }

    static string PickName(UnitData data, System.Random rng, IReadOnlyList<string> candidates)
    {
        var pool = candidates != null && candidates.Count > 0
                 ? candidates
                 : (data != null ? data.NamePool : null);

        if (pool != null && pool.Count > 0) return pool[rng.Next(pool.Count)];
        if (data != null && !string.IsNullOrEmpty(data.RaceName)) return data.RaceName;
        return "떠돌이";
    }

    // ── 값 ────────────────────────────────────────────────────────────────────

    // **사람 값은 스탯이 정한다.** 저작한 폭도, 지역도 아니다 — 기준이 등급 하나뿐이라
    // "왜 이 사람이 비싼가"에 답이 하나로 남고, 나중에 더 강한 사람이 열려도 값이 알아서 따라온다.
    //
    //   F- 한 사람 1000G에서 시작해 **등급 한 칸당 500G.**
    //   마을 후보 범위(F- ~ E)면 대략 **1000 ~ 3000G**, 그 위 등급이 열리면 그만큼 더 든다.
    const float PriceBase     = 1000f;
    const float PricePerGrade = 500f;

    // **같은 등급이라도 값은 다르다** — 급한 사람은 싸게 나오고 자기 값을 아는 사람은 비싸게
    // 부른다. 흔들림이 없으면 값이 곧 스탯 표가 되어 굳이 볼 이유가 없어진다.
    // 폭(±15%)이 등급 한 칸보다 좁아야 "대체로 강한 쪽이 비싸다"가 유지된다.
    //
    // 흔들림도 시드에서 나오므로 **다시 계산해도 같은 값**이다(메뉴를 여닫는다고 값이 바뀌면
    // 원하는 값이 나올 때까지 여닫게 된다). 시드를 한 번 더 섞어 스탯 추첨과 **다른 열**을 쓴다 —
    // 같은 열이면 "스탯이 좋으면 흔들림도 크다"는 상관이 생긴다.
    public int Price
    {
        get
        {
            var   rng    = new System.Random(unchecked(Seed * 31 + 977));
            float jitter = 0.85f + (float)rng.NextDouble() * 0.30f;   // 0.85 ~ 1.15

            return Mathf.Max(1, Mathf.RoundToInt(
                (PriceBase + GradeAverage * PricePerGrade) * jitter));
        }
    }
}

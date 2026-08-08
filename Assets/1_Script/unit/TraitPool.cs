using System.Collections.Generic;
using UnityEngine;

// **특성 레지스트리.** `Resources/Data/Traits/`에 `TraitData` 에셋을 두면 그것만으로 추첨 풀에
// 오른다 — 씬이나 프리팹 배열에 등록하지 않는다(`DataManager.AllBuildings`와 같은 관례).
//
// 목록을 배열로 들면 런타임 생성물에서 반드시 빈다. 이 저장소에서 네 번 겪은 일이라
// **레지스트리 폴백이 규칙**이다.
public static class TraitPool
{
    // 폰 하나가 받는 특성 수의 **상한**. 넷을 넘으면 폰 하나를 읽는 데 드는 품이 스탯 창을
    // 여는 것과 비슷해진다.
    public const int MaxCount = 3;

    // **몇 개를 받을지도 굴린다(0~3).** 전원이 정확히 셋이면 특성은 개성이 아니라 **양식**이 된다 —
    // 누구나 세 줄을 갖고 있으니 "이 폰은 특별하다"가 성립하지 않는다. 아무것도 없는 폰이
    // 있어야 셋 가진 폰이 눈에 띈다.
    //
    // 가중치는 가운데가 두껍다. 0이 흔하면 특성 시스템이 없는 것처럼 보이고, 3이 흔하면
    // 다시 양식이 된다.
    static readonly float[] CountWeights = { 0.8f, 1.8f, 3f, 2.6f };   // 0개 10% · 1개 22% · 2개 36% · 3개 32%

    public static int RollCount(System.Random rng)
    {
        float total = 0f;
        foreach (float w in CountWeights) total += w;

        double roll = rng.NextDouble() * total;
        for (int i = 0; i < CountWeights.Length; i++)
        {
            roll -= CountWeights[i];
            if (roll <= 0d) return i;
        }
        return CountWeights.Length - 1;
    }

    // 폰 하나 몫 — **개수까지 같은 난수열에서** 뽑는다. 시드가 같으면 몇 개인지도 같아야
    // 마을 후보 메뉴에서 본 사람과 합류한 사람이 같다.
    public static List<TraitData> RollForPawn(System.Random rng) => Roll(RollCount(rng), rng);

    static List<TraitData> _all;

    public static IReadOnlyList<TraitData> All
    {
        get
        {
            if (_all == null) Load();
            return _all;
        }
    }

    static void Load()
    {
        _all = new List<TraitData>(Resources.LoadAll<TraitData>("Data/Traits"));
        if (_all.Count == 0)
            Debug.LogWarning("[TraitPool] Resources/Data/Traits/ 에 TraitData 에셋이 없습니다 — 특성 없이 굴러갑니다.");
    }

    public static TraitData Get(string traitName)
    {
        foreach (var t in All)
            if (t != null && (t.name == traitName || t.DisplayName == traitName)) return t;
        return null;
    }

    // ── 추첨 ──────────────────────────────────────────────────────────────────

    // **가중치 추첨 + 배타 묶음 회피.** `rng`를 밖에서 받는 이유는 `PawnSpec`의 시드에서
    // 나와야 하기 때문이다 — 마을 후보는 메뉴에 뜬 특성과 합류한 폰의 특성이 같아야 한다.
    //
    // 풀이 얕으면(배타로 다 걸리면) 요청한 수보다 적게 준다. 억지로 채우려고 배타를 깨면
    // `용감`과 `겁쟁이`를 함께 가진 폰이 나오는데, 그건 특성이 둘 붙은 게 아니라 하나도
    // 안 붙은 것과 같다(오프셋이 상쇄된다).
    public static List<TraitData> Roll(int count, System.Random rng)
    {
        var picked = new List<TraitData>();
        if (count <= 0 || All.Count == 0) return picked;

        var candidates = new List<TraitData>();
        foreach (var t in All)
            if (t != null && t.Weight > 0f) candidates.Add(t);

        var usedGroups = new HashSet<string>();

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            float total = 0f;
            foreach (var t in candidates) total += t.Weight;
            if (total <= 0f) break;

            double roll = rng.NextDouble() * total;
            TraitData chosen = null;
            foreach (var t in candidates)
            {
                roll -= t.Weight;
                if (roll <= 0d) { chosen = t; break; }
            }
            chosen ??= candidates[candidates.Count - 1];

            picked.Add(chosen);
            candidates.Remove(chosen);

            // 같은 묶음의 나머지를 후보에서 덜어낸다.
            if (!string.IsNullOrWhiteSpace(chosen.ExclusiveGroup))
            {
                usedGroups.Add(chosen.ExclusiveGroup);
                candidates.RemoveAll(t => t.ExclusiveGroup == chosen.ExclusiveGroup);
            }
        }

        return picked;
    }

    // `강인함 · 겁쟁이` — 이름만 이어 붙인다. 좁은 창(체력 패널)이 쓴다.
    public static string NameList(IEnumerable<TraitData> traits)
    {
        if (traits == null) return "";

        var names = new List<string>();
        foreach (var t in traits)
            if (t != null) names.Add(t.DisplayName);
        return string.Join(" · ", names);
    }

    // 이미 가진 것들과 배타 묶음이 겹치나.
    public static bool Conflicts(IEnumerable<TraitData> have, TraitData candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.ExclusiveGroup)) return false;

        foreach (var t in have)
            if (t != null && t.ExclusiveGroup == candidate.ExclusiveGroup) return true;
        return false;
    }

    // ── 적용 ──────────────────────────────────────────────────────────────────

    // **특성은 스탯에 숫자를 얹을 뿐이다**(모델 A). 그래서 전투·작업 코드는 특성을 몰라도 된다.
    //
    // `GrowStat`을 쓰지 않는다 — 그건 성장이라 `IsLocked`(하수인)에서 막히고, 등급 승급
    // 알림까지 띄운다. 특성은 성장이 아니라 **타고난 것**이므로 태어날 때의 값을 바꾼다.
    public static void Apply(TraitData trait, UnitStats stats) => Offset(trait, stats, 1f);

    // 떼어낼 때. **정확히 되돌아오지는 않는다** — 얹을 때 0~130으로 잘렸으면 그만큼은
    // 못 돌려준다. 특성 제거가 흔한 일이 아니라 감수한다(흔해지면 보정분을 따로 누적할 것).
    public static void Remove(TraitData trait, UnitStats stats) => Offset(trait, stats, -1f);

    static void Offset(TraitData trait, UnitStats stats, float sign)
    {
        if (trait == null || stats == null || trait.StatOffsets == null) return;

        foreach (var o in trait.StatOffsets)
        {
            if (o == null || Mathf.Approximately(o.Delta, 0f)) continue;
            float v = Mathf.Clamp(stats.GetStat(o.Stat) + o.Delta * sign, 1f, StatGradeExt.MaxStatValue);
            stats.SetStat(o.Stat, v);
        }
    }
}

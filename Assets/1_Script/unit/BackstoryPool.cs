using System.Collections.Generic;
using UnityEngine;

// **성장 배경 레지스트리.** `Resources/Data/Backstories/`에 `BackstoryData` 에셋을 두면 그것만으로
// 추첨 풀에 오른다 — 씬이나 프리팹 배열에 등록하지 않는다(`TraitPool`과 같은 관례).
//
// 목록을 배열로 들면 런타임 생성물에서 반드시 빈다. 이 저장소에서 네 번 겪은 일이라
// **레지스트리 폴백이 규칙**이다.
public static class BackstoryPool
{
    static List<BackstoryData> _all;

    public static IReadOnlyList<BackstoryData> All
    {
        get
        {
            if (_all == null) Load();
            return _all;
        }
    }

    static void Load()
    {
        _all = new List<BackstoryData>(Resources.LoadAll<BackstoryData>("Data/Backstories"));
        if (_all.Count == 0)
            Debug.LogWarning("[BackstoryPool] Resources/Data/Backstories/ 에 BackstoryData 에셋이 " +
                             "없습니다 — 성장 배경 없이 굴러갑니다.");
    }

    public static BackstoryData Get(string assetName)
    {
        foreach (var b in All)
            if (b != null && (b.name == assetName || b.DisplayName == assetName)) return b;
        return null;
    }

    // ── 추첨 ──────────────────────────────────────────────────────────────────
    //
    // **칸마다 하나씩, 반드시 뽑는다.** 특성은 개수까지 굴리지만(없는 폰이 있어야 셋 가진
    // 폰이 눈에 띈다) 배경은 다르다 — 살아온 내력이 **없는** 사람은 없다. 그래서 개수 굴림이
    // 없고, 대신 **어느 배경이 나오느냐**가 사람을 가른다.
    //
    // `rng`를 밖에서 받는 이유는 `PawnSpec`의 시드에서 나와야 하기 때문이다 — 마을 후보는
    // 메뉴에 뜬 배경과 합류한 폰의 배경이 같아야 한다.
    // **공용이라는 것은 없다 — 모든 배경은 어느 무리의 것이다.**
    //
    // 한때 묶음이 빈 것을 "공용"으로 두고 늘 후보에 넣었다. 그러면 **산적에게서 `사원의 시동`이
    // 나온다** — 무리에서 뽑은 사람인데 그 무리와 아무 상관 없는 이력을 지고 오는 것이,
    // 세력마다 이력을 갈라 둔 뜻을 통째로 지운다.
    //
    // 지금 "공용"으로 보이던 것들(농가·대장간·사원·광산…)은 사실 **`civilian`**이다 —
    // 이름을 안 붙였을 뿐이다. 이름을 붙이고 나면 규칙이 한 줄로 준다: **제 무리의 것에서
    // 뽑는다.**
    //
    // **`civilian`이 폴백이다** — 묶음을 안 적은 세력(야수·망자·고블린…)이나 그 무리의 배경을
    // 아직 저작하지 않은 칸은 여기로 떨어진다. 아무 무리에도 안 걸려 배경이 **없는** 것보다,
    // 여느 사람처럼 자랐다고 보는 편이 늘 낫다.
    public const string DefaultGroup = "civilian";

    public static BackstoryData Roll(BackstorySlot slot, System.Random rng, string group = null)
    {
        var picked = RollIn(slot, rng, group);
        return picked != null ? picked : RollIn(slot, rng, DefaultGroup);
    }

    static BackstoryData RollIn(BackstorySlot slot, System.Random rng, string group)
    {
        float total = 0f;
        foreach (var b in All)
            if (Fits(b, slot, group)) total += b.Weight;

        if (total <= 0f) return null;

        double roll = rng.NextDouble() * total;
        BackstoryData last = null;

        foreach (var b in All)
        {
            if (!Fits(b, slot, group)) continue;
            last  = b;
            roll -= b.Weight;
            if (roll <= 0d) return b;
        }
        return last;
    }

    // 묶음을 안 적은 배경은 `civilian`으로 친다 — 저작을 빠뜨렸다고 아무 데서도 안 나오면
    // **아무도 그 사실을 모른 채** 풀만 조용히 얕아진다.
    static bool Fits(BackstoryData b, BackstorySlot slot, string group)
    {
        if (b == null || b.Slot != slot || b.Weight <= 0f) return false;

        string mine = string.IsNullOrWhiteSpace(b.Group) ? DefaultGroup : b.Group;
        return mine == group;
    }

    // ── 스탯에 얹기 ──────────────────────────────────────────────────────────
    //
    // **`GrowStat`을 쓰지 않는다** — 그건 성장이라 `IsLocked`(하수인)에서 막히고 승급 알림까지
    // 띄운다. 배경은 타고난 것이 아니라 살아온 것이지만, **태어날 때 이미 정해져 있다**는
    // 점에서는 특성과 같다(`TraitPool.Apply`와 같은 자리·같은 방법).
    public static void Apply(BackstoryData b, UnitStats stats) => Offset(b, stats, +1f);
    public static void Remove(BackstoryData b, UnitStats stats) => Offset(b, stats, -1f);

    static void Offset(BackstoryData b, UnitStats stats, float sign)
    {
        if (b?.StatOffsets == null || stats == null) return;

        foreach (var o in b.StatOffsets)
        {
            if (o == null || Mathf.Approximately(o.Delta, 0f)) continue;

            // **자르는 범위도 특성과 같다**(1 ~ 130). 뗄 때 정확히 안 돌아오는 것까지 같다 —
            // 얹을 때 잘렸으면 그만큼은 못 돌려준다.
            float v = Mathf.Clamp(stats.GetStat(o.Stat) + o.Delta * sign, 1f, StatGradeExt.MaxStatValue);
            stats.SetStat(o.Stat, v);
        }
    }

    // `거리의 아이 · 용병` — 이름만 이어 붙인다. 좁은 창(체력 패널)이 쓴다.
    public static string NameList(params BackstoryData[] list)
    {
        if (list == null) return "";

        var names = new List<string>();
        foreach (var b in list)
            if (b != null) names.Add(b.DisplayName);

        return string.Join(" · ", names);
    }
}

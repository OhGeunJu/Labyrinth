using System;
using System.Collections.Generic;
using UnityEngine;

// **장소의 상태 — 주인과 형편을 드는 곳**(2026-11-18, `docs/design_world_history.md` 6절 · 11절 5단계).
//
// 지금까지 장소의 주인은 **계산됐다** — 마을은 `TownSite.ResolveFaction`(경비병 → 주민 → 바이옴), 소굴은 주인 없이
// `WorldLairs`가 병력과 `_gone`만 들었다. 주인이 **바뀌는** 일(소굴을 다른 세력이 차지한다)을 담으려면 주인을
// **상태로** 들 곳이 필요하다.
//
// - **비어 있으면 저작/계산된 주인 그대로다**(저작 < 주입 — 상인 상품과 같은 규칙). 적힌 장소만 덮는다.
// - **오브젝트 참조가 없다** — 세력은 Id(`DataId.Resolve`). 세이브는 `States`를 쓰면 끝난다.
public enum SiteCondition { Intact, Ruined, Empty, Occupied }   // 꼬리에만 붙일 것(정수 직렬화)

[Serializable]
public class SiteState
{
    public string        SiteId;
    public string        OwnerFactionId;   // 비우면 저작/계산된 주인
    public SiteCondition Condition;
    public int           SinceDay;         // 지금 형편이 된 날
}

public static class SiteRegistry
{
    static readonly Dictionary<string, SiteState> _states = new();

    public static IReadOnlyCollection<SiteState> States => _states.Values;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _states.Clear();

    public static bool TryGet(string siteId, out SiteState state)
    {
        state = null;
        return !string.IsNullOrEmpty(siteId) && _states.TryGetValue(siteId, out state);
    }

    // 형편만 바꾼다(주인은 그대로).
    public static SiteState SetCondition(string siteId, SiteCondition condition)
    {
        if (string.IsNullOrEmpty(siteId)) return null;
        var s = Ensure(siteId);
        s.Condition = condition;
        s.SinceDay  = GameTimeManager.CurrentDay;
        return s;
    }

    // 주인이 바뀐다 — 차지했다.
    public static SiteState Claim(string siteId, FactionData owner)
    {
        if (string.IsNullOrEmpty(siteId)) return null;
        var s = Ensure(siteId);
        s.OwnerFactionId = owner != null ? DataId.Resolve(owner, null) : null;
        s.Condition      = SiteCondition.Occupied;
        s.SinceDay       = GameTimeManager.CurrentDay;
        return s;
    }

    // **적힌 주인** — 없으면 null(그러면 부르는 쪽이 제 계산대로). `TownSite.ResolveFaction`이 먼저 묻는다.
    public static FactionData OwnerOf(string siteId)
    {
        if (!TryGet(siteId, out var s) || string.IsNullOrEmpty(s.OwnerFactionId)) return null;
        foreach (var f in DataManager.AllFactions)
            if (f != null && DataId.Resolve(f, null) == s.OwnerFactionId) return f;
        return null;
    }

    static SiteState Ensure(string siteId)
    {
        if (!_states.TryGetValue(siteId, out var s))
            _states[siteId] = s = new SiteState { SiteId = siteId, Condition = SiteCondition.Intact };
        return s;
    }
}

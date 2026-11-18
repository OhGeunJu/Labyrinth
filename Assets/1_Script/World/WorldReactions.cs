using System;
using System.Collections.Generic;
using UnityEngine;

// **사건이 다음 사건을 부른다 — 뼈대**(2026-11-18, `docs/design_world_history.md` 7절 · 11절 5단계).
//
// `WorldHistory.OnRecorded`를 듣고 규칙 표(`Rules`)에 맞으면 **며칠 뒤로 예약**한다. 날이 바뀔 때
// (`GameTimeManager.OnDayChanged`) 때가 된 것을 푼다. **반응도 사건으로 적힌다** — 그래서 NPC가
// *"그 소굴이 비고 나서 오크들이 들어앉았다"*를 말할 수 있다.
//
// - **무엇이 무엇을 부르나는 표 하나다**(`Rules`) — 반응마다 코드를 짜지 않는다. 지금은 코드 표이고, 늘면 에셋으로 옮긴다.
// - 지금 규칙은 하나: **소굴이 비면(`LairCleared`) 3일 뒤 가까운 적대 세력의 소굴 주인이 차지한다**
//   (`SiteRegistry.Claim` + `SiteClaimed` 사건). 차지한 세력의 폰이 실제로 서는 것은 아직이다 — 소굴 내용물이
//   세력별로 갈려야 한다(설계 7절 "알려진 큰 일").
// - **예약은 사실로만 든다**(Id · 날 · 종류) — 세이브는 `Pending`을 쓰면 끝난다.
public static class WorldReactions
{
    public enum ReactionKind { ClaimByHostileNeighbour }   // 꼬리에만 붙일 것

    public readonly struct Rule
    {
        public readonly WorldEventType Trigger;
        public readonly int            DelayDays;
        public readonly ReactionKind   Kind;
        public Rule(WorldEventType trigger, int delayDays, ReactionKind kind)
        { Trigger = trigger; DelayDays = delayDays; Kind = kind; }
    }

    // ── 규칙 표 ──────────────────────────────────────────────────────────────
    public static readonly Rule[] Rules =
    {
        new(WorldEventType.LairCleared, 3, ReactionKind.ClaimByHostileNeighbour),
    };

    // 차지할 이웃을 찾는 반경(m) — 이보다 먼 소굴의 세력은 안 온다.
    public const float ClaimRadius = 600f;

    [Serializable]
    public class Pending
    {
        public int          DueDay;
        public ReactionKind Kind;
        public int          SourceEventId;
    }

    static readonly List<Pending> _pending = new();
    public static IReadOnlyList<Pending> PendingList => _pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _pending.Clear();

    // `WorldHistory.ResetStatics`(SubsystemRegistration)가 구독자를 비운 **뒤에** 붙는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        WorldHistory.OnRecorded -= OnRecorded;
        WorldHistory.OnRecorded += OnRecorded;
        GameTimeManager.OnDayChanged -= OnDay;
        GameTimeManager.OnDayChanged += OnDay;
    }

    static void OnRecorded(WorldEvent ev)
    {
        if (ev == null || ev.Count > 1) return;   // 합쳐진 줄은 새 일이 아니다
        foreach (var r in Rules)
        {
            if (r.Trigger != ev.Type) continue;
            if (_pending.Exists(p => p.SourceEventId == ev.Id && p.Kind == r.Kind)) continue;
            _pending.Add(new Pending { DueDay = ev.Day + r.DelayDays, Kind = r.Kind, SourceEventId = ev.Id });
        }
    }

    static void OnDay() => Resolve(GameTimeManager.CurrentDay);

    // 때가 된 예약을 푼다. `day`가 `int.MaxValue`면 날을 안 본다(디버그 창 — 기다리지 않고 시험).
    public static int Resolve(int day)
    {
        int done = 0;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var p = _pending[i];
            if (p.DueDay > day) continue;
            _pending.RemoveAt(i);
            Run(p);
            done++;
        }
        return done;
    }

    public static int ResolveAllNow() => Resolve(int.MaxValue);

    static void Run(Pending p)
    {
        var src = Find(p.SourceEventId);
        if (src == null) return;
        switch (p.Kind)
        {
            case ReactionKind.ClaimByHostileNeighbour: ClaimByHostileNeighbour(src); break;
        }
    }

    static WorldEvent Find(int id)
    {
        foreach (var e in WorldHistory.Events) if (e.Id == id) return e;
        return null;
    }

    // **비어 있는 소굴을 가까운 적대 세력이 차지한다.** 후보는 아직 서 있는 소굴들 — 그 소굴 무리의 세력이 원래 주인과
    // 적대이고(원래 주인을 모르면 아무나) 반경 안에서 가장 가까운 것. 없으면 아무도 안 온다(빈 채로 남는다).
    static void ClaimByHostileNeighbour(WorldEvent cleared)
    {
        if (string.IsNullOrEmpty(cleared.Place)) return;
        if (SiteRegistry.TryGet(cleared.Place, out var st) && st.Condition == SiteCondition.Occupied) return;

        var former = FindFaction(cleared.TargetFaction);
        var at = new Vector2(cleared.Position.x, cleared.Position.z);

        FactionData best = null;
        float bestSqr = ClaimRadius * ClaimRadius;
        foreach (var lair in WorldLairs.All)
        {
            var f = lair?.Band != null ? lair.Band.Faction : null;
            if (f == null || f == former || lair.Id == cleared.Place) continue;
            if (former != null && !FactionRelations.IsHostileBetween(former, f)) continue;
            float d = (lair.Position - at).sqrMagnitude;
            if (d >= bestSqr) continue;
            bestSqr = d;
            best    = f;
        }
        if (best == null) return;

        SiteRegistry.Claim(cleared.Place, best);
        WorldHistory.RememberFaction(best);
        WorldHistory.Record(WorldEventType.SiteClaimed, WorldHistory.FactionId(best), former,
                            cleared.Place, cleared.Position);
    }

    static FactionData FindFaction(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var f in DataManager.AllFactions)
            if (f != null && WorldHistory.FactionId(f) == id) return f;
        return null;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// **세계 역사 — 일어난 일을 사실로 적는다**(2026-11-18, `docs/design_world_history.md` 4절 · 11절 2단계).
//
// 지금까지 우호도는 숫자 하나였다 — 왜 움직였는지는 알림 한 줄로 흘러가고 사라졌다. 여기에 **사건**이 남는다:
// 누가(`Actor`) · 누구의 일을(`TargetFaction`) · 어디서(`Place`) · 누가 얽혔나(`Involved`) · 몇 번(`Count`).
// 우호도는 사건의 결과다 — **`FactionRelations.Add`를 직접 부르던 세 곳은 이제 사건을 적고, 표(`RuleOf`)가 값을 얹는다.**
//
// ## 규약
// - **문장을 저장하지 않는다 — 사실만**(설계 10절). 종류 · Id · 숫자로만 적고, 보이는 문장은 볼 때마다 `Describe`가 만든다.
// - **오브젝트 참조가 없다** — 폰은 `PawnRegistry`의 Id, 세력은 `DataId.Resolve`의 Id. 세이브는 `Events` + `NextId`를
//   `JsonUtility`로 쓰면 끝난다(색인은 다시 짓는다 — `Rebuild`).
// - **범위는 종류가 정한다**(`RuleOf` 표 하나) — 같은 종류가 사건마다 다르게 퍼지면 화면으로 설명할 수 없다.
// - **`WorldEventType`은 꼬리에만 붙인다**(정수 직렬화).
public enum WorldEventType
{
    Ransomed,          // 몸값을 받고 돌려보냈다            — WorldRansom.Sell
    PrisonerReleased,  // 값 없이 놓아 줬다                  — Imprisonment.Release(PrisonCage.cs)
    PawnKilled,        // 그 세력 사람을 죽였다              — UnitBrain.ReportDeathToFaction
    Traded,            // 사고팔았다                         — Shop.TryBuy / TrySell
    Hired,             // 용병 계약 · 돈 주고 데려옴          — Hireling.Sign · Conversation.Hire
    Converted,         // 회유 · 길들여 넘어왔다              — Conversion.Join
    LairCleared,       // 소굴이 비었다                      — WorldLairs.Clear
    PawnExecuted,      // 갇힌 채로 죽었다(처형 · 굶겨 죽임)  — UnitBrain.ReportDeathToFaction
    SiteClaimed,       // 빈 장소를 다른 세력이 차지했다      — WorldReactions(소굴이 빈 며칠 뒤)
}

// 얼마나 퍼지나 — 앎(설계 5절)이 읽는다. 지금은 적어만 둔다.
public enum EventScope { Involved, Site, Faction, Region, World }

// 사건에 얽힌 폰의 몫.
public enum EventRole { Subject, Victim, Witness, Beneficiary }

[Serializable]
public struct EventParty
{
    public string    PawnId;
    public EventRole Role;
}

[Serializable]
public class WorldEvent
{
    public int            Id;
    public int            Day;            // 마지막으로 일어난 날(합쳐지면 갱신)
    public int            FirstDay;
    public WorldEventType Type;
    public string         Actor;          // "player" · 세력 Id · 폰 Id · 비면 세계(누구의 짓도 아니다)
    public string         TargetFaction;  // 누구의 일인가(세력 Id)
    public string         Place;          // 사이트 Id — 없으면 `Position`
    public Vector3        Position;
    public List<EventParty> Involved = new();
    public EventScope     Scope;
    public float          Importance;
    public int            Count = 1;      // 합쳐진 횟수(4-4)
    public float          Amount;         // 숫자 사실 — 몸값 · 거래액(합쳐지면 더한다) · 계약 일수 · 데려온 값
    public float          Favor;          // 이 사건으로 그 세력의 우호도에 얹은 값(합쳐지면 더한다)
}

public static class WorldHistory
{
    public const string PlayerActor = "player";

    // ── 종류마다 한 줄 — 범위 · 우호도 · 무게 · 합치나 ─────────────────────────
    //
    // 우호도 값은 예전 `FactionRelations`의 상수를 그대로 쓴다(바뀐 것은 부르는 길뿐이다).
    // 새로 넣은 사건(거래 · 고용 · 전향 · 소굴)은 **아직 우호도를 안 움직인다** — 그건 균형을 정할 일이다.
    //
    // **개인 호감(`Opinion`)과 세력 우호도(`Favor`)는 다른 값이다**(설계 5-2, 사용자 결정) — 같은 사건이 둘 다의 원인이지만
    // 적대 판정은 세력 숫자만 본다. 개인 호감은 대화 · 값 · 영입에만 쓴다(지금은 첫마디 · 살펴보기 창).
    // `HalfLife`(일)마다 절반으로 흐려진다. 거래는 횟수의 제곱근으로 쌓인다(`Opinion` — 천장 없이 느려지는 곡선).
    public readonly struct Rule
    {
        public readonly EventScope Scope;
        public readonly float      Favor;
        public readonly float      Importance;
        public readonly bool       Merge;
        public readonly float      Opinion;
        public readonly float      HalfLife;
        public Rule(EventScope scope, float favor, float importance, bool merge, float opinion, float halfLife)
        { Scope = scope; Favor = favor; Importance = importance; Merge = merge; Opinion = opinion; HalfLife = halfLife; }
    }

    public static Rule RuleOf(WorldEventType t) => t switch
    {
        //                                       범위                세력 우호도                       무게  합침   호감   반감기(일)
        WorldEventType.Ransomed         => new(EventScope.Faction,  FactionRelations.RansomBonus,    3f, false,  10f, 20f),
        WorldEventType.PrisonerReleased => new(EventScope.Faction,  FactionRelations.ReleaseBonus,   3f, false,  15f, 30f),
        WorldEventType.PawnKilled       => new(EventScope.Site,     FactionRelations.KillPenalty,    5f, false, -20f, 30f),
        WorldEventType.PawnExecuted     => new(EventScope.Site,     FactionRelations.ExecutePenalty, 6f, false, -30f, 45f),
        WorldEventType.Traded           => new(EventScope.Involved, 0f,                              1f, true,    4f, 20f),
        WorldEventType.Hired            => new(EventScope.Site,     0f,                              2f, false,   6f, 20f),
        WorldEventType.Converted        => new(EventScope.Faction,  0f,                              4f, false,  -6f, 20f),
        WorldEventType.LairCleared      => new(EventScope.Region,   0f,                              8f, false,   0f, 30f),
        WorldEventType.SiteClaimed      => new(EventScope.Region,   0f,                              8f, false,   0f, 30f),
        _                               => new(EventScope.Involved, 0f,                              1f, false,   0f, 20f),
    };

    // ── 저장 · 색인 ──────────────────────────────────────────────────────────

    static readonly List<WorldEvent> _events = new();   // Id 순 — 지우지 않는다(정리는 나중)
    static int _nextId = 1;

    // 색인 셋(4-4) — 대화를 열 때 한 번 조회한다. **저장하지 않는다**(`Rebuild`가 사건에서 다시 짓는다).
    static readonly Dictionary<string, List<WorldEvent>> _byPawn    = new();
    static readonly Dictionary<string, List<WorldEvent>> _byPlace   = new();
    static readonly Dictionary<string, List<WorldEvent>> _byFaction = new();

    // 세력 Id → 이름. 문장을 만들 때만 쓰는 판 안의 캐시다(저장하지 않는다 — 세이브는 Id로 다시 찾는다).
    static readonly Dictionary<string, string> _factionNames = new();

    public static IReadOnlyList<WorldEvent> Events => _events;
    public static int NextId => _nextId;

    // 새 사건이 적혔거나 합쳐졌다 — 디버그 창 · 나중의 세력 창이 듣는다.
    public static event Action<WorldEvent> OnRecorded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _events.Clear();
        _nextId = 1;
        _byPawn.Clear(); _byPlace.Clear(); _byFaction.Clear();
        _factionNames.Clear();
        OnRecorded = null;
    }

    public static IReadOnlyList<WorldEvent> OfPawn(string pawnId)       => Lookup(_byPawn, pawnId);
    public static IReadOnlyList<WorldEvent> AtPlace(string placeId)     => Lookup(_byPlace, placeId);
    public static IReadOnlyList<WorldEvent> OfFaction(string factionId) => Lookup(_byFaction, factionId);

    static IReadOnlyList<WorldEvent> Lookup(Dictionary<string, List<WorldEvent>> map, string key)
        => !string.IsNullOrEmpty(key) && map.TryGetValue(key, out var l) ? l : Array.Empty<WorldEvent>();

    public static string FactionId(FactionData f) => f != null ? DataId.Resolve(f, null) : null;

    // 문장에 쓸 세력 이름을 기억해 둔다 — `Actor`가 세력일 때(반응 사건). 판 안의 캐시다.
    public static void RememberFaction(FactionData f)
    {
        string id = FactionId(f);
        if (id != null) _factionNames[id] = f.FactionName;
    }

    public static EventParty Party(string pawnId, EventRole role) => new() { PawnId = pawnId, Role = role };
    public static EventParty Party(UnitBrain pawn, EventRole role) => Party(pawn != null ? pawn.PawnId : null, role);

    // 그 폰이 어디 사람인가 — 사건의 `Place`로 쓴다(마을 사람이면 그 마을 · 무리면 소굴 · 모르면 비움).
    public static string HomeOf(UnitBrain pawn)
        => pawn != null && PawnRegistry.TryGet(pawn.PawnId, out var rec) ? rec.HomeSiteId : null;

    // ── 적는다 — 사건의 문은 여기 하나 ────────────────────────────────────────

    // `target`의 우호도는 표(`RuleOf`)가 움직인다 — 부르는 쪽은 **무슨 일이 있었나만** 넘긴다.
    // `involved`의 빈 Id(기록하지 않는 몸 — 짐승 · 이름 없는 몬스터)는 건너뛴다.
    public static WorldEvent Record(WorldEventType type, string actor, FactionData target,
                                    string place, Vector3 at, float amount = 0f,
                                    params EventParty[] involved)
    {
        var rule = RuleOf(type);
        string targetId = FactionId(target);
        if (targetId != null) _factionNames[targetId] = target.FactionName;

        var parties = new List<EventParty>();
        if (involved != null)
            foreach (var p in involved)
                if (!string.IsNullOrEmpty(p.PawnId)) parties.Add(p);

        // **목격자**(설계 5-3) — 그 자리 반경 안의 그 세력 사람. 합치는 사건(거래)에는 안 붙인다 — 붙이면 지나가던 사람에
        // 따라 같은 거래가 다른 줄이 된다.
        if (!rule.Merge && target != null) AddWitnesses(target, at, parties);

        int today = GameTimeManager.CurrentDay;

        // **합치기**(4-4) — 범위가 좁고 되풀이되는 일(거래)은 같은 상대와의 한 줄로 센다. 큰 사건은 합치지 않는다.
        var ev = rule.Merge ? FindMergeable(type, actor, targetId, place, parties) : null;
        if (ev != null)
        {
            ev.Count++;
            ev.Day      = today;
            ev.Amount  += amount;
            ev.Position = at;
        }
        else
        {
            ev = new WorldEvent
            {
                Id            = _nextId++,
                Day           = today,
                FirstDay      = today,
                Type          = type,
                Actor         = actor,
                TargetFaction = targetId,
                Place         = place,
                Position      = at,
                Involved      = parties,
                Scope         = rule.Scope,
                Importance    = rule.Importance,
                Amount        = amount,
            };
            _events.Add(ev);
            Index(ev);
        }

        // **우호도는 사건의 결과다** — 이유 문장은 사건에서 만든다(`Describe`). 플레이어가 한 일만 관계를 움직인다.
        if (!Mathf.Approximately(rule.Favor, 0f) && target != null && actor == PlayerActor)
        {
            ev.Favor += rule.Favor;
            FactionRelations.Add(target, rule.Favor, Describe(ev));
        }

        OnRecorded?.Invoke(ev);
        return ev;
    }

    public const float WitnessRadius = 25f;
    static readonly List<UnitBrain> _near = new();

    static void AddWitnesses(FactionData target, Vector3 at, List<EventParty> parties)
    {
        _near.Clear();
        FactionScan.MembersWithin(at, target, WitnessRadius, _near);
        foreach (var b in _near)
        {
            string id = b.PawnId;
            if (string.IsNullOrEmpty(id) || parties.Exists(q => q.PawnId == id)) continue;
            parties.Add(Party(id, EventRole.Witness));
        }
    }

    static WorldEvent FindMergeable(WorldEventType type, string actor, string target, string place, List<EventParty> parties)
    {
        // 같은 상대와의 **가장 최근** 한 줄 — 뒤에서부터 찾는다.
        for (int i = _events.Count - 1; i >= 0; i--)
        {
            var e = _events[i];
            if (e.Type != type || e.Actor != actor || e.TargetFaction != target || e.Place != place) continue;
            if (!SameParties(e.Involved, parties)) continue;
            return e;
        }
        return null;
    }

    static bool SameParties(List<EventParty> a, List<EventParty> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var p in b)
            if (!a.Exists(q => q.PawnId == p.PawnId && q.Role == p.Role)) return false;
        return true;
    }

    static void Index(WorldEvent ev)
    {
        foreach (var p in ev.Involved) AddTo(_byPawn, p.PawnId, ev);
        AddTo(_byPlace, ev.Place, ev);
        AddTo(_byFaction, ev.TargetFaction, ev);
    }

    static void AddTo(Dictionary<string, List<WorldEvent>> map, string key, WorldEvent ev)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!map.TryGetValue(key, out var l)) map[key] = l = new List<WorldEvent>();
        if (!l.Contains(ev)) l.Add(ev);
    }

    // 세이브에서 읽은 뒤 색인을 다시 짓는다(지금은 부르는 곳이 없다 — 세이브 단계에서).
    public static void Rebuild()
    {
        _byPawn.Clear(); _byPlace.Clear(); _byFaction.Clear();
        foreach (var ev in _events) Index(ev);
    }

    // ── 앎과 개인 호감 — 계산한다(설계 5절) ────────────────────────────────────
    //
    // **캐시하지 않는다** — 처지(세력 · 고향 · 자리)가 바뀌면 아는 것도 바뀌어야 한다. 부르는 곳은 대화를 열 때 ·
    // 살펴보기 창을 그릴 때뿐이라 사건 전체를 한 번 훑어도 된다.

    public const float RegionRadius = 150f;

    // 그 사람이 이 사건을 **얼마나 가까이서** 아나 — 0이면 모른다(4-2 범위 표 · 5-2 무게 표).
    // 당사자 1 · 목격 0.6 · 같은 장소 0.4 · 같은 세력 0.25 · 지역 소문 0.1. 넓은 범위는 좁은 범위를 품는다.
    public static float KnowWeight(PawnRecord rec, WorldEvent ev)
    {
        if (rec == null || ev == null) return 0f;

        foreach (var p in ev.Involved)
            if (p.PawnId == rec.Id) return p.Role == EventRole.Witness ? 0.6f : 1f;

        if (ev.Scope >= EventScope.Site && !string.IsNullOrEmpty(ev.Place) && rec.HomeSiteId == ev.Place)
            return 0.4f;
        if (ev.Scope >= EventScope.Faction && ev.TargetFaction != null && PawnRegistry.FactionIdNow(rec) == ev.TargetFaction)
            return 0.25f;
        if (ev.Scope >= EventScope.Region &&
            (PawnRegistry.PositionOf(rec) - ev.Position).sqrMagnitude <= RegionRadius * RegionRadius)
            return 0.1f;
        if (ev.Scope >= EventScope.World) return 0.1f;
        return 0f;
    }

    public static bool Knows(PawnRecord rec, WorldEvent ev) => KnowWeight(rec, ev) > 0f;

    // 이 사람이 플레이어가 한 일에 **직접** 얽힌 적이 있나(당사자 · 목격) — 돌아올 사람을 고를 때(`PawnRegistry.PickReturning`).
    public static bool SharesHistoryWithPlayer(string pawnId)
    {
        foreach (var ev in OfPawn(pawnId))
            if (ev.Actor == PlayerActor) return true;
        return false;
    }

    // **이 사람이 플레이어를 어떻게 보나** — 아는 사건마다 종류의 호감 × 아는 무게 × 흐려짐을 더한다.
    // 플레이어가 한 일만 센다. 내 사람은 묻지 않는다(0).
    public static float Opinion(PawnRecord rec)
    {
        if (rec == null) return 0f;
        var player = Faction.Player;
        if (player != null && player.Data != null && PawnRegistry.FactionIdNow(rec) == FactionId(player.Data)) return 0f;

        int today = GameTimeManager.CurrentDay;
        float sum = 0f;
        foreach (var ev in _events)
        {
            if (ev.Actor != PlayerActor) continue;
            var rule = RuleOf(ev.Type);
            if (Mathf.Approximately(rule.Opinion, 0f)) continue;

            float w = KnowWeight(rec, ev);
            if (w <= 0f) continue;

            float fade  = rule.HalfLife > 0f ? Mathf.Pow(0.5f, Mathf.Max(0, today - ev.Day) / rule.HalfLife) : 1f;
            float times = rule.Merge ? Mathf.Sqrt(Mathf.Max(1, ev.Count)) : 1f;
            sum += rule.Opinion * w * fade * times;
        }
        return sum;
    }

    public static float OpinionOf(UnitBrain pawn)
        => pawn != null && PawnRegistry.TryGet(pawn.PawnId, out var rec) ? Opinion(rec) : 0f;

    // 호감 구간 — 첫마디(`Conversation.Greeting`)와 살펴보기 창이 같은 칸을 쓴다.
    public enum OpinionBand { Hostile, Wary, Neutral, Warm, Trusting }

    public static OpinionBand BandOf(float opinion)
        => opinion <= -30f ? OpinionBand.Hostile
         : opinion <= -10f ? OpinionBand.Wary
         : opinion <   10f ? OpinionBand.Neutral
         : opinion <   30f ? OpinionBand.Warm
         :                   OpinionBand.Trusting;

    public static string BandLabel(OpinionBand b) => b switch
    {
        OpinionBand.Hostile  => "적의",
        OpinionBand.Wary     => "못마땅함",
        OpinionBand.Warm     => "호의",
        OpinionBand.Trusting => "신뢰",
        _                    => "무덤덤",
    };

    // ── 보이는 문장 — 볼 때마다 만든다 ────────────────────────────────────────

    public static string Describe(WorldEvent ev)
    {
        string subject = NameOf(ev, EventRole.Subject);
        string victim  = NameOf(ev, EventRole.Victim);
        string helped  = NameOf(ev, EventRole.Beneficiary);
        string faction = FactionName(ev.TargetFaction);
        string times   = ev.Count > 1 ? $" ({ev.Count}번)" : "";

        return ev.Type switch
        {
            WorldEventType.Ransomed         => $"{helped ?? "붙잡은 사람"}을(를) 몸값 {ev.Amount:N0} G를 받고 돌려보냈다",
            WorldEventType.PrisonerReleased => $"{helped ?? "붙잡은 사람"}을(를) 그냥 놓아 줬다",
            WorldEventType.PawnKilled       => $"{victim ?? "그 세력 사람"}을(를) 죽였다",
            WorldEventType.PawnExecuted     => $"{victim ?? "그 세력 사람"}을(를) 가둬 두고 죽였다",
            WorldEventType.Traded           => $"{faction ?? "상인"}과(와) 거래했다 — {ev.Amount:N0} G{times}",
            WorldEventType.Hired            => $"{subject ?? "누군가"}을(를) 고용했다",
            WorldEventType.Converted        => $"{subject ?? "누군가"}이(가) 우리 편이 됐다",
            WorldEventType.LairCleared      => $"{faction ?? "누군가"}의 소굴이 비었다",
            WorldEventType.SiteClaimed      => $"{FactionName(ev.Actor) ?? "누군가"}이(가) {(faction != null ? faction + "의 " : "")}빈 소굴을 차지했다",
            _                               => ev.Type.ToString(),
        };
    }

    static string NameOf(WorldEvent ev, EventRole role)
    {
        foreach (var p in ev.Involved)
            if (p.Role == role && PawnRegistry.TryGet(p.PawnId, out var rec) && rec.Spec != null)
                return rec.Spec.Name;
        return null;
    }

    public static string FactionName(string id)
        => id != null && _factionNames.TryGetValue(id, out var n) ? n : id;
}

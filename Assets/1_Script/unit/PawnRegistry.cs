using System;
using System.Collections.Generic;
using UnityEngine;

// **폰 기록 — 사라져도 남는 신원**(2026-11-18, `docs/design_world_history.md` 3절 · 11절 1단계).
//
// 폰(`UnitBrain`)은 청크를 벗어나면 파괴되고 무리는 멀어지면 토큰으로 접힌다. 그동안 *"그 사람"*은 어디에도 없었다 —
// 그래서 **죽인 마을 사람이 청크를 다시 밟으면 다시 섰다.** 여기가 그 사람이 남는 자리다.
//
// ## 실체 ↔ 기록 — 몸이 진짜인 곳은 늘 하나
// - `UnitBrain.Start` → `Attach` — 기록이 있으면 몸(`Body`)을 입힌다, 없으면 새로 만든다. 처지는 `Live`.
//   이때부터 몸의 진짜는 `UnitBrain`이라 기록의 `Body`는 비운다(낡은 사본이 남지 않게).
// - `UnitBrain.OnDestroy` → `Detach` — 몸을 기록에 다시 적고, 부르는 쪽이 넘긴 이유(`UnitBrain.Despawn`)로 처지를 정한다.
//   이유 없이 파괴되면 `Unknown`으로 적고 경고한다 — 조용히 틀리지 않게.
// - `UnitBrain.BecomeCorpse` → `MarkDead` — 기록은 지우지 않는다. **`Dead`는 되돌리지 않는다**(시체가 치워져도 · 일으켜져도).
// - `OnDisable`에는 걸지 않는다 — 원정 · 비활성 정착지는 폰을 끄기만 한다. 꺼진 동안에도 그 폰이 진짜다.
//
// ## 세이브 규약(설계 10절)
// 기록에는 **오브젝트 참조가 없다** — 특성 · 출신 · 아이템 · 세력 · 종족은 에셋 Id(`DataId.Resolve`)로만 적는다.
// 그래서 나중의 세이브는 `Records` + `NextSerial`을 `JsonUtility`로 쓰는 것으로 끝난다(필드가 전부 직렬화되는 꼴이다 —
// `Nullable`·딕셔너리를 넣지 말 것).
//
// **기록하는 것은 사람뿐이다**(`UnitData.UsesBackstory`) — 짐승 · 이름 없는 몬스터는 안 적는다.
// 정리(`Dead` · `Departed` 기록 지우기)는 아직 안 한다(설계 3-5 — 사건 기록이 생긴 뒤에).
public enum PawnStatus
{
    Live,       // 실체가 서 있다(꺼져 있어도) — 몸의 진짜는 `UnitBrain`
    InBand,     // 무리 토큰 속에 접혀 있다(`BandMaterializer`)
    AtSite,     // 제 자리(저작된 자리)에 있는데 청크가 내려갔다
    Departed,   // 떠났다(맵 밖으로 · 떠돌이 떼가 거둬짐 · 무리에서 떨어져 나감)
    Colonist,   // 내 사람이 됐다
    Prisoner,   // 붙잡혀 있다
    Dead,
    Unknown,    // 이유 없이 파괴됐다 — 그 경로가 이유를 안 넘겼다(경고가 같이 뜬다)
}

// 태생 — 굴린 명세(`PawnSpec`)의 Id판. **굴린 값은 다시 굴리지 않고 적는다**(같은 시드라도 굴리는 코드가 바뀌면 딴사람이 된다).
[Serializable]
public class PawnOrigin
{
    public int    Seed;
    public string Name;
    public string RaceId;          // `UnitData`
    public bool   HasGender;       // `Gender?` 대신 둘 — `JsonUtility`는 `Nullable`을 못 쓴다
    public Gender Gender;
    public float  BodyScale = 1f;
    public bool   Wanderer;
    public string LoadoutId;       // `PawnSpec.LoadoutOverride`
    public List<GradeEntry>   Grades   = new();
    public List<string>       TraitIds = new();
    public string ChildhoodId;
    public string AdulthoodId;
    public List<PassionEntry> Passions = new();

    [Serializable] public struct GradeEntry   { public StatName Stat; public StatGrade Grade; }
    [Serializable] public struct PassionEntry { public StatName Stat; public Passion Passion; }
}

// 몸 — 부상 · 스탯(경험치) · 소지품 · 장비. **실체가 없을 때만 여기가 진짜다**(`Attach`가 입히고 비운다).
[Serializable]
public class PawnBodyState
{
    public float Blood;
    public float BloodCap;
    public List<PartEntry> Parts = new();
    public List<StatEntry> Stats = new();
    public List<ItemEntry> Pack  = new();   // 배낭 — 격자 자리째
    public List<ItemEntry> Worn  = new();   // 차고 있는 것 — `Slot`이 어디에

    [Serializable] public struct PartEntry { public BodyPartType Part; public float Hp; }
    [Serializable] public struct StatEntry { public StatName Stat; public float Value; }

    public enum WornSlot { Pack, MainHand, OffHand, Armor }   // 꼬리에만 붙일 것(정수 직렬화)

    [Serializable]
    public struct ItemEntry
    {
        public string      ItemId;
        public int         Quantity;
        public ItemQuality Quality;
        public float       Left;
        public bool        Locked;
        public int         X, Y;
        public bool        Rotated;
        public WornSlot    Slot;
    }
}

[Serializable]
public class PawnRecord
{
    public string        Id;
    public PawnOrigin    Spec;
    public PawnBodyState Body;          // 실체가 있는 동안은 null — 그때는 `UnitBrain`이 진짜다
    public PawnStatus    Status;
    public string        FactionId;     // 지금 세력(전향하면 다음 `Detach`/`Attach`에서 바뀐다)
    public string        HomeSiteId;    // 어디 사람인가 — 무리는 소굴(`WorldBand.HomeId`) · 비면 떠돌이
    public Vector3       LastPosition;
    public int           CreatedDay;
}

public static class PawnRegistry
{
    // **id → 기록.** 세이브할 때는 값 목록만 쓴다(키는 `Id`에 들어 있다).
    static readonly Dictionary<string, PawnRecord> _records = new();
    static int  _nextSerial = 1;   // `p:<일련번호>` — 세이브 대상
    static bool _quitting;

    // **지금 서 있는 몸** — 판 안에서만 드는 짝(저장하지 않는다). 앎(`WorldHistory.Knows`)이 *"지금 어디 있나 ·
    // 지금 어느 세력인가"*를 물을 때 쓴다 — 기록의 `LastPosition` · `FactionId`는 내려갈 때 적은 값이라 낡았다.
    static readonly Dictionary<string, UnitBrain> _live = new();

    public static IReadOnlyCollection<PawnRecord> Records => _records.Values;
    public static int Count      => _records.Count;
    public static int NextSerial => _nextSerial;

    public const string PlacePrefix  = "place:";
    public const string RolledPrefix = "p:";

    // **도메인 리로드를 꺼 둔 에디터에서는 정적 값이 판을 넘어 남는다** — 지난 판의 `Dead`가 이번 판의 마을 사람을 막는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _records.Clear();
        _live.Clear();
        _nextSerial = 1;
        _quitting   = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    // 판을 닫을 때(Play 끄기 포함) 모든 폰이 이유 없이 파괴된다 — 그건 사건이 아니다.
    static void OnQuitting() => _quitting = true;

    public static bool TryGet(string id, out PawnRecord rec)
    {
        rec = null;
        return !string.IsNullOrEmpty(id) && _records.TryGetValue(id, out rec);
    }

    // **이 처지면 그 자리에 다시 세우지 않는다**(`WorldContentStreamer.Placed`).
    // `Live`도 막는다 — 몸이 이미 어딘가에 서 있다(데려온 마을 사람 · 업혀 간 포로). 세우면 같은 사람이 둘이 된다.
    public static bool BlocksRespawn(string id)
    {
        if (!TryGet(id, out var rec)) return false;
        switch (rec.Status)
        {
            case PawnStatus.Dead:
            case PawnStatus.Departed:
            case PawnStatus.Colonist:
            case PawnStatus.Live:
                return true;
            default:
                return false;
        }
    }

    // 그 사람의 실체(서 있으면) — 없으면 null.
    public static UnitBrain LiveOf(string id)
        => !string.IsNullOrEmpty(id) && _live.TryGetValue(id, out var b) && b != null ? b : null;

    // 지금 자리 — 서 있으면 실체, 아니면 마지막으로 적은 자리.
    public static Vector3 PositionOf(PawnRecord rec)
    {
        var b = LiveOf(rec.Id);
        return b != null ? b.transform.position : rec.LastPosition;
    }

    // 지금 세력 — 서 있으면 실체의 것(전향하면 바로 바뀐다), 아니면 기록의 것.
    public static string FactionIdNow(PawnRecord rec)
    {
        var b = LiveOf(rec.Id);
        return b != null ? FactionIdOf(b) : rec.FactionId;
    }

    public static bool Tracks(UnitBrain b) => b != null && b.Data != null && b.Data.UsesBackstory;

    // 처지별 개수 — 디버그 창 · 로그.
    public static int[] CountByStatus()
    {
        var n = new int[Enum.GetValues(typeof(PawnStatus)).Length];
        foreach (var r in _records.Values) n[(int)r.Status]++;
        return n;
    }

    public static string Summary()
    {
        var n = CountByStatus();
        var sb = new System.Text.StringBuilder();
        sb.Append($"[폰 기록] {_records.Count}명 (다음 일련번호 {_nextSerial})");
        for (int i = 0; i < n.Length; i++)
            if (n[i] > 0) sb.Append($" · {(PawnStatus)i} {n[i]}");
        return sb.ToString();
    }

    // ── 연결 지점 셋 ────────────────────────────────────────────────────────

    // `UnitBrain.Start`. 몸을 기록에서 입혔으면 true — 그러면 부르는 쪽은 장비 · 주머니를 새로 굴리지 않는다.
    public static bool Attach(UnitBrain b)
    {
        if (!Tracks(b)) return false;

        string id = b.Spec != null ? b.Spec.Id : null;
        if (string.IsNullOrEmpty(id))
        {
            id = RolledPrefix + _nextSerial++;
            // **명세에 적어 둔다** — 무리 멤버의 명세는 토큰(`BandMember.Spec`)과 같은 객체라, 다시 세울 때 같은 기록을 찾는다.
            if (b.Spec != null) b.Spec.Id = id;
        }
        b.PawnId = id;

        bool restored = false;
        if (_records.TryGetValue(id, out var rec))
        {
            if (rec.Status == PawnStatus.Live)
                Debug.LogWarning($"[폰 기록] {b.UnitName}({id}): 이미 서 있는 기록에 몸이 하나 더 섰다 — 같은 사람이 둘이다.", b);
            else if (rec.Status == PawnStatus.Dead)
                Debug.LogWarning($"[폰 기록] {b.UnitName}({id}): 죽은 기록의 사람이 다시 섰다 — 세운 자리가 처지를 안 물었다.", b);

            if (rec.Body != null) restored = ApplyBody(b, rec.Body);
            rec.Body = null;   // 이제 몸의 진짜는 실체다
        }
        else
        {
            rec = new PawnRecord
            {
                Id         = id,
                Spec       = OriginOf(b),
                HomeSiteId = b.Spec != null ? b.Spec.HomeId : null,
                CreatedDay = GameTimeManager.CurrentDay,
            };
            _records[id] = rec;
        }

        rec.Status       = PawnStatus.Live;
        _live[id]        = b;
        rec.FactionId    = FactionIdOf(b);
        rec.LastPosition = b.transform.position;
        return restored;
    }

    // `UnitBrain.OnDestroy`. `reason`은 `UnitBrain.Despawn`이 넘긴 것 — 없으면 null.
    public static void Detach(UnitBrain b, PawnStatus? reason)
    {
        if (_quitting || string.IsNullOrEmpty(b.PawnId)) return;
        if (_live.TryGetValue(b.PawnId, out var live) && live == b) _live.Remove(b.PawnId);
        if (!_records.TryGetValue(b.PawnId, out var rec)) return;

        rec.LastPosition = b.transform.position;

        // **죽음은 되돌리지 않는다** — 시체가 치워지거나(도축 · 매장 · 사라짐) 일으켜진 몸이 내려가도 그 사람은 죽었다.
        if (rec.Status == PawnStatus.Dead || b.IsDead) { rec.Status = PawnStatus.Dead; return; }

        // 씬이 내려가는 중이면(판 닫기 · 씬 전환) 그건 사건이 아니다 — 처지를 안 바꾼다.
        if (!b.gameObject.scene.isLoaded) return;

        rec.FactionId = FactionIdOf(b);
        rec.Body      = CaptureBody(b);

        if (reason.HasValue)
        {
            rec.Status = reason.Value;
            return;
        }

        rec.Status = PawnStatus.Unknown;
        Debug.LogWarning($"[폰 기록] {b.UnitName}({rec.Id})이(가) 이유 없이 파괴됐다 — `Unknown`으로 적는다. " +
                         $"자리 {b.transform.position}. 이 폰을 없앤 경로가 `UnitBrain.Despawn(이유)`을 안 거쳤다.", b);
    }

    // `UnitBrain.BecomeCorpse`.
    public static void MarkDead(UnitBrain b)
    {
        if (string.IsNullOrEmpty(b.PawnId) || !_records.TryGetValue(b.PawnId, out var rec)) return;
        rec.Status       = PawnStatus.Dead;
        rec.Body         = null;   // 몸은 시체가 든다(`Corpse`) — 사람의 몸으로 다시 입힐 일이 없다
        rec.LastPosition = b.transform.position;
    }

    // ── 떠난 사람이 돌아온다(설계 9절 · 4단계) ────────────────────────────────

    // **다시 찾아올 사람을 뽑는다** — 떠났고(`Departed`) · 지금 서 있지 않고 · 같은 종족이고 · **플레이어와 얽힌 사건이
    // 있는** 사람(`WorldHistory` — 풀어 준 포로 · 몸값으로 돌려보낸 사람 · 계약이 끝난 용병 · 자주 거래한 상인).
    // 얽힌 일이 없는 사람은 돌아와도 아무도 못 알아본다 — 새로 굴리는 것과 다를 게 없다.
    public static PawnRecord PickReturning(UnitData race)
    {
        if (race == null) return null;
        string raceId = DataId.Resolve(race, race.Id);
        var pool = new List<PawnRecord>();
        foreach (var r in _records.Values)
        {
            if (r.Status != PawnStatus.Departed || r.Spec == null || r.Spec.RaceId != raceId) continue;
            if (LiveOf(r.Id) != null || !WorldHistory.SharesHistoryWithPlayer(r.Id)) continue;
            pool.Add(r);
        }
        return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : null;
    }

    // **기록에서 명세를 다시 짓는다** — 굴리지 않고 적어 둔 값을 그대로(같은 이름 · 등급 · 특성 · 배경 · 열정 · 성별).
    // 신원(`Id`)이 실려 가므로 `Attach`가 같은 기록을 찾아 몸(부상 · 지닌 것)을 입힌다.
    // 소지(`LoadoutId`)는 되살리지 않는다 — 몸 기록이 지닌 것을 그대로 입히고, 그때는 소지를 새로 굴리지 않는다.
    public static PawnSpec SpecOf(PawnRecord rec)
    {
        if (rec?.Spec == null) return null;
        var o = rec.Spec;
        var spec = new PawnSpec
        {
            Id        = rec.Id,
            HomeId    = rec.HomeSiteId,
            Seed      = o.Seed,
            Name      = o.Name,
            BodyScale = o.BodyScale,
            Wanderer  = o.Wanderer,
            Gender    = o.HasGender ? o.Gender : (Gender?)null,
            Childhood = FindStory(o.ChildhoodId),
            Adulthood = FindStory(o.AdulthoodId),
        };
        foreach (var g in o.Grades)   spec.Grades[g.Stat]   = g.Grade;
        foreach (var p in o.Passions) spec.Passions[p.Stat] = p.Passion;
        foreach (var id in o.TraitIds)
        {
            var t = FindTrait(id);
            if (t != null) spec.Traits.Add(t);
        }
        spec.RecomputeAverage();
        return spec;
    }

    static TraitData FindTrait(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var t in TraitPool.All) if (t != null && DataId.Resolve(t, null) == id) return t;
        return null;
    }

    static BackstoryData FindStory(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var b in BackstoryPool.All) if (b != null && DataId.Resolve(b, null) == id) return b;
        return null;
    }

    // ── 태생 · 몸 ────────────────────────────────────────────────────────────

    static string FactionIdOf(UnitBrain b)
    {
        if (!b.TryGetComponent(out Faction f) || f.Data == null) return null;
        return DataId.Resolve(f.Data, null);
    }

    static PawnOrigin OriginOf(UnitBrain b)
    {
        var s = b.Spec;
        var o = new PawnOrigin
        {
            Seed      = s != null ? s.Seed : 0,
            Name      = b.UnitName,
            RaceId    = b.Data != null ? DataId.Resolve(b.Data, b.Data.Id) : null,
            BodyScale = s != null ? s.BodyScale : 1f,
            Wanderer  = s != null && s.Wanderer,
            LoadoutId = s != null && s.LoadoutOverride != null ? DataId.Resolve(s.LoadoutOverride, null) : null,
        };

        var g = s != null && s.Gender.HasValue ? s.Gender : b.Gender;
        o.HasGender = g.HasValue;
        if (g.HasValue) o.Gender = g.Value;

        // 명세가 있으면 명세의 것(태생), 없으면 지금 몸에 붙은 것 — 명세 없는 폰도 `Awake`가 특성 · 배경을 굴려 둔다.
        if (s != null)
        {
            foreach (var kv in s.Grades)   o.Grades.Add(new PawnOrigin.GradeEntry   { Stat = kv.Key, Grade = kv.Value });
            foreach (var kv in s.Passions) o.Passions.Add(new PawnOrigin.PassionEntry { Stat = kv.Key, Passion = kv.Value });
        }
        var traits = s != null ? (IReadOnlyList<TraitData>)s.Traits : b.Traits;
        if (traits != null)
            foreach (var t in traits) if (t != null) o.TraitIds.Add(DataId.Resolve(t, null));

        var child = s != null && s.Childhood != null ? s.Childhood : b.Childhood;
        var adult = s != null && s.Adulthood != null ? s.Adulthood : b.Adulthood;
        o.ChildhoodId = child != null ? DataId.Resolve(child, null) : null;
        o.AdulthoodId = adult != null ? DataId.Resolve(adult, null) : null;
        return o;
    }

    static string ItemIdOf(ItemData d) => d != null ? DataId.Resolve(d, d.Id) : null;

    static PawnBodyState.ItemEntry Entry(ItemInstance i, PawnBodyState.WornSlot slot) => new()
    {
        ItemId = ItemIdOf(i.Data), Quantity = i.Quantity, Quality = i.Quality, Left = i.Left,
        Locked = i.Locked, X = i.GridX, Y = i.GridY, Rotated = i.Rotated, Slot = slot,
    };

    static PawnBodyState CaptureBody(UnitBrain b)
    {
        var body = new PawnBodyState();

        if (b.TryGetComponent(out BodyPartSystem parts))
        {
            body.Blood    = parts.Blood;
            body.BloodCap = parts.BloodCap;
            foreach (BodyPartType t in Enum.GetValues(typeof(BodyPartType)))
                if (parts.HasPart(t)) body.Parts.Add(new PawnBodyState.PartEntry { Part = t, Hp = parts.PartHP(t) });
        }

        if (b.Stats != null)
            foreach (StatName s in Enum.GetValues(typeof(StatName)))
                body.Stats.Add(new PawnBodyState.StatEntry { Stat = s, Value = b.Stats.GetStat(s) });

        if (b.Inventory != null)
            foreach (var i in b.Inventory.Items)
                if (i?.Data != null) body.Pack.Add(Entry(i, PawnBodyState.WornSlot.Pack));

        var eq = b.Equipment;
        if (eq != null)
        {
            if (eq.MainHandInstance?.Data != null) body.Worn.Add(Entry(eq.MainHandInstance, PawnBodyState.WornSlot.MainHand));
            if (eq.OffHandInstance?.Data  != null) body.Worn.Add(Entry(eq.OffHandInstance,  PawnBodyState.WornSlot.OffHand));
            foreach (ArmorSlot slot in Enum.GetValues(typeof(ArmorSlot)))
            {
                var a = eq.ArmorInstance(slot);
                if (a?.Data != null) body.Worn.Add(Entry(a, PawnBodyState.WornSlot.Armor));
            }
        }
        return body;
    }

    // 기록의 몸을 입힌다 — `Start`에서 장비 · 주머니를 굴리기 **전에**. 못 찾은 아이템 Id는 알리고 건너뛴다.
    static bool ApplyBody(UnitBrain b, PawnBodyState body)
    {
        if (b.Stats != null)
            foreach (var s in body.Stats) b.Stats.SetStat(s.Stat, s.Value);

        if (b.TryGetComponent(out BodyPartSystem parts) && body.Parts.Count > 0)
        {
            var hp = new List<(BodyPartType, float)>(body.Parts.Count);
            foreach (var p in body.Parts) hp.Add((p.Part, p.Hp));
            parts.RestoreState(body.Blood, body.BloodCap, hp);
        }

        var inv = b.Inventory;
        if (inv != null)
        {
            inv.Clear();
            foreach (var e in body.Pack)
            {
                var data = Resolve(b, e.ItemId);
                if (data == null) continue;
                if (!inv.TryAddAt(data, e.Quantity, e.X, e.Y, e.Rotated, e.Quality, e.Locked, e.Left))
                    inv.TryAdd(data, e.Quantity, e.Quality);   // 격자가 바뀌었으면 빈자리에
            }
        }

        var eq = b.Equipment;
        if (eq != null)
            foreach (var e in body.Worn)
            {
                var data = Resolve(b, e.ItemId);
                if (data == null) continue;
                var inst = new ItemInstance(data, 1, 0, 0, false, e.Quality) { Left = e.Left, Locked = e.Locked };
                bool ok = e.Slot switch
                {
                    PawnBodyState.WornSlot.MainHand => eq.WearMainHand(inst),
                    PawnBodyState.WornSlot.OffHand  => eq.WearOffHand(inst),
                    PawnBodyState.WornSlot.Armor    => eq.WearArmor(inst),
                    _ => false,
                };
                if (!ok && inv != null) inv.TryAdd(data, 1, e.Quality);   // 못 차면 배낭으로 — 증발시키지 않는다
            }
        return true;
    }

    static ItemData Resolve(UnitBrain b, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var data = DataManager.GetItem(id);
        if (data == null)
            Debug.LogWarning($"[폰 기록] {b.UnitName}: 기록의 아이템 `{id}`을(를) 못 찾아 건너뛴다.", b);
        return data;
    }
}

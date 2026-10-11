using System.Collections.Generic;
using UnityEngine;

// **마을 — 처음 닿으면 경비병이 맞는다**(2026-10-01, 사용자: *"처음 발견하면 경비병과 대화 시스템이 뜨고 적대는 뭐
// 가라고 뭐라하고 중립이나 동맹은 들어와도 된다고 하는거지"* · *"첫 마을을 발견시 상단 시스템이 오픈되면 좋겠어"*).
//
// 마을 프리팹(`Site_Town`)에는 이미 경비병·상인·마을 사람이 자식으로 들어 있다 — 여기서 새로 세우지 않고 **그중
// 경비병이 말을 건다.** 마을의 세력도 그 사람들이 답한다(경비병 → 아무 주민 → 바이옴의 `TownFaction` 순). 지도 위
// 마을의 세력을 따로 적어 두면 *"말 거는 경비병은 변방민인데 마을은 마을 사람 세력"*이 생긴다.
//
// ## 발견은 한 번이다
//
// 내 폰이 `discoverRadius` 안에 들고 **그 자리가 보일 때**(`FogOfWar.IsVisible` — 안개 너머에서 닿은 것은 닿은 게
// 아니다) 한 번. 대화창이 이미 떠 있으면(다른 대화 중) **다음 박으로 미룬다** — 겹쳐 띄우면 앞의 대화가 사라진다.
// 발견한 마을은 신원(`WorldSites` Id)으로 기억한다 — 판을 다시 열면 비워진다(세이브가 아직 없다).
//
// **첫 마을이 상단을 연다**(`Caravans.Unlock`) — 세력이 적대여도 연다. 연 것은 *"교역이라는 것이 있다"*이고, 그
// 마을과 거래가 되느냐는 상단이 갈 때 다시 묻는다.
[DisallowMultipleComponent]
public class TownSite : MonoBehaviour
{
    public static readonly List<TownSite> All = new();
    static readonly HashSet<string> _discovered = new();

    [Tooltip("내 폰이 이 안(수평 m)에 들면 마을을 찾은 것이다.")]
    [SerializeField] float discoverRadius = 26f;

    public string      SiteId  { get; private set; }
    public string      Name    { get; private set; }
    public FactionData Faction { get; private set; }

    public bool Discovered => SiteId != null && _discovered.Contains(SiteId);

    public static bool IsDiscovered(string siteId) => siteId != null && _discovered.Contains(siteId);

    public static event System.Action<TownSite> OnDiscovered;

    float _nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _discovered.Clear(); OnDiscovered = null; }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        // 신원은 구운 장소 색인에서 — 마을은 굽기가 발급한 Id로 상점·인력이 매여 있다(`TownMarket`).
        var site = WorldSites.Nearest(WorldNodeContent.Town, transform.position);
        if (site.IsValid && (new Vector2(site.Position.x - transform.position.x,
                                         site.Position.y - transform.position.z)).sqrMagnitude < 25f)
        {
            SiteId = site.Id;
            Name   = string.IsNullOrEmpty(site.Name) ? "마을" : site.Name;
        }
        else
        {
            // 색인에 없으면(저작 씬에서 막 놓은 마을) 자리로 신원을 대신한다 — 발견을 기억할 열쇠는 있어야 한다.
            SiteId = $"town@{Mathf.RoundToInt(transform.position.x)},{Mathf.RoundToInt(transform.position.z)}";
            Name   = "마을";
        }

        Faction = ResolveFaction();
    }

    FactionData ResolveFaction()
    {
        FactionData any = null;
        foreach (var b in GetComponentsInChildren<UnitBrain>(true))
        {
            var f = b.GetComponent<Faction>();
            if (f == null || f.Data == null) continue;
            if (b.Npc != null && b.Npc.NpcType == NpcType.Guard) return f.Data;
            any ??= f.Data;
        }
        if (any != null) return any;

        var data = BiomeContent.Get(WorldMap.BiomeAtWorld(new Vector2(transform.position.x, transform.position.z)));
        return data != null ? data.TownFaction : null;
    }

    void Update()
    {
        if (Discovered || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;

        var visitor = NearestVisitor();
        if (visitor == null) return;
        if (!FogOfWar.IsVisible(transform.position) && !FogOfWar.IsVisible(visitor.transform.position)) return;

        var ui = DialogueUI.Instance;
        if (ui != null && ui.IsOpen) return;   // 다른 대화 중 — 다음 박에

        Discover(visitor);
    }

    UnitBrain NearestVisitor()
    {
        UnitBrain best = null;
        float bestSqr = discoverRadius * discoverRadius;
        var c = transform.position;

        foreach (var f in global::Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;
            var b = f.GetComponent<UnitBrain>();
            if (b == null || b.Blackboard == null || b.Blackboard.IsPrisoner || b.Blackboard.CarriedBy != null) continue;
            if (PawnCarry.IsCorpse(b)) continue;

            var p = b.transform.position;
            float d = (p.x - c.x) * (p.x - c.x) + (p.z - c.z) * (p.z - c.z);
            if (d < bestSqr) { bestSqr = d; best = b; }
        }
        return best;
    }

    void Discover(UnitBrain visitor)
    {
        _discovered.Add(SiteId);
        FactionRelations.Meet(Faction);

        bool firstTown = !Caravans.Unlocked;
        Caravans.Unlock();

        OpenGreeting(visitor, firstTown);
        OnDiscovered?.Invoke(this);
    }

    // ── 경비병의 첫 마디 ───────────────────────────────────────────────────

    UnitBrain Guard()
    {
        UnitBrain fallback = null;
        foreach (var b in GetComponentsInChildren<UnitBrain>(false))
        {
            if (b.Blackboard != null && (b.Blackboard.IsKnockedOut || b.Blackboard.IsPrisoner)) continue;
            if (PawnCarry.IsCorpse(b)) continue;
            if (b.Npc != null && b.Npc.NpcType == NpcType.Guard) return b;
            fallback ??= b;
        }
        return fallback;
    }

    FactionStance StanceToPlayer()
    {
        var player = global::Faction.Player;
        if (Faction == null || player == null || player.Data == null) return FactionStance.Neutral;
        return FactionRelations.StanceBetween(Faction, player.Data);
    }

    void OpenGreeting(UnitBrain visitor, bool firstTown)
    {
        var ui = DialogueUI.Instance;
        if (ui == null) return;

        var guard = Guard();
        string who = guard != null ? guard.DisplayTitle : Name;
        var face   = guard != null ? guard.EnsurePortrait() : null;
        string town = Faction != null ? $"{Name} ({Faction.FactionName})" : Name;

        string line;
        var opts = new List<PawnMenuOption>();
        switch (StanceToPlayer())
        {
            case FactionStance.Hostile:
                line = $"멈춰라! {town}에 너희 같은 놈들이 발 디딜 자리는 없다. 더 다가오면 베겠다 - 썩 꺼져라.";
                opts.Add(("물러난다", (System.Action)(() => ui.Close())));
                break;

            case FactionStance.Ally:
                line = $"오, 반갑소! {town}에 온 것을 환영하오. 우리 사이에 막을 문은 없지. 어서 들어오시오.";
                opts.Add(("들어간다", (System.Action)(() => ui.Close())));
                break;

            default:
                line = $"거기 서시오. 여기는 {town}이오. 칼을 뽑지 않는다면 들어와도 좋소 - 장사라면 언제든 환영이오.";
                opts.Add(("알겠소, 들어가겠소", (System.Action)(() => ui.Close())));
                opts.Add(("다음에 오지", (System.Action)(() => ui.Close())));
                break;
        }

        if (firstTown)
            line += "\n\n<color=#E8C566>첫 마을을 찾았다 - 이제 상단을 꾸려 교역할 수 있다 (T).</color>";

        ui.Open(who, face, line, opts);
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

// **상단(商團) — 짐을 싣고 다른 마을·내 거점과 오간다**(2026-10-01, 사용자 기획):
//
//   · 대표 행상인 한 명(사교가 값을 정한다 — `Trading.HagglerFor`와 같은 축) + 운반·호위 일행 + 수레 단 가축.
//   · 마을과는 **교역**(판다 · 산다), 내 거점과는 **운반**(내려 주고 · 가져온다).
//   · 주기적으로 저절로 떠나거나 손으로 띄운다.
//   · 판매 규칙 — 창고에 그 물건이 정한 수를 **넘으면 넘는 만큼**(초과분) 또는 **정한 수만큼**(정량) 싣는다.
//   · 구매 규칙 — 창고에 정한 수보다 **적으면 그 수까지** 산다.
//   · 우선 구매 — 그 마을이 뭘 파는지 몰라도 **있으면 먼저** 산다.
//
// ## 포탈로 떠난다 — 걸어서 안 간다
//
// 핵이 포탈이다(사용자: *"핵은 포탈 기능을 하게 … 그 포탈은 상단 시스템이 이용할 수도"*). 일행은 **출발 거점의 핵에
// 모였다가 포탈로 들어가** 필드에서 빠지고(원정대가 지도로 나가던 것과 같은 `SetActive(false)`), 정해진 시간 뒤에 같은
// 핵으로 돌아 나온다. 그사이는 **추상**이다 — 그림이 없을 뿐 가짜가 아니다: 짐은 실제 창고에서 빠지고 들어가며, 길에서
// 당한 상처는 몸이 받는다(`WorldBattle.ResolveAmbush` — 원정·습격과 같은 굴림).
//
// **걸리는 시간**: 내 거점끼리는 포탈에서 포탈로(`PortalHop`) · 마을은 **그 마을에서 가장 가까운 내 핵**까지 포탈로
// 가서 나머지를 걷는다(거점을 멀리 세울수록 교역로가 짧아진다) · 층이 다르면 한 층에 `FloorStep`.
public enum CaravanSellMode { Excess, Fixed }

[System.Serializable]
public class CaravanSellRule
{
    public ItemData Item;
    public CaravanSellMode Mode = CaravanSellMode.Excess;
    public int Amount = 50;
}

[System.Serializable]
public class CaravanBuyRule
{
    public ItemData Item;
    public int Target = 20;   // 창고에 이보다 적으면 이 수까지
}

[System.Serializable]
public class CaravanPriorityRule
{
    public ItemData Item;
    public int Max = 5;       // 있으면 이만큼까지 먼저
}

public enum CaravanState { Idle, Gathering, Outbound, Returning }

public enum CaravanDestKind { None, Town, Outpost }

public class Caravan
{
    // ── 눈금 ────────────────────────────────────────────────────────────────
    public const float PortalHopDays = 0.04f;    // 포탈에서 포탈 — 한 시간 남짓
    public const float WalkMetersPerDay = 2200f; // 포탈에서 나와 걷는 길(쉬고 짐 싣는 시간 포함)
    public const float FloorStepDays = 0.25f;    // 층 하나를 오르내리는 데
    public const float GatherTimeoutDays = 0.12f;
    public const float GatherRadius = 6f;

    // ── 설정 ────────────────────────────────────────────────────────────────
    public string Name = "상단";
    public DungeonCore Home;
    public CaravanDestKind DestKind;
    public string      DestTownId;
    public DungeonCore DestCore;
    public UnitBrain   Leader;
    public readonly List<UnitBrain> Members = new();   // 대표를 뺀 일행(사람·가축)

    public bool Auto;
    public int  IntervalDays = 3;
    public int  Budget = 500;

    public readonly List<CaravanSellRule>     Sell     = new();
    public readonly List<CaravanBuyRule>      Buy      = new();
    public readonly List<CaravanPriorityRule> Priority = new();

    // ── 상태 ────────────────────────────────────────────────────────────────
    public CaravanState State { get; private set; }
    public float StateEnds { get; private set; }     // `GameTimeManager.Elapsed` 기준
    public int   LastDepartDay { get; private set; } = int.MinValue / 2;
    public string LastReport { get; private set; } = "";
    public Inventory Cargo { get; private set; }

    readonly List<UnitBrain> _away = new();          // 지금 포탈 너머에 있는 일행(대표 포함)
    readonly Dictionary<UnitBrain, Vector3> _spots = new();   // 모이는 자리
    float _gatherDeadline;
    readonly StringBuilder _log = new();

    public Caravan(Inventory cargo) { Cargo = cargo; }

    public bool IsAway => State == CaravanState.Outbound || State == CaravanState.Returning;
    public IReadOnlyList<UnitBrain> Away => _away;

    // ── 목적지 ──────────────────────────────────────────────────────────────

    public bool HasDestination => DestKind == CaravanDestKind.Town ? !string.IsNullOrEmpty(DestTownId)
                                : DestKind == CaravanDestKind.Outpost && DestCore != null;

    public string DestName
    {
        get
        {
            switch (DestKind)
            {
                case CaravanDestKind.Town:
                    var s = WorldSites.ById(DestTownId);
                    return s.IsValid ? (string.IsNullOrEmpty(s.Name) ? "마을" : s.Name) : "(사라진 마을)";
                case CaravanDestKind.Outpost:
                    int i = DestCore != null ? DungeonCore.All.IndexOf(DestCore) : -1;
                    return i >= 0 ? $"{i + 1} 거점" : "(사라진 거점)";
                default: return "정하지 않음";
            }
        }
    }

    public Vector3 DestPosition => DestKind switch
    {
        CaravanDestKind.Town    => WorldSites.ById(DestTownId).World,
        CaravanDestKind.Outpost => DestCore != null ? DestCore.transform.position : Vector3.zero,
        _                       => Vector3.zero,
    };

    // 몇 층인가 — **자리가 답한다**(`Floors` — 층은 한 평면에 나란히 놓인 사분면이다).
    public int DestFloor => HasDestination ? Floors.At(DestPosition) : HomeFloor;
    public int HomeFloor => Home != null ? Floors.At(Home.transform.position) : 1;

    public void SetTown(string siteId)  { DestKind = CaravanDestKind.Town;    DestTownId = siteId; DestCore = null; }
    public void SetOutpost(DungeonCore c) { DestKind = CaravanDestKind.Outpost; DestCore = c;       DestTownId = null; }

    // 마을에서 가장 가까운 내 핵 — 포탈에서 나오는 자리. **같은 층의 핵만**(층 사이는 기반암이라 다른 층의 핵에서 나와서는
    // 걸어갈 수 없다). 없으면 `null` — 그 층의 통로에서 걷는다(`LegDays`).
    public static DungeonCore ExitPortalFor(Vector3 at)
    {
        DungeonCore best = null;
        float bestSqr = float.MaxValue;
        int floor = Floors.At(at);
        foreach (var c in DungeonCore.All)
        {
            if (c == null || Floors.At(c.transform.position) != floor) continue;
            float d = (c.transform.position - at).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = c; }
        }
        return best;
    }

    // 한쪽 길에 걸리는 날수.
    public float LegDays()
    {
        if (!HasDestination || Home == null) return 0f;
        float days = 0f;

        // **포탈은 층을 가리지 않는다** — 내 핵끼리는 몇 층이든 한 번에 넘는다(그것이 다른 층에 거점을 세우는 값이다).
        // 목적지 층에 내 핵이 없으면 **통로로 층을 하나씩 오르내리고**(`FloorStepDays`) 그 층의 통로에서 걷는다.
        if (DestKind == CaravanDestKind.Outpost) days = PortalHopDays;
        else
        {
            var at   = DestPosition;
            var exit = ExitPortalFor(at);
            Vector3 from;
            if (exit != null)
            {
                if (exit != Home) days += PortalHopDays;
                from = exit.transform.position;
            }
            else
            {
                days += Mathf.Abs(DestFloor - HomeFloor) * FloorStepDays;
                from = EntryOf(DestFloor, at, Home.transform.position);
            }
            days += Vector2.Distance(new Vector2(from.x, from.z), new Vector2(at.x, at.z)) / WalkMetersPerDay;
        }

        // **수레가 느리면 다 같이 느리다** — 일행은 가장 느린 걸음에 맞춘다.
        float slow = 1f;
        foreach (var m in Party())
        {
            var cart = m != null ? m.GetComponent<PawnCart>() : null;
            if (cart != null && cart.Data != null) slow = Mathf.Min(slow, cart.Data.SpeedMult);
        }
        return days / Mathf.Max(0.3f, slow);
    }

    // 그 층에 들어서는 자리 — 같은 층이면 출발 자리 그대로, 다른 층이면 그 층의 통로 중 목적지에 가까운 것.
    static Vector3 EntryOf(int floor, Vector3 near, Vector3 home)
    {
        if (Floors.At(home) == floor) return home;
        Vector3 best = Floors.Center(floor);
        float bestSqr = float.MaxValue;
        foreach (var p in FloorPassage.All)
        {
            if (p == null || p.Floor != floor) continue;
            float d = (p.transform.position - near).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p.transform.position; }
        }
        return best;
    }

    public IEnumerable<UnitBrain> AllMembers()
    {
        if (Leader != null) yield return Leader;
        foreach (var m in Members) if (m != null && m != Leader) yield return m;
    }

    // **길 위에서는 실제로 떠난 사람이 답한다** — 편성 목록은 길에 있는 동안 창에서 고칠 수 있어서, 그것으로
    // 재면 떠난 뒤 넣은 사람의 등짐이 짐칸에 더해진다. 모이는 중에도 `_away`가 답한다(모인 사람만 떠난다).
    IEnumerable<UnitBrain> Party()
    {
        if (State == CaravanState.Idle) return AllMembers();
        return _away;
    }

    public float Capacity
    {
        get
        {
            float kg = 0f;
            foreach (var m in Party()) if (m != null) kg += CarryCapacity.For(m);
            return kg;
        }
    }

    public float CargoWeight => Cargo != null ? Cargo.GetTotalWeight() : 0f;

    // ── 떠날 수 있나 ────────────────────────────────────────────────────────

    public static bool Available(UnitBrain b, Caravan except)
    {
        if (b == null || !b.isActiveAndEnabled) return false;
        var bb = b.Blackboard;
        if (bb == null || bb.IsKnockedOut || bb.IsPrisoner || bb.CarriedBy != null) return false;
        if (PawnCarry.IsCorpse(b)) return false;
        var f = b.GetComponent<Faction>();
        if (f == null || !f.IsPlayer) return false;
        if (b.Data is MinionData) return false;          // 하수인은 핵 곁을 안 떠난다
        return CaravanManager.OwnerOf(b) == null || CaravanManager.OwnerOf(b) == except;
    }

    public string WhyNotDepart()
    {
        if (State != CaravanState.Idle) return "이미 길에 있다";
        if (!Caravans.Unlocked) return "아직 교역할 곳을 모른다 - 마을을 먼저 찾을 것";
        if (Home == null) return "출발 거점을 정할 것";
        if (!HasDestination) return "목적지를 정할 것";
        if (DestKind == CaravanDestKind.Outpost && DestCore == Home) return "출발 거점과 목적지가 같다";
        if (DestKind == CaravanDestKind.Town && !TownSite.IsDiscovered(DestTownId)) return "아직 가 보지 않은 마을이다";
        if (Leader == null) return "대표 행상인을 정할 것";
        // **다 같이 떠난다**(2026-10-06, 사용자: *"상단은 한번에 다 모여서 같이 출발해야지"*) — 한 사람이라도
        // 못 떠나면 출발하지 않는다. 늦은 사람을 두고 가면 짐 한도·걸음이 편성과 어긋나고, 남겨진 가축의 수레가
        // 짐칸에 그대로 더해졌다.
        foreach (var m in AllMembers())
            if (!Available(m, this)) return $"{m.UnitName}이(가) 지금 떠날 수 없다";
        return null;
    }

    // ── 진행 ────────────────────────────────────────────────────────────────

    public bool Depart()
    {
        if (WhyNotDepart() != null) return false;

        _away.Clear();
        _away.AddRange(AllMembers());

        // 핵 둘레에 모인다 — 원정 집결과 같은 명령 규칙(타겟을 지우고 · 일을 놓고 · 움직임만 남긴다).
        var c = Home.transform.position;
        _spots.Clear();
        for (int i = 0; i < _away.Count; i++)
        {
            float a = i * Mathf.PI * 2f / Mathf.Max(1, _away.Count);
            var spot = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.5f;
            _spots[_away[i]] = spot;
            Order(_away[i], spot);
        }

        State = CaravanState.Gathering;
        _gatherDeadline = GameTimeManager.Elapsed + GatherTimeoutDays * GameTimeManager.DayDuration;
        LastDepartDay = GameTimeManager.CurrentDay;
        return true;
    }

    static void Order(UnitBrain brain, Vector3 spot)
    {
        var bb = brain.Blackboard;
        bb.Target     = null;
        bb.ManualMove = true;
        brain.AbandonWork();
        if (brain.TryGetComponent(out CombatHandler combat)) combat.ExitCombat();
        if (brain.TryGetComponent(out PawnMover mover)) mover.SetMoveTarget(spot);
    }

    public void Tick()
    {
        switch (State)
        {
            case CaravanState.Idle:
                if (Auto && GameTimeManager.CurrentDay - LastDepartDay >= Mathf.Max(1, IntervalDays)
                    && GameTimeManager.Hour >= 7 && GameTimeManager.Hour < 16 && WhyNotDepart() == null)
                    Depart();
                break;

            case CaravanState.Gathering: TickGather(); break;

            case CaravanState.Outbound:
                if (GameTimeManager.Elapsed >= StateEnds) Arrive();
                break;

            case CaravanState.Returning:
                if (GameTimeManager.Elapsed >= StateEnds) Return();
                break;
        }
    }

    void TickGather()
    {
        if (Home == null) { Abort("출발 거점이 사라졌다."); return; }

        var c = Home.transform.position;
        bool all = true;
        for (int i = _away.Count - 1; i >= 0; i--)
        {
            var m = _away[i];
            // **한 사람이라도 못 오게 되면 거둔다** — 다 같이 떠나는 것이 규칙이다(쓰러졌다 · 붙잡혔다 · 업혔다).
            if (m == null) { Abort("일행 하나가 사라졌다."); return; }
            if (!Available(m, this)) { Abort($"{m.UnitName}이(가) 올 수 없게 됐다."); return; }
            var p = m.transform.position;
            if ((new Vector2(p.x - c.x, p.z - c.z)).sqrMagnitude > GatherRadius * GatherRadius)
            {
                all = false;
                // **먼저 온 사람이 일하러 가 버리면 다시 부른다** — 닿는 순간 이동 명령이 내려가 BT가 일감을 집는다.
                if (!m.Blackboard.ManualMove && _spots.TryGetValue(m, out var spot)) Order(m, spot);
            }
        }

        if (!all)
        {
            // **늦은 사람을 두고 가지 않는다** — 시간 안에 다 못 모이면 출발을 거둔다(길이 막혔거나 너무 멀다).
            if (GameTimeManager.Elapsed >= _gatherDeadline) Abort("일행이 시간 안에 다 모이지 못했다.");
            return;
        }

        _log.Clear();
        Pack();

        foreach (var m in _away)
        {
            m.Blackboard.ManualMove = false;
            if (m.TryGetComponent(out PawnMover mPawnMover)) mPawnMover.StopMovement();
            m.gameObject.SetActive(false);   // 포탈 너머 — 필드에서 빠진다
        }

        State = CaravanState.Outbound;
        StateEnds = GameTimeManager.Elapsed + LegDays() * GameTimeManager.DayDuration;
        EventNotificationUI.Notify("상단 출발",
            $"{Name}이(가) {DestName}으로(로) 떠났습니다 - 일행 {_away.Count}, 짐 {CargoWeight:F0}/{Capacity:F0}kg.");
    }

    void Abort(string why)
    {
        foreach (var m in _away) if (m != null && m.Blackboard != null) m.Blackboard.ManualMove = false;
        _away.Clear();
        State = CaravanState.Idle;
        LastReport = $"출발 취소 - {why}";
        EventNotificationUI.Notify("상단", $"{Name}: {why} 출발을 거뒀습니다.");
    }

    // ── 싣기 — 판매 규칙대로 출발 거점 창고에서 ─────────────────────────────

    void Pack()
    {
        var at = Home.transform.position;
        float room = Capacity - CargoWeight;

        foreach (var rule in Sell)
        {
            if (rule.Item == null || rule.Amount < 0) continue;
            int stock = Storage.GetTotalByItem(rule.Item, at);
            int want = rule.Mode == CaravanSellMode.Excess ? stock - rule.Amount : Mathf.Min(stock, rule.Amount);
            if (want <= 0) continue;

            int moved = TakeFromStorages(rule.Item, want, at, ref room);
            if (moved > 0) _log.AppendLine($"싣기 {rule.Item.ItemName} {moved}");
        }
    }

    // 창고에서 짐칸으로 — **등급을 지킨다**(장비를 팔 때 값이 등급에서 나온다). 무게가 차면 멈춘다.
    int TakeFromStorages(ItemData item, int want, Vector3 territory, ref float room)
    {
        int moved = 0;
        foreach (var sb in Storage.All)
        {
            if (want <= 0) break;
            if (sb == null || !sb.IsActive || !Territory.Same(sb.transform.position, territory)) continue;

            var inv = sb.Inventory;
            var list = new List<ItemInstance>(inv.Items);
            foreach (var inst in list)
            {
                if (want <= 0) break;
                if (inst == null || inst.Data != item) continue;

                int n = Mathf.Min(want, inst.Quantity);
                if (item.Weight > 0f) n = Mathf.Min(n, Mathf.FloorToInt(room / item.Weight));
                if (n <= 0) return moved;

                var q = inst.Quality;
                int added = Cargo.TryAdd(item, n, q);
                if (added <= 0) continue;
                int removed = inv.Remove(item, added, q);
                if (removed < added) Cargo.Remove(item, added - removed, q);   // 어긋나면 되돌린다

                moved += removed;
                want  -= removed;
                room  -= removed * item.Weight;
            }
        }
        return moved;
    }

    // ── 도착 ────────────────────────────────────────────────────────────────

    void Arrive()
    {
        PruneAway();

        // **길에서 당할 수 있다** — 갈 때 한 번(마을 쪽 길만 — 포탈끼리는 안전하다).
        Ambush("가는 길");

        if (DestKind == CaravanDestKind.Town) TradeAtTown();
        else if (DestKind == CaravanDestKind.Outpost) TransferAtOutpost();

        State = CaravanState.Returning;
        StateEnds = GameTimeManager.Elapsed + LegDays() * GameTimeManager.DayDuration;
    }

    void PruneAway()
    {
        for (int i = _away.Count - 1; i >= 0; i--) if (_away[i] == null) _away.RemoveAt(i);
    }

    void Ambush(string leg)
    {
        if (DestKind != CaravanDestKind.Town || _away.Count == 0) return;

        var at = DestPosition;
        int tier = BiomeTier.Of(WorldMap.BiomeAtWorld(new Vector2(at.x, at.z)));
        float walk = 0f;
        var exit = ExitPortalFor(at);
        var entry = exit != null ? exit.transform.position
                  : EntryOf(Floors.At(at), at, Home != null ? Home.transform.position : at);
        walk = Vector3.Distance(entry, at);

        float chance = Mathf.Clamp01((0.05f + 0.04f * tier) * Mathf.Clamp(walk / 400f, 0.25f, 2f));
        if (Random.value > chance) return;

        float threat = 90f * Random.Range(1.5f, 3.5f) * BiomeTier.PowerMult(tier);
        var r = WorldBattle.ResolveAmbush(_away, threat);
        PruneAway();

        if (r.PlayerWon)
        {
            _log.AppendLine($"{leg}에 습격을 받았으나 물리쳤다 (다친 이 {r.Wounded.Count + r.Down.Count}).");
            return;
        }

        // 지면 짐을 일부 빼앗긴다 — 무거운 것부터가 아니라 고르게(빼앗는 쪽이 고른다).
        float lose = Random.Range(0.3f, 0.6f);
        int lost = 0;
        foreach (var inst in new List<ItemInstance>(Cargo.Items))
        {
            int n = Mathf.FloorToInt(inst.Quantity * lose);
            if (n <= 0) continue;
            lost += Cargo.Remove(inst.Data, n, inst.Quality);
        }
        _log.AppendLine($"{leg}에 습격을 받아 밀렸다 - 짐 {lost}개를 빼앗겼다 (쓰러진 이 {r.Down.Count}).");
    }

    // ── 마을 — 판다 · 산다 ──────────────────────────────────────────────────

    void TradeAtTown()
    {
        // 지도 위 시장(`TownMarket`)에 **그 마을에 실제로 서 있는 상인**의 가게를 더한다 — 저작한 바이옴 상인 풀이
        // 비어 있어도 마을 프리팹의 상인과는 거래가 된다.
        var shops = new List<Shop>(TownMarket.ShopsAt(DestTownId));
        foreach (var t in TownSite.All)
            if (t != null && t.SiteId == DestTownId)
                foreach (var m in t.GetComponentsInChildren<Merchant>(true))
                    if (m != null && m.Shop != null && !shops.Contains(m.Shop)) shops.Add(m.Shop);
        var town = TownFaction();
        var player = Faction.Player;
        if (town != null && player != null && player.Data != null
            && FactionRelations.StanceBetween(town, player.Data) == FactionStance.Hostile)
        {
            _log.AppendLine($"{DestName}이(가) 문을 열지 않았다 (적대).");
            return;
        }
        if (shops.Count == 0) { _log.AppendLine($"{DestName}에 장사하는 사람이 없다."); return; }

        var leader = Leader != null && _away.Contains(Leader) ? Leader : null;
        foreach (var sh in shops) { sh.Haggler = leader; sh.Reprice(); }   // 사는 값도 대표의 말솜씨로(`MerchantUI`와 같은 순서)

        var account = new CaravanAccount(this);
        int startGold = Wallet.Balance;

        // 판다 — 판매 규칙의 물건 전부(실은 것이 곧 팔 것이다).
        var sellItems = new HashSet<ItemData>();
        foreach (var r in Sell) if (r.Item != null) sellItems.Add(r.Item);
        foreach (var inst in new List<ItemInstance>(Cargo.Items))
        {
            if (inst == null || !sellItems.Contains(inst.Data)) continue;
            foreach (var sh in shops)
            {
                if (inst.Quantity <= 0) break;
                if (sh.GetSellPrice(inst) <= 0) continue;
                int sold = sh.TrySell(inst, Cargo, account, inst.Quantity);
                if (sold > 0) _log.AppendLine($"판매 {inst.Data.ItemName} {sold}");
            }
        }
        int earned = Wallet.Balance - startGold;

        // 산다 — 우선 구매가 먼저, 그다음 구매 규칙. **예산은 판 돈과 따로 센다**(쓴 돈만 센다).
        int spent = 0;
        foreach (var r in Priority)
        {
            if (r.Item == null || r.Max <= 0) continue;
            int have = Cargo.GetTotalQuantity(r.Item);
            int got = BuyFrom(shops, r.Item, r.Max - have, account, ref spent);
            if (got > 0) _log.AppendLine($"우선 구매 {r.Item.ItemName} {got}");
        }
        foreach (var r in Buy)
        {
            if (r.Item == null || r.Target <= 0) continue;
            int have = Storage.GetTotalByItem(r.Item, Home != null ? Home.transform.position : DestPosition)
                     + Cargo.GetTotalQuantity(r.Item);
            int got = BuyFrom(shops, r.Item, r.Target - have, account, ref spent);
            if (got > 0) _log.AppendLine($"구매 {r.Item.ItemName} {got}");
        }

        _log.AppendLine($"수입 {earned} G · 지출 {spent} G");

        // 흥정하던 사람을 내려놓는다 — 남겨 두면 다음에 내가 그 상인 앞에 섰을 때 포탈 너머 대표의 말솜씨로 값이 매겨진다.
        foreach (var sh in shops) { sh.Haggler = null; sh.Reprice(); }
    }

    FactionData TownFaction()
    {
        foreach (var t in TownSite.All) if (t != null && t.SiteId == DestTownId) return t.Faction;
        var data = BiomeContent.Get(WorldMap.BiomeAtWorld(new Vector2(DestPosition.x, DestPosition.z)));
        return data != null ? data.TownFaction : null;
    }

    // 가장 싼 진열부터 산다 — 예산·짐칸 무게·상인 재고가 먼저 닿는 데서 멈춘다.
    int BuyFrom(List<Shop> shops, ItemData item, int want, TradeAccount account, ref int spent)
    {
        if (want <= 0) return 0;
        var offers = new List<(Shop shop, MerchantStockEntry entry)>();
        foreach (var sh in shops)
            foreach (var e in sh.Stock)
                if (e != null && e.Item == item && e.Stock > 0 && e.Price > 0) offers.Add((sh, e));
        offers.Sort((a, b) => a.entry.Price.CompareTo(b.entry.Price));

        int got = 0;
        foreach (var (sh, e) in offers)
        {
            if (want <= 0) break;
            int n = Mathf.Min(want, e.Stock);
            int budgetLeft = Budget - spent;
            n = Mathf.Min(n, budgetLeft / Mathf.Max(1, e.Price));
            n = Mathf.Min(n, Mathf.FloorToInt(Wallet.Balance / (float)Mathf.Max(1, e.Price)));
            if (item.Weight > 0f) n = Mathf.Min(n, Mathf.FloorToInt((Capacity - CargoWeight) / item.Weight));
            if (n <= 0) continue;

            int before = Wallet.Balance;
            int bought = sh.TryBuy(e, account, Cargo, n);
            spent += before - Wallet.Balance;
            got  += bought;
            want -= bought;
        }
        return got;
    }

    // ── 내 거점 — 내려 주고 · 가져온다 ─────────────────────────────────────

    void TransferAtOutpost()
    {
        if (DestCore == null) { _log.AppendLine("목적지 거점이 사라졌다."); return; }
        var at = DestCore.transform.position;

        // 내려 준다 — 실은 것 전부(판매 규칙이 곧 *"저리로 보낼 것"*이다).
        int dropped = 0;
        foreach (var inst in new List<ItemInstance>(Cargo.Items))
        {
            if (inst == null) continue;
            int n = inst.Quantity;
            var item = inst.Data; var q = inst.Quality;
            Cargo.Remove(item, n, q);
            Storage.Deliver(Storage.FindNearest(at, item, n, q), item, n, q, null, at);
            dropped += n;
        }
        if (dropped > 0) _log.AppendLine($"{DestName}에 짐 {dropped}개를 내려 주었다.");

        // 가져온다 — 구매 규칙이 곧 *"이리로 가져올 것"*이다(출발 거점이 정한 수까지).
        float room = Capacity - CargoWeight;
        foreach (var r in Buy)
        {
            if (r.Item == null || r.Target <= 0 || Home == null) continue;
            int have = Storage.GetTotalByItem(r.Item, Home.transform.position) + Cargo.GetTotalQuantity(r.Item);
            int need = r.Target - have;
            if (need <= 0) continue;
            int moved = TakeFromStorages(r.Item, need, at, ref room);
            if (moved > 0) _log.AppendLine($"가져옴 {r.Item.ItemName} {moved}");
        }
    }

    // ── 귀환 ────────────────────────────────────────────────────────────────

    void Return() => Return(ambush: true);

    // `ambush` — 길을 실제로 걸어 돌아올 때만 굴린다. 창에서 상단을 지우거나 판을 닫을 때(`ForceReturn`) 데려오는
    // 길에 습격이 돌면 **[삭제]만 눌러도 일행이 다치고 짐이 사라진다.**
    void Return(bool ambush)
    {
        PruneAway();
        if (ambush) Ambush("돌아오는 길");

        var core = Home != null ? Home : (DungeonCore.All.Count > 0 ? DungeonCore.All[0] : null);
        var at = core != null ? core.transform.position : DestPosition;

        for (int i = 0; i < _away.Count; i++)
        {
            var m = _away[i];
            if (m == null) continue;
            float a = i * Mathf.PI * 2f / Mathf.Max(1, _away.Count);
            var spot = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2f;
            m.gameObject.SetActive(true);
            PawnPlacement.PlaceOnGround(m.gameObject, spot);
        }
        _away.Clear();

        // 부린다 — 창고 먼저, 넘치면 핵 옆 바닥(`Storage.Deliver`).
        int n = 0;
        foreach (var inst in new List<ItemInstance>(Cargo.Items))
        {
            if (inst == null) continue;
            int q = inst.Quantity; var item = inst.Data; var qual = inst.Quality;
            Cargo.Remove(item, q, qual);
            Storage.Deliver(Storage.FindNearest(at, item, q, qual), item, q, qual, null, at);
            n += q;
        }
        if (n > 0) _log.AppendLine($"짐 {n}개를 부렸다.");

        State = CaravanState.Idle;
        LastReport = _log.ToString().TrimEnd();
        EventNotificationUI.Notify("상단 귀환", $"{Name}이(가) {DestName}에서 돌아왔습니다.\n{LastReport}", holdWhenHidden: true);
    }

    // 판을 닫거나 상단을 없앨 때 — 포탈 너머 사람을 핵 옆으로 돌려놓는다(짐도).
    public void ForceReturn()
    {
        if (State == CaravanState.Gathering)
        {
            foreach (var m in _away) if (m != null && m.Blackboard != null) m.Blackboard.ManualMove = false;
            _away.Clear();
            State = CaravanState.Idle;
            return;
        }
        if (IsAway) Return(ambush: false);
    }
}

// 상단의 지갑 — **돈은 전부 하나**(`Wallet`)이고, 산 물건은 짐칸으로 간다(`Shop.TryBuy`에 짐칸을 넘기므로 여기서는
// 거의 안 받는다 — 무게가 남을 때만).
public class CaravanAccount : TradeAccount
{
    readonly Caravan _c;
    public CaravanAccount(Caravan c) { _c = c; }
    public override string Label => "상단";

    public override int Receive(ItemData item, int amount, ItemQuality quality)
    {
        if (item == null || amount <= 0 || _c.Cargo == null) return 0;
        if (item.Weight > 0f) amount = Mathf.Min(amount, Mathf.FloorToInt((_c.Capacity - _c.CargoWeight) / item.Weight));
        return amount > 0 ? _c.Cargo.TryAdd(item, amount, quality) : 0;
    }
}

using System.Collections.Generic;
using UnityEngine;

// **누구에게 무슨 말을 걸 수 있나 — 한 곳.**
//
// 우클릭 창은 "무엇을 시킬까"를 묻고, 대화창은 "무슨 말을 할까"를 묻는다. 둘은 다른 물음이지만
// **하는 일은 같은 함수로 흘러야 한다** — 설득도 몸값도 상점 열기도 이미 주인이 있으므로
// (`Conversion` · `WorldRansom` · `Merchant`), 여기서 다시 구현하지 않고 **부르기만** 한다.
// 문이 둘이어도 갈라질 데가 없다(건설 취소가 문 셋에서 `bp.Cancel()` 하나를 부르는 것과 같다).
//
// ── 왜 상대마다 다른가 ─────────────────────────────────────────────────────
//
// 말은 상대가 누구냐로 갈린다. 상인에게는 물건을 묻고, 떠돌이에게는 같이 가자고 하고,
// 붙잡아 둔 자에게는 넘어오라고 한다. **그 판단을 대화창이 하지 않는다** — 창은 받은 것을
// 그릴 뿐이고(`DialogueUI`), 무엇을 띄울지는 여기가 정한다.
public static class Conversation
{
    // 말이 통하는 상대인가. **기절·시체·업힌 사람은 빠진다** — 대답할 수 없는 상대에게
    // 말을 거는 선택지가 뜨면 눌러 놓고 왜 아무 일도 없는지 묻게 된다.
    public static bool CanTalk(UnitBrain target, UnitBrain speaker)
    {
        if (target == null || target == speaker) return false;
        if (PawnCarry.IsCorpse(target)) return false;

        var bb = target.Blackboard;
        if (bb == null || bb.IsKnockedOut || bb.CarriedBy != null) return false;

        // 갇힌 사람과는 말이 된다(설득이 그 일이다). 그 밖에는 **적대가 아닐 때만** —
        // 칼을 뽑고 달려드는 상대와 말을 섞는 것은 다른 게임의 규칙이다.
        if (bb.IsPrisoner) return true;

        var mine = speaker != null ? speaker.GetComponent<Faction>() : null;
        var his  = target.GetComponent<Faction>();
        return mine == null || his == null || !mine.IsHostileTo(his);
    }

    // 말을 건다 — 상대에 맞는 첫 마디를 띄운다.
    public static void Open(UnitBrain target, UnitBrain speaker)
    {
        var ui = DialogueUI.Instance;
        if (ui == null || target == null) return;

        string who  = Title(target);
        var    face = target.EnsurePortrait();
        var    opts = new List<PawnMenuOption>();

        string line = Greeting(target);

        AddMerchant(target, opts);
        AddPrisoner(target, speaker, opts);
        AddRecruit(target, speaker, opts, ref line);
        AddContract(target, speaker, opts, ref line);

        opts.Add(("그만둔다", (System.Action)(() => ui.Close())));
        ui.Open(who, face, line, opts);
    }

    // 창 제목 = **`UnitBrain.DisplayTitle`**(`던컨 (떠돌이 · 변방민)`).
    // 우클릭 창과 **같은 것**을 쓴다 — 창마다 따로 만들면 같은 폰이 창마다 다르게 불린다.
    static string Title(UnitBrain u) => u.DisplayTitle;

    // 첫 마디. **상대가 무엇인지가 말투로 드러나야** 선택지를 읽기 전에 감이 온다.
    static string Greeting(UnitBrain u)
    {
        if (u.Blackboard != null && u.Blackboard.IsPrisoner)
            return "…무슨 낯짝으로 말을 거는 거냐.";

        if (u.GetComponent<Merchant>() != null)
            return "쓸 만한 물건이 있소. 천천히 보시오.";

        if (u.Npc != null && u.Npc.NpcType == NpcType.Drifter)
            return "정처 없이 떠도는 중이오. 어디로 가든 상관은 없소만.";

        return "무슨 일이오?";
    }

    // ── 상인 ──────────────────────────────────────────────────────────────────
    //
    // **좌클릭이 바로 상점을 여는 길은 그대로 둔다** — 자주 하는 일에 한 단계를 더 얹으면
    // 그건 분위기가 아니라 손해다. 대화는 *다른 길*이지 대체가 아니다.
    static void AddMerchant(UnitBrain u, List<PawnMenuOption> opts)
    {
        var merchant = u.GetComponent<Merchant>();
        if (merchant == null) return;

        if (!merchant.CanTrade)
        {
            opts.Add(PawnMenuOption.Blocked("물건을 보여 주시오",
                "지금은 거래할 수 있는 상태가 아니다."));
            return;
        }

        opts.Add(("물건을 보여 주시오", (System.Action)(() =>
            MerchantUI.OpenFor(merchant.Shop, merchant.Account, null, Title(u)))));
    }

    // ── 붙잡아 둔 자 ──────────────────────────────────────────────────────────
    //
    // 설득·몸값·풀어 주기는 **이미 주인이 있다**. 여기서는 그 문을 대화 쪽에도 낼 뿐이다.
    static void AddPrisoner(UnitBrain u, UnitBrain speaker, List<PawnMenuOption> opts)
    {
        if (u.Blackboard == null || !u.Blackboard.IsPrisoner) return;

        if (Conversion.CanPersuade(u))
        {
            int pct  = Mathf.RoundToInt(u.Blackboard.ConvertProgress * 100f);
            float wait = Conversion.PersuadeCooldownLeft(u);

            if (wait > 0f)
                opts.Add(PawnMenuOption.Blocked($"우리 쪽으로 오지 않겠나 ({pct}%)",
                    $"방금 말을 붙였다 — {Mathf.CeilToInt(wait)}초 뒤에 다시."));
            else
                opts.Add(($"우리 쪽으로 오지 않겠나 ({pct}%)", (System.Action)(() =>
                {
                    var cage = PrisonCage.HolderOf(u);
                    if (cage != null && speaker != null)
                    {
                        speaker.AbandonWork();
                        cage.RequestPersuadeBy(speaker);
                    }
                })));
        }

        // **말을 건 그 사람이 흥정한다**(`Trading.HagglerFor`) — 눈앞에서 말을 주고받는 것이
        // 그 사람이다. 우클릭 창도 **같은 사람**을 고르므로(`Nearest(commanders, 대상)`)
        // 같은 포로의 값이 두 문에서 갈리지 않는다. 액수를 띄울 때와 치를 때 같은 사람을
        // 넘긴다(따로 구하면 표시와 결제가 갈린다).
        // **낱말에 돈이 있어야 한다.** `네 쪽에 돌려보내 주지`였는데, 그러면 바로 아래
        // `가라. 다시는 보지 말자`(공짜로 풀어 주기)와 **읽어서는 구별이 안 된다** — 액수는
        // 괄호 안에 있어 눈이 나중에 닿는다. 결과는 정반대다: 이쪽은 돈을 벌고(우호도 +8),
        // 저쪽은 안 받는 대신 더 산다(+14). 우클릭 쪽 문구(`몸값을 받고 넘긴다`)와도 맞춘다 —
        // 같은 동작이 문에 따라 다른 말로 불리면 같은 것인 줄 모른다.
        if (WorldRansom.CanSell(u))
        {
            var haggler = Trading.HagglerFor(speaker);
            int payout  = WorldRansom.PayoutFor(u, haggler);
            opts.Add(($"몸값을 받고 돌려보내 주지 ({payout:N0} G)",
                      (System.Action)(() => WorldRansom.Sell(u, haggler))));
        }

        // **값을 안 받는다는 것이 드러나야** 위의 몸값 줄과 고르는 일이 된다.
        opts.Add(("그냥 가라. 다시는 보지 말자", (System.Action)(() => Imprisonment.Release(u))));
    }

    // ── 떠돌이 영입 ───────────────────────────────────────────────────────────
    //
    // **값은 고용가 하나다**(`PawnSpec.Price`) — 마을에서 사람을 구하는 값과 같은 상수를 쓴다.
    // 값이 두 벌이면 "마을은 싼데 길에서는 비싸다"가 설명이 아니라 사고가 된다.
    static void AddRecruit(UnitBrain u, UnitBrain speaker, List<PawnMenuOption> opts,
                           ref string line)
    {
        if (u.Npc == null || u.Npc.NpcType != NpcType.Drifter) return;

        // **`Spec`을 직접 읽지 않는다** — 저작한 폰만 갖고 있어서, 굴려서 태어난 떠돌이는
        // 값이 0이 되어 **선택지가 통째로 사라진다**(실제로 그랬다). `PriceOf`가 명세가 있으면
        // 그 값을, 없으면 실제 스탯에서 낸다.
        //
        // **부르는 값에 흥정이 얹힌다**(`Trading.Haggle`) — 사람을 사는 것도 값을 다투는
        // 일이다. **말을 건 그 사람이 깎는다** — 사교 좋은 폰을 보내면 싸게 데려온다.
        int price = Trading.Haggle(PawnSpec.PriceOf(u), Trading.HagglerFor(speaker), buying: true);
        if (price <= 0) return;

        line = $"정처 없이 떠도는 중이오. 값만 맞으면 손을 빌려 드리리다 — {price:N0} G면 되오.";

        if (!Wallet.CanAfford(price))
        {
            opts.Add(PawnMenuOption.Blocked($"함께 가지 않겠나 ({price:N0} G)",
                $"돈이 모자란다 — 가진 것은 {Wallet.Balance:N0} G다."));
            return;
        }

        opts.Add(($"함께 가지 않겠나 ({price:N0} G)", (System.Action)(() => Hire(u, price))));
    }

    // ── 용병 계약 ─────────────────────────────────────────────────────────────
    //
    // **합류와 다른 축이다.** 합류는 값을 한 번 치르고 사람이 되는 것(일도 시키고 성장도 하고
    // 영영 남는다)이고, 계약은 **기간을 사는 것**이다. 그래서 값도 다르게 매긴다 —
    // 사람 값이 아니라 **하루치 × 날수**라, 오래 붙들수록 비싸진다. 그것이 "위기에 잠깐
    // 부른다"를 만든다(싸게 오래 사면 그냥 일꾼을 사는 것이 된다).
    //
    // 하루치는 **사람 값에서 뽑는다**(`Price / DayDivisor`) — 강한 용병이 더 비싸야 하고,
    // 값이 두 벌이면 "고용은 싼데 계약은 비싸다"가 설명이 아니라 사고가 된다.
    const int   ContractDays = 5;
    const float DayDivisor   = 14f;   // 사람 값의 1/14이 하루치 → 닷새면 값의 약 1/3

    static void AddContract(UnitBrain u, UnitBrain speaker, List<PawnMenuOption> opts,
                            ref string line)
    {
        // **누가 값을 받고 싸워 주는가는 세력이 답한다**(`FactionData.ForHire`).
        // 표시명(`FactionName`)으로 가리면 안 된다 — 그건 번역 대상이라 언어를 바꾸는 순간
        // 고용이 통째로 사라진다(이 저장소가 "표시명을 식별자로 쓰지 말 것"이라 적어 둔 그것).
        //
        // **고용해도 세력은 그대로**라 이 판정이 계약 중에도 통한다(그것이 "싸움만 한다"의 근거다).
        var fd = u.GetComponent<Faction>()?.Data;
        if (fd == null || !fd.ForHire) return;

        // 이미 우리 밑에 있으면 얼마나 남았는지 알려 준다 — 연장할지는 그걸 보고 정한다.
        var hired = u.GetComponent<Hireling>();

        // 하루치는 **사람 값에서 뽑는다** — `PriceOf`라 명세가 없는 용병도 값이 나온다.
        //
        // **흥정은 날수를 곱한 뒤에 얹는다.** 하루치에 먼저 곱하면 반올림이 닷새만큼
        // 증폭돼(하루치 300이 294로 깎이면 닷새에 30 차이) 화면의 하루치와 총액이 안 맞는다.
        int worth = PawnSpec.PriceOf(u);
        int price = worth > 0 ? Mathf.Max(1, Mathf.RoundToInt(worth / DayDivisor) * ContractDays) : 0;
        price = Trading.Haggle(price, Trading.HagglerFor(speaker), buying: true);
        if (price <= 0) return;

        line = hired != null
             ? $"아직 {hired.DaysLeft:F1}일 남았소. 더 쓰겠다면 값은 그대로요 — {price:N0} G에 {ContractDays}일."
             : $"칼값만 치르면 며칠이든 함께 가리다. {ContractDays}일에 {price:N0} G요.";

        string label = hired != null
                     ? $"{ContractDays}일 더 부탁하오 ({price:N0} G)"
                     : $"{ContractDays}일 고용하겠소 ({price:N0} G)";

        if (!Wallet.CanAfford(price))
        {
            opts.Add(PawnMenuOption.Blocked(label,
                $"돈이 모자란다 — 가진 것은 {Wallet.Balance:N0} G다."));
            return;
        }

        opts.Add((label, (System.Action)(() => Contract(u, price, fd))));
    }

    // 계약을 맺는다.
    //
    // **세력을 안 바꾼다.** 처음엔 플레이어로 바꿨는데, 그러면 명부(P)에 뜨고 좌클릭으로
    // 골라 우클릭 명령까지 먹는다 — **"싸움만 한다"가 성립하지 않는다.**
    //
    // 안 바꾸는 편이 오히려 **더 적게 만든다**: `Faction_Mercenary`는 이미 플레이어와 아군이고
    // 산적·고블린·망자·교단·야수에 적대다(`Setup Encounters`가 그렇게 저작했다). 그리고
    // 역할이 없고 플레이어도 아니므로 `UsesEnemyAi`가 참이라 **적 트리(감지 → 전투)를 쓴다** —
    // 곁에서 알아서 싸운다. 명부·선택·작업 큐는 전부 "플레이어 세력인가"로 거르므로
    // **아무 데도 안 뜬다**(막을 것을 따로 안 만들어도 된다).
    //
    // 그래서 계약이 하는 일은 **값을 치르고 며칠 머물게 하는 것**뿐이다.
    static void Contract(UnitBrain u, int price, FactionData home)
    {
        if (!Wallet.TrySpend(price)) return;

        Hireling.Sign(u, ContractDays, home);

        // **떠나던 중이었으면 붙든다** — 용병은 무리를 따라 지나가던 중일 수 있다.
        if (u.Blackboard != null) u.Blackboard.LeavingMap = false;

        EventNotificationUI.Notify("고용",
            $"{u.UnitName}을(를) {ContractDays}일간 고용했습니다. 명령은 안 듣고 곁에서 싸웁니다.");
    }

    // 데려온다. **`ChangeFaction`이 세력을 바꾸는 유일한 길**이다 — `Faction.SetData`만 부르면
    // NPC 트리를 그대로 든 아군이 된다(BT·성장 잠금·작업 설정이 전부 거기서 다시 정해진다).
    static void Hire(UnitBrain u, int price)
    {
        if (!Wallet.TrySpend(price)) return;   // 전액 아니면 0 — 반쯤 내는 일은 없다

        // **역할을 지운다.** 떠돌이 역할이 남으면 `BuildTree`가 NPC 트리를 다시 골라
        // 우리 편인데 명령을 안 듣고 떠돌다 맵을 나간다.
        u.ClearNpcRole();

        var player = PlayerFaction();
        if (player != null) u.ChangeFaction(player);

        // 떠나던 중이었으면 거둔다 — `ChangeFaction`이 이미 지우지만, 여기서 한 번 더 보는 것이
        // 아니라 **그쪽이 지운다는 것을 믿는다**(같은 일을 두 곳이 하면 어느 쪽이 규칙인지 모른다).

        EventNotificationUI.Notify("합류", $"{u.UnitName}이(가) 무리에 합류했습니다.");
    }

    // 플레이어 세력 SO. **씬에서 찾는다** — 어딘가에 필드를 두면 배선을 빠뜨렸을 때
    // 조용히 아무 일도 안 일어난다(레지스트리 폴백과 같은 규칙).
    static FactionData PlayerFaction()
    {
        foreach (var f in Faction.All)
            if (f != null && f.IsPlayer && f.Data != null) return f.Data;

        Debug.LogWarning("[대화] 플레이어 세력을 못 찾았습니다 — 합류시킬 수가 없습니다.");
        return null;
    }
}

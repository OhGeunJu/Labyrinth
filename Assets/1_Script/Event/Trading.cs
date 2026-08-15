using System.Collections.Generic;
using UnityEngine;

// **흥정 — 값을 흔드는 것은 상대가 아니라 우리 쪽 사람이다.**
//
// 한때 세력 우호도가 값 배율을 들었다가 걷어냈다(2026-08-14 (16)→(17)). 상대별 flat %는
// **선택이 아니라 순위를 만든다**: 지역차(`MerchantData.ItemOverrides` 0.5~2.0)는 품목마다
// 방향이 달라 "무엇을 어디서 사서 어디에 파나"를 묻는데, 상대에게 붙은 배율은 그 상인의
// **모든 품목을 같은 방향으로** 밀어 늘 같은 답을 준다.
//
// 무역을 다루는 게임들이 대체로 같은 자리에 놓는다 — 가격은 지역·수요공급이 정하고
// (마운트앤블레이드·Pirates!·X4), flat %는 **플레이어 쪽 축**에 붙인다(림월드의 Social,
// 스카이림의 화술). 모든 상인에 똑같이 걸리므로 어디로 갈지를 왜곡하지 않는다.
//
// ── 왜 사교인가 ────────────────────────────────────────────────────────────
//
// 포로를 설득하는 것과 상인과 값을 다투는 것은 **둘 다 말로 상대를 움직이는 일**이다.
// 스탯을 따로 두면 "설득은 잘하는데 흥정은 못한다"를 설명할 길이 없다(`StatName.Social`
// 주석에 그 판단이 있다).
//
// 덕분에 **"누구를 흥정하러 보내나"가 실제 선택이 된다** — `MerchantUI`는 원정대에서 거래할
// 폰을 이미 고르게 되어 있는데(`SetPawnCycle`) 지금껏 그 선택에 붙은 뜻이 배낭 무게뿐이었다.
public static class Trading
{
    // 사교 130에서 ±18%. **개선만 있고 벌칙은 없다** — 사교 0이 배율 1.0이다.
    //
    // 낮은 쪽에 벌칙을 주면 저작한 `BaseValue`가 "아무도 못 받는 값"이 되어, 데이터에 적은
    // 수와 화면에 뜨는 수가 늘 어긋난다. 기준값은 **맹탕이 흥정했을 때**로 두고 잘하는 만큼
    // 깎는 쪽이, 표를 읽을 때도 값을 저작할 때도 셈이 선다.
    public const float Swing = 0.18f;

    // 스탯 상한(`StatGrade`의 0~130)과 같은 축.
    const float MaxSkill = 130f;

    static float Normalized(UnitBrain pawn)
    {
        if (pawn == null || pawn.Stats == null) return 0f;
        return Mathf.Clamp01(pawn.Stats.Social / MaxSkill);
    }

    // 살 때 곱하는 값 — 잘 흥정할수록 싸다(130 → 0.82).
    public static float BuyFactor(UnitBrain pawn) => 1f - Normalized(pawn) * Swing;

    // 팔 때 곱하는 값 — **방향이 반대다**(130 → 1.18). 한쪽만 붙이면 사교를 올릴수록
    // 사고팔기 왕복이 손해가 되는 이상한 일이 생긴다.
    public static float SellFactor(UnitBrain pawn) => 1f + Normalized(pawn) * Swing;

    // 상점 창에 적을 한 줄 — **"누가 흥정하고 있고, 그래서 값이 얼마나 달라지나".**
    //
    // 값이 조용히 달라지면 상인이 원래 싼 것과 이 폰이 잘 깎은 것이 화면에서 구별되지
    // 않아, 사교를 올린 보람이 어디에도 안 남는다(이 저장소가 여러 번 적은 "모르게 굴리지
    // 말자"의 그 꼴이다).
    //
    // **배율이 1이어도 적는다.** 한때 `±0%`면 빈 문자열을 돌려줬는데(줄만 는다고 봤다),
    // 흥정하는 사람이 **배낭 주인과 다를 수 있게 되면서** 이 줄이 "누가 값을 부르나"까지
    // 답하게 됐다 — 그러면 사교가 낮아 0%인 것과 아예 아무도 안 붙은 것이 화면에서 같아진다.
    // 그 둘은 할 일이 다르다(사람을 키우는 것 ↔ 사람을 데려오는 것).
    public static string Note(UnitBrain pawn)
    {
        if (pawn == null) return "";

        int buy  = Mathf.RoundToInt((BuyFactor(pawn)  - 1f) * 100f);
        int sell = Mathf.RoundToInt((SellFactor(pawn) - 1f) * 100f);

        // **짧게 적는다.** 이 줄은 재고 목록 위 한 줄에 [바꾼다] 버튼과 나란히 서므로,
        // 길어지면 뒤가 잘려 정작 누를 것이 안 보인다.
        return $"{pawn.UnitName} 흥정 · 구매 {buy:+0;-0;0}% / 매입 {sell:+0;-0;0}%";
    }

    // ── 값이 오가는 다른 자리들 ────────────────────────────────────────────────
    //
    // **상점만 사교를 타면 흥정이 절반만 선다.** 이 게임에서 값을 부르고 받는 자리는 넷이고
    // (상점 · 몸값 · 마을 고용 · 용병 계약) **넷 다 흥정이다** — 몸값은 글자 그대로 그렇다.
    //
    // 규칙은 하나다 — **그 일을 하러 간 폰이 있으면 그 사람, 없으면 그 자리에 있는 우리 편
    // 중 제일 잘하는 사람이 한다.**
    //
    // | 어디 | 간 사람 | 누가 흥정하나 |
    // |---|---|---|
    // | 대화창 | 말을 건 폰 | **그 폰**(`HagglerFor`) |
    // | 필드 우클릭(몸값) | 명령 받은 폰 | **그 폰** — 대화창과 같은 사람을 고른다 |
    // | 상점 | 없다(상인을 클릭할 뿐) | 맵 최고(`BestOnField`) |
    // | 지도 위 | 없다(원정대가 통째로 갔다) | 원정대 최고(`BestOf`) |
    //
    // **한때 넷 다 "그 자리 최고"로 뭉갰다가 되돌렸다.** 뭉갠 이유는 같은 포로의 값이 우클릭
    // 창과 대화창에서 달라 보이는 것이었는데, 그러면 **맵 최고 ≥ 말을 건 폰**이 항상 참이라
    // 대화창의 `[네 쪽에 돌려보내 주지]`가 **영영 고를 이유가 없는 줄**이 된다(값을 받으려면
    // 창을 닫고 우클릭하는 것이 언제나 낫다). 두 값이 보이는 것보다 나쁘다.
    //
    // 진짜 답은 **두 문이 같은 사람을 고르게 하는 것**이었다 — 우클릭도 대화창과 같은
    // `Nearest(commanders, 대상)`를 쓴다. 그러면 값이 하나로 맞으면서, 사교 좋은 폰을
    // 보내는 것이 뜻을 갖는다.
    //
    // **기본값을 안 둔다.** 안 넘기면 조용히 맹탕 값이 나오는 대신, 부르는 쪽이 누가 흥정하는지
    // 매번 적게 한다 — 표시하는 곳과 치르는 곳이 **같은 사람**을 넘겨야 값이 안 갈린다.

    // 사교가 가장 높은 사람. 쓰러졌거나 갇힌 사람은 뺀다 — 말을 할 수 있는 상태가 아니다.
    public static UnitBrain BestOf(IEnumerable<UnitBrain> pawns)
    {
        UnitBrain best = null;
        if (pawns == null) return null;

        foreach (var p in pawns)
        {
            if (!CanHaggle(p)) continue;
            if (best == null || p.Stats.Social > best.Stats.Social) best = p;
        }
        return best;
    }

    // **보낸 사람이 있으면 그 사람, 없으면 맵 최고.** 부르는 쪽마다 `?? BestOnField()`를
    // 적으면 한 곳이 반드시 빠뜨린다 — 그 자리는 조용히 "흥정 없음"이 되어 눈에 안 띈다.
    public static UnitBrain HagglerFor(UnitBrain sent) => sent != null ? sent : BestOnField();

    // **필드에서 흥정하는 사람 — 지금 이 맵에 있는 우리 편 중 사교 최고.**
    //
    // 한때 "고른 폰들 중 최고"(상점·우클릭)와 "말을 건 그 사람"(대화창)으로 갈라 뒀다가
    // 하나로 모았다. 규칙이 셋이면 **같은 물건 값이 어디서 물었느냐로 달라지고**, 무엇보다
    // 흥정을 잘 하려면 매번 "그 폰을 골랐던가"를 신경 써야 한다 — 그건 선택이 아니라 잡일이다.
    // 협상은 우리 쪽에서 제일 말 잘하는 사람이 맡는 것이 자연스럽고, 그 사람이 누구인지는
    // 화면이 적어 준다(`Note`).
    //
    // **`Faction.All`이 이미 "지금 살아 있고 켜져 있는 것"을 든다** — 원정 나간 폰은
    // GameObject가 꺼져 있어 저절로 빠지고(그 사람은 여기 없다), 시체는 `Faction`이
    // 떼어져 빠진다. 용병은 세력이 그대로라 안 걸린다(우리 사람이 아니다).
    // **매 프레임 도는 자리라 목록을 안 만든다**(상점 창이 열려 있는 동안 `Update`가 부른다).
    // 훑는 대상은 아래 `CandidatesOnField`와 같다 — 규칙이 갈리면 "목록에 없는 사람이
    // 흥정하고 있다"가 생기므로, 고칠 때는 둘을 같이 볼 것.
    public static UnitBrain BestOnField()
    {
        UnitBrain best = null;

        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;

            var brain = f.GetComponent<UnitBrain>();
            if (!CanHaggle(brain)) continue;

            if (best == null || brain.Stats.Social > best.Stats.Social) best = brain;
        }
        return best;
    }

    // 지금 이 맵에서 흥정을 맡길 수 있는 사람들 — **고르는 목록**(누를 때 한 번 만든다).
    public static List<UnitBrain> CandidatesOnField()
    {
        var list = new List<UnitBrain>();

        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;

            var brain = f.GetComponent<UnitBrain>();
            if (brain != null && brain.Stats != null) list.Add(brain);
        }
        return list;
    }

    // 이 폰에게 흥정을 맡길 수 있나 — 쓰러졌거나 갇혔으면 말을 할 수 있는 상태가 아니다.
    // **자동 선정과 손으로 고르기가 같은 문을 쓴다**(한쪽만 막으면 못 할 사람을 고를 수 있다).
    public static bool CanHaggle(UnitBrain pawn)
    {
        if (pawn == null || pawn.Stats == null) return false;

        var bb = pawn.Blackboard;
        return bb == null || (!bb.IsKnockedOut && !bb.IsPrisoner);
    }

    // **값을 깎거나 올리는 자리 하나.** 부르는 쪽마다 곱하면 표시한 값과 치르는 값이 갈린다
    // — 이 프로젝트에서 가장 나쁜 종류의 어긋남이다(`Shop`이 `Price`를 한 숫자로 두는 것과
    // 같은 이유).
    //
    // `buying`이 방향을 정한다: 내가 내는 값이면 깎이고(몸값 되찾기·고용), 내가 받는 값이면
    // 올라간다(포로 넘기기).
    public static int Haggle(int basePrice, UnitBrain negotiator, bool buying)
    {
        if (basePrice <= 0) return basePrice;

        float f = buying ? BuyFactor(negotiator) : SellFactor(negotiator);
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * f));
    }

    // ── 흥정하면 는다 ─────────────────────────────────────────────────────────
    //
    // **거래 한 건에 얼마씩**이었다가 **오간 돈에 비례 + 하루 상한**으로 바꿨다(2026-08-15).
    // 앞의 규칙은 두 군데가 틀려 있었다.
    //
    // ① **스스로 막으려던 것을 부추겼다.** 주석은 *"개수로 주면 1개짜리를 오십 번 사는 것이
    //    가장 빨리 크는 길이 된다"*고 적어 놨는데 거꾸로다 — 건당 지급이라 화살 50개를
    //    **한 번에 사면 +1.5, 한 개씩 오십 번 사면 +47.7**(사교 F- → C+)이었다. 총량에
    //    비례하는 규칙이야말로 쪼개기에 중립인 유일한 형태다.
    //
    // ② **개수도 답이 아니다.** 개수로 재면 화살 50개(150G)가 강철 흉갑 한 벌(1,256G)보다
    //    쉰 배를 가르친다. 흥정은 물건 개수가 아니라 **얼마짜리를 놓고 다퉜느냐**의 일이다.
    //
    // 그래서 **오간 돈**으로 잰다. 쪼개 사든 한 번에 사든 같고, 값나가는 흥정이 더 가르친다.

    // **1점 오르는 데 드는 거래액**(사교 0 기준). 위로 갈수록 `Sol`이 눌러서 훨씬 더 든다.
    //
    // ── 상한을 두지 않는 이유 ─────────────────────────────────────────────────
    //
    // 한 번 하루 상한을 뒀다가 걷어냈다. 막으려던 것은 옳았다 — *광석 배수를 올려 몇 분 캐면
    // 10만G가 나오는데 그게 그대로 스탯이 되면 곤란하다.* 하지만 **상한은 그것을 막는 대신
    // 딴것을 부순다**: 그 뒤로는 돈이 아무리 많아도 못 올라가서, "돈을 쏟아부어 협상가를
    // 키운다"가 아예 불가능해진다. 게다가 상한은 **화면 밖의 규칙**이라 왜 안 오르는지를
    // 따로 적어 줘야 한다.
    //
    // **곡선이 그 일을 이미 한다.** 성장이 `Sol` = (1 − S/131)²에 눌리므로, 누적 거래액을
    // 적분하면 닫힌 식이 나온다:
    //
    //     G(S) = GoldPerPoint × S / (1 − S/131)
    //
    // 131에서 발산한다 — **상한이 아니라 값이 막는다.** 실제로 얼마가 드는지:
    //
    // | 사교 | 누적 거래액 |     | 사교 | 누적 거래액 |
    // |---:|---:|---|---:|---:|
    // |   5 |     5만 |  |  80 |   206만 |
    // |  10 |    11만 |  | 100 |   422만 |
    // |  30 |    39만 |  | 115 |   943만 |
    // |  50 |    81만 |  | 120 | 1,428만 |
    // |  65 |   129만 |  | 125 | 2,729만 |
    //
    // 그래서 *"몇 분이면 버는 10만G"*는 **바닥에서 9점**이고 위쪽에서는 거의 아무것도 아니다
    // (사교 100인 폰에게 같은 10만G를 부으면 0.6점도 안 된다). 사교 100을 장사로만 채우려면
    // **422만G**가 오가야 한다 — 마을 상인 넷의 하루 주머니가 4,000G 남짓이니 성실한 장사로는
    // 평생 못 닿고, **정말 돈을 쏟아부어야** 움직인다. 상한처럼 문을 잠그지 않으면서 같은
    // 것을 막는다.
    //
    // 주 성장로는 여전히 간수 쪽이다(`PersuadeJob`은 회차당 4.0) — 장사는 밑천만 있으면
    // 얼마든지 하지만 포로는 잡아 와야 하기 때문이다.
    public const float GoldPerPoint = 10000f;

    // `gold` = 그 거래에서 오간 총액. 0이면 아무 일도 안 한다.
    //
    // **총액에 비례한다** — 쪼개 사든 한 번에 사든 같고(건당 지급은 거꾸로였다: 화살 50개를
    // 한 개씩 사면 쉰 배였다), 값나가는 흥정이 더 가르친다(개수로 재면 화살 한 짐이 갑옷
    // 한 벌보다 쉰 배가 된다).
    public static void GrowFromTrade(UnitBrain pawn, int gold)
    {
        if (pawn == null || pawn.Stats == null || gold <= 0) return;

        pawn.Stats.GrowStat(StatName.Social,
                            gold / GoldPerPoint * CombatXp.Sol(pawn.Stats.Social));
    }
}

using System.Collections.Generic;
using UnityEngine;

// **붙잡힌 사람을 돈으로 되찾는다.**
//
// 쳐들어가서 구하는 것과 나란한 길이다 — 어느 쪽이 싼가가 판단거리가 된다.
// 다만 **아무와나 흥정이 되지는 않는다**: `FactionData.AcceptsRansom`이 꺼진 세력
// (고블린 같은 것들)은 돈을 받지 않으므로 치고 들어가는 수밖에 없다.
//
// 값은 **스탯 + 장비**다. 사람 값은 고용가(`PawnSpec.Price`)와 **같은 상수**를 쓴다 —
// 값이 두 벌이면 "고용은 싼데 되찾는 건 비싸다"가 설명이 아니라 사고가 된다.
public static class WorldRansom
{
    // 되사는 값은 새로 구하는 값보다 비싸다. 급한 쪽이 우리고, 저쪽은 팔지 않아도 그만이다.
    // 이 배수가 곧 "차라리 쳐들어간다"의 문턱이다.
    const float RansomFactor = 1.4f;

    // **넘길 때 받는 값은 되찾을 때 내는 값보다 훨씬 적다.**
    //
    // 흥정에서는 **아쉬운 쪽이 손해 본다.** 내가 되찾을 때는 내가 아쉽다(그 사람이 우리
    // 일꾼이고, 안 데려오면 영영 없다). 반대로 내가 붙잡은 사람을 넘길 때 저쪽은 아쉬울
    // 것이 없다 — 저쪽은 안 사도 그만이고 **나는 팔 곳이 그 세력 하나뿐이다.**
    //
    // 그리고 **대체 가능성이 다르다.** 내 일꾼은 이름이 있고 스탯이 자라 있어 대신할 것이
    // 없지만, 산적단에게 졸개 하나는 얼마든지 다시 뽑는다. 저쪽이 큰돈을 쓸 이유가 없다.
    //
    // **숫자로 재 보면 이렇다**(두 종족 모두 기본 등급 D → `BaseWorth` ≈ 4,500):
    //   - 넘기고 받는 값  4,500 × 0.3 ≈ **1,350 G**
    //   - 되찾는 값       4,500 × 1.4 ≈ **6,300 G**
    //   - 사람 고용가     ≈ **4,500 G**(`PawnSpec.Price`)
    //
    // 즉 **셋을 팔아야 하나를 고용한다.** 이 비율이 두 가지를 지킨다:
    //   ① **포로 장사가 벌이는 되지만 최적 전략은 아니다** — 0.7로 뒀더니 잡졸 하나가
    //      3,150 G(고용가의 70%)라 습격 한 번 막으면 12,000 G가 들어왔다. 그러면 붙잡기가
    //      늘 정답이 되고 회유·처형·풀어 주기가 전부 손해가 된다.
    //   ② **회유와 고르는 일이 된다** — 회유는 이틀 + 탈출 위험 + 간수 비용을 치르고 4,500 G짜리
    //      사람을 얻고, 몸값은 지금 당장 1,350 G를 얻는다. 어느 쪽도 늘 옳지 않다.
    //
    // **두목은 저절로 비싸진다** — 등급과 장비가 값에 그대로 실리므로(`BaseWorth`),
    // 산 채로 잡을 가치가 있는 상대가 생긴다.
    //
    // `ItemData.ResaleFactor`(쓰던 검은 중고다)와 같은 축이다 — 되파는 것은 늘 깎인다.
    const float PayoutFactor = 0.3f;

    // ── 값 ────────────────────────────────────────────────────────────────────

    // 스탯 + 장비. **두 값이 여기서 갈라져 나온다** — 사는 값과 파는 값이 각자 몸값을
    // 계산하면 한쪽만 고쳐 놓고 반대쪽을 잊는다.
    static float BaseWorth(UnitBrain u) =>
        PawnSpec.PriceBase + PawnSpec.GradeAverageOf(u) * PawnSpec.PricePerGrade + GearValue(u);

    // 한 사람의 몸값. **흔들림이 없다** — 고용가는 부르는 값이라 사람마다 달라도 되지만,
    // 몸값은 창을 여닫을 때마다 바뀌면 값이 나올 때까지 여닫게 된다.
    public static int PriceFor(UnitBrain u)
    {
        if (u == null) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(BaseWorth(u) * RansomFactor));
    }

    // 붙잡은 사람을 그 세력에 넘기고 받는 값.
    public static int PayoutFor(UnitBrain u)
    {
        if (u == null) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(BaseWorth(u) * PayoutFactor));
    }

    public static int PriceFor(IEnumerable<UnitBrain> members)
    {
        int sum = 0;
        if (members != null) foreach (var m in members) sum += PriceFor(m);
        return sum;
    }

    // 걸치고 있는 것의 시세. **등급 배율을 곱한다** — 희귀 철검을 일반 철검 값에 되찾을 수는 없다.
    static float GearValue(UnitBrain u)
    {
        var eq = u.Equipment;
        if (eq == null) return 0f;

        float v = 0f;
        if (eq.MainHand != null) v += eq.MainHand.BaseValue;
        if (eq.OffHand  != null) v += eq.OffHand.BaseValue;

        foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
        {
            var armor = eq.GetArmor(slot);
            if (armor != null) v += armor.BaseValue * eq.GetArmorQualityMult(slot);
        }
        return v;
    }

    // ── 지불 ──────────────────────────────────────────────────────────────────
    //
    // **돈은 하나다**(`Wallet`) — 창고에 있는지 배낭에 있는지 묻지 않는다.
    // 전에는 "배낭 먼저, 그다음 창고"를 여기서 따로 짰는데, 돈을 묻는 곳이 늘 때마다
    // 그 순서를 다시 짜야 했고 실제로 한 번 어긋났다(`TakeGold`가 전액 아니면 0이라
    // 부족한 액수를 그대로 넣으면 한 푼도 안 나왔다).

    public static int Available() => Wallet.Balance;

    public static bool TryPay(int amount) => Wallet.TrySpend(amount);

    // ── 필드에서 넘긴다 ───────────────────────────────────────────────────────
    //
    // **월드맵 몸값의 반대 방향이다.** 저쪽은 잡아간 내 사람을 돈 받고 돌려주고, 나는
    // 붙잡아 둔 저쪽 사람을 돈 받고 돌려준다 — 그래서 **같은 파일에 둔다**: 값이 두 벌이면
    // "되찾는 건 비싼데 넘기는 건 더 비싸다" 같은 것이 조용히 생긴다.
    //
    // 하는 일은 **`Imprisonment.Release`에 돈이 붙는 것**뿐이다. 자리 비우기·갇힘 풀기·
    // 탈출 게이지 지우기·도주 성향·맵 이탈이 전부 거기 있으므로 여기서 다시 하지 않는다
    // (풀려나는 길이 둘인데 처리가 둘이면 한쪽이 어긋난다).

    // 이 사람을 넘길 수 있나. **말이 통해야 한다** — `AcceptsRansom`이 꺼진 세력
    // (고블린·야수·망자·교단)은 돈을 주고 데려가지 않는다. 그쪽 포로는 회유하거나
    // 풀어 주거나 처형하는 수밖에 없고, 그것이 그 세력을 다르게 만드는 자리다.
    public static bool CanSell(UnitBrain u)
    {
        if (u == null || u.Blackboard == null || !u.Blackboard.IsPrisoner) return false;

        // **우리 폰은 못 판다.** 갇혀 있어도 우리 편이다(`IsCaptive`가 "붙잡은 사람"의 단일 정의).
        if (!PrisonEscort.IsCaptive(u)) return false;

        var faction = u.GetComponent<Faction>();
        return faction != null && faction.Data != null && faction.Data.AcceptsRansom;
    }

    // 넘긴다 — 돈을 받고 놓아준다. **받은 액수를 돌려준다**(0이면 아무 일도 안 했다).
    public static int Sell(UnitBrain u)
    {
        if (!CanSell(u)) return 0;

        int paid = PayoutFor(u);
        Wallet.Add(paid);

        // **거래는 거래다** — 사람을 돌려보냈으니 그쪽 우호도가 조금 오른다. 죽이는 것과
        // 반대 방향의 길이 있어야 우호도가 **벌칙이 아니라 선택**이 된다(붙잡은 자를 어떻게
        // 처리하느냐가 곧 그 세력과의 관계다).
        var fd = u.GetComponent<Faction>()?.Data;
        if (fd != null)
            FactionRelations.Add(fd, FactionRelations.RansomBonus,
                                 $"{u.UnitName}을(를) 몸값을 받고 돌려보냈다");

        // **`Release`가 나머지를 전부 한다.** `reason`은 로그에만 남지만, 손으로 놓아 준 것과
        // 몸값을 받은 것이 로그에서 구별되지 않으면 "왜 없어졌지"를 되짚을 때 가릴 수가 없다.
        Imprisonment.Release(u, $"몸값 {paid} G를 받고 넘겼다");
        return paid;
    }
}

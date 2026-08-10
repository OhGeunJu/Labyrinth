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

    // ── 값 ────────────────────────────────────────────────────────────────────

    // 한 사람의 몸값. **흔들림이 없다** — 고용가는 부르는 값이라 사람마다 달라도 되지만,
    // 몸값은 창을 여닫을 때마다 바뀌면 값이 나올 때까지 여닫게 된다.
    public static int PriceFor(UnitBrain u)
    {
        if (u == null) return 0;

        float body = PawnSpec.PriceBase + GradeAverage(u) * PawnSpec.PricePerGrade;
        return Mathf.Max(1, Mathf.RoundToInt((body + GearValue(u)) * RansomFactor));
    }

    public static int PriceFor(IEnumerable<UnitBrain> members)
    {
        int sum = 0;
        if (members != null) foreach (var m in members) sum += PriceFor(m);
        return sum;
    }

    // **실제 스탯에서 등급 평균을 낸다.** `PawnSpec.GradeAverage`를 쓰지 않는 이유는
    // 명세가 없는 폰(굴려서 태어난 적·하수인)도 붙잡힐 수 있어서다. 실제 값을 보면 자란 만큼도
    // 값에 반영된다 — 오래 데리고 다닌 사람이 더 비싸다.
    static float GradeAverage(UnitBrain u)
    {
        if (u.Stats == null) return 0f;

        int sum = 0, count = 0;
        foreach (StatName stat in System.Enum.GetValues(typeof(StatName)))
        {
            sum += (int)StatGradeExt.FromValue(u.Stats.GetStat(stat));
            count++;
        }
        return count > 0 ? sum / (float)count : 0f;
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
}

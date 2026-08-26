using UnityEngine;

// **원예가 하는 일 한 곳**(2026-08-26).
//
// 그전까지 `StatName.Growing`은 이름과 설명(*"씨를 뿌리고 거둔다. 수확량에도 걸린다"*)만 있고
// **코드 어디에서도 안 읽혔다** — 농지 일감(`SeedJob`·`FarmHarvestJob`)이 스탯을 아예 안 봤다.
// 스탯 창에는 등급까지 떠 있었으니 **화면이 거짓말을 하고 있었다**(강인함이 도발 없이는 평생
// 안 자라던 것과 같은 자리).
//
// **답을 여기 모으는 이유**: 심는 쪽과 거두는 쪽 둘이 부르고, 앞으로 온실·관개가 붙으면 더
// 는다. 배율식을 일감마다 적으면 하나가 반드시 어긋난다.
public static class Farming
{
    // 원예 130이면 수확량 두 배. **곱이지 덧셈이 아니다** — 작물마다 기본 수확량이 다른데
    // 고정값을 더하면 값싼 작물일수록 배수가 커진다.
    const float YieldGain = 1f;

    public const float HarvestXp = 4f;   // 한 번 거둘 때
    public const float SeedXp    = 2f;   // 한 번 심을 때 (거두는 쪽이 값을 낸다)

    public static float Skill(UnitBrain unit) =>
        unit != null && unit.Stats != null ? unit.Stats.Growing : 0f;

    // **정수라 반올림한다** — 내림이면 기본 2개짜리 작물에서 원예 0~64가 전부 2개로 같아져
    // (3개가 되려면 65가 필요하다) 낮은 구간이 통째로 죽는다.
    public static int HarvestAmount(int baseAmount, UnitBrain unit)
    {
        float mult = 1f + Skill(unit) / StatGradeExt.MaxStatValue * YieldGain;
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * mult));
    }

    // 도전 대상이 없어 SOL만 — 노동 스탯이 다 그 모양이다(`Research`·`Social`과 같은 자리).
    public static void Grow(UnitBrain unit, float amount)
    {
        if (unit == null || unit.Stats == null) return;
        unit.Stats.GrowStat(StatName.Growing, amount * CombatXp.Sol(unit.Stats.Growing));
    }
}

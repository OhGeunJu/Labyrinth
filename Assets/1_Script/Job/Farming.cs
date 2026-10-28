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

    // **거두면 씨앗도 나온다**(2026-09-05, 사용자 요청). 심을 때 한 알을 쓰므로(`SeedJob`)
    // 평균 1.5알이면 밭이 **저절로 이어지고 천천히 넓어진다** — 씨앗을 사 와야만 농사가
    // 도는 구조는 지하에서 상인이 드문 만큼 곧 막다른 길이 된다.
    //
    // **원예를 안 태운다.** 늘어나야 하는 것은 *수확량*이고(`HarvestAmount`가 이미 그 축이다)
    // 씨앗은 *이어 가는 것*이다 — 둘 다 스탯을 타면 잘 키운 농부 하나가 씨앗을 홍수처럼
    // 내놓아 밭의 크기가 스탯 하나로 정해진다.
    //
    // **작물마다 저작하지 않는다.** `SeedData`에 필드를 더하면 **이미 있는 에셋은 전부 0**이라
    // (새 필드의 기본값) *"고쳤는데 씨앗이 안 나온다"*가 된다. 갈라야 할 이유가 생기면 그때
    // 저작으로 옮긴다.
    public static int SeedReturn(UnitBrain unit) => Random.Range(1, 3);

    // 도전 대상이 없어 SOL만 — 노동 스탯이 다 그 모양이다(`Research`·`Social`과 같은 자리).
    public static void Grow(UnitBrain unit, float amount)
    {
        if (unit == null || unit.Stats == null) return;
        unit.Stats.GrowStat(StatName.Growing, amount * CombatXp.Sol(unit.Stats.Growing));
    }
}

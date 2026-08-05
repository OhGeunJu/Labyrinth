using System.Collections.Generic;
using UnityEngine;

// 폰이 얼마나 질 수 있는가 — **적재 한도의 단일 출처.**
//
// 전에는 같은 것을 네 군데가 따로 계산했고, 그중 둘이 서로 달랐다:
//   `PawnMover` / `CombatHandler` / `ExpeditionParty` = 근력 × 5   (근력 10 → 50)
//   `PartyCarry`                                      = 10 + 근력 × 0.5 (근력 10 → 15)
// 3.3배 어긋난 두 답이 **같은 원정대**에 동시에 쓰였다. 편성 화면은 15kg이라 하고
// 실제 적재는 50까지 받는 식이다. 여기로 모아 한 곳에서만 답한다.
//
// **작은 쪽(`10 + 근력 × 0.5`)을 남겼다.** 근력 × 5는 130에서 650kg이라 무게가 사실상
// 아무것도 막지 못한다 — 폰 하나가 광맥을 통째로 비우고 온다. 무게를 넣는 이유가
// "한 번에 다 못 가져간다"인데 한도가 그만큼 크면 넣으나 마나다.
public static class CarryCapacity
{
    public const float Base        = 10f;   // 빈손 기본 적재(kg)
    public const float PerStrength = 0.5f;  // 근력 1당 추가 → 근력 10:15kg, 130:75kg

    // ── 폰 단위 ──────────────────────────────────────────────────────────────
    public static float For(UnitStats stats) =>
        stats == null ? 0f : Base + stats.Strength * PerStrength;

    public static float For(UnitBrain brain) =>
        brain == null ? 0f : For(brain.Stats);

    public static float Load(UnitBrain brain) =>
        brain == null || brain.Inventory == null ? 0f : brain.Inventory.GetTotalWeight();

    public static float Free(UnitBrain brain) => Mathf.Max(0f, For(brain) - Load(brain));

    // 한도 대비 얼마나 실었나. 1을 넘으면 과적이다(막지는 않는다 — 아래 주석 참조).
    public static float LoadRatio(UnitBrain brain)
    {
        float cap = For(brain);
        return cap <= 0f ? 0f : Load(brain) / cap;
    }

    // ── 몇 개나 더 실을 수 있나 ───────────────────────────────────────────────
    // **한도는 자동 운반에만 강제된다.** 플레이어가 배낭에 직접 끌어다 넣는 것은 막지 않고,
    // 대신 무거울수록 느려진다(`PawnMover` / `CombatHandler`의 무게 보정). 손으로 한 일을
    // 거부하면 "왜 안 들어가지"가 되고, 자동으로 한 일이 한도를 무시하면 "왜 이 폰만 느리지"가 된다.
    public static int RoomFor(UnitBrain brain, ItemData item)
    {
        if (brain == null || item == null) return 0;

        // 무게가 없는 물건은 무게로 막지 않는다 — 배낭 격자가 막는다.
        if (item.Weight <= 0f) return int.MaxValue;

        return Mathf.Max(0, Mathf.FloorToInt(Free(brain) / item.Weight));
    }

    // ── 여럿 합산 (원정대) ────────────────────────────────────────────────────
    public static float TotalFor(IEnumerable<UnitBrain> members)
    {
        float sum = 0f;
        if (members != null) foreach (var m in members) sum += For(m);
        return sum;
    }

    public static float TotalLoad(IEnumerable<UnitBrain> members)
    {
        float sum = 0f;
        if (members != null) foreach (var m in members) sum += Load(m);
        return sum;
    }
}

using System.Collections.Generic;

// 파티 등짐 적재 로직. 데이터는 멤버별 Inventory에 그대로 존재(GetTotalWeight) —
// 여기선 파티 단위 합산/용량/자동분배만. A뷰(멤버별)·B뷰(공용 풀)가 같은 이 모델을 공유.
//
// 용량 = Strength 파생(켄시식). 무거운 짐 운반 시 근력 성장(GrowStat 연결)은 운반 노동에서 후속.
// 무게(용량)는 "경고용 소프트 한도", 실제 입고 가능 여부는 Inventory 격자 자리(Inventory.TryAdd)가 결정.
public static class PartyCarry
{
    public const float BaseCapacity       = 10f;  // 멤버 기본 적재(kg)
    public const float CapacityPerStrength = 0.5f; // Strength 1당 추가(kg) → 10:15kg, 130:75kg

    // ── 멤버 단위 ───────────────────────────────────────────────────────────
    public static float MemberCapacity(UnitBrain m) =>
        m == null || m.Stats == null ? 0f : BaseCapacity + m.Stats.Strength * CapacityPerStrength;

    public static float MemberLoad(UnitBrain m) =>
        m == null || m.Inventory == null ? 0f : m.Inventory.GetTotalWeight();

    public static float MemberFree(UnitBrain m) => MemberCapacity(m) - MemberLoad(m);

    // ── 파티 합산 ───────────────────────────────────────────────────────────
    public static float TotalCapacity(IEnumerable<UnitBrain> members)
    {
        float c = 0f;
        if (members != null) foreach (var m in members) c += MemberCapacity(m);
        return c;
    }

    public static float TotalLoad(IEnumerable<UnitBrain> members)
    {
        float l = 0f;
        if (members != null) foreach (var m in members) l += MemberLoad(m);
        return l;
    }

    public static bool IsOverloaded(IEnumerable<UnitBrain> members) =>
        TotalLoad(members) > TotalCapacity(members);

    // ── B뷰: 공용 풀 자동 분배 입고 ─────────────────────────────────────────
    // 여유 무게가 많은 멤버부터 격자에 입고 시도. 반환: 실제 입고 수량.
    public static int TryAddToParty(IReadOnlyList<UnitBrain> members, ItemData data, int amount,
                                    ItemQuality quality = ItemQuality.Common)
    {
        if (members == null || data == null || amount <= 0) return 0;

        // 여유 무게 내림차순 정렬 (원본 보존 위해 복사)
        var ordered = new List<UnitBrain>(members);
        ordered.Sort((a, b) => MemberFree(b).CompareTo(MemberFree(a)));

        int remaining = amount;
        foreach (var m in ordered)
        {
            if (remaining <= 0) break;
            if (m == null || m.Inventory == null) continue;
            remaining -= m.Inventory.TryAdd(data, remaining, quality);
        }
        return amount - remaining;
    }
}

using UnityEngine;

public static class CombatXp
{
    const float MaxLevel = StatGradeExt.MaxStatValue;

    // 130 캡에 맞춰 켄시 비율 비례 조정 (켄시: 22.5/50 @ 100 cap)
    const float ZeroXpAbove = 29.25f;  // 상대보다 이만큼 위 → 0.1x
    const float MaxXpBelow  = 65f;     // 상대보다 이만큼 아래 → 6.0x
    const float MinMult = 0.1f;
    const float MaxMult = 6.0f;

    // 상대 스킬 차이에 따른 XP 배율
    public static float ChallengeMult(float mySkill, float opponentSkill)
    {
        float diff = mySkill - opponentSkill;
        if (diff >= ZeroXpAbove) return MinMult;
        if (diff <= -MaxXpBelow) return MaxMult;
        return diff >= 0f
            ? Mathf.Lerp(1.0f, MinMult, diff / ZeroXpAbove)
            : Mathf.Lerp(1.0f, MaxMult, -diff / MaxXpBelow);
    }

    // SOL — 후반 둔화: (1 - level/131)²
    public static float Sol(float mySkill)
    {
        float r = 1f - mySkill / (MaxLevel + 1f);
        return r * r;
    }

    // 수적 열세 보너스: +10% per extra enemy, cap 1.666x at 1v8+
    public static float OutnumberedMult(int enemyCount)
    {
        if (enemyCount <= 1) return 1f;
        return Mathf.Min(1f + (enemyCount - 1) * 0.1f, 1.666f);
    }

    public static float Calculate(float mySkill, float opponentSkill, int enemyCount = 1)
        => ChallengeMult(mySkill, opponentSkill) * Sol(mySkill) * OutnumberedMult(enemyCount);

    // 주변 적 수 카운트 (수적 보너스용)
    public static int CountNearbyEnemies(GameObject self, float radius = 6f)
    {
        var myFaction = self.GetComponent<Faction>();
        if (myFaction == null) return 1;

        int count = 0;
        foreach (var col in Physics.OverlapSphere(self.transform.position, radius))
        {
            if (col.gameObject == self) continue;
            var f = col.GetComponent<Faction>();
            if (f != null && myFaction.IsHostileTo(f)) count++;
        }
        return Mathf.Max(1, count);
    }
}

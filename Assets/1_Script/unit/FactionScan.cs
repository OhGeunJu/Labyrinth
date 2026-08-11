using UnityEngine;

// **주변에서 세력을 훑는 일 한 곳.** 탐지(`DetectNode`)·도주(`FleeNode`)·수적 보너스
// (`CombatXp.CountNearbyEnemies`)가 같은 일을 세 벌로 하고 있었다.
//
// ── 왜 `Physics.OverlapSphere`를 그만뒀나 ──────────────────────────────────
//
// 셋 다 `OverlapSphere` → `GetComponent<Faction>()`이었다. 둘 다 값이 크다:
//
// **① 매번 배열을 새로 만든다.** BT는 폰마다 초당 열 번 돌고, 전투 중에는 공격 한 번에
// `CountNearbyEnemies`가 네 번쯤 불린다 — 폰 스무 마리면 초당 수백 개의 배열이 GC로 간다.
//
// **② 콜라이더를 거쳐 컴포넌트를 되묻는다.** 정작 찾는 것은 `Faction`인데 물리에게
// "여기 뭐 있냐"고 묻고 나서 하나하나 `GetComponent`로 걸러 냈다.
//
// **`Faction.All`이 이미 있다.** 그 레지스트리는 정확히 이 목적으로 만들어졌다 —
// 주석에도 "폰을 훑어야 할 때 `FindObjectsByType`을 매번 도는 대신 쓴다"고 적혀 있다.
// 거리를 직접 재면 할당이 없고 `GetComponent`도 없다.
//
// ── 달라지는 것 ────────────────────────────────────────────────────────────
//
// **콜라이더가 아니라 존재를 본다.** 그래서 셋이 조용히 고쳐진다:
//  · 콜라이더가 자식에 달린 프리팹도 잡힌다(예전엔 `GetComponent`가 못 찾아 안 보였다).
//  · 콜라이더가 여럿인 폰을 두 번 세지 않는다(`CountNearbyEnemies`가 그랬다).
//  · 꺼진 폰(원정)은 `OnDisable`로 레지스트리에서 빠져 있어 애초에 안 잡힌다.
//
// **벽 너머도 보이는 것은 예전 그대로다** — `OverlapSphere`도 시야를 안 봤다.
public static class FactionScan
{
    // 가장 가까운 적대 대상.
    //
    // `theirsToMine` — **관계는 방향별**이라(`FactionData.StanceTo`) 어느 쪽으로 묻는지가
    // 다르다. 탐지는 "내가 적대하는 쪽"(내가 친다), 도주는 "나를 적대하는 쪽"(쟤가 나를 친다).
    // 인자로 받지 않으면 둘 중 하나가 반드시 반대로 물어보게 된다.
    public static GameObject NearestHostile(GameObject self, Faction mine, float range,
                                            bool theirsToMine)
    {
        if (self == null || mine == null) return null;

        Vector3 pos    = self.transform.position;
        float rangeSqr = range * range;

        GameObject best = null;
        float bestSqr   = float.MaxValue;

        var all = Faction.All;
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null || other.gameObject == self) continue;

            bool hostile = theirsToMine ? other.IsHostileTo(mine) : mine.IsHostileTo(other);
            if (!hostile) continue;

            float d = (other.transform.position - pos).sqrMagnitude;
            if (d > rangeSqr || d >= bestSqr) continue;

            // **거리를 먼저 보고 나서 묻는다** — `IsAttackable`은 `GetComponent`를 몇 번 타므로
            // 멀리 있는 것까지 물어보면 아낀 것을 도로 쓴다.
            if (!BTTargetUtil.IsAttackable(other.gameObject)) continue;

            bestSqr = d;
            best    = other.gameObject;
        }
        return best;
    }

    // 범위 안 적대 세력 수. **최소 1** — 수적 보너스 계산이 0으로 나누지 않게 하는 것이
    // 아니라, "혼자 상대한다"가 기준값(1)이기 때문이다.
    public static int CountHostiles(GameObject self, Faction mine, float range)
    {
        if (self == null || mine == null) return 1;

        Vector3 pos    = self.transform.position;
        float rangeSqr = range * range;
        int count      = 0;

        var all = Faction.All;
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null || other.gameObject == self) continue;
            if (!mine.IsHostileTo(other)) continue;
            if ((other.transform.position - pos).sqrMagnitude > rangeSqr) continue;
            count++;
        }
        return Mathf.Max(1, count);
    }
}

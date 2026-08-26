using System.Collections.Generic;
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

    // 범위 안 적대 **전부**. 도발처럼 여럿을 한꺼번에 잡는 능력이 쓴다.
    //
    // **담을 그릇을 받는다** — 새 리스트를 돌려주면 이 파일이 `OverlapSphere`를 그만둔 이유
    // (매번 배열을 새로 만들지 않는다)를 그대로 되돌리는 셈이다. 부르는 쪽이 한 벌을 돌려 쓴다.
    //
    // 거르는 규칙은 `NearestHostile`과 **같다**(적대 · 범위 · 칠 수 있는 상대) — 갈라 두면
    // "탐지에는 걸리는데 도발은 안 되는" 상대가 생긴다.
    public static int HostilesWithin(GameObject self, Faction mine, float range,
                                     bool theirsToMine, List<GameObject> into)
    {
        if (into == null) return 0;
        into.Clear();
        if (self == null || mine == null) return 0;

        Vector3 pos    = self.transform.position;
        float rangeSqr = range * range;

        var all = Faction.All;
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null || other.gameObject == self) continue;

            bool hostile = theirsToMine ? other.IsHostileTo(mine) : mine.IsHostileTo(other);
            if (!hostile) continue;

            if ((other.transform.position - pos).sqrMagnitude > rangeSqr) continue;
            if (!BTTargetUtil.IsAttackable(other.gameObject)) continue;

            into.Add(other.gameObject);
        }
        return into.Count;
    }

    // **사수 앞 부채꼴 안에 있는 사람** — 빗나간 화살이 이걸 묻는다(2026-08-26).
    //
    // **세력을 안 가린다**(사용자 결정 — *"오사가 아군오사뿐만 아니라 사실 그냥 다른 상대를
    // 쏠 수도 있는거잖아"*). 빗나간 화살은 누가 거기 서 있는지를 따지지 않는다 — 동료가 맞으면
    // 손해고 옆에 선 다른 적이 맞으면 그냥 다른 놈이 맞는 것이다. **한때 비적대만 훑었는데**,
    // 그러면 *"적들만 있는 난전에 대고 쏘면 정밀사격이 아무 일도 안 한다"*가 되어 **겨눈 놈을
    // 마무리하려는 순간에** 그 스탯이 사라졌다.
    //
    // 적대를 찾는 위 셋과 **묻는 각이 다를 뿐**이라 여기 나란히 둔다. 규칙은 같다(레지스트리를
    // 훑는다 · 칠 수 있는 상대만 · 담을 그릇을 안 만든다).
    //
    // **선이 아니라 부채꼴이다**(사용자 결정) — 레이저처럼 가는 선으로 재면 동료가 그 위에
    // 정확히 서 있어야만 걸려, 규칙은 있는데 **거의 안 난다.** 화살은 손이 흔들리면 옆으로도
    // 벌어지므로, *"내 앞 이쯤에 우리 편이 있나"*가 물어야 할 것이다.
    //
    // **가장 가까운 하나** — 여럿이면 앞사람이 먼저 맞는다.
    //
    // **`exclude`는 겨눈 표적이다 — 반드시 넘길 것.** 안 빼면 표적 자신이 *"내 앞에 선
    // 사람"*으로 걸려 **제 몫의 빗맞음 대상이 된다**(부채꼴의 축 위, 정확히 그 거리에 있다).
    // 맞는 사람은 어차피 같지만 그건 빗맞은 것이 아니라 그냥 쏜 것이다.
    public static GameObject NearestPawnInCone(GameObject self, Vector3 dir,
                                               float range, float cosHalfAngle,
                                               GameObject exclude = null)
    {
        if (self == null) return null;

        float dirLen = dir.magnitude;
        if (dirLen <= 0.001f) return null;
        dir /= dirLen;

        Vector3 pos    = self.transform.position;
        float rangeSqr = range * range;

        GameObject best = null;
        float bestSqr   = float.MaxValue;

        var all = Faction.All;
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null || other.gameObject == self) continue;
            if (other.gameObject == exclude) continue;

            Vector3 delta = other.transform.position - pos;
            float d       = delta.sqrMagnitude;
            if (d > rangeSqr || d >= bestSqr || d <= 0.0001f) continue;

            // **높이는 안 본다** — 부채꼴은 수평으로 재고, 폰의 피벗이 발밑이라 위아래를
            // 섞으면 오르막에 선 동료가 각도 밖으로 밀려난다.
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float flatLen = flat.magnitude;
            if (flatLen <= 0.001f) continue;
            if (Vector3.Dot(flat / flatLen, new Vector3(dir.x, 0f, dir.z).normalized) < cosHalfAngle)
                continue;

            // **쓰러진 몸은 안 맞는다** — 화살이 지나가는 높이에 없고, 무엇보다 기절이
            // 회복 가능한 상태라는 규칙을 빗맞은 화살이 뒤로 뚫으면 안 된다(시체도 빠진다).
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

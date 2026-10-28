using UnityEngine;

// **어느 거점의 것인가**(2026-08-26).
//
// 필드맵이 200m 한 장이던 시절에는 물을 것이 없었다 — 창고도 일감도 폰도 다 한 맵에 있었으니
// `Storage.GetTotalByItem`이 전부 더해도 맞았다. **세계가 29km로 이어지면서 그게 거짓말이
// 된다**: 거점이 둘이면 **29km 밖 창고의 목재로 여기서 집을 짓는다.**
//
// **가르는 것은 핵 영역이다**(사용자 결정). 거리로 자를 수도 있지만, 그러면 *"얼마나 멀면
// 다른 거점인가"*라는 새 숫자가 생긴다 — **핵은 이미 영역을 갖고 있고**(메인 + 서브 union)
// `BuildingDormancy`가 그 답을 쓰고 있다. 답이 하나면 *"휴면은 아닌데 창고 집계에서는 빠지는"*
// 자리가 안 생긴다.
//
// **영역 밖은 가장 가까운 핵에 붙는다** — 야생 한복판의 물건도 어딘가에는 속해야 하고,
// 안 그러면 아무 거점에도 안 세어지는 창고가 생긴다.
public static class Territory
{
    // 이 자리를 품는 핵. 어느 영역에도 안 들면 **가장 가까운** 핵.
    public static DungeonCore CoreAt(Vector3 worldPos)
    {
        var all = DungeonCore.All;
        if (all.Count == 0) return null;
        if (all.Count == 1) return all[0];

        DungeonCore nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i];
            if (c == null) continue;
            if (c.IsWithinAnyRangeWorld(worldPos)) return c;   // 영역 안이면 그것이 답이다

            float d = (c.transform.position - worldPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; nearest = c; }
        }
        return nearest;
    }

    // 두 자리가 같은 거점인가. **핵이 하나뿐이면 언제나 참** — 거점 하나짜리 판은 예전 그대로다.
    public static bool Same(Vector3 a, Vector3 b)
    {
        if (DungeonCore.All.Count <= 1) return true;
        return CoreAt(a) == CoreAt(b);
    }

    public static bool Same(Vector3 a, DungeonCore core) =>
        DungeonCore.All.Count <= 1 || CoreAt(a) == core;
}

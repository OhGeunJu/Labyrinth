using UnityEngine;

// **채우는 일의 규칙은 여기 하나뿐이다** — `Feeding`의 짝(2026-09-04).
//
// 골렘은 마력석을 태워 **에너지**를, 해골은 뼈를 바쳐 **사기**를 채운다. 눈금(감소·임계·
// 바닥 벌칙)은 여기가 아니라 **`Needs`**가 들고, 여기가 답하는 것은 *어디로 가서 무엇을
// 쓰나* 하나다.
//
// ## 밥과 갈리는 자리
//
// 음식은 **아무 데서나** 먹는다(배낭·창고). 충전은 **그 자리에 가야 한다** — 그것이
// 이 축이 허기의 복사본이 아닌 이유이고(2026-08-20에 갈증을 걷어낸 그 함정), 딸려 오는
// 결정이 *충전대를 어디에 몇 개 세우나*다. 굴이 넓어질수록 그 결정이 커진다.
//
// ## 멈춘 골렘도 계속 묻는다
//
// 에너지가 바닥나면 골렘은 **그 자리에 멈춘다**(`Needs.StarvePerSecond`가 0이라 안 죽는다).
// 그런데 멈춘 것이 BT를 끄는 것이면 **재료가 생겨도 영영 못 간다** — 데드락이다. 그래서
// 멈춤은 `RechargeNode`가 **`Running`을 돌려주는 것**으로 낸다: 위에 있으니 일·순찰·배회가
// 안 돌고, 재료가 생기면 그 틱에 곧바로 걸어간다.
public static class Recharge
{
    // **한 번 채우는 데 드는 시간.** 밥(2초)보다 길다 — 자리를 차지하고 서 있는 것이
    // 이 축의 값이라, 순식간이면 충전대 하나로 온 콜로니가 돈다.
    public const float Seconds = 6f;

    // 이 폰이 채워야 하는 축. **산 몸이면 없다** — 밥과 잠은 `Feeding`·`RestNode`가 답한다.
    public static bool TryAxisOf(UnitBrain brain, out NeedKind kind)
    {
        kind = NeedKind.Energy;
        if (brain == null) return false;

        switch (Needs.BodyOf(brain))
        {
            case BodyKind.Construct: kind = NeedKind.Energy; return true;
            case BodyKind.Undead:    kind = NeedKind.Morale; return true;
            default: return false;
        }
    }

    // **도는 축일 때만 참이다** — 갇힌 해골은 사기가 안 도는데(`Needs.Simulates`) 여기서
    // 임계만 보면 **채우러 갈 수 없는 폰이 영영 채우려 든다.**
    public static bool IsLow(UnitBrain brain, NeedKind kind)
        => Needs.Simulates(brain, kind) && Needs.IsLow(brain, kind);

    // 바닥에 닿았나 — 골렘이면 여기서 멈춘다.
    public static bool Stalled(UnitBrain brain, NeedKind kind)
    {
        var bb = brain != null ? brain.Blackboard : null;
        return bb != null && bb.Need(kind) <= Needs.CollapseAt;
    }

    // ── 어디로 가나 ──────────────────────────────────────────────────────
    //
    // **재료가 있는 곳을 고르지 않는다 — 가까운 곳을 고른다.** 재료는 창고에서 나오므로
    // (`MaterialPool`) 어느 충전대를 골라도 같은 재고를 본다. 고를 것이 거리뿐이라
    // `Feeding.BestInStorage`처럼 "무엇이 들었나"를 물을 자리가 없다.
    public static Recharger Nearest(NeedKind kind, Vector3 from)
    {
        Recharger best = null;
        float bestSq = float.PositiveInfinity;

        foreach (var r in Recharger.All)
        {
            if (r == null || !r.isActiveAndEnabled || r.Fills != kind) continue;

            // **힘을 못 받는 충전대는 고르지 않는다**(2026-09-06). 고르고 나서 못 쓰면
            // 폰이 **걸어와서 빈손으로 접고 다시 온다** — 재료가 없을 때 안 고르는 것과
            // 같은 규칙이다(`PowerUser`가 없는 옛 건물은 예전 그대로 참이다).
            if (!PowerUser.Ok(r)) continue;

            float sq = (r.transform.position - from).sqrMagnitude;
            if (sq >= bestSq) continue;

            bestSq = sq;
            best   = r;
        }
        return best;
    }

    // ── 재료 ─────────────────────────────────────────────────────────────
    //
    // **세는 것과 쓰는 것이 같은 문을 봐야 한다**(`MaterialPool`) — 갈리면 폰이 걸어와서
    // 빈손으로 접고 다시 오기를 되풀이한다(`Feeding.ExistsAnywhere`가 먼저 겪은 자리).
    public static bool HasFuel(Recharger site, UnitBrain carrier)
        => site != null && (site.Fuel == null
                            || MaterialPool.Available(site.Fuel, carrier) >= site.FuelAmount);

    public static bool Consume(Recharger site, UnitBrain carrier)
        => site != null && (site.Fuel == null
                            || MaterialPool.Consume(site.Fuel, site.FuelAmount, carrier));

    // ── 채운다 ───────────────────────────────────────────────────────────
    //
    // **한 번에 가득 찬다.** 음식처럼 영양이 갈리지 않으므로 *무엇을 고를까*가 없고, 그래서
    // 낭비를 아끼는 `Feeding.Better`에 해당하는 규칙도 없다 — 대신 임계에서만 오므로
    // 한 번의 값이 곧 하루치다.
    public static void Fill(UnitBrain brain, NeedKind kind)
    {
        var bb = brain != null ? brain.Blackboard : null;
        if (bb == null) return;

        bb.SetNeed(kind, Needs.Full);
    }
}

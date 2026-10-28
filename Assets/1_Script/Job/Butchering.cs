using System.Collections.Generic;
using UnityEngine;

// **시체에서 무엇이 나오나 — 답은 여기 하나다.**
//
// 도축하는 길이 앞으로 늘어날 수 있고(우클릭 명령 · 나중에 도축대 · 매장 시 부산물), 그때마다
// "썩은 고기는 안 나온다"를 각자 적으면 반드시 한쪽이 빠진다. 이 저장소가 `CarryCapacity`·
// `Feeding`·`Treatment`에서 이미 겪은 그 모양이다.
//
// ── 단계가 곧 규칙이다 ────────────────────────────────────────────────────────
//
//   신선 → 몸에서 나오는 것 전부 + 뼈
//   썩음 → **음식만 빠진다** + 뼈        (살점은 못 쓰지만 가죽은 아직 쓸 만하다)
//   뼈   → 뼈만
//
// 음식인지는 **물건이 답한다**(`is FoodData`) — 종족마다 "이건 고기다"를 따로 적게 하면
// 저작 실수 하나로 썩은 고기가 나온다.
public static class Butchering
{
    // 이 단계에서 나올 것들을 굴린다. `(아이템, 개수)` 목록.
    public static List<(ItemData item, int count)> Roll(UnitData race, CorpseStage stage)
    {
        var result = new List<(ItemData, int)>();
        if (race == null) return result;

        // 살점·가죽 — 뼈만 남은 몸에는 없다.
        if (stage != CorpseStage.Bones)
            foreach (var e in race.ButcherYield)
            {
                var item = e?.Resolve();
                if (item == null) continue;

                // **썩으면 먹을 것이 안 나온다.** 가죽은 그대로다.
                if (stage == CorpseStage.Rotten && item is FoodData) continue;

                int n = e.Roll();
                if (n > 0) result.Add((item, n));
            }

        // 뼈 — 단계를 안 가린다.
        foreach (var e in race.BoneYield)
        {
            var item = e?.Resolve();
            if (item == null) continue;

            int n = e.Roll();
            if (n > 0) result.Add((item, n));
        }

        return result;
    }

    // **도축할 값이 있나.** 아무것도 안 나오는 시체에 [도축한다]를 띄우면 폰을 보내 놓고
    // 빈손으로 돌아온다 — 저작이 비었는지(사람 시체 등) 여기서 먼저 답한다.
    //
    // 굴림이 아니라 **저작 목록**을 본다: 확률로 0이 나올 수 있다고 선택지를 감추면
    // 같은 시체가 눌러 볼 때마다 있었다 없었다 한다.
    public static bool HasYield(UnitData race, CorpseStage stage)
    {
        if (race == null) return false;

        if (stage != CorpseStage.Bones)
            foreach (var e in race.ButcherYield)
            {
                var item = e?.Resolve();
                if (item == null) continue;
                if (stage == CorpseStage.Rotten && item is FoodData) continue;
                if (e.MaxCount > 0) return true;
            }

        foreach (var e in race.BoneYield)
            if (e?.Resolve() != null && e.MaxCount > 0) return true;

        return false;
    }

    // **산 채로 쓰러진 것을 가를 수 있나.**
    //
    // 기절은 회복 가능한 상태라(켄시식) 그대로 두면 일어난다 — 짐승을 때려눕히고 그 자리에서
    // 손질하는 것이 사냥의 정상 경로이고, **가장 신선할 때** 가르는 길이기도 하다.
    //
    // 조건은 셋뿐이다: **쓰러져 있을 것**(멀쩡한 것을 클릭 한 번에 잡는 건 사냥이 아니다) ·
    // **내 편이 아닐 것** · 아직 살아 있을 것. 대가(세력 우호도, 가둬 둔 자면 처형 값)는
    // `UnitBrain.Die`가 이미 문다 — 여기서 셈하지 않는다.
    //
    // 부르는 쪽이 둘이라(우클릭 메뉴 · 일감의 재확인) 답은 여기 하나여야 한다. 갈리면
    // "메뉴엔 떴는데 가서는 안 한다"가 된다.
    public static bool CanButcherAlive(UnitBrain brain)
    {
        if (brain == null || brain.IsDead) return false;

        var bb = brain.Blackboard;
        if (bb == null || !bb.IsKnockedOut) return false;

        var faction = brain.GetComponent<Faction>();
        return faction == null || !faction.IsPlayer;
    }

    // **이 몸을 가를 수 있나 — 시체든 산 채로 쓰러진 것이든.**
    //
    // 두 물음을 하나로 묶는다: *가를 수 있는 상태인가*(시체이거나 쓰러졌거나) 와
    // *나올 것이 있는가*. 부르는 쪽이 셋이라(우클릭 · 도축대 우클릭 · 도축대 규칙)
    // 각자 물으면 "메뉴엔 떴는데 가서는 안 한다"가 생긴다.
    public static bool CanButcher(UnitBrain brain)
    {
        if (brain == null) return false;

        var corpse = brain.GetComponent<Corpse>();
        if (corpse == null)
            return CanButcherAlive(brain) && HasYield(brain.Data, CorpseStage.Fresh);

        return HasYield(brain.Data, corpse.Stage);
    }

    public static string StageName(CorpseStage stage) => stage switch
    {
        CorpseStage.Fresh  => "신선",
        CorpseStage.Rotten => "썩음",
        _                  => "뼈",
    };
}

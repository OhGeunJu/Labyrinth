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
// **몸에서 나오는 돌의 갈래.** 정수로 직렬화된다 — 꼬리에만 붙일 것.
// `Default`는 *"여기서는 안 정한다"*다: 종족이면 마력석, 세력이면 종족을 따른다.
public enum BodyStone { Default = 0, Magic = 1, Holy = 2, Void = 3, None = 4 }

public static class Butchering
{
    // ── 몸에서 나오는 돌 ─────────────────────────────────────────────────────
    //
    // (2026-11-11, 사용자 설계) *"인간 엘프나 새로운 종족을 잡으면 신성돌 · 해골 좀비는 어둠돌 ·
    // 그 외 미궁몹은 마력석"* · 오크는 신성 · 고블린 · 트롤은 마력 · *"사람은 층보다는 스탯 기준"*.
    //
    // **도축으로 나온다** — 몸에서 나오는 것은 갈라야 나온다는 축(`UnitData.ButcherYield`)과 같다.
    // **편을 안 가린다**(사용자: *"같은 팀에도 당연히 나와야지"*) — 동료를 가르는 것을 한 번 묻는
    // 것은 실수를 막는 확인이지 막는 규칙이 아니다. 뼈처럼 **단계를 안 가린다**.
    //
    // 답이 셋에서 오므로(종족 · 일으켜진 몸 · 세력) 묻는 자리는 여기 하나다.
    public static BodyStone StoneOf(UnitBrain brain, FactionData faction)
    {
        var race = brain != null ? brain.Data : null;
        if (race == null) return BodyStone.None;

        // 한 번 죽은 몸 — 해골이든 일으켜진 사람이든.
        if (brain.IsUndead) return BodyStone.Void;

        if (faction != null && faction.Stone != BodyStone.Default) return faction.Stone;

        return race.Stone == BodyStone.Default ? BodyStone.Magic : race.Stone;
    }

    // 층마다 마력석 하나(`docs/design_metals.md` "마력석도 층마다 하나다") — 죽은 자리의 층.
    static readonly string[] MagicByFloor =
        { "Res_WeakMagicStone", "Res_MagicStone", "Res_RefinedMagicStone", "Res_AbyssMagicStone" };

    const float MagicChance = 0.35f;   // 미궁 몹 — 한 마리에 한 개, 셋에 하나꼴
    const float VoidChance  = 0.40f;   // 망자 · 교단 — 1~2개
    const float HolyPerStat = 40f;     // 신성 — 상위 스탯 셋의 평균 40마다 하나(나머지는 확률)

    static (ItemData item, int count) RollStone(UnitBrain brain, FactionData faction, Vector3 at)
    {
        switch (StoneOf(brain, faction))
        {
            case BodyStone.Magic:
            {
                int floor = Mathf.Clamp(Floors.At(at), 1, MagicByFloor.Length);
                var item  = DataManager.GetItem(MagicByFloor[floor - 1]);
                return (item, Random.value < MagicChance ? 1 : 0);
            }
            case BodyStone.Void:
                return (DataManager.GetItem("Res_Voidstone"),
                        Random.value < VoidChance ? Random.Range(1, 3) : 0);
            case BodyStone.Holy:
            {
                // **층이 아니라 그 사람이 얼마나 뛰어났나**(사용자). 갓 영입한 F급(~10)은 넷에
                // 하나꼴, 한 분야를 갈고닦은 60은 1.5개, 130은 3.25개 — 상한 없이 곡선으로 는다.
                float x = TopStatMean(brain) / HolyPerStat;
                int   n = Mathf.FloorToInt(x);
                if (Random.value < x - n) n++;
                return (DataManager.GetItem("Res_Etherstone"), n);
            }
            default:
                return (null, 0);
        }
    }

    static readonly StatName[] AllStats = (StatName[])System.Enum.GetValues(typeof(StatName));

    // 상위 스탯 셋의 평균 — 한 분야만 높은 사람과 두루 높은 사람을 둘 다 센다.
    static float TopStatMean(UnitBrain brain)
    {
        var stats = brain != null ? brain.Stats : null;
        if (stats == null) return 0f;

        float a = 0f, b = 0f, c = 0f;
        foreach (var s in AllStats)
        {
            float v = stats.GetStat(s);
            if      (v > a) { c = b; b = a; a = v; }
            else if (v > b) { c = b; b = v; }
            else if (v > c) { c = v; }
        }
        return (a + b + c) / 3f;
    }

    // 이 단계에서 나올 것들을 굴린다. `(아이템, 개수)` 목록.
    // `faction`은 시체면 `Corpse.Faction`(컴포넌트는 지워져 있다), 산 것이면 그 폰의 세력.
    public static List<(ItemData item, int count)> Roll(UnitBrain brain, FactionData faction,
                                                         CorpseStage stage, Vector3 at)
    {
        var result = new List<(ItemData, int)>();
        var race   = brain != null ? brain.Data : null;
        if (race == null) return result;

        var (stoneItem, stoneCount) = RollStone(brain, faction, at);
        if (stoneItem != null && stoneCount > 0) result.Add((stoneItem, stoneCount));

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

        // 돌이 나올 수 있는 몸. 세력 · 일으켜짐이 `None`을 덮어쓸 수는 있어도 거꾸로는
        // 없으니(지금 `None`을 적는 세력이 없다) 종족만 봐도 답이 같다.
        if (race.Stone != BodyStone.None) return true;

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

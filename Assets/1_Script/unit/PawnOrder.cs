using System.Collections.Generic;

// **무리를 어떤 순서로 늘어놓나 — 답은 여기 하나.**
//
// **방랑자 먼저, 그다음 하수인. 같은 종류끼리는 이름순.** 방랑자가 앞인 것은 명령을 받는 쪽이
// 먼저 보여야 하기 때문이고(하수인은 알아서 일한다), 이름순인 것은 스탯이나 체력으로 정렬하면
// 같은 폰이 다칠 때마다 목록에서 자리를 옮겨 **눈이 매번 다시 찾아야** 하기 때문이다.
//
// 로스터(`PawnRosterUI`)와 무리 관리 패널(`ColonyUI`)이 **같은 비교식을 복붙해** 갖고 있었다.
// 거기에 "무리 첫 번째를 자동으로 고른다"(`SelectionManager`)가 셋째로 붙을 참이라 모았다 —
// 순서가 갈리면 **명부에서 맨 위인 폰과 자동으로 골리는 폰이 달라져**, 왜 저 애가 골렸는지
// 설명할 수 없게 된다.
public static class PawnOrder
{
    // 목록에 늘어놓는 순서.
    public static int Compare(UnitBrain a, UnitBrain b)
    {
        if (a == null || b == null) return 0;

        bool aMinion = a.Data is MinionData;
        bool bMinion = b.Data is MinionData;
        if (aMinion != bMinion) return aMinion ? 1 : -1;

        return string.CompareOrdinal(a.UnitName, b.UnitName);
    }

    // **무리 첫 번째** — 지금 이 맵에 있는 우리 편 중 목록 맨 위에 설 폰.
    //
    // `Faction.All`을 훑는다(`Trading.BestOnField`·`MaterialPool`과 같은 통로). 그 레지스트리가
    // 이미 "지금 살아 있고 켜져 있는 것"을 들고 있어서 **원정 나간 폰은 저절로 빠진다**(그 사람은
    // 여기 없다). 시체는 `Faction`이 떼어져 빠지고, 용병은 세력이 그대로라 안 걸린다.
    //
    // **갇히거나 업힌 폰은 뺀다** — 명령을 못 받으므로 골라 놔야 "시켰는데 아무 일도 안 일어난다"가
    // 된다(`MoveController`가 이미 같은 이유로 명령 목록에서 뺀다).
    //
    // 아무도 없으면 null — 폰이 다 죽었거나 전부 원정을 나갔다는 뜻이다.
    public static UnitBrain FirstOfBand()
    {
        UnitBrain best = null;

        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;

            var brain = f.GetComponent<UnitBrain>();
            if (brain == null) continue;

            // **기절한 폰도 뺀다** — `PawnMover.SetMoveTarget`이 거부하므로 골라 놓으면
            // 이 함수가 없애려던 "시켰는데 아무 일도 안 일어난다"가 그대로 남는다.
            var bb = brain.Blackboard;
            if (bb != null && (bb.IsPrisoner || bb.CarriedBy != null || bb.IsKnockedOut)) continue;

            if (best == null || Compare(brain, best) < 0) best = brain;
        }
        return best;
    }

    // 정렬 편의 — 목록을 통째로 늘어놓을 때.
    public static void Sort(List<UnitBrain> pawns) => pawns?.Sort(Compare);
}

using System.Collections.Generic;
using UnityEngine;

// 업은 사람을 **어떤 자리로** 데려가나. 특정 우리/침대가 아니라 **종류**다.
//
// `UnitBlackboard.PutDownBed`/`PutDownCage`와 다른 축이다 — 그쪽은 "저 우리에 넣어라"라는
// **한 자리**를 가리키고(우리를 우클릭해서 고른 것), 이쪽은 "우리에 넣어라"라는 **종류**다.
// 종류로 두면 걸어가는 동안 그 칸이 차도 다음 칸으로 갈아탈 수 있고, 자리를 미리 고르지
// 않으므로 여럿이 동시에 나를 때 서로 같은 칸을 노리다 헛걸음하지 않는다.
public enum EscortDest
{
    None,   // 데려갈 곳 없음 — 업은 채로 있는다(간수가 꺼져 있으면 여기)
    Cage,   // 우리로
    Bed,    // 죄수 침대로
}

// **붙잡은 사람을 자리에 넣는 일.** 간수(`JobCategory.Warden`)가 하는 두 가지 중 하나다
// (나머지는 설득 — `PersuadeJob`).
//
// ── 왜 일감이 아니라 업은 폰이 하는가 ──────────────────────────────────────────
//
// 일감으로 만들면 "누가 업을 것인가"부터 정해야 하는데, 업는 것은 이미 명령이다(우클릭
// [업는다]). 업은 다음에 **다른 폰을 불러 넘겨받게** 하면 어깨에서 어깨로 옮기는 동작이
// 필요하고, 그동안 업은 폰은 한 손이 막힌 채 서 있는다.
//
// 그래서 **업은 폰이 그대로 데려간다.** 간수 설정은 "이 폰이 그 걸음을 걸을까"를 정한다:
//  · **켜져 있으면** 붙잡은 사람을 업는 순간 자리로 데려간다 — 전투가 끝나면 저절로 정리된다.
//  · **꺼져 있으면 계속 업고만 있는다** — 어디에 둘지는 플레이어가 정한다(우클릭).
//
// 손으로 시킨 [우리로 옮긴다]·[죄수 침대로 옮긴다]는 **설정을 안 본다**. 손으로 시킨 일은
// "이 작업을 맡길까"의 결과가 아니라 지금 이 순간의 지시다(자원 노드 직접 배정·[치료한다]와
// 같은 규칙).
public static class PrisonEscort
{
    // **잡아 둘 사람인가.** 남인데 쓰러졌거나, 이미 갇혀 있거나(자리를 옮기는 중).
    //
    // **우리 폰은 절대 아니다 — 갇혀 있어도.** 멀쩡한 동료를 침대로 옮기다 간수가 켜져 있다고
    // 우리에 처넣으면 사람을 나르는 일이 통째로 못 쓸 것이 되고, 우리 폰을 우리에서 [끌어낸다]로
    // 꺼내면 간수가 곧바로 도로 집어넣는다. 우리 편을 가두는 것은 늘 **손으로 시키는 일**이다
    // (우리·침대를 눌러 [여기 가둔다]).
    public static bool IsCaptive(UnitBrain brain)
    {
        if (brain == null || brain.Blackboard == null) return false;

        var faction = brain.GetComponent<Faction>();
        if (faction != null && faction.IsPlayer) return false;

        return brain.Blackboard.IsPrisoner || brain.Blackboard.IsKnockedOut;
    }

    // 이 폰이 간수 일을 맡고 있나. `WorkSettings`가 없는 폰(적·NPC)은 아니다.
    public static bool IsWarden(UnitBrain brain)
    {
        var work = brain != null ? brain.Work : null;
        return work != null && work.IsEnabled(JobCategory.Warden);
    }

    // ── 자리 찾기 ────────────────────────────────────────────────────────────

    public static PrisonCage FindCage(Vector3 from) => PrisonCage.FindNearestFree(from);

    public static Bed FindPrisonBed(Vector3 from) => Bed.FindNearestFreePrison(from);

    // **어디로 데려갈지 지금 다시 정한다.** 매 틱 불린다 — 걸어가는 사이에 그 칸이 차면
    // 다음 칸으로 갈아타야 하고, 자리가 하나도 없으면 업은 채로 서 있어야 한다.
    //
    // `dest`가 `None`이면 **간수의 자동 판단**이다:
    //   **다쳤으면 죄수 침대, 아니면 우리.** 우리는 돌려세우는 자리고 침대는 살려 두는
    //   자리라(`Conversion`의 `InCage` 주석), 피 흘리는 사람을 우리에 세워 두면 회유가
    //   시작되기 전에 죽는다. 원하는 쪽이 없으면 있는 쪽으로 간다 — 자리에 넣는 것이
    //   길에 눕혀 두는 것보다 늘 낫다.
    public static bool Resolve(UnitBrain carried, EscortDest dest, Vector3 from,
                               out Bed bed, out PrisonCage cage)
    {
        bed  = null;
        cage = null;
        if (carried == null) return false;

        switch (dest)
        {
            case EscortDest.Cage:
                cage = FindCage(from);
                return cage != null;

            case EscortDest.Bed:
                bed = FindPrisonBed(from);
                return bed != null;
        }

        var body = carried.GetComponent<BodyPartSystem>();
        bool hurt = body != null && body.NeedsTreatment;

        if (hurt)
        {
            bed = FindPrisonBed(from);
            if (bed != null) return true;
        }

        cage = FindCage(from);
        if (cage != null) return true;

        bed = FindPrisonBed(from);
        return bed != null;
    }

    // ── 명령 ─────────────────────────────────────────────────────────────────
    //
    // **"누구를 보내 어디로 데려가나"의 단일 출처.** 문이 둘이다 — 우클릭 메뉴와
    // `UnitHP_Panel` 버튼. 각자 적으면 한쪽만 "이미 업고 있으면 그 폰에게 시킨다"를 빠뜨리는
    // 식으로 갈라진다(이 저장소가 여러 번 겪은 그것).

    // 이 사람을 그 종류의 자리로 데려가라고 시킬 수 있나 — 빈 자리가 있고, 보낼 폰이 있을 때.
    // 빈 자리가 없으면 **선택지를 아예 안 띄운다**: 눌러도 아무 일이 안 일어나면 명령이
    // 먹혔는지 알 길이 없다.
    public static bool CanOrder(IReadOnlyList<PawnMover> commanders, UnitBrain target, EscortDest dest)
    {
        if (!IsCaptive(target)) return false;
        if (PickCarrier(commanders, target) == null) return false;

        var from = target.transform.position;
        return dest == EscortDest.Bed ? FindPrisonBed(from) != null : FindCage(from) != null;
    }

    public static void Order(IReadOnlyList<PawnMover> commanders, UnitBrain target, EscortDest dest)
    {
        var carrier = PickCarrier(commanders, target);
        if (carrier == null) return;

        carrier.Blackboard.EscortTo = dest;

        // 아직 안 업었으면 업으러 보낸다. **다른 폰의 `CarryTarget`은 지운다** — 사람 하나를
        // 여럿이 업으러 가면 먼저 닿은 하나만 업고 나머지는 헛걸음한다([줍는다]와 같은 규칙).
        if (target.Blackboard.CarriedBy == null)
            foreach (var pawn in commanders)
            {
                var b = pawn != null ? pawn.GetComponent<UnitBrain>() : null;
                if (b?.Blackboard == null) continue;
                b.Blackboard.CarryTarget = b == carrier ? target : null;
            }

        carrier.AbandonWork();   // 하던 일을 놓아야 이 명령이 돈다
    }

    // **이미 업고 있으면 그 폰이 간다** — 다시 업을 필요가 없다. 아니면 업을 수 있는 폰 중
    // 가장 가까운 하나([업는다]와 같은 규칙: 가장 가까운 폰이 이미 누굴 업고 있어도
    // 선택지가 사라지지 않게, 고르고 나서 거르지 말고 **거르고 나서 고른다**).
    static UnitBrain PickCarrier(IReadOnlyList<PawnMover> commanders, UnitBrain target)
    {
        if (commanders == null || target?.Blackboard == null) return null;

        var holder = target.Blackboard.CarriedBy;
        if (holder != null)
        {
            // 남의 폰(적)이 업고 있으면 시킬 수 없다.
            foreach (var pawn in commanders)
                if (pawn != null && pawn.GetComponent<UnitBrain>() == holder) return holder;
            return null;
        }

        UnitBrain best = null;
        float bestDist = float.MaxValue;

        foreach (var pawn in commanders)
        {
            if (pawn == null) continue;
            var b = pawn.GetComponent<UnitBrain>();
            if (b == null || !PawnCarry.CanCarry(b, target)) continue;

            float d = (pawn.transform.position - target.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best;
    }
}

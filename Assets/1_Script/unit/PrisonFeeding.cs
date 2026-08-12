using UnityEngine;

// **가두는 자리** — 우리(`PrisonCage`)와 죄수 침대(`Bed.ForPrisoners`)가 같은 규약으로
// 먹이기 일감을 든다. 자리마다 규칙을 따로 적으면 한쪽에서 포로가 조용히 굶는다.
public interface IPrisonSite
{
    // 지금 이 자리에 **갇혀 있는** 사람. 없으면 null.
    UnitBrain HeldPrisoner { get; }

    // 이 자리가 든 먹이기 일감(한 번에 하나) — 치료·설득과 같은 그릇을 쓴다.
    SiteJobSlot FeedSlot { get; }
}

// **붙잡은 사람을 먹인다** — 간수(`JobCategory.Warden`)가 하는 세 가지 중 하나다
// (나머지는 설득 `PersuadeJob`과 데려가기 `PrisonEscort`).
//
// **왜 여기 모았나.** 가두는 곳은 둘인데 먹이기는 양쪽 다 필요하다 — 우리는 회유하는
// 자리고 침대는 살려 두는 자리인데, 어느 쪽이든 굶기면 안 된다. 설득은 우리에만 있으니
// `PrisonCage`가 직접 들어도 됐지만(그래서 그렇게 돼 있다), 먹이기를 자리마다 적으면
// 반드시 어긋난다: 침대의 포로만 굶거나, 한쪽만 쿨타임을 안 보거나.
//
// **일감의 주인은 자리다**(`Bed`가 `TreatJob`을, `PrisonCage`가 `PersuadeJob`을 내는 것과
// 같은 자리). 포로 자신이 내면 죽거나 넘어왔을 때 스스로 등록을 못 지우고, 누르는 사람마다
// 만들면 한 사람에게 여럿이 붙는다.
public static class PrisonFeeding
{
    // 이 사람에게 밥이 필요한가 — **갇혀 있고, 배고프다.**
    //
    // **기절한 포로도 먹인다.** 허기는 기절 중에도 도는데(`UnitBrain.UpdateHunger`) 그때
    // 안 먹이면, 오래 쓰러져 있는 포로는 반드시 굶어 죽는다 — 깨어나기를 기다리는 것이
    // 곧 죽기를 기다리는 것이 된다.
    // (죽었나·업혀 있나는 `Feeding.Simulates`가 이미 본다 — 여기서 또 세면 두 벌이 된다.)
    public static bool NeedsFood(UnitBrain prisoner)
    {
        var bb = prisoner != null ? prisoner.Blackboard : null;
        if (bb == null || !bb.IsPrisoner) return false;

        return Feeding.Simulates(prisoner) && Feeding.IsHungry(prisoner);
    }

    // **이 사람이 갇혀 있는 자리.** 우클릭 명령이 일감을 넘길 곳을 찾는 데 쓴다 —
    // 부르는 쪽이 우리인지 침대인지 따지게 하면 언젠가 한쪽을 빠뜨린다.
    public static IPrisonSite SiteOf(UnitBrain prisoner)
    {
        if (prisoner == null) return null;

        var cage = PrisonCage.HolderOf(prisoner);
        if (cage != null) return cage;

        var bed = Bed.FindOccupiedBy(prisoner);
        return bed != null && bed.HeldPrisoner == prisoner ? bed : null;
    }

    // **먹이기 슬롯을 만든다** — 자리는 `Awake`에서 이걸 부르기만 한다.
    //
    // 만드는 법과 걸어도 되는 조건이 자리마다 같으므로 여기서 묶는다. 자리에 맡기면
    // 우리와 침대가 같은 두 줄을 각자 적게 되고, 그게 어긋나는 자리다.
    // (`Awake`에서 부르는 이유: 필드 초기화식에서는 `this`를 못 잡는다 — CS0236.)
    public static SiteJobSlot NewSlot(IPrisonSite site)
        => new(w => new FeedJob(site.HeldPrisoner, w), Feeding.ExistsAnywhere);

    // 자리가 주기적으로 부른다(`Update`, 1초).
    //
    // **먹일 것이 없으면 아직 안 건다** — 걸어 두고 `IsReady`로 막으면 폰이 우리 앞까지
    // 갔다가 빈손으로 돌아서는 일이 되풀이된다. 물자가 들어오면 1초 뒤에 걸린다.
    // (`ExistsAnywhere`는 창고를 전부 훑으므로 **이미 일감이 있으면 안 불린다** —
    // `SiteJobSlot.Tick`이 지연 평가한다.)
    public static void Tick(IPrisonSite site)
    {
        if (site?.FeedSlot == null) return;
        site.FeedSlot.Tick(NeedsFood(site.HeldPrisoner));
    }

    // **이 폰에게 맡긴다**(우클릭 [먹인다]). 넘기는 규칙은 `SiteJobSlot`이 든다 —
    // 여기서 답하는 것은 "누구를, 어느 자리에서"뿐이다.
    public static bool RequestBy(UnitBrain prisoner, UnitBrain feeder)
    {
        if (feeder == null || feeder == prisoner) return false;
        if (!NeedsFood(prisoner)) return false;

        var site = SiteOf(prisoner);
        return site?.FeedSlot != null && site.FeedSlot.RequestBy(feeder);
    }

    // 자리가 사라지거나 포로가 없어졌을 때.
    public static void Drop(IPrisonSite site) => site?.FeedSlot?.Drop();
}

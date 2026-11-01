using UnityEngine;

// **납치 노드 둘**(2026-11-01 — `Abduction`). 먹는 종족(`UnitData.Devours`)의 트리에만 붙는다(`AddCombatBranch`).
//
// 둘 다 **떠나기 · 도주 · 교전보다 위다.** 메고 가는 동안은 싸우지도 떠나지도 않는다 — 한 손이라 약하고
// (`PawnCarry`), 쫓아와 쓰러뜨리면 떨어뜨린다. 메러 가는 것도 위인 이유: 졌거나 후퇴하는 습격대가 쓰러진 사람을
// 그냥 두고 가지 않게. 다만 **패주(`Routed`)하는 놈은 안 멘다**, 그리고 지키는 이가 오면 그 자리에서 접는다.

// 메고 굴로 간다. 닿으면 내려놓고 묶는다(`Abduction.Deliver`).
public class HaulToDenNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        var self   = bb.Brain;
        var victim = bb.Carrying;
        if (self == null || victim == null) return BTStatus.Failure;

        // 우리 폰을 멘 것만 — 다른 짐(시체 등)은 이 노드의 일이 아니다.
        if (PawnCarry.IsCorpse(victim) || !victim.TryGetComponent(out Faction f) || !f.IsPlayer)
            return BTStatus.Failure;

        if (!Abduction.TryHaulDen(self, out var den))
        {
            PawnCarry.PutDown(self);
            return BTStatus.Failure;
        }

        var flat = den - bb.Self.transform.position; flat.y = 0f;
        if (flat.sqrMagnitude > Abduction.ArriveDist * Abduction.ArriveDist)
        {
            bb.Mover.SetMoveTarget(den);
            return BTStatus.Running;
        }

        bb.Mover.StopMovement();
        Abduction.Deliver(self, den);
        return BTStatus.Success;
    }
}

// 쓰러진 내 폰을 메러 간다. 지키는 이가 오거나 남이 먼저 메면 접는다.
public class SeizeDownedNode : BTNode
{
    const float ReachDistance = 1.4f;   // `CarryPawnNode`와 같다
    const float LookEvery     = 1f;     // 찾는 것은 1초에 한 번(실시간 — 틱 값이 아니다)

    UnitBrain _prey;
    float     _nextLook;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var self = bb.Brain;
        if (self == null || bb.Carrying != null || bb.Routed) { Drop(self); return BTStatus.Failure; }

        if (_prey != null && !Abduction.StillPrey(self, _prey)) Drop(self);
        if (_prey == null)
        {
            if (Time.time < _nextLook) return BTStatus.Failure;
            _nextLook = Time.time + LookEvery;
            _prey = Abduction.FindPrey(self);
            if (_prey == null) return BTStatus.Failure;
            Abduction.Claim(self, _prey);
        }

        if (Vector3.Distance(bb.Self.transform.position, _prey.transform.position) > ReachDistance)
        {
            bb.Mover.SetMoveTarget(_prey.transform.position);
            return BTStatus.Running;
        }

        bb.Mover.StopMovement();
        var prey = _prey;
        _prey = null;
        if (!PawnCarry.TryPickUp(self, prey)) { Abduction.Unclaim(self); return BTStatus.Failure; }
        return Abduction.BeginHaul(self) ? BTStatus.Running : BTStatus.Failure;
    }

    void Drop(UnitBrain self)
    {
        if (_prey == null) return;
        _prey = null;
        Abduction.Unclaim(self);
    }
}

// **끼니가 되면 기둥에 묶인 사람을 뜯는다**(`Devoured`). 교전 · 수색 **뒤**다 — 싸울 것이 있으면 먹지 않고, 다 끝나면 돌아와
// 하던 끼니를 잇는다. 뜯는 동안은 서서 손으로 뜯는 동작(`WorkAnim.Skinning` — 도축대의 가죽 벗기기)을 쓴다.
public class DevourNode : BTNode
{
    const float SeekRange = 25f;   // 이 안의 기둥만 본다(m)
    const float Reach     = 0.8f;  // 먹는 자리에 닿았다고 치는 거리(m)

    Devoured _meal;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var self = bb.Brain;
        if (self == null || bb.Carrying != null) return BTStatus.Failure;

        if (_meal == null || _meal.Eater != self) _meal = FindMeal(self);
        if (_meal == null || _meal.Post == null) return BTStatus.Failure;

        var spot = _meal.Post.EaterSpot;
        var flat = spot - bb.Self.transform.position; flat.y = 0f;
        if (flat.sqrMagnitude > Reach * Reach)
        {
            bb.Mover.SetMoveTarget(spot);
            return BTStatus.Running;
        }

        bb.Mover.StopMovement();
        bb.Mover.FaceTowards(_meal.transform.position);
        bb.Working = WorkAnim.Skinning;
        if (_meal.Gnaw(self, bb.TickDelta)) return BTStatus.Running;

        _meal = null;
        return BTStatus.Success;
    }

    static Devoured FindMeal(UnitBrain self)
    {
        var at = self.transform.position;
        foreach (var post in CaptivePost.All)
        {
            if (post == null || post.Captive == null) continue;
            if ((post.transform.position - at).sqrMagnitude > SeekRange * SeekRange) continue;
            if (post.Captive.TryGetComponent(out Devoured d) && d.TryClaimMeal(self)) return d;
        }
        return null;
    }
}

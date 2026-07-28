using UnityEngine;

public partial class UnitBrain
{
    BTNode BuildNpcTree(NpcData npc)
    {
        // 홈포인트 초기화 (배회·귀환 기준점)
        _bb.HomePosition = transform.position;
        _bb.HomeSet      = true;

        return npc.NpcType switch
        {
            NpcType.Villager      => BuildVillagerTree(npc),
            NpcType.Guard         => BuildGuardTree(npc),
            NpcType.ShopMerchant  => BuildShopMerchantTree(npc),
            NpcType.EventMerchant => BuildEventMerchantTree(npc),
            _                     => new IdleNode(),
        };
    }

    // 주민: (성향) → 배회 → 대기. 기본은 도주지만 AI 프로필로 싸우게 만들 수 있다(자경단 등).
    BTNode BuildVillagerTree(NpcData npc)
    {
        var root = new BTSelector();
        AddCombatBranch(root, CombatStance.Flee, npc.FleeDetectRange);
        AddWanderBranch(root, npc.WanderRadius);
        return root.Add(new IdleNode());
    }

    // 경비: (성향) → 순찰 → 배회 → 대기. 순찰 구역이 없으면 배회로 떨어진다.
    BTNode BuildGuardTree(NpcData npc)
    {
        var root = new BTSelector();
        AddCombatBranch(root, CombatStance.Fight, npc.FleeDetectRange);
        root.Add(new PatrolNode());
        AddWanderBranch(root, npc.WanderRadius);
        return root.Add(new IdleNode());
    }

    // 상점 상인: MerchantData 연결 → (성향) → 대기.
    // 기본은 도주지만 AI 프로필로 싸우게 할 수 있다(용병 상인 등). 위협이 없으면 자리를 지킨다.
    BTNode BuildShopMerchantTree(NpcData npc)
    {
        var merchant = GetComponent<Merchant>() ?? gameObject.AddComponent<Merchant>();
        merchant.Data = npc.MerchantData;

        var root = new BTSelector();
        AddCombatBranch(root, CombatStance.Flee, npc.FleeDetectRange);
        AddWanderBranch(root, npc.WanderRadius, defaultOn: false);  // 가게를 지켜야 하므로 기본 제자리
        return root.Add(new IdleNode());
    }

    // 이벤트 상인: 스폰 위치 기억 → 코어 근처로 이동 → 대기 → 귀환 → 소멸
    BTNode BuildEventMerchantTree(NpcData npc)
    {
        // 홈(귀환 목적지)은 스폰 위치, NpcDestination은 던전 코어 근처
        var core = DungeonCore.Instance;
        _bb.NpcDestination = core != null
            ? core.transform.position + (Vector3)(Random.insideUnitCircle * 5f)
            : transform.position;
        _bb.NpcPhase = 0;

        return new NpcEventMerchantNode(npc.StayDuration);
    }
}

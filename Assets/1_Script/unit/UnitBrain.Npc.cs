using UnityEngine;

public partial class UnitBrain
{
    BTNode BuildNpcTree(NpcData npc)
    {
        // 홈포인트 초기화
        _bb.HomePosition = transform.position;

        return npc.NpcType switch
        {
            NpcType.Villager      => BuildVillagerTree(npc),
            NpcType.Guard         => BuildGuardTree(npc),
            NpcType.ShopMerchant  => BuildShopMerchantTree(),
            NpcType.EventMerchant => BuildEventMerchantTree(npc),
            _                     => new IdleNode(),
        };
    }

    // 주민: 도주 → 배회 → 대기
    BTNode BuildVillagerTree(NpcData npc)
    {
        return new BTSelector()
            .Add(new NpcFleeNode(npc.FleeDetectRange))
            .Add(new NpcWanderNode(npc.WanderRadius))
            .Add(new IdleNode());
    }

    // 경비: 감지+전투 → 순찰 → 대기
    BTNode BuildGuardTree(NpcData npc)
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new PatrolNode())
            .Add(new IdleNode());
    }

    // 상점 상인: 그냥 서있음
    BTNode BuildShopMerchantTree()
    {
        return new IdleNode();
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

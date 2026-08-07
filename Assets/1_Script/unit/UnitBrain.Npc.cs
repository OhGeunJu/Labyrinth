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

    // 상점 상인: 상품 연결 → (성향) → 대기.
    // 기본은 도주지만 AI 프로필로 싸우게 할 수 있다(용병 상인 등). 위협이 없으면 자리를 지킨다.
    BTNode BuildShopMerchantTree(NpcData npc)
    {
        EnsureMerchant(npc);

        var root = new BTSelector();
        AddCombatBranch(root, CombatStance.Flee, npc.FleeDetectRange);
        AddWanderBranch(root, npc.WanderRadius, defaultOn: false);  // 가게를 지켜야 하므로 기본 제자리
        return root.Add(new IdleNode());
    }

    // 이벤트 상인: 스폰 위치 기억 → 코어 근처로 이동 → 대기 → 귀환 → 소멸
    //
    // **장사도 한다.** 전엔 `Merchant`를 안 붙여서 행상인이 거점 앞까지 걸어와 서 있다가
    // 그냥 돌아갔다 — 클릭해도 아무것도 안 열렸다(2026-08-07). 오래 머물지 않는다는 것이
    // 이 상인의 성질이지, 물건이 없다는 뜻이 아니다.
    BTNode BuildEventMerchantTree(NpcData npc)
    {
        EnsureMerchant(npc);

        // 홈(귀환 목적지)은 스폰 위치, NpcDestination은 던전 코어 근처
        var core = DungeonCore.Instance;
        _bb.NpcDestination = core != null
            ? core.transform.position + (Vector3)(Random.insideUnitCircle * 5f)
            : transform.position;
        _bb.NpcPhase = 0;

        return new NpcEventMerchantNode(npc.StayDuration);
    }

    // **상인은 "씌우는 것"이다.** 폰 프리팹(종족)에 `Merchant`를 붙이고 `MerchantData`를
    // 지정하면 그 폰이 그 물건을 파는 상인이 된다 — 고블린 무기상인, 엘프 식료상인이
    // 각각의 프리팹 계보를 만들지 않고도 성립한다.
    //
    // **누가 이기는가**: 컴포넌트에 이미 물린 상품 > 역할(`NpcData.MerchantData`)의 기본값.
    // 전엔 역할이 무조건 덮어써서, 프리팹에 상품을 물려 둬도 **역할이 비어 있으면 null로
    // 지워졌다**(빈 상점이 열린다). 그러면 "무엇을 파는가"를 역할 에셋에만 적을 수 있어
    // 종류마다 역할이 하나씩 필요해진다 — 역할 × 상품의 곱셈이다.
    // 스폰하는 쪽이 정해 주는 경우(`EventManager.SpawnMerchant`)는 이 뒤에 덮어쓴다.
    void EnsureMerchant(NpcData npc)
    {
        var merchant = GetComponent<Merchant>() ?? gameObject.AddComponent<Merchant>();
        if (merchant.Data == null && npc.MerchantData != null)
            merchant.Data = npc.MerchantData;

        if (merchant.Data == null)
            Debug.LogWarning($"[상인] {name}: 팔 물건이 없습니다 — 프리팹의 `Merchant.Data`나 " +
                             $"역할 에셋({npc.name})의 `MerchantData` 중 하나를 채우세요. " +
                             "지금은 빈 상점이 열립니다.");
    }
}

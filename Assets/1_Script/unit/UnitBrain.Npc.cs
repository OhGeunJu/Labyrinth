using UnityEngine;

public partial class UnitBrain
{
    BTNode BuildNpcTree(NpcData npc)
    {
        // 홈포인트 초기화 (배회·귀환 기준점)
        _bb.HomePosition = transform.position;
        _bb.HomeSet      = true;

        if (npc.Package == null)
            Debug.LogWarning($"[AI 패키지] {name}: 역할 '{npc.name}'에 패키지가 없다 — 대기만 한다.");
        else if (npc.Package.Trades)
            EnsureMerchant(npc);
        return BuildPackageTree(npc.Package);
    }

    // **패키지 → 트리**(2026-10-18). 목표를 위에서부터 셀렉터에 붙이고 맨 끝에 대기를 단다.
    // 전에는 `NpcType`마다 `BuildVillagerTree` 같은 함수가 하나씩이었다 — 그 다섯과 적 트리가 이제 패키지 여섯이다
    // (`Resources/Data/AiPackages/` · 순서와 값은 옮기기 전과 같다).
    BTNode BuildPackageTree(AiPackageData package)
    {
        var root = new BTSelector();
        if (package != null)
            foreach (var goal in package.Goals)
                BuildGoal(root, goal);
        return root.Add(new IdleNode());
    }

    // 목표 하나를 셀렉터에 붙인다. **새 목표는 `AiGoalKind` 꼬리에 더하고 여기 한 줄.**
    void BuildGoal(BTSelector root, AiGoal goal)
    {
        switch (goal.Kind)
        {
            case AiGoalKind.Combat:
                AddCombatBranch(root, goal.Stance, goal.Range);
                break;

            case AiGoalKind.Patrol:
                root.Add(new PatrolNode());   // 순찰 구역이 없으면 실패 — 아래 목표로 떨어진다
                break;

            case AiGoalKind.Wander:
                AddWanderBranch(root, goal.Radius, goal.WanderByDefault);
                break;

            // 시간을 재는 것과 걸어 나가는 것을 **따로 둔다**. 한 노드로 묶으면 "떠나는 중"이라는 상태가 그 노드
            // 안에만 있어서, 밖에서 떠나게 만드는 길(붙잡혔다 풀려나는 등)과 두 벌이 된다 — `UnitBlackboard.LeavingMap`
            // 하나가 그 답이다. 나가는 길은 `LeaveMapNode`가 풀려난 포로에게 이미 하는 일이다(맞으면 돌아선다).
            case AiGoalKind.Depart:
                root.Add(new DrifterDepartNode(goal.Seconds));
                root.Add(new LeaveMapNode());
                break;

            // 홈(귀환 목적지)은 스폰 위치, `NpcDestination`은 던전 코어 근처. 이 노드 하나가 들렀다 가는 하루 전부다.
            case AiGoalKind.VisitCore:
            {
                var core = DungeonCore.Instance;
                _bb.NpcDestination = core != null
                    ? core.transform.position + (Vector3)(Random.insideUnitCircle * 5f)
                    : transform.position;
                _bb.NpcPhase = 0;
                root.Add(new NpcEventMerchantNode(goal.Seconds));
                break;
            }

            default:
                Debug.LogWarning($"[AI 패키지] {name}: 모르는 목표 {goal.Kind} — 건너뛴다.");
                break;
        }
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
        if (!TryGetComponent(out Merchant merchant)) merchant = gameObject.AddComponent<Merchant>();
        if (merchant.Data == null && npc.MerchantData != null)
            merchant.Data = npc.MerchantData;

        if (merchant.Data == null)
            Debug.LogWarning($"[상인] {name}: 팔 물건이 없습니다 — 프리팹의 `Merchant.Data`나 " +
                             $"역할 에셋({npc.name})의 `MerchantData` 중 하나를 채우세요. " +
                             "지금은 빈 상점이 열립니다.");
    }
}

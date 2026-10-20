// 적 BT: 패키지(`AiPkg_Enemy` — 교전 → 배회 → 대기). 세력 관계가 "누가 적인가"를, AI 프로필이 "적을 만나면
// 어떻게 하는가"를, 패키지가 "평소에 무엇을 하나"를 정한다(2026-10-18 — 전에는 여기 코드로 짰다).
public partial class UnitBrain
{
    BTNode BuildEnemyTree() => BuildPackageTree(AiPackages.Enemy);

    // 성향에 따른 전투/도주 분기를 셀렉터에 붙인다. 패키지의 `Combat` 목표가 부른다.
    // fallback = 프로필이 없을 때의 기본 성향(역할별로 다름).
    // fleeRange = 0이면 유닛의 DetectRange 사용.
    void AddCombatBranch(BTSelector root, CombatStance fallback, float fleeRange = 0f)
    {
        _roleStanceFallback = fallback;   // 프로필도 오버라이드도 없을 때 쓰는 값

        // **떠나는 것이 도망보다 위다.** 풀려난 사람은 위협을 피해 서성이는 게 아니라
        // 가장자리로 가서 사라진다 — 도주가 먼저 잡으면 구석에서 맴돌기만 한다.
        // (맞으면 `LeaveMapNode`가 스스로 꺼지고 아래 분기가 평소대로 받는다.)
        root.Add(new LeaveMapNode());

        // 성향은 **매 틱** 읽는다(런타임 변경 즉시 반영). 거리 값만 조립 시점에 고정.
        root.Add(new FleeNode(FleeRange(fleeRange), FleeDist(), _ => ShouldFleeNow()));

        // 도주 성향이면 교전하지 않는다 — 조건으로 막아 런타임 전환에도 대응.
        AddFight(root, skipWhenFleeing: true);
    }

    // **전투 묶음 — 한 벌이다**(2026-10-18). 방랑자 · 하수인 · 가축 · NPC/적(`AddCombatBranch`) 네 트리가 같은
    // 묶음을 저마다 조립하고 있었다(하수인·NPC만 능동이 있고 · NPC만 도주 성향 조건이 있고). 노드를 하나 더할
    // 때마다 네 곳을 고쳐야 했고, 그 사이에 **순서 규칙**(능동은 감지와 교전 사이)이 주석으로만 지켜졌다.
    //
    // - **순서: 감지 → 능동 → 교전.** 능동은 *표적을 잡은 뒤·때리기 전이다* — 앞이면 겨눌 것이 없어 늘
    //   헛치고, 뒤면 `FightNode`가 `Running`을 물고 있어 영영 안 온다.
    // - **능동은 누구에게 넣어도 된다** — 사람이 눌러 주는 폰(`AbilityByHand`)에게는 `UseAbilityNode`가 아무것도
    //   안 하고 비켜 준다. 방랑자·가축 트리에 없던 것을 넣어도 그 폰들의 행동은 그대로다.
    // - **수색(`SearchNode`)이 묶음 바로 뒤에 붙는다** — 지각 기억(`docs/combat.md`).
    // - `skipWhenFleeing` — 도주 성향이면 묶음 전체를 건너뛴다(NPC·적). 내 폰은 끈다: 도주 성향 폰도
    //   맞으면 `Engaged`로 되받는 것이 지금 규칙이다(`DetectNode`).
    void AddFight(BTSelector root, bool skipWhenFleeing)
    {
        var fight = new BTSequence();
        if (skipWhenFleeing) fight.Add(new ConditionNode(_ => EffectiveStance != CombatStance.Flee));
        fight.Add(new DetectNode())
             .Add(new UseAbilityNode())
             .Add(new FightNode());
        root.Add(fight);

        // **놓친 표적을 찾는 것은 묶음 바로 뒤다**(2026-10-18) — 감지가 다시 잡으면 위 묶음이 `Running`을 물어
        // 여기는 안 돈다. 소집 · 도주 성향은 노드가 스스로 거른다(`SearchNode.MaySearch`).
        root.Add(new SearchNode());
    }

    // 플레이어 유닛(방랑자/하수인)용 도주 분기.
    // 기본 성향이 Fight라 **평소엔 즉시 실패**해서 기존 트리 동작에 영향이 없고,
    // 프로필을 붙이거나 SetStanceOverride를 호출하는 순간부터 도주가 켜진다.
    // 소집(Draft) 중에는 플레이어 명령이 우선이므로 도망치지 않는다.
    BTNode PlayerFleeNode()
    {
        _roleStanceFallback = CombatStance.Fight;
        return new FleeNode(FleeRange(0f), FleeDist(), bb => !bb.IsDrafted && ShouldFleeNow());
    }

    // 우선순위: 프로필 설정 > 호출자 지정(패키지 Combat 목표의 `Range`) > 유닛 DetectRange
    float FleeRange(float callerRange) =>
        aiProfile != null && aiProfile.FleeDetectRange > 0f ? aiProfile.FleeDetectRange
        : callerRange > 0f ? callerRange
        : unitData != null ? unitData.DetectRange : 10f;

    float FleeDist() => aiProfile != null ? aiProfile.FleeDistance : 15f;

    // 유휴 배회 분기. 프로필이 없으면 배회 ON(기본 반경 defaultRadius),
    // 있으면 그 설정을 따른다(Wander 해제 시 분기 자체를 넣지 않음 → 제자리 대기).
    void AddWanderBranch(BTSelector root, float defaultRadius, bool defaultOn = true)
    {
        bool on = aiProfile != null ? aiProfile.Wander : defaultOn;
        if (!on) return;

        float radius = aiProfile != null && aiProfile.WanderRadius > 0f
            ? aiProfile.WanderRadius : defaultRadius;
        if (radius <= 0f) return;

        if (aiProfile != null) root.Add(new WanderNode(radius, aiProfile.PauseMin, aiProfile.PauseMax));
        else                   root.Add(new WanderNode(radius));
    }

    // 혈액이 비율 아래인가 (BodyPartSystem이 없는 유닛은 판정 불가 → 계속 교전).
    bool IsBloodBelow(float ratio)
    {
        if (_body == null || _body.MaxBlood <= 0f) return false;
        return _body.Blood <= _body.MaxBlood * ratio;
    }
}

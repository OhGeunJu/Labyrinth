// 적 BT: 프로필(AiProfileData)에 따라 교전/도주 → 배회 → 대기
// 세력 관계가 "누가 적인가"를, AI 프로필이 "적을 만나면 어떻게 하는가"를 정한다.
public partial class UnitBrain
{
    BTNode BuildEnemyTree()
    {
        var root = new BTSelector();
        AddCombatBranch(root, CombatStance.Fight);   // 프로필 없으면 교전(기존 적 유닛 동작 유지)
        AddWanderBranch(root, defaultRadius: 12f);   // 할 일 없으면 제자리 대신 배회
        return root.Add(new IdleNode());
    }

    // 성향에 따른 전투/도주 분기를 셀렉터 앞쪽에 붙인다. 적·경비·주민·상인 트리 공용.
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
        root.Add(new BTSequence()
            .Add(new ConditionNode(_ => EffectiveStance != CombatStance.Flee))
            .Add(new DetectNode())
            .Add(new FightNode()));
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

    // 우선순위: 프로필 설정 > 호출자 지정(NPC 주민의 FleeDetectRange) > 유닛 DetectRange
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

using System;
using System.Collections.Generic;
using UnityEngine;

// 한 세력이 상대 세력을 어떻게 대하는가.
public enum FactionStance
{
    Ally,      // 아군 — 공격 안 함(사격 시 오사 회피 대상)
    Neutral,   // 중립 — 먼저 공격하지 않음(맞으면 반격)
    Hostile,   // 적대 — 발견 시 공격
}

// 세력 정의(SO). 관계는 **방향별**이다 — A가 B를 적대해도 B는 A를 중립으로 둘 수 있다.
// (예: 산적 → 마을사람 Hostile / 마을사람 → 산적 Neutral = 산적이 먼저 덤비고 마을사람은 도망)
// 맞았을 때의 반격은 관계와 무관하게 동작한다(UnitBlackboard.LastAttacker).
[CreateAssetMenu(fileName = "Faction", menuName = "DungeonColony/FactionData")]
public class FactionData : ScriptableObject
{
    [Serializable]
    public struct Relation
    {
        public FactionData  Other;
        public FactionStance Stance;
    }

    [Header("표시")]
    [SerializeField] string factionName = "세력";

    [Header("역할")]
    [Tooltip("플레이어가 조작하는 세력인가 (선택/명령/우선순위 UI 대상). 보통 한 세력만 켠다.")]
    [SerializeField] bool playerControlled;

    [Tooltip("붙잡은 사람을 **돈을 받고 놓아주는가.** 끄면 몸값 협상 자체가 없다 — " +
             "쳐들어가서 되찾는 수밖에 없다.\n" +
             "이건 무리가 아니라 **세력의 성질**이다: 산적은 돈이 목적이라 흥정이 되지만, " +
             "고블린 같은 것들과는 애초에 말이 통하지 않는다. 무리마다 따로 적으면 " +
             "같은 세력인데 어떤 무리는 흥정이 되고 어떤 무리는 안 되는 꼴이 된다.")]
    [SerializeField] bool acceptsRansom = true;

    [Header("관계")]
    [Tooltip("아래 목록에 없는 세력을 대하는 기본 태도.")]
    [SerializeField] FactionStance defaultStance = FactionStance.Neutral;
    [Tooltip("상대별 예외. 같은 상대가 여러 번 있으면 첫 항목이 이긴다.")]
    [SerializeField] List<Relation> relations = new();

    public string FactionName     => string.IsNullOrEmpty(factionName) ? name : factionName;
    public bool   PlayerControlled => playerControlled;
    public bool   AcceptsRansom    => acceptsRansom;
    public FactionStance DefaultStance => defaultStance;
    public IReadOnlyList<Relation> Relations => relations;

    // 이 세력이 other를 어떻게 대하는가 (방향별 — other가 나를 어떻게 보는지와 다를 수 있다).
    public FactionStance StanceTo(FactionData other)
    {
        if (other == null) return FactionStance.Neutral;

        foreach (var r in relations)
            if (r.Other == other) return r.Stance;

        // 같은 세력끼리는 목록에 없으면 아군 (자기 세력을 매번 적어야 하는 번거로움 방지)
        if (other == this) return FactionStance.Ally;

        return defaultStance;
    }

    public bool IsHostileTo(FactionData other) => StanceTo(other) == FactionStance.Hostile;
}

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

    [Tooltip("**돈을 받고 며칠 동안 싸워 주는가.** 켜면 그 세력 사람에게 말을 걸어 계약할 수 있다.\n" +
             "이것도 무리가 아니라 **세력의 성질**이다 — 용병단이니까 값을 받고 오는 것이지, " +
             "이 무리가 마침 돈이 궁해서가 아니다. `AcceptsRansom`과 같은 자리.")]
    [SerializeField] bool forHire;

    // ── 죽은 자를 일으킨다 ───────────────────────────────────────────────────
    //
    // **치우지 않은 시체가 습격의 재료가 된다.** 이 세력이 쳐들어오면 맵에 널브러진 시체가
    // 그 자리에서 일어난다 — 바깥에서 오는 것이 아니라 **거점 한복판에서** 시작된다.
    // 그래서 도축·매장·화장이 잡일이 아니라 **방어**가 된다.
    //
    // **세력이 드는 이유**: 습격이 오는 길은 둘인데(이벤트 추첨 · 지도에서 걸어온 무리)
    // 이벤트 에셋에 적으면 지도에서 온 교단은 시체를 안 일으킨다 — 플레이어에게 그 둘은
    // 똑같이 '교단이 몰려온 것'이다. 실제로 쳐들어온 폰들의 소속을 물으면 두 길이 저절로
    // 같아진다(`Necromancy`).
    [Header("죽은 자")]
    [Tooltip("이 세력의 습격이 **맵에 남은 시체를 일으키는가.** 켜면 널브러진 시체가 그 자리에서 " +
             "일어나 함께 싸운다 — 살점이 남았으면 그 몸이 그대로(이름·스탯·장비까지), " +
             "뼈만 남았으면 아래 프리팹이 선다.")]
    [SerializeField] bool raisesDead;

    [Tooltip("**무덤도 파는가.** 끄면 묻어 둔 시체는 안 일어난다 — 그것이 무덤에 값을 준다. " +
             "켜는 것은 교단처럼 굳이 땅을 파헤칠 이유가 있는 세력뿐이다. " +
             "화장한 몸은 어느 쪽으로도 안 돌아온다(그것이 무덤과 화장을 가르는 축이다).")]
    [SerializeField] bool exhumesGraves;

    [Tooltip("**뼈만 남은 시체에서 세우는 것**(해골). 비우면 뼈는 안 일어난다 — 살점이 남은 것만. " +
             "살점이 남은 몸은 프리팹이 필요 없다(그 사람이 그대로 일어난다).")]
    [SerializeField] GameObject risenBones;

    [Tooltip("이 세력이 **나를 어떻게 보고 시작하는가**(−100 ~ +100). 적대 세력이라도 정도가 다르다 — " +
             "산적은 돈이 목적이라 무를 여지가 있고, 망자와는 애초에 말이 통하지 않는다.\n" +
             "**0이면 아래 태도에서 뽑는다**(적대 −70 / 아군 +75 / 중립 0) — 안 적어 둔 세력이 " +
             "0에서 시작해 `적대 (0)`으로 뜨는 것을 막는 폴백이다.")]
    [Range(-100f, 100f)]
    [SerializeField] float startingFavor;

    // **이 세력 사람은 어떤 이력을 갖고 있나**(`BackstoryData.Group`).
    //
    // 비우면 공용 배경만 나온다. 여러 세력이 같은 묶음을 써도 된다 — 마을 사람과 변방민은
    // 둘 다 `civilian`이다(사는 곳이 다를 뿐 살아온 모양은 비슷하다).
    [Tooltip("이 세력 사람이 뽑는 성장 배경 묶음(예: bandit · cult). 비우면 공용만.")]
    [SerializeField] string backstoryGroup;

    [Header("관계")]
    [Tooltip("아래 목록에 없는 세력을 대하는 기본 태도.")]
    [SerializeField] FactionStance defaultStance = FactionStance.Neutral;
    [Tooltip("상대별 예외. 같은 상대가 여러 번 있으면 첫 항목이 이긴다.")]
    [SerializeField] List<Relation> relations = new();

    public string FactionName     => string.IsNullOrEmpty(factionName) ? name : factionName;
    public bool   PlayerControlled => playerControlled;
    public bool   AcceptsRansom    => acceptsRansom;
    public bool   ForHire          => forHire;
    public float  StartingFavor    => startingFavor;
    public string BackstoryGroup   => backstoryGroup;
    public bool   RaisesDead      => raisesDead;
    public bool   ExhumesGraves   => exhumesGraves;
    public GameObject RisenBones  => risenBones;

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

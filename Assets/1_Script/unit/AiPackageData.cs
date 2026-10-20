using System;
using System.Collections.Generic;
using UnityEngine;

// **AI 패키지 — "평소에 무엇을 하는가"를 데이터로 적는다**(2026-10-18, 사용자: *"AI 패키지를 데이터로 저작하는
// 걸 하자"*). 설계는 `docs/design_ai_packages.md`.
//
// 축이 하나 늘었다: 종족(`UnitData`) · 세력(`FactionData`) · 성향(`AiProfileData` — 위협에 **어떻게 반응하나**) ·
// 역할(`NpcData` — **누구인가**, 이름·파는 물건) · **패키지(이 SO — 평소에 무엇을 하나)**. 같은 경비 역할이라도
// 마을 경비 패키지와 야영지 보초 패키지를 따로 쓸 수 있다.
//
// - **목표는 위에서부터 묻는다** — 셀렉터 한 줄이다. 맨 끝에는 늘 대기(`IdleNode`)가 붙는다(적지 않는다).
// - **목표는 코드에 있는 행동을 고르는 것이다** — 행동 자체를 여기서 짜지 않는다(범용 트리 편집기가 아니다).
//   새 행동이 필요하면 `AiGoalKind`에 꼬리로 붙이고 `UnitBrain.BuildGoal`에 한 줄을 더한다.
// - **내 폰(방랑자·하수인·가축)은 패키지를 안 쓴다** — 소집·일감·Needs가 맞물린 시스템이라 순서를 데이터로
//   풀면 조용히 깨진다. 패키지는 NPC와 적의 것이다.
// - 목록은 `Resources/Data/AiPackages/`. 적의 기본은 `AiPkg_Enemy`(`AiPackages.Enemy`).
[CreateAssetMenu(fileName = "AiPkg_", menuName = "DungeonColony/AiPackageData")]
public class AiPackageData : ScriptableObject
{
    [Tooltip("저작할 때 보는 이름(`마을 경비`). 화면의 역할 이름은 `NpcData`가 든다.")]
    public string DisplayName;

    [Tooltip("장사를 하나 — 켜면 `Merchant`를 붙인다(파는 물건은 프리팹 > 역할 에셋 순).")]
    public bool Trades;

    [Tooltip("위에서부터 묻는다. 맨 끝의 대기는 저절로 붙는다.")]
    public List<AiGoal> Goals = new();

    // **맵을 떠나는 패키지인가** — 돌아다니는 무리의 식구로 넣으면 하나씩 빠져나간다(`WildSpawner` 경고).
    public bool LeavesMap => Goals.Exists(g => g.Kind is AiGoalKind.Depart or AiGoalKind.VisitCore);
}

// 목표의 종류. **정수로 직렬화된다 — 꼬리에만 붙인다**(`StatName`·`JobCategory`와 같은 규칙).
public enum AiGoalKind
{
    Combat,     // 떠나는 중이면 떠난다 → 도주 → (도주 성향이 아니면) 감지·능동·교전. `Stance`·`Range`
    Patrol,     // 배정된 순찰 구역(`bb.AssignedPatrolZone`)을 돈다 — 없으면 건너뛴다
    Wander,     // 집 둘레를 서성인다. `Radius`·`WanderByDefault` — 성향(`AiProfileData.Wander`)이 있으면 그쪽이 이긴다
    Depart,     // `Seconds`만큼 머문 뒤 맵 밖으로 걸어 나간다(떠돌이)
    VisitCore,  // 핵 근처까지 걸어와 `Seconds` 머물고 돌아가 사라진다(행상인) — 이것 하나가 하루 전부다
}

[Serializable]
public class AiGoal
{
    public AiGoalKind Kind;

    [Tooltip("Combat — 성향 에셋이 없을 때의 기본 성향")]
    public CombatStance Stance = CombatStance.Fight;
    [Tooltip("Combat — 도주 감지 범위(m). 0이면 종족의 DetectRange")]
    public float Range;

    [Tooltip("Wander — 배회 반경(m)")]
    public float Radius = 10f;
    [Tooltip("Wander — 성향 에셋이 없을 때 배회하나(끄면 제자리 — 가게 상인)")]
    public bool WanderByDefault = true;

    [Tooltip("Depart · VisitCore — 머무는 시간(초)")]
    public float Seconds = 120f;
}

// 패키지를 이름으로 찾는 곳 — 적처럼 역할 에셋이 없는 폰의 기본값.
public static class AiPackages
{
    const string Dir = "Data/AiPackages/";

    static AiPackageData _enemy;
    public static AiPackageData Enemy => _enemy != null ? _enemy : _enemy = Load("AiPkg_Enemy");

    // **Play 시작 때 한 번 대조한다** — 역할에 패키지가 비면 그 NPC는 조용히 대기만 한다(트리가 대기 하나다).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Check()
    {
        var roles    = Resources.LoadAll<NpcData>("Data/Npc");
        var packages = Resources.LoadAll<AiPackageData>(Dir.TrimEnd('/'));
        var missing  = System.Array.FindAll(roles, r => r.Package == null);
        string list  = string.Join(" · ", System.Array.ConvertAll(roles,
                          r => $"{r.name}→{(r.Package != null ? r.Package.name : "없음")}"));
        if (missing.Length > 0 || Enemy == null)
            Debug.LogWarning($"[AI 패키지] 빠진 것 있음 — {list} · 적 {(Enemy != null ? "있음" : "없음")}");
        else
            Debug.Log($"[AI 패키지] 역할 {roles.Length} · 패키지 {packages.Length} · 빠진 것 없음 — {list}");
    }

    static AiPackageData Load(string id)
    {
        var p = Resources.Load<AiPackageData>(Dir + id);
        if (p == null) Debug.LogError($"[AI 패키지] Resources/{Dir}{id}가 없다 — 그 폰은 대기만 한다.");
        return p;
    }
}

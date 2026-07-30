using UnityEngine;

// 폰이 "지금 뭘 하고 있는지"를 한 단어로. 로스터·툴팁 등 표시용 공용 변환.
//
// BT는 상태를 밖으로 내놓지 않으므로 블랙보드에서 역추론한다.
// 우선순위는 BT 분기 순서와 맞춘다 — 위에 있는 분기가 실제로 실행 중일 가능성이 높다.
public static class PawnStatusText
{
    public static string Describe(UnitBrain brain)
    {
        if (brain == null) return "-";

        var bb = brain.Blackboard;
        if (bb == null) return "-";

        if (bb.IsKnockedOut) return "기절";
        if (bb.IsDrafted)    return "소집";
        if (bb.Target != null) return "전투";

        // 침대에 누워 있는 중 (RestNode) — 이동 중이면 아직 가는 길이다
        var mover = brain.GetComponent<PawnMover>();
        if (mover != null && mover.IsSleeping) return "휴식";

        if (bb.HoldPosition) return "집결 대기";
        if (bb.ManualMove) return "이동";

        if (bb.AssignedNode != null)
        {
            var data = bb.AssignedNode.Data;
            bool cutting = data != null && data.ResourceType == ResourceType.Wood;
            return cutting ? "벌목" : "채굴";
        }

        if (bb.CurrentJob != null) return JobCategoryInfo.DisplayName(bb.CurrentJob.Category);
        if (bb.AssignedPatrolZone != null) return "순찰";
        if (Bed.FindAssigned(brain) != null) return "휴식";   // 배정은 됐지만 아직 가는 중

        return "대기";
    }

    // 상태별 색 — 한눈에 훑을 때 색이 정보가 되게.
    public static Color ColorOf(string status) => status switch
    {
        "전투" => new Color(0.90f, 0.55f, 0.45f),
        "기절" => new Color(0.85f, 0.35f, 0.35f),
        "휴식" => new Color(0.60f, 0.75f, 0.90f),
        "대기" => new Color(0.55f, 0.55f, 0.58f),
        "소집" => new Color(0.90f, 0.80f, 0.45f),
        "이동" => new Color(0.75f, 0.80f, 0.60f),
        _      => new Color(0.72f, 0.84f, 0.72f),   // 작업 중
    };
}

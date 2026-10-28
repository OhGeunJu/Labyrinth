using UnityEngine;

// **무엇을 가를 것인가 — 도축대가 드는 규칙 하나.**
//
// ── 왜 규칙이 필요한가 ────────────────────────────────────────────────────────
//
// 도축이 큐를 안 타던 이유는 하나였다: *"자동이면 시키지도 않았는데 동료 시신을 손질한다."*
// 그 걱정을 없애는 것은 자동화를 막는 것이 아니라 **사람이 미리 정해 두는 것**이다 —
// 무엇을 가를지 적어 두면 폰이 알아서 집어가도 동의 없이 벌어지는 일이 아니다.
//
// ── 왜 한 도축대에 규칙이 하나인가 ───────────────────────────────────────────
//
// 림월드에서 주문이 여럿인 것은 **한 작업대가 여러 레시피를 맡기 때문**이다. 여기 도축대는
// 하는 일이 하나뿐이라, 규칙이 여럿이면 갈리는 것은 조건뿐이고 그 둘은 곧바로 서로 겨룬다
// (같은 시체가 두 규칙에 걸리면 어느 쪽 횟수가 줄어드나). **규칙이 더 필요하면 도축대를 더
// 짓는다** — 우리·침대가 자리마다 제 몫을 갖는 것과 같은 모양이고, 목록 UI도 필요 없다.
[System.Serializable]
public class ButcherBill
{
    // **누구를 가르나.** 세력으로 가른다 — 종족에 "짐승인가" 같은 칸을 새로 파면 저작할 것이
    // 늘고, 정작 사람이 신경 쓰는 것은 *"내 편은 건드리지 마라"*다.
    // **세 칸은 관계를 한 칸씩 넓혀 간다** — 적대 → 거기에 중립까지 → 거기에 내 편까지.
    // 전에 가운데를 *"내 편만 빼고"*로 뒀더니 **동맹이 어느 쪽인지 알 수 없었다**(글자만
    // 모호한 게 아니라 실제로 용병·마을 사람 시신까지 가르고 있었다). 값을 치르고 부른
    // 용병이 죽었는데 폰이 알아서 손질하고 있으면 그건 규칙이 아니라 사고다.
    public enum Whom
    {
        Hostile,    // 적대 세력만 (기본) — 짐승·산적·고블린
        NotAllied,  // + 중립까지 (아군·내 세력은 뺀다)
        Everyone,   // + 내 폰까지
    }

    // **얼마나 삭은 것까지 나르나.** 뼈만 남은 것을 도축대까지 업어 오는 것은 뼈 몇 개를
    // 위해 폰을 왕복시키는 일이라, "그건 무덤으로 보내겠다"가 성립해야 한다.
    public enum Ripeness
    {
        FreshOnly,  // 신선한 것만 — 고기가 나올 때만
        NotBones,   // 뼈만 남은 것 빼고
        Any,        // 전부 (기본)
    }

    public enum Repeat
    {
        Forever,   // 무한 (기본)
        Count,     // 남은 횟수만큼
    }

    public bool     Suspended;
    public Whom     Target   = Whom.Hostile;
    public Ripeness Stage    = Ripeness.Any;
    public Repeat   Mode     = Repeat.Forever;
    public int      Remaining = 5;

    // 아직 할 일이 남았나.
    public bool Wanted => !Suspended && (Mode == Repeat.Forever || Remaining > 0);

    // 한 구를 마쳤다.
    public void Done()
    {
        if (Mode == Repeat.Count) Remaining = Mathf.Max(0, Remaining - 1);
    }

    // 이 시체가 규칙에 걸리나.
    //
    // **세력 판정은 `Corpse.Faction`이 든다** — `BecomeCorpse`가 `Faction` 컴포넌트를
    // 지우기 때문이다(자동 탐지를 끊으려고). 그래서 **"모르는 것은 플레이어가 아니다"**:
    // `faction == null`을 내 편으로 치면 세력을 잃은 시체가 `Hostile` 규칙에서 조용히 빠진다.
    public bool Matches(Corpse corpse)
    {
        if (corpse == null) return false;

        // **이미 어딘가에 놓인 몸은 안 건드린다.** 시체가 남의 자식이라는 것은 곧
        // 묻혔거나(`Grave`) 타는 중이거나(`Pyre`) 업혀 있다는 뜻이다 — 한 줄로 셋을 다 막는다.
        // 안 막으면 도축대가 **화장터에서 타던 시체를 빼 온다**(화장터는 그것을 모른 채 60초
        // 뒤에 어디에 있든 태워 없앤다).
        if (corpse.Buried || corpse.transform.parent != null) return false;

        var stage = corpse.Stage;
        if (Stage == Ripeness.FreshOnly && stage != CorpseStage.Fresh) return false;
        if (Stage == Ripeness.NotBones  && stage == CorpseStage.Bones) return false;

        var data = corpse.Faction;
        bool mine = data != null && data.PlayerControlled;

        return Target switch
        {
            Whom.Everyone  => true,
            // **아군은 뺀다.** 세력을 모르면(`data == null`) 내 편이 아닌 것으로 본다 —
            // "모르는 것은 플레이어가 아니다"(`Corpse.Faction`이 비는 경우의 기존 규약).
            Whom.NotAllied => !mine && Stance(data) != FactionStance.Ally,
            // **판정은 실제 전투와 같은 길**(`Stance` → `FactionRelations.StanceBetween`) —
            // 따로 재면 "표에는 적대인데 안 가른다"가 생긴다.
            _              => !mine && data != null
                           && Stance(data) == FactionStance.Hostile,
        };
    }

    static FactionStance Stance(FactionData other)
    {
        var me = Faction.Player;
        if (me == null || me.Data == null || other == null) return FactionStance.Neutral;
        return FactionRelations.StanceBetween(me.Data, other);
    }

    // **글자가 무엇을 포함하는지 말해야 한다** — "내 편만 빼고"로는 동맹이 어느 쪽인지
    // 알 수가 없었다. 넓어지는 순서를 그대로 읽히게 적는다.
    public string TargetName => Target switch
    {
        Whom.Everyone  => "적대 + 중립 + 내 폰",
        Whom.NotAllied => "적대 + 중립 (아군 제외)",
        _              => "적대 세력만",
    };

    public string StageName => Stage switch
    {
        Ripeness.FreshOnly => "신선한 것만",
        Ripeness.NotBones  => "뼈만 남은 것 빼고",
        _                  => "삭은 정도 무관",
    };

    public string RepeatName => Mode == Repeat.Forever ? "무한" : $"{Remaining}구 남음";
}

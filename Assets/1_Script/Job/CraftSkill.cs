// 제작 레시피에서 사용하는 스킬 종류
public enum CraftSkill
{
    Labor,          // 일반 노동
    WeaponCrafting, // 무기 제작
    ArmorCrafting,  // 방어구 제작
    Cooking,        // 요리
    Alchemy,        // 연금술
    Research,       // 연구
    Smelting,       // 제련 (용광로)
    Stonecutting,   // 석재 가공 (석재 가공대)
    Woodworking,    // 목공 (제재소)
    Gemcutting,     // 보석 가공 (보석 연마대)
}

public static class CraftSkillExt
{
    public static string DisplayName(this CraftSkill skill) => skill switch
    {
        CraftSkill.WeaponCrafting => "무기 제작대",
        CraftSkill.ArmorCrafting  => "방어구 제작대",
        CraftSkill.Cooking        => "요리대",
        CraftSkill.Alchemy        => "연금술대",
        CraftSkill.Research       => "연구대",
        CraftSkill.Smelting       => "용광로",
        CraftSkill.Stonecutting   => "석재 가공대",
        CraftSkill.Woodworking    => "제재소",
        CraftSkill.Gemcutting     => "보석 연마대",
        // 건물 표시명(`CraftingTable_Labor.asset`)과 같은 말을 쓴다 — 예전엔 기본값 "제작대"로
        // 떨어져서, 지어 놓은 건물과 그 패널이 서로 다른 이름을 달고 있었다.
        CraftSkill.Labor          => "작업대",
        _                         => "작업대",
    };
}

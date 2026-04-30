// 제작 레시피에서 사용하는 스킬 종류
public enum CraftSkill
{
    Labor,          // 일반 노동
    WeaponCrafting, // 무기 제작
    ArmorCrafting,  // 방어구 제작
    Cooking,        // 요리
    Alchemy,        // 연금술
    Research,       // 연구
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
        _                         => "제작대",
    };
}

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

    // **가죽·천 갑옷은 금속 갑옷과 다른 손이다**(2026-08-25). 하나였을 때는 방어구 제작대
    // 하나가 가죽 모자부터 강철 투구까지 다 만들었고, 그러면 **그 작업대를 무엇으로 잠글지가
    // 성립하지 않는다** — 재봉으로 열면 강철 투구가 딸려 오고, 주조로 열면 초반에 가죽옷을
    // 못 만든다. **값은 뒤에 붙인다**(직렬화가 정수다).
    Tailoring,      // 재봉 — 가죽·천 갑옷 (재봉대)
}

public static class CraftSkillExt
{
    public static string DisplayName(this CraftSkill skill) => skill switch
    {
        CraftSkill.WeaponCrafting => "무기 제작대",
        CraftSkill.ArmorCrafting  => "갑주 제작대",
        CraftSkill.Cooking        => "요리대",
        CraftSkill.Alchemy        => "연금술대",
        CraftSkill.Research       => "연구대",
        CraftSkill.Smelting       => "용광로",
        CraftSkill.Stonecutting   => "석재 가공대",
        CraftSkill.Woodworking    => "제재소",
        CraftSkill.Gemcutting     => "보석 연마대",
        CraftSkill.Tailoring      => "재봉대",
        // 건물 표시명(`CraftingTable_Labor.asset`)과 같은 말을 쓴다 — 예전엔 기본값 "제작대"로
        // 떨어져서, 지어 놓은 건물과 그 패널이 서로 다른 이름을 달고 있었다.
        CraftSkill.Labor          => "작업대",
        _                         => "작업대",
    };
}

public enum ArmorSlot
{
    Head,
    Chest,
    Hands,
    Legs,

    // **허리 — 램프를 단다**(2026-10-15, 사용자: *"허리 슬롯이 새로 생기는 건 좋은 거 같아"*). 막는 것이 아니라
    // 손을 비운 채 빛을 드는 자리다. **값은 꼬리에** — 정수로 직렬화된다(갑옷 에셋·JSON은 이름이라 괜찮지만
    // 장비칸 `EquipSlotUI.SlotKind`도 같은 순서를 쓴다).
    Belt,

    // **발 — 신발**(2026-10-18, 사용자: *"신발은 필요해 보여"* · 하는 일은 *"다리 방어 + 이동 속도"*). 몸에 발
    // 부위는 없다 — 다리를 맞으면 다리 갑옷과 **같이** 막는다(`BodyPartSystem`) · 걸음은 `ArmorData.MoveSpeedMult`.
    Feet,
}

public static class ArmorSlotExt
{
    public static string DisplayName(this ArmorSlot s) => s switch
    {
        ArmorSlot.Head  => "머리",
        ArmorSlot.Chest => "몸통",
        ArmorSlot.Hands => "손",
        ArmorSlot.Legs  => "다리",
        ArmorSlot.Belt  => "허리",
        ArmorSlot.Feet  => "발",
        _               => s.ToString(),
    };
}

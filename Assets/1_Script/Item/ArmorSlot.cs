public enum ArmorSlot
{
    Head,
    Chest,
    Hands,
    Legs,
}

public static class ArmorSlotExt
{
    public static string DisplayName(this ArmorSlot s) => s switch
    {
        ArmorSlot.Head  => "머리",
        ArmorSlot.Chest => "몸통",
        ArmorSlot.Hands => "손",
        ArmorSlot.Legs  => "다리",
        _               => s.ToString(),
    };
}

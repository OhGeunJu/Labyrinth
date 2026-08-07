using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ArmorData")]
public class ArmorData : ItemData
{
    // 무기와 같은 이유로 크게 깎인다(`WeaponData.ResaleFactor` 참고).
    // 갑옷은 몸에 맞춰 고쳐 입어야 해서 되팔 때 더 안 쳐준다.
    public override float ResaleFactor => 0.5f;

    // 어느 칸에 입는가가 곧 종류다 — "몸통 방어구".
    public override string TypeName => Slot.DisplayName() + " 방어구";

    [Header("방어구")]
    public ArmorSlot Slot;
    public float DamageReduction = 5f;  // 피해 감소 (고정값)
}

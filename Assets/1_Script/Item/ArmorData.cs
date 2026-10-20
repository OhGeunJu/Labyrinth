using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ArmorData")]
public class ArmorData : ItemData
{
    // 무기와 같은 이유로 크게 깎인다(`WeaponData.ResaleFactor` 참고).
    // 갑옷은 몸에 맞춰 고쳐 입어야 해서 되팔 때 더 안 쳐준다.
    public override float ResaleFactor => 0.5f;

    // 어느 칸에 입는가가 곧 종류다 — "몸통 방어구".
    public override string TypeName => Slot.DisplayName() + " 방어구";

    // 무기와 같다 — 솜씨가 물건에 남는다.
    public override bool HasQuality => true;

    [Header("방어구")]
    public ArmorSlot Slot;
    public float DamageReduction = 5f;  // 피해 감소 (고정값) — **물리에만 걸린다**

    // **원소는 평탄 감소가 아니라 비율로 막는다**(2026-08-23). 갑옷의 `DamageReduction`은
    // 물리를 깎는 값이고(칼이 두꺼운 판을 못 뚫는다), 불·전기는 두께가 아니라 **재질**의
    // 문제다 — 쇠사슬은 칼을 막지만 번개에는 오히려 나쁘다. 비우면 저항 0.
    [Tooltip("이 갑옷이 막아 주는 원소. 비우면 원소에는 아무 힘이 없다.")]
    public List<DamageResistance> Resistances = new();

    // **걸음 배율**(2026-10-18 — 신발이 쓴다, 다른 부위도 쓸 수 있다). 1이면 그대로 · 가벼운 신은 1보다 크게,
    // 무거운 철갑화는 작게. 찬 것 전부를 곱한다(`Equipment.MoveSpeedMult` → `PawnMover`).
    [Tooltip("이동 속도 배율. 1 = 그대로, 1.1 = 10% 빠르게")]
    [Range(0.5f, 1.5f)] public float MoveSpeedMult = 1f;

    // **능동은 폰이 아니라 장비가 준다**(2026-08-23) — 중갑이 도발을 준다.
    // 폰에 스킬 슬롯을 두면 *"어느 폰에게 무엇을 배울까"*가 폰 수만큼 늘어나는 잡일이 되는데,
    // 장비가 주면 **갈아입히는 것이 곧 역할을 바꾸는 것**이라 이미 있는 조작으로 끝난다.
    [Tooltip("이 장비를 차면 쓸 수 있는 능동 능력. 비우면 없다")]
    public AbilityData Ability;

}

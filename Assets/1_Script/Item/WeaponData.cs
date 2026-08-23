using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/WeaponData")]
public class WeaponData : ItemData
{
    // 쓰던 무기는 **중고다.** 사는 값과 파는 값이 크게 벌어져야 "장비는 사서 쓰는 것이지
    // 되팔아 돈 버는 수단이 아니다"가 값으로 드러난다. 상인 매입률(0.45)과 곱해 실질 0.25 —
    // 대략 사는 값의 4분의 1이다.
    public override float ResaleFactor => 0.55f;

    public override string TypeName => Type.DisplayName();

    // 잘 벼린 검과 대충 만든 검은 다른 물건이다 — 등급이 성립하는 축.
    public override bool HasQuality => true;

    [Header("무기 분류")]
    public WeaponType Type;
    public bool IsLargeWeapon;  // 양손무기/폴암 → 근력 공격속도 보정
    public bool IsBladed;       // 검/도끼 → 손재주 절단 피해 보정
    public bool IsRanged;

    [Header("전투 스탯")]
    public float BaseDamage         = 10f;
    public float BaseAttackInterval = 2f;
    public float AttackRange        = 1.5f;  // 원거리는 크게 설정

    [Header("방어 보너스 (방패 전용)")]
    public float BlockBonus        = 0f;  // 근접 방어 확률 +
    public float RangedBlockBonus  = 0f;  // 원거리 방어 확률 +

    [Header("피해 유형")]
    // **축이 하나다**(2026-08-23) — 물리 셋 + 원소 일곱. 전에는 `DamageCategory` ×
    // `DamageElement`로 갈라 두고 마법 무기는 `WeaponType`에서 되짚었는데, 그 추론 사다리
    // 셋(`IsMagicWeapon`·`CategoryFromType`·`ElementFromType`)이 통째로 없어졌다.
    public DamageType DamageType = DamageType.Slash;

    // **능동은 폰이 아니라 장비가 준다**(2026-08-23) — 지팡이는 마법을, 방패는 도발을 준다.
    // 폰에 스킬 슬롯을 두면 *"어느 폰에게 무엇을 배울까"*가 폰 수만큼 늘어나는 잡일이 되는데,
    // 장비가 주면 **갈아입히는 것이 곧 역할을 바꾸는 것**이라 이미 있는 조작으로 끝난다.
    [Tooltip("이 장비를 차면 쓸 수 있는 능동 능력. 비우면 없다")]
    public AbilityData Ability;


    [Range(0f, 1f)]
    public float EffectChance = 0.2f;

    public bool IsMagicWeapon => Type == WeaponType.Staff;
}

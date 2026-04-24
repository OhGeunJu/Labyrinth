using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/WeaponData")]
public class WeaponData : ItemData
{
    [Header("무기 분류")]
    public WeaponType Type;
    public bool IsLargeWeapon;  // 양손무기/폴암 → 근력 공격속도 보정
    public bool IsBladed;       // 검/도끼 → 손재주 절단 피해 보정
    public bool IsRanged;

    [Header("전투 스탯")]
    public float BaseDamage         = 10f;
    public float BaseAttackInterval = 2f;
    public float AttackRange        = 1.5f;  // 원거리는 크게 설정
}

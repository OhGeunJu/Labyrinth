using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "DungeonColony/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
    public string RaceName;
    public float MaxHP      = 100f;
    public float MoveSpeed  = 4f;

    [Header("전투")]
    public float AttackDamage  = 10f;
    public float AttackRange   = 1.5f;
    public float DetectRange   = 10f;

    [Header("속성 범위")]
    public Vector2 StrengthRange  = new Vector2(5f, 20f);
    public Vector2 ToughnessRange = new Vector2(5f, 20f);
    public Vector2 DexterityRange = new Vector2(5f, 20f);
    public Vector2 CognitionRange = new Vector2(5f, 20f);

    [Header("전투 기술 범위")]
    public Vector2 MeleeAttackRange       = new Vector2(1f, 10f);
    public Vector2 MeleeDefenseRange      = new Vector2(1f, 10f);
    public Vector2 RangedDefenseRange     = new Vector2(1f, 10f);
    public Vector2 PrecisionShootingRange = new Vector2(1f, 10f);

    [Header("근접 무기 범위")]
    public Vector2 OneHandSwordRange = new Vector2(1f, 10f);
    public Vector2 OneHandAxeRange   = new Vector2(1f, 10f);
    public Vector2 OneHandMaceRange  = new Vector2(1f, 10f);
    public Vector2 TwoHandSwordRange = new Vector2(1f, 10f);
    public Vector2 TwoHandAxeRange   = new Vector2(1f, 10f);
    public Vector2 TwoHandMaceRange  = new Vector2(1f, 10f);
    public Vector2 PolearmRange      = new Vector2(1f, 10f);

    [Header("원거리 무기 범위")]
    public Vector2 BowRange      = new Vector2(1f, 10f);
    public Vector2 CrossbowRange = new Vector2(1f, 10f);
    public Vector2 JavelinRange  = new Vector2(1f, 10f);

    [Header("마법 범위")]
    public Vector2 ElementalMagicRange = new Vector2(1f, 10f);
    public Vector2 DarkMagicRange      = new Vector2(1f, 10f);
    public Vector2 HolyMagicRange      = new Vector2(1f, 10f);

    [Header("마법 원소 범위")]
    public Vector2 ElementalFireRange      = new Vector2(1f, 10f);
    public Vector2 ElementalIceRange       = new Vector2(1f, 10f);
    public Vector2 ElementalLightningRange = new Vector2(1f, 10f);
    public Vector2 ElementalPoisonRange    = new Vector2(1f, 10f);
    public Vector2 DarkFireRange           = new Vector2(1f, 10f);
    public Vector2 DarkIceRange            = new Vector2(1f, 10f);
    public Vector2 DarkLightningRange      = new Vector2(1f, 10f);
    public Vector2 DarkPoisonRange         = new Vector2(1f, 10f);
    public Vector2 HolyFireRange           = new Vector2(1f, 10f);
    public Vector2 HolyIceRange            = new Vector2(1f, 10f);
    public Vector2 HolyLightningRange      = new Vector2(1f, 10f);
    public Vector2 HolyPoisonRange         = new Vector2(1f, 10f);

    [Header("신체 기술 범위")]
    public Vector2 MartialArtsRange = new Vector2(1f, 10f);
    public Vector2 EvasionRange     = new Vector2(1f, 10f);
    public Vector2 AthleticsRange   = new Vector2(5f, 20f);
    public Vector2 SwimmingRange    = new Vector2(1f, 10f);

    [Header("노동 범위")]
    public Vector2 LaborRange          = new Vector2(1f, 10f);
    public Vector2 MiningRange         = new Vector2(1f, 10f);
    public Vector2 GrowingRange        = new Vector2(1f, 10f);
    public Vector2 CuttingRange        = new Vector2(1f, 10f);
    public Vector2 TamingRange         = new Vector2(1f, 10f);
    public Vector2 CookingRange        = new Vector2(1f, 10f);
    public Vector2 MedicineRange       = new Vector2(1f, 10f);
    public Vector2 ResearchRange       = new Vector2(1f, 10f);
    public Vector2 WeaponCraftingRange = new Vector2(1f, 10f);
    public Vector2 ArmorCraftingRange  = new Vector2(1f, 10f);
    public Vector2 AlchemyRange        = new Vector2(1f, 10f);
}

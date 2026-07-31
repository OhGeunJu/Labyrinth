using System;
using UnityEngine;

// StreamingAssets/Data/ 또는 Mods/ 폴더의 .json 파일 역직렬화용 스키마.
// JsonUtility 사용 — 모든 클래스에 [Serializable] 필수.
// 열거형은 문자열로 작성 (예: "Slash", "Physical", "WeaponCrafting").

[Serializable]
public class ItemSchema
{
    // 식별자. **모드는 이걸 쓸 것** — 비우면 itemName이 식별자가 되는데, 그건 번역 대상이라
    // 언젠가 끊긴다. 기존 콘텐츠 호환을 위해 비워도 동작은 한다.
    public string id;
    public string itemName;
    public string description;
    public string iconPath;      // "Icons/iron_sword" → Resources.Load<Sprite>
    public float  weight;
    public int    width  = 1;
    public int    height = 1;
}

[Serializable]
public class WeaponSchema : ItemSchema
{
    public string weaponType         = "OneHandSword";
    public bool   isLargeWeapon;
    public bool   isBladed;
    public bool   isRanged;
    public float  baseDamage         = 10f;
    public float  baseAttackInterval = 2f;
    public float  attackRange        = 1.5f;
    public float  blockBonus;
    public float  rangedBlockBonus;
    public string damageCategory     = "Physical";
    public string physicalType       = "Slash";
    public string damageElement      = "None";
    public float  effectChance       = 0.2f;
}

[Serializable]
public class ArmorSchema : ItemSchema
{
    public string armorSlot       = "Chest";
    public float  damageReduction = 5f;
}

[Serializable]
public class ResourceItemSchema : ItemSchema
{
    public string resourceType = "Stone";
    public int    maxStack     = 999;
}

[Serializable]
public class IngredientSchema
{
    // `itemId`를 쓸 것. `itemName`은 표시명 조회(레거시)라 번역하면 끊긴다.
    public string itemId;
    public string itemName;
    public int    amount;

    public string Key => !string.IsNullOrWhiteSpace(itemId) ? itemId : itemName;
}

[Serializable]
public class RecipeSchema
{
    public string             id;            // 비우면 recipeName이 식별자 (레거시)
    public string             recipeName;
    public IngredientSchema[] ingredients;
    public string             resultItemId;  // 비우면 resultItemName (레거시)
    public string             resultItemName;

    public string ResultKey =>
        !string.IsNullOrWhiteSpace(resultItemId) ? resultItemId : resultItemName;
    public int                resultAmount      = 1;
    public float              craftTime         = 5f;
    public string             requiredSkillType = "Labor";
    public float              requiredSkillLevel;
    public float              skillGrowthAmount = 0.5f;
    public string             requiredResearchId;
}

[Serializable]
public class FoodSchema : ItemSchema
{
    public float nutritionValue = 30f;
    public float eatTime        = 2f;
}

// JSON 파일의 "type" 필드로 아이템 종류 판별
[Serializable]
public class ItemTypeHint
{
    public string type; // "weapon" | "armor" | "resource" | "food" | (없으면 기본 아이템)
}

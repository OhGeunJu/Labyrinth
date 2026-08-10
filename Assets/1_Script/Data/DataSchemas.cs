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

    // 그림. **자기 폴더 기준 상대 경로**를 쓸 것 — `"icons/iron_sword.png"`.
    // 확장자는 생략해도 되고(PNG/JPG), 없으면 에셋 번들 → Resources 순으로도 찾는다(`ModAssets`).
    public string iconPath;

    // 바닥에 떨어질 때 쓰는 3D 모델. **`.obj`를 자기 폴더에 넣고 가리키면 된다** —
    // 같은 폴더의 `.mtl`과 텍스처까지 같이 읽는다(`ObjModel`). `.fbx`는 런타임에 해석이 안 된다.
    // 에셋 번들 안의 프리팹 이름도 받는다(뼈대·애니메이션이 필요할 때).
    // 비우면 공용 꾸러미 모델로 폴백.
    public string modelPath;

    public float  weight;
    public int    width  = 1;
    public int    height = 1;

    // 기준 가격(G). 상인 진열가·판매가·툴팁의 "가치"가 전부 여기서 온다.
    // 안 적으면 0이고, 그러면 상인이 기본값 10G로 팔아 값이 뒤죽박죽이 된다.
    public int    baseValue;

    // 한 칸에 몇 개까지 쌓이는가. **자원만의 속성이 아니다** — 밀·빵·씨앗도 쌓여야 한다.
    // 안 적으면 1(무기·방어구처럼 한 칸에 하나).
    public int    maxStack = 1;
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
    // 묶음(Stone/Ore/Gem/Ingot/Wood/Currency). **자원의 종류가 아니라 분류다** —
    // 종류는 이 아이템 자신(`id`)이다. 창고 필터와 UI가 이 값으로 자원을 갈라 놓는다.
    public string resourceGroup = "Stone";

    // 2026-08-02 이전 스키마. 자원 하나하나를 가리키던 이름("IronOre")이 들어온다.
    // 그 시절 이름을 묶음으로 옮겨 주므로 옛 JSON도 그대로 실린다(`ResourceGroupOf`).
    public string resourceType;
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

[Serializable]
public class MedicineSchema : ItemSchema
{
    public float potency    = 1f;
    public float treatSpeed = 1f;
}

// JSON 파일의 "type" 필드로 아이템 종류 판별
[Serializable]
public class ItemTypeHint
{
    public string type; // "weapon" | "armor" | "resource" | "food" | "medicine" | (없으면 기본 아이템)
}

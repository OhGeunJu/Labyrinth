# Data & Modding System

## DataManager (`Assets/1 Script/Data/`)

`DataManager` (static class, `RuntimeInitializeOnLoadMethod` 자동 초기화) — 게임 데이터 중앙 레지스트리.

**로딩 순서** (나중에 오는 데이터가 앞선 것을 덮어씀):
```
1. Assets/Resources/Data/Items|Recipes|Research   ← ScriptableObject 에셋
2. StreamingAssets/Data/items|recipes             ← 기본 JSON 콘텐츠
3. StreamingAssets/Mods/[ModName]/items|recipes   ← 모드 JSON
```

**API:**
- `GetItem(name)` / `GetRecipe(name)` / `GetResearch(name)` — 이름으로 단건 조회
- `GetRecipesBySkill(CraftSkill)` — 제작대 타입별 레시피 목록
- `AllItems` / `AllRecipes` — 전체 조회

## JSON 파일 형식

### 아이템 (`StreamingAssets/Data/items/`)

`type` 필드로 종류 구분:

```json
{
  "type": "weapon",
  "itemName": "철검",
  "iconPath": "Icons/iron_sword",
  "weight": 2.0,
  "weaponType": "OneHandSword",
  "isBladed": true,
  "baseDamage": 15,
  "baseAttackInterval": 1.8,
  "attackRange": 1.5,
  "damageCategory": "Physical",
  "physicalType": "Slash",
  "damageElement": "None",
  "effectChance": 0.15
}
```

`type` 값: `"weapon"` | `"armor"` | `"resource"` | `"food"` | 없으면 기본 ItemData

### 레시피 (`StreamingAssets/Data/recipes/`)

```json
{
  "recipeName": "철검 제작",
  "ingredients": [
    { "itemName": "철 주괴", "amount": 2 },
    { "itemName": "나무 막대", "amount": 1 }
  ],
  "resultItemName": "철검",
  "resultAmount": 1,
  "craftTime": 10.0,
  "requiredSkillType": "WeaponCrafting",
  "requiredSkillLevel": 0,
  "skillGrowthAmount": 0.5
}
```

### 열거형 (문자열, 대소문자 무시)

- `weaponType`: `OneHandSword`, `TwoHandAxe`, `Bow`, `ElementalFire`, `DarkIce`, `HolyFire`, ...
- `damageCategory`: `Physical`, `Elemental`, `Dark`, `Holy`
- `physicalType`: `Slash`, `Pierce`, `Blunt`
- `damageElement`: `None`, `Fire`, `Ice`, `Lightning`, `Poison`
- `armorSlot`: `Head`, `Chest`, `Hands`, `Legs`
- `resourceType`: `Stone`, `CutStone`, `IronOre`, `CopperOre`, `TinOre`, `Coal`, `SilverOre`, `GoldOre`, `MagicStone`, `Ruby`, `Sapphire`, `Topaz`, `Emerald`, `Etherstone`, `Voidstone`, `Bronze`, `Steel`, `SilverIngot`, `GoldIngot`, `Wood`, `Lumber`
- `requiredSkillType`: `Labor`, `WeaponCrafting`, `ArmorCrafting`, `Cooking`, `Alchemy`, `Research`, `Smelting`, `Stonecutting`, `Woodworking`, `Gemcutting`

**아이콘**: `iconPath`는 `Resources/` 기준 경로 (확장자 제외). 스프라이트를 `Assets/Resources/Icons/`에 위치.

**미구현 JSON 타입** (향후 추가 예정): UnitData/MinionData, SubCoreData (프리팹 참조 문제), ResearchData

## ScriptableObject Assets (`Assets/Resources/Data/`)

| Type | 생성 메뉴 | 주요 필드 |
|---|---|---|
| `UnitData` | `DungeonColony/UnitData` | RaceName, MaxHP, MoveSpeed, AttackDamage, AttackRange, DetectRange + stat ranges |
| `MinionData` | `DungeonColony/MinionData` | (UnitData) + SummonCostType, SummonCostAmount, AllowedJobs, CanProvideWater |
| `NpcData` | `DungeonColony/NpcData` | (UnitData) + NpcType, WanderRadius, FleeDetectRange, StayDuration |
| `WeaponData` | `DungeonColony/WeaponData` | WeaponType, BaseDamage, BaseAttackInterval, AttackRange, IsLargeWeapon, IsBladed, IsRanged, BlockBonus, RangedBlockBonus, DamageCategory, PhysicalType, DamageElement, EffectChance |
| `ArmorData` | `DungeonColony/ArmorData` | ArmorSlot, DamageReduction |
| `BuildingData` | `DungeonColony/BuildingData` | BuildingName, Category (BuildCategory enum), CostType, CostAmount, Prefab, UsesBlueprint |
| `DungeonCoreData` | `DungeonColony/DungeonCoreData` | CoreType, BaseRadius, MaxGrade, BaseMinionCap, HungerRateModifier, per-frame modifiers |
| `ResourceNodeData` | `DungeonColony/ResourceNodeData` | ResourceType, GaugeRequired, DropMin, DropMax, DropItem |
| `ItemData` | `DungeonColony/ItemData` | ItemName, Description, Icon, Width, Height, Weight |
| `ResourceItemData` | `DungeonColony/ResourceItemData` | (ItemData) + ResourceType, Stack (MaxStack 오버라이드) |
| `SeedData` | `DungeonColony/SeedData` | (ItemData) + HarvestItem, HarvestAmount, GrowthTime, MoistureDecay, WaterFillAmount |
| `FoodData` | `DungeonColony/FoodData` | (ItemData) + NutritionValue, EatTime. MaxStack=20 |
| `RecipeData` | `DungeonColony/RecipeData` | RecipeName, Ingredients, Result, ResultAmount, CraftTime, Category, SkillGrowthAmount, RequiredResearch |
| `ResearchData` | `DungeonColony/ResearchData` | ResearchName, Prerequisites, Ingredients, ResearchTime, UnlockedRecipes |
| `GameEventData` | `DungeonColony/GameEventData` | EventName, Description, Icon, Weight, MinGameTime + 타입별 필드 |

**아이템 계층:**
- `ItemData` (기본)
  - `ResourceItemData` — 자원 (스택 가능)
  - `WeaponData` — 무기
  - `ArmorData` — 방어구
  - `SeedData` — 씨앗 (MaxStack=99)
  - `FoodData` — 음식 (MaxStack=20)
- `UnitData`
  - `MinionData` — 하수인
  - `NpcData` — NPC

## 게임 데이터 Excel

`Tools/GameData.xlsx` — `Tools/make_gamedata.py` 실행으로 재생성.
시트: Items / Recipes / Buildings / Enums(참고). 드롭다운 유효성 검사, 카테고리별 색상 구분.

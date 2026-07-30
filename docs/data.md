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

---

# 데이터를 어디에 두나 — `5_Data` vs `Resources/Data`

| | `Assets/5_Data/` | `Assets/Resources/Data/` |
|---|---|---|
| 빌드 포함 | **참조된 것만** (씬·프리팹·다른 SO가 물고 있어야 함) | **전부 무조건** |
| 찾는 법 | 인스펙터 연결(직렬화 참조) | `Resources.Load` / `LoadAll` — **이름·경로로** |
| 쓰는 것 | `ResourceNodeData` · `BuildingData` · `UnitData` · `FactionData` · `AiProfileData` · `WorldMapData` … | `ItemData` · `RecipeData` · `ResearchData`(`DataManager`) / `BiomeContentData`(`BiomeContent`) |

**가르는 기준은 하나** — *"코드가 이름만 알고 실물을 몰라도 찾아와야 하는가?"*

- 모드 JSON이 `"itemName": "철검"`처럼 **이름으로 가리키는** 아이템·레시피·연구는 레지스트리가 필요하다 → `Resources`.
- 노드·건물·유닛처럼 **누군가 인스펙터에서 직접 꽂아 쓰는** 것은 참조만으로 충분하다 → `5_Data`.

주의: `Resources` 폴더는 **빌드에서 뺄 수 없고 시작 시 다 로드**된다. 참조로 충분한 것을 여기 두면 안 쓰는 에셋까지 빌드에 실린다. 그래서 기본값은 `5_Data`이고, **레지스트리가 필요할 때만** `Resources`로 보낸다.

---

# 테이블은 데이터를 어디서 얻나 — **레지스트리에서 끌어온다**

씬에 미리 놓아둔 인스턴스에만 목록을 채워 넣으면, **런타임에 지은 것은 목록이 텅 빈다.** 그래서 작업대류는 자기 인스턴스가 아니라 레지스트리를 본다.

| 컴포넌트 | 목록 출처 | 인스턴스 설정 |
|---|---|---|
| `CraftingTable` | `DataManager.GetRecipesBySkill(tableType)` | `tableType`(어느 스킬의 작업대인가)만 |
| `ResearchTable` | `DataManager.AllResearch` | `availableResearch`는 **비워 두는 게 기본** |

**`availableResearch`는 큐레이션용 오버라이드다** — 채우면 그게 이기고, 비우면 전체를 쓴다. "데이터가 없으면 기본 동작"이라는 이 프로젝트의 관례(`BiomeContentData` · `WorldTileVisualData`와 같은 규칙)를 따른다.

> 2026-07-31 이전에는 `ResearchTable`이 **인스턴스 목록만** 봤다. 씬에 놓인 연구대에만 데이터가 들어가고 나중에 건설하는 연구대는 빈다는 지적으로 고쳤다. 새 작업대/테이블을 만들 때도 같은 규칙을 따를 것 — **인스턴스에는 "무엇을 다루는 테이블인가"만 두고, 목록은 레지스트리에서 끌어온다.**

---

# 생성한 콘텐츠 데이터 (2026-07-31)

`DungeonColony → Create Missing Data` (`Assets/Editor/DataScaffoldCreator.cs`)로 한 번에 만든다.
**없는 것만 만들고 이미 있는 에셋은 손대지 않는다** — 여러 번 실행해도 안전하다.
숫자는 전부 **임시 초안**이다. 밸런스가 아니라 "시스템이 실제로 굴러가는 걸 눈으로 보려고" 채운 값이다.

## 무엇이 어디에 생기나

| 종류 | 경로 | 개수 | 비고 |
|---|---|---|---|
| 자원 아이템 `ResourceItemData` | `Resources/Data/Items/Res_*.asset` | ResourceType 22종 중 **없는 것만** | 기존 `Stone_item`·`Ore_item`·`Wood_item`·`Crystal_item`·`Gold`은 재사용 |
| 작물·음식 | `Resources/Data/Items/Crop_*·Food_*` | 7 | 밀 / 생고기 / 생선 / 빵 / 구운 고기 / 산딸기 / 버섯 |
| 씨앗 `SeedData` | `Resources/Data/Items/Seed_*` | 3 | 밀 / 산딸기 / 버섯 |
| 무기 `WeaponData` | `Resources/Data/Items/Wpn_*` | 11 | 검·도끼·철퇴·창·활·석궁·지팡이·방패 2종 |
| 방어구 `ArmorData` | `Resources/Data/Items/Arm_*` | 8 | 가죽 4부위 / 철 4부위 |
| 자원 노드 `ResourceNodeData` | `5_Data/Resource/Node_*` | 10 | 철·구리·주석·석탄·은·금·마법석·에테르석·공허석·루비 |
| 레시피 `RecipeData` | `Resources/Data/Recipes/Rec_*` | 10 | 제련 4 / 가공 2 / 요리 2 / 장비 2 |
| 연구 `ResearchData` | `Resources/Data/Research/Res_*` | 6 | 제련술·제강술·석공술·목공술·조리법·무기 제작 |
| 바이옴 매장·노드 풀 | `Resources/Data/Biomes/Biome_*` | 7개 에셋을 **채움** | `Deposits` / `ResourceNodes`가 비어 있을 때만 |

## 관계

```
[씨앗]  Seed_Wheat ──심는다──> Crop_Wheat ──Rec_Bread──> Food_Bread
        Seed_Berry ─────────> Food_Berry (바로 먹음)
        Seed_Mushroom ──────> Food_Mushroom (바로 먹음)
        Food_RawMeat ──Rec_CookedMeat──> Food_CookedMeat

[채굴]  Node_IronVein ──캔다──> Res_IronOre ─┐
        Node_CoalSeam ─────────> Res_Coal ──┴─Rec_Steel──> Res_Steel ──Rec_IronChest──> Arm_IronChest
        Node_CopperVein ───────> Res_CopperOre ─┐
        Node_TinVein ──────────> Res_TinOre ────┴─Rec_Bronze──> Res_Bronze
        Stone ─────────────────> Res_Stone ──Rec_CutStone──> Res_CutStone
        Wood ──────────────────> Res_Wood  ──Rec_Lumber────> Res_Lumber

[연구]  Res_Smelting(제련술) ──해금──> Rec_Bronze
          └선행─> Res_Steelmaking(제강술) ──해금──> Rec_Steel, Rec_IronChest
        Res_Stonecutting ──> Rec_CutStone
        Res_Woodworking  ──> Rec_Lumber
        Res_Cooking      ──> Rec_Bread, Rec_CookedMeat
        Res_WeaponSmithing ─> Rec_IronSword

[월드]  BiomeContentData
          ├ Deposits      → 월드맵 칸의 매장량 (WorldDeposits가 좌표 해시로 굴림) → 채집기지가 캔다
          └ ResourceNodes → 필드맵에 등장할 자원 노드 풀 + Discovery 이벤트 대상
```

**두 갈래를 구분할 것** — 같은 자원이라도 얻는 경로가 다르다.
- `Deposits` = **월드맵 칸에 묻힌 양**. 채집기지가 매일 캐서 쌓고, 원정대가 실어온다.
- `ResourceNodes` = **필드맵에 서는 채굴 대상**. 폰이 직접 캔다(`MineJob`/`Mining`).

## 바이옴별 성격

| 바이옴 | 매장 (확률·범위) | 자원 노드 풀 |
|---|---|---|
| 평원 | 돌 100%·40~90 / 목재 80%·30~70 / 구리 40%·15~35 | 돌, 구리 |
| 숲 | **목재 100%·80~160** / 돌 50% / 산딸기 60% | 나무, 돌 |
| 사막 | 돌 100%·60~120 / 주석 60% / **금 25%** | 돌, 주석, 금 |
| 설원 | 돌 80% / 철 60% / 은 30% | 철, 은 |
| 용암지대 | **석탄 100%·60~130** / 철 70% / 루비 30% / 마법석 35% | 석탄, 철, 루비, 마법석 |
| 동굴 | 돌 100%·80~160 / **철 80%·40~90** / 은 40% / 공허석 25% / 버섯 50% | 돌, 철, 은, 공허석 |
| 늪 | 목재 60% / 석탄 50% / **에테르석 20%** / 버섯 70% | 나무, 석탄, 에테르석 |

- 마법 촉매는 셋으로 갈렸다 — **마법석(원소)=용암 / 공허석(암흑)=동굴 / 에테르석(신성)=늪**. 세 계열을 다 쓰려면 세 바이옴을 다 밟아야 한다.
- 평원이 시작 지역이라 **막히지 않게** 흔한 것 위주로 뒀다.
- `ResourceSite`로 저작한 칸은 매장량 ×2.5(`WorldDeposits.ResourceSiteMultiplier`).

## 레거시 아이템 — 이름이 타입과 어긋나 있었다 (2026-07-31 교정)

`ResourceType` enum이 커지면서 **저장된 정수의 의미가 밀렸다.** 에셋 이름은 옛 뜻 그대로인데 타입은 새 뜻이 된 상태.

| 에셋 | 실제 `ResourceType` | 옛 `ItemName` | 고친 `ItemName` |
|---|---|---|---|
| `Items/Ore_item` | **CutStone** | "Ore" | **다듬은 돌** |
| `Items/Crystal_item` | **CopperOre** | "Crystal" | **구리 광석** |

**타입은 모든 참조에서 일관되게 쓰이고 있었다** — `Rec_CutStone`의 결과물, `Rec_Bronze`의 재료, `Node_CopperVein`의 드롭, 평원 매장 전부 타입 기준으로 맞물려 있다. 그래서 **이름만 고쳤다**(참조는 하나도 안 건드림).

> 같은 종류의 사고를 막으려면: `ResourceType`에 값을 **중간에 끼워 넣지 말고 끝에 추가**할 것. 기존 에셋은 정수로 저장돼 있어 순서가 바뀌면 조용히 다른 자원이 된다.

## 알려진 잔재

- `Assets/5_Data/Resource/Ore.asset` · `Crystal.asset` — **아무도 참조하지 않는다.** 새로 만든 `Node_*` 세트가 같은 역할을 하므로 중복. 삭제 후보.
- `Assets/Resources/Data/Biome/New Biome Content Data.asset` — 폴더가 **`Biome`(단수)**라 `BiomeContent`가 읽는 `Data/Biomes`에 안 잡힌다. 죽은 파일.
- `Assets/Resources/Data/Research/New Research Data.asset` — 빈 스캐폴드. `Res_*` 6종이 생겼으므로 정리 대상.
- ~~`Assets/5_Data/Item/`~~ — 2026-07-31 사용자가 삭제 완료.

## 아직 비어 있는 것

- **아이콘 · 월드 모델** — `Icon` / `WorldModel`이 전부 null이다(만들 수 없는 자산). 인벤토리에서 빈 칸으로 보인다.
- **제작대 연결** — 레시피는 `CraftSkill`로만 분류돼 있다. 어느 작업대에서 뽑을지는 작업대 프리팹 쪽 설정을 봐야 한다.

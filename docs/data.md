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
- `GetItem(id)` / `GetRecipe(id)` / `GetResearch(id)` / `GetBuilding(name)` / `GetUnit(id)`
- `GetRecipesBySkill(CraftSkill)` — 제작대 타입별 레시피 목록
- `AllItems` / `AllRecipes` / `AllResearch` / `AllBuildings` / `AllUnits` / `AllMinions`

## JSON 파일 형식

### 아이템 (`StreamingAssets/Data/items/`)

`type` 필드로 종류 구분:

```json
{
  "type": "weapon",
  "itemName": "철검",
  "iconPath": "icons/iron_sword.png",
  "modelPath": "IronSword",
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
- `resourceGroup`: `Stone`, `Ore`, `Gem`, `Ingot`, `Wood`, `Currency` (옛 `resourceType`도 받아 묶음으로 옮기고 경고)
- `requiredSkillType`: `Labor`, `WeaponCrafting`, `ArmorCrafting`, `Cooking`, `Alchemy`, `Research`, `Smelting`, `Stonecutting`, `Woodworking`, `Gemcutting`

**미구현 JSON 타입** (향후 추가 예정): UnitData/MinionData, SubCoreData (프리팹 참조 문제), ResearchData

## 모드가 그림·모델을 들고 온다 (`ModAssets`, 2026-08-01)

전에는 `iconPath`가 `Resources.Load` 하나였다 — **빌드에 이미 들어 있는 것만** 가리킬 수 있었으니 모드가 새 PNG를 넣을 자리가 없었고, 모델은 경로 필드조차 없었다. 그래서 "숫자만 바꾸는 모드"까지만 가능했다.

`ModAssets.LoadSprite(path, root)` / `LoadPrefab(path, root)`가 **경로 하나를 세 곳에서 차례로 찾는다**:

| 순서 | 어디서 | 무엇에 쓰이나 |
|---|---|---|
| 1 | **그 콘텐츠 폴더 안의 파일** — `icons/x.png`, `models/x.obj` | **모드 구조의 본체.** 그림(PNG/JPG)과 모델(OBJ). 확장자 생략 가능 |
| 2 | **에셋 번들** — 모드 폴더의 `*.bundle`(하위 폴더 포함) 안 에셋 이름 | OBJ로 표현 못 하는 것(프리팹·뼈대·애니메이션) |
| 3 | **Resources** | 예전 동작. 기본 콘텐츠 호환용 폴백 |

- `root`(콘텐츠 폴더)는 `DataManager`가 로딩 내내 들고 다닌다 — 그래서 **폴더를 통째로 넣고 빼면 콘텐츠가 통째로 들어오고 나간다.**
- **폴더 밖(`../`)은 해석하지 않는다.** 모드가 남의 파일을 읽을 이유가 없다.
- 캐시 키는 `루트|경로`이고 **실패(null)도 캐시한다** — 100개가 같은 그림을 가리켜도 파일은 한 번만 읽고, 없는 경로를 매번 다시 훑지 않는다.
- 만든 텍스처·스프라이트는 `Reset`(Play 재시작)에서 직접 지운다. **에셋 번들도 반드시 내린다** — 안 내리면 다음 Play에서 같은 파일을 못 열어 그 모드의 모델이 통째로 사라진다.

### 모델은 OBJ로 넣는다 (`ObjModel`)

**번들만으로는 모드 구조가 성립하지 않는다.** 번들을 구우려면 모드 제작자가 유니티를 설치하고 **정확히 같은 버전**으로 구워야 한다 — "폴더를 넣으면 된다"가 모델에서만 깨진다.

`.fbx`는 런타임에 해석할 방법이 없지만 **`.obj`는 순수 텍스트라 읽을 수 있고 모든 3D 툴이 내보낸다.** 그래서 `ObjModel`이 OBJ/MTL을 직접 파싱한다 — 텍스처는 이미 있는 PNG 로더가 받는다.

| 읽는 것 | |
|---|---|
| OBJ | `v` · `vt` · `vn` · `f`(n각형 부채꼴 삼각화) · `usemtl`(재료별 서브메시) · `mtllib` |
| MTL | `newmtl` · `Kd`(기본색) · `d`/`Tr`(투명) · `map_Kd`(텍스처) |
| 안 함 | 뼈대·애니메이션·PBR 맵 — 그건 에셋 번들 몫 |

구현에서 틀리기 쉬운 지점:
- **좌표계.** OBJ는 오른손(Y 위, −Z 앞), 유니티는 왼손. **Z를 뒤집고 삼각형 감는 방향도 뒤집는다** — 축만 뒤집으면 거울상이 되고, 감는 방향만 뒤집으면 앞뒤가 뒤집혀 안쪽만 보인다.
- **정점 펼치기.** OBJ는 위치·UV·법선을 따로 색인하지만 유니티 메시는 정점 하나에 셋이 함께 붙는다. `v/vt/vn` 조합마다 정점을 하나씩 만들어야 한다.
- **색인은 1부터, 음수는 끝에서 센다**(−1 = 마지막). 음수 색인을 빠뜨리면 그걸 쓰는 도구가 뽑은 파일이 통째로 깨진다.
- **`InvariantCulture`로 파싱한다.** 한국어 윈도우 기본 문화권으로 읽으면 소수점 해석이 달라져 좌표가 통째로 어긋난다.
- **정점 65535개를 넘으면 `IndexFormat.UInt32`.** 안 올리면 조용히 잘린다.
- **법선이 하나라도 비면 통째로 다시 계산한다.** 섞으면 그 면만 새까맣게 나온다.

런타임에 만든 모델을 `Instantiate` 원본으로 쓰는 데도 함정이 있다. 원본이 그냥 씬에 있으면 원점에 모델이 하나 떠 있고, 원본을 끄면 **사본도 꺼진 채로 나와 떨어진 아이템이 안 보인다.** → **부모만 끄고 원본은 켜 둔다**(`[ModAssets Templates]`). 사본은 부모 없이 태어나므로 정상이다.

### 에셋 번들 (보조 경로)

OBJ로 표현 못 하는 것 — 프리팹, 뼈대, 애니메이션 — 만 번들로 간다.

```
에디터: 모델·프리팹 선택 → DungeonColony → Build Mod AssetBundle → 모드 폴더 지정
JSON:   "modelPath": "<번들 안의 에셋 이름>"
```

번들 이름은 **모드 폴더 이름**이라 모드마다 하나씩 생기고, 다시 구우면 덮어써 쌓이지 않는다. 압축은 청크(LZ4) — `LoadFromFile`은 기본 LZMA면 통째로 풀어야 해서 느려진다. **번들은 유니티 버전·플랫폼마다 다르다**(다른 버전에서 구운 것은 안 열린다).

`.fbx`를 그냥 폴더에 넣는 흔한 실수는 `ModAssets`가 파일 이름으로 알아채 "OBJ로 내보내라"고 알려 준다. 그냥 "못 찾음"이라고만 하면 파일이 눈앞에 있으니 원인을 못 찾는다.

### 예시 모드

`StreamingAssets/Mods/ExampleMod/` — 아이템 1 + 레시피 1 + 아이콘 PNG + OBJ/MTL/텍스처. **유니티 없이 이것만으로 실린다.** `용광로`에서 구리 광석 1개로 `예시 부적`을 만들 수 있고, 목록에서 고르면 결과 그림에 모드의 PNG가, 바닥에 버리면 줄무늬 팔면체가 나온다. **검증용이므로 지워도 된다**(지우면 안의 콘텐츠도 함께 사라진다). 폴더 구조와 규칙은 그 안의 `README.txt`.

## ScriptableObject Assets (`Assets/Resources/Data/`)

| Type | 생성 메뉴 | 주요 필드 |
|---|---|---|
| `UnitData` | `DungeonColony/UnitData` | RaceName, NamePool, MaxHP, MoveSpeed, AttackDamage, AttackRange, DetectRange, DefaultMin/MaxGrade, StatGrades, StatGrowthRates |
| `MinionData` | `DungeonColony/MinionData` | (UnitData) + SummonCostItem, SummonCostAmount, AllowedJobs, CanProvideWater, Prefab, SortOrder |
| `NpcData` | `DungeonColony/NpcData` | **역할 전용**(종족 상속 아님) — NpcType, WanderRadius, FleeDetectRange, StayDuration, ArrivalOffset, MerchantData |
| `WeaponData` | `DungeonColony/WeaponData` | WeaponType, BaseDamage, BaseAttackInterval, AttackRange, IsLargeWeapon, IsBladed, IsRanged, BlockBonus, RangedBlockBonus, DamageCategory, PhysicalType, DamageElement, EffectChance |
| `ArmorData` | `DungeonColony/ArmorData` | ArmorSlot, DamageReduction |
| `BuildingData` | `DungeonColony/BuildingData` | BuildingName, Category, SortOrder, **Size**, CostItem, CostAmount, Prefab, UsesBlueprint, IsFloor, **OccupiesTile**, RequiredResearch |
| `DungeonCoreData` | `DungeonColony/DungeonCoreData` | CoreType, BaseRadius, MaxGrade, BaseMinionCap, HungerRateModifier, per-frame modifiers |
| `ResourceNodeData` | `DungeonColony/ResourceNodeData` | DropItem, GaugeRequired, DropMin, DropMax, RequiredSkill, IsTree |
| `ItemData` | `DungeonColony/ItemData` | ItemName, Description, Icon, Width, Height, Weight |
| `ResourceItemData` | `DungeonColony/ResourceItemData` | (ItemData) + `Group`(`ResourceGroup`), Stack (MaxStack 오버라이드) |
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
- `UnitData`(종족)
  - `MinionData` — 소환 가능한 종족

`NpcData`(역할)는 **종족을 상속하지 않는다** — 2026-08-06에 갈랐다. 조합은 프리팹이 한다(`UnitBrain`이 `unitData`와 `npcData`를 따로 든다).

## 게임 데이터 Excel

`python Tools/export_gamedata.py` → `Tools/GameData.xlsx`(Items·Recipes·Buildings·Enums) + `Tools/Items.csv`·`Recipes.csv`(git diff용).

**표는 저작물이 아니라 에셋의 그림자다** — 에셋과 코드를 직접 읽어 만든다. 값을 바꿨으면 다시 돌릴 것. 제작 배수(산출값 ÷ 재료비)가 1 미만이면 붉게 칠해진다.

---

# 식별자는 `Id`다 — 표시명이 아니다 (2026-08-01)

`ItemData` · `RecipeData` · `ResearchData` · `BuildingData`에 **`Id` 필드**가 있다.

**규칙: `Id`가 비어 있으면 에셋 파일명을 쓴다.** (`DataId.Resolve`)
파일명은 이미 유일하고 ASCII이며(`Res_IronOre`, `Rec_Bronze`) 잘 안 바뀐다. 그래서 **대부분은 비워 두면 되고**, 파일명과 다르게 하고 싶을 때만 채운다 — "데이터가 없으면 기본 동작"이라는 이 프로젝트의 관례와 같다.

## 왜 표시명을 쓰면 안 되나

07-31까지 `_items[item.ItemName]`, `_recipes[recipe.RecipeName]`처럼 **한글 표시명이 곧 키**였다.

| 문제 | 결과 |
|---|---|
| 표시명은 **번역 대상** | "철검"을 "Iron Sword"로 바꾸는 순간 그걸 가리키던 모드 JSON·참조가 전부 끊긴다 |
| 표시명은 **겹칠 수 있다** | 모드 둘이 같은 이름을 쓰면 **경고도 없이** 하나가 사라진다 |
| 표시명은 **저작 중 자주 바뀐다** | 식별자는 안 바뀌어야 한다 |

게다가 연구만 파일명 기준이라 **네 종류의 규칙이 제각각**이었다. 지금은 넷 다 같다.

## 이행은 끝났다 (2026-08-02) — 경고는 이제 부채의 신호다

저장소 안의 콘텐츠는 **전부 Id로 가리킨다.** 표시명 조회(보조 색인)는 **남의 모드를 위해** 남겨 뒀다 — 조회 순서는 **`Id` 먼저 → 없으면 표시명**이고, 표시명으로 찾히면 키마다 한 번씩 경고한다. `Id` 충돌은 시끄럽게 알린다(무엇이 무엇을 덮었는지).

> **콘솔에 표시명 경고가 뜨면 그건 남은 부채다.** `WarnLegacy`는 못 옮긴 참조가 있다는 뜻이고, `WarnDuplicateName`은 같은 이름의 데이터가 둘이라 표시명 참조가 **뒤엣것**을 집는다는 뜻이다. **경고가 침묵인 것이 정상 상태다.**

모드가 쓸 JSON 식별자 필드:

| 스키마 | 필드 | 비우면 |
|---|---|---|
| `ItemSchema` | `id` | `itemName`이 식별자 (레거시) |
| `RecipeSchema` | `id` · `resultItemId` | `recipeName` · `resultItemName` |
| `IngredientSchema` | `itemId` | `itemName` |

> JSON으로 만든 아이템·레시피는 에셋 파일이 아니라 `.name`이 비어 있다 — 파일명 폴백이 안 통하므로 `ApplyBase`/레시피 로더가 **Id를 직접 못 박는다.**

**에셋 이름을 고칠 때는 `.meta`째 `git mv`**(GUID 보존 → 씬·프리팹 참조 무사) 하고 **`.asset` 안의 `m_Name`도 같이 고칠 것** — `DataId.Resolve`가 읽는 건 파일명이 아니라 `asset.name`이다. 파일만 바꾸면 식별자는 옛 이름 그대로다.

---

# 데이터를 어디에 두나 — `5_Data` vs `Resources/Data`

| | `Assets/5_Data/` | `Assets/Resources/Data/` |
|---|---|---|
| 빌드 포함 | **참조된 것만** (씬·프리팹·다른 SO가 물고 있어야 함) | **전부 무조건** |
| 찾는 법 | 인스펙터 연결(직렬화 참조) | `Resources.Load` / `LoadAll` — **이름·경로로** |
| 쓰는 것 | `ResourceNodeData` · `UnitData` · `FactionData` · `AiProfileData` · `WorldMapData` … | `ItemData` · `RecipeData` · `ResearchData` · **`BuildingData`**(`DataManager`) / `BiomeContentData`(`BiomeContent`) |

**가르는 기준은 하나** — *"코드가 이름만 알고 실물을 몰라도 찾아와야 하는가?"*

- 모드 JSON이 `"itemName": "철검"`처럼 **이름으로 가리키는** 아이템·레시피·연구는 레지스트리가 필요하다 → `Resources`.
- 노드·건물·유닛처럼 **누군가 인스펙터에서 직접 꽂아 쓰는** 것은 참조만으로 충분하다 → `5_Data`.

주의: `Resources` 폴더는 **빌드에서 뺄 수 없고 시작 시 다 로드**된다. 참조로 충분한 것을 여기 두면 안 쓰는 에셋까지 빌드에 실린다. 그래서 기본값은 `5_Data`이고, **레지스트리가 필요할 때만** `Resources`로 보낸다.

## 예외 — static 팩토리가 쓰는 고정 에셋 (`Resources/Prefabs/`)

레지스트리가 아니어도 `Resources`에 두는 것이 하나 있다. **`Resources/Prefabs/DropBundle.prefab`** — 바닥에 떨어진 아이템의 공용 모델(`ItemData.WorldModel`이 빈 경우의 폴백).

프리팹은 원래 `3_Prefab` + 인스펙터 참조가 기본인데, 여기만 다른 이유:

1. **꽂아줄 자리가 없다.** `DroppedItem.Spawn`은 씬에 존재하지 않는 **static 팩토리**다. 위의 가르는 기준 — *"코드가 이름만 알고 실물을 몰라도 찾아와야 하는가?"* — 에 그대로 해당한다.
2. **절대 실패하면 안 되는 폴백이다.** 존재 이유가 "아무것도 지정되지 않았을 때 대신 나서는 것"인데, 씬 참조로 두면 **배선을 안 한 새 씬에서 정확히 폴백이 필요한 순간에 없다.** 정착지가 여럿 생기면 맵마다 배선을 반복해야 하고, 하나만 빠뜨려도 거기서만 조용히 흰 큐브가 된다.
3. 값은 **프리팹 하나 + 머티리얼 하나**라 빌드에 강제로 실리는 비용이 무시할 만하다.

> 같은 판단이 필요한 경우: **static/전역 코드가 쓰고, 없으면 기능이 조용히 망가지는 고정 에셋**이면 `Resources/Prefabs/`. 그 외 프리팹은 전부 `3_Prefab` + 인스펙터 참조.

---

# 테이블은 데이터를 어디서 얻나 — **레지스트리에서 끌어온다**

씬에 미리 놓아둔 인스턴스에만 목록을 채워 넣으면, **런타임에 지은 것은 목록이 텅 빈다.** 그래서 작업대류는 자기 인스턴스가 아니라 레지스트리를 본다.

| 컴포넌트 | 목록 출처 | 인스턴스 설정 |
|---|---|---|
| `CraftingTable` | `DataManager.GetRecipesBySkill(tableType)` | `tableType`(어느 스킬의 작업대인가)만 |
| `ResearchTable` | `DataManager.AllResearch` | `availableResearch`는 **비워 두는 게 기본** |
| `BuildModeManager` | `DataManager.AllBuildings` | `placeableBuildings`는 **비워 두는 게 기본** |

**건설 목록도 2026-08-01에 같은 규칙으로 옮겼다.** 전엔 `BuildingData`가 `5_Data/Building`에 있고 목록이 `BuildModeManager.placeableBuildings` **씬 배열**이었는데, 두 가지가 나빴다:
- **씬에 사는 목록이라 조용히 날아갔다** — 에디터 도구가 15건을 등록해도 씬을 저장하지 않으면 사라진다. 콘솔에는 성공 로그가 찍히므로 **디스크를 봐야만** 알 수 있었다(2026-07-31에 실제로 겪음).
- **모드가 건물을 추가할 진입점이 없었다.**

이제 `Assets/Resources/Data/Buildings/`에 에셋을 두기만 하면 목록에 오른다. 메뉴 순서는 **`Category` → `BuildingData.SortOrder` → 이름** — `Resources.LoadAll`이 순서를 보장하지 않으므로 `SortOrder`가 순서의 유일한 근거다.

**`availableResearch`는 큐레이션용 오버라이드다** — 채우면 그게 이기고, 비우면 전체를 쓴다. "데이터가 없으면 기본 동작"이라는 이 프로젝트의 관례(`BiomeContentData` · `WorldTileVisualData`와 같은 규칙)를 따른다.

> 2026-07-31 이전에는 `ResearchTable`이 **인스턴스 목록만** 봤다. 씬에 놓인 연구대에만 데이터가 들어가고 나중에 건설하는 연구대는 빈다는 지적으로 고쳤다. 새 작업대/테이블을 만들 때도 같은 규칙을 따를 것 — **인스턴스에는 "무엇을 다루는 테이블인가"만 두고, 목록은 레지스트리에서 끌어온다.**

---

# 생성한 콘텐츠 데이터 (2026-07-31)

`DungeonColony → Create Missing Data` (`Assets/Editor/DataScaffoldCreator.cs`)로 한 번에 만든다.
**없는 것만 만들고 이미 있는 에셋은 손대지 않는다** — 여러 번 실행해도 안전하다.

> 최초 생성 시 숫자는 "시스템이 굴러가는 걸 눈으로 보려고" 채운 임시 초안이었다.
> **2026-07-31 밸런스 패스에서 전부 재산정했다** — 기준은 아래 "시간 축"과 "가치 사슬" 절이 정본이다.

## 무엇이 어디에 생기나

| 종류 | 경로 | 개수 | 비고 |
|---|---|---|---|
| 자원 아이템 `ResourceItemData` | `Resources/Data/Items/Res_*.asset` | 22종 | 종류는 아이템 자체가 정한다(옛 `ResourceType` enum은 08-02 삭제) |
| 작물·음식 | `Resources/Data/Items/Crop_*·Food_*` | 7 | 밀 / 생고기 / 생선 / 빵 / 구운 고기 / 산딸기 / 버섯 |
| 씨앗 `SeedData` | `Resources/Data/Items/Seed_*` | 3 | 밀 / 산딸기 / 버섯 |
| 무기 `WeaponData` | `Resources/Data/Items/Wpn_*` | 11 | 검·도끼·철퇴·창·활·석궁·지팡이·방패 2종 |
| 방어구 `ArmorData` | `Resources/Data/Items/Arm_*` | 8 | 가죽 4부위 / 철 4부위 |
| 자원 노드 `ResourceNodeData` | `5_Data/Resource/Node_*` | 10 | 철·구리·주석·석탄·은·금·마법석·에테르석·공허석·루비 |
| 레시피 `RecipeData` | `Resources/Data/Recipes/Rec_*` | 10 | 제련 4 / 가공 2 / 요리 2 / 장비 2 |
| 연구 `ResearchData` | `Resources/Data/Research/Res_*` | 7 | 제련술·제강술·석공술·목공술·조리법·무기 제작 + **귀금속 제련**(07-31 추가) |
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
          ├선행─> Res_Steelmaking(제강술) ──해금──> Rec_Steel, Rec_IronChest
          └선행─> Res_PreciousMetals(귀금속 제련) ──해금──> Rec_SilverIngot, Rec_GoldIngot
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

> **enum 값은 중간에 끼우지 말고 끝에 추가할 것.** 에셋은 정수로 저장돼 있어 순서가 바뀌면 조용히 다른 뜻이 된다(`ResourceType` 시절 실제로 겪었고, 지금은 `MerchantType`·`ItemQuality`가 같은 성질이다).

## 알려진 잔재

- `5_Data/New Merchant Data.asset` — 빈 스캐폴드. 정리 후보.

## 아직 비어 있는 것

- **아이콘 · 월드 모델** — `Icon` / `WorldModel`이 전부 null이다(만들 수 없는 자산). 인벤토리에서 빈 칸으로 보인다.
- **제작대 연결** — 레시피는 `CraftSkill`로만 분류돼 있다. 어느 작업대에서 뽑을지는 작업대 프리팹 쪽 설정을 봐야 한다.

---

# 시간 축 — 모든 지속시간의 기준 (2026-07-31)

`GameTimeManager.dayDuration` = **1800초(30분), 낮 20분 + 밤 10분**(`dayFraction` 2/3)이 **단일 기준 축**이다.
07-31 이전엔 하루가 3600초인데 나머지 데이터가 훨씬 짧은 하루를 가정해 서로 다른 축에 있었다 — 허기는 하루 18끼, 밀은 하루 40회전, 연구는 하루면 전 테크가 끝났다.

**하루 길이를 바꾸면 아래를 전부 같이 옮겨야 한다.** 개별 숫자보다 이 비율이 정본이다.

| 축 | 어디 | 기준 |
|---|---|---|
| 허기 | `UnitBrain.BaseHungerDecayRate` = 0.111/초 | 100 → 0 이 900초 = **하루 2끼** |
| 작물 | `SeedData.GrowthTime` 800~1600초 | 밀 1600 ≈ **낮 한 판** / `MoistureDecay`는 100 ÷ (GrowthTime÷2) → **급수 2회** |
| 연구 | `ResearchData.ResearchTime` 250~900초 | **하루에 1~3개** |
| 제작 | `RecipeData.CraftTime` 12~120초 | 판자 12 … 철 갑옷 120 |
| 채굴 | `ResourceNodeData.GaugeRequired` 60~420 | 단위가 **스킬-초**. 값 = 초보(스킬 10) 기준 **1회 산출까지의 초 × 10** |

## 채굴 게이지가 "스킬-초"인 이유

`ResourceNode.Work(power, deltaTime)`는 `power × deltaTime`을 누적한다. 그래서 게이지 단위는 **스킬 수치 × 작업한 초**다.
스킬 10인 폰이 `GaugeRequired 150`짜리 철광맥에 붙으면 **15초에 1회** 산출한다 — 값을 읽을 때 "초보 기준 몇 초?"로 나누면 된다.

> 07-31 이전엔 `Work(skill)`이 BT 틱(0.1초)마다 **델타 없이** 불려 실제 속도가 초당 `skill×10`이었다. 스킬 10이 돌 노드(당시 `GaugeRequired 3`)에서 **초당 33개**를 캤다. 시그니처에 `deltaTime`을 넣어 호출자가 빠뜨릴 수 없게 했다.

| 노드 | Gauge | 초보 기준 | 산출 |
|---|---|---|---|
| 돌 | 60 | 6초 | 2~4 |
| 나무 | 80 | 8초 | 2~3 |
| 석탄 | 100 | 10초 | 2~3 |
| 구리 · 주석 | 120 | 12초 | 1~3 |
| 철 | 150 | 15초 | 1~2 |
| 은 | 220 | 22초 | 1~2 |
| 마력석 | 260 | 26초 | 1 |
| 금 | 320 | 32초 | 1 |
| 에테르석 · 공허석 | 360 | 36초 | 1 |
| 루비 | 420 | 42초 | 1 |

---

# 가치 사슬 — 제작은 손해가 아니어야 한다

규칙: **결과물 `BaseValue` ≈ 재료 합의 1.4~1.8배.** 그 아래면 팔아서 손해고, 그 위면 제작이 돈 찍는 기계가 된다.

07-31 이전엔 제련이 전부 적자였다(은 주괴 110 ← 재료 115 / 금 주괴 180 ← 재료 185 / 강철은 마진 0). 원인은 원석 값만 정하고 주괴를 따로 매겨서다.

| 결과물 | 재료 | 재료 합 | 값 |
|---|---|---|---|
| 다듬은 돌 | 돌 2 | 4 | **6** |
| 판자 ×2 | 목재 3 | 9 | 8 ×2 = **16** |
| 청동 주괴 | 구리3 + 주석1 + 석탄1 | 60 | **90** |
| 강철 주괴 | 철3 + 석탄2 | 80 | **120** |
| 은 주괴 | 은3 + 석탄1 | 115 | **170** |
| 금 주괴 | 금3 + 석탄2 | 185 | **280** |
| 철검 | 철광4 + 판자1 | 88 | **150** |
| 철 갑옷 | 강철3 + 판자1 | 368 | **550** |

철 방어구는 강철 인상분을 따라 세트 비율(갑옷 1 : 각반 0.58 : 투구 0.42 : 건틀릿 0.31)을 유지한 채 올렸다 — 550 / 320 / 230 / 170.
가죽 세트는 레시피가 없는 **상인 전용 초반 장비**라 그대로 뒀다.

---

# 연구가 실제로 레시피를 잠근다 (2026-07-31)

이전엔 **연구가 아무것도 잠그지 않았다.** 원인이 둘 겹쳐 있었다.

1. **양쪽 필드가 서로 다른 쪽을 봤다.** `ResearchManager.IsUnlocked`는 `RecipeData.RequiredResearch`만 읽는데 레시피 10개 전부 비어 있었고, 데이터는 반대편인 `ResearchData.UnlockedRecipes`에만 채워져 있었다 — 그 필드를 읽는 코드가 없었다.
2. **`ResearchManager.Instance`가 항상 null이었다.** `Instance` 프로퍼티와 `_instance` 필드가 따로 있었고 `Awake`가 필드에만 대입했다. 이걸 읽는 6곳이 전부 "매니저 없음" 분기로 빠져 **연구 완료·선행 검사·해금이 통째로 무시**됐다.

지금은 **양방향 어느 쪽으로 적어도 걸린다** — `UnlockedRecipes`(연구 하나가 여러 레시피를 여는 큐레이션, `Awake`에서 역인덱스 생성) ∥ `RequiredResearch`(레시피 개별 지정). 둘 다 비면 처음부터 열린 레시피다.

**새 레시피를 만들면 어느 연구에 매달지 정할 것** — 안 매달면 조용히 처음부터 열린다. `Rec_SilverIngot`·`Rec_GoldIngot`이 그래서 유일하게 무연구 상태였고, `Res_PreciousMetals`(귀금속 제련, 선행=제련술)를 새로 만들어 붙였다.

```
제련술 ──> 청동 주괴
  ├─ 제강술 ──> 강철 주괴, 철 갑옷
  └─ 귀금속 제련 ──> 은 주괴, 금 주괴
석공술 ──> 다듬은 돌      목공술 ──> 판자
조리법 ──> 빵, 구운 고기   무기 제작 ──> 철검
```

시작 시 제작 가능한 레시피는 **0개**다. 연구 재료(돌·목재·석탄·철광석·은광석)는 전부 채굴로만 얻을 수 있어 교착은 없다.

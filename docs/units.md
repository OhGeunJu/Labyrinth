# Units System

## UnitBrain & UnitBlackboard

`UnitBrain` — BT를 0.1초 틱으로 실행. `IsKnockedOut` 또는 `IsStaggered` 동안 BT 정지.

`UnitBrain`은 partial class로 분리:
- `UnitBrain.cs` — `BuildTree()` 분기 (unitData 타입에 따라 서브 메서드 호출)
- `UnitBrain.Wanderer.cs` — `BuildWandererTree()`
- `UnitBrain.Minion.cs` — `BuildMinionTree()`
- `UnitBrain.Enemy.cs` — `BuildEnemyTree()`
- `UnitBrain.Npc.cs` — `BuildNpcTree()` + NpcType별 서브 메서드

새 유닛 타입 추가 시 파일만 추가. `UnitBrain.cs` 본체는 건드리지 않음.

`UnitBlackboard` — 런타임 상태:

| 필드 | 설명 |
|---|---|
| `Target` | 전투 대상 |
| `CurrentJob` | 현재 수행 중인 작업 |
| `AssignedNode` | 작업 대상 노드 |
| `IsDrafted` | 방랑자 징집 여부 |
| `IsUnarmed` | 비무장 여부 |
| `IsKnockedOut` | 기절 여부 |
| `IsStaggered` / `StaggerTimer` | 비틀거림 |
| `AttackDamage` / `AttackRange` | 무기 스탯 (Equipment.SyncBlackboard로 갱신) |
| `Hunger` | 0~100. Player 팩션만 시간에 따라 감소 |
| `AssignedPatrolZone` | 배정된 순찰 구역 |
| `HomePosition` | NPC 귀환 기준점 |
| `NpcDestination` / `NpcTimer` / `NpcPhase` | NPC BT 내부 상태 |

HP는 없음 — `BodyPartSystem.Blood`를 혈액 수치로 사용.

## Stat System (`UnitStats.cs`, `UnitData.cs`, `StatGrade.cs`)

**모든 스탯 0–130, 5단위 등급제** (`StatGrade.cs`).

### 등급 시스템

`StatGrade` enum — 27단계: `FMinus, F, FPlus, EMinus, ..., SSSMinus, SSS, SSSPlus`
- `ToValue()` — 5단위 매핑 (FMinus=0, F=5, ..., SSSPlus=130)
- `ToDisplay()` — "B+", "SS-" 형태 표시명
- `FromValue(float)` — 수치 → 등급 역변환
- `RandomBetween(min, max)` — 미니언 등급 추첨용
- `GetColor()` — 9단계 티어별 색상 (회색~빨강 무지개)

`StatName` enum — 51개 스탯 식별자. `UnitStats.GetStat(StatName)` / `SetStat(StatName, float)` / `GrowStat(StatName, float)` / `GetGrade(StatName)`로 통합 접근.

### UnitData 등급 정의

`UnitData`는 더 이상 `Vector2 *Range` 필드를 가지지 않음. 대신:
- `DefaultMinGrade` / `DefaultMaxGrade` — 오버라이드 없는 모든 스탯의 기본 등급 범위
- `StatGrades: List<StatGradeEntry>` — 스탯별 등급 오버라이드 (`{StatName, Min, Max}`)
- `GetGradeRange(stat)` / `RollGrade(stat)` — Min~Max 사이 가중치 없는 랜덤 등급 추첨

`UnitStats.FromData(data)` — 모든 `StatName`을 순회하며 `data.RollGrade(stat).ToValue()`로 초기화. Min==Max면 고정값.

### 미니언 스탯 잠금

`UnitStats.IsLocked: bool` — true면 `GrowStat` 호출이 no-op. `UnitBrain.Awake`에서 `unitData is MinionData`일 때 자동 잠금.

→ 미니언은 소환 시 등급 랜덤 추첨된 후 모든 스탯이 영구 고정. 방랑자/NPC만 켄시식 XP로 성장.

### 등급 승급 이벤트

`UnitStats.OnGradeUp` (인스턴스), `UnitStats.OnAnyGradeUp` (정적) — `(stats, statName, newGrade)` 시그니처. `GrowStat`에서 등급 경계 넘을 때 발동. `Owner: GameObject` 필드로 소속 유닛 추적.

| 카테고리 | 필드 |
|---|---|
| **속성** | Strength, Toughness, Dexterity, Cognition |
| **전투 기술** | MeleeAttack, MeleeDefense, RangedDefense, PrecisionShooting |
| **근접 무기** | OneHandSword, OneHandAxe, OneHandMace, TwoHandSword, TwoHandAxe, TwoHandMace, Polearm |
| **원거리 무기** | Bow, Crossbow, Javelin |
| **마법 (카테고리)** | ElementalMagic, DarkMagic, HolyMagic |
| **마법 원소** | ElementalFire/Ice/Lightning/Poison, DarkFire/Ice/Lightning/Poison, HolyFire/Ice/Lightning/Poison |
| **신체 기술** | MartialArts, Evasion, Athletics, Swimming |
| **노동** | Labor, Mining, Growing, Cutting, Taming, Cooking, Medicine, Research, WeaponCrafting, ArmorCrafting, Alchemy, Smelting, Stonecutting, Woodworking, Gemcutting |

`UnitStats` 헬퍼:
- `GetSkillForWeapon(WeaponType)` — 무기 종류별 숙련도 반환
- `GrowSkillForWeapon(WeaponType, amount)` — 해당 스킬 성장 (100 캡)
- `GetMagicCategorySkill(WeaponType)` — 마법 카테고리 스킬 (정확도/빈도용)
- `GetAttackCombatSkill(bool unarmed)` — 비무장이면 MartialArts, 아니면 MeleeAttack
- `GetDefenseCombatSkill(bool unarmed)` — 비무장이면 MartialArts, 아니면 MeleeDefense

## Equipment System (`Equipment.cs`)

`UnitBrain.Awake`에서 자동 추가.

**슬롯:**
- `MainHand` — `Equip(WeaponData)` / `Unequip()`
- `OffHand` — `EquipOffHand` / `UnequipOffHand`. IsLargeWeapon 시 `OffHandLocked`
- `Head / Chest / Hands / Legs` — `EquipArmor(ArmorData)` / `UnequipArmor(ArmorSlot)`

**ItemQuality 연동:**
- `_mainHandInst`, `_offHandInst`, `_armorInst` 딕셔너리로 `ItemInstance` 추적
- `SyncBlackboard()`: `AttackDamage = BaseDamage × Quality.StatMultiplier()`
- `GetArmorQualityMult(ArmorSlot)` — 갑옷 슬롯별 등급 배율 노출
- 장착/해제 시 `Inventory.TryAdd(item, amount, quality)`로 등급 보존

`WeaponData` 주요 필드: `WeaponType`, `BaseDamage`, `BaseAttackInterval`, `AttackRange`, `IsLargeWeapon`, `IsBladed`, `IsRanged`, `BlockBonus`, `RangedBlockBonus`, `DamageCategory`, `PhysicalType`, `DamageElement`, `EffectChance`

`WeaponType` enum: Unarmed, Shield, OneHand/TwoHand 근접, Bow/Crossbow/Javelin, ElementalFire/Ice/Lightning/Poison, DarkFire/Ice/Lightning/Poison, HolyFire/Ice/Lightning/Poison

## Movement (`PawnMover.cs`)

- `SetStats(UnitStats, Inventory)` — Athletics 속도 및 무게 보정 활성화
- `SetMoveTarget(Vector3)` / `SetDestination(Vector3)` (별칭) — 이동 목표 설정
- `IsMoving()` — `_hasTarget` 반환 (NPC BT 도착 여부 판단용)
- 유효 이동속도: `baseMoveSpeed × (1 + Athletics/100) × weightCorrection × MoveSpeedMult`
- `MoveSpeedMult` — `StatusEffectSystem`에서 제공 (Slowed 디버프)
- `Athletics` — 이동 중 초당 성장

## Hunger System

`UnitBlackboard.Hunger` — 0~100. `FactionType.Player`인 유닛만 감소.

**감소 로직 (`UnitBrain.UpdateHunger`):**
- 기본 감소율: `BaseHungerDecayRate = 0.5f` (초당, 약 3.3분에 고갈)
- `DungeonCoreData.HungerRateModifier`로 감소율 조절 가능
- 허기 0 → 초당 2 피해

**`FoodData : ItemData`** (`Item/FoodData.cs`):
- `NutritionValue` — 섭취 시 허기 회복량
- `EatTime` — 먹는 데 걸리는 시간(초)
- `MaxStack = 20`

**`EatNode` (BT):**
- 허기 60 이하 시 트리거
- 인벤토리에 음식 있으면 바로 먹기, 없으면 가장 가까운 창고 수거 후 섭취
- BT 위치: Fight → Rest → **Eat** → Work

## Bed & BloodCap System

**`BodyPartSystem.BloodCap`** — 자연회복 상한선:
- 침대 없이는 최대 절반까지만 자연회복 가능
- `HealBlood(amount, degradeCap)` — 포션/마법 힐은 `BloodCap`을 회복량 × 0.5 감소
- `RestoreBloodCap(amount)` — 침대 치료 시 호출, 캡 복구

**`Bed.cs`** (씬 오브젝트):
- `TryOccupy()` / `Vacate()` — 점유 관리
- `Assign(unit)` / `Unassign()` — 배정 관리
- 클릭 시 `OnSelected` 이벤트

**`RestNode` (BT):**
- 혈액 60% 이하 또는 BloodCap 미복구 시 트리거
- 배정된 침대로 이동해 회복
- BT 위치: Fight → **Rest** → Eat → Work

## Patrol System

**`PatrolZone.cs`** (씬 오브젝트):
- `PatrolMode` enum: `Waypoint` (지정 포인트 순환) / `Area` (구역 내 무작위) / `Idle` (중심점 대기)
- 여러 하수인 배정 가능. 기즈모 시각화.

**`PatrolNode` (BT):**
- `UnitBlackboard.AssignedPatrolZone`이 없으면 Failure (작업으로 폴백)
- 있으면 모드별 이동
- BT 위치: Fight → Rest → Eat → **Patrol** → Work (하수인 전용)

`PatrolZoneUI` — 클릭 시 열림. 모드 전환 버튼 3개 + 배정 유닛 목록 + 해제 버튼

## NPC System

**`NpcData : UnitData`** (ScriptableObject, `DungeonColony/NpcData`):
- `NpcType` — Villager, Guard, ShopMerchant, EventMerchant
- `WanderRadius`, `FleeDetectRange`, `StayDuration`

**NpcType별 BT:**

| NpcType | BT |
|---|---|
| Villager | Flee → Wander → Idle |
| Guard | Detect+Fight → Patrol → Idle |
| ShopMerchant | Idle (고정) |
| EventMerchant | WalkIn → Stay(StayDuration) → WalkOut → Despawn |

**BT 노드 (`BTNpcNodes.cs`):**
- `NpcFleeNode(detectRange, fleeDistance=15f)` — 적 감지 시 반대 방향으로 `fleeDistance`만큼 이동
- `NpcWanderNode(radius)` — HomePosition 중심으로 radius 내 랜덤 배회. 도착 후 2~6초 대기
- `NpcEventMerchantNode(stayDuration)` — 이동→대기→귀환→소멸 단계 처리

**`NpcEventMerchantNode` 내부 동작:**
- Phase 0 (이동): `_moveSent` 플래그로 `SetDestination` 최초 1회만 호출. 거리 < 2f이면 Phase 1로 전환
- Phase 1 (대기): `NpcTimer`가 `stayDuration` 초과 시 HomePosition으로 출발 후 Phase 2
- Phase 2 (귀환): `!IsMoving()`이면 GameObject 소멸

> 주의: Phase 0 도착 판정은 `!IsMoving()`이 아닌 거리 체크. `IsMoving()` 기반이면 경로 초기화 전 첫 틱에 즉시 Phase 1로 넘어가는 버그 발생.

`UnitBlackboard.HomePosition` — NPC 귀환 기준점 (스폰 위치)
`UnitBlackboard.NpcDestination` — EventMerchant 목적지 (던전 코어 근처 랜덤 오프셋)
`UnitBlackboard.NpcTimer` / `NpcPhase` — EventMerchant BT 내부 상태

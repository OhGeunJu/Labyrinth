# Combat System

## Damage Type System (`Combat/`)

모든 피해는 `DamageInfo` struct로 전달됨.

```
DamageCategory: Physical, Elemental, Dark, Holy
PhysicalType:   Slash, Pierce, Blunt          (Category == Physical)
DamageElement:  None, Fire, Ice, Lightning, Poison  (Category != Physical)
```

**물리 피해 효과:**
- `Slash` — 추가 출혈 (actualDamage × 0.5 혈액 추가 감소)
- `Pierce` — 갑옷 관통
- `Blunt` — 데미지 × 1.3

**마법 피해 효과 (EffectChance 확률로 발동):**

| Category | Element | 효과 |
|---|---|---|
| Elemental | Fire | Burning DoT (5초, 5 DPS) |
| Elemental | Ice | 갑옷 관통 |
| Elemental | Lightning | 데미지 × 1.2 |
| Elemental | Poison | AoE — 미구현 |
| Dark | Fire | HealReduction 10초 |
| Dark | Ice | Slowed 8초 |
| Dark | Lightning | Weakened 8초 |
| Dark | Poison | CorruptionGauge +amount×0.3 |

`WeaponData` 필드: `DamageCategory`, `PhysicalType`, `DamageElement`, `EffectChance`  
마법 계열 `WeaponType`은 `EffectiveDamageCategory/Element`로 자동 추론.

## Status Effect System (`StatusEffectSystem.cs`)

`UnitBrain.Awake`에서 자동 추가.

| 효과 | 영향 |
|---|---|
| `Burning` | `BodyPartSystem.TakeDamage` DoT 매 프레임 |
| `HealReduction` | `HealMult < 1` |
| `Slowed` | `AttackIntervalMult > 1`, `MoveSpeedMult < 1` |
| `Hasted` | `MoveSpeedMult > 1` (SubCore 범위 효과) |
| `Weakened` | `AttackChanceMod < 1`, `DefenseChanceMod < 1` |
| `CorruptionGauge` | 회복 불가 2차 피통 (Dark Poison 누적) |

- `ApplyEffect(type, duration, strength)` — 동일 타입이면 duration/strength 갱신
- `AddCorruption(amount)` — 부패 게이지 누적

## Kenshi-style XP System (`CombatXp.cs`)

전투/노동 스킬 성장에 켄시 RPG 방식의 XP 보정 적용. 모든 성장 호출은 `UnitStats.GrowStat()`을 통과 (IsLocked 미니언 차단).

### 3가지 보정 곱

```
최종 XP = baseAmount × ChallengeMult × Sol × OutnumberedMult × eventMult
```

**`ChallengeMult(mySkill, oppSkill)`** — 상대 스킬 차이 배율:
- 상대보다 29.25 위 → 0.1x (최소)
- 동급 → 1.0x
- 상대보다 65 아래 → 6.0x (최대)
- 선형 보간

**`Sol(mySkill)`** — 후반 둔화: `(1 - skill/131)²`
- 레벨 10 (F+) ≈ 0.85x
- 레벨 50 (C+) ≈ 0.38x
- 레벨 100 (S+) ≈ 0.05x
- 레벨 120 (SSS-) ≈ 0.006x

**`OutnumberedMult(enemyCount)`** — 수적 열세 보너스: `1 + (n-1) × 0.1`, 캡 1.666x at 1v8+. `CountNearbyEnemies(self, radius=6f)`로 자동 카운트.

### CombatHandler 이벤트별 XP 배율

| 상황 | 방어자 | 공격자 |
|---|---|---|
| Block 성공 | 방어 스킬 1.0x | 무기 0.5x, MeleeAttack 0.25x |
| Dodge 성공 | Evasion 1.0x | 0 (히트박스 미접촉) |
| 피격 (적중) | 방어 8.0x + Evasion 2.0x | 무기 1.0x, MeleeAttack 0.5x |
| 원거리 Block | RangedDef 1.0x | 무기 0.5x |
| 원거리 적중 | RangedDef 8.0x | 무기 1.0x |

### 노동/제작/연구 적용

- **`BTLeafNodes` WorkNode** — Mining/Cutting 성장. `node.Data.RequiredSkill`을 도전 대상으로 사용. `0.05 × deltaTime × ChallengeMult × Sol`
- **`CraftJob`** — `_recipe.SkillGrowthAmount × ChallengeMult(skill, requiredSkillLevel) × Sol`
- **`ResearchJob`** — `SkillGrowthAmount × Sol` (도전 대상 없음)
- **`PawnMover` Athletics** — `growthRate × deltaTime × Sol` (도전 대상 없음)
- **`CombatHandler` PrecisionShooting** — `growthRate × Sol` (도전 대상 없음)

### ResourceNodeData

`RequiredSkill: float` — 자원별 권장 스킬 레벨 (XP 도전 비율 계산용). 0이면 누구나 같은 속도로 성장.

## Combat System (`CombatHandler.cs`)

`Equipment.MainHand`로 무기 타입 결정. `CombatHandler.Update()`에서 `weapon.IsRanged`로 분기.

**근접 공격 흐름:**
1. Weakened 디버프: attackSkill × AttackChanceMod
2. Block chance = `(defenderMeleeDefense + shield.BlockBonus) / (attackerMeleeAttack + effective)` × DefenseChanceMod
3. Dodge chance = `defenderEvasion / (attackerMeleeAttack + evasion)` × DefenseChanceMod
4. 피격 → `BodyPartSystem.TakeDamage(DamageInfo)`

**원거리 공격 흐름:**
1. `TryFireRanged` — Cognition × (1 − distancePenalty) 정확도 판정
2. `TryBlockRanged` — `(RangedDefense + shield.RangedBlockBonus) / (attackerSkill + effective)` × DefenseChanceMod
3. `FireRanged` — PrecisionShooting 분기: 60~100% 타겟 적중, 0~40% 아군 오사

**데미지 공식:**
```
damage = AttackDamage
       × (1 + weaponSkill / 100)
       × [1 + Dexterity/100 × 0.5]   // IsBladed 무기만
       × [1 + Strength/100 × 0.5]    // 비무장만
       × statusCorrection
       × weightCorrection
```

**공격 인터벌 공식:**
```
interval = weapon.BaseAttackInterval
         / (1 + Dexterity/100)
         / [1 + Strength/100 × 0.5]  // IsLargeWeapon만
         / statusCorrection
         / weightCorrection
         × AttackIntervalMult        // StatusEffectSystem (Slowed 등)
```

**무게 보정:** `maxCarryWeight = Strength × 5`. 초과 시: `weightCorrection = maxWeight / currentWeight`

**상태 보정** (사지 장애):
- RightArm × 0.5 + LeftArm × 0.3 + RightLeg × 0.1 + LeftLeg × 0.1
- 양팔 OR 양다리 모두 장애 → 0

## Body-Part System (`BodyPartSystem.cs`)

7개 부위: Head, Chest, Abdomen, LeftArm, RightArm, LeftLeg, RightLeg

**타격 부위 추첨** (`GetRandomBodyPart`): 균등이 아니라 `BodyPart.HitWeight` 가중치 — 가슴 30 / 복부 20 / 머리 10 / 사지 각 10 (합 100). 몸통 50%, 머리 10%(작은 표적). 부위별 튜닝은 `InitBodyParts`.

| 상태 | 임계값 | Head/Chest/Abdomen | Arm | Leg |
|---|---|---|---|---|
| Disabled | ≤ 0 | TryKnockOut | AttackAbility 갱신 | SetLimping/Crawling |
| Destroyed | ≤ −100 | Die() | TryKnockOut | TryKnockOut |

- `TryKnockOut`: `resistChance = Toughness / 100`. 실패 → `IsKnockedOut = true`
- `TakeDamage(DamageInfo)` 처리 순서:
  1. 타입 배율 (Blunt ×1.3, Lightning ×1.2)
  2. 갑옷 감소 `armor.DamageReduction × qualityMult` (Pierce/Elemental Ice 관통)
  3. Toughness 저항 `/ (1 + Toughness/100)`
  4. 베기 추가 출혈
  5. 상태이상 발동 (EffectChance 확률)

**부위 → 갑옷 슬롯 매핑:** Head→Head, Chest/Abdomen→Chest, Arms→Hands, Legs→Legs

## BloodCap System

`BodyPartSystem.BloodCap` — 자연회복 상한선:
- 침대 없이는 최대 절반까지만 자연회복 가능
- `HealBlood(amount, degradeCap=false)` — `degradeCap=true`이면 BloodCap을 회복량 × 0.5 감소 (포션/마법 힐 시 사용)
- `RestoreBloodCap(amount)` — 침대 치료 시 호출

## ItemQuality → 전투 연동

`Equipment.SyncBlackboard()`: `AttackDamage = BaseDamage × Quality.StatMultiplier()`

`BodyPartSystem.TakeDamage()`: `armor.DamageReduction × GetArmorQualityMult(slot)` 적용

등급 확률 (WeaponCrafting 스킬 100 기준, 고차 다항식 `t^n`):
- 전설 2% / 에픽 5% / 유니크 10% / 희귀 20% / 고급 35% / 일반 28%
- 스킬 미달 시 일반 고정

WeaponData/ArmorData → 등급 판정 + 수량 고정  
그 외 아이템 → 등급 없음 + 수량 배율 (스킬 100 = ×1.5)

제작 완료마다 `RecipeData.SkillGrowthAmount` 만큼 스킬 성장.

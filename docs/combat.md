# Combat System

## Damage Type System (`Combat/`)

**축은 하나다**(2026-08-23 — `DamageCategory` × `DamageElement` 2D 매트릭스를 접었다).

```
DamageType:
  물리  Slash / Pierce / Blunt          ← 갑옷이 막는다
  원소  Fire / Water / Earth / Lightning / Arcane / Dark / Holy   ← 저항이 막는다
```

`DamageTypeExt.IsPhysical()` 하나가 갈래를 답한다. `DamageInfo { Amount, Type, EffectChance,
IgnoreArmor, Source }`.

**물리:**
- `Slash` — 출혈 계수 0.25(가장 높다)
- `Pierce` — 갑옷 두께를 지나간다
- `Blunt` — 피해 ×1.3, 출혈 계수 0.05(부위로 눕힌다)

**원소 — 저마다 하나씩만 한다** (`EffectChance` 확률로 발동):

| 원소 | 피해 | 효과 |
|---|---|---|
| `Fire` | — | `Burning` 5초 5dps. **`Wet`을 말린다** |
| `Water` | — | `Wet` 8초(이동 −10%) |
| `Earth` | — | `TryKnockOut(bonus 0.35)` — 켄시식 KO 굴림에 직접 얹힌다 |
| `Lightning` | ×1.2, **젖었으면 추가 ×1.5** | `Slowed` 8초(경직) |
| `Arcane` | — | **갑옷·저항을 지나간다.** 상태이상 없음 |
| `Dark` | — | `HealReduction` 10초 + `CorruptionGauge` +amount×0.3 |
| `Holy` | **언데드에게 ×2** | 휘두른 쪽 회복 30/부위 10 + 디버프 제거 |

- **독은 원소가 아니다** — 효과다(불이 화상을 주듯). `StatusEffectSystem`이 든다.
- **`Holy` ×2는 저항표가 아니라 `UnitBrain.IsUndead`가 답한다.** 표에만 적으면 **일으켜진
  몸**은 사람 종족이라 표에 없어서 성물이 안 듣는다 — 시체가 일어나는 게임에서 그건 신성을
  넣은 뜻을 통째로 지운다.
- **`Water` 하나로는 약하다 — 전기 증폭이 그것을 산다.** 원소끼리 얽히는 유일한 자리라,
  없으면 물은 색만 다른 감속기다.

### 저항 (`DamageResistance`)

**저항이 없으면 원소는 장식이다** — 무기를 바꿀 이유가 없어진다.

두 곳이 들고 **더해진다**: `ArmorData.Resistances`(입은 것) · `UnitData.Resistances`(타고난 것).
`Multiplier()`가 `1 - Σ`를 **0.1~3.0으로 자른다**(음수 = 취약). 완전 면역을 안 만드는 이유:
화면에서 *"안 맞는다"*로 보여 무엇이 일어나는지 알 수가 없다.

**갑옷의 `DamageReduction`은 물리에만 걸린다** — 원소는 두께가 아니라 재질의 문제다.

저작된 종족 저항:

| 종족 | 저항 |
|---|---|
| 해골 | 베기 0.4 · **타격 −0.35** · 암흑 0.75 · 불 0.2 (+ 신성 ×2는 `IsUndead`) |
| 곰 | 베기 0.2 · 불 −0.3 |
| 늑대 | 전기 −0.25 |
| 고블린 | 암흑 0.25 · 신성 −0.2 |

### 무기

`WeaponData.DamageType` 하나. **마법 무기는 `WeaponType.Staff` 하나**이고 무엇을 쏘느냐는
`DamageType`이 정한다 — 종류로도 가르면 원소를 더할 때마다 `WeaponType`이 같이 는다.
(옛 추론 사다리 셋 `IsMagicWeapon`·`CategoryFromType`·`ElementFromType`은 없어졌다.)

**어느 스탯이 자라나는 `UnitStats.StatFor(WeaponData)` 하나가 답한다** — 지팡이는 `Type`만
봐서는 못 가른다(전부 `Staff`). JSON은 `"damageType"`, 옛 세 키(`damageCategory`·
`physicalType`·`damageElement`)는 `DataManager.MigrateLegacyDamage`가 읽어 옮기고 경고한다.

## Status Effect System (`StatusEffectSystem.cs`)

`UnitBrain.Awake`에서 자동 추가.

| 효과 | 영향 |
|---|---|
| `Burning` | `BodyPartSystem.TakeDamage` DoT 매 프레임 |
| `HealReduction` | `HealMult < 1` |
| `Slowed` | `AttackIntervalMult > 1`, `MoveSpeedMult < 1` (전기의 경직) |
| `Hasted` | `MoveSpeedMult > 1` (SubCore 범위 효과) |
| `Weakened` | `AttackChanceMod < 1`, `DefenseChanceMod < 1` |
| `CorruptionGauge` | 회복 불가 2차 피통 (암흑 누적) |
| **`Wet`** | `MoveSpeedMult` −10%. **`BodyPartSystem`이 `IsWet`을 보고 전기 피해 ×1.5** — 이게 물의 본체다 |

- `IsWet` / `DryOff()` — 불이 물을 말린다(안 그러면 불·전기가 둘 다 잘 드는 공짜 조합).
- `ClearDebuffs()`가 `Wet`도 걷어낸다.

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
| Block 성공 | 방어 1.0x + **Shield 1.0x** | 무기 0.5x, MeleeAttack 0.25x |
| Dodge 성공 | Evasion 1.0x | 0 (히트박스 미접촉) |
| 피격 (적중) | 방어 8.0x + Evasion 2.0x + **Shield 8.0x** | 무기 1.0x, MeleeAttack 0.5x |
| 원거리 Block | RangedDef 1.0x + **Shield 1.0x** | 무기 0.5x |
| 원거리 적중 | RangedDef 8.0x + **Shield 8.0x** | 무기 1.0x |

**`Shield`는 방패를 들고 있을 때만 얹는다**(`GrowShieldXp` → `HasShield`) — 안 그러면 맨몸으로
맞은 폰의 방패 솜씨가 는다. **맞았을 때가 여덟 배인 것이 켄시다**(*두들겨 맞으면서 배운다*).

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
2. Block chance = `(defenderMeleeDefense + shield.BlockBonus × (1 + Shield/100)) / (attackerMeleeAttack + effective)` × DefenseChanceMod
3. Dodge chance = `defenderEvasion / (attackerMeleeAttack + evasion)` × DefenseChanceMod
4. 피격 → `BodyPartSystem.TakeDamage(DamageInfo)`

**원거리 공격 흐름:**
1. `TryFireRanged` — Cognition × (1 − distancePenalty) 정확도 판정
2. `TryBlockRanged` — `(RangedDefense + shield.RangedBlockBonus × (1 + Shield/100)) / (attackerSkill + effective)` × DefenseChanceMod
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
- `TryKnockOut(bonus)` — `bonus`는 넘어뜨리는 쪽의 보정(땅 원소가 0.35). **저항에서 곧장 뺀다** — 강인함을 깎는 식으로 넣으면 같은 값이 사망 문턱 같은 다른 판정에도 새어 나간다.
- `TakeDamage(DamageInfo)` 처리 순서:
  1. 타입 배율 (Blunt ×1.3, Lightning ×1.2) + **젖음 ×1.5**(전기) + **언데드 ×2**(신성)
  2. 보호막 흡수
  3. **물리면** 갑옷 `DamageReduction × qualityMult`(Pierce 관통) / **원소면** 저항 배율
     — `Arcane`은 **둘 다 지나간다**
  4. Toughness 저항 `/ (1 + Toughness/100)`
  5. 출혈 (Slash 0.25 / Blunt 0.05 / 그 밖 0.125)
  6. 상태이상 발동 (EffectChance 확률)

**부위 → 갑옷 슬롯 매핑:** Head→Head, Chest/Abdomen→Chest, Arms→Hands, Legs→Legs

## 방패 스탯 (`StatName.Shield`)

**무기는 종류마다 스탯이 있는데 방패만 없었다**(2026-08-23까지) — `WeaponToStat`에서
`WeaponType.Shield`만 `_ => MartialArts` 폴백에 떨어졌고, 막기 계산은 평탄 `BlockBonus`만
더했다. **그래서 방패는 자라지 않았다**: 천 번을 막아도 나아지지 않고 좋은 방패를 사는 것이
유일한 개선이었다.

**무기와 대칭이다** — 공통 스탯이 굴림, 종류 스탯이 크기:

| | 공통 | 종류 |
|---|---|---|
| 공격 | `MeleeAttack` (맞히나) | `OneHandSword` 등 (얼마나 아픈가) |
| 원거리 | `PrecisionShooting` | `Bow`·`Crossbow`·`Javelin` / **지팡이는 원소 스탯** |
| 방어 | `MeleeDefense` (막나) | **`Shield`** (방패가 얼마나 일하나) |

```
effective = MeleeDefense + BlockBonus × (1 + Shield / 100)
```

- **`MeleeDefense`를 대체하지 않는다** — 그건 무기로 흘리고 몸으로 피하는 것이라 방패가
  없어도 돈다.
- **방패가 없으면 저절로 0이다**(`BlockBonus` 0 × 무엇 = 0) — *"방패를 든 폰만 자란다"*가
  규칙을 따로 만들지 않아도 나온다. 스탯 창에도 **들고 있을 때만** 뜬다.
- **근접·원거리를 안 가른다** — `RangedBlockBonus`도 같은 스탯을 탄다. 가르면 방패 하나에
  스탯이 둘이 되고, 그건 방패를 두 물건으로 만드는 것이다.

## 마법 스탯 — 원소 일곱 (15 → 7)

전에는 계열 셋(`ElementalMagic`·`DarkMagic`·`HolyMagic`) × 원소 넷 = **열다섯**이었다.
**계열 셋은 전투에서 한 번도 안 읽혔고**(`GetMagicCategorySkill`을 부르는 곳이 없었다)
조합 열둘 중 **여덟은 무기가 아예 없었다.**

지금은 `FireMagic` · `WaterMagic` · `EarthMagic` · `LightningMagic` · `ArcaneMagic` ·
`DarkMagic` · `HolyMagic`. **`UnitStats.MagicStatFor(DamageType)`**가 짝을 짓는다.

**`StatName` 정수 자리를 옮겼으므로 에셋도 같이 옮겼다**(45개 파일) — 실제로 저작에 쓰이던
마법 스탯은 옛 `DarkMagic`(19)뿐이었고(교단 성장 배경 다섯) 그건 새 자리(23)에 그대로 산다.
**스탯은 53 → 46개**(마법 −8, `Shield` +1).

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

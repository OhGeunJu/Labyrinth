# Units System

## UnitBrain & UnitBlackboard

`UnitBrain` — BT를 0.1초 틱으로 실행. `IsKnockedOut` 동안 BT 정지.

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

`UnitStats.FromData(data)` — 모든 `StatName`을 순회하며 `data.RollGrade(stat).ToValue()`로 초기화. Min==Max면 고정값. `SourceData`(소속 `UnitData`)도 여기서 세팅.

### 종족별 성장 속도

`UnitData.StatGrowthRates: List<StatGrowthEntry>` — 종족이 **특정 스탯만** 빠르게/느리게 성장. `StatGrowthEntry { StatName Stat; float Multiplier; }`. 목록에 없는 스탯은 기본 1.0.

`UnitStats.GrowStat`에서 `amount *= SourceData.GetGrowthRate(stat)` 적용 → 전투/노동/마법 모든 성장에 자동 반영. 배율 0 = 해당 스탯 성장 차단. 마이너스 성장(퇴화)은 미지원.

예: Human.asset `StatGrowthRates`에 `Mining=1.5`, `MeleeAttack=0.7`만 넣으면 채굴은 빠르게·근접공격은 느리게, 나머지는 기본 속도.

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

### 폰끼리의 충돌 규칙

**같은 레이어(= 같은 편)끼리는 서로 통과한다.** `PawnMover.Awake`가 `CharacterController.excludeLayers = 1 << gameObject.layer`를 건다.

- 이유: 폰 지름이 타일 한 칸과 비슷해 아군이 앞을 막으면 비킬 여유가 없다 → ㄷ자 포위·정면 교착이 여기서 났다. 통과시키면 그 부류가 **원인 채로** 사라진다(림월드 모델).
- 적(다른 레이어)과는 그대로 충돌 — 몸으로 라인을 막는 전술은 살아 있다.
- **유닛은 반드시 전용 레이어(Player/Enemy 등)에 둘 것.** Default(0)는 지면·잡오브젝트가 섞여 있어 제외 규칙을 걸면 바닥을 뚫는다 → `layer == 0`이면 규칙을 적용하지 않는다.
- 길찾기 베이크도 같은 기준(`PawnMover.IsBlockedBy`)을 쓴다 — 물리에서 통과하는 상대를 그래프에서 벽으로 굽으면 헛돈다.

### 지역 회피 & 정체 탈출

길찾기 그래프는 **벽(정적)만** 안다. 다른 폰·침대·창고처럼 그래프에 없는 장애물은 경로가 그대로 관통해서 나오고, `CharacterController`가 막혀 제자리에서 밀기만 한다. 두 겹으로 처리한다.

1. **조향 단계 회피** (`AvoidNearbyPawns`) — `avoidRadius`(1.5) 안, 진행 방향 앞쪽(dot > 0.2)에 있는 다른 폰에서 벗어나는 성분을 더한다. 측면 회피는 **항상 자기 기준 오른쪽**(우측통행) — 마주 오는 둘이 서로 반대 세계방향으로 갈라지므로 "맞대고 아무도 못 감" 교착이 자동으로 풀린다.
   - **예외 2가지**: 교전 상대(`Blackboard.Target`)는 피하지 않는다(붙어야 때린다). 최종 목적지 `arriveDistance × 2` 안에서도 회피를 끈다(도착 지점에 모여야 하는 경우 서로 밀어내면 아무도 도착 못 함).
2. **정체 탈출** (`TrackProgress`/`OnBlocked`) — `blockedCheckTime`(0.35초)마다 실제 이동량이 기대치의 35%에 못 미치면 막힌 것으로 보고 단계적으로: ① 오른쪽으로 **한 번만** 비켜서기(`sidestepTime` 0.6초) → ② 막힌 자리를 `PathBlockMemory`에 기록 + 로컬 재계획 → ③ 기록 + 전역 경로 재계산 → ④ 정지(BT가 다른 행동을 고르거나 다음 틱에 재명령).
   - **좌우로 번갈아 비켜서지 않는다.** U자로 둘러싸인 자리에서 그러면 한쪽으로 갔다 되돌아오는 진동만 반복한다. 우회는 반사적 조향이 아니라 **플래너의 몫**이라는 게 이 구조의 원칙.

**계측** — `MovementDiagnostics`(자동 설치, 씬 작업 불필요):
- 10초마다 집계하되 **실질 문제(2단계 이상)가 있을 때만** 요약을 찍는다. 문제가 없으면 1분에 한 번 생존 신고만. 1단계(스치는 정체)가 몇 번 도는 건 정상 상태다.
- 요약의 차단 셀 수는 **베이크 회당 평균**으로 정규화한다(절대 수치는 베이크 횟수에 비례해 해석 불가).
- 개별 이벤트는 **2단계부터** `[이동진단] {폰} 막힘 N단계(...) @ (x, z)` — 폰당 1초 1줄 제한.
- **포기(4단계)는 `LogWarning` + 상세**: 목표 좌표·거리·로컬경로 유무·목표칸 상태(통행/바닥/차단기억). 못 간 이유가 여기서 갈린다 — 예: `목표칸 점유됨` + `로컬경로 없음` = 목적지 칸이 막혀 경로가 안 나온 것.
- 토글: `DungeonColony → Toggle Movement Diagnostics` / `... (Events Only)`. 플레이 중에도 즉시 반영.
- 용도: 완충장치를 걷어내기 전에 **무엇이 실제로 쓰이는지** 관측으로 판단하기 위한 것. 정리가 끝나면 같이 걷어낼 수 있다.

**목적지는 물체 중심이 아니라 접근 지점으로 줄 것.** 중심을 주면 콜라이더에 정면으로 처박혀 굳는다. 작업은 `WorkNode.GetAdjacentPassable`, 침대는 `Bed.ApproachPosition(from)`(네 면 중 설 수 있고 가장 가까운 쪽)을 쓴다.

## Faction System

`FactionData` (SO, `Create → DungeonColony → FactionData`) — 세력 1개 정의. **관계는 방향별**이다.

| 필드 | 의미 |
|---|---|
| `FactionName` | 표시명 |
| `PlayerControlled` | 플레이어 조작 세력 여부 (선택/명령/우선순위/허기/시체화 분기의 기준) |
| `DefaultStance` | 목록에 없는 세력을 대하는 기본 태도 (`Ally`/`Neutral`/`Hostile`) |
| `Relations` | 상대별 예외 목록 `{Other, Stance}` — 같은 상대가 여럿이면 첫 항목 |

- `StanceTo(other)` — 목록 → (자기 자신이면 Ally) → `DefaultStance` 순으로 결정.
- **방향별**: `Bandit→Villager = Hostile`, `Villager→Bandit = Neutral`이면 산적만 선공하고 마을사람은 도망만 간다.
- `Faction` 컴포넌트: `Data`(SO) + 레거시 `factionType`(Player/Enemy) 폴백. **한쪽이라도 SO가 없으면** 레거시 규칙(플레이어 편 여부가 다르면 적대)으로 판정 → SO를 안 붙인 기존 씬은 동작 그대로.
- `IsPlayer` / `StanceTo` / `IsHostileTo`(선공 여부) / `IsAllyOf`(같은 편).

**반격은 관계와 무관** — `UnitBlackboard.LastAttacker`(+`RetaliationMemory` 15초). `BodyPartSystem.TakeDamage`가 `DamageInfo.Source`를 기록하고(아군 오사는 제외), `DetectNode`는 주변 탐색보다 **반격 대상을 우선**한다. `NpcFleeNode`도 때린 상대를 위협으로 본다. → 중립 세력을 선공하면 그쪽이 대응한다.

**스타터 에셋**: `Assets/5_Data/Faction/`에 플레이어/고블린/산적/마을사람 4종 + 관계 기입(고블린은 기본 적대·산적만 예외, 산적→마을사람 일방 적대). 각 유닛 프리팹의 `Faction.Data`에 연결해 쓴다.

### AI 프로필 (`AiProfileData` SO)

**세 축 분리** — 종족(`UnitData`, 스탯) / 세력(`FactionData`, 누가 적인가) / **역할(`AiProfileData`, 어떻게 반응하는가)**.
성향·배회는 종족 속성이 아니므로 별도 SO다 — 같은 "사람" 종족·같은 마을 세력이어도 마을사람은 겁쟁이 프로필, 경비병은 전투 프로필을 쓴다. **연결은 유닛 프리팹의 `UnitBrain.aiProfile`** (컴포넌트를 새로 붙일 필요 없음). 프로필 하나를 여러 프리팹이 공유한다.

| `Stance` | 행동 |
|---|---|
| `Fight` | 교전 |
| `Flee` | 항상 도주 — 교전 노드 자체가 트리에 없음 |
| `FightWhileHealthy` | 교전하다 혈액이 `FleeBloodRatio`(기본 0.4) 아래면 도주 |

- 전투 필드: `FleeDistance`(15, 한 번에 물러나는 거리), `FleeDetectRange`(0 = 유닛 `DetectRange` 또는 NPC `FleeDetectRange`).
- 유휴 필드: `Wander`(배회 on/off), `WanderRadius`(0 = 타입 기본), `PauseMin/Max`(도착 후 대기 2~6초).
- **프로필이 비면 역할별 기본값**: 일반 유닛·경비 = 교전+배회 / NPC 주민 = 도주+배회 / 상점 상인 = 도주+제자리. → 기존 프리팹은 안 건드려도 동작 그대로.
- **스타터 에셋**: `Assets/5_Data/AiProfile/`에 겁쟁이 민간인 / 전투원 / 신중한 전투원(다치면 후퇴) / 보초(제자리) 4종.
- 트리 조립은 `UnitBrain.AddCombatBranch(root, fallback, fleeRange)` + `AddWanderBranch(root, defaultRadius, defaultOn)` — 적/경비/주민/상인 트리가 공유한다.

**성향 우선순위**: 런타임 오버라이드 > AI 프로필 > 역할 기본값. 성향은 **매 틱 읽으므로** 런타임 변경이 즉시 반영된다(BT 재조립 불필요).

```csharp
brain.SetStanceOverride(CombatStance.FightWhileHealthy);  // 이 유닛만 "다치면 후퇴"
brain.ClearStanceOverride();                              // 프로필/기본값으로 복귀
brain.EffectiveStance                                     // 현재 실제 적용값
```

**인게임 전환**: `ColonyUI`(P키) 성향 열 — 버튼 클릭마다 자동(프로필/기본값) → 교전 → 신중 → 도주 → 자동 순환. "자동"은 오버라이드 없음(`ClearStanceOverride`) 상태.

**직접 명령 우선**(`ManualOrderNode`, 방랑자 트리에서 소집 바로 아래): 우클릭 이동 명령은 `bb.ManualMove`를 켜고 **`bb.Target`을 지운다**(교전 이탈 + `CombatHandler.ExitCombat`). 이게 없으면 `FightNode`가 매 틱 목적지를 적 위치로 덮어써 **명령해도 다시 싸우러 간다**. 도착하거나 길찾기가 포기해 멈추면 자동 해제되어 평소 행동으로 복귀한다. 적을 우클릭한 공격 명령은 반대로 `ManualMove`를 끈다.

**이동 명령 = 하던 작업 포기**(`UnitBrain.AbandonWork()`): 큐 일감 취소 + 노드 직접 배정 해제. 배정을 남기면 도착하자마자 다시 일하러 가서 명령이 안 먹는 것처럼 보인다. `Die()`도 같은 경로를 쓴다. **침대 배정은 유지** — 작업이 아니라 "쉬는 중"이라는 지속 명령이라서(패널에 상태로 표시 + 해제 버튼).

취소는 각 Job의 `OnCancelled`가 뒷정리한다 — 제작/연구는 **재료 환불 + 주문 재등록**, 운반은 **들고 있던 짐을 그 자리에 드롭**(→ `GroundHaulJob` 자동 등록), 건설은 `OnJobEnded → TryRegisterJob`으로 즉시 재등록. 새 Job 타입을 만들 때 이 뒷정리를 빠뜨리면 **취소할 때마다 자원이 증발한다.**

**플레이어 유닛(방랑자/하수인)**: 트리에 도주 분기(`PlayerFleeNode`)가 들어 있지만 **기본 성향이 `Fight`라 평소엔 즉시 실패** → 기존 동작과 동일. 프로필을 붙이거나 `SetStanceOverride`를 호출하는 순간부터 도주가 켜진다. **소집(Draft) 중에는 도망치지 않는다** — 플레이어 명령이 우선. NPC/적은 프로필로 고정 운용, 플레이어는 상황에 따라 전환하는 용법.
- 도주 트리거는 **상대가 나를 적대할 때**(`f.IsHostileTo(me)`) + **나를 때린 상대**(반격 기억). 내가 상대를 어떻게 보든 무관. 가장 가까운 위협 기준.
- **계속 달아난다**: 도주를 시작하면 감지 범위의 **2.5배**까지 위협을 추적(히스테리시스) — 경계에서 멈췄다 다시 뛰는 딸꾹질 방지. 위협이 완전히 사라지면 정지.
- **막다른 곳 대응**: 반대 방향이 벽·공허면 좌우 부채꼴(±25~110°)로 설 수 있는 지점을 찾고, 다 막히면 거리를 줄여 재시도. 0.3초마다 또는 멈춰 섰을 때 목적지를 갱신한다.
### BT 시간 축 — `UnitBlackboard.TickDelta`

BT는 `UnitBrain.tickInterval`(기본 0.1초)마다 돈다. **노드 안에서 `Time.deltaTime`을 누적하면 안 된다** — 한 프레임(≈0.016초)만 더해져 시간이 6배 이상 느리게 흐른다. 시간을 다루는 노드는 `bb.TickDelta`(그 틱까지 실제 경과 시간)를 쓸 것. 2026-07-27에 전수 교체(식사/침대 회복/노동 XP/순찰 대기/배회 대기/행상인 체류).

### 유휴 배회

할 일이 없을 때 제자리에 서 있지 않고 주변을 배회한다(**비플레이어 유닛만** — 플레이어 유닛은 작업 BT가 유휴를 채운다). 설정은 위 AI 프로필의 유휴 필드.

- **프로필이 없을 때 기본값**: 적 = 배회(반경 12), NPC 주민·경비 = 배회(`NpcData.WanderRadius`), **상점 상인 = 제자리**(가게를 지켜야 하므로).
- 트리 위치: 성향(전투/도주) → (경비는 순찰) → **배회** → 대기. 앞 분기가 실패할 때만 돈다.
- **홈 기준점**: NPC는 스폰 위치(`BuildNpcTree`에서 설정). 그 외 유닛은 `UnitBlackboard.HomeSet`이 false라 **처음 배회하는 순간의 위치**를 홈으로 잡는다 → 침공 유닛은 집결지에 도착해 멈춘 자리를 중심으로 배회한다.
- `WanderNode`는 **이동 중이면 목적지를 새로 주지 않는다** → 스폰 직후 받은 이동 명령(침공 집결 등)을 덮어쓰지 않는다.
- 배회가 필요한 민간인은 `NpcData` + `NpcType.Villager`(도주→배회→대기)가 여전히 가장 간단하다.

**AI 역할 판정**: `UnitBrain.UsesEnemyAi` = `NpcData/MinionData가 아님 && !IsPlayer` → 적 BT + 사망 시 시체화. 세력이 늘어나도 플레이어 편만 아니면 자동으로 적 AI.

## Hunger System

`UnitBlackboard.Hunger` — 0~100. 플레이어 세력(`Faction.IsPlayer`) 유닛만 감소.

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
- `TryOccupy()` / `Vacate()` — 점유 관리 (**같은 유닛의 재점유는 허용** — 전투로 자리를 떴다 돌아올 때 막히면 안 됨)
- `Assign(unit)` / `Unassign()` — 배정 관리. **배정 = "이 폰은 쉬는 중"이라는 명령**
- `SleepPosition` / `SleepRotationFrom(approachPos)` — 누울 위치·자세. 위치는 **렌더러 바운즈 윗면 중앙 + `lieHeight`(0.35)**, 자세는 **폰이 다가온 면(침대 로컬 ±X/±Z 중 가까운 쪽)에 맞춰 네 방향** 중 하나(머리가 들어온 쪽 반대편). `sleepAnchor`(Transform)를 연결하면 그 지점·회전으로 고정
- 클릭 시 `OnSelected` 이벤트

**`RestNode` (BT)** — **배정된 침대가 있으면 그 침대로 가서 눕는다. 누워 있는 동안 일하지 않고 회복만 한다.**
- 회복이 끝나도 일어나지 않는다(배정이 곧 명령). **해제해야 다시 일하러 간다.**
- 일어나는 조건: 배정 해제 / 허기 60 이하(먹고 돌아옴) / 상위 분기(소집·전투·도주)가 가져감
- 회복량: 혈액 2/s, BloodCap 1/s, 부위 HP 1/s (캡 감소 없음)
- BT 위치: Fight → **Rest** → Eat → Work

**눕기 구현** — `PawnMover.SetSleeping(on, pos, rot)`:
- 누울 때 `CharacterController`를 끄고 위치·회전을 침대에 고정한다(중력·조향이 계속 돌면 침대에서 미끄러지거나 파묻힌다).
- 일어날 때는 **눕기 직전 서 있던 자리**로 되돌린다 — 침대 콜라이더 위에서 깨어나 끼는 것 방지.
- `SetMoveTarget`은 자동으로 깨운다(이동 명령 = 기상).
- BT 노드에는 중단 콜백이 없으므로, 휴식이 상위 분기에 밀렸을 때는 `UnitBrain.Update`가 `bb.RestTicked`를 보고 일으켜 세운다.

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

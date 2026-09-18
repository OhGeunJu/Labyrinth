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

`StatName` enum — **46개** 스탯 식별자(2026-08-23: 마법 15 → 원소 7, `Shield` 신설). `UnitStats.GetStat(StatName)` / `SetStat(StatName, float)` / `GrowStat(StatName, float)` / `GetGrade(StatName)`로 통합 접근.

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
| **전투 기술** | MeleeAttack, MeleeDefense, RangedDefense, PrecisionShooting, **Shield** |
| **근접 무기** | OneHandSword, OneHandAxe, OneHandMace, TwoHandSword, TwoHandAxe, TwoHandMace, Polearm |
| **원거리 무기** | Bow, Crossbow, Javelin |
| **마법 원소** | FireMagic, WaterMagic, EarthMagic, LightningMagic, ArcaneMagic, DarkMagic, HolyMagic |
| **신체 기술** | MartialArts, Evasion, Athletics, Swimming |
| **노동** | Labor, Mining, Growing, Cutting, Taming, Cooking, Medicine, Research, Social, WeaponCrafting, ArmorCrafting, Alchemy, Smelting, Stonecutting, Woodworking, Gemcutting |

`UnitStats` 헬퍼:
- **`StatFor(WeaponData)`** — 그 무기가 쓰는 스탯. **무기 종류가 아니라 무기를 받는다** — 지팡이는 전부 `WeaponType.Staff`라 `Type`만 보면 불과 암흑이 같아진다(`MagicStatFor(DamageType)`가 짝을 짓는다). 방패도 여기서 `StatName.Shield`로 답한다(전에는 `_ => MartialArts` 폴백에 떨어져 **자라지 않았다**)
- `GetSkillFor(WeaponData)` / `GrowSkillFor(WeaponData, amount)` — 위를 통해 읽고 키운다
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

`WeaponData` 주요 필드: `WeaponType`, `BaseDamage`, `BaseAttackInterval`, `AttackRange`, `IsLargeWeapon`, `IsBladed`, `IsRanged`, `BlockBonus`, `RangedBlockBonus`, **`DamageType`**, `EffectChance`

`WeaponType` enum: Unarmed, Shield, OneHand/TwoHand 근접, Polearm, Bow/Crossbow/Javelin, **Staff**(마법 열둘을 하나로 — 무엇을 쏘느냐는 `DamageType`이 든다)

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

**반격은 관계와 무관** — `UnitBlackboard.LastAttacker`(+`RetaliationMemory` 15초). `BodyPartSystem.TakeDamage`가 `DamageInfo.Source`를 기록하고(아군 오사는 제외), `DetectNode`는 주변 탐색보다 **반격 대상을 우선**한다. `FleeNode`도 때린 상대를 위협으로 본다. → 중립 세력을 선공하면 그쪽이 대응한다.

**스타터 에셋**: `Assets/Resources/Data/Factions/`에 플레이어/고블린/산적/마을사람 4종 + 관계 기입(고블린은 기본 적대·산적만 예외, 산적→마을사람 일방 적대). 각 유닛 프리팹의 `Faction.Data`에 연결해 쓴다.

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

**AI 역할 판정**: `UnitBrain.UsesEnemyAi` = **`NpcData`가 없고 && !IsPlayer** → 적 BT. 세력이 늘어나도 플레이어 편만 아니면 자동으로 적 AI. **`MinionData`인지는 안 묻는다**(2026-08-07) — 같은 종족이 소환물일 수도 야생일 수도 있고, 그걸 가르는 건 세력이다. 시체화 판정도 역할이 아니라 세력이 정한다.

## Needs — 몸이 요구하는 것 둘 (허기 · 피로)

**2026-08-20에 `Needs` 한 벌로 모았다.** 그전에는 허기 하나가 `UnitBrain.UpdateHunger`·
`BaseHungerDecayRate`·`EatNode`·포로 급식·살펴보기 창 여섯 군데에 흩어져 있어서, 축이 하나
늘 때마다 그 여섯이 통째로 복사됐다. **`UnitBrain.UpdateHunger`·`BaseHungerDecayRate`는 없다.**

| 축 | 100→임계(40) | 임계→가득 | 100→0 | 바닥에서 |
|---|---|---|---|---|
| 허기 | 540초(하루 3.3끼) | 먹으면 즉시 | 900초 | 혈액 2/s → 65초면 죽는다 |
| 피로 | 1200초(하루의 2/3) | 침대 600초 / 바닥 1200초 | 2000초 | 안 죽는다 — 그 자리에 쓰러져 잔다 |

- **눈금은 하루(`GameTimeManager.DayDuration`)에서 나온다** — 초당 감소량을 상수로 박으면
  하루 길이를 바꿨을 때 안 따라온다. "하루에 몇 번"으로 적고 거기서 나눈다.
- **저장은 한 벌** — `UnitBlackboard`가 `float[]`을 들고 `Hunger`/`Rest`는 그것을 가리키는
  별명이다. 축을 도는 코드는 `bb.Need(kind)`/`SetNeed`.
- **누가 도는가는 `Needs.Simulates(brain, kind)`가 답한다** — 플레이어 세력 + 갇힌 사람,
  업힌 동안은 아무도 안 돈다. **갇힌 사람은 피로가 안 돈다**(잠은 대신 자 줄 수 없다).
- **핵 보정(`HungerRateModifier`)은 허기에만** 건다.
- 임계는 `Needs.Threshold`(**40**), 깨는 값은 `WakeAt`(100). 상세는 CLAUDE.md.

**`FoodData : ItemData`** (`Item/FoodData.cs`): `NutritionValue` / `EatTime` / `MaxStack = 20`.

**무엇을 먹는지는 `Feeding`이 답한다** — 먹는 길이 둘이라(제 발로 `EatNode` / 남이 먹여 주는
`FeedJob`) 양쪽에 적으면 갈린다. **가장 가까운 창고부터**, 그 안에서 **넘치지 않는 가장 큰 것**.

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
- **누가 배정했느냐가 배정의 뜻을 가른다**(`Bed.AutoClaimed`): 손으로 붙였으면 *"가서 쉬어라"*라는 명령이라 **뗄 때까지 눕고**(다친 동료를 눕히는 길), 졸린 폰이 저 혼자 잡았으면 **가득 채우면 일어난다**.
- 일어나는 조건: 배정 해제 / 허기가 임계(40) 아래(먹고 돌아옴) / 피로가 `WakeAt`(100) — 저 혼자 잡은 침대만 / 상위 분기(소집·전투·도주)가 가져감
- **가는 문턱과 나오는 문턱이 다르다**(40 → 100). 하나로 두면 문턱 위에서 떨며 침대를 들락거린다.
- 회복량: 혈액 2/s, BloodCap 1/s, 부위 HP 3/s (캡 감소 없음) — 부위만 몸의 눈금(31→100)과 같이 올렸다
- **자는 동안에는 피로가 안 준다**(`Needs.Decays`) — 안 그러면 바닥잠은 감소와 상쇄돼 영영 안 찬다
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

**`NpcData`** (ScriptableObject, `DungeonColony/NpcData`) — **역할이지 종족이 아니다.** 2026-08-06까지 `UnitData`를 상속해 종족 데이터를 겸했는데, 그러면 "주민"이라는 종족이 있는 꼴이라 종족을 늘릴 때마다 역할마다 이름 풀·스탯을 복붙해야 했다(종족 × 역할의 곱셈). 지금은 `UnitBrain`이 `unitData`(종족)와 `npcData`(역할)를 **따로** 들고 프리팹이 둘을 조합한다.
- `NpcType` — Villager, Guard, ShopMerchant, EventMerchant
- `WanderRadius`, `FleeDetectRange`, `StayDuration`, `ArrivalOffset`
- `MerchantData` — **역할의 기본 상품일 뿐이다.** 프리팹의 `Merchant` 컴포넌트에 물린 것이 이긴다(`UnitBrain.EnsureMerchant`).

**NpcType별 BT:**

| NpcType | BT |
|---|---|
| Villager | (성향) → Wander → Idle |
| Guard | (성향) → Patrol → Wander → Idle |
| ShopMerchant | 상품 연결 → (성향) → Idle. 기본 제자리 |
| EventMerchant | 상품 연결 → WalkIn → Stay(StayDuration) → WalkOut → Despawn |

**상인 둘 다 `Merchant`를 붙인다** — 2026-08-07까지 이벤트 상인은 안 붙여서 클릭해도 아무것도 안 열렸다. 오래 머물지 않는 것이 그 상인의 성질이지 물건이 없다는 뜻이 아니다.

**BT 노드 (`BTNpcNodes.cs`):**
- `FleeNode(detectRange, fleeDistance, 조건)` — 적 감지 시 반대 방향으로 이동. 조건은 매 틱 성향을 읽는다
- `WanderNode(radius, pauseMin, pauseMax)` — HomePosition 중심 랜덤 배회. **이동 중인 목적지는 안 덮는다**(외부 명령 보존)
- `NpcEventMerchantNode(stayDuration)` — 이동→대기→귀환→소멸 단계 처리

**`NpcEventMerchantNode` 내부 동작:**
- Phase 0 (이동): `_moveSent` 플래그로 `SetDestination` 최초 1회만 호출. 거리 < 2f이면 Phase 1로 전환
- Phase 1 (대기): `NpcTimer`가 `stayDuration` 초과 시 HomePosition으로 출발 후 Phase 2
- Phase 2 (귀환): `!IsMoving()`이면 GameObject 소멸

> 주의: Phase 0 도착 판정은 `!IsMoving()`이 아닌 거리 체크. `IsMoving()` 기반이면 경로 초기화 전 첫 틱에 즉시 Phase 1로 넘어가는 버그 발생.

`UnitBlackboard.HomePosition` — NPC 귀환 기준점 (스폰 위치)
`UnitBlackboard.NpcDestination` — EventMerchant 목적지 (던전 코어 근처 랜덤 오프셋)
`UnitBlackboard.NpcTimer` / `NpcPhase` — EventMerchant BT 내부 상태

---

# Needs 상세 — CLAUDE.md에서 내려온 것 (2026-08-27)

`CLAUDE.md`의 "몸이 요구하는 것 둘"에 표와 어기기 쉬운 규칙이 남아 있다.
(위 "Needs — 몸이 요구하는 것 둘" 절과 겹치는 부분이 있으면 아래가 더 자세한 쪽이다.)

## 몸이 요구하는 것 — `Needs` (허기 · 피로 · 에너지 · 사기)

### 몸이 축을 정한다 (2026-09-04)

*"골렘 하수인 허기가 있는게 이상함"*(사용자). 그전에는 `Needs.Simulates`가 **"플레이어
세력인가"** 하나만 물어서, 내 편이면 무엇이든 허기·피로가 돌았다 — 골렘이 밥을 먹으러
창고에 가고 침대에 누웠고, **창고가 비면 돌덩이가 굶어 죽었다.**

| 몸 | `BodyKind` | 요구하는 것 | 바닥에서 | 채우는 자리 |
|---|---|---|---|---|
| 산 몸 | `Living` | 허기 · 피로 | 굶어 죽는다 / 쓰러져 잔다 | 음식 · 침대 |
| 골렘 | `Construct` | **에너지** | **일·순찰·배회가 멎는다** | 마력 충전대(마력석) |
| 해골 | `Undead` | **사기** | **무너진다**(혈액 2/s) | 망자의 제단(뼈) |

- **답하는 문은 `Needs.BodyOf`/`Runs` 하나**이고 `Simulates`가 그것을 부른다. 밖에서
  `IsConstruct`를 다시 물어 축을 가르지 말 것 — 몸이 늘 때 그 자리가 뒤처진다.
- **`IsUndead`와 `IsConstruct`는 다른 물음이다.** 저쪽은 *한 번 죽은 것인가*(신성 특효 ·
  기절 없음 · 산 채로 못 잡음), 이쪽은 *애초에 생물이 아닌가*. 골렘은 죽은 적이 없으므로
  성물에 특히 약할 이유가 없다 — 그래서 두 값을 합칠 수 없다. `BodyOf`는 **언데드를 먼저**
  본다(둘 다 참인 에셋이 하나 생기는 날 답이 자리에 따라 갈리지 않게).
- **비대칭이 곧 성격이다.** 골렘은 기계라 멈출 뿐이고 해골은 붙들어 둔 의지라 놓치면
  흩어진다. 골렘은 비싸고 안전하며, 해골은 싸고 손이 간다.
- **에너지·사기 눈금**: 하루 한 번 바닥나는 값(`ChargesPerDay` 1)이라 임계까지 1,080초 —
  **하루 1.67번** 채우러 간다(밥은 3.3번). **한 번에 가득 찬다**(`Recharge.Fill`) — 음식처럼
  영양이 갈리지 않으므로 임계~가득 사이를 재는 `Band` 셈이 필요 없다. 채우는 데 6초
  (밥 2초보다 긴 것은 **그 자리를 차지하고 서 있는 것**이 이 축의 값이기 때문이다).
- **갇힌 사람은 허기만 돈다**(전에는 "피로만 안 돈다"였다). 충전대와 제단은 갇힌 채로는 갈
  수가 없고, 특히 **사기를 안 막으면 가두는 것이 곧 처형이 된다.** 딸려 오는 것: **골렘과
  해골은 굶겨서 회유할 수 없다** — 도는 축이 없다.
- **`Feeding.Simulates`는 허기 축을 묻는다.** 안 좁히면 *무엇이든 요구하는 폰인가*라
  **간수가 돌덩이에게 밥을 나른다.**

### 채우는 자리 — `Recharger` / `Recharge` / `RechargeNode`

- **건물은 하나이고 저작이 둘이다**(`fills` 축 + `fuel` 재료). 나누면 *가장 가까운 것 찾기 ·
  재료 세기 · 소모 · 채우기*가 두 벌이 되고, 몸이 하나 더 늘 때 세 벌이 된다.
- **재료는 창고에서 바로 뺀다**(`MaterialPool`, 화장터와 같은 길 — 배낭을 먼저 본다).
- **재료는 그 하수인이 남기는 것으로 골랐다** — 뼈는 해골이 죽으면 나오므로 *뼈 → 연구 →
  해골 → 또 뼈*의 고리가 닫히고, 마력석은 광맥에서만 나오므로 **골렘은 광산이 곧 유지비**다.
- **유지 시설은 그 하수인과 같은 연구로 열린다** — 해골이 `뼈 세공`이면 제단도 그 연구.
  어긋나면 **부를 수는 있는데 반드시 죽는 하수인**이 된다. 새 하수인을 넣을 때 같이 볼 것.
- **`RechargeNode`는 전투·명령 아래, 순찰·일감 위다.** *"멈춘다"*는 **`Running`을 돌려주는
  것**으로 낸다 — BT를 끄면 **재료가 생겨도 제 발로 못 가는 데드락**이다.
- **`BuildingDormancy`에 넣었다** — 안 넣으면 핵 영역 밖 충전대가 후보로 남아 골렘이 지도
  반대편으로 걸어간다.
- **`NeedKind`는 꼬리에 붙인다** — 블랙보드가 `(int)kind`로 색인하는 `float[]`이라 중간에
  끼우면 산 폰의 허기 칸에 에너지가 들어간다.

### 산 몸의 두 축

**둘은 기계적으로 같은 물건이다**: 0~100에서 저절로 줄고, 임계(`Threshold` **40**) 아래면 폰이 채우러 가고,
바닥에 닿으면 몸이 상한다. 갈리는 것은 **눈금과 채우는 방법**뿐이다. 허기 하나뿐이던 시절에도
그 규칙이 여섯 군데에 흩어져 있었으므로(블랙보드·감소·임계·`EatNode`·포로 급식·살펴보기 창)
축이 늘 때마다 그 여섯이 통째로 복사된다 — 그래서 `Needs` 하나로 모았다(2026-08-20).

| 축 | 100→임계(40) | 임계→가득 | 100→0 | 바닥에서 |
|---|---|---|---|---|
| 허기 | **540초**(하루 3.3끼) | 먹으면 즉시 | 900초 | 혈액 2/s → **65초**면 죽는다 |
| **피로** | **1200초**(하루의 2/3) | **침대 600초 / 바닥 1200초** | 2000초(1.11일) | **안 죽는다 — 그 자리에 쓰러져 잔다** |

**피로의 눈금은 0~100이 아니라 임계(40)~가득(100) 사이다.** 폰은 바닥이 아니라 **임계에서 자러
가고 가득 차면 일어나므로**, 0~100으로 재면 실제 주기가 그 40%로 줄어 **하루에 두세 번 눕는다**
(4분 자고 8분 일한다). 지금은 **1200 + 600 = 1800초, 정확히 하루 한 번**이고 그 600초가
어두운 때(밤+새벽)와 같은 길이다. **그래서 임계를 옮겨도 피로 주기는 안 바뀐다** — 갈리는 것은
여유뿐이다(임계 60→40으로 내리면서 쓰러지기까지가 1.67일 → 1.11일, 밤새 바닥잠이 20 → 30점).
문턱을 내린 진짜 이유는 **허기**다: 60은 절반도 안 쓰고 움직이는 값이라 **하루 다섯 번** 창고를
왕복했는데, 그건 "하루 2끼"라고 적어 둔 눈금과 너무 멀다. 허기는 이 셈이 다르다 — 굶어 죽는 자리가 실제로 있어
0까지 쓰는 눈금이고, 임계는 *먹으러 갈까*만 정한다(그래서 실제로는 더 자주 먹으러 간다).

- **눈금은 하루(`GameTimeManager.DayDuration`)에서 나온다** — 초당 감소량을 상수로 박으면 하루
  길이를 바꿨을 때 안 따라온다. "하루에 몇 번"으로 적고 거기서 나눈다.
- **피로 1200초는 하루의 2/3고, 남은 600초는 어두운 때(밤+새벽 8시간)와 정확히 같다** — 잘
  굴러가는 콜로니는 저절로 *밤에 자고 낮에 일하는* 모양이 된다. 밤이 견디는 시간이 아니라
  **자는 시간**이 되는 자리고, `UnderworldLighting`이 어둡게 하는 그 시간이다.
- **저장은 한 벌이다** — `UnitBlackboard`가 `float[]`을 들고 `Hunger`/`Rest`는 그것을 가리키는
  **별명**이다. 축을 도는 코드는 `bb.Need(kind)`/`SetNeed`를 쓴다.
- **누가 도는가는 `Needs.Simulates` 하나가 답한다**(플레이어 세력 + 갇힌 사람, 업힌 동안은
  아무도 안 돈다). **축마다 다시 묻는다** — **갇힌 사람은 피로가 안 돈다**: 먹이는 것은 남이
  해 줄 수 있지만(`FeedJob`) **잠은 대신 자 줄 수 없고**, 갇히면 BT가 안 돌아 `RestNode`도
  안 돈다. 채울 길 없는 바는 관리할 것이 아니라 **화면의 거짓말**이다. 살펴보기 창도 같은
  답을 쓴다(안 도는 축은 줄 자체가 안 뜬다).
- **핵 보정(`HungerRateModifier`)은 허기에만 건다** — *"이 핵의 하수인은 덜 먹는다"*는 뜻으로
  저작된 손잡이라, 피로까지 조용히 따라 움직이면 저작한 사람이 모르는 채 둘이 바뀐다.
- **한때 갈증이 셋째 축이었다**(2026-08-20에 넣었다 같은 날 걷어냈다). 걷어낸 이유는 눈금이
  아니라 **하는 일이 허기와 같아서**다: 둘 다 *창고에서 소모품을 찾아 먹기*이고 우물이 서면
  물은 사실상 무한이라, **축은 둘인데 플레이어가 내리는 결정은 늘지 않았다.** 설계(마실 것을
  `FoodData.WaterValue`로 두는 법, 우물 갈래, 간수가 떠다 먹이는 2-phase)는 그날 worklog에
  통째로 남아 있다 — **되살릴 때 잃는 것이 없으므로 코드는 지웠다.**

### 잠 — **누가 배정했느냐가 배정의 뜻을 가른다** (`Bed.AutoClaimed`)

- **손으로 붙였으면 "가서 쉬어라"라는 명령이다** — 안 졸려도 눕고 **뗄 때까지 눕는다**(옛 규칙
  그대로). 그것이 **다친 동료를 눕히는 길**이다.
- **졸린 폰이 저 혼자 잡았으면 그냥 제 잠자리다** — 졸리면 가고 **가득 채우면 일어나** 일하러
  간다. 배정은 남아서 다음에도 그 침대로 간다.

**안 가르면 둘 중 하나가 깨진다.** 늘 눕게 두면 침대를 준 폰이 하루 종일 누워 있고, 피로만 보게
두면 **배정해도 안 눕는다** — 2026-08-20에 실제로 후자를 만들었다(*"침대에 배정하면 눕지도
않고"*). 그때 *"배정은 소유고 눕는 시점은 몸이 정한다"*고 적었는데, 그 한 문장이 **다친 폰을
눕히는 길을 통째로 없앴다.**

- **침대에서 일으킬 때 배정을 거둘지도 그 축이 정한다**(`Bed.RouseFrom` — 이동 명령이 부른다):
  손으로 붙인 것은 거두고(그 명령을 덮어쓰는 것이다), **저 혼자 잡은 것과 소집 중인 폰의 것은
  남긴다**. 점유와 자세는 늘 푼다. 그냥 `ReleaseFrom`을 부르면 **한 번 옮겼다는 이유로 잠자리를
  잃는다**(자원 노드에서 겪은 것과 같은 사고).
- **점유를 잃으면 `RestNode._bed`를 놓는다** — 그 필드가 히스테리시스의 기준이라(*"이미 누우러
  갔으면 가득 찰 때까지"*) 남겨 두면 **안 졸린 폰이 명령을 마치자마자 침대로 돌아간다**.
- **졸린데 제 침대가 없으면 빈 것을 잡는다**(`Bed.ClaimFreeFor` — 이쪽이 `AutoClaimed`를 세우는
  유일한 자리다). 사람마다 배정하게 두면 폰이 늘 때마다 잡일이 늘고, 잊으면 **그 폰만 조용히
  바닥에서 잔다**.
- **침대 600초 / 바닥 1200초**(임계→가득, `Needs.SleepPerSecond`). **눈금은 배율 하나다**(2026-08-25) — 바닥 `Needs.FloorQuality` 0.5 · 보통 침대 `Bed.RestQuality` 1.0 · **좋은 침대 1.4**(429초). 전에는 `bool inBed` 하나라 짚더미든 깃털 요든 똑같았고, 그러면 **가구를 지을 이유가 없다**. **아래로는 안 쓴다** — 나쁜 침대는 바닥과 구별이 안 가고 그건 짓지 말라는 뜻이라 선택이 아니다. 바닥잠은 **밤새(600초) 자도
  40점 중 20점밖에 못 채워서** 다음 날 낮에 또 졸리고, **침대가 그 고리를 끊는다** — 그게 침대를
  짓는 이유다.
- **바닥(0)에 닿으면 자리를 안 가리고 쓰러진다** — 습격 한복판이든 광산이든. 임계에서 자러
  가는 것은 *판단*이고 이건 *한계*라, 피로는 안 죽이면서도 아프다.
- **저 혼자 잡은 잠자리는 가는 문턱과 나오는 문턱이 다르다**(`Threshold` 40 → `WakeAt` 100). **하나로 두면 문턱 위에서
  떤다** — 자러 가서 조금 차면 "이제 안 졸리다"가 되어 일어나고, 나가면 다시 졸려서 돌아온다
  (2026-08-20에 실제로 그랬다: 침대로 갔다 바로 일어나기를 되풀이했다). **누워 있을지 정하는
  자리는 `RestNode`에 하나뿐이어야 한다** — 두 곳에서 물으면 낮은 문턱이 늘 이겨 히스테리시스가
  도로 사라진다. **다친 것이 남았으면 계속 눕는다**(치료를 기다리는 폰이 잠이 깼다고 걸어
  나가면 안 된다).
- **자는 동안에는 피로가 안 준다**(`Needs.Decays`). 피로는 *깨어 있는 값*이라 그게 맞고, 안
  그러면 채우는 속도에서 주는 속도가 상쇄된다 — 침대는 두 배 느려지고 **바닥잠은 영영 안 찬다**
  (바닥 회복이 감소와 정확히 같은 값이라 net 0이다: 쓰러진 폰이 영원히 못 일어난다).
- **굶주림이 잠보다 먼저다** — 굶은 채로 자면 자는 동안 바닥을 치는데 **자는 폰은 스스로 못
  일어나 그대로 죽는다**.

---

# 개체 명세와 특성·배경 — CLAUDE.md에서 내려온 상세 (2026-08-27)

## 개체 명세 — `PawnSpec` (`Assets/1_Script/unit/PawnSpec.cs`)

**"이 폰이 어떤 사람인가"를 태어나기 전에 적어 둔 것.** 이름 · 스탯 등급 53개 · 몸 크기, 그리고 그것을 만든 **시드 하나**.

`UnitBrain`은 `Awake`에서 이름과 스탯을 제 마음대로 굴린다 — 그게 기본값이다. 하지만 **보고 나서 데려오는 경우**(마을 고용)엔 메뉴에 뜬 사람과 합류한 사람이 같아야 하므로, `UnitBrain.ApplySpec(spec)`이 그 결과를 덮는다. `Spec` 프로퍼티로 남는다.

- **모든 무작위가 시드 하나에서 나온다** → 같은 시드면 몇 번을 다시 계산해도 같은 사람(세이브 없이도 일관).
- **외모 커스터마이징의 자리이기도 하다.** 굴리는 것과 사람이 정하는 것이 **같은 그릇**을 쓰므로, 나중에 머리·피부색·장식이 늘어도 `PawnSpec`에 필드가 늘 뿐 적용 통로(`ApplySpec`)는 그대로다. 지금은 `BodyScale` · `MaterialOverride` · `ModelOverride`가 그 축이다(`ApplyLook`이 입힌다 — 전용 모델은 기본 렌더러를 **끄고** 자식으로 붙인다. 지우면 초상화·선택 표식처럼 렌더러를 훑는 것들이 깨진다).
- **수준(tier)을 먼저 뽑고 스탯은 그 언저리(±1칸)에서 굴린다.** 53개를 각각 균등하게 굴리면 평균이 중심으로 몰려(표준편차 1/√53) 누구나 한복판이 되고 "강한 방랑자"가 성립하지 않는다.
- **저작할 수 있다 — `PawnSpecData`(SO, `Create → DungeonColony → PawnSpecData`).** 이름 있는 동료는 매번 다른 사람이면 안 되는데, **시드는 굴림의 결과를 붙들 뿐 무엇이 나올지 고를 수 없다**(원하는 조합이 나올 때까지 숫자를 바꿔 돌리는 건 저작이 아니라 뽑기다). 그래서 **적고 싶은 것만 적고 나머지는 굴린다** — `Grades`에 적은 스탯만 그 등급으로 못 박히고, 안 적은 것은 종족 범위에서 나온다. `Seed`를 비우면 **이름에서 안정 해시로 뽑아** 시드를 안 만져도 늘 같은 사람이다(`GetHashCode`는 실행마다 달라져 못 쓴다). 못 박은 등급이 평균을 바꾸므로 `RecomputeAverage`로 다시 센다 — 안 하면 저작한 S급 동료가 굴린 값 그대로 싸게 나온다.
- **굴리기가 둘이다**: `Roll(data, seed, min, max, …)`는 범위를 **덮어쓴다**(마을 후보 — 지역 등급이 종족 기본을 이긴다), `Roll(data, seed, …)`는 **종족이 스탯별로 저작한 범위를 그대로** 쓴다(고정 개체 — 그 종족다움이 남아야 한다).
- **배선은 `UnitBrain`**: `fixedName`(이름만) / `pawnSpec`(전부). 우선순위는 **저작 < 주입** — 마을 고용처럼 밖에서 `ApplySpec`을 주면 그쪽이 이긴다(상인 상품과 같은 규칙).
- `PriceFrom(basePrice)` — `기준가 × (1 + 평균등급 × 0.12) × 흔들림(±22%)`. **등급이 값을 정하되 같은 등급이라도 값은 다르다** — 흔들림이 없으면 값이 곧 스탯 표가 되어 굳이 볼 이유가 없다. 흔들림도 시드에서 나오므로 메뉴를 여닫아도 안 바뀐다(스탯 추첨과는 **다른 난수열**을 쓴다 — 같은 열이면 "스탯이 좋으면 흔들림도 크다"는 상관이 생긴다).

## 특성 — `TraitData` / `TraitPool` (`Assets/1_Script/unit/`)

**"이 폰은 어떤 사람인가"를 숫자 대신 이름으로 답하는 것.** `강인함 62`는 비교해야 뜻이 생기지만 `강인함`이라는 딱지는 그 자체로 읽힌다.

**모델 A — 특성은 스탯에 숫자를 얹을 뿐이다**(2026-07-26 결정, `docs/design_traits.md`). 기능을 특성으로 게이트하지 않는다: 그러면 전투·작업 코드가 전부 "이 폰이 그 특성을 가졌나"를 묻게 되고 특성 하나 늘 때마다 분기가 사방에 는다. 그래서 `BodyPartSystem.TryKnockOut`은 지금도 `Stats.Toughness`만 읽는다.

- `TraitData`(SO, `Create → DungeonColony → TraitData`) — `StatOffsets` · `Weight` · **`ExclusiveGroup`**(같은 묶음은 한 폰에 하나 — `용감`+`겁쟁이`는 설명이 안 되고 오프셋이 **상쇄돼** 안 붙은 것과 같아진다). 에셋은 `Resources/Data/Traits/`(10종, 배타 4쌍 + 단독 2).
- **`GrowStat`을 쓰지 않는다** — 그건 성장이라 `IsLocked`(하수인)에서 막히고 승급 알림까지 띄운다. 특성은 타고난 것이라 태어날 때의 값을 바꾼다.
- **개수도 굴린다 — 0~3개**(`TraitPool.RollForPawn`, 가중치 `{0.8, 1.8, 3, 2.6}` → 0개 10% · 1개 22% · 2개 36% · 3개 32%). 전원이 정확히 셋이면 특성은 개성이 아니라 **양식**이 된다. 다만 **저작한 특성은 굴린 개수를 이긴다**(0이 나와도 `PawnSpecData`에 적은 건 붙는다).
- **`PawnSpec.Traits`가 시드에서 나온다**(굴림 **맨 끝** — 앞 순서를 안 건드려야 기존 시드의 이름·스탯이 그대로다). 개수도 같은 난수열에서 뽑으므로 마을 후보 메뉴에서 본 특성 그대로 합류한다.
- `ApplySpec`은 **스탯을 덮어쓴 뒤에** 얹는다(순서가 뒤집히면 등급이 보정을 지운다). `SetTraits`가 이전 것을 되돌리므로 두 번 불려도 두 겹으로 안 쌓인다.
- **전부 받는다** — 방랑자·하수인·적·NPC. 명세 없는 폰은 `ApplyAuthoredSpec`이 굴린다. 하수인도 받으므로 **같은 하수인을 소환해도 다른 놈이 나온다**.
- **나중에 붙일 수 있다** — `UnitBrain.AddTrait`/`RemoveTrait`(부상 트라우마 등의 진입점). 뗄 때 0~130 클램프에 잘린 만큼은 안 돌아온다.
- 저작은 `PawnSpecData.Traits` — **적은 것만 못 박고 남는 자리는 굴린 것으로 채운다**(등급과 같은 규칙).
- 나쁜 특성도 스탯을 하나는 올린다(`곰손` = 손재주 −12, 근력 +8). 순수하게 나쁘기만 하면 개성이 아니라 벌칙이다.
- **스탯 표시명은 `StatNameExt.ToDisplay` 하나뿐**(`StatGrade.cs`). 전엔 `GradeUpNotificationUI`의 private 메서드였다. 스탯 설명은 `StatInfo.Describe`(툴팁 전용).
- **표시**: 스탯 창(C)은 맨 위에 이름+오프셋(`곰손  손재주 -12 · 근력 +8`), 체력 창은 이름만 한 줄(`특성  강인함 · 겁쟁이`). **마우스를 올리면 설명**(`HoverTooltipUI`, 씬 오브젝트) — 스탯·특성 둘 다. TMP **`<link>`로 글자 안에서 영역을 잡으므로**(`FindIntersectingLink`) 스탯 53개마다 오브젝트를 두지 않는다. 구조·위치 계산은 **`ItemInfoUI`와 같다**(커서 툴팁이 둘인데 규칙이 다르면 안 된다): 컴포넌트는 **늘 켜져 있는 부모**에 살고 자식 `panel`만 토글한다 — 꺼지는 오브젝트에 붙이면 `Awake`가 안 돌아 `Instance`가 null로 남고 툴팁이 한 번도 안 뜬다. 배경은 `raycastTarget = false`(커서를 먹으면 깜빡인다). **링크 태그는 정렬 바깥에 감을 것** — `{F(...),-50}`처럼 rich text에 자릿수를 매겨 열을 맞추는 코드라 태그가 안쪽에 들어가면 열이 어긋난다.

## 성장 배경 — `BackstoryData` / `BackstoryPool` (2026-08-19)

**유년기 하나 · 성년기 하나.** 림월드식으로 *"어떻게 자랐나"*와 *"무엇을 하며 살았나"*를 가른다 — 하나로 뭉치면 둘 다 있어야 말이 되는 사람(거리에서 자란 상단 호위)이 표현되지 않는다. 에셋은 `Resources/Data/Backstories/`(36종 — 유년기 16 · 성년기 20), 레지스트리 스캔이라 씬·프리팹 배열에 등록하지 않는다.

- **특성과 나란한 축이지 같은 축이 아니다.** 특성은 **타고난 기질**이라 개수를 굴리고(0~3) 배타 묶음으로 겹침을 막지만, 배경은 **살아온 이력**이라 칸이 정해져 있고 **반드시 하나씩** 나온다(내력이 없는 사람은 없다). 그래서 개수 굴림도 배타 묶음도 없다.
- **`PawnSpec`이 시드에서 굴린다 — 그런데 맨 끝에서.** 난수열은 부르는 횟수로 자리가 정해지므로 특성 뒤·스탯 앞에 끼우면 **스탯 굴림이 통째로 밀려 같은 시드가 다른 사람을 낳는다**(저작한 고정 개체와 마을 후보가 한꺼번에 바뀐다). 꼬리에 붙이면 앞의 것이 전부 그대로다.
- **사람이면 누구나 갖는다** — 내 폰이든 산적이든 마을 사람이든(`UnitData.UsesBackstory`, 늑대에게 유년기를 굴려 줄 이유는 없다). **붙잡은 산적을 살펴볼 때 `패거리 두목`이라고 뜨는 것**이야말로 회유할지 몸값을 받을지 정하는 재료다. 안 얹으면 세력별 이력을 저작한 것이 화면에 한 번도 안 나타난다. **작업 금지는 저절로 갈린다** — `WorkSettings`는 플레이어 세력 일꾼에만 붙으므로(`ApplyJobBlocks`가 `Work == null`이면 아무 일도 안 한다) 적에게는 걸릴 자리가 없고, 회유해 오는 순간 `ChangeFaction`이 다시 얹는다. 그래서 `무능력: 운반`은 적을 볼 때 **데려오면 어떻게 되는지**를 미리 알려 주는 줄이 된다.
- **공용이라는 것은 없다 — 모든 배경은 어느 무리의 것이다.** 한때 묶음이 빈 것을 "공용"으로 두고 늘 후보에 넣었는데, 그러면 **산적에게서 `사원의 시동`이 나온다** — 무리에서 뽑은 사람이 그 무리와 상관없는 이력을 지고 오면 세력마다 이력을 갈라 둔 뜻이 통째로 지워진다. "공용"으로 보이던 것들(농가·대장간·사원·광산)은 사실 **`civilian`**이었고, 이름을 붙이고 나니 규칙이 한 줄로 줄었다: **제 무리의 것에서 뽑는다**(유년기도 성년기도).
- **`civilian`이 폴백이다** — 묶음을 안 적은 세력(야수·망자·고블린)이나 그 무리의 배경을 아직 저작하지 않은 칸은 여기로 떨어진다. 아무 무리에도 안 걸려 배경이 **없는** 것보다 여느 사람처럼 자랐다고 보는 편이 늘 낫다. `BackstoryData.Group`을 비운 것도 `civilian`으로 친다 — 저작을 빠뜨렸다고 아무 데서도 안 나오면 **아무도 모른 채 풀만 조용히 얕아진다.**
- **그래서 무리마다 칸마다 여럿이어야 한다** — 하나뿐이면 잡아 온 산적이 하나같이 `도망친 아이 · 산적`이 되어 이력이 개성이 아니라 딱지가 된다. 지금 **36종**: 유년기 16(civilian 7 · bandit 4 · cult 3 · mercenary 2) · 성년기 20(civilian 6 · bandit 7 · cult 3 · mercenary 4).
- **적으로 태어난 사람도 굴려는 둔다 — 얹지 않을 뿐이다**(`UnitBrain._rolledChildhood/_rolledAdulthood`). 안 굴려 두면 **회유해 온 산적이 배경 없는 사람**이 된다. 데려오고 나서 굴리면 그건 이력이 아니라 합류 보너스다.
- **작업도 막는다 — 그러나 막는 길은 `WorkSettings` 하나다**(`BackstoryData.DisabledJobs` → `WorkSettings.SetBlocked`). 큐 배정이 이미 `IsEnabled` 하나를 지나가므로(`JobManager.RequestJob`) 거기서 거짓을 돌려주면 **작업·전투 코드는 성장 배경을 몰라도 된다** — 특성이 스탯만 얹는 것과 같은 취지다(모델 A: 분기를 늘리지 않는다). **끈 것(`_disabled`)과 다른 축**이라 사람이 P패널에서 켤 수 없고, 칸에 **`×`로 뜬다**(빈 칸으로 두면 "안 켠 것"과 구별이 안 돼 눌러 보고 나서야 안다).
- **직접 배정만 예외로 한 번 더 묻는다** — 손으로 붙이는 길은 작업 설정을 안 보므로(그건 명령이다) `IsEnabled`가 걸러 주지 않는다. `ResourceNode.Assign`·`PatrolZone.Assign`이 `IsBlocked`를 보고 **배정 자체를 거절**한다(안 그러면 *"무능력인데 바위는 캔다"*). **막힌 것은 켤 수도 없으므로**(`SetEnabled`) 자율 배정도 저절로 걸러진다.
- **무능력은 드물다** — 17종 중 셋만 막는다(귀족가의 아이=운반 · 학자=경비 · 옥살이=간수). 전부가 하나씩 막으면 개성이 아니라 세금이고, 폰을 뽑을 때마다 "이번엔 뭘 못 하나"를 확인하는 일이 된다.
- **`TraitData.StatOffset`을 빌려 쓴다** — 같은 모양을 하나 더 만들면 두 벌이 되고, 공용 타입으로 **옮기면** 유니티가 직렬화 타입을 못 찾아 **기존 특성 에셋의 값이 통째로 날아간다**.
- 표시: 스탯 창(C) `■ 성장 배경`(특성 **위** — 기질보다 내력이 먼저 읽힌다) + `<link>` 툴팁, `InspectPanel`은 이름만 한 줄. 첫 벌은 `DungeonColony → Create Backstories`가 만들었다.

---

# 소집(Draft) — CLAUDE.md에서 내려온 상세 (2026-08-27)

**소집(Draft) — `R`키로 고른 폰을 싸울 태세로 붙든다**(2026-08-20에 붙였다). **분기가 아니라 게이트다**: 한동안 트리 맨 위에 `DraftedNode`가 있었는데 그것이 `Running`만 돌려줘 **켜는 순간 아래가 통째로 멎었다**(이동 명령도 반격도 안 먹는 소집이라 부르는 입력도 안 붙어 있었다). 소집은 *무엇을 하는 상태*가 아니라 **자동 행동을 멈추고 싸울 태세로 두는 상태**라, 아래 노드들이 저마다 물어야 한다(업고 있는 것과 **같은 모양**).

뜻하는 것은 셋(`UnitBrain.Draft`):
- **자동 행동을 멈추되 배정은 남긴다** — 일감을 안 집고(`WorkNode`) 눕지 않는다(`RestNode`). **하던 일감은 `Draft()`가 `DropCurrentJob`으로 놓는다** — 게이트만 걸면 그 일감이 예약된 채 얼어붙어 **다른 폰도 못 가져간다**. **`AbandonWork`가 아니다**: 그건 자원 노드 배정까지 거두므로 소집을 풀 때마다 캐던 바위를 손으로 다시 붙여 줘야 한다. **침대도 안 건드린다** — 자던 폰을 일으키는 것은 `RestNode` 게이트의 `LeaveBed`가 이미 하고(자세와 **점유만** 풀고 **배정은 남긴다**), 여기서 `Bed.ReleaseFrom`을 부르면 배정까지 거둬 그 사이 다른 폰이 침대를 가져간다. **소집은 잠시 딴 일을 시키는 것이지 담당과 잠자리를 떼는 일이 아니다.**

**`DropCurrentJob` ↔ `AbandonWork` — 가르는 축은 "명령이냐 상태냐"다.**

- **명령은 옛 명령을 덮어쓴다** → `AbandonWork`(일감 + 배정). 이동·공격·치료·먹이기·설득·업기·줍기·호송·대화, 그리고 사망·원정 집결. 자원 노드 배정도 **플레이어가 붙인 명령**이라 새 명령이 덮는 것이 맞다(이동 명령이 `PutDownSite`·`FollowTarget`·`EscortTo`를 거두는 것과 같은 자리). 심부름 끝나고 하던 바위로 돌아가게 만들면, 시킬 때마다 그 폰이 **무엇을 하던 중이었는지 기억하고 있어야** 한다.
- **소집은 명령이 아니라 상태다**(켜고 끄는 스위치) → `DropCurrentJob`(일감만). 끄면 이전으로 돌아가는 것이 그 스위치의 뜻이라 **배정을 거둘 이유가 없다**. 손에 든 일감만 놓는 것은 게이트로 막기만 하면 그 일감이 **예약된 채 얼어붙어 다른 폰도 못 가져가기** 때문이다.
- **소집 중의 이동 명령도 배정을 안 거둔다** — 자원 노드(`MoveController`)와 **침대**(`Bed.RouseFrom`) 둘 다. 진형을 잡으려고 한 번 옮겼을 뿐이고, `WorkNode`·`RestNode`가 이미 막혀 있어 *"도착하자마자 도로 일하러/눕으러 간다"*가 성립하지 않는다.
- **밥은 그대로 먹는다**(`EatNode`는 안 막았다) — 막으면 **소집해 둔 것을 잊었을 때 굶어 죽는다**(업기가 먹는 것만 열어 둔 그 판단이다). 피로는 안 죽으므로(쓰러질 뿐이다) 막아도 사고가 안 난다.
- **먼저 안 친다 — 소집은 성향의 *먼저 걸기*를 끈다**(2026-08-26에 뒤집었다, 사용자 결정). 08-20에는 *"소집은 싸울 태세라 먼저 친다"*로 두었는데, 그러면 **세워 둔 폰이 표적을 찾아 걸어 나가** 진형을 잡으려던 뜻이 사라진다. 소집의 뜻은 **"내가 조종할 때까지 그 자리에 있어라"**다 — 먼저 거는 것은 성향이 할 일이고 소집은 그것을 끈다(교전 성향이어도 소집 중에는 안 건다). **다만 맞으면 되받아치고 곁의 적까지는 문다** — 맞거나 표적을 들면 `UnitBlackboard.Engaged`가 서고(**드는 길이 둘이다 — 맞았거나 치고 있거나**) **주위가 빌 때까지** 훑기가 켜지며, 그 훑는 범위가 **짧은 목줄**이다(`DetectNode.DraftLeash`, 공격 사거리 ×3 = 근접 4.5m). (한 번 문 표적은 목줄 밖으로 달아나도 쫓는다 — 지목한 표적을 유지하는 규칙과 갈라 쓸 수가 없어서다.)
- **도망치지 않는다** — `PlayerFleeNode`가 이미 `!bb.IsDrafted`를 본다. 상태 글자는 `PawnStatusText`가 이미 띄운다.

**명령을 받는가는 폰이 답한다** — `UnitBrain.AcceptsOrders`(하수인은 *원래* 안 받는다) / `CanObeyNow`(갇히거나 업힌 폰은 *지금* 못 받는다). **둘은 다른 물음이라** 부르는 쪽이 갈라 쓴다(우클릭은 후자를 "빠졌습니다"로 알리고 전자는 조용히 건너뛴다). 묻는 곳이 둘이라(`MoveController`·`DraftController`) 각자 적으면 **명령이 조용히 사라지는 대상**이 생긴다.


---

# 폰이 서고 걷고 휘두른다 — CLAUDE.md에서 내려온 상세 (2026-09-13)

**피벗 · `PawnRig` 규격 · 휴머노이드 아바타 · 손에 드는 도구 · `AttackStyle`.**

- **폰의 피벗은 발밑이다** — 콜라이더 바닥과 발끝이 모두 0이라 **씬에 놓을 때 루트 Y = 지면 높이**다. 몸통 자리를 묻는 곳은 **`PawnPlacement.CenterOffset`**(상수로 박지 말 것 — 높이가 종족마다 다르다). **`Model`은 발에 있다**(2026-09-09 — 전에는 몸 한가운데가 원점이라 `Model.localPosition.y = 몸집 − 1`로 띄웠는데, 휴머노이드 아바타는 애니메이터가 붙은 오브젝트를 **발 자리**로 삼아 엉덩이를 그 위에 놓으므로 그 규칙 그대로 세우니 **폰이 1m 떠서 걸었다.** `2. Build Pawn Rigs`가 `Model`을 0으로 내리고 살을 그만큼 올린다 — 몸집은 `Model.localScale`만 바꾸면 된다. 콜라이더는 루트에 있어 안 커진다). **표면 "위에" 찍는다**(`TileToGround`는 윗면이라 그대로 찍으면 캡슐 아래 절반이 파묻힌다). **폰의 관절은 `PawnRig` 규격이다**(2026-09-09) — 살(큐브·구)은 관절의 자식이고 클립은 관절 **경로**로 묶이므로 이름·계층을 바꾸면 그 클립이 조용히 안 먹는다. 짓는 문은 `DungeonColony/Pawn/0. Rebuild Pawn Rigs And Animations` 하나다(손으로 세우지 말 것 — 관절 자리는 살의 크기에서 나온다). `PawnAnimator`는 판정을 **옮길 뿐** 제 판단이 없다(걷나 = 실제 변위 · 휘두르나 = `CombatHandler.OnStrike` · 누웠나 = 시체·잠·기절·업힘). `OnStrike`는 **결과가 아니라 동작**이다 — 막힌 공격도 팔은 움직인다. **두발 폰은 받은 휴머노이드 클립을 블록 인형 그대로 쓴다**(2026-09-09, 사용자: *"폰 생김새를 바꾸고 싶던 건 아니었는데"*) — 휴머노이드 클립은 휴머노이드 아바타에만 붙는데, 그 아바타는 **사람 뼈 열다섯만 있으면 어떤 계층에든** `AvatarBuilder`로 세울 수 있다. 그래서 마네킹으로 갈아 끼우지 않고(그 길은 접었다) 블록 관절에 팔꿈치·무릎·손·발을 더해 아바타를 세운다(`Controller/Avatars/`). **T자로 저장된다**(아바타의 기준 자세 — 판이 열리면 Idle이 덮는다). **컨트롤러가 둘이어도 매개변수는 한 벌이다**(`Speed`·`Attack`·`Lying`·`Work` — `PawnAnimator`가 어느 쪽인지 몰라야 한다. 네발에는 `Work`가 없어 있는지 한 번 보고 넣는다 — 없는 매개변수를 넣으면 매 프레임 경고다). **일하는 쪽을 본다**(2026-09-10 — 걷는 동안은 `PawnMover`가 가는 쪽을 보게 하는데 **멈춰 서서 일할 때는 아무도 안 돌려 줘** 마지막으로 걷던 방향으로 곡괭이를 휘둘렀다. `WorkNode`가 일감·노드 쪽으로 `FaceTowards`). **두 손 도구는 자루의 밑동을 왼손에 둔다**(피벗을 손에 두면 팩마다 피벗 규약이 달라 곡괭이 머리가 폰 머리 위로 솟는다 — 밑동은 메시 경계에서 낸다). **무엇으로 일하나는 일감이 답한다**(`JobBase.Anim` → `WorkAnim` — 단계가 있는 일감은 자리에서 일하는 단계에서만) — `WorkNode`가 자리에서 돌릴 때 `UnitBlackboard.Working`에 적고 뇌가 틱마다 없음으로 되돌린다. 애니메이터의 일 상태와 **손에 드는 도구**(`PawnProps` · `HandPropSet` — 무기는 `WeaponType`으로, 도구는 `WorkAnim`으로 고른다. 장비 에셋마다 모델을 적지 않는다: JSON 아이템은 프리팹을 못 가리킨다)가 같은 값을 읽는다. 쥐는 자리는 프롭 프리팹의 `HandProp`이 들고 **눈으로 맞춘다**. **두 손 자세는 도구의 것이지 무기의 것이 아니다**(2026-09-11) — 자루를 **팔뚝의 연장**으로 내는 그 자세는 도구가 **일하는 중에만 보여서**(`Working`) 늘 팔이 들린 클립 안에 있기에 성립한다. **무기는 대기·걷기에도 보이므로** 같은 자세를 주면 가만히 선 창병의 내려간 팔을 따라 **2m 창이 바닥에 박힌다** — 무기는 검·활·방패처럼 고정 그립이고, 그래야 `GripEuler`·`GripOffset`이라는 **눈으로 맞출 손잡이**가 생긴다. **팩에 없는 것은 블록으로 짓는다**(창·지팡이 — 폰이 블록 인형이라 기본 프리미티브가 오히려 어울리고 **내장 메시는 프리팹이 가리키므로 빌드에 들어간다**). **프롭은 무기 종류당 하나라 원소로 색을 못 가른다** — 지팡이가 여덟이라 하나를 골라 칠하면 일곱은 거짓말이다(맑은 수정 하나로 둔다). **공격은 무기마다 다르다**(2026-09-11, `AttackStyle` — 한손·양손·장병기·활·캐스팅·맨손 여섯). `PawnAnimator`가 `Style` 정수를 넣고 컨트롤러가 스타일마다 공격 상태를 하나씩 든다(`HumanoidClipSet.StyleClips`). **스타일을 저작하지 않는다 — 무기 종류에서 파생시킨다**(`WeaponTypeExt.AttackStyle`): `WeaponData`에 필드를 두면 같은 물음에 답이 둘이 되고 **새 무기에서 빠뜨리면 조용히 맨손으로 휘두른다**(JSON 아이템은 더 쉽게 빠뜨린다). **클립이 없는 스타일도 상태를 갖는다** — 빼면 그 무기를 든 폰은 **갈 곳이 없어 트리거만 쌓이고 아무 동작도 안 한다**(그게 창병이 *공격 모션이 없는 것처럼* 보였던 모양이고, 원인은 클립이 아니라 **손이 비어 있던 것**이었다). **값은 꼬리에 붙일 것**(컨트롤러에 정수 조건으로 구워진다 — `WorkAnim`과 같은 규칙). **누움은 아직 루트 회전이다**(`PawnMover`) — 죽음 클립으로 옮기려면 침대·업기·기절·시체 넷을 한꺼번에 옮겨야 한다.

# 치비 폰 — 림월드 얼굴을 3D로 (2026-09-18)

**도구:** `Pawn/3. Create Chibi Pawns` → `Pawn/2. Build Pawn Rigs` · 눈 자리는 `Pawn/5. Chibi Face Layout` ·
아틀라스는 `Pawn/4. Build Face Atlases`(얼굴 그림 폴더 `assetsss/face/eyes|mouths/`가 바뀌면 저절로).

- **보이는 것은 머리 · 몸통 · 손 공 둘뿐이다** — 팔다리는 휴머노이드 뼈 열다섯 때문에 **살은 두고 렌더러만 끈다.**
  머리에 얹을 것은 `Head.` 꼴 이름이라 리거가 머리 관절에 붙인다(`관절.무엇`).
- **외곽선은 뒤집은 껍질이다**(`Resources/Shaders/ToonOutline.shader`) — 패스는 `SRPDefaultUnlit`(URP는 같은 LightMode
  패스를 하나만 그린다: 둘 다 `UniversalForward`면 **폰이 통째로 검다**). 굵기는 월드 6cm와 화면 최소 중 큰 쪽.
  **외곽선은 메시 테두리마다 선다** — 보이면 안 되는 이음매는 **메시를 가르지 않는다**(머리카락이 두 조각이던 동안
  이마를 가로지르는 줄이 섰다). **머리와 몸 사이 틈 6.5cm는 외곽선 6cm보다 넓어야 한다**(좁으면 몸통 꼭대기가
  머리 껍질 안으로 들어가 셔츠가 선을 뚫는다 — 굵기를 바꾸면 틈도 같이).
- **머리형 열둘은 림월드 이름 그대로다**(`Male_AverageNormal` … `Female_NarrowWide`) — **옆선은 그림에서 쟀다**
  (`Tools/chibi_heads/` 의 `trace_heads.py` → `gen_profiles.py` → `Assets/Editor/ChibiHeadProfiles.cs`, 자동 생성).
  숫자로 지은 옆선은 세 번 다 틀렸다(역삼각형 외계인 두 번 · 실크햇 한 번). **크기도 그림의 몸통 높이가 눈금이다**
  (`TorsoH` ÷ 몸통 픽셀) — 통째로 키우려면 `HeadScale` 하나. 깊이는 그림에 없어 폭의 0.95, **턱은 아랫반쪽만 앞으로 민다**
  (`Shear` — 뒤통수는 둥근 채). **체형이 성별을 들고 제 성별의 여섯만 굽는다.**
- **조합은 전부 굽고 `PawnLook`이 씨앗으로 한 벌씩 켠다** — 굴리지 않는다(청크가 다시 올라올 때마다 얼굴이 바뀐다).
  인스펙터에서 머리형·머리카락·왼눈·오른눈·입을 못 박는다(`PawnLookEditor`). 앞서는 순서는 **코드의 표정(`SetFace`)
  > 인스펙터 > 구울 때의 기본.** 머리카락은 기준 머리(`…AverageNormal`)에 맞춰 굽고 **고른 머리의 틀로 옮긴다**
  (`fittedTo` — 구운 값을 따로 들면 옮긴 자리가 저장된 뒤 다시 읽혀 **옮길 때마다 어긋난다**). 윗머리 모양이 둘이라
  (`Wide` 턱은 네모) 머리카락도 두 벌이고 `PawnLook.HairFitOf`가 하나만 켠다.
- **눈·입은 채널 분할 아틀라스의 칸이다**(R 잉크 · G 흰자 · B 강조 · A 자르기 — 색은 재질이 칠한다) · **칸은 이름으로
  든다**(번호면 팩이 늘 때 표정이 밀린다) · **눈 아틀라스와 입 아틀라스는 따로다** · **판은 머리마다 따로 굽는다**
  (곡면이 달라 한 벌이면 묻히거나 뜬다) · **왼눈이 그림 그대로, 오른눈이 거울.**
- **눈 자리는 사람이 고치는 표다**(`ChibiFaceLayout` — `5_Data/Chibi/Chibi_FaceLayout.asset`) — 처음 값은 측정이고
  **고친 것이 이기며 도구는 없는 머리만 채운다.** **슬라이더를 움직이는 즉시 눈·입 메시만 제자리에서 다시 굽는다**
  (`ChibiPawnCreator.RebakeFaces` — 프리팹·폰은 그 메시를 가리키므로 손대지 않는다. [다시 굽기]를 따로 눌러야 먹던
  동안은 *"설정해도 안바껴"*였다). **눈은 가로 타원이다**(`EyeAspect` 1.35) — 동그란 판을 둥근 머리 옆에 붙이면 옆으로
  눌려 세로로 길쭉해 보인다. 측정은 **진한 알맹이**로 한다(번짐까지 세면 눈이 커진다).
- **팔은 손 바로 위에서 내려온다 · 위팔 내림 한계는 90°다**(`PawnRigBuilder.ArmLimit`) — 이 뼈대에는 어깨 뼈가 없어
  휴머노이드가 팔을 T자에서 60°까지만 내린다(어깨 몫 -15°가 빠진다). 손 공을 손 관절에서 떼어 두면 그 벌어짐이
  지렛대가 되어 **가만히 서 있어도 손이 턱까지 들린다.** 어깨 뼈를 넣지 않은 이유: 관절 규격과 클립 경로가 바뀌어
  모든 폰을 다시 세워야 한다. **이 한계는 모든 사람형 폰에 걸린다.**
- **메시 에셋은 있으면 속만 갈아 끼운다**(`Save` — 같은 경로에 `CreateAsset`을 다시 부르면 옛 객체가 죽어 로드된
  프리팹이 빈 렌더러를 그린다) · **이번에 안 만든 `Chibi_` 메시는 치운다**(이름이 바뀌면 옛 것이 쌓인다).

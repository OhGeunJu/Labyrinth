# DungeonColony

림월드 + 켄시 + 던전 코어 시뮬레이터

---

## 게임 컨셉

플레이어는 던전 코어(미궁핵)를 운영하는 주체로서, 두 종류의 유닛을 관리한다.

- **방랑자** — 림월드/켄시 스타일. 직접 선택·명령 가능. 다양한 작업과 전투 수행.
- **하수인** — 펠월드 팰 스타일. 던전 코어가 자원을 소비해 소환. 배정된 작업만 자동 수행, 공격받으면 반격.

---

## 개발 환경

- Unity 6.3 (6000.3.9f1)
- Wingman
- DOTween Pro

---

## 프로젝트 구조

```
Assets/
├── 1 Script/
│   ├── Map/
│   │   ├── TileData.cs              # 타일 데이터 (Passable / Impassable)
│   │   ├── MapManager.cs            # 맵 전체 관리 (싱글톤)
│   │   ├── GridRenderer.cs          # 타일 변경 시 시각화 갱신
│   │   └── ResourceNode.cs          # 자원 노드 (채굴 → 인벤토리 → HaulJob)
│   │
│   ├── unit/
│   │   ├── UnitBrain.cs             # AI 진입점 (BT 실행, 방랑자/하수인/적 분기)
│   │   ├── UnitBlackboard.cs        # 유닛 공유 런타임 상태
│   │   ├── UnitStats.cs             # 숙련도 (0-100, 플레이 중 성장)
│   │   ├── UnitData.cs              # 유닛 템플릿 (ScriptableObject)
│   │   ├── CombatHandler.cs         # 근접/원거리 전투, 무게·상태 보정
│   │   ├── BodyPartSystem.cs        # 부위별 체력 / 혈액 / 기절
│   │   ├── BodyPart.cs              # 부위 데이터 (HP 0=기능상실, -100=파괴)
│   │   ├── Faction.cs               # 진영 (Player / Enemy)
│   │   ├── BTNode.cs / BTSequence / BTSelector
│   │   └── BTLeafNodes.cs           # ConditionNode, DetectNode, FightNode, WorkNode, IdleNode
│   │
│   ├── Item/
│   │   ├── ItemData.cs              # 아이템 템플릿 (Weight 포함)
│   │   ├── WeaponData.cs            # 무기 데이터 (ItemData 상속)
│   │   ├── MinionData.cs            # 하수인 데이터 (UnitData 상속)
│   │   ├── ItemStack.cs             # (ItemData, Quantity) 값 타입
│   │   ├── Inventory.cs             # 아이템 저장소 (무게·타입 집계 포함)
│   │   └── DroppedItem.cs           # 씬 내 드롭 아이템 (Spawn() 팩토리)
│   │
│   ├── Job/
│   │   ├── JobBase.cs               # 작업 추상 클래스 (Category 포함)
│   │   ├── JobCategory.cs           # 작업 카테고리 열거형
│   │   ├── MineJob.cs               # 채굴 작업
│   │   ├── HaulJob.cs               # 운반 작업 (ResourceNode → StorageBuilding)
│   │   ├── GroundHaulJob.cs         # 운반 작업 (DroppedItem → StorageBuilding)
│   │   └── JobManager.cs            # 작업 관리 (우선순위 점수 적용)
│   │
│   ├── core/
│   │   ├── DungeonCore.cs           # 미궁핵 (하수인 소환 포함)
│   │   ├── DungeonCoreManager.cs    # 미궁핵 관리 (싱글톤)
│   │   └── DungeonCoreData.cs       # 미궁핵 데이터 (ScriptableObject)
│   │
│   ├── UI/
│   │   ├── UnitInfoUI.cs            # 유닛 정보 패널
│   │   ├── PawnInventoryPanel.cs    # 인벤토리 패널 (I키)
│   │   ├── InventorySlotUI.cs       # 슬롯 드래그 드롭
│   │   ├── PriorityPanel.cs         # 작업 우선순위 패널 (P키)
│   │   ├── NodeInfoPanel.cs         # 자원 노드 정보 패널
│   │   ├── PawnRosterPanel.cs       # 폰 목록 패널
│   │   └── PawnEntryUI.cs           # 폰 목록 항목
│   │
│   ├── WeaponType.cs                # 무기 타입 열거형
│   ├── Equipment.cs                 # 장착 슬롯 (MainHand)
│   ├── StorageBuilding.cs           # 창고 (아이템 필터, 전체 집계)
│   ├── PriorityManager.cs           # 글로벌 작업 우선순위 (싱글톤)
│   ├── PawnMover.cs                 # 유닛 이동 (Athletics, 무게 보정)
│   ├── Pathfinder.cs                # Theta* 경로탐색
│   ├── SelectionManager.cs          # 유닛 선택
│   ├── MoveController.cs            # 이동/공격 명령 (방랑자만)
│   └── CameraController.cs          # 카메라 조작
│
└── 5 Data/
    ├── UnitData/
    ├── MinionData/
    ├── WeaponData/
    └── DungeonCoreData/
```

---

## 핵심 시스템

### 유닛 종류

| 종류 | 데이터 | BT 트리 | 특징 |
|---|---|---|---|
| **방랑자** | UnitData | 소집→전투→작업→대기 | 직접 명령, 모든 작업 가능 |
| **하수인** | MinionData | 자동감지→작업→대기 | 자원 소비로 소환, 허용 작업만 수행 |
| **적** | UnitData | 감지→전투→대기 | 자동 AI |

### BT (Behavior Tree)

기절(IsKnockedOut) 시 틱 중단.

### 작업 시스템

작업 선택 점수 = `글로벌 우선순위 × 100 + 작업 자체 우선순위`

| 작업 | 클래스 | 흐름 |
|---|---|---|
| 채굴 | MineJob | ResourceNode.Work() 반복 |
| 노드 운반 | HaulJob | ResourceNode 인벤토리 → 폰 → StorageBuilding |
| 지면 운반 | GroundHaulJob | DroppedItem.PickUp() → 폰 → StorageBuilding |

### 우선순위 시스템

P키로 패널 열기. 카테고리별 0(OFF)~5 설정. 0이면 해당 작업 완전 무시.

카테고리: 채굴, 운반, 원예, 벌목, 건설, 경비

### 창고 시스템

- `StorageBuilding` — 인벤토리 보유, 허용 아이템 타입 필터 설정 가능
- `DroppedItem.Spawn()` — 씬에 아이템 생성 + GroundHaulJob 자동 등록
- `StorageBuilding.GetTotal(type)` / `Consume(type, amount)` — 전체 창고 집계

### 캐릭터 스탯 (0–100)

| 카테고리 | 스탯 | 주요 효과 |
|---|---|---|
| **속성** | 근력 | 맨손 피해, 대형무기 속도, 최대 수용 무게(×5) |
| | 강인함 | 피해 저항, 기절 저항 |
| | 손재주 | 공격/방어 속도, 절단 피해 |
| | 인지력 | 원거리 명중률 |
| **전투 기술** | 근접공격/방어 | 무기 착용 시 공격빈도/블록 |
| | 정밀사격 | 아군 피격 회피 (60~100%) |
| | 무술 | 맨손 전투 전반 |
| | 회피 | 회피 확률 |
| | 육상 | 이동속도 (`base × (1 + Athletics/100)`) |
| **무기 숙련** | 한손검/도끼/둔기 등 | 해당 무기 피해량 |
| **노동** | 채굴/원예/벌목 등 | 작업 속도/품질 |

### 전투 공식

```
// 근접 데미지
damage = AttackDamage × (1 + 무기숙련도/100)
       × [1 + Dexterity/100 × 0.5]  // 절단무기
       × [1 + Strength/100 × 0.5]   // 맨손
       × 상태보정 × 무게보정

// 공격 속도
interval = BaseAttackInterval
         / (1 + Dexterity/100)
         / [1 + Strength/100 × 0.5] // 대형무기
         / 상태보정 / 무게보정

// 무게 보정 (Strength × 5 초과 시)
weightCorrection = maxWeight / currentWeight
```

### 부위별 체력

| 부위 | 기능 상실 (0) | 파괴 (-100) |
|---|---|---|
| 머리/가슴/복부 | 기절 시도 (강인함 저항) | 즉사 |
| 팔 | 공격력 감소 | 기절 시도 |
| 다리 | 절뚝거림/기기 | 기절 시도 |

혈액 0% 미만 → 기절. -30% → 사망.

### 인벤토리 & 드롭

- I키: 선택 유닛 인벤토리 열기
- 슬롯 드래그 → UI 밖에서 손 놓으면 해당 위치에 아이템 드롭
- 드롭된 아이템은 자동으로 GroundHaulJob 등록 → 운반 폰이 창고로 전달

### 하수인 소환

`DungeonCore` 인스펙터에서 `MinionEntry(MinionData + Prefab)` 목록 설정.
`DungeonCore.Summon(minionData, position)` — StorageBuilding에서 비용 소비 후 프리팹 스폰.

### 미궁핵 유형

| 유형 | 효과 |
|---|---|
| Cave | 기본 동굴 |
| Meadow | 초원 |
| Desert | 허기 증가, 이동속도 감소 |
| Swamp | 이동속도 감소, 독 |
| Ice | 이동속도 감소, 미끄러짐 |
| Lava | 주변 지속 데미지 |
| Jungle | 이동속도/시야 감소 |
| Cursed | 아군 스탯 감소, 적 스탯 증가 |

---

## 단축키 모음

| 키 | 기능 |
|---|---|
| I | 선택 유닛 인벤토리 |
| P | 작업 우선순위 패널 |
| WASD / 중클릭 드래그 | 카메라 이동 |
| 스크롤 | 카메라 줌 |

---

## 다음 할 것

- **DungeonCore 영역 효과** — 환경 타입별 범위 내 유닛 버프/패널티
- **적 스폰 시스템** — 웨이브 또는 이벤트 기반
- **제작/가공 시스템** — 자원 → 아이템 변환
- **적 사망 전리품** — `DroppedItem.Spawn()` 연결

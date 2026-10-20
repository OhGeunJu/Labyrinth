# AI 패키지 — 평소 행동을 데이터로 저작한다

2026-10-18 · 사용자: *"아까 말했던 AI 패키지를 데이터로 저작하는 걸 하자 이게 더 좋은 방식이 맞나?"*

## 왜

전에는 NPC 역할(`NpcType`) 다섯마다 코드로 짠 트리가 하나씩 있었다(`BuildVillagerTree` 등). 적도 트리 하나가 코드에 박혀 있었다.
데이터로 고를 수 있는 것은 성향(`AiProfileData` — 싸우나·도망가나·배회하나)뿐이었다. 그래서 새 역할(*"밤에는 모닥불 곁, 낮에는 두 지점 사이를 순찰하는 산적"*)을 만들려면 매번 코드를 새로 짜야 했다.

이 게임은 세계를 손으로 저작한다. 마을과 NPC를 저작 씬에 놓고, 무엇이 어디 있는지는 사용자가 정한다(`docs/design_world_editor.md`). 그렇다면 *"이 무리는 여기서 무엇을 하나"*도 같은 자리에서 고를 수 있어야 짝이 맞는다. 모드가 새 역할을 넣는 길도 된다.

## 무엇이 패키지이고 무엇이 아닌가

- **패키지 = 위에서부터 묻는 목표 목록**이다(셀렉터 한 줄). 맨 끝의 대기는 저절로 붙는다.
- **목표는 코드에 있는 행동을 이름으로 고른다**(`AiGoalKind`). 행동을 데이터로 짜는 범용 트리 편집기는 만들지 않는다. 순서를 하나 잘못 놓았을 때 조용히 깨지고 디버그가 어려워서다.
- **내 폰(방랑자·하수인·가축)은 패키지를 안 쓴다.** 소집·일감·Needs가 맞물린 시스템이라 순서가 곧 규칙이다(`CLAUDE.md` BT 순서 표).
- **습격은 `RaidLord`의 것이다.** 패키지는 평소 행동만 맡는다.

## 축 여섯

종족(`UnitData`) · 세력(`FactionData`) · 성향(`AiProfileData` — 위협에 **어떻게 반응하나**) · 역할(`NpcData` — **누구인가**: 이름 · 파는 물건 · 경비인가) · 소지(`LoadoutData`) · **패키지(`AiPackageData` — 평소에 무엇을 하나)**.

- `NpcType`은 남았다. 이름표(`InspectPanelUI`), 마을 경비 찾기(`TownSite`), 영입 판정(`Conversation`)이 *누구인가*로 묻는다. **트리를 고르는 일만 패키지로 갔다.**
- 맵을 떠나는가도 패키지가 답한다(`AiPackageData.LeavesMap` → `WildSpawner` 경고).
- 성향은 패키지 위에 얹힌다. 목표의 `Stance`·`WanderByDefault`는 *성향 에셋이 없을 때*의 기본값이다(`AddCombatBranch`·`AddWanderBranch`의 규칙 그대로).

## 목표 (`AiGoalKind` — 꼬리에만 붙인다)

| 목표 | 하는 일 | 값 |
|---|---|---|
| `Combat` | 떠나는 중이면 떠난다 → 도주 → (도주 성향이 아니면) 감지 · 능동 · 교전 | `Stance` · `Range` |
| `Patrol` | 배정된 순찰 구역을 돈다. 없으면 아래로 떨어진다 | — |
| `Wander` | 집 둘레를 서성인다 | `Radius` · `WanderByDefault` |
| `Depart` | 머문 뒤 맵 밖으로 걸어 나간다 | `Seconds` |
| `VisitCore` | 핵 근처에 들러 머물고 돌아가 사라진다 — 이것 하나가 하루 전부다 | `Seconds` |

새 목표는 `AiGoalKind` 꼬리에 더하고 `UnitBrain.BuildGoal`에 한 줄.

## 1단계 (2026-10-18) — 옮기기만 했다

역할 다섯 + 적이 패키지 여섯이 됐다(`Resources/Data/AiPackages/`). **순서와 값은 옮기기 전과 같다** — 화면에서 아무것도 안 바뀌어야 맞다.

| 패키지 | 목표 | 쓰는 곳 |
|---|---|---|
| `AiPkg_Villager` | Combat(도주, 8m) → Wander 10 | `Villager` |
| `AiPkg_Guard` | Combat(교전, 8m) → Patrol → Wander 12 | `Guard` |
| `AiPkg_Drifter` | Combat(도주, 10m) → Depart 600초 → Wander 22 | `Drifter` |
| `AiPkg_ShopMerchant` | 장사 · Combat(도주, 8m) → Wander(기본 꺼짐) | `Npc_Merchant` |
| `AiPkg_Peddler` | 장사 · VisitCore 420초 | `Npc_Peddler` |
| `AiPkg_Enemy` | Combat(교전) → Wander 12 | 역할 없는 남의 폰 전부(`AiPackages.Enemy`) |

- `NpcData`의 `WanderRadius` · `FleeDetectRange` · `StayDuration`은 지웠다(값은 위 표로 옮겼다).
- Play 시작 때 `[AI 패키지] 역할 5 · 패키지 6 · 빠진 것 없음`이 뜬다(`AiPackages.Check`).

## 2단계 (다음) — 새 목표 · 조건 · 저작 자리

- **조건** — 목표마다 시간대(낮·밤 · `GameTimeManager`)와 장소를 단다. *"밤에는 모닥불 곁"*이 이것이다.
- **새 목표 후보** — 지점 지키기(서서 바라본다) · 두 지점 사이 순찰(순찰 구역 없이) · 불 곁에 머물기 · 잠자기.
- **저작 자리** — 저작 씬에 놓은 폰과 야생 무리 표(`WildSpawnEntry`)에서 패키지를 고른다. 역할 에셋의 패키지는 그때 기본값이 된다.
- 무엇을 넣을지는 사용자와 정한다.

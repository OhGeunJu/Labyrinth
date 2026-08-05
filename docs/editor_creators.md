# Editor UI Creator 패턴

`DungeonColony/` 메뉴 아이템으로 씬에 UI를 자동 생성하는 에디터 스크립트 형식.

## 기본 구조

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class XxxUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    // 프리팹 저장 경로가 필요하면:
    // const string PrefabDir  = "Assets/3_Prefab/UI";
    // const string EntryPath  = "Assets/3_Prefab/UI/XxxEntry.prefab";

    [MenuItem("DungeonColony/Create Xxx UI")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        // UI 생성 ...

        AssetDatabase.SaveAssets(); // 프리팹 저장이 있을 때만
        Debug.Log("[XxxUICreator] 생성 완료");
    }
}
```

## 공통 헬퍼 (매 스크립트에 복붙)

```csharp
static Canvas GetOrCreateCanvas()
{
    var canvas = Object.FindFirstObjectByType<Canvas>();
    if (canvas != null) return canvas;
    var go = new GameObject("Canvas");
    canvas = go.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    go.AddComponent<CanvasScaler>();
    go.AddComponent<GraphicRaycaster>();
    return canvas;
}

static GameObject MakeRect(string name, Transform parent)
{
    var go = new GameObject(name);
    go.AddComponent<RectTransform>();
    if (parent != null) go.transform.SetParent(parent, false);
    return go;
}

static void SetStretch(GameObject go)
{
    var rt = go.GetComponent<RectTransform>();
    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
    rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
}

static void ApplyFont(TextMeshProUGUI tmp)
{
    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
    if (font != null) tmp.font = font;
}

// 프리팹 저장이 필요할 때
static void EnsureFolder(string path)
{
    var parts = path.Split('/');
    string cur = parts[0];
    for (int i = 1; i < parts.Length; i++)
    {
        string next = cur + "/" + parts[i];
        if (!AssetDatabase.IsValidFolder(next))
            AssetDatabase.CreateFolder(cur, parts[i]);
        cur = next;
    }
}
```

## 레이아웃 패턴

### 패널 배치 앵커 기준점

| 위치 | anchorMin | anchorMax | pivot |
|---|---|---|---|
| 화면 중앙 | (0.5, 0.5) | (0.5, 0.5) | (0.5, 0.5) |
| 우측 하단 | (1, 0) | (1, 0) | (1, 0) |
| 좌측 하단 | (0, 0) | (0, 0) | (0, 0) |
| 전체 스트레치 | (0, 0) | (1, 1) | — |

### 헤더 바 (상단 고정 N px)

```csharp
var header   = MakeRect("Header", panel.transform);
var headerRT = header.GetComponent<RectTransform>();
headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
headerRT.pivot     = new Vector2(0.5f, 1);
headerRT.anchoredPosition = Vector2.zero;
headerRT.sizeDelta = new Vector2(0, 48); // 높이
header.AddComponent<Image>().color = new Color(0.05098f, 0.05098f, 0.05098f, 1f); // #0D0D0D (고정)
```

> **헤더 색은 #0D0D0D 고정**, 그리고 헤더는 위 예시처럼 패널 상단을 **좌우 풀폭으로 꽉 채워야** 한다(anchorMin (0,1) ~ anchorMax (1,1)). 헤더를 패널의 VerticalLayoutGroup 자식으로 넣으면 패딩 때문에 패널 바탕이 헤더 위·옆으로 비친다 — 반드시 레이아웃 그룹 **밖에서** 앵커링하고, 나머지 콘텐츠는 헤더 아래를 채우는 별도 Content 컨테이너(`offsetMax = (0, -headerH)`)에 둘 것.

### 닫기 버튼 (우측 상단 28×28, LayoutGroup 없이)

```csharp
var closeGO = MakeRect("CloseButton", header.transform);
var closeRT = closeGO.GetComponent<RectTransform>();
closeRT.anchorMin = new Vector2(1, 1); closeRT.anchorMax = new Vector2(1, 1);
closeRT.pivot     = new Vector2(1, 1);
closeRT.anchoredPosition = new Vector2(-6, -6);
closeRT.sizeDelta = new Vector2(28, 28);
closeGO.AddComponent<Image>().color = new Color(0.50196f, 0.14902f, 0.14902f, 1f); // #802626 (고정)
closeGO.AddComponent<Button>();
// 텍스트
var cTxtGO = MakeRect("Text", closeGO.transform); SetStretch(cTxtGO);
var cTMP = cTxtGO.AddComponent<TextMeshProUGUI>();
cTMP.text = "X"; cTMP.fontSize = 13;
cTMP.alignment = TextAlignmentOptions.Center; cTMP.color = Color.white;
ApplyFont(cTMP);
```

> **규칙 1:** 헤더 안에 HorizontalLayoutGroup 사용 금지. 닫기 버튼을 LayoutGroup에 넣으면 크기가 이상하게 설정된다. 타이틀 텍스트와 닫기 버튼 모두 RectTransform 앵커로 수동 배치할 것.
>
> **규칙 2:** 닫기 버튼 텍스트는 반드시 `"X"` 사용. `"✕"` 특수문자는 NanumGothic SDF 폰트와 호환되지 않아 렌더링이 깨진다. 닫기 버튼 배경색은 **#802626 고정**.
>
> **폰트 체인 (2026-08-01 확정)** — `Project Settings → TextMesh Pro`:
> ```
> Default Font Asset : NanumGothic SDF
> Fallbacks          : [1] NanumGothic SDF   ← 한글
>                      [2] LiberationSans SDF ← 도형·기호(█ ░ ■ 등)
> ```
> **폴백이 두 단계인 이유**: 기본값만 NanumGothic으로 바꾸면 이미 씬에 만들어져 LiberationSans를 쓰는 텍스트가 한글에서 □가 되고, 반대로 NanumGothic만 두면 `UnitHPUI`의 막대(`█`/`░`)가 □가 된다. 둘 다 폴백에 넣어야 양쪽이 산다.
> 그래도 **새로 쓰는 UI 텍스트에는 특수문자를 쓰지 말 것** — 폴백은 이미 있는 것을 살리는 안전망이지 허가가 아니다. 폰트가 섞이면 자간·기준선이 어긋난다.
>
> **닫기 버튼만의 얘기가 아니다 — UI 텍스트 전반에 특수문자를 쓰지 말 것.** NanumGothic SDF는 한글+ASCII 위주라 도형·화살표 글리프가 대부분 없고, 없으면 조용히 `□`로 떨어진다(콘솔에 `The character with Unicode value \uXXXX was not found` 경고). 확인된 사례: `✕`(U+2715), `▸`(U+25B8). **머리표·아이콘 같은 장식은 글자 대신 `Image`(색 막대/사각형)로 만들 것** — 폰트 커버리지와 무관해진다. 굳이 글자를 써야 하면 ASCII로.
>
> **규칙 3:** 닫기 동작은 공용 `CloseButton` 컴포넌트(`Assets/1_Script/UI/CloseButton.cs`)로 처리한다. 닫기 버튼 GameObject에 컴포넌트를 붙이고 `target`에 닫을 패널을 연결할 것. 패널 스크립트에 별도 close 메서드/리스너를 만들지 말 것.
>
> ```csharp
> var closeComp = closeGO.AddComponent<CloseButton>();
> var closeSO   = new SerializedObject(closeComp);
> closeSO.FindProperty("target").objectReferenceValue = panel;
> closeSO.ApplyModifiedProperties();
> ```

### 하단 바 (하단 고정 N px)

```csharp
var barGO = MakeRect("BottomBar", panel.transform);
var barRT = barGO.GetComponent<RectTransform>();
barRT.anchorMin = new Vector2(0, 0); barRT.anchorMax = new Vector2(1, 0);
barRT.pivot     = new Vector2(0.5f, 0);
barRT.anchoredPosition = Vector2.zero;
barRT.sizeDelta = new Vector2(0, 48);
barGO.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f);
```

### 스크롤 뷰 (헤더/하단 바 사이 채우기)

```csharp
var sv   = MakeRect("ScrollView", panel.transform);
var svRT = sv.GetComponent<RectTransform>();
svRT.anchorMin = new Vector2(0, 0); svRT.anchorMax = new Vector2(1, 1);
svRT.offsetMin = new Vector2(0, bottomOffset);  // 하단 바 높이
svRT.offsetMax = new Vector2(0, -topOffset);    // 헤더 높이

var sr = sv.AddComponent<ScrollRect>();
sr.horizontal = false; sr.vertical = true;

var vp = MakeRect("Viewport", sv.transform);
SetStretch(vp);
vp.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
vp.AddComponent<Mask>().showMaskGraphic = false;
sr.viewport = vp.GetComponent<RectTransform>();

var content   = MakeRect("Content", vp.transform);
var contentRT = content.GetComponent<RectTransform>();
contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
contentRT.pivot     = new Vector2(0.5f, 1);
contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = Vector2.zero;
var vg = content.AddComponent<VerticalLayoutGroup>();
vg.childControlWidth = vg.childControlHeight = true;
vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
vg.spacing = 2f; vg.padding = new RectOffset(8, 8, 4, 4);
content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
sr.content = contentRT;
```

### 액션 버튼 (우측 고정)

```csharp
var btnGO = MakeRect("ActionButton", bar.transform);
var btnRT = btnGO.GetComponent<RectTransform>();
btnRT.anchorMin = new Vector2(1, 0.5f); btnRT.anchorMax = new Vector2(1, 0.5f);
btnRT.pivot     = new Vector2(1, 0.5f);
btnRT.anchoredPosition = new Vector2(-12, 0);
btnRT.sizeDelta = new Vector2(120, 34);
btnGO.AddComponent<Image>().color = bgColor;
var btn = btnGO.AddComponent<Button>();
btn.interactable = false;
btn.navigation   = new Navigation { mode = Navigation.Mode.None };
```

## 컴포넌트 SerializedObject 연결

```csharp
var comp = root.AddComponent<XxxPanel>();
var so   = new SerializedObject(comp);
so.FindProperty("panel").objectReferenceValue      = panel;
so.FindProperty("titleText").objectReferenceValue  = titleTMP;
// ... 기타 필드
so.ApplyModifiedProperties();
```

## 씬 기존 오브젝트 업데이트 패턴

씬에 이미 있는 컴포넌트에 요소를 추가하는 경우:

```csharp
var target = Object.FindFirstObjectByType<XxxPanel>();
if (target == null) { Debug.LogError("씬에 XxxPanel이 없습니다."); return; }

var so = new SerializedObject(target);
// panel 필드로 패널 루트 찾기
var panelGO = so.FindProperty("panel").objectReferenceValue as GameObject;

// 기존 요소 제거 후 재생성
var existing = panelGO.transform.Find("NewElement");
if (existing != null) Object.DestroyImmediate(existing.gameObject);

// ... 새 요소 생성 및 연결 ...

so.ApplyModifiedProperties();
EditorUtility.SetDirty(target);
```

## 프리팹 생성 패턴

엔트리 UI 같은 반복 사용 프리팹:

```csharp
var root = MakeRect("EntryPrefab", null); // parent = null
// ... 컴포넌트 추가 ...
var prefabGO = PrefabUtility.SaveAsPrefabAsset(root, "Assets/3_Prefab/UI/Entry.prefab");
Object.DestroyImmediate(root);
return prefabGO.GetComponent<EntryUI>();
```

## 모양은 씬이, 코드는 데이터만 (반복 항목 = 템플릿 복제)

목록·그리드처럼 항목이 반복되는 UI는 **행 하나를 씬에 만들어 두고 런타임에 그걸 복제**한다. 코드가 모양을 만들면 안 된다.

```
Content
 └ RowTemplate   ← 편집 중 보이는 그 행. 런타임에 SetActive(false) + Instantiate 원본
```

- 런타임 컴포넌트는 **데이터 연결만** 한다(`PawnRosterRowUI.Bind`: 이름·초상화·상태·클릭). 여백·폰트·색·크기는 전부 씬 오브젝트가 갖는다.
- 이렇게 하면 **편집 중 모양 = 게임 모양**이 구조적으로 보장된다. 미리보기를 따로 그리는 방식은 반드시 실제와 어긋난다.
- 요소는 앵커로 붙일 것 — 이름 칸을 좌우 스트레치로 두면 패널 폭만 바꿔도 목록이 알아서 늘어난다(코드 계산 불필요).
- 편집 중 생성물은 씬 폰트 상속이 안 되므로 생성기가 `ApplyFont`로 한글 폰트를 직접 박을 것.
- 프리팹 대신 씬 안 템플릿을 쓰는 이유: 그 패널에서만 쓰는 행이라 에셋으로 분리할 이유가 없고, 씬에서 바로 보면서 고칠 수 있다.

## 생성기의 수명 — 다 쓰면 지운다

생성기는 **씬을 한 번 짓기 위한 일회성 도구**다. 산출물이 씬/에셋에 자리를 잡으면 스크립트는 지운다. 남겨두면 `DungeonColony` 메뉴만 불어나고, 실제 모양은 씬에 있는데 코드가 정본인 것처럼 보여 헷갈린다(→ "모양은 씬이, 코드는 데이터만").

- 이후 수정은 **인스펙터에서** 한다. 그러라고 헤더·스크롤·행 템플릿까지 씬 오브젝트로 만들어 두는 것이다.
- 다시 지어야 하면 git 이력에서 꺼내거나, 없으면 다시 쓴다. 산출물(씬·에셋)이 정본이므로 생성기가 사라져도 잃는 건 없다.
- 아래 "지어진 UI" 표는 **무엇이 어떤 컴포넌트로 씬에 있는지의 기록**이지, 지금 쓸 수 있는 메뉴 목록이 아니다.

### 지어진 UI (기록)

| 씬 오브젝트 | 컴포넌트 | 비고 |
|---|---|---|
| `Storage_UI` | `StorageUI` | 창고 필터/그리드 |
| `Bed_UI` | `BedUI` | 수면 배정 |
| `Farm_UI` | `FarmUI` | 농장 관리(씨앗 선택·성장·수분) |
| `PatrolZone_UI` | `PatrolZoneUI` | 순찰 구역 |
| `EventNotification_UI` | `EventNotificationUI` | 이벤트 알림 + **조작 거절 알림**(`Notify(title, message)`) |
| `Merchant_UI` | `MerchantUI` | 상인 재고(구매), `InventoryUI` 연동. 목록↔격자 전환 |
| `Inventory_UI` | `InventoryUI` | 인벤토리+장착슬롯+액션바(판매/옮기기)+`CarryBar`(무게·짐내림·잠금) |
| `CorpseInventory_UI` | `CorpseInventoryUI` | 시체 루팅(+EquipRow 6슬롯), `InventoryUI` 연동 |
| `ItemInfo_UI` | `ItemInfoUI` | 아이템 툴팁. **커서를 따라다니므로 부모가 캔버스와 같은 크기여야 한다** |
| `CraftingTable_UI` | `CraftingTableUI` | 마스터-디테일 + 연료 게이지·산출물 그리드·수량 주문 |
| `ResearchTable_UI` | `ResearchTableUI` | 마스터-디테일. [연구] 버튼과 진행 게이지가 **같은 칸을 번갈아 쓴다** |
| `ResourceNode_UI` | `ResourceNodeUI` | 우측 패널, 상단 #0D0D0D 헤더+CloseButton, 재고/배정/이송/운반임계값 |
| `PawnRoster_UI` | `PawnRosterUI` | 우측 하단 세로 명부(340×760). 행은 씬의 **`RowTemplate`을 복제** |
| `Colony_UI` | `ColonyUI` · `ColonyRowUI` | 중앙 표 패널(P키). **행·열 라벨은 런타임 생성**(작업 카테고리에서 파생) |
| `UnitStats_UI` · GradeUpNotification | `UnitStatsUI` · `GradeUpNotificationUI` | 스탯 패널(C키) / 등급 승급 알림 |
| `UnitHP_UI` | `UnitHPUI` | 좌측 하단 HP/혈액/신체부위 |
| `DungeonCore_UI` | `DungeonCoreUI` | 코어 정보/소환/[핵 이전] |
| `BuildMode_UI` | `BuildModeUI` | 하단 건설 바 |
| `PawnContextMenu_UI` | `PawnContextMenuUI` | 우클릭 선택지 팝업. 줄은 씬의 `OptionTemplate` 복제 |
| `Expedition_UI` | `ExpeditionUI` · `ExpeditionRowUI` | 원정대 편성(G키). 행은 씬의 `RowTemplate` 복제 |
| `Canvas/WorldUI/WorldMenu_UI` | `WorldMenuUI` | 월드맵 도착 메뉴(M&B식, 화면 왼쪽 고정). **`WorldUI`는 창이 아니라 월드 전용 UI 묶음** — `GameModeManager`가 이것만 반대로 켠다. 머리표는 글자가 아니라 색 막대 `Image`인 `Marker` 자식 |
| `Time_UI/TimeBar` | `TimeHUD` | 상단 가운데 `일차 / 시각 / 낮·밤` + 배속 버튼 4개 |
| MerchantStockTile | `MerchantStockEntryUI` | 격자 보기 항목(96×112). 목록 행과 **같은 컴포넌트**, 배치만 다름 |
| Settlement_0 | (루트 GameObject) | 필드 오브젝트 전체의 부모. `SettlementManager.settlements[0]`(WorldNode 8,8) |
| 3_Prefab/CraftingTable_{Weapon,Smelt,Cook} · ResearchTable | — | **작업대는 `tableType` 때문에 종류마다 프리팹이 하나씩** 필요하다 |

**창 오브젝트 이름 규약(2026-08-04 확정)** — 이름은 **컴포넌트에서 유도한다.**

```
<기준>_UI      ← 컴포넌트가 붙은, 늘 켜져 있는 부모
<기준>_Panel   ← 보였다 사라지는 자식 (`panel` 필드가 가리키는 것)
```

기준 = 컴포넌트 클래스 이름에서 꼬리 `UI`를 뗀 것 —
`StorageUI` → `Storage_UI`/`Storage_Panel`, `PawnContextMenuUI` → `PawnContextMenu_UI`/`PawnContextMenu_Panel`.
**컴포넌트 접미사도 `UI` 하나로 통일했다**(옛 `XxxPanel` 8개 개명).

전에는 세 갈래가 섞여 있었다: 자식에 밑줄이 있는 것(`UnitStats_Panel`), 없는 것(`BedPanel`),
루트에 밑줄이 없는 것(`DungeonCoreUI`), 짝 이름이 아예 다른 것(`Storage_UI`↔`StorageBuildingPanel`).
마지막 것 때문에 **이름으로는 짝을 짐작할 수 없다** — `panel` 필드가 짝을 정확히 알고 있으므로
거기서 읽는다. 도구는 `DungeonColony → Rename UI Objects (Preview / Apply)`.

이름은 `fileID` 참조에 영향을 주지 않고, 이 프로젝트에는 **창을 문자열로 찾는 코드가 없다**
(2026-08-04 전수 확인 — `transform.Find`는 전부 창 안쪽 부품용이고 `GameObject.Find`는 0건).

**UI 이름 규약(2026-07-31 일괄 정리)**: 제목 바는 전부 **`TitleBar`**(옛 `TopBar`·`Header` 통일), 닫기 버튼은 **`CloseButton`**(옛 `CloseBtn`). 제목 바에는 `DraggablePanel`(target=패널), 닫기 버튼에는 공용 `CloseButton` 스크립트(target=패널)가 붙어 있다. 단, `ItemInfoUI/Header`는 툴팁 안의 **섹션 헤더**라 제외했다 — 제목 바 판정은 이름이 아니라 **깊이**(`Canvas > *_UI > *Panel > 바`)로 한다.

## 남아 있는 도구

| 메뉴 | 하는 일 | 왜 남겼나 |
|---|---|---|
| `World Map Editor` | 월드맵(`WorldMapData`)을 격자에 칠해 저작하는 `EditorWindow`. 브러시 3종(바이옴/내용물/시작 노드), 드래그 페인트 1회 = Undo 1덩어리, 크기 변경 시 내용 보존, [새로 만들기]/[씬에 연결]. 여기서 칠하는 건 **논리 정보뿐** — 지형(높이·생김새)은 씬에 직접 만들어 붙인다. **절차 생성 없음.** | 일회성이 아니라 **상시 저작 도구** |
| `Setup World Map Scene` | 월드맵에 필요한 오브젝트/컴포넌트를 **없는 것만 채우고 참조만 잇는다**(WorldMap·SettlementManager·GameModeManager·WorldMapView·MapRoot·WorldCamera·WorldMapGrid + 에셋 배선). **아무것도 지우지 않는다** — MapRoot 아래 지형과 손으로 맞춘 카메라가 살아남아야 하므로. 여러 번 실행해도 안전. | 배선이 끊길 때마다 다시 돌리는 **복구 도구** |
| `Create Biome Content Assets` | `Resources/Data/Biomes/`에 바이옴별 `BiomeContentData` 스캐폴드 생성. 이미 있으면 건너뛴다. | 실행됨(에셋 7종 생성). 바이옴을 늘리면 다시 돌린다 |
| `Toggle Movement Diagnostics` | 이동 계측 on/off. | 이동 개편 Play Mode 검증이 아직 안 끝났다 |
| `Relayout Research Detail Panel` | 연구대 디테일 패널의 칸을 다시 나눈다(제목·시간·설명·선행/해금·재료를 위에서 쌓고, 게이지·[연구] 버튼은 바닥 고정). **아무것도 만들거나 지우지 않고** RectTransform만 다시 잡으므로 여러 번 실행해도 안전하다. 글자 줄바꿈·자동 축소도 같이 못 박는다 — 칸만 넓히면 자동 축소가 꺼져 있어 여전히 잘린다 | 칸이 어긋날 때 다시 돌리는 **복구 도구** |
| `Create Research Assets` | `Resources/Data/Research/`에 연구 에셋 생성(연금술·원소 공학). **없는 것만 만든다.** `UnlockedRecipes`는 비워 둔다 — JSON 레시피가 `requiredResearchId`로 자기를 가리키므로, 양쪽에 적으면 목록이 어긋났을 때 어느 쪽이 맞는지 알 수 없다 | 연구를 늘릴 때 다시 돌리는 **저작 도구** |
| `Create New Buildings` | `3_Prefab` 프리팹 + `Resources/Data/Buildings` 데이터를 한 쌍으로 생성(벽·큰 창고). **없는 것만 만든다.** 레지스트리 방식이라 씬은 안 건드린다 | 건물을 늘릴 때 다시 돌리는 **저작 도구** |
| `Resize Buildings And Rebuild Models` | `BuildingData.Size`를 정하고 **모델을 그 크기로 다시 짓는다**(작업대 8종·연구대·침대·창고). 기존 모델은 조각마다 회전이 있어 부모를 비균등 스케일하면 찌그러진다 — 늘리는 대신 파라미터로 다시 조립한다. **`WorkSpot`은 안 지우고** 새 크기의 앞 가장자리로 옮긴다. 제자리 수정이라 `BuildingData.Prefab` 참조가 유지된다 | 크기를 바꿀 때마다 다시 돌리는 **저작 도구** |
| `Setup Work Spots` | `3_Prefab`의 작업대·연구대 프리팹에 **작업 자리(`WorkSpot`)** 를 붙인다(건물 앞 1칸, 반투명 표식 포함). **이미 있으면 건너뛴다.** 프리팹은 **제자리 수정**(`LoadPrefabContents` → `SaveAsPrefabAsset` 같은 경로)이라 `BuildingData.Prefab` 참조가 안 끊긴다 | 작업대를 늘릴 때 다시 돌리는 **배선 도구** |
| `Upgrade Crafting Table Panel` | 작업대 패널에 연료 게이지·산출물 그리드·운반 허용 버튼을 붙이고 `CraftingTableUI`에 배선. 새 구역을 **패널 아래에 매달아** 기존 자식 좌표를 안 건드린다. 다시 실행하면 이전 구역을 지우고 새로 짓는다 | 산출물/연료 배치를 다시 만들 때 쓰는 **재생성 도구** |
| `Migrate Minion Prefabs` | 씬의 `DungeonCore.minionEntries` 배선을 레지스트리로 이전 — 씬 오브젝트를 `3_Prefab/Minion_*.prefab`으로 뜨고(**있으면 안 만듦**) `MinionData.Prefab`에 연결한 뒤 `minionEntries`를 비운다. **전부 성공했을 때만** 비운다(대체물 없이 지우면 소환이 죽는다). 뜰 때 **루트를 켠다** — 씬 템플릿이 꺼져 있어 그대로 뜨면 소환된 하수인이 꺼진 채 나온다 | 하수인 종류를 늘릴 때 다시 돌리는 **이전 도구** |
| `Fix Window Structure` | 창 컴포넌트를 **자기 전용 `<기준>_UI` 오브젝트**로 옮기고 패널을 그 자식으로 내린다. 어긋나는 꼴 둘을 편다 — (1) 컴포넌트가 **패널 자신에** 붙은 경우(`ItemInfoUI`: 꺼진 채 저장돼 `Awake`가 안 돌아 툴팁이 한 번도 안 떴다) → 부모를 바깥에 세운다, (2) 컴포넌트가 **남의 오브젝트에 얹힌** 경우(`WorldMenuUI`가 월드 UI 묶음 `WorldUI`에) → 그 안쪽에 세운다. 컴포넌트는 **복사해 붙인다**(`ComponentUtility`) — 인스펙터 참조를 들고 가야 해서. 부모는 제 부모와 같은 크기로 펼친다(자식의 `anchoredPosition`이 뜻을 유지해야 한다). **대상은 손으로 적는다** — "이 오브젝트가 다른 역할도 하는가"는 자동으로 알 수 없다. 여러 번 실행해도 안전 | 구조가 어긋났을 때 다시 돌리는 **복구 도구** |
| `Rename UI Objects (Preview / Apply)` | 창 오브젝트 이름을 `<기준>_UI`/`<기준>_Panel`로 통일한다. `panel` 필드에서 짝을 읽고 컴포넌트 타입에서 이름을 유도하므로 **표를 손으로 적지 않는다**. Preview는 계획만 찍고 아무것도 안 바꾼다. 프리팹 소속·구조용 묶음(`WorldUI`)은 건너뛴다 | 창을 늘릴 때 다시 돌리는 **정리 도구** |
| `Create Carry Bar` | 배낭 창(I키) 액션 바 위에 `CarryBar` 한 줄을 세운다 — `무게 12/15kg` + [짐내림] + [잠금] — 그리고 `InventoryUI`에 배선한다. 다시 실행하면 이전 것을 지우고 새로 짓는다(자리를 손봤다면 다시 손봐야 한다). 액션 바와 **같은 부모**에 두므로 액션 바를 옮기면 따라간다 | 배낭 창 하단을 다시 지을 때 쓰는 **재생성 도구** |
| `Build Mod AssetBundle` | 프로젝트 창에서 고른 모델·프리팹·텍스처를 에셋 번들로 구워 모드 폴더에 넣는다. 번들 이름 = **모드 폴더 이름**(모드마다 하나, 다시 구우면 덮어씀). 읽는 쪽은 `ModAssets`. | **유니티가 런타임에 .fbx/.obj를 못 읽어서** 모드가 모델을 넣으려면 번들이 유일한 길이다. 모드를 만들 때마다 쓰는 **상시 도구** |

**씬을 건드리는 생성기는 씬을 직접 저장해야 한다.** `AssetDatabase.SaveAssets()`는 **에셋만** 저장한다 — 씬 컴포넌트를 고치고 `EditorUtility.SetDirty`만 부르면 씬이 더티로 표시되지 않아 저장 대상에서 빠지고, Unity를 닫는 순간 작업이 통째로 날아간다. 2026-07-31에 건설 목록 등록 15건과 연구 게이지 배선이 이렇게 사라졌다.
```csharp
EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
EditorSceneManager.SaveOpenScenes();
```
프리팹·SO는 에셋이라 `SaveAssets()`로 남는다 — **씬에 사는 것만** 이 처리가 필요하다(`BuildModeManager.placeableBuildings`, UI 배선 등).

**기존 프리팹의 모양을 고칠 땐 반드시 제자리 수정** — `PrefabUtility.LoadPrefabContents` → 내용 교체 → `SaveAsPrefabAsset`(같은 경로) → `UnloadPrefabContents`.
`BuildingData`·`ResourceNodeData`가 프리팹을 **`guid` + 루트 GameObject의 `fileID`**로 물고 있어서, 같은 경로에 새로 저장하면 GUID가 같아도 **루트 fileID가 바뀌어 참조가 끊긴다.** 제자리 수정이면 루트와 그 위의 컴포넌트(`ResourceNode`·`CraftingTable`·`Bed`…)가 살아남는다.

**프리팹 기본 위치를 바꾸기 전엔 씬 인스턴스의 오버라이드를 확인할 것.** 인스턴스가 `m_LocalPosition`을 오버라이드하고 있으면 프리팹 기본값을 바꿔도 안 움직이지만, 안 하고 있으면 씬의 오브젝트가 전부 따라 움직인다. 확인법:
```
grep "m_SourcePrefab: {fileID: 100100000, guid: <프리팹guid>" Scene1.unity   # 인스턴스 수
그 블록의 m_Modifications 에 propertyPath: m_LocalPosition.y 가 있는지
```

**건설 가능한 것을 늘리는 생성기의 규약** — 셋 다 이렇게 짰다:
- **없는 것만 만든다.** 이미 있는 프리팹/에셋은 손대지 않는다(인스펙터에서 모양·값을 손봤을 수 있다). 여러 번 실행해도 안전.
- 건설 목록(`BuildModeManager.placeableBuildings`) 등록은 **중복을 걸러 덧붙이기만** 한다. 배열을 통째로 갈아치우면 손으로 넣은 항목이 날아간다.
- 씬 오브젝트는 `FindObjectsInactive.Include`로 찾는다.
- **프리팹 루트 `localPosition.y`는 0.** 양수면 `BuildModeManager`가 "저작 부양 높이"로 해석해 그만큼 띄운다 — 조각이 여럿인 모델은 높이가 제각각이라 맞지 않는다. 0으로 두면 `SnapOnSurface`가 렌더러 바닥면을 표면에 맞춰 준다.
- **콜라이더는 루트에 하나.** 조각마다 달면 `SelectionManager`의 단일 레이캐스트 라우팅이 지저분해진다(조각을 맞고 건물을 못 찾는다).

**저작 창의 색은 구역 표시색이지 지형색이 아니다** — 게임 화면의 지형은 저작한 Terrain의 텍스처가 갖는다. `BiomeExt.Color()` / `WorldNodeContentExt.Color()`는 저작 격자와 (프리팹 없는) 기본 마커가 공유하는 단일 출처.

**씬을 훑을 땐 `FindObjectsInactive.Include`를 기본으로** — 이 씬은 필드 오브젝트가 `Settlement_0` 아래에 있고 그중 상당수(`ResearchTable` · `CraftingTable` · `Terrain` · 적 유닛 등)가 **꺼진 채로 저장돼 있다.** `FindFirstObjectByType<T>()`는 비활성을 제외하므로 "씬에 없습니다"라고 잘못 판단한다. 2026-07-31에 이걸로 연구대를 못 찾은 채 연구 에셋을 지워 목록에 빈 항목을 남겼다. 배선·정리 도구는 예외 없이 비활성을 포함해 찾고, **여러 개일 수 있으니 `FindObjectsByType`으로 전부 처리**할 것.

**분류는 전역 화이트리스트로** — `SettlementRootCreator.GlobalTypes`에 없는 루트는 전부 필드로 본다. 새로 만드는 씬 오브젝트는 대개 필드 것이라 이 기본값이 맞고, 전역 매니저를 추가했다면 그 목록에 넣어야 한다. 자동 분류를 그냥 믿지 않도록 **이동/유지 목록을 로그로 출력**하고 `Undo`를 지원한다.

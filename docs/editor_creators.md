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

### 뿌리는 화면 전체, 자리는 판이 든다 (2026-09-08 사용자 결정)

`Xxx_UI`(뿌리)는 **캔버스 전체로 늘린다**(anchorMin (0,0) ~ anchorMax (1,1), offset 0).
**자리와 크기는 자식 `Xxx_Panel`이 든다.**

```
Canvas
 └ Xxx_UI      ← 스크립트만. 전체 스트레치. 늘 켜져 있다
    └ Xxx_Panel ← 앵커·자리·크기·바탕색. 보였다 사라지는 것은 이쪽
```

씬의 창 서른둘 중 **스물하나가 이미 그 모양**이었다(2026-09-08에 셌다). 나머지는
뿌리에 자리가 박혀 있었는데, 그것이 곧 이 규칙이 필요한 이유다:

- **만지는 자리가 하나여야 한다.** 뿌리에도 자리가 있고 판에도 있으면 *어느 쪽을 옮겨야
  하나*가 창마다 달라진다 — 지금 씬이 정확히 그 상태다.
- **해상도가 바뀌어도 안 밀린다.** 뿌리가 점 하나로 앵커되면 그 점만 앵커를 따라가고
  안쪽은 절대 좌표라, 다른 해상도에서 판이 화면 모서리를 놓친다.
- **커서를 따라다니는 창**(`ItemInfo_UI`·`HoverTooltip_UI`)은 뿌리가 캔버스와 같은 크기여야
  좌표가 맞는다. 그 둘만 예외로 두는 대신 **전부 그렇게 둔다** — 예외가 있으면 새 창을 지을
  때마다 *이건 어느 쪽이더라*를 다시 재게 된다.

**2026-09-09에 남은 것을 도구로 옮겼다**(`DungeonColony/UI: 뿌리 규격 맞추기`) — 세어 보니
**열하나가 아니라 열셋**이었다(그 뒤에 둘이 더 어긋났다. **문서에 적은 개수는 센 날의
것이라 곧 뒤처진다** — 세는 일은 도구가 한다).

- **자식의 앵커와 피벗은 안 건드리고 오프셋만 다시 낸다.** 오늘 화면이 한 픽셀도 안 바뀌고
  (앵커까지 바꾸면 그건 이사가 아니라 재배치이고, 창 열셋을 하나씩 열어 봐야 확인된다),
  **판의 앵커가 그제야 뜻을 갖는다** — 지금까지는 *뿌리 안에서 어디*였는데 뿌리가 화면만
  해지면 그것이 곧 *화면 어디*다.
- 그래서 **저작한 사람이 적어 둔 것이 살아난다**: `BuildMode_Panel`은 폭 0짜리 뿌리 안에서
  1920px로 늘어나 있었는데 옮기고 나면 `offset (0,0)~(0,234)` — **해상도와 무관하게 화면
  폭을 채우는 하단 바**다. `Storage_Panel`은 화면 중앙 기준 절대좌표 `(944,-524)`였던 것이
  **우측 하단 16px 여백**이 된다.
- **고쳐 주지는 않는다.** 앵커가 애초에 어긋난 판(`ModeToolBar_UI`는 화면 아래에 있으면서
  좌상단에 앵커돼 있다)은 **그대로 옮겨진다** — 다만 뿌리가 치워진 뒤라 인스펙터에서
  그 사실이 처음으로 눈에 보인다.
- **뿌리 안에 또 뿌리가 있을 수 있다**(`ModeToolBar_UI` ⊂ `BuildMode_UI`) — 얕은 것부터
  손댄다.
- **일회성 이사 도구다** — 씬이 규격에 맞고 나면 지운다("생성기의 수명").

**생성기가 물려주는 것도 판의 자리다.** 뿌리는 늘 같은 값이라 물려줘 봐야 소용이 없고,
정작 사람이 옮긴 것은 판이다 — 뿌리만 지키면 **다시 돌릴 때마다 손으로 맞춘 자리가 날아간다**
(`ModeBarSetup`·`FormationBarSetup`이 2026-09-08에 그렇게 고쳐졌다). **크기는 여전히 안
물려준다** — 그건 버튼 수에서 나오는 값이지 손으로 정하는 값이 아니다.

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
- **같은 대상을 다루는 생성기를 둘 두지 말 것.** 어느 쪽을 돌려야 하는지 알 수 없고, 섞어 돌리면 산출물이 깨진다(실제로 연구 패널이 그랬다).
- 2026-08-06에 15개를 지웠다. 경위는 `docs/worklog.md`.
- 2026-08-22에 넷을 더 지웠다 — `CampfireCreator`·`BuildMenuUpdater`(그날 만들어 그날 다 썼다)와
  `BackstoryCreator`·`CorpseFacilityUICreator`(산출물이 에셋 36개·씬 창 셋으로 자리를 잡았다).
- 아래 "지어진 UI" 표는 **무엇이 어떤 컴포넌트로 씬에 있는지의 기록**이지, 지금 쓸 수 있는 메뉴 목록이 아니다.

### 지어진 UI (기록)

| 씬 오브젝트 | 컴포넌트 | 비고 |
|---|---|---|
| `Storage_UI` | `StorageUI` | 창고 필터/그리드 |
| ~~`Bed_UI`~~ | ~~`BedUI`~~ | **없앴다**(2026-08-12). 배정/해제는 우클릭으로, 정보는 `UnitHP_Panel`로 — 문이 둘인데 한쪽만 죄수 침대 규칙을 알고 있었다 |
| `Farm_UI` | `FarmUI` | 농장 관리(씨앗 선택·성장·수분) |
| `PatrolZone_UI` | `PatrolZoneUI` | 순찰 구역 |
| `EventNotification_UI` | `EventNotificationUI` | 이벤트 알림 + **조작 거절 알림**(`Notify(title, message)`) |
| `Merchant_UI` | `MerchantUI` | 상인 재고(구매), `InventoryUI` 연동. 목록↔격자 전환 |
| `Inventory_UI` | `InventoryUI` | 인벤토리+장착슬롯+액션바(판매/옮기기)+`CarryBar`(무게·짐내림·잠금) |
| `Grave_UI` | `GraveUI` | 무덤 — 묻힌 사람·멈춘 단계·[파낸다] |
| `Pyre_UI` | `PyreUI` | 화장터 — 진행 게이지·연료 부족 사유·[내린다] |
| `ButcherTable_UI` | `ButcherTableUI` | 도축대 — 규칙 셋(순환 버튼)·산출물 격자·운반/정리 |
| `CorpseInventory_UI` | `CorpseInventoryUI` | 시체 루팅(+EquipRow 6슬롯), `InventoryUI` 연동 |
| `ItemInfo_UI` | `ItemInfoUI` | 아이템 툴팁. **커서를 따라다니므로 부모가 캔버스와 같은 크기여야 한다** |
| `CraftingTable_UI` | `CraftingTableUI` | 마스터-디테일 + 연료 게이지·산출물 그리드·수량 주문 |
| `ResearchTable_UI` | `ResearchTableUI` | 마스터-디테일. [연구] 버튼과 진행 게이지가 **같은 칸을 번갈아 쓴다** |
| `ResourceNode_UI` | `ResourceNodeUI` | 우측 패널, 상단 #0D0D0D 헤더+CloseButton, 재고/배정/이송/운반임계값 |
| `FormationBar_UI` | `FormationBarUI` | 진형 넷(방진·횡대·종대·쐐기). **소집한 폰을 고르고 있을 때만 뜬다** — 진형이 실제로 먹는 조건과 같다. 상태를 안 들고 매 프레임 `Formation.Current`를 읽는다(키 `F1~F4`와 같은 상태) |
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
| 3_Prefab/Building/CraftingTable_{Weapon,Smelt,Cook} · ResearchTable | — | **작업대는 `tableType` 때문에 종류마다 프리팹이 하나씩** 필요하다 |

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
거기서 읽는다. 개명은 `Rename UI Objects` 도구가 한 번에 했고, 다 쓰고 지웠다(2026-08-06).

이름은 `fileID` 참조에 영향을 주지 않고, 이 프로젝트에는 **창을 문자열로 찾는 코드가 없다**
(2026-08-04 전수 확인 — `transform.Find`는 전부 창 안쪽 부품용이고 `GameObject.Find`는 0건).

> **뿌리에 `Image`를 붙이지 말 것**(2026-09-02에 두 번 어겼다 — 모드 바·밭 구역 바). `UIHelper.MakePanel`이 뿌리에 `Image`를 붙여 주므로, 창 규약을 따르려면 **떼어서 자식 `<기준>_Panel`로 옮겨야** 한다. 규약이 뿌리와 창을 가른 이유는 이름 맞추기가 아니라 **스크립트가 제 오브젝트를 끄면 `Update`가 안 돌아 다시 못 켜기 때문**이다 — 그걸 `CanvasGroup`으로 우회하면 규약만 어기고 문제는 그대로 남는다(어긴 두 곳이 정확히 그랬다).

**UI 이름 규약(2026-07-31 일괄 정리)**: 제목 바는 전부 **`TitleBar`**(옛 `TopBar`·`Header` 통일), 닫기 버튼은 **`CloseButton`**(옛 `CloseBtn`). 제목 바에는 `DraggablePanel`(target=패널), 닫기 버튼에는 공용 `CloseButton` 스크립트(target=패널)가 붙어 있다. 단, `ItemInfoUI/Header`는 툴팁 안의 **섹션 헤더**라 제외했다 — 제목 바 판정은 이름이 아니라 **깊이**(`Canvas > *_UI > *Panel > 바`)로 한다.

## 남아 있는 도구

**2026-08-06에 13개, 08-13에 넷, 08-14에 열, 08-23에 둘, 09-09에 열다섯, 09-13에 하나를 지웠다.**
산출물이 씬·에셋에 전부 자리를 잡아서다 — 아래 "생성기의 수명" 참조.

**09-09에 지운 열아홉**(53 → 34): `UiRootNormalizer`(뿌리 이사, 그날 다 썼다) ·
`TechTreeCreator` · `CampfireUpgrader` · `DungeonCoreSetup` · `GolemCreator` ·
`UpkeepCreator` · `TorchCreator` · `ShellCreator` · `FarmFurnitureCreator` ·
`WorldSiteCreator` · `ResearchPanelSetup` · `ResourceNodePanelSetup` ·
`HaulPolicyPanelSetup` · `WorldMapPanelSetup` · `ModeToolBarSetup` ·
**`FormationBarSetup` · `HudOrderSetup` · `ModeBarSetup` · `StatPanelSetup`**.

**09-13에 지운 하나**: `IronMerge`(철 티어를 강철에 합치고 은을 장비에서 뺐다 —
로드아웃 재배선·철 갑옷 삭제·철검 개명). **한 번 돌면 끝나는 이행**이라 산출물(에셋·로드아웃)이
정본이 된 뒤로는 할 일이 0이다.

**가른 잣대는 동사다.** *옮긴다 · 늘린다 · 입힌다 · 올린다*는 **한 번이면 끝난다**(이사·
확장·모델 교체·목록 등록). *만든다(없으면) · 굽는다*는 **데이터가 바뀌면 또 돈다** — 남겼다.

**UI 규격 도구(`Setup …`)는 하나도 안 남겼다**(2026-09-09, 사용자: *"이것들도 이미 다
만들었으니깐 필요 없는 거 아니야?"*). 한 번은 *"개수가 데이터에서 오니 손으로 못 맞춘다"*로
넷을 남겼는데 — **그 논리는 이 문서가 이미 기각한 것**이다(*"언젠가 또 필요할지도"는 남길
이유가 아니다*). 그리고 판이 씬에서 손으로 다듬어진 뒤에 다시 돌리는 것은 **고치는 것이
아니라 지우는 것**이라, 정작 필요해진 날에도 그냥은 못 돌린다. **규격이 자리를 잡으면 씬이
정본이고 수정은 인스펙터에서 한다** — 그러라고 헤더·스크롤·행 템플릿까지 씬 오브젝트로
만들어 두는 것이다.

딸려 오는 것 하나: **줄 수가 안 맞는 것은 런타임이 알린다**(`StatColumns` ↔ `UnitStatsUI`).
도구가 없어도 *무엇이 어긋났는지*는 콘솔에 남는다 — 그것이 도구를 대신하는 안전망이다.

**지운 도구를 가리키던 글은 과거형으로 고쳤다**(`CLAUDE.md`·`docs/build.md`·
`docs/singletons.md`·`HaulPolicyPanel.cs`·`FireArt.cs`) — 남겨 두면 **없는 메뉴를 찾게 되고,
그건 표가 아니라 거짓말이다.** 산출물(씬·에셋)이 정본이므로 **잃은 것은 없다.**

**"언젠가 또 필요할지도"는 남길 이유가 아니다**(2026-08-14). 그 말로 남긴 도구들을 실제로
재 보니 전부 할 일이 0이었다 — 씬에 손으로 놓은 건물 0개(`Snap`), 콜라이더 드리프트 0건
(`Fit`), 루트 오프셋 0건(`Zero`). **필요해지면 그때 다시 쓰는 편이, 메뉴에 열두 개를 두고
어느 것이 살아 있는지 매번 재는 것보다 싸다.** 남은 넷은 도구가 아니라 **기능**이다 —
지도 저작 · 모드 번들 · Play Mode 디버그 · 이동 계측.

**이 표는 실제 메뉴 목록이어야 한다** — 도구를 지우거나 더하면 여기서 같이 지우고 더할 것.
빠뜨리면 없는 메뉴를 찾게 되고, 그건 표가 아니라 거짓말이다.

| 메뉴 | 하는 일 | 왜 남겼나 |
|---|---|---|
| `World Map Editor` | 월드맵(`WorldMapData`)을 격자에 칠해 저작하는 `EditorWindow`. 브러시 3종(바이옴/내용물/시작 노드), 드래그 페인트 1회 = Undo 1덩어리, 크기 변경 시 내용 보존, [새로 만들기]/[씬에 연결]. 여기서 칠하는 건 **논리 정보뿐** — 지형은 씬에 직접 만들어 붙인다. **절차 생성 없음** | 지도를 그릴 때마다 쓰는 **상시 저작 도구** |
| `Build Mod AssetBundle` | 프로젝트 창에서 고른 모델·프리팹·텍스처를 에셋 번들로 구워 모드 폴더에 넣는다. 번들 이름 = **모드 폴더 이름**. 읽는 쪽은 `ModAssets` | **유니티가 런타임에 .fbx를 못 읽어서**, 모드가 프리팹·뼈대·애니메이션을 넣으려면 번들이 유일한 길이다. 지우면 모딩 기능이 함께 사라진다 |
| `Debug/Give All Items` · `Complete All Research` | 전 아이템을 **디버그 상자**(핵 옆 20×14, 모자라면 한 줄로 더 만든다·최대 12개)에 지급 — **장비는 등급 6종 각 1개, 그 밖은 일반 등급으로 최대 스택** / 연구 전부 완료 | Play Mode 검증용. **등급은 장비에만** — `StatMultiplier`가 실제로 바꾸는 건 무기 피해·갑옷 감소·값 셋뿐이고 앞의 둘이 장비 전용이라, **광석·음식은 등급이 달라도 값만 오르고 하는 일이 같다**(여섯 벌씩 주면 상자만 불고 정작 비교할 장비가 묻힌다). 상자를 **키우지 않고 늘리는** 이유: 격자가 화면보다 커지면 열어 봐도 못 본다 |
| `Debug/Add Gold` | 지갑에 5000 G(`Wallet.Add`) | `Give All Items`는 상자에 **동전을** 넣어 주는데 그건 더블클릭해야 잔고가 된다. **몸값·시장은 상자가 아니라 `Wallet.Balance`를 본다** — 월드맵에서 흥정을 시험하려는데 배낭 창을 열어 동전을 눌러야 하는 건 시험이 아니라 준비 작업이다 |
| `Debug/Arm Selected Pawn` | 하이어라키에서 고른 폰에 **무기·방패·갑옷 4부위 + 소지품 6종 + 동전 25**를 채운다. 인벤토리에 넣고 `Equipment.Equip*`을 태운다(손으로 차는 것과 같은 경로) | 적이 실제로 지니는 것은 이제 **데이터**가 정한다(`UnitData.Loot`/`Loadout`). 이건 **데이터에 없는 조합을 즉석에서 시험**하는 자리 — `Give All Items`가 내 창고를 채우고 이건 저 폰을 채운다 |
| `Debug/Raise the Dead (검은 교단)` · `(망자)` · `Age Corpses → 썩음` · `→ 뼈` | 맵의 시체를 그 자리에서 일으킨다(세력별) · 시체 나이를 옮긴다(`Corpse.SetAge`) | 부패가 신선 1일 → 썩음 3일 → 뼈라 **실기로 기다리면 한 시간이 넘는다.** 단계별로 무엇이 일어나는지(살점=그 몸 / 뼈=해골)를 보려면 나이를 옮기는 문이 있어야 한다 |
| `Create Craft Research` | **잎 연구 다섯**(가죽 세공·직조·의술·보석 세공·궁술 공방)을 만든다. 레시피 잠금은 **JSON의 `requiredResearchId`가 들고**, 이 도구는 연구 에셋만 만든다(답이 두 벌이 되지 않게) | **상시** — 값·선행을 고치면 다시 돌린다. 있으면 값만 맞춘다(guid 유지) |
| `Create Vein Research` | 광맥 연구 **열넷**(뿌리 `광맥 이식` + 열셋)을 `Resources/Data/Research/`에 짓고 `Node_*.asset` 열셋의 `RequiredResearch`를 거기 물린다. **재료는 그 광맥의 `CostItem`을 그대로 읽는다** — 표를 따로 두면 심는 값과 연구하는 값 중 한쪽만 고쳐진다. 그래프 모양(선행)은 도구 안의 `Table`이 든다 | **상시** — 광맥을 더하거나 그래프를 고치면 다시 돌린다. 있으면 값만 맞추므로(guid 유지) 몇 번 돌려도 같다 |
| `Pawn/0. Rebuild Pawn Rigs And Animations` (= `1. Bake Pawn Animations` → `2. Build Pawn Rigs`) | 폰 프리팹의 `Model` 아래에 **관절**(`PawnRig` 규격)을 세우고 살을 그 아래로 옮긴다(월드 자세 유지 — 생김새는 그대로). **두발은 사람 뼈 열다섯**(팔·다리 큐브를 반으로 갈라 팔꿈치·무릎을 둔다)이고 그 위에 `AvatarBuilder`로 **휴머노이드 아바타**(`Controller/Avatars/<프리팹>.asset`)를 세워 **받은 휴머노이드 클립**(`assetsss/Kevin Iglesias`, `HumanoidClipSet` → `Controller/Humanoid.controller`: Idle01·Walk01·Run01 블렌드 / 휘두르기 = `TreeChopping01` / Lying = 얼린 Idle)을 그대로 쓴다. 네발은 관절 회전 곡선을 직접 굽는다(`Quadruped.*`). `Animator`·`PawnAnimator`를 붙인다. **다시 돌려도 된다**(살을 되돌리고 가른 큐브를 합친 뒤 다시 세운다 · 컨트롤러·아바타는 guid 유지) | **상시** — 관절 자리 규칙이나 클립을 바꾸면 다시 돌린다. 팩에 공격 클립이 없어 나무 찍기를 쓴다(`HumanoidClipSet.AttackClip` 한 줄). 걷기 문턱(`WalkAt`·`RunAt`)은 눈으로 맞춘 값. 골렘(스킨 메시)은 건너뛴다. **팩은 git 밖**(`assetsss/`)이라 경로가 그 폴더 이름에 기댄다 |
| `Pawn/3. Create Hand Props` | 손에 드는 프롭 여덟(곡괭이·도끼·망치·칼·물뿌리개 = KI 데모 도구 / 검·활·방패 = DoubleL)을 `3_Prefab/Prop/`에 짓고(루트 `HandProp` = 쥐는 자리, 콜라이더 없음), 재질을 URP Lit로 옮기고(`2_Material/Prop/`), `Resources/Data/HandPropSet.asset`(무기 종류·일 동작 → 프리팹)을 채운다. **다시 돌려도 된다** — 다만 `HandProp`의 쥐는 값은 있으면 그대로 둔다(손으로 맞추는 값) | **상시** — 프롭을 더하면 `Props`·`Weapons`·`Tools` 표에 적고 다시 돌린다. 쥐는 회전(`GripEuler`)은 **눈으로 맞춰야 한다** |
| `Debug/Movement Diagnostics` | 이동 계측 on/off. `(Events Only)` 변형이 따로 있다 | 이동 개편 검증이 아직 안 끝났다 |
| `Bake Research Graph` | 연구 노드·선을 씬에 굽는다 | **상시** — 연구가 늘면 다시 돈다. **옮긴 자리는 안 없어진다**(있던 노드를 다시 쓴다) |
| `Debug/습격 부르기` | 습격을 지금 부르고 `RaidLord`의 상태·갈래·지휘관을 본다 | 습격은 하루 두 번 남짓이라 **기다려서는 못 본다** |
| `Building/2·3. Dress · Undress Buildings` | 건물에 모델을 입히고 되돌린다 | **되돌리는 문이 있어야 입힐 수 있다.** 번호가 2·3에서 시작하는 것은 1·4·5를 지운 자리다 |
| `World/B. Create Metal Ladder` | 광석·주괴 값 전부(구리 8 · 주석 10 · 철 20 · 티타늄 45 · 미스릴 90 / 청동 70 · 강철 130 · 티타늄 250 · 미스릴 470) · 티타늄/미스릴 광맥 데이터 · **지하 여덟 층의 광맥 풀** · 명장 단조 재료 | **상시** — 값이나 배선을 고치면 다시 돌린다. 광맥 풀은 **통째로 다시 쓴다**(더하면 지상 시절 잔재가 남는다) |
| `World/D. Rebuild Vein Art` | 광맥 열다섯 + 돌 + 바닥 돌의 **모양**을 다시 굽는다. 금속은 **깎아 만든 바위에 광석 서른 개가 찔끔 박힌 것**, 보석은 **기둥 다발**. 메시(`Vein_Boulder`·`Vein_Shard`·`Vein_Nugget`)와 재질도 같이 굽고 **안 쓰는 옛 색 재질을 치운다** | **상시** — 색·모양을 고치면 다시 돌린다. **처음부터 다시 짓지 않고 자식만 갈아 끼운다**(`PlacedBuilding`·`PowerUser`를 떨어뜨려 광맥이 통째로 안 보인 적이 있다) |
| `Terrain/…` · `World/…` (스물넷) | 지하 세계를 짓는 파이프라인 — 바이옴·팔레트·장소 배치·굽기·저작 씬·버섯·전력·야생 매장량·이력 | **상시** — 세계를 다시 세우는 문이 `World → 1. Rebuild Underworld`이고 나머지는 그 단계들이다(`CLAUDE.md`에 순서가 적혀 있다). **번호에 빈 자리가 있다**(`Terrain/4`·`Terrain/6`·`Terrain/B`·`Terrain/C`·`Terrain/D`·`Terrain/E`·`Building/1`) — 지운 도구의 자리이고, **번호는 문서가 인용하므로 다시 매기지 않는다**. `B`(침식 높이맵)·`C`(세계 색조)·`D`(산·분지)·`E`(세계 미리보기)는 **지상 시절의 것이라 2026-09-12에 지웠다** |

### 메뉴는 겹치지 않고, 디버그는 한자리에 (2026-09-09)

도구를 지우고 나서 남은 쉰둘의 메뉴 경로를 정리했다. **같은 번호가 여덟 자리에서 겹치고
있었다** — 겹친 번호는 *어느 것을 먼저 돌리나*에 답하지 않으므로 번호가 아니라 장식이다.

- **디버그는 하위 메뉴로 묶었다** — `Debug: X` 여덟 개가 최상위에 흩어져 있던 것을
  **`Debug/X`** 아래로. 이동 계측 둘(`Toggle Movement Diagnostics`)도 디버그라 같이 넣었다.
- **저작 씬에서 손으로 짓는 일은 갈라 냈다** — `Authoring/`(터레인 보이기·감추기·선택을
  장소로 저장·저작 씬 터레인). **굽기 파이프라인과 다른 활동**이고, `Terrain/B`·`C`의
  충돌이 이 하나로 같이 풀렸다.
- **옛 이사·점검은 번호 밖으로** — `World/Legacy/`(사이트 이사 · 콘텐츠 점검).
- **같은 단계의 갈래는 글자로 가른다** — `World/0a~0e`(지하 준비 다섯) ·
  `Terrain/7a·7b`(지도 ×2·×4).
- **빈 번호는 그대로 둔다** — `Terrain/4`·`6`·`A`는 지운 도구의 자리다. **번호를 당기면
  그것을 인용한 문서가 조용히 틀린다**(`CLAUDE.md`가 `Terrain → 5. Populate World` 식으로
  단계를 인용한다). **빈 자리는 흔적이지 결함이 아니다.**
- 옮긴 메뉴를 가리키던 글은 같이 고쳤다(`CLAUDE.md`·`docs/design_world_editor.md`·
  `docs/combat.md`).

**씬을 건드리는 생성기는 씬을 직접 저장해야 한다.** `AssetDatabase.SaveAssets()`는 **에셋만** 저장한다 — 씬 컴포넌트를 고치고 `EditorUtility.SetDirty`만 부르면 씬이 더티로 표시되지 않아 저장 대상에서 빠지고, Unity를 닫는 순간 작업이 통째로 날아간다. 2026-07-31에 건설 목록 등록 15건과 연구 게이지 배선이 이렇게 사라졌다.
```csharp
EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
EditorSceneManager.SaveOpenScenes();
```
프리팹·SO는 에셋이라 `SaveAssets()`로 남는다 — **씬에 사는 것만** 이 처리가 필요하다(`BuildModeManager.placeableBuildings`, UI 배선 등).

**`sizeDelta`의 뜻은 앵커가 정한다 — 남의 패널에 무언가를 끼울 땐 먼저 앵커부터 볼 것.**
- **스트레치**(`anchorMin != anchorMax`)면 `sizeDelta`는 **여백**이다. `-12`는 "부모보다 12 작게".
- **코너**(`anchorMin == anchorMax`)면 `sizeDelta`는 **크기 그 자체**다.

두 번 다쳤다. ① 건설 하단 바는 가로 스트레치인데 `sizeDelta.x`가 **1920**이라 실제 폭이
화면+1920이었다 — 오른쪽 끝에 붙인 버튼이 **960px 화면 밖**에 서서 "버튼이 안 보인다"가 됐다.
② `UnitHP_Panel`의 글자는 `ScrollRect` 안 `Content`의 자식이고 **코너 앵커**인데, 스트레치인 줄
알고 여백 값을 넣어 **폭 100 · 높이 −74**로 만들었다 — 글자 13에 폭 100이면 일곱 자마다 접힌다.

**줄바꿈이 꺼져 있으면 이런 게 안 드러난다** — 넘친 글자가 그냥 흘러 나가 마스크에 잘릴 뿐이라,
칸이 좁은 게 아니라 "글자가 길어서"로 보인다.

**레이아웃 그룹은 `childControl*`을 켜야 실제로 정한다.** 끄면 그룹이 크기를 안 잡고 자식이
들고 있던 값을 쓴다 — `LayoutElement`에 값을 적어 놔도 먹지 않는다. **정할 사람을 안 두면
아무도 안 정한다**(버튼이 커진 것도, 글자가 100폭에 갇힌 것도 같은 원인이었다).

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

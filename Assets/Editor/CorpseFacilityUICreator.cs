using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 무덤 · 화장터 · 도축대 세 창을 씬에 짓는다.
//
// **다 쓰면 지운다**(`docs/editor_creators.md` "생성기의 수명") — 산출물인 씬이 정본이고,
// 이후 수정은 인스펙터에서 한다. 그러라고 헤더·버튼·격자 칸까지 전부 씬 오브젝트로 만든다.
//
// 셋을 한 파일에 둔 것은 **같은 규약을 세 번 베끼지 않으려는** 것이다(헤더 색·닫기 버튼·
// 폰트·행 만들기). 셋은 나란한 물건이라 같이 짓고 같이 지운다.
public static class CorpseFacilityUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    static readonly Color HeaderBg = new(0.05098f, 0.05098f, 0.05098f, 1f);   // #0D0D0D 고정
    static readonly Color CloseBg  = new(0.50196f, 0.14902f, 0.14902f, 1f);   // #802626 고정
    static readonly Color PanelBg  = new(0.08f, 0.08f, 0.10f, 0.96f);
    static readonly Color BarBg    = new(0.13f, 0.13f, 0.16f, 1f);
    static readonly Color BtnBg    = new(0.20f, 0.20f, 0.24f, 1f);
    static readonly Color BtnWarm  = new(0.34f, 0.22f, 0.16f, 1f);

    [MenuItem("DungeonColony/Create Corpse Facility UI")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        CreateGrave(canvas);
        CreatePyre(canvas);
        CreateButcher(canvas);

        Debug.Log("[CorpseFacilityUICreator] 무덤·화장터·도축대 창 생성 완료");
    }

    // ── 무덤 ────────────────────────────────────────────────────────────────
    static void CreateGrave(Canvas canvas)
    {
        Replace(canvas, "Grave_UI");

        var root  = MakeRect("Grave_UI", canvas.transform);
        var panel = MakePanel(root.transform, "Grave_Panel", new Vector2(360, 260));
        var (header, title) = MakeHeader(panel, "무덤");
        MakeClose(header, panel);

        var content = MakeContent(panel, top: 48, bottom: 56);
        var body    = MakeText(content.transform, "Body", 14, TextAlignmentOptions.TopLeft);
        Stretch(body.gameObject, 12, 12, 10, 10);

        var bar    = MakeBottomBar(panel, 56);
        var (btn, label) = MakeWideButton(bar.transform, "ExhumeButton", "파낸다", BtnBg);

        var ui = root.AddComponent<GraveUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue        = panel;
        so.FindProperty("titleText").objectReferenceValue    = title;
        so.FindProperty("bodyText").objectReferenceValue     = body;
        so.FindProperty("exhumeButton").objectReferenceValue = btn;
        so.FindProperty("exhumeLabel").objectReferenceValue  = label;
        so.ApplyModifiedProperties();
    }

    // ── 화장터 ──────────────────────────────────────────────────────────────
    static void CreatePyre(Canvas canvas)
    {
        Replace(canvas, "Pyre_UI");

        var root  = MakeRect("Pyre_UI", canvas.transform);
        var panel = MakePanel(root.transform, "Pyre_Panel", new Vector2(380, 300));
        var (header, title) = MakeHeader(panel, "화장터");
        MakeClose(header, panel);

        var content = MakeContent(panel, top: 48, bottom: 96);
        var body    = MakeText(content.transform, "Body", 14, TextAlignmentOptions.TopLeft);
        Stretch(body.gameObject, 12, 12, 10, 10);

        // 진행 게이지 — 하단 바 **바로 위**에 붙인다.
        var gauge   = MakeRect("ProgressBar", panel.transform);
        var gaugeRT = gauge.GetComponent<RectTransform>();
        gaugeRT.anchorMin = new Vector2(0, 0); gaugeRT.anchorMax = new Vector2(1, 0);
        gaugeRT.pivot     = new Vector2(0.5f, 0);
        gaugeRT.anchoredPosition = new Vector2(0, 60);
        gaugeRT.sizeDelta = new Vector2(-24, 24);
        gauge.AddComponent<Image>().color = new Color(0.16f, 0.14f, 0.13f, 1f);

        var fillGO = MakeRect("Fill", gauge.transform);
        Stretch(fillGO, 0, 0, 0, 0);
        var fill = fillGO.AddComponent<Image>();
        fill.color       = new Color(0.88f, 0.49f, 0.24f);
        fill.type        = Image.Type.Filled;
        fill.fillMethod  = Image.FillMethod.Horizontal;
        fill.fillOrigin  = 0;
        fill.fillAmount  = 0f;

        var pct = MakeText(gauge.transform, "Percent", 12, TextAlignmentOptions.Center);
        Stretch(pct.gameObject, 0, 0, 0, 0);

        var bar = MakeBottomBar(panel, 56);
        var (btn, label) = MakeWideButton(bar.transform, "EjectButton", "내린다", BtnWarm);

        var ui = root.AddComponent<PyreUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue        = panel;
        so.FindProperty("titleText").objectReferenceValue    = title;
        so.FindProperty("bodyText").objectReferenceValue     = body;
        so.FindProperty("progressFill").objectReferenceValue = fill;
        so.FindProperty("progressText").objectReferenceValue = pct;
        so.FindProperty("ejectButton").objectReferenceValue  = btn;
        so.FindProperty("ejectLabel").objectReferenceValue   = label;
        so.ApplyModifiedProperties();
    }

    // ── 도축대 ──────────────────────────────────────────────────────────────
    static void CreateButcher(Canvas canvas)
    {
        Replace(canvas, "ButcherTable_UI");

        var root  = MakeRect("ButcherTable_UI", canvas.transform);
        var panel = MakePanel(root.transform, "ButcherTable_Panel", new Vector2(420, 560));
        var (header, title) = MakeHeader(panel, "도축대");
        MakeClose(header, panel);

        var content   = MakeContent(panel, top: 48, bottom: 48);
        var contentRT = content.GetComponent<RectTransform>();
        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 6f; vg.padding = new RectOffset(12, 12, 10, 10);

        var status = MakeText(content.transform, "Status", 15, TextAlignmentOptions.Left);
        AddHeight(status.gameObject, 24);

        // 진행 게이지 — 일하는 동안에만 뜬다(`ButcherTableUI`가 켜고 끈다).
        var progRow = MakeRect("ProgressRow", content.transform);
        AddHeight(progRow, 22);
        progRow.AddComponent<Image>().color = new Color(0.16f, 0.13f, 0.13f, 1f);

        var progFillGO = MakeRect("Fill", progRow.transform);
        Stretch(progFillGO, 0, 0, 0, 0);
        var progFill = progFillGO.AddComponent<Image>();
        progFill.color      = new Color(0.78f, 0.36f, 0.30f);
        progFill.type       = Image.Type.Filled;
        progFill.fillMethod = Image.FillMethod.Horizontal;
        progFill.fillOrigin = 0;
        progFill.fillAmount = 0f;

        var progText = MakeText(progRow.transform, "Percent", 12, TextAlignmentOptions.Center);
        Stretch(progText.gameObject, 0, 0, 0, 0);

        var (suspendBtn, suspendLbl) = MakeRowButton(content.transform, "SuspendButton", "도축을 멈춘다", BtnWarm);
        var (targetBtn,  targetLbl)  = MakeRowButton(content.transform, "TargetButton",  "대상", BtnBg);
        var (stageBtn,   stageLbl)   = MakeRowButton(content.transform, "StageButton",   "상태", BtnBg);
        var (repeatBtn,  repeatLbl)  = MakeRowButton(content.transform, "RepeatButton",  "반복", BtnBg);

        var outLabel = MakeText(content.transform, "OutputLabel", 13, TextAlignmentOptions.Left);
        outLabel.text = "산출물";
        outLabel.color = new Color(0.62f, 0.62f, 0.66f);
        AddHeight(outLabel.gameObject, 20);

        // 격자가 들어갈 칸. `InventoryGridUI`가 런타임에 칸을 채운다.
        var grid = MakeRect("OutputGrid", content.transform);
        grid.AddComponent<InventoryGridUI>();
        AddHeight(grid, 220);

        var (haulBtn, haulLbl) = MakeRowButton(content.transform, "HaulToggle", "운반", BtnBg);

        var ui = root.AddComponent<ButcherTableUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue               = panel;
        so.FindProperty("titleText").objectReferenceValue           = title;
        so.FindProperty("statusText").objectReferenceValue          = status;
        so.FindProperty("progressRow").objectReferenceValue         = progRow;
        so.FindProperty("progressFill").objectReferenceValue        = progFill;
        so.FindProperty("progressText").objectReferenceValue        = progText;
        so.FindProperty("suspendButton").objectReferenceValue       = suspendBtn;
        so.FindProperty("suspendLabel").objectReferenceValue        = suspendLbl;
        so.FindProperty("targetButton").objectReferenceValue        = targetBtn;
        so.FindProperty("targetLabel").objectReferenceValue         = targetLbl;
        so.FindProperty("stageButton").objectReferenceValue         = stageBtn;
        so.FindProperty("stageLabel").objectReferenceValue          = stageLbl;
        so.FindProperty("repeatButton").objectReferenceValue        = repeatBtn;
        so.FindProperty("repeatLabel").objectReferenceValue         = repeatLbl;
        so.FindProperty("outputGridContainer").objectReferenceValue = grid.GetComponent<RectTransform>();
        so.FindProperty("haulToggleButton").objectReferenceValue    = haulBtn;
        so.FindProperty("haulToggleLabel").objectReferenceValue     = haulLbl;
        so.ApplyModifiedProperties();
    }

    // ── 공통 헬퍼 ───────────────────────────────────────────────────────────

    // 같은 이름이 이미 있으면 지운다 — 두 번 돌려도 겹치지 않게.
    static void Replace(Canvas canvas, string name)
    {
        var old = canvas.transform.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

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

    static void Stretch(GameObject go, float l, float r, float t, float b)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    static void AddHeight(GameObject go, float h)
    {
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = h;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }

    // 화면 가운데에 뜨는 창 하나.
    static GameObject MakePanel(Transform parent, string name, Vector2 size)
    {
        var panel = MakeRect(name, parent);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        panel.AddComponent<Image>().color = PanelBg;
        return panel;
    }

    static (GameObject, TextMeshProUGUI) MakeHeader(GameObject panel, string titleText)
    {
        var header   = MakeRect("Header", panel.transform);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot     = new Vector2(0.5f, 1);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0, 48);
        header.AddComponent<Image>().color = HeaderBg;

        var title   = MakeText(header.transform, "Title", 17, TextAlignmentOptions.Left);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = new Vector2(14, 0); titleRT.offsetMax = new Vector2(-40, 0);
        title.text = titleText;

        return (header, title);
    }

    static void MakeClose(GameObject header, GameObject panel)
    {
        var closeGO = MakeRect("CloseButton", header.transform);
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 1); closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot     = new Vector2(1, 1);
        closeRT.anchoredPosition = new Vector2(-6, -6);
        closeRT.sizeDelta = new Vector2(28, 28);
        closeGO.AddComponent<Image>().color = CloseBg;
        closeGO.AddComponent<Button>();

        var cTMP = MakeText(closeGO.transform, "Text", 13, TextAlignmentOptions.Center);
        Stretch(cTMP.gameObject, 0, 0, 0, 0);
        cTMP.text = "X";

        var closeComp = closeGO.AddComponent<CloseButton>();
        var closeSO   = new SerializedObject(closeComp);
        closeSO.FindProperty("target").objectReferenceValue = panel;
        closeSO.ApplyModifiedProperties();
    }

    static GameObject MakeContent(GameObject panel, float top, float bottom)
    {
        var content   = MakeRect("Content", panel.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(0, bottom);
        contentRT.offsetMax = new Vector2(0, -top);
        return content;
    }

    static GameObject MakeBottomBar(GameObject panel, float h)
    {
        var barGO = MakeRect("BottomBar", panel.transform);
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 0); barRT.anchorMax = new Vector2(1, 0);
        barRT.pivot     = new Vector2(0.5f, 0);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0, h);
        barGO.AddComponent<Image>().color = BarBg;
        return barGO;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, float size,
                                    TextAlignmentOptions align)
    {
        var go  = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        ApplyFont(tmp);
        return tmp;
    }

    // 하단 바를 가득 채우는 버튼 하나.
    static (Button, TextMeshProUGUI) MakeWideButton(Transform parent, string name,
                                                    string label, Color bg)
    {
        var go = MakeRect(name, parent);
        Stretch(go, 12, 12, 11, 11);
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        var tmp = MakeText(go.transform, "Text", 15, TextAlignmentOptions.Center);
        Stretch(tmp.gameObject, 0, 0, 0, 0);
        tmp.text = label;
        return (btn, tmp);
    }

    // 세로 목록에 쌓이는 줄 버튼.
    static (Button, TextMeshProUGUI) MakeRowButton(Transform parent, string name,
                                                   string label, Color bg)
    {
        var go = MakeRect(name, parent);
        AddHeight(go, 34);
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        var tmp = MakeText(go.transform, "Text", 14, TextAlignmentOptions.Left);
        Stretch(tmp.gameObject, 10, 10, 0, 0);
        tmp.text = label;
        return (btn, tmp);
    }
}

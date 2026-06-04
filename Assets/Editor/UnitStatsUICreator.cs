using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Create Unit Stats UI 실행
// → 씬의 UnitStatsUI 컴포넌트를 찾아 UI를 새로 생성하고 필드 연결
public static class UnitStatsUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const float  PanelW   = 380f;
    const float  PanelH   = 560f;
    const float  TitleH   = 36f;

    [MenuItem("DungeonColony/Create Unit Stats UI")]
    static void Create()
    {
        var comp = Object.FindFirstObjectByType<UnitStatsUI>();
        if (comp == null)
        {
            Debug.LogError("[UnitStatsUICreator] 씬에 UnitStatsUI 컴포넌트가 없습니다. 빈 GameObject 만들고 컴포넌트 추가하세요.");
            return;
        }

        var so = new SerializedObject(comp);

        var oldPanel = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel);

        var canvas = comp.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[UnitStatsUICreator] 씬에 Canvas가 없습니다.");
            return;
        }

        // SelectionManager 자동 연결
        var selProp = so.FindProperty("selectionManager");
        if (selProp.objectReferenceValue == null)
        {
            var sm = Object.FindFirstObjectByType<SelectionManager>();
            if (sm != null) selProp.objectReferenceValue = sm;
        }

        // ── 패널 루트 (우측 상단)
        var panel   = MakeRect("UnitStats_Panel", comp.transform != canvas.transform ? comp.transform : canvas.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot     = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta = new Vector2(PanelW, PanelH);

        panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.97f);
        panel.SetActive(false);

        // ── 타이틀 바
        var titleBar   = MakeRect("TitleBar", panel.transform);
        var titleBarRT = titleBar.GetComponent<RectTransform>();
        titleBarRT.anchorMin = new Vector2(0, 1); titleBarRT.anchorMax = new Vector2(1, 1);
        titleBarRT.pivot     = new Vector2(0.5f, 1);
        titleBarRT.anchoredPosition = Vector2.zero;
        titleBarRT.sizeDelta = new Vector2(0, TitleH);
        titleBar.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 1f);

        // 타이틀 텍스트
        var titleGO  = MakeRect("TitleText", titleBar.transform);
        var titleRT  = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(10, 0); titleRT.offsetMax = new Vector2(-40, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "스탯"; titleTMP.fontSize = 14;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        titleTMP.raycastTarget = false;
        ApplyFont(titleTMP);

        // 닫기 버튼
        var closeBtnGO  = MakeRect("CloseButton", titleBar.transform);
        var closeBtnRT  = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = closeBtnRT.anchorMax = new Vector2(1, 1);
        closeBtnRT.pivot     = new Vector2(1, 1);
        closeBtnRT.anchoredPosition = new Vector2(-4, -4);
        closeBtnRT.sizeDelta = new Vector2(28, 28);
        closeBtnGO.AddComponent<Image>().color = new Color(0.4f, 0.15f, 0.15f, 1f);
        var closeBtnComp = closeBtnGO.AddComponent<Button>();
        closeBtnComp.onClick.AddListener(() => panel.SetActive(false));
        var closeLabel   = MakeRect("Label", closeBtnGO.transform);
        SetStretch(closeLabel);
        var closeTMP = closeLabel.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "X"; closeTMP.fontSize = 14; closeTMP.color = Color.white;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.raycastTarget = false;
        ApplyFont(closeTMP);

        // ── 스크롤 뷰
        var scrollGO   = MakeRect("ScrollView", panel.transform);
        var scrollRT   = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(0, 0);
        scrollRT.offsetMax = new Vector2(0, -TitleH);
        scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;

        // 뷰포트
        var viewportGO = MakeRect("Viewport", scrollGO.transform);
        SetStretch(viewportGO);
        var viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.01f);
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;

        // 콘텐츠
        var contentGO = MakeRect("Content", viewportGO.transform);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot     = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 600f);
        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 정보 텍스트
        var infoGO = MakeRect("InfoText", contentGO.transform);
        var infoRT = infoGO.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0, 1); infoRT.anchorMax = new Vector2(1, 1);
        infoRT.pivot     = new Vector2(0.5f, 1);
        infoRT.anchoredPosition = new Vector2(0, 0);
        var infoTMP = infoGO.AddComponent<TextMeshProUGUI>();
        infoTMP.text = "";
        infoTMP.fontSize = 13;
        infoTMP.color = new Color(0.92f, 0.92f, 0.92f);
        infoTMP.alignment = TextAlignmentOptions.TopLeft;
        infoTMP.raycastTarget = false;
        infoTMP.margin = new Vector4(8, 6, 8, 6);
        ApplyFont(infoTMP);

        var infoFitter = infoGO.AddComponent<ContentSizeFitter>();
        infoFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        infoFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportGO.GetComponent<RectTransform>();
        scrollRect.content  = contentRT;

        // ── 필드 연결
        so.FindProperty("panel").objectReferenceValue    = panel;
        so.FindProperty("infoText").objectReferenceValue = infoTMP;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create Unit Stats UI");
        EditorUtility.SetDirty(comp);
        Debug.Log("[UnitStatsUICreator] UnitStats UI 생성 완료");
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
}

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Create Resource Node Panel 실행
// → 씬의 ResourceNodePanel 컴포넌트를 찾아 UI를 새로 생성하고 모든 필드 연결
public static class ResourceNodePanelCreator
{
    const string FontPath        = "Assets/NanumGothic SDF.asset";
    const string PawnEntryPath   = "Assets/3_Prefab/UI/Pawn Entry Prefab.prefab";
    const float  PanelW = 280f;
    const float  PanelH = 480f;
    const float  HeaderH = 32f;
    static readonly Color HeaderColor = new Color(0.05098f, 0.05098f, 0.05098f, 1f); // #0D0D0D
    static readonly Color CloseColor  = new Color(0.50196f, 0.14902f, 0.14902f, 1f); // #802626

    [MenuItem("DungeonColony/Create Resource Node Panel")]
    static void Create()
    {
        var comp = Object.FindFirstObjectByType<ResourceNodePanel>();
        if (comp == null)
        {
            Debug.LogError("[ResourceNodePanelCreator] 씬에 ResourceNodePanel 컴포넌트가 없습니다.");
            return;
        }

        var so = new SerializedObject(comp);

        var oldPanel = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel);

        var canvas = comp.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ResourceNodePanelCreator] 씬에 Canvas가 없습니다.");
            return;
        }

        // ── 패널 루트 (우측 중앙)
        var panel   = MakeRect("ResourceNode_Panel", comp.transform != canvas.transform ? comp.transform : canvas.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(1f, 0.5f);
        panelRT.pivot     = new Vector2(1f, 0.5f);
        panelRT.anchoredPosition = new Vector2(-10f, 0f);
        panelRT.sizeDelta = new Vector2(PanelW, PanelH);
        panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

        // ── 헤더 (상단 풀폭 바). 규칙 1: LayoutGroup 없이 RectTransform 앵커로 수동 배치.
        var header   = MakeRect("Header", panel.transform);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f); headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot     = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, HeaderH);
        header.AddComponent<Image>().color = HeaderColor;

        // 타이틀 (좌측 스트레치, 닫기 버튼 폭만큼 우측 여백)
        var nameTMP = MakeText("NameText", header.transform, "자원 노드", 15, TextAlignmentOptions.MidlineLeft);
        var nameRT  = nameTMP.GetComponent<RectTransform>();
        nameRT.anchorMin = Vector2.zero; nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(8f, 0f); nameRT.offsetMax = new Vector2(-34f, 0f);

        // 닫기 버튼 (우상단 28×28, 수동 앵커). 규칙 3: 공용 CloseButton.cs로 닫기 처리.
        var closeGO = MakeRect("CloseButton", header.transform);
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot     = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-2f, -2f);
        closeRT.sizeDelta = new Vector2(28f, 28f);
        closeGO.AddComponent<Image>().color = CloseColor;
        closeGO.AddComponent<Button>();
        var cLabel = MakeRect("Label", closeGO.transform);
        SetStretch(cLabel);
        var cTMP = cLabel.AddComponent<TextMeshProUGUI>();
        cTMP.text = "X"; cTMP.fontSize = 14; cTMP.color = Color.white;
        cTMP.alignment = TextAlignmentOptions.Center; cTMP.raycastTarget = false;
        ApplyFont(cTMP);
        var closeComp = closeGO.AddComponent<CloseButton>();
        var closeSO = new SerializedObject(closeComp);
        closeSO.FindProperty("target").objectReferenceValue = panel;
        closeSO.ApplyModifiedProperties();

        // ── 콘텐츠 영역 (헤더 아래 전체를 채우는 세로 레이아웃)
        var content   = MakeRect("Content", panel.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero; contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = new Vector2(0f, -HeaderH);
        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 6f; vg.padding = new RectOffset(10, 10, 10, 10);

        // ── 게이지 진행도
        var gaugeTMP = MakeText("GaugeText", content.transform, "0%", 12, TextAlignmentOptions.Center);
        gaugeTMP.color = new Color(0.7f, 0.85f, 0.7f);
        AddLE(gaugeTMP.gameObject, 18);

        // ── 재고 (아이콘 + 수량)
        var stockRow = MakeRect("StockRow", content.transform);
        AddLE(stockRow, 72);
        var stockHG = stockRow.AddComponent<HorizontalLayoutGroup>();
        stockHG.childControlWidth = stockHG.childControlHeight = true;
        stockHG.childForceExpandWidth = false; stockHG.childForceExpandHeight = true;
        stockHG.childAlignment = TextAnchor.MiddleCenter; stockHG.spacing = 8f;

        var iconGO  = MakeRect("StockIcon", stockRow.transform);
        AddLE(iconGO, 64, 64);
        var stockIcon = iconGO.AddComponent<Image>();
        stockIcon.preserveAspect = true; stockIcon.raycastTarget = false;

        var stockCount = MakeText("StockCount", stockRow.transform, "", 16, TextAlignmentOptions.MidlineLeft);
        AddLE(stockCount.gameObject, 0, 60);

        // ── 운반 임계값 행 ( [-]  운반:N  [+] )
        var haulRow = MakeRect("HaulRow", content.transform);
        AddLE(haulRow, 30);
        var haulHG = haulRow.AddComponent<HorizontalLayoutGroup>();
        haulHG.childControlWidth = haulHG.childControlHeight = true;
        haulHG.childForceExpandWidth = false; haulHG.childForceExpandHeight = true;
        haulHG.childAlignment = TextAnchor.MiddleCenter; haulHG.spacing = 6f;

        var haulDown = MakeButton("HaulDown", haulRow.transform, "-", 30, new Color(0.3f, 0.3f, 0.35f));
        var haulText = MakeText("HaulText", haulRow.transform, "운반:10", 12, TextAlignmentOptions.Center);
        AddLE(haulText.gameObject, 0, 90);
        var haulUp   = MakeButton("HaulUp", haulRow.transform, "+", 30, new Color(0.3f, 0.3f, 0.35f));

        // ── 액션 버튼 행 (배정 / 이송)
        var actionRow = MakeRect("ActionRow", content.transform);
        AddLE(actionRow, 32);
        var actHG = actionRow.AddComponent<HorizontalLayoutGroup>();
        actHG.childControlWidth = actHG.childControlHeight = true;
        actHG.childForceExpandWidth = true; actHG.childForceExpandHeight = true;
        actHG.spacing = 6f;

        var assignBtn   = MakeButton("AssignButton", actionRow.transform, "배정", 0, new Color(0.2f, 0.4f, 0.5f));
        var transferBtn = MakeButton("TransferButton", actionRow.transform, "이송", 0, new Color(0.45f, 0.4f, 0.2f));

        // ── 배정된 폰 목록 (스크롤)
        var listGO = MakeRect("AssignedList", content.transform);
        AddLE(listGO, 0, -1, flexibleHeight: 1f);
        listGO.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.9f);
        var listGrid = listGO.AddComponent<GridLayoutGroup>();
        listGrid.cellSize        = new Vector2(78f, 88f);
        listGrid.spacing         = new Vector2(6f, 6f);
        listGrid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        listGrid.constraintCount = 3;
        listGrid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        listGrid.childAlignment  = TextAnchor.UpperLeft;
        listGrid.padding         = new RectOffset(6, 6, 6, 6);

        // ── 필드 연결
        var pawnEntry = BuildPawnEntryPrefab(canvas.transform);

        so.FindProperty("panel").objectReferenceValue                = panel;
        so.FindProperty("nameText").objectReferenceValue             = nameTMP;
        so.FindProperty("gaugeText").objectReferenceValue            = gaugeTMP;
        so.FindProperty("stockIcon").objectReferenceValue            = stockIcon;
        so.FindProperty("stockCount").objectReferenceValue           = stockCount;
        so.FindProperty("assignButton").objectReferenceValue         = assignBtn;
        so.FindProperty("assignedListRoot").objectReferenceValue     = listGO.transform;
        so.FindProperty("pawnEntryPrefab").objectReferenceValue      = pawnEntry;
        so.FindProperty("transferButton").objectReferenceValue       = transferBtn;
        so.FindProperty("haulThresholdText").objectReferenceValue    = haulText;
        so.FindProperty("haulThresholdUpButton").objectReferenceValue   = haulUp;
        so.FindProperty("haulThresholdDownButton").objectReferenceValue = haulDown;
        so.ApplyModifiedProperties();

        panel.SetActive(false);
        Undo.RegisterCreatedObjectUndo(panel, "Create Resource Node Panel");
        EditorUtility.SetDirty(comp);
        Debug.Log("[ResourceNodePanelCreator] ResourceNode 패널 생성 완료");
    }

    // 배정 목록 폰 엔트리 프리팹: 정사각 카드 [초상화 위 / 개체명 아래], 해제 X는 우상단 코너.
    // GridLayoutGroup이 카드 크기를 cellSize로 고정하므로 격자로 딱딱 배치된다.
    static PawnEntryUI BuildPawnEntryPrefab(Transform tempParent)
    {
        var root = MakeRect("PawnEntry", tempParent);
        root.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.19f, 1f);

        var vg = root.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = false; vg.childForceExpandHeight = false;
        vg.childAlignment = TextAnchor.UpperCenter; vg.spacing = 2f;
        vg.padding = new RectOffset(4, 4, 4, 4);

        var portraitGO = MakeRect("Portrait", root.transform);
        var pLE = portraitGO.AddComponent<LayoutElement>();
        pLE.preferredWidth = 60; pLE.preferredHeight = 60;
        var portrait = portraitGO.AddComponent<Image>();
        portrait.preserveAspect = true; portrait.raycastTarget = false;

        var nameGO = MakeRect("NameText", root.transform);
        var nLE = nameGO.AddComponent<LayoutElement>();
        nLE.preferredWidth = 70; nLE.preferredHeight = 14;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "이름"; nameTMP.fontSize = 11; nameTMP.color = Color.white;
        nameTMP.alignment = TextAlignmentOptions.Center; nameTMP.raycastTarget = false;
        nameTMP.enableWordWrapping = false; nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        ApplyFont(nameTMP);

        // 해제 버튼: 카드 우상단 코너 오버레이 (레이아웃 무시)
        var unGO = MakeRect("UnassignButton", root.transform);
        unGO.AddComponent<LayoutElement>().ignoreLayout = true;
        var uRT = unGO.GetComponent<RectTransform>();
        uRT.anchorMin = uRT.anchorMax = new Vector2(1f, 1f);
        uRT.pivot     = new Vector2(1f, 1f);
        uRT.anchoredPosition = new Vector2(-2f, -2f);
        uRT.sizeDelta = new Vector2(18f, 18f);
        unGO.AddComponent<Image>().color = CloseColor;
        var unBtn = unGO.AddComponent<Button>();
        var uLabel = MakeRect("Label", unGO.transform);
        SetStretch(uLabel);
        var uTMP = uLabel.AddComponent<TextMeshProUGUI>();
        uTMP.text = "X"; uTMP.fontSize = 10; uTMP.color = Color.white;
        uTMP.alignment = TextAlignmentOptions.Center; uTMP.raycastTarget = false;
        ApplyFont(uTMP);

        var entry = root.AddComponent<PawnEntryUI>();
        var eso = new SerializedObject(entry);
        eso.FindProperty("portraitImage").objectReferenceValue  = portrait;
        eso.FindProperty("nameText").objectReferenceValue       = nameTMP;
        eso.FindProperty("unassignButton").objectReferenceValue = unBtn;
        eso.ApplyModifiedProperties();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PawnEntryPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[ResourceNodePanelCreator] PawnEntry 프리팹 저장: {PawnEntryPath}");
        return prefab.GetComponent<PawnEntryUI>();
    }

    // ── 헬퍼 ──────────────────────────────────────────────
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

    static LayoutElement AddLE(GameObject go, float preferredHeight, float preferredWidth = -1, float flexibleHeight = 0f)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredHeight > 0) le.preferredHeight = preferredHeight;
        if (preferredWidth  > 0) le.preferredWidth  = preferredWidth;
        le.flexibleHeight = flexibleHeight;
        return le;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go  = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = Color.white;
        tmp.alignment = align; tmp.raycastTarget = false;
        ApplyFont(tmp);
        return tmp;
    }

    static Button MakeButton(string name, Transform parent, string label, float fixedWidth, Color color)
    {
        var go = MakeRect(name, parent);
        go.AddComponent<Image>().color = color;
        var btn = go.AddComponent<Button>();
        if (fixedWidth > 0) AddLE(go, 0, fixedWidth);
        var labelGO = MakeRect("Label", go.transform);
        SetStretch(labelGO);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 13; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        ApplyFont(tmp);
        return btn;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}

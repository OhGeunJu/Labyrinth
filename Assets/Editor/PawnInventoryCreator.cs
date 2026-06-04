using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Create Pawn Inventory UI 실행
// → 씬의 PawnInventoryPanel 컴포넌트를 찾아 UI를 새로 생성하고 모든 필드 연결
public static class PawnInventoryCreator
{
    const string FontPath   = "Assets/NanumGothic SDF.asset";
    const float  PanelW     = 360f;
    const float  PanelH     = 520f;
    const float  TitleH     = 40f;
    const float  EquipRowH  = 60f;
    const float  ActionBarH = 44f;

    [MenuItem("DungeonColony/Create Pawn Inventory UI")]
    static void Create()
    {
        var comp = Object.FindFirstObjectByType<PawnInventoryPanel>();
        if (comp == null)
        {
            Debug.LogError("[PawnInventoryCreator] 씬에 PawnInventoryPanel 컴포넌트가 없습니다.");
            return;
        }

        var so = new SerializedObject(comp);

        // 기존 panel 제거
        var oldPanel = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel);

        // Canvas 찾기
        var canvas = comp.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PawnInventoryCreator] 씬에 Canvas가 없습니다.");
            return;
        }

        // ── 패널 루트
        var panel   = MakeRect("PawnInventory_Panel", comp.transform != canvas.transform ? comp.transform : canvas.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0f, 0.5f);
        panelRT.pivot     = new Vector2(0f, 0.5f);
        panelRT.anchoredPosition = new Vector2(10f, 0f);
        panelRT.sizeDelta = new Vector2(PanelW, PanelH);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.10f, 0.10f, 0.12f, 0.97f);
        panel.SetActive(false);

        // ── 타이틀 바
        var titleBar   = MakeRect("TitleBar", panel.transform);
        var titleBarRT = titleBar.GetComponent<RectTransform>();
        titleBarRT.anchorMin = new Vector2(0, 1); titleBarRT.anchorMax = new Vector2(1, 1);
        titleBarRT.pivot     = new Vector2(0.5f, 1);
        titleBarRT.anchoredPosition = Vector2.zero;
        titleBarRT.sizeDelta = new Vector2(0, TitleH);
        titleBar.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 1f);

        // 타이틀 텍스트 (좌측)
        var titleGO  = MakeRect("TitleText", titleBar.transform);
        var titleRT  = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(10, 0); titleRT.offsetMax = new Vector2(-40, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "인벤토리"; titleTMP.fontSize = 14;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        titleTMP.raycastTarget = false;
        ApplyFont(titleTMP);

        // 닫기 버튼 (우상단 28×28)
        var closeBtnGO  = MakeRect("CloseButton", titleBar.transform);
        var closeBtnRT  = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = closeBtnRT.anchorMax = new Vector2(1, 1);
        closeBtnRT.pivot     = new Vector2(1, 1);
        closeBtnRT.anchoredPosition = new Vector2(-4, -4);
        closeBtnRT.sizeDelta = new Vector2(28, 28);
        closeBtnGO.AddComponent<Image>().color = new Color(0.4f, 0.15f, 0.15f, 1f);
        var closeBtnComp = closeBtnGO.AddComponent<Button>();
        var closeLabel   = MakeRect("Label", closeBtnGO.transform);
        SetStretch(closeLabel);
        var closeTMP = closeLabel.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "X"; closeTMP.fontSize = 14; closeTMP.color = Color.white;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.raycastTarget = false;
        ApplyFont(closeTMP);

        // ── 장착 슬롯 행
        var equipRow   = MakeRect("EquipRow", panel.transform);
        var equipRowRT = equipRow.GetComponent<RectTransform>();
        equipRowRT.anchorMin = new Vector2(0, 1); equipRowRT.anchorMax = new Vector2(1, 1);
        equipRowRT.pivot     = new Vector2(0.5f, 1);
        equipRowRT.anchoredPosition = new Vector2(0, -TitleH);
        equipRowRT.sizeDelta = new Vector2(0, EquipRowH);
        equipRow.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.95f);

        var hg = equipRow.AddComponent<HorizontalLayoutGroup>();
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = hg.childForceExpandHeight = true;
        hg.spacing = 4f; hg.padding = new RectOffset(6, 6, 5, 5);

        var slotDefs = new (string name, EquipSlotUI.SlotKind kind)[]
        {
            ("MainHandSlot", EquipSlotUI.SlotKind.MainHand),
            ("OffHandSlot",  EquipSlotUI.SlotKind.OffHand),
            ("HeadSlot",     EquipSlotUI.SlotKind.Head),
            ("ChestSlot",    EquipSlotUI.SlotKind.Chest),
            ("HandsSlot",    EquipSlotUI.SlotKind.Hands),
            ("LegsSlot",     EquipSlotUI.SlotKind.Legs),
        };

        var slots = new EquipSlotUI[slotDefs.Length];
        for (int i = 0; i < slotDefs.Length; i++)
        {
            slots[i] = BuildSlot(slotDefs[i].name, equipRow.transform);
            slots[i].Init(slotDefs[i].kind);
        }

        // ── 액션 바 (하단 고정, 기본 비활성)
        var actionBar   = MakeRect("ActionBar", panel.transform);
        var actionBarRT = actionBar.GetComponent<RectTransform>();
        actionBarRT.anchorMin = new Vector2(0, 0); actionBarRT.anchorMax = new Vector2(1, 0);
        actionBarRT.pivot     = new Vector2(0.5f, 0);
        actionBarRT.anchoredPosition = Vector2.zero;
        actionBarRT.sizeDelta = new Vector2(0, ActionBarH);
        actionBar.AddComponent<Image>().color = new Color(0.07f, 0.10f, 0.07f, 0.97f);
        actionBar.SetActive(false);

        var actionHG = actionBar.AddComponent<HorizontalLayoutGroup>();
        actionHG.childControlWidth = actionHG.childControlHeight = true;
        actionHG.childForceExpandHeight = true;
        actionHG.childForceExpandWidth = false;
        actionHG.spacing = 6f; actionHG.padding = new RectOffset(8, 8, 6, 6);

        // 아이템 텍스트 (왼쪽, 늘어남)
        var itemTextGO  = MakeRect("ActionItemText", actionBar.transform);
        var itemTextLE  = itemTextGO.AddComponent<LayoutElement>();
        itemTextLE.flexibleWidth = 1f;
        var itemTMP = itemTextGO.AddComponent<TextMeshProUGUI>();
        itemTMP.text = "아이템을 선택하세요"; itemTMP.fontSize = 11;
        itemTMP.color = new Color(0.85f, 0.85f, 0.85f);
        itemTMP.alignment = TextAlignmentOptions.MidlineLeft;
        itemTMP.raycastTarget = false;
        ApplyFont(itemTMP);

        // 액션 버튼 (오른쪽, 고정 너비)
        var actionBtnGO  = MakeRect("ActionButton", actionBar.transform);
        var actionBtnLE  = actionBtnGO.AddComponent<LayoutElement>();
        actionBtnLE.preferredWidth = 64f;
        actionBtnGO.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 1f);
        var actionBtnComp = actionBtnGO.AddComponent<Button>();
        var actionLabelGO = MakeRect("Label", actionBtnGO.transform);
        SetStretch(actionLabelGO);
        var actionLabelTMP = actionLabelGO.AddComponent<TextMeshProUGUI>();
        actionLabelTMP.text = "판매"; actionLabelTMP.fontSize = 12;
        actionLabelTMP.color = Color.white;
        actionLabelTMP.alignment = TextAlignmentOptions.Center;
        actionLabelTMP.raycastTarget = false;
        ApplyFont(actionLabelTMP);

        // ── 그리드 컨테이너 (EquipRow ~ ActionBar 사이)
        var gridGO  = MakeRect("GridContainer", panel.transform);
        var gridRT  = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = Vector2.zero; gridRT.anchorMax = Vector2.one;
        gridRT.offsetMin = new Vector2(0, ActionBarH);
        gridRT.offsetMax = new Vector2(0, -(TitleH + EquipRowH));

        // ── 필드 연결
        so.FindProperty("panel").objectReferenceValue           = panel;
        so.FindProperty("gridContainer").objectReferenceValue   = gridRT;
        so.FindProperty("mainHandSlot").objectReferenceValue    = slots[0];
        so.FindProperty("offHandSlot").objectReferenceValue     = slots[1];
        so.FindProperty("headSlot").objectReferenceValue        = slots[2];
        so.FindProperty("chestSlot").objectReferenceValue       = slots[3];
        so.FindProperty("handsSlot").objectReferenceValue       = slots[4];
        so.FindProperty("legsSlot").objectReferenceValue        = slots[5];
        so.FindProperty("actionBar").objectReferenceValue       = actionBar;
        so.FindProperty("actionItemText").objectReferenceValue  = itemTMP;
        so.FindProperty("actionButtonLabel").objectReferenceValue = actionLabelTMP;
        so.FindProperty("actionButton").objectReferenceValue    = actionBtnComp;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create Pawn Inventory UI");
        EditorUtility.SetDirty(comp);
        Debug.Log("[PawnInventoryCreator] PawnInventory UI 생성 완료");
    }

    static EquipSlotUI BuildSlot(string name, Transform parent)
    {
        var go = MakeRect(name, parent);
        go.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 0.95f);

        var iconGO  = MakeRect("Icon", go.transform);
        SetStretch(iconGO);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = new Color(1, 1, 1, 0);
        iconImg.raycastTarget = false;

        var labelGO  = MakeRect("SlotLabel", go.transform);
        var labelRT  = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 1); labelRT.anchorMax = new Vector2(1, 1);
        labelRT.pivot     = new Vector2(0, 1);
        labelRT.anchoredPosition = new Vector2(3, -2);
        labelRT.sizeDelta = new Vector2(-3, 14);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.fontSize = 8; labelTMP.color = new Color(0.6f, 0.6f, 0.6f);
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelTMP.raycastTarget = false;
        ApplyFont(labelTMP);

        var itemNameGO  = MakeRect("ItemNameText", go.transform);
        var itemNameRT  = itemNameGO.GetComponent<RectTransform>();
        itemNameRT.anchorMin = Vector2.zero; itemNameRT.anchorMax = new Vector2(1, 0);
        itemNameRT.pivot     = new Vector2(0.5f, 0);
        itemNameRT.anchoredPosition = new Vector2(0, 2);
        itemNameRT.sizeDelta = new Vector2(0, 13);
        var itemNameTMP = itemNameGO.AddComponent<TextMeshProUGUI>();
        itemNameTMP.fontSize = 7; itemNameTMP.color = new Color(0.85f, 0.85f, 0.85f);
        itemNameTMP.alignment = TextAlignmentOptions.Center;
        itemNameTMP.raycastTarget = false;
        ApplyFont(itemNameTMP);

        var slot = go.AddComponent<EquipSlotUI>();
        var sso  = new SerializedObject(slot);
        sso.FindProperty("bg").objectReferenceValue           = go.GetComponent<Image>();
        sso.FindProperty("icon").objectReferenceValue         = iconImg;
        sso.FindProperty("slotLabel").objectReferenceValue    = labelTMP;
        sso.FindProperty("itemNameText").objectReferenceValue = itemNameTMP;
        sso.ApplyModifiedProperties();

        return slot;
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

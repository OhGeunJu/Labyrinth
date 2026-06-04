using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Add Corpse Equip Row 실행
// → 씬의 CorpseInventoryPanel에 EquipRow 추가, GridContainer 위치 자동 조정
public static class CorpseEquipRowCreator
{
    const string FontPath    = "Assets/NanumGothic SDF.asset";
    const float  RowHeight   = 68f;
    const float  SlotSize    = 56f;

    [MenuItem("DungeonColony/Add Corpse Equip Row")]
    static void Create()
    {
        var comp = Object.FindFirstObjectByType<CorpseInventoryPanel>();
        if (comp == null)
        {
            Debug.LogError("[CorpseEquipRowCreator] 씬에 CorpseInventoryPanel이 없습니다.");
            return;
        }

        var so      = new SerializedObject(comp);
        var panelGO = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (panelGO == null)
        {
            Debug.LogError("[CorpseEquipRowCreator] CorpseInventoryPanel.panel 필드가 비어있습니다.");
            return;
        }

        // 기존 EquipRow 제거 후 재생성
        var existing = panelGO.transform.Find("EquipRow");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // ── EquipRow 루트 (타이틀 바 아래)
        var row   = MakeRect("EquipRow", panelGO.transform);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1); rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot     = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, -48); // 타이틀 바(48px) 아래
        rowRT.sizeDelta = new Vector2(0, RowHeight);
        row.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.95f);

        var hg = row.AddComponent<HorizontalLayoutGroup>();
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = hg.childForceExpandHeight = true;
        hg.spacing = 4f; hg.padding = new RectOffset(6, 6, 6, 6);

        // ── 슬롯 6개 생성
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
            slots[i] = BuildSlot(slotDefs[i].name, row.transform);
            slots[i].Init(slotDefs[i].kind);
        }

        // ── GridContainer 위치 조정 (EquipRow 높이만큼 상단 여백 추가)
        var gridProp = so.FindProperty("gridContainer");
        var gridRT   = gridProp?.objectReferenceValue as RectTransform;
        if (gridRT != null)
        {
            var om = gridRT.offsetMax;
            gridRT.offsetMax = new Vector2(om.x, -(48f + RowHeight + 4f));
        }

        // ── CorpseInventoryPanel에 슬롯 직접 연결
        so.FindProperty("mainHandSlot").objectReferenceValue = slots[0];
        so.FindProperty("offHandSlot").objectReferenceValue  = slots[1];
        so.FindProperty("headSlot").objectReferenceValue     = slots[2];
        so.FindProperty("chestSlot").objectReferenceValue    = slots[3];
        so.FindProperty("handsSlot").objectReferenceValue    = slots[4];
        so.FindProperty("legsSlot").objectReferenceValue     = slots[5];
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(row, "Add Corpse Equip Row");
        EditorUtility.SetDirty(comp);
        Debug.Log("[CorpseEquipRowCreator] EquipRow 추가 완료");
    }

    static EquipSlotUI BuildSlot(string name, Transform parent)
    {
        var go = MakeRect(name, parent);
        go.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 0.95f);

        // 아이콘
        var iconGO  = MakeRect("Icon", go.transform);
        SetStretch(iconGO);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = new Color(1, 1, 1, 0);
        iconImg.raycastTarget = false;

        // 슬롯 라벨 (좌상단)
        var labelGO  = MakeRect("SlotLabel", go.transform);
        var labelRT  = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 1); labelRT.anchorMax = new Vector2(1, 1);
        labelRT.pivot     = new Vector2(0, 1);
        labelRT.anchoredPosition = new Vector2(3, -2);
        labelRT.sizeDelta = new Vector2(-3, 16);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.fontSize = 9; labelTMP.color = new Color(0.6f, 0.6f, 0.6f);
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelTMP.raycastTarget = false;
        ApplyFont(labelTMP);

        // 아이템 이름 (하단)
        var itemGO  = MakeRect("ItemNameText", go.transform);
        var itemRT  = itemGO.GetComponent<RectTransform>();
        itemRT.anchorMin = Vector2.zero; itemRT.anchorMax = new Vector2(1, 0);
        itemRT.pivot     = new Vector2(0.5f, 0);
        itemRT.anchoredPosition = new Vector2(0, 2);
        itemRT.sizeDelta = new Vector2(0, 14);
        var itemTMP = itemGO.AddComponent<TextMeshProUGUI>();
        itemTMP.fontSize = 8; itemTMP.color = new Color(0.85f, 0.85f, 0.85f);
        itemTMP.alignment = TextAlignmentOptions.Center;
        itemTMP.raycastTarget = false;
        ApplyFont(itemTMP);

        var slot = go.AddComponent<EquipSlotUI>();
        var so   = new SerializedObject(slot);
        so.FindProperty("bg").objectReferenceValue           = go.GetComponent<Image>();
        so.FindProperty("icon").objectReferenceValue         = iconImg;
        so.FindProperty("slotLabel").objectReferenceValue    = labelTMP;
        so.FindProperty("itemNameText").objectReferenceValue = itemTMP;
        so.ApplyModifiedProperties();

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

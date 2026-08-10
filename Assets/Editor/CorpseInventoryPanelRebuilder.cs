using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 시체 창을 **배낭 창(`Inventory_Panel`)을 복제해서** 다시 짓는다.
//
// 둘은 같은 일을 한다 — 격자 하나와 장비 슬롯 여섯. 그런데 시체 창은 320×480에 장비를
// **가로 한 줄**로 늘어놓은 옛 설계라, 나란히 열어 놓으면 같은 물건이 두 가지 모양으로 보였다.
//
// **베끼지 않고 복제한다.** 배낭 창의 치수를 손으로 옮겨 적으면 그 순간에만 맞고, 나중에
// 배낭 창을 인스펙터에서 손보면 둘이 다시 갈라진다. 복제는 지을 때 한 번은 반드시 같다.
// (그 뒤로 갈라지는 것은 어쩔 수 없다 — 다시 갈라지면 이 도구를 한 번 더 돌리면 된다.)
//
// 시체에 없는 것만 걷어낸다: **액션 바**(수량·판매는 내 배낭의 일)와
// **적재 바**(무게·짐내림·잠금·거래 상대는 살아 있는 폰의 것).
public static class CorpseInventoryPanelRebuilder
{
    const string PanelName = "CorpseInventory_Panel";

    // 배낭 창 오른쪽에 세운다. 시체를 뒤지면 배낭 창이 같이 열리므로(`ForceOpen`)
    // 겹쳐 놓으면 물건을 끌어 옮길 수가 없다. 캔버스가 ConstantPixelSize라 이 값은 실제 px다.
    const float PanelX = 680f;

    // 걷어낼 것 — 배낭 창에만 있는 아래쪽 두 줄.
    static readonly string[] StripChildren = { "ActionBar", "CarryBar" };

    // 아래쪽을 비웠으니 격자와 장비 열이 그만큼 내려온다. 위(제목 바)는 그대로.
    const float BottomInset = 12f;

    [MenuItem("DungeonColony/Rebuild Corpse Inventory Panel")]
    static void Rebuild()
    {
        // **비활성 포함으로 찾는다.** 이 씬은 UI가 꺼진 채 저장돼 있을 수 있고,
        // `FindFirstObjectByType<T>()`는 비활성을 빼서 "씬에 없습니다"라고 잘못 답한다.
        var corpseUI = Object.FindFirstObjectByType<CorpseInventoryUI>(FindObjectsInactive.Include);
        if (corpseUI == null) { Debug.LogError("[CorpsePanel] 씬에 CorpseInventoryUI가 없습니다."); return; }

        var invUI = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (invUI == null) { Debug.LogError("[CorpsePanel] 씬에 InventoryUI가 없습니다(복제 원본)."); return; }

        var invSO    = new SerializedObject(invUI);
        var template = invSO.FindProperty("panel").objectReferenceValue as GameObject;
        if (template == null) { Debug.LogError("[CorpsePanel] InventoryUI.panel이 비어 있습니다."); return; }

        var corpseSO = new SerializedObject(corpseUI);
        var oldPanel = corpseSO.FindProperty("panel").objectReferenceValue as GameObject;

        // 새 패널은 시체 UI 루트 밑에 둔다(옛 패널이 있으면 그 부모, 없으면 컴포넌트 자신).
        Transform parent = oldPanel != null ? oldPanel.transform.parent : corpseUI.transform;

        var panel = Object.Instantiate(template, parent);
        panel.name = PanelName;
        panel.SetActive(true);   // 편집 중에는 보여야 고칠 수 있다. 런타임은 Awake가 끈다.

        Place(panel);
        Strip(panel);
        Reflow(panel);
        Wire(corpseSO, panel);

        if (oldPanel != null && oldPanel != panel) Object.DestroyImmediate(oldPanel);

        corpseSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(corpseUI);

        // **씬은 직접 저장해야 한다.** `AssetDatabase.SaveAssets()`는 에셋만 저장하므로,
        // `SetDirty`만 부르고 말면 유니티를 닫는 순간 배선이 통째로 날아간다(2026-07-31에 겪음).
        EditorSceneManager.MarkSceneDirty(corpseUI.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[CorpsePanel] {PanelName}을(를) {template.name} 기준으로 다시 지었습니다.");
    }

    // 배낭 창과 **같은 세로 기준선**(왼쪽 중앙 앵커)에 두고 x만 오른쪽으로 민다.
    static void Place(GameObject panel)
    {
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(PanelX, 0f);
    }

    static void Strip(GameObject panel)
    {
        foreach (var name in StripChildren)
        {
            var child = panel.transform.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }

    // 걷어낸 만큼 아래를 채운다. **offsetMin/Max로 만진다** — sizeDelta·anchoredPosition을
    // 손으로 계산하면 피벗이 낀 곱셈이라 한 번은 반드시 틀린다.
    static void Reflow(GameObject panel)
    {
        SetBottom(panel.transform.Find("GridContainer"), BottomInset);
        SetBottom(panel.transform.Find("EquipColumn"),   BottomInset);
    }

    static void SetBottom(Transform t, float inset)
    {
        if (t == null) return;
        var rt = (RectTransform)t;
        rt.offsetMin = new Vector2(rt.offsetMin.x, inset);
    }

    static void Wire(SerializedObject so, GameObject panel)
    {
        so.FindProperty("panel").objectReferenceValue = panel;

        var grid = panel.transform.Find("GridContainer") as RectTransform;
        so.FindProperty("gridContainer").objectReferenceValue = grid;

        var titleBar = panel.transform.Find("TitleBar");
        if (titleBar != null)
        {
            var title = titleBar.Find("Title") ?? titleBar.Find("TitleText");
            if (title != null)
                so.FindProperty("titleText").objectReferenceValue = title.GetComponent<TextMeshProUGUI>();

            var close = titleBar.Find("CloseButton");
            if (close != null)
            {
                so.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>();

                // 복제라 이미 새 패널을 가리키지만, 원본이 딴 것을 가리키고 있었을 수 있어 못 박는다.
                var cb = close.GetComponent<CloseButton>();
                if (cb != null)
                {
                    var cbSO = new SerializedObject(cb);
                    cbSO.FindProperty("target").objectReferenceValue = panel;
                    cbSO.ApplyModifiedProperties();
                }
            }
        }

        var column = panel.transform.Find("EquipColumn");
        BindSlot(so, column, "mainHandSlot", "MainHandSlot");
        BindSlot(so, column, "offHandSlot",  "OffHandSlot");
        BindSlot(so, column, "headSlot",     "HeadSlot");
        BindSlot(so, column, "chestSlot",    "ChestSlot");
        BindSlot(so, column, "handsSlot",    "HandsSlot");
        BindSlot(so, column, "legsSlot",     "LegsSlot");
    }

    static void BindSlot(SerializedObject so, Transform column, string field, string childName)
    {
        if (column == null) { Debug.LogWarning($"[CorpsePanel] EquipColumn이 없어 {field}를 못 물렸습니다."); return; }

        var child = column.Find(childName);
        if (child == null) { Debug.LogWarning($"[CorpsePanel] {childName}을(를) 못 찾았습니다."); return; }

        so.FindProperty(field).objectReferenceValue = child.GetComponent<EquipSlotUI>();
    }
}

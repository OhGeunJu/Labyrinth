using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 창고 패널에 **용량 표시 + 묶음 필터 바**를 더한다.
//
// 기존 `GridContainer`는 **건드리지 않는다** — `InventoryGridUI`의 슬롯 배선이 살아 있어야 하고,
// 모양도 이미 손봤을 수 있다. 위아래 여백만 조정해 새 요소가 들어갈 자리를 낸다.
//
// 여러 번 실행해도 안전 — 기존 산출물(CapacityText/FilterBar)을 지우고 다시 만든다.
// 일회성 생성기 — 산출물이 자리 잡으면 지운다.
public static class StoragePanelUpgradeCreator
{
    const string FontPath   = "Assets/NanumGothic SDF.asset";
    const string BarName    = "FilterBar";
    const string CapName    = "CapacityText";
    const float  TitleH     = 48f;   // 기존 TitleBar 높이
    const float  BarH       = 40f;
    const float  CapH       = 24f;

    [MenuItem("DungeonColony/Upgrade Storage Panel")]
    static void Create()
    {
        var uis = Object.FindObjectsByType<StorageBuildingUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (uis.Length == 0) { Debug.LogError("[StoragePanel] 씬에 StorageBuildingUI가 없습니다."); return; }

        foreach (var ui in uis) Build(ui);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[StoragePanel] 완료 — {uis.Length}개 패널 갱신 (씬 저장됨)");
    }

    static void Build(StorageBuildingUI ui)
    {
        var so    = new SerializedObject(ui);
        var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
        var grid  = so.FindProperty("gridContainer").objectReferenceValue as RectTransform;
        if (panel == null) { Debug.LogError($"[StoragePanel] {ui.name}: panel 미연결"); return; }

        foreach (var n in new[] { BarName, CapName })
        {
            var old = panel.transform.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // ── 용량 (제목 바 바로 아래, 우측 정렬) ──────────────────────────────
        var capGO = MakeRect(CapName, panel.transform);
        var capRT = capGO.GetComponent<RectTransform>();
        capRT.anchorMin = new Vector2(0, 1); capRT.anchorMax = new Vector2(1, 1);
        capRT.pivot     = new Vector2(0.5f, 1);
        capRT.anchoredPosition = new Vector2(0, -TitleH - 2f);
        capRT.sizeDelta = new Vector2(-16, CapH);
        var cap = capGO.AddComponent<TextMeshProUGUI>();
        cap.text      = "";
        cap.fontSize  = 14;
        cap.alignment = TextAlignmentOptions.MidlineRight;
        cap.color     = new Color(0.72f, 0.77f, 0.72f);
        ApplyFont(cap);

        // ── 필터 바 (하단 고정) ──────────────────────────────────────────────
        var barGO = MakeRect(BarName, panel.transform);
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 0); barRT.anchorMax = new Vector2(1, 0);
        barRT.pivot     = new Vector2(0.5f, 0);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0, BarH);
        barGO.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.95f);

        var lay = barGO.AddComponent<HorizontalLayoutGroup>();
        lay.childAlignment      = TextAnchor.MiddleCenter;
        lay.spacing             = 4f;
        lay.padding             = new RectOffset(8, 8, 6, 6);
        lay.childControlWidth   = true;  lay.childControlHeight  = true;
        lay.childForceExpandWidth = true; lay.childForceExpandHeight = true;

        // ── 필터 버튼 템플릿 (꺼둔 채로 둔다 — 런타임에 복제해 채운다) ────────
        var tplGO = MakeRect("FilterTemplate", barGO.transform);
        tplGO.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.25f);
        var tpl = tplGO.AddComponent<Button>();
        tpl.navigation = new Navigation { mode = Navigation.Mode.None };

        var tTxtGO = MakeRect("Text", tplGO.transform); SetStretch(tTxtGO);
        var tTxt = tTxtGO.AddComponent<TextMeshProUGUI>();
        tTxt.text      = "석재";
        tTxt.fontSize  = 13;
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.color     = Color.white;
        ApplyFont(tTxt);
        tplGO.SetActive(false);

        // ── 그리드 자리 조정 — 위(제목+용량)와 아래(필터 바) 사이를 채운다 ──
        if (grid != null)
        {
            grid.anchorMin = new Vector2(0, 0); grid.anchorMax = new Vector2(1, 1);
            grid.offsetMin = new Vector2(8, BarH + 6f);
            grid.offsetMax = new Vector2(-8, -(TitleH + CapH + 4f));
        }

        so.FindProperty("capacityText").objectReferenceValue    = cap;
        so.FindProperty("filterContainer").objectReferenceValue = barGO.transform;
        so.FindProperty("filterTemplate").objectReferenceValue  = tpl;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);

        Debug.Log($"[StoragePanel] {panel.name}: 용량 표시 + 필터 바 부착");
    }

    // ── 공통 헬퍼 ─────────────────────────────────────────────────────────
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

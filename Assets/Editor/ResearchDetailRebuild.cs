using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 연구대 **디테일 패널을 지우고 새로 짓는다.**
//
// 왜 다시 짓나: 기존 패널은 칸 높이가 고정이라 해금 목록이 길어지면 잘리거나 밖으로 나갔다.
// 위치만 다시 잡는 것으로는 한계가 있었다 — 애초에 **내용 길이에 따라 늘어나는 구조**가 아니었다.
// `VerticalLayoutGroup`으로 다시 지으면 글자가 길어진 만큼 칸이 알아서 늘어난다.
//
// **자리는 물려받는다** — 옛 패널의 부모·위치·크기를 그대로 쓰고 내용만 새로 만든다.
// 진행 게이지(`progressRoot`)는 건드리지 않는다: 그건 본 패널에 있고, 항목을 고르지 않아도
// 보여야 하는 정보라 디테일 안으로 들어오면 안 된다.
public static class ResearchDetailRebuild
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const string PanelName = "ResearchDetailPanel";

    static readonly Color PanelBg = new Color(0.09f, 0.09f, 0.11f, 0.98f);
    static readonly Color BarBg   = new Color(0.05098f, 0.05098f, 0.05098f, 1f);
    static readonly Color BtnBg   = new Color(0.20f, 0.32f, 0.24f, 1f);

    [MenuItem("DungeonColony/Rebuild Research Detail Panel")]
    static void Rebuild()
    {
        var uis = Object.FindObjectsByType<ResearchTableUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (uis.Length == 0) { Debug.LogError("[ResearchDetailRebuild] 씬에 ResearchTableUI가 없습니다."); return; }

        foreach (var ui in uis)
        {
            var so  = new SerializedObject(ui);
            var old = so.FindProperty("detailPanel").objectReferenceValue as GameObject;
            if (old == null) { Debug.LogError($"[ResearchDetailRebuild] {ui.name}: detailPanel이 없습니다."); continue; }

            // 자리를 물려받기 위해 부모와 RectTransform 값을 먼저 베낀다.
            var oldRT  = (RectTransform)old.transform;
            var parent = oldRT.parent;
            var (aMin, aMax, piv, pos, size) =
                (oldRT.anchorMin, oldRT.anchorMax, oldRT.pivot, oldRT.anchoredPosition, oldRT.sizeDelta);
            int siblingIndex = oldRT.GetSiblingIndex();

            Object.DestroyImmediate(old);

            var panel   = MakeRect(PanelName, parent);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = aMin; panelRT.anchorMax = aMax; panelRT.pivot = piv;
            panelRT.anchoredPosition = pos; panelRT.sizeDelta = size;
            panelRT.SetSiblingIndex(siblingIndex);
            panel.AddComponent<Image>().color = PanelBg;

            Build(panel, so);

            so.FindProperty("detailPanel").objectReferenceValue = panel;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ResearchDetailRebuild] {uis.Length}개 디테일 패널을 새로 지었습니다.");
    }

    static void Build(GameObject panel, SerializedObject so)
    {
        // ── 제목 바 (규약: #0D0D0D 고정, 좌우 풀폭) ──────────────────────────
        var bar   = MakeRect("TitleBar", panel.transform);
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot     = new Vector2(0.5f, 1);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0, 34);
        bar.AddComponent<Image>().color = BarBg;

        var title = MakeText("Title", bar.transform, "연구", 15, TextAlignmentOptions.Left);
        Stretch(title.rectTransform, 10, -10);

        // ── 내용: 세로로 쌓되 **글자 길이만큼 칸이 늘어난다** ────────────────
        // 이게 이번 재작성의 핵심이다. 고정 높이였기 때문에 해금 목록이 잘렸다.
        var content   = MakeRect("Content", panel.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(10, 48);    // 아래 [연구] 버튼 자리
        contentRT.offsetMax = new Vector2(-10, -40);  // 위 제목 바 자리

        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth  = true;  vg.childControlHeight  = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 8f;

        var time    = MakeText("TimeText",     content.transform, "연구 시간: —", 12, TextAlignmentOptions.TopLeft);
        var desc    = MakeText("DescText",     content.transform, "",             12, TextAlignmentOptions.TopLeft);
        var prereq  = MakeText("PrereqText",   content.transform, "",             12, TextAlignmentOptions.TopLeft);
        var ingred  = MakeText("IngredText",   content.transform, "",             12, TextAlignmentOptions.TopLeft);

        // ── [연구] 버튼: 바닥 고정 ──────────────────────────────────────────
        var btn   = MakeRect("ResearchButton", panel.transform);
        var btnRT = btn.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0, 0); btnRT.anchorMax = new Vector2(1, 0);
        btnRT.pivot     = new Vector2(0.5f, 0);
        btnRT.anchoredPosition = new Vector2(0, 6);
        btnRT.sizeDelta = new Vector2(-20, 36);
        btn.AddComponent<Image>().color = BtnBg;
        var button = btn.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var btnText = MakeText("Text", btn.transform, "연구", 13, TextAlignmentOptions.Center);
        Stretch(btnText.rectTransform, 0, 0);

        so.FindProperty("titleText").objectReferenceValue        = title;
        so.FindProperty("researchTimeText").objectReferenceValue = time;
        so.FindProperty("descriptionText").objectReferenceValue  = desc;
        so.FindProperty("prerequisiteText").objectReferenceValue = prereq;
        so.FindProperty("ingredientsText").objectReferenceValue  = ingred;
        so.FindProperty("researchButton").objectReferenceValue   = button;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────
    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt, float left, float right)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, 0); rt.offsetMax = new Vector2(right, 0);
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text,
                                    float size, TextAlignmentOptions align)
    {
        var go  = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = Color.white;
        tmp.raycastTarget = false;

        // **자동 축소를 켜지 않는다.** 칸이 내용만큼 늘어나므로 글자를 줄일 이유가 없다 —
        // 켜 두면 레이아웃이 계산되기 전 한 프레임에 9pt로 쪼그라들어 보인다.
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
        return tmp;
    }
}

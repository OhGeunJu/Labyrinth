using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **거점 바를 씬에 세운다**(2026-10-01, `OutpostBarUI`) — 한 번 돌리면 씬이 정본이다(생성기의 수명).
//
// 규격(`docs/editor_creators.md`): 뿌리 `OutpostBar_UI`는 화면 전체(캔버스 직속 — 필드 UI 묶음이라 지도에서는
// 꺼진다) · 자리는 판 `OutpostBar_Panel`이 든다(좌상단) · 폰트 NanumGothic · 특수문자 없음 · 칸은 씬의 템플릿.
// **다시 돌리면 판의 자리를 물려준다**(사람이 옮긴 것은 판이다).
public static class OutpostBarUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    const float H = 34f, SlotW = 120f, Pad = 4f;   // 칸에 층까지 적는다(`1 거점 · 2층`)

    static readonly Color PanelBg = new(0.08f, 0.08f, 0.10f, 0.85f);
    static readonly Color SlotBg  = new(0.16f, 0.16f, 0.19f, 0.95f);

    [MenuItem("DungeonColony/UI/Create Outpost Bar (거점 바)")]
    static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[거점 바] 씬에 캔버스가 없다."); return; }

        Vector2? keepPos = null;
        var old = Object.FindFirstObjectByType<OutpostBarUI>(FindObjectsInactive.Include);
        if (old != null)
        {
            var p = old.transform.Find("OutpostBar_Panel") as RectTransform;
            if (p != null) keepPos = p.anchoredPosition;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var root = MakeRect("OutpostBar_UI", canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "거점 바");
        Stretch(root);

        // 판 — 좌상단. 폭은 칸 수에 맞춰 레이아웃이 정한다(크기는 물려주지 않는다 — 버튼 수에서 나온다).
        var panel = MakeRect("OutpostBar_Panel", root.transform);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0, 1);
        prt.anchoredPosition = keepPos ?? new Vector2(12, -12);
        panel.AddComponent<Image>().color = PanelBg;
        var hg = panel.AddComponent<HorizontalLayoutGroup>();
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = hg.childForceExpandHeight = false;
        hg.spacing = Pad; hg.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
        var fit = panel.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 지휘소·상단 버튼은 하단 모드 바에 선다(2026-10-09 — `UI → Setup Command Bar`).
        var slot = MakeButton("SlotTemplate", panel.transform, "1 거점 - 1층", SlotW, SlotBg);

        var ui = root.AddComponent<OutpostBarUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue         = panel;
        so.FindProperty("slotParent").objectReferenceValue    = panel.transform;
        so.FindProperty("slotTemplate").objectReferenceValue  = slot;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[거점 바] `OutpostBar_UI`를 지었다 — 좌상단. 거점이 둘 이상일 때만 뜬다(지휘소·상단은 하단 모드 바).");
    }

    static Button MakeButton(string name, Transform parent, string label, float w, Color bg)
    {
        var go = MakeRect(name, parent);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = H;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };

        var t = MakeRect("Text", go.transform);
        Stretch(t);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        ApplyFont(tmp);
        return b;
    }

    // ── 헬퍼(규격 문서의 것) ─────────────────────────────────────────────────

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent != null ? parent.gameObject.layer : 5;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void ApplyFont(TextMeshProUGUI t)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
    }
}

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 스탯·특성 설명 툴팁을 씬에 만든다. `DungeonColony → Create Hover Tooltip UI`
//
// **구조가 요점이다** — 컴포넌트는 늘 켜져 있는 부모(`HoverTooltip_UI`)에 붙고, 보였다
// 사라지는 것은 자식 `Panel`이다. 컴포넌트를 꺼지는 오브젝트에 붙이면 `Awake`가 안 돌아
// `Instance`가 null로 남고 툴팁이 한 번도 안 뜬다(`ItemInfoUI`에서 실제로 겪은 사고).
public static class HoverTooltipUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const float  MaxWidth = 400f;

    [MenuItem("DungeonColony/Create Hover Tooltip UI")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        var existing = Object.FindFirstObjectByType<HoverTooltipUI>();
        if (existing != null)
        {
            Debug.LogWarning($"[HoverTooltipUICreator] 이미 있습니다 — {existing.name}. 지우고 다시 실행하세요.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // 늘 켜져 있는 부모 (컴포넌트가 여기 산다)
        var root = MakeRect("HoverTooltip_UI", canvas.transform);
        SetStretch(root);
        root.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

        // 보였다 사라지는 상자 — 커서를 따라다니므로 **좌상단 피벗**에 중앙 앵커.
        // (`ItemInfoUI`와 같은 규약: 위치 계산이 캔버스 중심 기준이다.)
        var panel   = MakeRect("Panel", root.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot     = new Vector2(0f, 1f);
        panelRT.sizeDelta = new Vector2(MaxWidth, 100f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05098f, 0.05098f, 0.05098f, 0.97f);   // #0D0D0D — 창 바탕과 같은 색
        // **커서를 가로채지 않는다.** 툴팁이 레이캐스트를 먹으면 그 밑의 글자에서 벗어난 것으로
        // 판정돼 툴팁이 깜빡인다.
        bg.raycastTarget = false;

        var outline = panel.AddComponent<Outline>();
        outline.effectColor    = new Color(0.35f, 0.32f, 0.22f, 1f);
        outline.effectDistance = new Vector2(1f, -1f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 9, 10);
        layout.spacing = 3f;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;

        // 내용 길이가 제각각이라 상자가 따라 커진다.
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var title = MakeText(panel.transform, "Title", 19f,
                             new Color(0.90980f, 0.77255f, 0.40000f), FontStyles.Bold);   // #E8C566 — 창 제목색
        var body  = MakeText(panel.transform, "Body", 16f,
                             new Color(0.82f, 0.82f, 0.84f), FontStyles.Normal);

        var comp = root.AddComponent<HoverTooltipUI>();
        var so   = new SerializedObject(comp);
        so.FindProperty("panel").objectReferenceValue     = panel;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue  = body;
        so.ApplyModifiedProperties();

        panel.SetActive(false);

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        Debug.Log("[HoverTooltipUICreator] 생성 완료 — 스탯 창(C)에서 스탯·특성에 마우스를 올려 보세요.");
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, float size, Color color, FontStyles style)
    {
        var go = MakeRect(name, parent);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize      = size;
        tmp.color         = color;
        tmp.fontStyle     = style;
        tmp.richText      = true;
        tmp.raycastTarget = false;
        ApplyFont(tmp);

        // 폭은 여기서 묶고 높이는 내용이 정한다 — 설명 길이가 제각각이라 줄바꿈이 필요하다.
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = MaxWidth - 24f;   // 좌우 패딩만큼 뺀다
        return tmp;
    }

    // ── 공통 헬퍼 ─────────────────────────────────────────────────────────────

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
}

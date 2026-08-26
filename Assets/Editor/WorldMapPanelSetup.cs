using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// **지도 창의 규격은 씬이 갖는다**(2026-08-26).
//
// 이 저장소가 UI에 쓰는 방식 그대로 — 창 크기·칸·컨테이너는 도구가 씬에 세우고, 그 안을
// 채우는 것만 런타임이 한다. 코드가 창을 넓히면 **편집 중에 보이는 모양과 게임의 모양이 갈린다.**
//
// **세계가 정사각(29,440m)이라 지도도 정사각이다** — 비율이 다르면 표식이 지형 위에서 어긋난다.
public static class WorldMapPanelSetup
{
    const float PanelSize = 900f;   // 1920×1080 기준
    const float Margin    = 24f;

    [MenuItem("DungeonColony/Terrain/4. Setup World Map Panel")]
    static void Setup()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogWarning("[지도] 씬에 Canvas가 없다."); return; }

        var existing = canvas.transform.Find("WorldMap_Panel");
        var panel = existing != null ? existing.gameObject : NewPanel(canvas.transform);

        var comp = panel.GetComponent<WorldMapPanel>();
        var holder = canvas.GetComponent<WorldMapPanel>();

        // **컴포넌트는 늘 켜져 있는 곳에 산다** — 꺼지는 오브젝트에 붙이면 `Update`가 안 돌아
        // M키가 한 번도 안 먹는다(`ItemInfoUI`·`HoverTooltipUI`가 밟은 함정과 같은 자리).
        if (comp != null) Object.DestroyImmediate(comp);
        if (holder == null) holder = Undo.AddComponent<WorldMapPanel>(canvas.gameObject);

        var img = EnsureChild(panel.transform, "Map", PanelSize - Margin * 2f);
        var raw = img.GetComponent<RawImage>() ?? Undo.AddComponent<RawImage>(img);
        raw.color = Color.white;

        var markers = EnsureChild(panel.transform, "Markers", PanelSize - Margin * 2f);
        var mrt = markers.GetComponent<RectTransform>();
        // **표식 판은 클릭을 안 먹는다** — 지도 그림이 받아야 한다.
        var mimg = markers.GetComponent<Image>();
        if (mimg != null) Object.DestroyImmediate(mimg);

        var template = markers.transform.Find("MarkerTemplate")?.gameObject;
        if (template == null)
        {
            template = new GameObject("MarkerTemplate", typeof(RectTransform), typeof(Image));
            template.transform.SetParent(markers.transform, false);
            Undo.RegisterCreatedObjectUndo(template, "표식 원본");
        }

        var trt = (RectTransform)template.transform;
        // **앵커를 가운데로 못 박는다** — 기본값이면 `anchoredPosition`이 모서리 기준이 되어
        // 표식이 전부 한쪽 구석에 몰린다.
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(8f, 8f);

        var timg = template.GetComponent<Image>();
        timg.raycastTarget = false;
        // **스프라이트가 없으면 `Image`는 아무것도 안 그린다** — 표식이 "안 보이는" 정체가
        // 이것이다(오브젝트는 생겼는데 화면에 없다). 유니티 기본 동그라미를 쓴다.
        if (timg.sprite == null)
            timg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        timg.color = Color.white;

        template.SetActive(false);

        var click = img.GetComponent<WorldMapClickArea>() ?? Undo.AddComponent<WorldMapClickArea>(img);

        var so = new SerializedObject(holder);
        so.FindProperty("panel").objectReferenceValue          = panel;
        so.FindProperty("mapImage").objectReferenceValue       = raw;
        so.FindProperty("markerRoot").objectReferenceValue     = mrt;
        so.FindProperty("markerTemplate").objectReferenceValue = template;
        so.ApplyModifiedPropertiesWithoutUndo();

        var cs = new SerializedObject(click);
        cs.FindProperty("panel").objectReferenceValue = holder;
        cs.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        EditorUtility.SetDirty(canvas.gameObject);
        Selection.activeGameObject = panel;

        Debug.Log($"[지도] 창을 세웠다 — {PanelSize}×{PanelSize}. M키로 연다. " +
                  $"세계 {TerrainGrid.WorldSize}m를 정사각으로 그린다.");
    }

    static GameObject NewPanel(Transform parent)
    {
        var go = new GameObject("WorldMap_Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(PanelSize, PanelSize);
        go.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        Undo.RegisterCreatedObjectUndo(go, "지도 창");
        return go;
    }

    static GameObject EnsureChild(Transform parent, string name, float size)
    {
        var t = parent.Find(name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
        }
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        return go;
    }
}

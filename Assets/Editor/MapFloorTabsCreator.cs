using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **지도 창에 층 탭을 붙인다**(2026-10-01, `WorldMapPanel.floorTabs`) — 한 번 돌리면 씬이 정본이다.
//
// 지도 그림의 **왼쪽 위 모서리 안쪽에 겹쳐** 띄운다 — 창마다 헤더 높이가 달라 그림 바깥에 두면 무엇과 겹칠지 모른다.
// 이미 있으면 지우고 다시 세운다(자리는 물려준다).
public static class MapFloorTabsCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    [MenuItem("DungeonColony/UI/Add Map Floor Tabs (지도 층 탭)")]
    static void Create()
    {
        var map = Object.FindFirstObjectByType<WorldMapPanel>(FindObjectsInactive.Include);
        if (map == null) { Debug.LogError("[지도 층] 씬에 `WorldMapPanel`이 없다."); return; }

        var so = new SerializedObject(map);
        var image = so.FindProperty("mapImage").objectReferenceValue as RawImage;
        if (image == null) { Debug.LogError("[지도 층] 지도 그림(`mapImage`)이 비어 있다."); return; }

        var parent = image.transform.parent;
        Vector2? keep = null;
        var old = parent.Find("FloorTabs");
        if (old != null) { keep = ((RectTransform)old).anchoredPosition; Undo.DestroyObjectImmediate(old.gameObject); }

        // 그림의 왼쪽 위 모서리 — 그림 자리를 그대로 따르게 같은 앵커 틀에서 잰다.
        var irt = image.rectTransform;
        var corners = new Vector3[4];
        irt.GetWorldCorners(corners);                    // 0 좌하 · 1 좌상 · 2 우상 · 3 우하
        var prt = (RectTransform)parent;
        var tl = prt.InverseTransformPoint(corners[1]);
        var tr = prt.InverseTransformPoint(corners[2]);

        var row = new GameObject("FloorTabs", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(row, "지도 층 탭");
        row.transform.SetParent(parent, false);
        row.layer = parent.gameObject.layer;
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.pivot = new Vector2(0, 1);
        // 부모 국소 좌표는 부모의 피벗에서 잰다 — 가운데 앵커의 기준점(`rect.center`)만큼 옮겨 맞춘다.
        var mid = prt.rect.center;
        rrt.anchoredPosition = keep ?? new Vector2(tl.x + 8 - mid.x, tl.y - 8 - mid.y);
        rrt.sizeDelta = new Vector2(tr.x - tl.x - 16, 28);
        var hg = row.AddComponent<HorizontalLayoutGroup>();
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = hg.childForceExpandHeight = false;
        hg.spacing = 4;

        var tabs = new Button[WorldMap.MaxFloor];
        for (int i = 0; i < tabs.Length; i++)
        {
            var go = new GameObject($"Floor{i + 1}", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);
            go.layer = row.layer;
            var img = go.AddComponent<Image>(); img.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
            var b = go.AddComponent<Button>(); b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 52; le.preferredHeight = 26;
            Label(go.transform, $"{i + 1}층", 13, TextAlignmentOptions.Center);
            tabs[i] = b;
        }

        var noteGo = new GameObject("FloorNote", typeof(RectTransform));
        noteGo.transform.SetParent(row.transform, false);
        noteGo.layer = row.layer;
        var nle = noteGo.AddComponent<LayoutElement>(); nle.preferredWidth = 260; nle.preferredHeight = 26;
        var note = Label(noteGo.transform, "1층 - 지금 서 있는 곳", 13, TextAlignmentOptions.MidlineLeft);
        note.rectTransform.offsetMin = new Vector2(8, 0);

        var arr = so.FindProperty("floorTabs");
        arr.arraySize = tabs.Length;
        for (int i = 0; i < tabs.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
        so.FindProperty("floorNote").objectReferenceValue = note;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[지도 층] 층 탭 넷을 지도 그림 왼쪽 위에 붙였다.");
    }

    static TextMeshProUGUI Label(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
        return t;
    }
}

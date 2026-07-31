using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 자원 노드 패널의 채굴 진행 표시를 다시 짠다.
//   · 퍼센트 텍스트를 **StockRow(아이콘 옆)** 로 옮긴다 — 수량과 같은 줄에서 읽히게.
//   · 진행 막대를 **Content 아래**에 만든다.
//
// **첫 시도가 안 보였던 이유**: 막대를 패널 직속으로 만들고 `GaugeText`의 anchoredPosition을
// 베꼈는데, 그 텍스트는 `Content` 안(레이아웃 그룹 자식)에 있었다. **부모가 다르면 좌표는
// 무의미하다** — 엉뚱한 자리로 갔다. 레이아웃이 자리를 잡는 곳에서는 좌표를 베끼지 말고
// 그 레이아웃의 자식으로 넣고 `LayoutElement`로 크기만 말할 것.
//
// 여러 번 실행해도 안전. 일회성 생성기 — 다 쓰면 지운다.
public static class NodeGaugeCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const string BarName  = "GaugeBar";
    const float  BarH     = 18f;

    [MenuItem("DungeonColony/Add Resource Node Gauge")]
    static void Create()
    {
        var panels = Object.FindObjectsByType<ResourceNodePanel>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length == 0) { Debug.LogError("[NodeGauge] 씬에 ResourceNodePanel이 없습니다."); return; }

        foreach (var p in panels) Build(p);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[NodeGauge] 완료 — {panels.Length}개 패널 갱신 (씬 저장됨)");
    }

    static void Build(ResourceNodePanel ui)
    {
        var so    = new SerializedObject(ui);
        var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
        var gText = so.FindProperty("gaugeText").objectReferenceValue as TextMeshProUGUI;
        if (panel == null) { Debug.LogError($"[NodeGauge] {ui.name}: panel 미연결"); return; }

        // 이전 실행이 남긴 것을 어디에 있든 지운다(첫 시도는 패널 직속에 만들었다).
        foreach (var rt in panel.GetComponentsInChildren<RectTransform>(true))
            if (rt != null && rt.gameObject.name == BarName)
            { Object.DestroyImmediate(rt.gameObject); break; }

        var content  = FindChild(panel.transform, "Content");
        var stockRow = FindChild(panel.transform, "StockRow");
        if (content == null)
        {
            Debug.LogError($"[NodeGauge] {panel.name}: 'Content'를 찾지 못했습니다.");
            return;
        }

        // ── 1) 퍼센트를 아이콘 옆으로 ────────────────────────────────────────
        if (gText != null && stockRow != null)
        {
            gText.transform.SetParent(stockRow, false);
            gText.transform.SetAsLastSibling();          // 아이콘·수량 뒤에
            gText.alignment = TextAlignmentOptions.MidlineRight;
            gText.fontSize  = 15;
            gText.color     = new Color(0.86f, 0.72f, 0.40f);
            ApplyFont(gText);

            var le = gText.GetComponent<LayoutElement>() ?? gText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth  = 60f;
            le.flexibleWidth   = 0f;
            le.minWidth        = 44f;
        }

        // ── 2) 진행 막대 (Content의 자식 = 레이아웃이 자리를 잡는다) ─────────
        var bar = MakeRect(BarName, content);
        bar.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.95f);   // 홈

        var barLE = bar.AddComponent<LayoutElement>();
        barLE.preferredHeight = BarH;
        barLE.minHeight       = BarH;
        barLE.flexibleWidth   = 1f;

        // 재고 줄 바로 아래로. 없으면 맨 위.
        bar.transform.SetSiblingIndex(stockRow != null ? stockRow.GetSiblingIndex() + 1 : 0);

        var fillGO = MakeRect("Fill", bar.transform);
        SetStretch(fillGO);
        var fill = fillGO.AddComponent<Image>();
        // Filled 타입은 **스프라이트가 있어야** fillAmount가 먹는다.
        // 없으면 항상 꽉 찬 것처럼 보인다(연구 게이지에서도 같은 함정).
        fill.sprite     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type       = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        fill.color      = new Color(0.85f, 0.68f, 0.30f, 1f);   // 채굴 = 광석 색

        so.FindProperty("gaugeFill").objectReferenceValue = fill;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);

        Debug.Log($"[NodeGauge] {panel.name}: 퍼센트를 StockRow로, 진행 막대를 Content 아래에 부착");
    }

    // 이름으로 자식을 깊이 우선 탐색(중첩된 계층에서도 찾게).
    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
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

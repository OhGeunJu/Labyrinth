using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 씬의 연구대 패널 하단에 진행 게이지(막대 + %)를 붙이고 ResearchTableUI에 연결한다.
// 게이지는 **선택한 항목이 아니라 그 연구대가 지금 돌리고 있는 연구**를 보여주므로
// 디테일 패널이 아니라 메인 패널에 단다.
//
// 일회성 생성기 — 산출물이 씬에 자리 잡으면 지운다(docs/editor_creators.md "생성기의 수명").
public static class ResearchProgressCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const string RootName = "ProgressBar";
    const float  BarHeight = 34f;

    [MenuItem("DungeonColony/Add Research Progress Bar")]
    static void Create()
    {
        // 이 씬의 UI는 상당수가 꺼진 채 저장돼 있다 — 비활성을 반드시 포함해 찾는다.
        var uis = Object.FindObjectsByType<ResearchTableUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (uis.Length == 0) { Debug.LogError("[ResearchProgress] 씬에 ResearchTableUI가 없습니다."); return; }

        foreach (var ui in uis) Build(ui);

        AssetDatabase.SaveAssets();
        // AssetDatabase.SaveAssets()는 **에셋만** 저장한다 — 씬은 따로 저장해야 디스크에 남는다.
        // EditorUtility.SetDirty만으로는 씬이 더티로 표시되지 않아 저장 대상에서 빠진다.
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ResearchProgress] 완료 — ResearchTableUI {uis.Length}개 처리 (씬 저장됨)");
    }

    static void Build(ResearchTableUI ui)
    {
        var so    = new SerializedObject(ui);
        var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (panel == null) { Debug.LogError($"[ResearchProgress] {ui.name}: panel이 연결돼 있지 않습니다."); return; }

        // 다시 돌려도 안전하게 — 기존 것을 지우고 새로 만든다.
        var old = panel.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // ── 묶음 (하단 고정 바) ────────────────────────────────────────────
        var root   = MakeRect(RootName, panel.transform);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0, 0); rootRT.anchorMax = new Vector2(1, 0);
        rootRT.pivot     = new Vector2(0.5f, 0);
        rootRT.anchoredPosition = new Vector2(0, 8);
        rootRT.sizeDelta = new Vector2(-16, BarHeight);   // 좌우 8px씩 여백

        // ── 배경(홈) ──────────────────────────────────────────────────────
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.10f, 0.12f, 0.95f);

        // ── 채움 ──────────────────────────────────────────────────────────
        // Image.fillAmount를 쓰려면 Type=Filled + sprite가 있어야 한다.
        // 스프라이트 없이 두면 fillAmount가 무시되고 항상 꽉 찬 상태로 보인다.
        var fillGO = MakeRect("Fill", root.transform);
        SetStretch(fillGO);
        var fill = fillGO.AddComponent<Image>();
        fill.sprite      = BuiltinSprite();
        fill.type        = Image.Type.Filled;
        fill.fillMethod  = Image.FillMethod.Horizontal;
        fill.fillOrigin  = (int)Image.OriginHorizontal.Left;
        fill.fillAmount  = 0f;
        fill.color       = new Color(0.36f, 0.66f, 0.94f, 1f);   // 연구 = 청색 계열

        // ── 텍스트 ('제련술  42%') ────────────────────────────────────────
        var txtGO = MakeRect("Label", root.transform);
        SetStretch(txtGO);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "";
        tmp.fontSize  = 15;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        ApplyFont(tmp);

        // ── 연결 ──────────────────────────────────────────────────────────
        so.FindProperty("progressRoot").objectReferenceValue = root;
        so.FindProperty("progressFill").objectReferenceValue = fill;
        so.FindProperty("progressText").objectReferenceValue = tmp;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);

        root.SetActive(false);   // 진행 중일 때만 켜진다
        Debug.Log($"[ResearchProgress] {panel.name} 하단에 게이지 부착");
    }

    // Unity 내장 UISprite — 별도 에셋 없이 Filled 이미지를 쓰려면 스프라이트가 필요하다.
    static Sprite BuiltinSprite() =>
        AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

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

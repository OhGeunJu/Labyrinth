using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 배낭 창(I키) 액션 바 **바로 위 한 줄**을 짓는다: `무게 12/15kg` + [짐내림] + [잠금].
//
// 왜 생성기인가 — 한때 `InventoryUI`가 이 셋을 런타임에 만들었다. 그러면 편집 중에는
// 없는 것이라 자리·크기를 눈으로 맞출 수가 없고(잠금 버튼이 화면에 안 잡히는 걸 Play 해 보기
// 전엔 몰랐다), 인스펙터에서 옮길 수도 없다. 씬에 실물로 세워 두고 코드는 값만 넣는다.
//
// 다시 실행하면 이전 `CarryBar`를 지우고 새로 짓는다(자리를 손봤다면 다시 손봐야 한다).
public static class CarryBarCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const string BarName  = "CarryBar";

    const float BarHeight = 20f;
    const float BarGap    = 4f;    // 액션 바 윗변과의 틈
    const float Inset     = 10f;   // 패널 왼쪽 끝에 딱 붙으면 글자가 테두리를 문다

    const float WeightW   = 116f;
    const float BtnW      = 52f;
    const float BtnH      = 18f;
    const float BtnGap    = 4f;

    static readonly Color OffBg    = new Color(0.24f, 0.24f, 0.28f, 1f);
    static readonly Color OffLabel = new Color(0.55f, 0.55f, 0.58f, 1f);
    static readonly Color WeightFg = new Color(0.82f, 0.82f, 0.86f, 1f);

    [MenuItem("DungeonColony/Create Carry Bar")]
    static void Create()
    {
        // 이 씬은 필드 오브젝트가 꺼진 채 저장돼 있는 것이 많다 — 비활성을 포함해 찾는다.
        var panels = Object.FindObjectsByType<InventoryUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (panels == null || panels.Length == 0)
        {
            Debug.LogError("[CarryBarCreator] 씬에 PawnInventoryPanel이 없습니다.");
            return;
        }

        // 씬은 먼저 잡아 둔다 — 생성기가 오브젝트를 지우고 만드는 동안 참조가 죽을 수 있다.
        var scene = panels[0].gameObject.scene;

        foreach (var target in panels) BuildFor(target);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[CarryBarCreator] 적재 바 생성 완료 ({panels.Length}개 패널)");
    }

    static void BuildFor(InventoryUI target)
    {
        var so = new SerializedObject(target);

        var panelGO  = so.FindProperty("panel").objectReferenceValue as GameObject;
        var actionGO = so.FindProperty("actionBar").objectReferenceValue as GameObject;
        if (panelGO == null)
        {
            Debug.LogError($"[CarryBarCreator] {target.name}: panel 필드가 비어 있습니다.");
            return;
        }

        // 액션 바와 **같은 부모**에 둔다 — 액션 바를 씬에서 옮기면 이 줄도 같은 칸에 산다.
        var actionRT = actionGO != null ? actionGO.GetComponent<RectTransform>() : null;
        var parent   = actionRT != null ? actionRT.parent : panelGO.transform;

        var existing = parent.Find(BarName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var bar   = MakeRect(BarName, parent);
        var barRT = bar.GetComponent<RectTransform>();

        // 가로는 부모 폭을 따라 늘어나고, 세로는 액션 바 위에 얹는다.
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot     = new Vector2(0f, 0f);
        barRT.sizeDelta = new Vector2(0f, BarHeight);

        // 액션 바가 없으면 패널 바닥에서 띄운다(액션 바는 판매/루팅 때만 켜지지만 자리는 늘 있다).
        float bottom = BarGap;
        if (actionRT != null)
            bottom = actionRT.anchoredPosition.y
                   + actionRT.rect.height * (1f - actionRT.pivot.y) + BarGap;
        barRT.anchoredPosition = new Vector2(0f, bottom);

        // ── 무게 글자 ────────────────────────────────────────────────────────
        var weight   = MakeRect("WeightText", bar.transform);
        var weightRT = weight.GetComponent<RectTransform>();
        weightRT.anchorMin = weightRT.anchorMax = new Vector2(0f, 0.5f);
        weightRT.pivot     = new Vector2(0f, 0.5f);
        weightRT.sizeDelta = new Vector2(WeightW, BarHeight);
        weightRT.anchoredPosition = new Vector2(Inset, 0f);

        var weightTMP = weight.AddComponent<TextMeshProUGUI>();
        weightTMP.text      = "무게 0/0kg";
        weightTMP.fontSize  = 12;
        weightTMP.alignment = TextAlignmentOptions.Left;
        weightTMP.color     = WeightFg;
        weightTMP.raycastTarget = false;   // 글자가 버튼 클릭을 먹지 않게
        ApplyFont(weightTMP);

        // ── 버튼 둘 ──────────────────────────────────────────────────────────
        float x = Inset + WeightW + BtnGap;
        var unload = MakeToggleButton(bar.transform, "UnloadToggle", "짐내림", x, out var unloadTMP);

        x += BtnW + BtnGap;
        var lockBtn = MakeToggleButton(bar.transform, "LockToggle", "잠금", x, out var lockTMP);

        // ── 배선 ─────────────────────────────────────────────────────────────
        so.FindProperty("weightText").objectReferenceValue   = weightTMP;
        so.FindProperty("unloadButton").objectReferenceValue = unload;
        so.FindProperty("unloadLabel").objectReferenceValue  = unloadTMP;
        so.FindProperty("lockButton").objectReferenceValue   = lockBtn;
        so.FindProperty("lockLabel").objectReferenceValue    = lockTMP;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    static Button MakeToggleButton(Transform parent, string name, string label, float x,
                                   out TextMeshProUGUI tmp)
    {
        var go = MakeRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(BtnW, BtnH);
        rt.anchoredPosition = new Vector2(x, 0f);

        go.AddComponent<Image>().color = OffBg;

        var btn = go.AddComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        var txtGO = MakeRect("Text", go.transform);
        SetStretch(txtGO);
        tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 10;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = OffLabel;
        tmp.raycastTarget = false;
        ApplyFont(tmp);

        return btn;
    }

    // ── 공통 헬퍼 ────────────────────────────────────────────────────────────

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

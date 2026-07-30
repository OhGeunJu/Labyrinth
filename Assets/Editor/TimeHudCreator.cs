using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// DungeonColony → Create Time HUD
//
// 화면 상단 가운데에 시계 + 배속 버튼을 짓는다. 필드 UI이므로 **캔버스 직속**(`Canvas/WorldUI` 밖).
// 배속은 `Time.timeScale`이라 월드맵에서도 그대로 걸리지만, HUD 자체는 필드 UI 규칙을 따른다.
//
// 다 쓰면 지운다(생성기의 수명). 모양 수정은 인스펙터에서.
public static class TimeHudCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const string RootName = "Time_UI";

    static readonly Color BarBg    = new(0.06f, 0.06f, 0.08f, 0.88f);
    static readonly Color BtnOff   = new(0.20f, 0.20f, 0.24f, 1f);

    // 라벨은 **한글/ASCII만** — NanumGothic SDF에 없는 특수문자를 쓰면 □로 떨어진다.
    static readonly (string label, float speed)[] Speeds =
    {
        ("정지", 0f), ("1배", 1f), ("3배", 3f), ("10배", 10f),
    };

    [MenuItem("DungeonColony/Create Time HUD")]
    static void Create()
    {
        var canvas = FindFieldCanvas();
        if (canvas == null) { Debug.LogError("[TimeHUD] 씬에 Canvas가 없습니다."); return; }

        var old = canvas.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = MakeRect(RootName, canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "Create Time HUD");
        SetStretch(root);

        // 상단 가운데 바
        var bar   = MakeRect("TimeBar", root.transform);
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 1f);
        barRT.pivot     = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = new Vector2(0f, -8f);
        barRT.sizeDelta = new Vector2(420f, 40f);
        bar.AddComponent<Image>().color = BarBg;

        // 시계
        var clockGO = MakeRect("Clock", bar.transform);
        var crt = clockGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0, 1);
        crt.pivot = new Vector2(0, 0.5f);
        crt.anchoredPosition = new Vector2(14f, 0f);
        crt.sizeDelta = new Vector2(210f, 0f);
        var clock = clockGO.AddComponent<TextMeshProUGUI>();
        clock.text = "1일차   06:00   낮";
        clock.fontSize = 15;
        clock.alignment = TextAlignmentOptions.Left;
        clock.color = Color.white;
        ApplyFont(clock);

        // 배속 버튼 — 오른쪽에서 왼쪽으로 쌓는다
        var buttons = new List<Button>();
        const float w = 52f, gap = 4f;
        for (int i = 0; i < Speeds.Length; i++)
        {
            int fromRight = Speeds.Length - 1 - i;
            var b = MakeRect($"Speed_{Speeds[i].label}", bar.transform);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-8f - fromRight * (w + gap), 0f);
            rt.sizeDelta = new Vector2(w, 28f);
            b.AddComponent<Image>().color = BtnOff;

            var btn = b.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var t = MakeRect("Label", b.transform); SetStretch(t);
            var tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = Speeds[i].label; tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
            ApplyFont(tmp);

            buttons.Add(btn);
        }

        var comp = root.AddComponent<TimeHUD>();
        var so   = new SerializedObject(comp);
        so.FindProperty("clockText").objectReferenceValue = clock;

        var btnList = so.FindProperty("speedButtons");
        var spdList = so.FindProperty("speeds");
        btnList.ClearArray(); spdList.ClearArray();
        for (int i = 0; i < buttons.Count; i++)
        {
            btnList.InsertArrayElementAtIndex(i);
            btnList.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            spdList.InsertArrayElementAtIndex(i);
            spdList.GetArrayElementAtIndex(i).floatValue = Speeds[i].speed;
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = bar;

        Debug.Log("[TimeHUD] 생성 완료 — 상단 가운데에 '일차 / 시각 / 낮·밤'과 배속 버튼.\n" +
                  "  스페이스 = 일시정지 토글(풀면 직전 배속으로 복귀).\n" +
                  "  하루가 기본 3600초라 검증할 땐 10배로 두거나 GameTimeManager.dayDuration을 줄이세요.");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    static Canvas FindFieldCanvas()
    {
        var view = Object.FindFirstObjectByType<WorldMapView>();
        var mapRoot = view != null ? view.MapRoot : null;

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (c == null) continue;
            if (mapRoot != null && c.transform.IsChildOf(mapRoot)) continue;
            return c;
        }
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

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// **모드 버튼 바를 씬에 세운다**(2026-08-29).
//
// 건설 `B` · 채굴 `K` · 운반 `H` — 셋 다 **껐다 켜는 상태**인데 화면에 아무 표시가 없었다.
// 키만 있으면 **있는 줄도 모르고**, 켜져 있는지도 모른다. 특히 채굴은 지하에서 가장 자주 쓰는
// 조작이라 이게 없으면 게임을 시작할 수가 없다.
//
// **규격은 도구가 씬에 세우고 상태는 런타임이 읽는다**(`docs/editor_creators.md`) —
// `ModeBarUI`는 아무것도 안 들고 매 프레임 각 모드에 물어본다.
public static class ModeBarSetup
{
    const string BarName = "ModeBar_UI";

    // 화면 **왼쪽 아래**. 오른쪽은 무리 목록, 위쪽은 시계가 이미 쓴다.
    static readonly Vector2 ButtonSize = new(96f, 34f);
    const float Gap     = 6f;
    const float Margin  = 14f;

    static readonly (string label, ModeBarUI.Mode mode)[] Modes =
    {
        ("건설 (B)", ModeBarUI.Mode.Build),
        ("채굴 (K)", ModeBarUI.Mode.Mine),
        ("운반 (H)", ModeBarUI.Mode.Haul),
    };

    [MenuItem("DungeonColony/Setup Mode Bar")]
    public static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[모드바] 씬에 Canvas가 없다."); return; }

        // **이미 있으면 지우고 다시 만든다** — 모양을 고쳤을 때 옛것이 남으면 둘이 겹쳐
        // 눌리지 않는 버튼이 생긴다(창 생성기들이 쓰는 규칙 그대로).
        //
        // **다만 자리는 지킨다**(2026-08-29, 사용자가 손으로 옮겼다). 도구를 다시 돌렸다고
        // 손으로 맞춘 자리가 원래대로 돌아가면 **다시 돌릴 엄두가 안 난다** — 뿌리기 규칙과
        // 장소 배치가 이미 쓰는 판단이다. 모양만 새로 만들고 `RectTransform`은 물려준다.
        bool  keepPlace = false;
        Vector2 keepMin = default, keepMax = default, keepPivot = default, keepPos = default;
        Vector2 keepSize = default;

        var old = canvas.transform.Find(BarName);
        if (old != null)
        {
            var ort = (RectTransform)old;
            keepPlace = true;
            keepMin   = ort.anchorMin;
            keepMax   = ort.anchorMax;
            keepPivot = ort.pivot;
            keepPos   = ort.anchoredPosition;
            keepSize  = ort.sizeDelta;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        float h = Modes.Length * ButtonSize.y + (Modes.Length - 1) * Gap;

        // **캔버스의 직속 자식이다** — `GameModeManager`가 필드/월드를 갈아 끼울 때 다루는
        // 단위가 그것이고, `WindowRaiser`도 "창"을 그렇게 센다.
        var bar = UIHelper.MakePanel(canvas.transform, BarName,
                                     new Vector2(ButtonSize.x + Margin, h + Margin),
                                     new Vector2(0f, 0f),                 // 왼쪽 아래 기준
                                     new Vector2(Margin, Margin));
        Undo.RegisterCreatedObjectUndo(bar, "모드바");
        bar.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.09f, 0.85f);

        var ui = bar.AddComponent<ModeBarUI>();
        var so = new SerializedObject(ui);
        var slots = so.FindProperty("slots");
        slots.arraySize = Modes.Length;

        for (int i = 0; i < Modes.Length; i++)
        {
            var (label, mode) = Modes[i];

            // 아래에서 위로 쌓는다 — 첫 항목(건설)이 맨 아래.
            float y = Margin * 0.5f + i * (ButtonSize.y + Gap);

            var btn = UIHelper.MakeButton(bar.transform, "Btn_" + mode, label,
                                          Vector2.zero, ButtonSize, 13f);
            var rt = (RectTransform)btn.transform;
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(Margin * 0.5f, y);

            var e = slots.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Button").objectReferenceValue = btn;
            e.FindPropertyRelative("Mode").enumValueIndex = (int)mode;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // 옛 자리를 물려준다. **크기까지 물려준다** — 버튼 수가 그대로면 크기도 그대로여야
        // 하고, 사용자가 늘려 뒀다면 그것도 뜻이 있다.
        if (keepPlace)
        {
            var rt = (RectTransform)bar.transform;
            rt.anchorMin        = keepMin;
            rt.anchorMax        = keepMax;
            rt.pivot            = keepPivot;
            rt.anchoredPosition = keepPos;
            rt.sizeDelta        = keepSize;
        }

        EditorUtility.SetDirty(ui);
        Selection.activeGameObject = bar;

        Debug.Log($"[모드바] 왼쪽 아래에 버튼 {Modes.Length}개를 세웠다 — 건설·채굴·운반.\n" +
                  "켜져 있으면 호박색으로 물든다. **키와 같은 상태를 읽으므로** 키로 켜도 따라온다.\n" +
                  "**씬을 저장할 것.**");
    }
}

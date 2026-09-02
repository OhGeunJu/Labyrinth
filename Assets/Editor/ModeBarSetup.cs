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
    const string BarName   = "ModeBar_UI";
    const string PanelName = "ModeBar_Panel";

    // 화면 **왼쪽 아래**. 오른쪽은 무리 목록, 위쪽은 시계가 이미 쓴다.
    static readonly Vector2 ButtonSize = new(96f, 34f);
    const float Gap     = 6f;
    const float Margin  = 14f;

    // **키가 있는 것만 괄호를 단다.** 구역은 일부러 키를 안 줬으므로(자주 쓰는 조작이
    // 아니고, 키가 늘수록 *있는 줄 모르는 조작*이 는다) 이 버튼이 켜는 유일한 문이다 —
    // 없는 키를 적어 두면 그것이 곧 거짓말이다.
    static readonly (string label, ModeBarUI.Mode mode)[] Modes =
    {
        ("건설 (B)", ModeBarUI.Mode.Build),
        ("채굴 (K)", ModeBarUI.Mode.Mine),
        ("운반 (H)", ModeBarUI.Mode.Haul),
        ("밭 구역",  ModeBarUI.Mode.FarmZone),
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

        var old = canvas.transform.Find(BarName);
        if (old != null)
        {
            var ort = (RectTransform)old;
            keepPlace = true;
            keepMin   = ort.anchorMin;
            keepMax   = ort.anchorMax;
            keepPivot = ort.pivot;
            keepPos   = ort.anchoredPosition;
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

        // **창 규약을 지킨다**(`docs/editor_creators.md`): `<기준>_UI`는 **스크립트만** 든
        // 늘 켜져 있는 뿌리이고, 보였다 사라지는 그림은 자식 `<기준>_Panel`이 든다.
        // `MakePanel`이 뿌리에 `Image`를 붙이므로 여기서 떼고 자식에 옮긴다.
        Object.DestroyImmediate(bar.GetComponent<Image>());

        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(bar.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.09f, 0.85f);

        var ui = bar.AddComponent<ModeBarUI>();
        var so = new SerializedObject(ui);
        var slots = so.FindProperty("slots");
        slots.arraySize = Modes.Length;

        for (int i = 0; i < Modes.Length; i++)
        {
            var (label, mode) = Modes[i];

            // 아래에서 위로 쌓는다 — 첫 항목(건설)이 맨 아래.
            float y = Margin * 0.5f + i * (ButtonSize.y + Gap);

            var btn = UIHelper.MakeButton(panel.transform, "Btn_" + mode, label,
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

        slots.serializedObject.FindProperty("panel").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 옛 **자리**를 물려준다 — 손으로 옮겨 둔 것은 뜻이 있다.
        //
        // **크기는 안 물려준다**(2026-09-02 수정). 전에는 크기까지 물려줬는데, **버튼이 하나
        // 늘자 판은 셋짜리 그대로라 넷째 버튼이 판 밖으로 삐져나왔다**(사용자 화면). 크기는
        // *손으로 정한 값*이 아니라 **버튼 수에서 나오는 값**이다 — 물려주면 개수를 바꿀
        // 때마다 손으로 다시 맞춰야 하고, 안 맞춘 채로 남는다.
        if (keepPlace)
        {
            var rt = (RectTransform)bar.transform;
            rt.anchorMin        = keepMin;
            rt.anchorMax        = keepMax;
            rt.pivot            = keepPivot;
            rt.anchoredPosition = keepPos;
        }

        EditorUtility.SetDirty(ui);
        Selection.activeGameObject = bar;

        Debug.Log($"[모드바] 왼쪽 아래에 버튼 {Modes.Length}개를 세웠다 — 건설·채굴·운반.\n" +
                  "켜져 있으면 호박색으로 물든다. **키와 같은 상태를 읽으므로** 키로 켜도 따라온다.\n" +
                  "**씬을 저장할 것.**");
    }
}

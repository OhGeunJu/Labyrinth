using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// **진형 바를 씬에 세운다**(2026-09-08).
//
// **규격은 도구가 씬에 세우고 상태는 런타임이 읽는다**(`docs/editor_creators.md`) -
// `FormationBarUI`는 아무것도 안 들고 매 프레임 `Formation.Current`에 물어본다.
//
// 그전에는 고른 진형이 **알림 한 줄로만** 떴다. 그건 누른 직후에만 보이므로 *지금 무엇으로
// 서 있는지*를 확인할 방법이 없었다 - **찾을 수 없는 스위치는 없는 스위치다.**
//
// `ModeBarSetup`과 같은 규칙 셋을 그대로 따른다:
//   - **이미 있으면 지우고 다시 만든다**(모양을 고쳤을 때 옛것이 남으면 겹쳐서 안 눌린다)
//   - **자리는 물려준다**(손으로 옮겨 둔 것은 뜻이 있다 - 도구가 덮으면 다시 돌릴 엄두가 안 난다)
//   - **크기는 안 물려준다**(버튼 수에서 나오는 값이지 손으로 정하는 값이 아니다)
public static class FormationBarSetup
{
    const string BarName   = "FormationBar_UI";
    const string PanelName = "FormationBar_Panel";

    // 가로 한 줄. 넷뿐이고 이름이 두 글자라 세로로 쌓을 이유가 없다(모드 바는 넷이 세로인데,
    // 그쪽은 **모드**라 늘 떠 있고 이쪽은 소집 중에만 뜬다 - 겹칠 일이 없다).
    static readonly Vector2 ButtonSize = new(82f, 32f);
    const float Gap    = 6f;
    const float Margin = 10f;

    // 모드 바 위에 놓는다(처음 세울 때만 - 그 뒤로는 씬에서 옮긴 자리가 답이다).
    const float FirstY = 190f;

    // == 뿌리는 화면 전체, 자리는 판이 든다 (2026-09-08 사용자 결정) ==========
    //
    // `Xxx_UI`(뿌리)는 **캔버스 전체로 늘린다**. 자리와 크기는 자식 `Xxx_Panel`이 든다.
    // 씬의 창 서른둘 중 스물하나가 이미 그 모양이었고, 사용자가 나머지를 그쪽으로 맞추고 있다.
    //
    // 이유 셋:
    //   - **만지는 자리가 하나다.** 뿌리에도 자리가 있고 판에도 있으면 *어느 쪽을 옮겨야
    //     하나*가 창마다 달라진다 - 실제로 지금 씬이 그 상태였다.
    //   - **해상도가 바뀌어도 안 밀린다.** 뿌리가 점 하나로 앵커되면 그 점만 따라가고
    //     안쪽은 절대 좌표라, 다른 해상도에서 판이 화면 모서리를 놓친다.
    //   - **커서를 따라다니는 창**(툴팁 둘)은 뿌리가 캔버스와 같은 크기여야 좌표가 맞는다 -
    //     그 창들만 예외로 두는 대신 **전부 그렇게 둔다.**
    //
    // 그래서 **물려주는 것도 판의 자리다.** 뿌리를 물려줘 봐야 늘 같은 값이고, 정작 사람이
    // 옮긴 것은 판이다 - 뿌리만 지키면 다시 돌릴 때마다 **손으로 맞춘 자리가 날아간다.**


    // **키를 같이 적는다** - 이 버튼이 켜는 유일한 문이 아니고, 오히려 손이 빠른 쪽은 키다.
    static readonly (string label, Formation.Shape shape)[] Shapes =
    {
        ("방진 (F1)", Formation.Shape.Box),
        ("횡대 (F2)", Formation.Shape.Line),
        ("종대 (F3)", Formation.Shape.Column),
        ("쐐기 (F4)", Formation.Shape.Wedge),
    };

    [MenuItem("DungeonColony/Setup Formation Bar")]
    public static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[진형 바] 씬에 Canvas가 없다."); return; }

        // 처음 세울 때의 자리 - 왼쪽 아래, 모드 바 위.
        var placeMin   = new Vector2(0f, 0f);
        var placeMax   = new Vector2(0f, 0f);
        var placePivot = new Vector2(0f, 0f);
        var placePos   = new Vector2(Margin, FirstY);

        var old = canvas.transform.Find(BarName);
        if (old != null)
        {
            var oldPanel = old.Find(PanelName) as RectTransform;
            if (oldPanel != null)
            {
                placeMin   = oldPanel.anchorMin;
                placeMax   = oldPanel.anchorMax;
                placePivot = oldPanel.pivot;
                placePos   = oldPanel.anchoredPosition;
            }
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        float w = Shapes.Length * ButtonSize.x + (Shapes.Length - 1) * Gap;

        // **캔버스의 직속 자식이고 화면 전체를 덮는다** - `GameModeManager`가 필드/월드를
        // 갈아 끼울 때 다루는 단위가 그것이고, `WindowRaiser`도 "창"을 그렇게 센다.
        var bar = new GameObject(BarName, typeof(RectTransform));
        bar.transform.SetParent(canvas.transform, false);
        var brt = (RectTransform)bar.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(bar, "진형 바");

        // 창 규약: `<기준>_UI`는 **스크립트만** 든 늘 켜진 뿌리, 보였다 사라지는 그림은
        // 자식 `<기준>_Panel`이 들고 **자리도 그쪽이 든다.**
        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(bar.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin        = placeMin;
        prt.anchorMax        = placeMax;
        prt.pivot            = placePivot;
        prt.anchoredPosition = placePos;

        // **크기는 안 물려준다** - 버튼 수에서 나오는 값이지 손으로 정하는 값이 아니다
        // (모드 바가 크기까지 물려줬다가 버튼이 하나 늘자 판 밖으로 삐져나왔다).
        prt.sizeDelta = new Vector2(w + Margin, ButtonSize.y + Margin);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.09f, 0.85f);

        var ui = bar.AddComponent<FormationBarUI>();
        var so = new SerializedObject(ui);
        var slots = so.FindProperty("slots");
        slots.arraySize = Shapes.Length;

        for (int i = 0; i < Shapes.Length; i++)
        {
            var (label, shape) = Shapes[i];

            var btn = UIHelper.MakeButton(panel.transform, "Btn_" + shape, label,
                                          Vector2.zero, ButtonSize, 13f);
            var rt = (RectTransform)btn.transform;
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(Margin * 0.5f + i * (ButtonSize.x + Gap),
                                              Margin * 0.5f);

            var e = slots.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Button").objectReferenceValue = btn;
            e.FindPropertyRelative("Shape").enumValueIndex = (int)shape;
        }

        so.FindProperty("panel").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ui);
        Selection.activeGameObject = bar;

        Debug.Log($"[진형 바] 왼쪽 아래(모드 바 위)에 버튼 {Shapes.Length}개를 세웠다.\n" +
                  "**소집한 폰을 고르고 있을 때만 뜬다** - 진형이 실제로 먹는 조건과 같다.\n" +
                  "고른 진형이 호박색으로 물들고, **키(F1~F4)로 바꿔도 따라온다.**\n" +
                  "**씬을 저장할 것.**");
    }
}

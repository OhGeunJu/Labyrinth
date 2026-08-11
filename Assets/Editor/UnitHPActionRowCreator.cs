using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// `UnitHP_Panel` 아래쪽에 **동작 버튼 줄**을 만든다. `DungeonColony → Create UnitHP Action Row`
//
// 이 패널은 이제 폰만 보여주는 곳이 아니다 — 청사진을 누르면 [건설을 취소한다], 다 지은 건물을
// 누르면 [이동한다]·[분해한다]가 뜬다. 버튼 **모양은 씬이 갖고**(`ActionTemplate`을 복제한다)
// 코드는 글자와 눌렀을 때만 채운다.
//
// **앵커를 조심할 것.** 지난번 건설 하단 바에 붙인 버튼이 안 보였던 건, 그 바가 가로 스트레치
// 앵커인데 `sizeDelta.x`가 1920이라 **폭이 화면+1920**이었기 때문이다(오른쪽 끝이 960px 밖).
// 이 패널은 코너 앵커 300×400이라 그 함정이 없고, 줄은 스트레치로 붙이되 `sizeDelta.x`를
// **음수 여백**으로 준다(자식 `Text`가 -12를 쓰는 것과 같은 규약).
//
// 다 쓰면 지운다 — `docs/editor_creators.md` "생성기의 수명". 이후 수정은 인스펙터에서.
public static class UnitHPActionRowCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    // **세로로 쌓는다.** 가로로 늘어놓으면 300px 패널에서 버튼 하나가 140px이라 글자가
    // 잘리고, 버튼이 셋이 되는 순간 더 좁아진다. 세로면 폭이 늘 같고 글자도 안 잘린다.
    const float BtnH    = 26f;
    const float Spacing = 4f;
    const int   MaxBtns = 2;    // 지금 가장 많은 경우(건물: 이동·분해)
    const float RowH    = BtnH * MaxBtns + Spacing * (MaxBtns - 1);
    const float Margin  = 6f;   // 패널 안쪽 여백 (자식 Text의 -12 = 좌우 6씩과 같은 값)

    [MenuItem("DungeonColony/Create UnitHP Action Row")]
    static void Create()
    {
        var ui = Object.FindFirstObjectByType<UnitHPUI>(FindObjectsInactive.Include);
        if (ui == null) { Debug.LogError("[UnitHPActionRow] 씬에 UnitHPUI가 없습니다."); return; }

        var so      = new SerializedObject(ui);
        var panelGO = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (panelGO == null) { Debug.LogError("[UnitHPActionRow] UnitHPUI.panel이 비어 있습니다."); return; }

        // 다시 돌려도 되도록 먼저 지운다.
        var old = panelGO.transform.Find("ActionRow");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // ── 줄 ─────────────────────────────────────────────────────────────
        var rowGO = MakeRect("ActionRow", panelGO.transform);
        var rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin        = new Vector2(0f, 0f);
        rowRT.anchorMax        = new Vector2(1f, 0f);
        rowRT.pivot            = new Vector2(0.5f, 0f);
        rowRT.sizeDelta        = new Vector2(-Margin * 2f, RowH);   // 폭 = 패널 - 좌우 여백
        rowRT.anchoredPosition = new Vector2(0f, Margin);

        // 세로로 쌓되 **아래에서 위로** 채운다 — 버튼이 하나뿐일 때 위쪽이 비면 글자 칸과
        // 사이가 벌어져 떠 보인다. 줄 높이는 최대 개수로 고정하고 남는 자리는 비운다
        // (개수마다 패널이 들썩이면 무엇이 어디 있는지 외울 수가 없다).
        var layout = rowGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = Spacing;
        layout.childAlignment         = TextAnchor.LowerCenter;
        layout.childControlWidth      = true;
        // **높이도 그룹이 잡는다.** `childControlHeight = false`로 두면 그룹이 크기를 안 정하고
        // 자식의 `RectTransform`을 그대로 쓰는데, 복제본은 그 값이 제멋대로라 버튼이 커진다.
        // 정할 사람을 안 두면 아무도 안 정한다 — `LayoutElement`의 값이 실제로 먹게 하려면
        // 그룹이 제어해야 한다.
        layout.childControlHeight     = true;
        layout.childForceExpandWidth  = true;
        // 남는 자리를 나눠 갖지 않는다 — 늘리면 버튼 하나일 때 줄 높이만큼 부풀어 오른다.
        layout.childForceExpandHeight = false;

        // ── 복제할 버튼 하나 ────────────────────────────────────────────────
        var btnGO = MakeRect("ActionTemplate", rowGO.transform);
        var bg    = btnGO.AddComponent<Image>();
        bg.color  = new Color(0.24f, 0.24f, 0.26f, 1f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.navigation    = new Navigation { mode = Navigation.Mode.None };

        // 최소·선호를 **같은 값으로** 못 박는다. 하나만 주면 남는 자리에 따라 늘었다 줄었다 한다.
        var le = btnGO.AddComponent<LayoutElement>();
        le.minHeight        = BtnH;
        le.preferredHeight  = BtnH;
        le.flexibleHeight   = 0f;

        var labelGO = MakeRect("Label", btnGO.transform);
        SetStretch(labelGO);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = "동작";
        tmp.fontSize      = 15f;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;   // 커서를 먹으면 버튼이 안 눌린다
        ApplyFont(tmp);

        // 템플릿은 원본이라 꺼 둔다 — `UnitHPUI.Awake`도 끄지만, 편집 중에도 빈 버튼이
        // 하나 떠 있으면 실제 모양과 달라 보인다.
        btnGO.SetActive(false);

        // ── 스크롤 뷰를 줄만큼 비켜 준다 ────────────────────────────────────
        //
        // **글자(`TextMeshProUGUI`)를 건드리면 안 된다.** 한때 `GetComponentInChildren`으로
        // 글자를 찾아 줄였는데, 이 패널의 글자는 `ScrollRect` 안 `Content`의 자식이고
        // **코너 앵커**다 — 그쪽에서 `sizeDelta`는 여백이 아니라 **크기 자체**여서, 스트레치인
        // 줄 알고 계산한 값을 넣으니 폭 100·높이 -74가 됐다. 폭 100에 글자 13이면 일곱 자마다
        // 접힌다(그전에는 줄바꿈이 꺼져 있어 그냥 넘쳐 흘러 안 보였을 뿐이다).
        //
        // 줄여야 하는 것은 **패널 직속 자식인 스크롤 뷰**다. 그건 스트레치라 `sizeDelta`가 여백이다.
        var viewRT = FindScrollView(panelGO, rowGO);
        if (viewRT != null)
        {
            // **절대값으로 잡는다** — 지금 값에서 빼면 두 번 돌릴 때 두 번 줄어든다.
            float take = RowH + Margin;
            viewRT.sizeDelta        = new Vector2(-Margin * 2f, -Margin * 2f - take);
            viewRT.anchoredPosition = new Vector2(0f, take * 0.5f);
        }
        else
        {
            Debug.LogWarning("[UnitHPActionRow] 내용 영역을 못 찾아 겹칠 수 있습니다 — 인스펙터에서 조정하세요.");
        }

        // ── 글자가 스크롤 폭을 쓰게 한다 ────────────────────────────────────
        //
        // `Content`의 `VerticalLayoutGroup`이 `childControlWidth = false`였다. 그러면 그룹이
        // 자식 폭을 안 정하고 자식이 들고 있는 값(여기선 100)을 그대로 쓴다 — 줄바꿈을 켜자
        // 일곱 자마다 접힌 이유가 이것이다. **정할 사람을 안 두면 아무도 안 정한다.**
        var content = panelGO.GetComponentInChildren<VerticalLayoutGroup>(true);
        if (content != null && content.transform != rowGO.transform)
        {
            content.childControlWidth     = true;
            content.childForceExpandWidth = true;
            EditorUtility.SetDirty(content);
        }

        // ── 연결 ───────────────────────────────────────────────────────────
        so.FindProperty("actionRow").objectReferenceValue      = rowGO.transform;
        so.FindProperty("actionTemplate").objectReferenceValue = btn;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(panelGO);

        // **씬에 사는 것은 씬을 직접 저장해야 남는다** — `SaveAssets()`는 에셋만 저장한다.
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Selection.activeGameObject = rowGO;
        Debug.Log("[UnitHPActionRow] UnitHP_Panel에 동작 버튼 줄을 만들고 씬을 저장했습니다.");
    }

    // 내용이 들어 있는 **패널 직속 자식**. 이름이 아니라 구조로 찾는다 —
    // 방금 만든 버튼 줄만 빼면 남는 것이 그것이다(지금은 `ScrollRect` 하나뿐).
    static RectTransform FindScrollView(GameObject panelGO, GameObject exclude)
    {
        foreach (RectTransform child in panelGO.transform)
        {
            if (child.gameObject == exclude) continue;
            if (child.GetComponent<ScrollRect>() != null) return child;
        }
        // 스크롤이 아니어도 직속 자식이 하나뿐이면 그게 내용 영역이다.
        foreach (RectTransform child in panelGO.transform)
            if (child.gameObject != exclude) return child;
        return null;
    }

    // ── 공통 헬퍼 ───────────────────────────────────────────────────────────
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

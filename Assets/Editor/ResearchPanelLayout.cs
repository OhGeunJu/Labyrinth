using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 연구대 **디테일 패널을 다시 잡는다.**
//
// 왜: 칸이 한두 줄짜리라 해금 목록이 길어지면 잘려서 한두 개밖에 안 보였다. 글자를 줄이는 것으론
// 한계가 있어서 **자리 자체를 다시 나눈다** — 위에서부터 제목·시간·설명·선행/해금·재료를 쌓고,
// 진행 게이지와 [연구] 버튼은 바닥에 붙인다.
//
// **아무것도 만들거나 지우지 않는다.** 이미 배선된 오브젝트의 RectTransform만 다시 잡으므로
// 여러 번 실행해도 안전하고, 참조가 끊길 일이 없다.
public static class ResearchPanelLayout
{
    // 위에서부터 쌓는 높이. 해금 목록이 들어갈 칸을 가장 넉넉하게 준다.
    const float Pad        = 12f;
    const float TitleH     = 30f;
    const float TimeH      = 22f;
    const float DescH      = 86f;
    const float PrereqH    = 76f;   // 선행 연구 + 해금 목록 — 여기가 부족했다
    const float IngredH    = 90f;
    const float Gap        = 6f;

    const float ButtonH    = 38f;
    const float ProgressH  = 22f;

    [MenuItem("DungeonColony/Relayout Research Detail Panel")]
    static void Relayout()
    {
        // 이 씬은 UI가 꺼진 채로 저장돼 있다 — 비활성 포함으로 찾는다.
        var uis = Object.FindObjectsByType<ResearchTableUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (uis.Length == 0)
        {
            Debug.LogError("[ResearchPanelLayout] 씬에 ResearchTableUI가 없습니다.");
            return;
        }

        foreach (var ui in uis)
        {
            var so     = new SerializedObject(ui);
            var detail = so.FindProperty("detailPanel").objectReferenceValue as GameObject;
            if (detail == null)
            {
                Debug.LogError($"[ResearchPanelLayout] {ui.name}: detailPanel이 연결돼 있지 않습니다.");
                continue;
            }

            // **패널 높이 안에 넣는다.** 고정 높이로 쌓기만 하면 패널보다 길어져
            // 아래 칸들이 패널 밖으로 나가고, 마스크가 있으면 통째로 안 보인다.
            float panelH = ((RectTransform)detail.transform).rect.height;
            float need   = TitleH + TimeH + DescH + PrereqH + IngredH + Gap * 4;
            float room   = panelH - Pad * 2 - ButtonH - Gap;
            float k      = (room > 0f && need > room) ? room / need : 1f;   // 넘치면 비율대로 줄인다

            if (k < 1f)
                Debug.Log($"[ResearchPanelLayout] {ui.name}: 패널 높이 {panelH:F0} < 필요 {need:F0} " +
                          $"— 칸을 {k * 100f:F0}%로 줄여 맞춥니다.");

            float y = Pad;

            StackTop(so, "titleText",        detail, ref y, TitleH  * k, 16f, false);
            StackTop(so, "researchTimeText", detail, ref y, TimeH   * k, 12f, false);
            StackTop(so, "descriptionText",  detail, ref y, DescH   * k, 12f, true);
            StackTop(so, "prerequisiteText", detail, ref y, PrereqH * k, 12f, true);
            StackTop(so, "ingredientsText",  detail, ref y, IngredH * k, 12f, true);

            // 바닥에 [연구] 버튼.
            //
            // `progressRoot`는 **일부러 건드리지 않는다** — 그건 디테일 패널이 아니라 본 패널에 있다.
            // 진행 게이지는 "지금 이 연구대가 무엇을 돌리는가"라서 레시피를 고르지 않아도 보여야 하고,
            // 디테일 안으로 옮기면 항목을 고를 때만 보이게 된다. 지금 자리가 맞다.
            PinBottom(so, "researchButton", detail, Pad, ButtonH);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        }

        // **씬을 직접 저장한다.** SaveAssets는 씬을 저장하지 않는다.
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ResearchPanelLayout] {uis.Length}개 패널을 다시 배치했습니다.");
    }

    // 위에서부터 좌우 풀폭으로 한 칸씩 쌓는다.
    static void StackTop(SerializedObject so, string field, GameObject parent,
                         ref float y, float height, float fontSize, bool wrap)
    {
        var rt = RectOf(so, field, parent);
        if (rt == null) return;

        // **offsetMin/Max와 sizeDelta를 같이 쓰지 않는다.** 둘은 같은 값을 다른 방식으로 쓰는 것이라
        // 섞으면 마지막 대입이 앞의 것을 덮어 높이가 0이 되기도 한다(글자가 통째로 사라지는 원인).
        // 여기서는 앵커 + anchoredPosition + sizeDelta만 쓴다.
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta        = new Vector2(-Pad * 2, Mathf.Max(14f, height));

        // 글자 설정도 여기서 못 박는다 — 칸을 넓혀도 자동 축소가 꺼져 있으면 여전히 잘린다.
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode       = TextOverflowModes.Ellipsis;
            tmp.enableAutoSizing   = wrap;      // 여러 줄 칸만 줄어들게
            tmp.fontSizeMin        = 9f;
            tmp.fontSizeMax        = fontSize;
            if (!wrap) tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.TopLeft;
        }

        y += height + Gap;
    }

    // 바닥에서 `fromBottom` 만큼 띄워 좌우 풀폭으로 붙인다.
    static void PinBottom(SerializedObject so, string field, GameObject parent,
                          float fromBottom, float height)
    {
        var rt = RectOf(so, field, parent);
        if (rt == null) return;

        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, fromBottom);
        rt.sizeDelta = new Vector2(-Pad * 2, height);
    }

    // 배선된 필드에서 RectTransform을 꺼낸다. 어떤 필드는 GameObject, 어떤 건 컴포넌트다.
    static RectTransform RectOf(SerializedObject so, string field, GameObject parent)
    {
        var prop = so.FindProperty(field);
        if (prop == null || prop.objectReferenceValue == null)
        {
            Debug.LogWarning($"[ResearchPanelLayout] '{field}'이(가) 비어 있어 건너뜁니다.");
            return null;
        }

        var go = prop.objectReferenceValue as GameObject
              ?? (prop.objectReferenceValue as Component)?.gameObject;
        var rt = go != null ? go.GetComponent<RectTransform>() : null;

        // 디테일 패널 밖의 것을 옮기면 엉뚱한 곳이 망가진다 — 계층을 확인하고 건너뛴다.
        if (rt != null && !rt.IsChildOf(parent.transform))
        {
            Debug.LogWarning($"[ResearchPanelLayout] '{field}'이(가) detailPanel 밖에 있어 건너뜁니다.");
            return null;
        }
        return rt;
    }
}

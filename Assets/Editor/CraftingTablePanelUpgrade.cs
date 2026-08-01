using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 작업대 패널에 **연료 게이지 · 산출물 칸 · 운반 허용 버튼**을 붙인다.
//
// 왜 필요했나: 제작품이 폰 인벤토리로 바로 들어가고 넘치면 바닥에 떨어져서, 무엇이 만들어졌고
// 어디로 갔는지 볼 곳이 없었다. 연료도 마찬가지 — 용광로가 왜 안 도는지 화면에 아무 단서가 없었다
// (연료 없이 폰이 작업대에 붙은 채 영원히 기다린다).
//
// 기존 배치는 건드리지 않는다. 새 구역은 패널 **아래에 매달아** 기존 자식들의 좌표가
// 하나도 안 변하게 했다 — 패널을 늘리면 안에 있는 것들이 어떻게 밀릴지 여기서 알 수 없다.
public static class CraftingTablePanelUpgrade
{
    const string FontPath    = "Assets/NanumGothic SDF.asset";
    const string SectionName = "OutputSection";

    // 산출물 기본 격자는 8×3. `InventoryGridUI.cellSize`가 48이므로 384×144가 필요하다 —
    // 구역 높이는 거기에 연료 행·라벨·여백을 더한 값이다.
    const float SectionH = 262f;
    const float ProgH    = 28f;
    const float FuelH    = 26f;

    static readonly Color SectionBg = new Color(0.09f, 0.09f, 0.11f, 0.97f);
    static readonly Color BarBg     = new Color(0.18f, 0.18f, 0.2f, 1f);
    static readonly Color GridBg    = new Color(0.13f, 0.14f, 0.16f, 1f);
    static readonly Color BtnBg     = new Color(0.2f, 0.28f, 0.2f, 1f);

    [MenuItem("DungeonColony/Upgrade Crafting Table Panel")]
    static void Upgrade()
    {
        // 이 씬은 UI가 꺼진 채로 저장돼 있다 — 비활성을 반드시 포함해 찾는다.
        var uis = Object.FindObjectsByType<CraftingTableUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (uis.Length == 0)
        {
            Debug.LogError("[CraftingTablePanelUpgrade] 씬에 CraftingTableUI가 없습니다.");
            return;
        }

        foreach (var ui in uis)
        {
            var so    = new SerializedObject(ui);
            var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
            if (panel == null)
            {
                Debug.LogError($"[CraftingTablePanelUpgrade] {ui.name}: panel이 연결돼 있지 않습니다.");
                continue;
            }

            // 다시 실행해도 안전하도록 이전 결과를 지우고 새로 짓는다.
            var old = panel.transform.Find(SectionName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Build(panel, so);
            BuildCrafterRow(so);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        }

        // **씬을 직접 저장한다.** SaveAssets는 씬을 저장하지 않아서, 이걸 빠뜨리면
        // 유니티를 닫는 순간 배선이 통째로 날아간다(2026-07-31에 겪음).
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[CraftingTablePanelUpgrade] {uis.Length}개 패널에 연료·산출물 구역을 붙였습니다.");
    }

    static void Build(GameObject panel, SerializedObject so)
    {
        // ── 구역 루트: 패널 아래에 매달린다(좌우 풀폭) ────────────────────────
        var section   = MakeRect(SectionName, panel.transform);
        var sectionRT = section.GetComponent<RectTransform>();
        sectionRT.anchorMin = new Vector2(0, 0);
        sectionRT.anchorMax = new Vector2(1, 0);
        sectionRT.pivot     = new Vector2(0.5f, 1);
        sectionRT.anchoredPosition = Vector2.zero;
        sectionRT.sizeDelta = new Vector2(0, SectionH);
        section.AddComponent<Image>().color = SectionBg;

        // ── 연료 행 (연료 쓰는 작업대에서만 켜진다 — 런타임이 결정) ───────────
        var fuelRow   = MakeRect("FuelRow", section.transform);
        var fuelRowRT = fuelRow.GetComponent<RectTransform>();
        fuelRowRT.anchorMin = new Vector2(0, 1);
        fuelRowRT.anchorMax = new Vector2(1, 1);
        fuelRowRT.pivot     = new Vector2(0.5f, 1);
        fuelRowRT.anchoredPosition = new Vector2(0, -8);
        fuelRowRT.sizeDelta = new Vector2(-24, FuelH);

        var fuelText = MakeText("FuelText", fuelRow.transform, "연료", 13, TextAlignmentOptions.Left);
        var fuelTxRT = fuelText.rectTransform;
        fuelTxRT.anchorMin = new Vector2(0, 0); fuelTxRT.anchorMax = new Vector2(0.55f, 1);
        fuelTxRT.offsetMin = Vector2.zero;      fuelTxRT.offsetMax = Vector2.zero;

        var barBg   = MakeRect("FuelBar", fuelRow.transform);
        var barBgRT = barBg.GetComponent<RectTransform>();
        barBgRT.anchorMin = new Vector2(0.57f, 0.5f);
        barBgRT.anchorMax = new Vector2(1f,    0.5f);
        barBgRT.pivot     = new Vector2(0.5f,  0.5f);
        barBgRT.anchoredPosition = Vector2.zero;
        barBgRT.sizeDelta = new Vector2(0, 14);
        barBg.AddComponent<Image>().color = BarBg;

        var fill   = MakeRect("FuelFill", barBg.transform);
        SetStretch(fill);
        var fillImg = fill.AddComponent<Image>();
        // **`Filled`는 스프라이트가 있어야 `fillAmount`가 먹는다.** 없으면 항상 꽉 찬 것처럼 보인다.
        fillImg.sprite      = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fillImg.type        = Image.Type.Filled;
        fillImg.fillMethod  = Image.FillMethod.Horizontal;
        fillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount  = 1f;
        fillImg.color       = new Color(0.35f, 0.75f, 0.35f);

        // ── 제작 진행 행 (제작 중일 때만 켜진다 — 런타임이 결정) ──────────────
        var progRow   = MakeRect("ProgressRow", section.transform);
        var progRowRT = progRow.GetComponent<RectTransform>();
        progRowRT.anchorMin = new Vector2(0, 1);
        progRowRT.anchorMax = new Vector2(1, 1);
        progRowRT.pivot     = new Vector2(0.5f, 1);
        progRowRT.anchoredPosition = new Vector2(0, -(8 + FuelH + 2));
        progRowRT.sizeDelta = new Vector2(-24, FuelH);

        var progBg = MakeRect("ProgressBar", progRow.transform);
        SetStretch(progBg);
        progBg.AddComponent<Image>().color = BarBg;

        var progFillGO = MakeRect("ProgressFill", progBg.transform);
        SetStretch(progFillGO);
        var progFill = progFillGO.AddComponent<Image>();
        progFill.sprite     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        progFill.type       = Image.Type.Filled;
        progFill.fillMethod = Image.FillMethod.Horizontal;
        progFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progFill.fillAmount = 0f;
        progFill.color      = new Color(0.35f, 0.6f, 0.9f);

        // 숫자는 막대 **위에** 얹는다 — 자리를 따로 잡으면 좁은 구역이 더 좁아진다.
        var progText = MakeText("ProgressText", progRow.transform, "", 12, TextAlignmentOptions.Center);
        SetStretch(progText.gameObject);
        progText.rectTransform.offsetMin = Vector2.zero;
        progText.rectTransform.offsetMax = Vector2.zero;

        // ── 산출물 라벨 ───────────────────────────────────────────────────────
        var label   = MakeText("OutputLabel", section.transform, "산출물", 13, TextAlignmentOptions.Left);
        var labelRT = label.rectTransform;
        labelRT.anchorMin = new Vector2(0, 1); labelRT.anchorMax = new Vector2(0, 1);
        labelRT.pivot     = new Vector2(0, 1);
        labelRT.anchoredPosition = new Vector2(12, -(8 + FuelH + ProgH + 6));
        labelRT.sizeDelta = new Vector2(120, 20);

        // ── 산출물 그리드 컨테이너 (InventoryGridUI는 런타임에 붙는다) ────────
        var grid   = MakeRect("OutputGrid", section.transform);
        var gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 1); gridRT.anchorMax = new Vector2(0, 1);
        gridRT.pivot     = new Vector2(0, 1);
        gridRT.anchoredPosition = new Vector2(12, -(8 + FuelH + ProgH + 28));
        gridRT.sizeDelta = new Vector2(384, 144);
        grid.AddComponent<Image>().color = GridBg;

        // ── 운반 허용 버튼 (우측) ─────────────────────────────────────────────
        var btn   = MakeRect("HaulToggleButton", section.transform);
        var btnRT = btn.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1, 1); btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot     = new Vector2(1, 1);
        btnRT.anchoredPosition = new Vector2(-12, -(8 + FuelH + ProgH + 28));
        btnRT.sizeDelta = new Vector2(180, 40);
        btn.AddComponent<Image>().color = BtnBg;
        var button = btn.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var btnText   = MakeText("Text", btn.transform, "운반 켜짐", 12, TextAlignmentOptions.Center);
        SetStretch(btnText.gameObject);
        btnText.rectTransform.offsetMin = Vector2.zero;
        btnText.rectTransform.offsetMax = Vector2.zero;

        // ── 배선 ──────────────────────────────────────────────────────────────
        so.FindProperty("progressRow").objectReferenceValue          = progRow;
        so.FindProperty("progressText").objectReferenceValue         = progText;
        so.FindProperty("progressFill").objectReferenceValue         = progFill;
        so.FindProperty("fuelRow").objectReferenceValue             = fuelRow;
        so.FindProperty("fuelText").objectReferenceValue            = fuelText;
        so.FindProperty("fuelFill").objectReferenceValue            = fillImg;
        so.FindProperty("outputGridContainer").objectReferenceValue = gridRT;
        so.FindProperty("haulToggleButton").objectReferenceValue    = button;
        so.FindProperty("haulToggleText").objectReferenceValue      = btnText;
    }

    // 제작자 지정 버튼은 **디테일 패널** 안에 붙인다 — 레시피별 설정이라 레시피를 고른 화면에 있어야 한다.
    // 디테일 패널 바닥에 얹으므로 기존 자식들의 자리는 그대로다.
    const string CrafterName = "CrafterRow";

    static void BuildCrafterRow(SerializedObject so)
    {
        var detail = so.FindProperty("detailPanel").objectReferenceValue as GameObject;
        if (detail == null)
        {
            Debug.LogWarning("[CraftingTablePanelUpgrade] detailPanel이 없어 제작자 버튼을 건너뜁니다.");
            return;
        }

        var old = detail.transform.Find(CrafterName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var row   = MakeRect(CrafterName, detail.transform);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 0);
        rowRT.anchorMax = new Vector2(1, 0);
        rowRT.pivot     = new Vector2(0.5f, 0);
        rowRT.anchoredPosition = new Vector2(0, 8);
        rowRT.sizeDelta = new Vector2(-24, 34);
        row.AddComponent<Image>().color = BtnBg;

        var button = row.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var text = MakeText("Text", row.transform, "제작자: 아무나", 12, TextAlignmentOptions.Center);
        SetStretch(text.gameObject);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        so.FindProperty("crafterButton").objectReferenceValue = button;
        so.FindProperty("crafterText").objectReferenceValue   = text;

        BuildCrafterPicker(so, detail);
    }

    // 제작자를 **고르는 패널**. 디테일 패널 위에 겹쳐 뜬다.
    // 목록 행은 씬의 `RowTemplate`을 복제해 채운다 — "모양은 씬이, 코드는 데이터만".
    const string PickerName = "CrafterPicker";

    static void BuildCrafterPicker(SerializedObject so, GameObject detail)
    {
        var old = detail.transform.Find(PickerName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var picker   = MakeRect(PickerName, detail.transform);
        var pickerRT = picker.GetComponent<RectTransform>();
        pickerRT.anchorMin = new Vector2(0.05f, 0.1f);
        pickerRT.anchorMax = new Vector2(0.95f, 0.9f);
        pickerRT.offsetMin = Vector2.zero;
        pickerRT.offsetMax = Vector2.zero;
        picker.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.99f);

        // 제목 바 — 규약대로 #0D0D0D 고정, 좌우 풀폭.
        var bar   = MakeRect("TitleBar", picker.transform);
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot     = new Vector2(0.5f, 1);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0, 34);
        bar.AddComponent<Image>().color = new Color(0.05098f, 0.05098f, 0.05098f, 1f);

        var title = MakeText("Title", bar.transform, "제작자 고르기", 13, TextAlignmentOptions.Left);
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 0); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = new Vector2(10, 0); titleRT.offsetMax = new Vector2(-40, 0);

        // 닫기 버튼 — 규약: 28×28, #802626, 글자는 "X", 공용 CloseButton 컴포넌트.
        var closeGO = MakeRect("CloseButton", bar.transform);
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 1); closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot     = new Vector2(1, 1);
        closeRT.anchoredPosition = new Vector2(-4, -3);
        closeRT.sizeDelta = new Vector2(28, 28);
        closeGO.AddComponent<Image>().color = new Color(0.50196f, 0.14902f, 0.14902f, 1f);
        closeGO.AddComponent<Button>();
        var closeTxt = MakeText("Text", closeGO.transform, "X", 13, TextAlignmentOptions.Center);
        SetStretch(closeTxt.gameObject);

        var closeComp = closeGO.AddComponent<CloseButton>();
        var closeSO   = new SerializedObject(closeComp);
        closeSO.FindProperty("target").objectReferenceValue = picker;
        closeSO.ApplyModifiedProperties();

        // 목록
        var content   = MakeRect("Content", picker.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(8, 8); contentRT.offsetMax = new Vector2(-8, -38);
        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 3f;

        // 행 템플릿 — 런타임에 꺼진 채로 복제 원본이 된다.
        var rowGO = MakeRect("RowTemplate", content.transform);
        rowGO.AddComponent<Image>().color = new Color(0.16f, 0.18f, 0.2f, 1f);
        var rowBtn = rowGO.AddComponent<Button>();
        rowBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.minHeight = 30f; rowLE.preferredHeight = 30f;

        var rowText = MakeText("Text", rowGO.transform, "폰 이름", 12, TextAlignmentOptions.Left);
        SetStretch(rowText.gameObject);
        rowText.rectTransform.offsetMin = new Vector2(10, 0);
        rowText.rectTransform.offsetMax = new Vector2(-10, 0);

        picker.SetActive(false);

        so.FindProperty("crafterPickerPanel").objectReferenceValue   = picker;
        so.FindProperty("crafterPickerContent").objectReferenceValue = content.transform;
        so.FindProperty("crafterRowTemplate").objectReferenceValue   = rowGO;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────────

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

    static TextMeshProUGUI MakeText(string name, Transform parent, string text,
                                    float size, TextAlignmentOptions align)
    {
        var go  = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = Color.white;
        tmp.raycastTarget = false;

        // 편집 중 생성물은 씬 폰트를 상속하지 않는다 — 직접 박는다.
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
        return tmp;
    }
}

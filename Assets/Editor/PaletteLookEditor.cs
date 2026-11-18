using UnityEditor;
using UnityEngine;

// **계절 색(팔레트)을 이름으로 고른다**(2026-11-14, 사용자: *"0 1 2 3으로 고르는 게 좀 귀찮긴 한데"*).
//
// 단추 하나가 팔레트 하나 — 누르면 `paletteOverride`에 적고 바로 칠한다(프리팹 모드면 그 프리팹 · 씬이면 그 개체의 오버라이드). 첫 단추는 씨앗.
// 단추 밑에 그 팔레트의 첫 재질 칸 색(엔트는 잎 셋)을 견본으로 깐다. 칸 색 자체를 다듬는 목록은 아래에 접어 둔다(`SkinLookEditor`와 같은 꼴).
[CustomEditor(typeof(PaletteLook))]
public class PaletteLookEditor : Editor
{
    // 팔레트 이름 → 보이는 이름(엔트 계절 — 모르는 이름은 그대로).
    static string Label(string name) => name switch
    {
        "spring" => "봄", "summer" => "여름", "autumn" => "가을", "late" => "늦가을",
        // 리자드맨
        "Leaf" => "잎빛", "Crested" => "크림 볏", "Olive" => "올리브", "Skink" => "악어도마뱀", "Abronia" => "에메랄드", "Tokay" => "토케이",
        "Leachie" => "거인", "FatTail" => "살찐꼬리", "Leopard" => "표범", "Phelsuma" => "낮도마뱀", "LeafTail" => "잎꼬리",
        "Green" => "풀빛", "Slate" => "청회색", "Coal" => "숯빛", "Amber" => "주황", "Boelen" => "보엘렌", "Kingrat" => "누런 구렁이",
        "Indigo" => "인디고", "Red" => "붉은 뱀",
        "Straw" => "짚빛", "Teal" => "청록", "Crimson" => "진홍", "Speckle" => "점박이", "Checker" => "격자", _ => name,
    };

    bool _showList;

    void OnEnable()  => Undo.undoRedoPerformed += Reapply;
    void OnDisable() => Undo.undoRedoPerformed -= Reapply;
    void Reapply()   { if (target is PaletteLook look && look != null) { look.Apply(); SceneView.RepaintAll(); } }

    public override void OnInspectorGUI()
    {
        var look = (PaletteLook)target;
        var pals = look.Palettes;
        if (pals == null || pals.Count == 0)
        {
            EditorGUILayout.HelpBox("팔레트가 없다 — 생성기가 채운다(엔트는 `Pawn/17`).", MessageType.None);
            return;
        }
        if (look.Index < 0 && Application.isPlaying) look.Apply();

        string now = look.PaletteOverride < 0
            ? (Application.isPlaying ? $"씨앗 → {Label(look.Current)}" : "씨앗 (프리팹 · 씬에서는 첫 팔레트 · 판에서는 개체마다)")
            : Label(look.Current);
        EditorGUILayout.LabelField($"색 — {now}", EditorStyles.boldLabel);

        // 단추는 줄을 감는다(리자드맨 도마뱀은 팔레트가 열하나).
        const float H = 22f, SW = 8f, BW = 78f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 30f) / (BW + 4f)));
        for (int start = -1; start < pals.Count; start += perRow)
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int idx = start; idx < Mathf.Min(pals.Count, start + perRow); idx++)
            {
                string text = idx < 0 ? "씨앗" : Label(pals[idx].Name);
                bool chosen = look.PaletteOverride == idx;
                var rect = GUILayoutUtility.GetRect(BW, H + SW + 2, GUILayout.Width(BW), GUILayout.Height(H + SW + 2));
                var btn = new Rect(rect.x, rect.y, rect.width, H);
                var old = GUI.backgroundColor;
                if (chosen) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                if (GUI.Button(btn, text, EditorStyles.miniButton)) Set(look, idx);
                GUI.backgroundColor = old;
                // 견본 — 첫 재질 칸의 색들(엔트는 잎 셋)
                if (idx >= 0)
                {
                    var swatch = Swatch(pals[idx]);
                    float w = rect.width / Mathf.Max(1, swatch.Length);
                    for (int k = 0; k < swatch.Length; k++) EditorGUI.DrawRect(new Rect(rect.x + w * k, rect.y + H + 1, w, SW), swatch[k]);
                }
            }
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.HelpBox("단추를 누르면 이 개체(프리팹 모드면 그 프리팹)의 색이 된다. '씨앗'은 프리팹 · 씬에서는 첫 팔레트이고, 판에서는 개체마다 씨앗이 고른다.", MessageType.None);

        // 반점 배치 — 씨앗 · 1 ~ N(구워 둔 벌)
        if (look.SpotLayouts > 0)
        {
            string sn = look.SpotOverride < 0 ? $"씨앗 → {look.SpotIndex + 1}번" : $"{look.SpotOverride + 1}번";
            EditorGUILayout.LabelField($"반점 배치 — {sn}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int idx = -1; idx < look.SpotLayouts; idx++)
                {
                    var old = GUI.backgroundColor;
                    if (look.SpotOverride == idx) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                    if (GUILayout.Button(idx < 0 ? "씨앗" : (idx + 1).ToString(), EditorStyles.miniButton, GUILayout.MinWidth(idx < 0 ? 40 : 26)))
                    {
                        Undo.RecordObject(look, "반점 배치 고르기");
                        look.SpotOverride = idx;
                        EditorUtility.SetDirty(look);
                        if (!Application.isPlaying) PrefabUtility.RecordPrefabInstancePropertyModifications(look);
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = old;
                }
                GUILayout.FlexibleSpace();
            }
        }

        _showList = EditorGUILayout.Foldout(_showList, "팔레트 칸 색 다듬기(생성기를 다시 돌리면 미리보기 값으로 덮인다)", true);
        if (_showList)
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("palettes"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }

    // 팔레트의 첫 재질에 칠하는 색들 — 단추 밑 견본. 꺼진 덧칠 칸(알파 0)과 겹친 색은 뺀다.
    static Color[] Swatch(PaletteLook.Palette p)
    {
        if (p.Slots == null || p.Slots.Count == 0) return new Color[0];
        string first = p.Slots[0].Material;
        var list = new System.Collections.Generic.List<Color>();
        foreach (var s in p.Slots)
        {
            if (s.Material != first || s.Raw || s.Color.a <= 0f) continue;
            var c = s.Color; c.a = 1f;
            if (!list.Contains(c)) list.Add(c);
        }
        return list.ToArray();
    }

    static void Set(PaletteLook look, int idx)
    {
        Undo.RecordObject(look, "계절 색 고르기");
        look.PaletteOverride = idx;
        EditorUtility.SetDirty(look);
        if (!Application.isPlaying) PrefabUtility.RecordPrefabInstancePropertyModifications(look);
        SceneView.RepaintAll();
    }
}

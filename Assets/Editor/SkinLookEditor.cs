using UnityEditor;
using UnityEngine;

// **살색을 인스펙터에서 고른다**(2026-11-01, 사용자: *"오거 플레임은 그거에 맞는 색도 고를 수 있고"*).
//
// 후보는 종족의 것(`UnitData.SkinTones`)만 낸다 — 붉은 오우거 칸에 푸른 색이 끼지 않는다. 견본을 누르면 `toneOverride`에 적고
// 바로 칠한다(프리팹 모드에서 고르면 그 프리팹의 저작 · 씬에서 고르면 그 개체의 오버라이드). 첫 칸은 씨앗.
[CustomEditor(typeof(SkinLook))]
public class SkinLookEditor : Editor
{
    // 되돌리면 번호만 돌아오고 칠은 그대로라 다시 칠한다(블록은 저장되지 않는다).
    void OnEnable()  => Undo.undoRedoPerformed += Reapply;
    void OnDisable() => Undo.undoRedoPerformed -= Reapply;
    void Reapply()   { if (target is SkinLook look && look != null) { look.Apply(); SceneView.RepaintAll(); } }

    public override void OnInspectorGUI()
    {
        var look  = (SkinLook)target;
        var tones = look.Tones;
        if (tones == null || tones.Count == 0)
        {
            EditorGUILayout.HelpBox("종족에 살색 후보가 없다(`UnitData.SkinTones`) — 칠하지 않는다.", MessageType.None);
            return;
        }
        if (look.ToneIndex < 0 && Application.isPlaying) look.Apply();

        // 에디터에서 못 박지 않았으면 프리팹의 기본 색(살 재질)이 보인다 — 씨앗은 판에서만 고른다(`SkinLook`).
        string now = look.ToneIndex < 0 ? "프리팹 기본 색 (판에서는 씨앗이 고른다)"
                   : $"지금 {look.ToneIndex + 1}번{(look.ToneOverride < 0 ? " (씨앗)" : "")}";
        EditorGUILayout.LabelField($"살색 — {now}", EditorStyles.boldLabel);

        const float W = 34f, H = 22f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 40f) / (W + 4f)));
        int cells = tones.Count + 1;
        for (int start = 0; start < cells; start += perRow)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(cells, start + perRow); i++)
                {
                    int idx = i - 1;   // -1 = 씨앗
                    var rect = GUILayoutUtility.GetRect(W, H, GUILayout.Width(W), GUILayout.Height(H));
                    bool chosen = look.ToneOverride == idx;
                    if (idx < 0)
                    {
                        GUI.Box(rect, "씨앗");
                    }
                    else
                    {
                        EditorGUI.DrawRect(rect, tones[idx]);
                        GUI.Label(rect, (idx + 1).ToString(), EditorStyles.miniBoldLabel);
                    }
                    if (chosen) DrawFrame(rect, Color.white);
                    else if (idx == look.ToneIndex) DrawFrame(rect, new Color(1f, 1f, 1f, 0.35f));
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        Set(look, idx);
                        Event.current.Use();
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        EditorGUILayout.HelpBox("견본을 누르면 이 개체(프리팹 모드면 그 프리팹)의 살색이 된다. '씨앗'은 프리팹 · 씬에서는 프리팹의 기본 색이고, " +
                                "판에서는 개체마다 씨앗이 고른다(명세 Seed · 없으면 이름 해시). 후보는 종족 에셋의 `SkinTones`.", MessageType.None);
    }

    static void DrawFrame(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, 2), c);
        EditorGUI.DrawRect(new Rect(r.x - 2, r.yMax, r.width + 4, 2), c);
        EditorGUI.DrawRect(new Rect(r.x - 2, r.y, 2, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax, r.y, 2, r.height), c);
    }

    static void Set(SkinLook look, int idx)
    {
        Undo.RecordObject(look, "살색 고르기");
        look.ToneOverride = idx;
        EditorUtility.SetDirty(look);
        if (!Application.isPlaying) PrefabUtility.RecordPrefabInstancePropertyModifications(look);
        SceneView.RepaintAll();
    }
}

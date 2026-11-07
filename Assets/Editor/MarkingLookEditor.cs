using UnityEditor;
using UnityEngine;

// **문신 칸은 문신 겹이 있는 몸에서만 보인다**(2026-11-06, 사용자: *"Ink는 뭔 역할인데 변화가 없어"*). 문신 색(`ink`)은 얼굴 문신 겹
// (`Head.Pick.Tat*` — 지금은 오크 머리뿐)을 칠한다. 엘프 · 드워프처럼 겹이 없는 몸에서는 바꿔도 아무것도 안 바뀌니 감춘다.
[CustomEditor(typeof(MarkingLook))]
public class MarkingLookEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        bool inked = HasTattooLayers(((MarkingLook)target).transform);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("hair"));
        if ((MarkingLook.HairTone)serializedObject.FindProperty("hair").intValue == MarkingLook.HairTone.Custom)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("customHair"));

        if (inked)
        {
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tattoo"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ink"));
            if ((MarkingLook.Ink)serializedObject.FindProperty("ink").intValue == MarkingLook.Ink.Custom)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("customInk"));
        }
        else EditorGUILayout.HelpBox("이 몸에는 얼굴 문신 겹이 없다 — 문신 · 문신 색 칸은 감췄다(오크 머리만 쓴다).", MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    static bool HasTattooLayers(Transform root)
    {
        var model = root.Find("Model");
        if (model == null) return false;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Head.Pick.Tat")) return true;
        return false;
    }
}

using UnityEditor;
using UnityEngine;
using TMPro;

// DungeonColony/Create Grade Up Notification UI 실행
// → 씬에 GradeUpNotificationUI GameObject 생성 + 폰트 자동 연결
public static class GradeUpNotificationUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    [MenuItem("DungeonColony/Create Grade Up Notification UI")]
    static void Create()
    {
        var existing = Object.FindFirstObjectByType<GradeUpNotificationUI>();
        if (existing != null)
        {
            Debug.LogWarning("[GradeUpNotificationUICreator] 씬에 이미 GradeUpNotificationUI가 있습니다. 기존 객체에 폰트만 재연결합니다.");
            WireFont(existing);
            EditorUtility.SetDirty(existing);
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("GradeUpNotification");
        Undo.RegisterCreatedObjectUndo(go, "Create Grade Up Notification UI");

        var comp = go.AddComponent<GradeUpNotificationUI>();
        WireFont(comp);

        Selection.activeGameObject = go;
        EditorUtility.SetDirty(comp);
        Debug.Log("[GradeUpNotificationUICreator] GradeUpNotification GameObject 생성 완료");
    }

    static void WireFont(GradeUpNotificationUI comp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogWarning($"[GradeUpNotificationUICreator] 폰트 미발견: {FontPath}. 인스펙터에서 직접 지정하세요.");
            return;
        }
        var so = new SerializedObject(comp);
        so.FindProperty("font").objectReferenceValue = font;
        so.ApplyModifiedProperties();
    }
}

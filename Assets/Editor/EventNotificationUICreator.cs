using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class EventNotificationUICreator
{
    [MenuItem("DungeonColony/Create Event Notification UI")]
    static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("씬에 Canvas가 없습니다.");
            return;
        }

        // 루트 패널
        var root = new GameObject("EventNotificationUI");
        root.transform.SetParent(canvas.transform, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot     = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(500f, 120f);
        rootRect.anchoredPosition = new Vector2(0f, -20f);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // 아이콘
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(root.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot     = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(80f, 80f);
        iconRect.anchoredPosition = new Vector2(20f, 0f);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.white;

        // 타이틀
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(root.transform, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0f, 1f);
        titleRect.sizeDelta = new Vector2(-130f, 36f);
        titleRect.anchoredPosition = new Vector2(120f, -12f);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "이벤트 제목";
        titleTMP.fontSize  = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color     = new Color(1f, 0.85f, 0.3f);

        // 설명
        var descGO = new GameObject("DescText");
        descGO.transform.SetParent(root.transform, false);
        var descRect = descGO.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot     = new Vector2(0f, 1f);
        descRect.sizeDelta = new Vector2(-130f, -50f);
        descRect.anchoredPosition = new Vector2(120f, -50f);
        var descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text     = "이벤트 설명";
        descTMP.fontSize = 14f;
        descTMP.color    = Color.white;

        // 닫기 버튼
        var closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(root.transform, false);
        var closeRect = closeGO.AddComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot     = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(30f, 30f);
        closeRect.anchoredPosition = new Vector2(-4f, -4f);
        var closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(0.6f, 0.1f, 0.1f);
        var closeBtn = closeGO.AddComponent<Button>();

        var closeLabelGO = new GameObject("Label");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        var closeLabelRect = closeLabelGO.AddComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.sizeDelta = Vector2.zero;
        var closeTMP = closeLabelGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text      = "✕";
        closeTMP.fontSize  = 16f;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color     = Color.white;

        // EventNotificationUI 컴포넌트 부착 및 연결
        var notifUI = root.AddComponent<EventNotificationUI>();
        var so = new SerializedObject(notifUI);
        so.FindProperty("panel").objectReferenceValue     = root;
        so.FindProperty("iconImage").objectReferenceValue = iconImg;
        so.FindProperty("titleText").objectReferenceValue = titleTMP;
        so.FindProperty("descText").objectReferenceValue  = descTMP;
        so.ApplyModifiedProperties();

        // 닫기 버튼 → Close() 연결
        closeBtn.onClick.AddListener(notifUI.Close);

        root.SetActive(false);

        Selection.activeGameObject = root;
        Undo.RegisterCreatedObjectUndo(root, "Create EventNotificationUI");
        Debug.Log("EventNotificationUI 생성 완료.");
    }
}

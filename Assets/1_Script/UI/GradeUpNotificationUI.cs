using System.Collections;
using UnityEngine;
using TMPro;

// 유닛 머리 위에 월드 공간 텍스트가 떠오르며 사라짐.
// 사용법: 씬에 빈 GameObject + 이 컴포넌트 부착. 폰트는 인스펙터에서 지정.
//
// **떠오르는 글자의 단일 출처다.** 이름은 등급 승급에서 시작했지만(그때는 그것뿐이었다)
// 지금은 설득 성공·실패도 여기로 온다 — 같은 자리에 같은 모양으로 떠야 하고, 폰트·크기·
// 지속시간이 씬에 하나만 있어야 한다. 두 벌을 만들면 하나가 반드시 다르게 보인다.
public class GradeUpNotificationUI : MonoBehaviour
{
    // **폰 머리 위에 한 줄 띄운다.** 씬에 이 컴포넌트가 없으면 조용히 아무 일도 안 한다 —
    // 알림이 없다고 게임 로직이 멈출 이유는 없다.
    public static void Popup(Transform follow, string text, Color color)
    {
        if (_instance == null || follow == null || string.IsNullOrEmpty(text)) return;
        _instance.StartCoroutine(_instance.Animate(_instance.CreatePopup(follow, text, color)));
    }

    static GradeUpNotificationUI _instance;

    [Header("월드 텍스트 설정")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField] float         heightOffset  = 2.2f;   // 유닛 머리 위 시작 높이
    [SerializeField] float         floatDistance = 1.5f;   // 위로 떠오르는 거리
    [SerializeField] float         duration      = 2.5f;   // 표시 시간 (초)
    [SerializeField] float         fontSize      = 24f;
    [SerializeField] float         worldScale    = 0.01f;  // Canvas 월드 스케일

    Camera _cam;

    void Awake()
    {
        _cam = Camera.main;
        _instance = this;
    }

    void OnEnable()  => UnitStats.OnAnyGradeUp += HandleGradeUp;
    void OnDisable() => UnitStats.OnAnyGradeUp -= HandleGradeUp;

    void OnDestroy() { if (_instance == this) _instance = null; }

    void HandleGradeUp(UnitStats stats, StatName stat, StatGrade newGrade)
    {
        if (stats.Owner == null) return;

        string text = $"{StatDisplayName(stat)} {newGrade.ToDisplay()} ↑";
        var popup = CreatePopup(stats.Owner.transform, text, newGrade.GetColor());
        StartCoroutine(Animate(popup));
    }

    GameObject CreatePopup(Transform follow, string text, Color color)
    {
        var go = new GameObject($"GradeUpPopup_{follow.name}");
        go.transform.position = follow.position + Vector3.up * heightOffset;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var canvasRT = go.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(4f, 1f);
        canvasRT.localScale = Vector3.one * worldScale;

        var tmpGO = new GameObject("Text");
        tmpGO.transform.SetParent(go.transform, false);
        var rt = tmpGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = tmpGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = Josa.Fix(text);
        tmp.fontSize      = fontSize;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = color;
        tmp.raycastTarget = false;
        tmp.fontStyle     = FontStyles.Bold;
        if (font != null) tmp.font = font;

        // 외곽선으로 가독성 (검은 그림자 효과)
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        tmp.outlineWidth = 0.25f;

        return go;
    }

    IEnumerator Animate(GameObject popup)
    {
        var tmp = popup.GetComponentInChildren<TextMeshProUGUI>();
        Color baseColor = tmp.color;
        Vector3 startPos = popup.transform.position;
        float elapsed = 0f;

        while (popup != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            popup.transform.position = startPos + Vector3.up * (floatDistance * t);

            float alpha = 1f - Mathf.Pow(t, 2f);
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                popup.transform.rotation = Quaternion.LookRotation(popup.transform.position - _cam.transform.position);

            yield return null;
        }

        if (popup != null) Destroy(popup);
    }

    // 표시명은 `StatNameExt.ToDisplay` 하나뿐이다 — 특성 요약도 같은 것을 쓴다.
    static string StatDisplayName(StatName s) => s.ToDisplay();
}

using DG.Tweening;
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
        _instance.Animate(_instance.CreatePopup(follow, text, color));
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
        Animate(CreatePopup(stats.Owner.transform, text, newGrade.GetColor()));
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

    // **떠오르는 것과 옅어지는 것을 따로 건다**(2026-08-29, 코루틴에서 DOTween으로).
    //
    // 둘의 곡선이 다르기 때문이다 — 글자는 **고르게** 오르는데 사라지는 것은 뒤로 갈수록
    // 빨라진다(옛 코루틴의 `1 − t²`가 그 모양이었다). 한 트윈에 묶으면 곡선을 하나밖에 못 준다.
    //
    // **`SetUpdate(true)` — 배속을 안 탄다.** 옛 코루틴은 `Time.deltaTime`이라 10배속에서
    // 0.25초 만에 지나가 읽을 수가 없었다. 이건 화면의 것이지 시뮬의 것이 아니다.
    void Animate(GameObject popup)
    {
        var tmp = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null) { Destroy(popup); return; }

        Color baseColor = tmp.color;
        float startY    = popup.transform.position.y;

        popup.transform.DOMoveY(startY + floatDistance, duration)
             .SetEase(Ease.Linear)
             .SetUpdate(true)
             .SetLink(popup)
             .OnUpdate(() => FaceCamera(popup.transform))
             .OnComplete(() => Destroy(popup));

        // **`DOFade`(TMP 모듈)를 안 쓴다.** 그건 DOTween Pro의 TextMeshPro 모듈을 켜야 도는데,
        // 유틸리티 패널의 체크박스 하나에 컴파일이 걸리게 만들 이유가 없다. `DOTween.To`는
        // 어느 모듈도 안 탄다.
        float alpha = 1f;
        DOTween.To(() => alpha, a =>
                 {
                     alpha = a;
                     tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                 }, 0f, duration)
               .SetEase(Ease.InQuad)
               .SetUpdate(true)
               .SetLink(popup);
    }

    // 늘 카메라를 본다. **`Camera.main`을 다시 잡는다** — 씬 전환이나 카메라 교체 뒤에도
    // 글자가 옆을 보고 서 있지 않게.
    void FaceCamera(Transform popup)
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            popup.rotation = Quaternion.LookRotation(popup.position - _cam.transform.position);
    }

    // 표시명은 `StatNameExt.ToDisplay` 하나뿐이다 — 특성 요약도 같은 것을 쓴다.
    static string StatDisplayName(StatName s) => s.ToDisplay();
}

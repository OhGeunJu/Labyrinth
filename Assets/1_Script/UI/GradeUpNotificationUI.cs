using System.Collections;
using UnityEngine;
using TMPro;

// 유닛의 등급 승급 시 머리 위에 월드 공간 텍스트가 떠오르며 사라짐.
// 사용법: 씬에 빈 GameObject + 이 컴포넌트 부착. 폰트는 인스펙터에서 지정.
public class GradeUpNotificationUI : MonoBehaviour
{
    [Header("월드 텍스트 설정")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField] float         heightOffset  = 2.2f;   // 유닛 머리 위 시작 높이
    [SerializeField] float         floatDistance = 1.5f;   // 위로 떠오르는 거리
    [SerializeField] float         duration      = 2.5f;   // 표시 시간 (초)
    [SerializeField] float         fontSize      = 24f;
    [SerializeField] float         worldScale    = 0.01f;  // Canvas 월드 스케일

    Camera _cam;

    void Awake() => _cam = Camera.main;

    void OnEnable()  => UnitStats.OnAnyGradeUp += HandleGradeUp;
    void OnDisable() => UnitStats.OnAnyGradeUp -= HandleGradeUp;

    void HandleGradeUp(UnitStats stats, StatName stat, StatGrade newGrade)
    {
        if (stats.Owner == null) return;

        string text = $"{StatDisplayName(stat)} {newGrade.ToDisplay()} ↑";
        var popup = CreatePopup(stats.Owner.transform, text, newGrade);
        StartCoroutine(Animate(popup));
    }

    GameObject CreatePopup(Transform follow, string text, StatGrade grade)
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
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = grade.GetColor();
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

    static string StatDisplayName(StatName s) => s switch
    {
        StatName.Strength          => "근력",
        StatName.Toughness         => "강인함",
        StatName.Dexterity         => "손재주",
        StatName.Cognition         => "인지력",
        StatName.MeleeAttack       => "근접공격",
        StatName.MeleeDefense      => "근접방어",
        StatName.RangedDefense     => "원거리방어",
        StatName.PrecisionShooting => "정밀사격",
        StatName.MartialArts       => "무술",
        StatName.Evasion           => "회피",
        StatName.Athletics         => "육상",
        StatName.Swimming          => "수영",
        StatName.Mining            => "채굴",
        StatName.Growing           => "원예",
        StatName.Cutting           => "벌목",
        StatName.Taming            => "조련",
        StatName.Cooking           => "요리",
        StatName.Medicine          => "의료",
        StatName.Research          => "연구",
        StatName.WeaponCrafting    => "무기제작",
        StatName.ArmorCrafting     => "갑옷제작",
        StatName.Alchemy           => "연금",
        StatName.Smelting          => "제련",
        StatName.Stonecutting      => "석재가공",
        StatName.Woodworking       => "목공",
        StatName.Gemcutting        => "보석가공",
        StatName.Labor             => "노동",
        StatName.OneHandSword      => "한손검",
        StatName.OneHandAxe        => "한손도끼",
        StatName.OneHandMace       => "한손둔기",
        StatName.TwoHandSword      => "양손검",
        StatName.TwoHandAxe        => "양손도끼",
        StatName.TwoHandMace       => "양손둔기",
        StatName.Polearm           => "장창",
        StatName.Bow               => "활",
        StatName.Crossbow          => "쇠뇌",
        StatName.Javelin           => "투창",
        StatName.ElementalMagic    => "원소마법",
        StatName.DarkMagic         => "암흑마법",
        StatName.HolyMagic         => "신성마법",
        StatName.ElementalFire     => "원소·불",
        StatName.ElementalIce      => "원소·얼음",
        StatName.ElementalLightning=> "원소·전기",
        StatName.ElementalPoison   => "원소·독",
        StatName.DarkFire          => "암흑·불",
        StatName.DarkIce           => "암흑·얼음",
        StatName.DarkLightning     => "암흑·전기",
        StatName.DarkPoison        => "암흑·독",
        StatName.HolyFire          => "신성·불",
        StatName.HolyIce           => "신성·얼음",
        StatName.HolyLightning     => "신성·전기",
        StatName.HolyPoison        => "신성·독",
        _                          => s.ToString(),
    };
}

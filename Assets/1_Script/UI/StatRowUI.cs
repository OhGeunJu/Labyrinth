using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// 스탯 한 줄 — `이름` 왼쪽, `등급` 오른쪽. **데이터 연결만 한다**(여백·글꼴·색은 씬이 갖는다).
//
// **한 덩어리 글자에 `<pos=64%>`로 자리를 흉내 내던 것을 그만뒀다.** 그 방식은 ① 값이
// **오른쪽 정렬이 안 되고**(비율 위치라 자릿수가 다르면 끝이 들쭉날쭉) ② 간격을 바꾸려면
// `lineSpacing`이라는 간접적인 손잡이를 써야 하며 ③ 설명 툴팁이 `<link>` 파싱에 얹혀 있어
// 글자 덩어리가 늘 때마다 훑는 곳이 늘었다. 줄이 오브젝트면 셋 다 그냥 사라진다.
public class StatRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI valueText;

    [Tooltip("등급 옆에 진행도(%)를 적나. 캐릭터 생성 화면처럼 **전부 최하**인 자리에서는 0%만 줄줄이 떠서 읽기를 방해한다.")]
    [SerializeField] bool showPercent = true;

    StatName _stat;
    bool     _bound;
    Passion  _passion;

    public void Bind(UnitStats stats, StatName stat, bool held)
    {
        _stat    = stat;
        _bound   = true;
        _passion = stats.PassionOf(stat);

        if (nameText != null)
        {
            // **열정은 이름 옆에**(`+` 작은 · `++` 큰) — 불꽃 같은 기호는 폰트에 없어 네모가 된다.
            string mark   = Passions.Mark(_passion);
            nameText.text = mark.Length > 0 ? $"{stat.ToDisplay()} {mark}" : stat.ToDisplay();
            // **든 것은 색으로 표시한다** — 무엇을 들었는지(아이템 이름)는 왼쪽 상자가 적는다.
            nameText.color = held ? HeldColor : NormalColor;
        }
        if (valueText != null) valueText.text = Grade(stats, stat, showPercent);
    }

    static readonly Color NormalColor = new(0.85f, 0.85f, 0.88f);
    static readonly Color HeldColor   = new(0.67f, 0.67f, 1f);

    // 등급 + 진행도. `UnitStatsUI`가 쓰던 그 표기 그대로다.
    static string Grade(UnitStats stats, StatName name, bool showPercent)
    {
        float val   = stats.GetStat(name);
        StatGrade g = stats.GetGrade(name);
        string hex  = ColorUtility.ToHtmlStringRGB(g.GetColor());

        if (g == StatGrade.SSSPlus || !showPercent)
            return $"<color=#{hex}><b>{g.ToDisplay()}</b></color>";

        float baseline = (int)g * StatGradeExt.StepValue;
        int percent    = Mathf.Clamp(
            Mathf.FloorToInt((val - baseline) / StatGradeExt.StepValue * 100f), 0, 99);
        return $"<color=#{hex}><b>{g.ToDisplay()}</b></color> <size=70%><color=#888>{percent}%</color></size>";
    }

    // 설명은 올렸을 때. **줄이 오브젝트라 그냥 포인터 이벤트다** — 글자 안에서 링크 영역을
    // 찾아 헤맬 필요가 없다.
    public void OnPointerEnter(PointerEventData _)
    {
        if (!_bound) return;
        string body = _stat.Describe();
        if (_passion != Passion.None)
            body += $"\n\n<color=#E8A33D>{_passion.DisplayName()}</color>" +
                    $"  <color=#888>(성장 {Passions.Rate(_passion):0.#}배)</color>";
        HoverTooltipUI.Show(_stat.ToDisplay(), body);
    }

    public void OnPointerExit(PointerEventData _) => HoverTooltipUI.Hide();

    // 창이 닫힐 때는 `OnPointerExit`가 안 온다 — 설명만 화면에 남는다.
    void OnDisable() => HoverTooltipUI.Hide();
}

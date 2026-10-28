using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// **세력 우호도를 한눈에.** `L`키.
//
// 우호도가 움직이는데 볼 자리가 없으면 "왜 갑자기 덤비지"에 답이 없다. 살펴보기 창은 **고른
// 폰의 세력** 하나만 알려 주고(그 폰이 눈앞에 있어야 한다), 알림은 문턱을 넘는 순간에만 뜬다 —
// **지금 전체가 어떤지**를 묻는 자리가 따로 있어야 한다.
//
// ── 만난 세력만 뜬다 ───────────────────────────────────────────────────────
//
// 저작된 세력 전부(`DataManager.AllFactions`) 가운데 **만난 것만**(`FactionRelations.IsKnown`).
// 08-14에는 전부 띄웠다 — 판을 열면 창이 비어 있었기 때문이다. 2026-10-01에 뒤집었다(사용자:
// *"팩션UI가 발견하지 않은 팩션은 안 보이게"*) — 세상에 누가 있는지는 **찾아내는 것**이 됐다.
// 비어 있을 때는 비어 있다고 말한다(창이 고장 난 것처럼 보이지 않게).
//
// 세력 에셋은 `Resources/Data/Factions`에 산다 — **전부를 훑어야 하는 곳이 생기면 폴더 스캔**이라는
// 이 저장소의 규약대로다(목록을 배열로 들면 런타임 생성물에서 반드시 빈다).
//
// ── 모양은 글자 한 덩어리 ──────────────────────────────────────────────────
//
// 줄마다 오브젝트를 두지 않는다(`InspectPanelUI`와 같은 방식) — 세력은 열 남짓이고, 여기서
// 하는 일은 읽는 것뿐이라 누를 것이 없다. 줄 템플릿은 **누를 것이 있을 때** 값을 한다.
public class FactionPanelUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI infoText;

    // **박자는 세계에 하나다**(`UiRefresh`) — 세 박(0.45초)마다. 우호도는 사건으로만 움직이므로
    // 자주 볼 이유가 없다.
    int _beat = -1;

    // 모드 바의 `세력 (L)` 버튼이 부른다(2026-10-09) — 키와 같은 문.
    public static FactionPanelUI Instance { get; private set; }
    public bool IsOpen => panel != null && panel.activeSelf;
    public void Toggle()
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) Refresh();
    }

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);

        // **줄바꿈을 못 박는다** — 씬에서 꺼져 있으면 긴 세력 이름이 오른쪽에서 그냥 잘린다
        // (`InspectPanelUI`가 건물 설명에서 겪은 그것).
        if (infoText != null)
        {
            infoText.textWrappingMode = TextWrappingModes.Normal;
            infoText.overflowMode     = TextOverflowModes.Overflow;
        }
    }

    void OnEnable()  { FactionRelations.OnChanged += OnRelationChanged; FactionRelations.OnMet += OnMet; }
    void OnDisable() { FactionRelations.OnChanged -= OnRelationChanged; FactionRelations.OnMet -= OnMet; }

    void OnMet(FactionData f)
    {
        if (panel != null && panel.activeSelf) Refresh();
    }

    // 값이 움직이면 **열려 있을 때만** 다시 그린다 — 닫힌 창을 갱신할 이유가 없다.
    void OnRelationChanged(FactionData f, float favor, string reason)
    {
        if (panel != null && panel.activeSelf) Refresh();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame && panel != null && !InputFocus.KeysBlocked())
            Toggle();

        if (panel == null || !panel.activeSelf) return;

        if (!UiRefresh.Due(ref _beat, 3)) return;
        Refresh();
    }

    void Refresh()
    {
        if (infoText == null) return;

        var list = Collect(out int total);
        if (total == 0)
        {
            infoText.text = "<color=#888>세력 데이터가 없습니다 — `Resources/Data/Factions`를 확인하세요.</color>";
            return;
        }
        if (list.Count == 0)
        {
            infoText.text = "<color=#888>아직 만난 세력이 없습니다.\n탐험하며 다른 이들을 만나면 여기에 적힙니다.</color>";
            return;
        }

        // **나쁜 쪽이 위로.** 급한 것이 위에 있어야 창을 열자마자 읽힌다.
        list.Sort((a, b) => FactionRelations.Of(a).CompareTo(FactionRelations.Of(b)));

        var sb = new StringBuilder();
        foreach (var f in list)
        {
            float favor = FactionRelations.Of(f);

            sb.AppendLine($"<b><color=#E8C566>{f.FactionName}</color></b>  " +
                          $"<color={StanceColor(f)}>{StanceLabel(f)}</color>");
            sb.AppendLine($"  {Bar(favor)}  <color=#9AB>{favor:+0;-0;0}</color>");


            sb.AppendLine();
        }

        int unknown = total - list.Count;
        if (unknown > 0) sb.Append($"<color=#777>아직 못 만난 세력 {unknown}</color>");

        infoText.text = sb.ToString();
    }

    // 만난 세력. **플레이어 세력은 뺀다**(자기 자신과의 관계는 물을 것이 없다). `total`은 남의 세력 전부.
    static List<FactionData> Collect(out int total)
    {
        var list = new List<FactionData>();
        total = 0;
        foreach (var f in DataManager.AllFactions)
        {
            if (f == null || f.PlayerControlled) continue;
            total++;
            if (FactionRelations.IsKnown(f)) list.Add(f);
        }
        return list;
    }

    // 지금 이 세력이 나를 어떻게 대하는가. **`Faction.StanceTo`를 거친다** — 우호도가 저작한
    // 값을 덮는 판정이 거기 있으므로, 여기서 따로 재면 "표에는 적대인데 안 때린다"가 생긴다.
    static FactionStance Stance(FactionData f)
    {
        var player = Faction.Player;
        if (player == null || player.Data == null) return FactionStance.Neutral;

        // 방향별이라 **저쪽이 나를 어떻게 보는가**를 묻는다 — 덤비는 쪽이 그쪽이다.
        if (FactionRelations.TryOverride(f, out var over)) return over;
        return f.StanceTo(player.Data);
    }

    static string StanceLabel(FactionData f) => Stance(f) switch
    {
        FactionStance.Hostile => "적대",
        FactionStance.Ally    => "아군",
        _                     => "중립",
    };

    static string StanceColor(FactionData f) => Stance(f) switch
    {
        FactionStance.Hostile => "#F44336",
        FactionStance.Ally    => "#8BC34A",
        _                     => "#BBB",
    };

    // −100 ~ +100을 스무 칸으로. **가운데(0)가 저작한 그대로**라는 것이 눈에 보여야 해서
    // 한쪽으로 차오르는 막대가 아니라 **가운데에서 갈라지는** 모양으로 둔다.
    static string Bar(float favor)
    {
        const int Half = 10;
        int fill = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(favor) / 100f * Half), 0, Half);

        var left  = new StringBuilder();
        var right = new StringBuilder();

        for (int i = 0; i < Half; i++)
        {
            bool on = favor < 0 && i >= Half - fill;
            left.Append(on ? "█" : "░");
        }
        for (int i = 0; i < Half; i++)
        {
            bool on = favor > 0 && i < fill;
            right.Append(on ? "█" : "░");
        }

        return $"<color=#C0504D>{left}</color><color=#666>|</color><color=#6A9955>{right}</color>";
    }
}

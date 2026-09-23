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
// ── 세력은 전부 뜬다 ───────────────────────────────────────────────────────
//
// 저작된 세력 전부를 나열한다(`DataManager.AllFactions`). 처음엔 "만난 세력만" 띄웠는데,
// 그러면 **판을 시작하고 창을 열면 거의 비어 있다** — 세상에 누가 있는지를 알려 주는 것이
// 이 창의 절반이고, 안 만난 세력의 시작 우호도도 **저작된 값이라 볼 값이 있다**
// (`FactionData.StartingFavor` — 망자 −100, 산적 −55는 만나기 전에도 참이다).
//
// 그래서 세력 에셋을 `Resources/Data/Factions`로 옮겼다 — **전부를 훑어야 하는 곳이 생기면
// 폴더 스캔**이라는 이 저장소의 규약대로다(목록을 배열로 들면 런타임 생성물에서 반드시 빈다).
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

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        // **줄바꿈을 못 박는다** — 씬에서 꺼져 있으면 긴 세력 이름이 오른쪽에서 그냥 잘린다
        // (`InspectPanelUI`가 건물 설명에서 겪은 그것).
        if (infoText != null)
        {
            infoText.textWrappingMode = TextWrappingModes.Normal;
            infoText.overflowMode     = TextOverflowModes.Overflow;
        }
    }

    void OnEnable()  => FactionRelations.OnChanged += OnRelationChanged;
    void OnDisable() => FactionRelations.OnChanged -= OnRelationChanged;

    // 값이 움직이면 **열려 있을 때만** 다시 그린다 — 닫힌 창을 갱신할 이유가 없다.
    void OnRelationChanged(FactionData f, float favor, string reason)
    {
        if (panel != null && panel.activeSelf) Refresh();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame && panel != null && !InputFocus.KeysBlocked())
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Refresh();
        }

        if (panel == null || !panel.activeSelf) return;

        if (!UiRefresh.Due(ref _beat, 3)) return;
        Refresh();
    }

    void Refresh()
    {
        if (infoText == null) return;

        var list = Collect();
        if (list.Count == 0)
        {
            infoText.text = "<color=#888>세력 데이터가 없습니다 — `Resources/Data/Factions`를 확인하세요.</color>";
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

        infoText.text = sb.ToString();
    }

    // 저작된 세력 전부. **플레이어 세력만 뺀다**(자기 자신과의 관계는 물을 것이 없다).
    static List<FactionData> Collect()
    {
        var list = new List<FactionData>();
        foreach (var f in DataManager.AllFactions)
            if (f != null && !f.PlayerControlled) list.Add(f);
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

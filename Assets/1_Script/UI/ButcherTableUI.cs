using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// **도축대 창 — 규칙을 거는 자리.**
//
// 여기가 이 건물의 조작 화면이다. 살펴보기 창은 지금 어떻게 걸려 있는지만 적는다.
//
// **규칙은 셋이고 저마다 누르면 다음 값으로 돌아간다.** 목록을 펼치는 대신 순환으로 둔 것은
// 값이 세 가지씩뿐이라서다 — 펼치면 값 하나 바꾸는 데 두 번 눌러야 한다. 무엇이 다음에 오는지는
// 누르면 곧바로 보이고, 한 바퀴가 세 번이라 되돌리기도 쉽다.
//
// **산출물 격자를 같이 둔다** — 작업대 창과 같은 짝이다. 나온 것을 보고 집어 갈 자리가
// 창 안에 있어야 "쌓였는데 어디 갔지"가 안 생긴다.
public class ButcherTableUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI statusText;

    [Header("진행")]
    // **다른 작업대와 같은 눈금**(`CraftingTableUI.progressFill`) — 얼마나 남았는지가
    // 글자가 아니라 길이로 보여야 "곧 끝나겠다"가 읽힌다.
    [SerializeField] GameObject      progressRow;
    [SerializeField] Image           progressFill;
    [SerializeField] TextMeshProUGUI progressText;

    [Header("규칙")]
    [SerializeField] Button          suspendButton;
    [SerializeField] TextMeshProUGUI suspendLabel;
    [SerializeField] Button          targetButton;
    [SerializeField] TextMeshProUGUI targetLabel;
    [SerializeField] Button          stageButton;
    [SerializeField] TextMeshProUGUI stageLabel;
    [SerializeField] Button          repeatButton;
    [SerializeField] TextMeshProUGUI repeatLabel;

    [Header("산출물")]
    [SerializeField] RectTransform   outputGridContainer;
    [SerializeField] Button          haulToggleButton;
    [SerializeField] TextMeshProUGUI haulToggleLabel;

    static readonly Color Cut       = new(0.78f, 0.36f, 0.30f);   // 손질 중
    static readonly Color Slay      = new(0.62f, 0.20f, 0.20f);   // 죽이는 중 — 더 짙게
    static readonly Color Running   = new(0.30f, 0.85f, 0.36f);
    static readonly Color Waiting   = new(0.62f, 0.62f, 0.66f);
    static readonly Color Suspended = new(1.00f, 0.60f, 0.00f);

    ButcherTable    _table;
    InventoryGridUI _grid;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        // **버튼은 도축대가 든 규칙을 그대로 돌린다** — 우클릭 쪽과 같은 함수를 부르므로
        // 문이 둘이어도 갈릴 데가 없다(침대 창을 없앤 이유가 *규칙이 두 벌*이어서였지
        // 문이 둘이어서가 아니다).
        Hook(suspendButton, () => _table?.ToggleSuspended());
        Hook(targetButton,  () => _table?.CycleTarget());
        Hook(stageButton,   () => _table?.CycleStage());
        Hook(repeatButton,  () => _table?.CycleRepeat());
        Hook(haulToggleButton, () => _table?.SetAllowHaulOut(!_table.AllowHaulOut));

        // **[정리]는 없다.** 격자를 손으로 추스를 값이 있으려면 *자리가 한도*여야 하는데
        // (상점 좌판이 그렇다 — 칸이 차면 더 못 판다) 여기 산출물은 쌓이는 족족 창고로
        // 실려 간다. 조각나 봐야 다음 짐이 나가면서 저절로 풀린다.

        ButcherTable.OnSelected          += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void Hook(Button b, System.Action act)
    {
        if (b == null) return;
        b.onClick.AddListener(() => { act(); Refresh(); });
    }

    void OnDestroy()
    {
        ButcherTable.OnSelected          -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Open(ButcherTable table)
    {
        _table = table;
        WindowRaiser.Open(panel);
        Refresh();
    }

    public void Close()
    {
        _table = null;
        // 닫힌 창이 남의 인벤토리를 붙들지 않게(작업대 창과 같은 규약).
        _grid?.Bind(null);
        if (panel != null) panel.SetActive(false);
    }

    [SerializeField] float refreshInterval = 0.2f;
    float _timer;

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (_table == null) { Close(); return; }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = refreshInterval;
        Refresh();
    }

    void Refresh()
    {
        if (_table == null) return;

        var bill = _table.Bill;

        if (titleText != null) titleText.text = "도축대";

        bool slaying = _table.Slaughtering;

        if (statusText != null)
        {
            if (bill.Suspended)      { statusText.text = "멈춰 있다";       statusText.color = Suspended; }
            else if (slaying)        { statusText.text = "죽이는 중";       statusText.color = Slay; }
            else if (_table.Cutting) { statusText.text = "손질하는 중";     statusText.color = Running; }
            else                     { statusText.text = "시체를 기다린다"; statusText.color = Waiting; }
        }

        // **일이 없으면 게이지 줄을 통째로 숨긴다** — 0%짜리 빈 막대는 "멈춰 있다"로 읽힌다
        // (연료를 안 쓰는 작업대에서 연료 줄을 숨기는 것과 같은 규칙).
        bool working = _table.Cutting;
        if (progressRow != null) progressRow.SetActive(working);
        if (working)
        {
            float p = _table.Progress;
            if (progressFill != null)
            {
                progressFill.fillAmount = p;
                progressFill.color      = slaying ? Slay : Cut;
            }
            if (progressText != null)
                progressText.text = $"{(slaying ? "죽이는 중" : "손질")}  {p * 100f:F0}%";
        }

        // **글자가 곧 지금 값이다** — "무엇으로 바뀌나"가 아니라 "지금 무엇인가"를 적는다.
        // 바뀔 값을 적으면 화면을 보고 현재 설정을 알 수가 없다.
        Set(suspendLabel, bill.Suspended ? "도축을 시작한다" : "도축을 멈춘다");
        Set(targetLabel,  $"대상  {bill.TargetName}");
        Set(stageLabel,   $"상태  {bill.StageName}");
        Set(repeatLabel,  $"반복  {bill.RepeatName}");

        if (outputGridContainer != null && _table.Inventory != null)
        {
            if (_grid == null)
                _grid = outputGridContainer.GetComponent<InventoryGridUI>()
                     ?? outputGridContainer.gameObject.AddComponent<InventoryGridUI>();
            _grid.Bind(_table.Inventory);
        }

        Set(haulToggleLabel, _table.AllowHaulOut
            ? "<color=#88FF88>운반 켜짐</color>  창고로 옮김"
            : "<color=#FF9966>운반 꺼짐</color>  여기 쌓아 둠");
    }

    static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
}

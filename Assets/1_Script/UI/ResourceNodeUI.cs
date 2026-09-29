using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 자원 노드 선택 시 정보 패널 표시.
// 노드는 한 종류 자원만 쌓이므로 그리드 인벤토리 대신 단일 아이콘 + 수량으로 표시.
public class ResourceNodeUI : MonoBehaviour
{
    [Header("씬 참조")]
    [Tooltip("노드 선택 시 활성화될 패널 루트")]
    [SerializeField] GameObject panel;

    [Tooltip("(선택) 노드 이름 표시 텍스트")]
    [SerializeField] TextMeshProUGUI nameText;

    [Tooltip("(선택) 게이지 진행도 텍스트")]
    [SerializeField] TextMeshProUGUI gaugeText;

    [Tooltip("채굴 진행 막대. Image Type=Filled, Fill Method=Horizontal 로 설정.")]
    [SerializeField] Image gaugeFill;

    [Header("재고 표시")]
    [Tooltip("드롭 아이템 아이콘이 표시될 Image")]
    [SerializeField] Image stockIcon;

    [Tooltip("재고 수량 텍스트 (예: x12)")]
    [SerializeField] TextMeshProUGUI stockCount;

    [Tooltip("아이콘 크기 산정용 셀 크기. ItemData의 Width/Height에 곱해짐.")]
    [SerializeField] float cellSize = 64f;

    [Tooltip("재고가 0일 때 아이콘을 흐리게 표시")]
    [SerializeField] Color emptyIconTint = new Color(1f, 1f, 1f, 0.25f);

    [Header("배정")]
    [Tooltip("선택된 폰들을 이 노드에 배정하는 버튼")]
    [SerializeField] Button assignButton;

    [Tooltip("배정된 폰 목록 부모 (VerticalLayoutGroup이 있는 Transform)")]
    [SerializeField] Transform assignedListRoot;

    [Tooltip("폰 엔트리 프리팹 (PawnEntryUI 컴포넌트 포함)")]
    [SerializeField] PawnEntryUI pawnEntryPrefab;

    [Header("빈 자리")]
    [Tooltip("아직 아무도 안 붙었지만 붙일 수 있는 자리")]
    [SerializeField] Color slotFrameColor = new Color(1f, 1f, 1f, 0.16f);

    [Tooltip("자리 수를 넘어선 칸 — 애초에 붙일 수 없는 자리")]
    [SerializeField] Color slotLockedColor = new Color(1f, 1f, 1f, 0.05f);

    [Tooltip("잠긴 칸의 바탕. 창보다 어두워야 *못 쓰는 자리*로 읽힌다.")]
    [SerializeField] Color slotLockedFill = new Color(0f, 0f, 0f, 0.28f);

    const float SlotEdge = 1.5f;

    // 줄에 늘 그리는 칸 수. **규격이다**(2026-09-06, 사용자 결정) — 자리가 하나뿐인
    // 광맥에서도 넷을 그리고, 남는 셋은 **잠긴 칸**으로 어둡게 둔다.
    //
    // 그냥 자리 수만큼만 그렸더니 **창의 모양이 노드마다 달라졌다**(돌은 한 칸, 철은 세 칸).
    // 넷으로 못 박으면 *이 광맥은 자리가 몇인가*가 **밝은 칸을 세는 일**이 된다 —
    // 숫자를 읽는 것보다 빠르고, 창의 높이도 안 흔들린다.
    //
    // 넷인 이유는 **줄 하나에 들어가는 수**다(100 x 4 + 9 x 3 = 427, 안쪽 폭 428).
    const int SlotColumns = 4;

    [Header("이송")]
    [Tooltip("노드 → 선택된 폰으로 재고를 이송하는 버튼")]
    [SerializeField] Button transferButton;

    // ── 2026-09-06에 더한 것 ────────────────────────────────────────────────
    //
    // 그전에는 **이름 · 진행 게이지 · 재고 · 배정 목록**뿐이었다. 그래서 광맥이 멈춰 있을 때
    // *왜*를 이 창에서 알 수가 없었다 — 전력이 모자란 건지, 아직 안 익은 건지, 붙은 폰이
    // 없는 건지. **멈춘 이유를 못 적는 창은 상태를 보여 준 것이 아니다.**
    [Header("상태 (전력 · 숙성 · 붙은 폰)")]
    [Tooltip("여러 줄. 전력·숙성·산출 추정치를 여기 적는다.")]
    [SerializeField] TextMeshProUGUI statusText;

    [Tooltip("이 품목을 자동으로 나를지. `HaulPolicy`에 쓴다 — **이 광맥 하나가 아니라 그 품목**이다.")]
    [SerializeField] Button haulPolicyButton;

    [Tooltip("전력을 끊었다 잇는다. 끄면 그 힘이 **뒤에 지은 건물에게 간다**.")]
    [SerializeField] Button powerButton;

    [Header("운반 임계값")]
    [Tooltip("운반 임계값 표시 텍스트 (예: 운반: 10개)")]
    [SerializeField] TextMeshProUGUI haulThresholdText;
    [SerializeField] Button haulThresholdUpButton;
    [SerializeField] Button haulThresholdDownButton;

    ResourceNode _current;
    SelectionManager _selection;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (assignButton != null)        assignButton.onClick.AddListener(AssignSelected);
        if (transferButton != null)      transferButton.onClick.AddListener(TransferToSelected);
        if (haulThresholdUpButton != null)   haulThresholdUpButton.onClick.AddListener(IncreaseThreshold);
        if (haulThresholdDownButton != null) haulThresholdDownButton.onClick.AddListener(DecreaseThreshold);
    }

    void Start()
    {
        _selection = FindFirstObjectByType<SelectionManager>();
    }

    void OnEnable()
    {
        SelectionManager.OnNodeSelected  += OnNodeSelected;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        ResourceNode.OnAssignmentChanged += OnAssignmentChanged;
        ExclusiveMode.Claimed            += OnModeClaimed;
    }

    // 건설·명령·밭 구역이 켜지면 닫는다(`FocusMode`) — 드래그할 자리를 가린다.
    void OnModeClaimed(object _) { if (panel != null && panel.activeSelf) Close(); }

    void OnDisable()
    {
        SelectionManager.OnNodeSelected  -= OnNodeSelected;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        ResourceNode.OnAssignmentChanged -= OnAssignmentChanged;
        ExclusiveMode.Claimed            -= OnModeClaimed;
        Unsubscribe();
    }

    // 패널 밖에서 배정이 풀린 경우(이동 명령·사망 등)에도 목록을 맞춘다.
    void OnAssignmentChanged(ResourceNode node)
    {
        if (node == _current) RefreshAssigned();
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        // 다른 패널들과 같은 규칙 — ESC로 닫는다.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (_current == null) return;

        float p = Mathf.Clamp01(_current.GaugeProgress);
        if (gaugeText != null) gaugeText.text = $"{p * 100f:F0}%";
        // 숫자만으론 "차오르고 있다"가 잘 안 읽힌다 — 막대가 움직이는 게 훨씬 빠르다.
        if (gaugeFill != null) gaugeFill.fillAmount = p;

        // **매 프레임 다시 적는다** — 전력·숙성·붙은 폰이 전부 밖에서 바뀌는 값이라,
        // 열 때 한 번만 적으면 **창을 닫았다 열어야 진짜 상태가 보인다.**
        RefreshStatus();
    }

    // ── 상태 ────────────────────────────────────────────────────────────────
    //
    // **순서가 곧 원인의 순서다**: 전력 → 사람. 위에서부터 막힌 첫 줄이 *왜 멈췄나*의
    // 답이라, 읽는 사람이 아래까지 안 읽어도 된다.
    void RefreshStatus()
    {
        if (statusText == null || _current == null) return;

        var d  = _current.Data;
        var sb = new System.Text.StringBuilder();

        // 전력
        if (PowerUser.Has(_current, out var pu))
        {
            var core = Territory.CoreAt(_current.transform.position);
            int cap  = PowerGrid.CapacityOf(core);
            int used = PowerGrid.UsedOf(core);

            sb.Append("전력 ").Append(pu.Cost).Append("  ");
            sb.AppendLine(!pu.SwitchedOn
                ? "<color=#888>꺼 둠</color>"
                : pu.Powered
                    ? $"<color=#7fe08a>가동 중</color>  <color=#888>({used}/{cap} · {cap - used} 남음)</color>"
                    : $"<color=#F44336>멈춤 — 전력 부족</color>  <color=#888>({used}/{cap})</color>");
        }

        // ③ 붙은 폰과 그 스탯. **왜 느린지의 답이 여기 있다** — 같은 광맥이라도 채굴 20짜리와
        //    60짜리는 세 배 다르다. 이름만 적으면 그 차이가 안 보인다.
        bool tree = d != null && d.IsTree;
        var  stat = tree ? StatName.Cutting : StatName.Mining;

        float power = 0f;
        int   n     = 0;
        foreach (var b in _current.AssignedPawns)
        {
            if (b == null || b.Stats == null) continue;
            power += b.Stats.GetStat(stat);
            n++;
        }

        if (n > 0)
        {
            sb.AppendLine($"<color=#888>{(tree ? "벌목" : "채굴")} 합계 {power:F0}  ·  {n}명</color>");

            // **초당 산출 추정** — 광맥을 늘릴지 사람을 늘릴지가 여기서 갈린다.
            // 숙성이 있으면 그쪽이 천장이므로 **둘 중 느린 쪽**을 적는다.
            if (d != null && d.GaugeRequired > 0.01f)
            {
                float perYieldSec = d.GaugeRequired / Mathf.Max(0.01f, power);
                float avg = (d.DropMin + d.DropMax) * 0.5f;
                sb.AppendLine($"<color=#666>대략 {perYieldSec:F0}초마다 {avg:F0}개</color>");
            }
        }
        else if (_current.CanDesignate)
            sb.AppendLine("<color=#F90>야생입니다 — 배정이 아니라 찍어서 캡니다(C 명령의 채굴).</color>");
        else
            sb.AppendLine("<color=#F90>붙은 폰이 없습니다.</color>");

        statusText.text = sb.ToString();

        RefreshHaulPolicyButton();
        RefreshPowerButton();
    }

    void RefreshHaulPolicyButton()
    {
        if (haulPolicyButton == null) return;

        var item = _current != null && _current.Data != null ? _current.Data.DropItem : null;
        haulPolicyButton.gameObject.SetActive(item != null);
        if (item == null) return;

        var t = haulPolicyButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
            t.text = HaulPolicy.IsAuto(item) ? "자동운반 허용" : "자동운반 불가";

        var img = haulPolicyButton.GetComponent<Image>();
        if (img != null)
            img.color = HaulPolicy.IsAuto(item) ? new Color(0.18f, 0.42f, 0.22f)
                                                : new Color(0.20f, 0.20f, 0.22f);

        haulPolicyButton.onClick.RemoveAllListeners();
        haulPolicyButton.onClick.AddListener(() => HaulPolicy.Toggle(item));
    }

    void RefreshPowerButton()
    {
        if (powerButton == null) return;

        bool has = PowerUser.Has(_current, out var pu);
        powerButton.gameObject.SetActive(has);
        if (!has) return;

        var t = powerButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null) t.text = pu.SwitchedOn ? "전력 끈다" : "전력 켠다";

        powerButton.onClick.RemoveAllListeners();
        powerButton.onClick.AddListener(pu.ToggleSwitch);
    }

    public void Close()
    {
        Unsubscribe();
        _current = null;
        WindowRaiser.Close(panel);
    }

    // **야생 노드에는 이 창이 안 뜬다**(2026-09-06). 여기는 *배정하는 창*인데 야생은
    // 배정을 안 받는다 — 띄우면 **눌러도 아무 일이 없는 버튼**을 보여 주는 셈이고, 실제로
    // *"자리가 모자란다"*는 엉뚱한 메시지가 찍혔다(자리는 비어 있었다).
    //
    // 야생은 `InspectPanel`이 맡는다 — 거기 **[캔다]/[벤다]** 버튼이 있다.
    void OnNodeSelected(ResourceNode node)
    {
        if (node == null) return;

        // **야생에도 뜬다**(2026-09-17, 사용자: *"내가 설치하지 않은 자원들도 걍 자원 노드
        // 패널이 보이면 좋겠어"*). 막아 뒀던 이유는 *"여기는 배정하는 창인데 야생은 배정을
        // 안 받는다"*였는데, **야생도 배정을 받게 되면서 그 전제가 없어졌다**
        // (`ResourceNode.Assign` 참고 — 야생은 유한하므로 사람을 붙여도 무한 채굴이 아니다).

        Unsubscribe();
        _current = node;

        if (panel == null) return;
        WindowRaiser.Open(panel);

        if (nameText != null && node.Data != null)
            nameText.text = node.Data.DropItem != null ? node.Data.DropItem.ItemName : "자원";

        if (node.Inventory != null)
            node.Inventory.OnChanged += RefreshStock;
        RefreshStock();
        RefreshAssigned();
        RefreshThreshold();
    }

    void IncreaseThreshold()
    {
        if (_current == null) return;
        _current.HaulThreshold += 5;
        RefreshThreshold();
    }

    void DecreaseThreshold()
    {
        if (_current == null) return;
        _current.HaulThreshold = Mathf.Max(1, _current.HaulThreshold - 5);
        RefreshThreshold();
    }

    void RefreshThreshold()
    {
        if (haulThresholdText != null && _current != null)
            haulThresholdText.text = $"운반:{_current.HaulThreshold}";
    }

    // 자리만큼만 받는다. 넘친 인원은 **조용히 버리지 않고** 로그로 말한다 —
    // 다섯을 골라 눌렀는데 둘만 붙으면 플레이어는 왜 그런지 알아야 한다.
    void AssignSelected()
    {
        if (_current == null || _selection == null) return;

        int added = 0, rejected = 0;
        foreach (var pawn in _selection.Selected)
        {
            var brain = pawn.GetComponent<UnitBrain>();
            if (brain == null) continue;

            if (_current.Assign(brain)) added++;
            else                        rejected++;
        }

        // **왜 거절됐는지를 갈라 적는다** — 야생이라 못 붙는 것과 자리가 없어서 못 붙는
        // 것은 **고칠 방법이 다르다**(찍어서 캐라 ↔ 기다려라).
        if (rejected > 0)
            Debug.Log(_current.CanDesignate
                ? $"[자원 노드] {_current.name}: 야생 자원은 배정하지 않습니다 — 찍어서 캡니다(C 명령의 채굴)."
                : $"[자원 노드] {_current.name}: 자리가 {_current.MaxWorkers}명뿐이라 " +
                  $"{rejected}명은 배정하지 못했습니다.");

        if (added > 0 || rejected > 0) RefreshAssigned();
    }

    void TransferToSelected()
    {
        if (_current == null || _current.Data == null || _current.Inventory == null) return;
        if (_selection == null || _selection.Selected.Count == 0) return;

        var pawn = _selection.Selected[0];
        var pawnInv = pawn.GetComponent<Inventory>();
        if (pawnInv == null) return;

        var dropItem = _current.Data.DropItem;
        if (dropItem == null) return;

        int available = _current.Inventory.GetTotalQuantity(dropItem);
        if (available <= 0) return;

        // 폰이 받을 수 있는 만큼만 이송
        int taken = pawnInv.TryAdd(dropItem, available);
        if (taken > 0)
            _current.Inventory.Remove(dropItem, taken);
    }

    void RefreshAssigned()
    {
        if (_current == null) return;

        RefreshAssignButton();

        if (assignedListRoot == null || pawnEntryPrefab == null) return;

        // 기존 엔트리 제거
        for (int i = assignedListRoot.childCount - 1; i >= 0; i--)
            Destroy(assignedListRoot.GetChild(i).gameObject);

        // 배정된 폰들 엔트리 생성
        int filled = 0;
        foreach (var brain in _current.AssignedPawns)
        {
            if (brain == null) continue;
            var entry = Instantiate(pawnEntryPrefab, assignedListRoot);
            entry.Setup(brain, _current, RefreshAssigned);
            filled++;
        }

        // **빈 자리도 그린다**(2026-09-06) — 안 그리면 *몇 자리짜리 광맥인가*가 화면에 없다.
        // 한 명 붙은 1자리와 한 명 붙은 4자리가 **똑같이 보이고**, 그 차이는 버튼의 `1/4`를
        // 읽어야만 알 수 있었다. 인벤토리 칸과 같은 규칙이다 — **자리는 비어 있어도 자리다.**
        //
        // 자리 수를 넘어선 칸도 그리되 **잠긴 것으로 그린다** — 없는 자리를 빈 자리처럼
        // 그리면 *세 명 더 붙일 수 있다*는 거짓말이 된다.
        int columns = Mathf.Max(SlotColumns, _current.MaxWorkers);
        for (int i = filled; i < columns; i++)
            MakeEmptySlot(assignedListRoot, i < _current.MaxWorkers);
    }

    // 빈 자리 — **채우지 않고 테두리만 그린다.** 면을 칠하면 *여기 무언가 있다*로 읽혀
    // 붙은 폰과 구별이 안 간다(그래서 큰 회색 판을 걷어냈다).
    void MakeEmptySlot(Transform parent, bool open)
    {
        var go = new GameObject(open ? "EmptySlot" : "LockedSlot", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        // **크기는 폰 줄에서 물려받는다** — 상수로 박으면 프리팹을 키우는 날 줄이 어긋난다.
        rt.sizeDelta = pawnEntryPrefab != null
            ? ((RectTransform)pawnEntryPrefab.transform).sizeDelta
            : new Vector2(100f, 100f);

        // 잠긴 칸은 **바탕까지 어둡게** 판다 — 테두리만 흐리게 하면 *멀리 있는 빈 자리*로
        // 보인다. 파여 있어야 "여기는 애초에 자리가 아니다"가 읽힌다.
        if (!open)
        {
            var fill = go.AddComponent<Image>();
            fill.color = slotLockedFill;
            fill.raycastTarget = false;
        }

        var c = open ? slotFrameColor : slotLockedColor;
        Edge(rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, SlotEdge), c);
        Edge(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, SlotEdge), c);
        Edge(rt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(SlotEdge, 0f), c);
        Edge(rt, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(SlotEdge, 0f), c);
    }

    void Edge(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Color color)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = (aMin + aMax) * 0.5f;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // 남은 자리를 **버튼에 적는다.** 따로 칸을 만들면 씬을 고쳐야 하고, 자리 수는
    // "배정 버튼을 누를까"를 정할 때 보는 값이라 버튼에 붙어 있는 게 맞다.
    void RefreshAssignButton()
    {
        if (assignButton == null || _current == null) return;

        bool limited = _current.MaxWorkers > 0;
        assignButton.interactable = !_current.IsFull;

        var label = assignButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = limited
                ? $"배정 {_current.AssignedCount}/{_current.MaxWorkers}"
                : "배정";
    }

    void Unsubscribe()
    {
        if (_current != null && _current.Inventory != null)
            _current.Inventory.OnChanged -= RefreshStock;
    }

    void RefreshStock()
    {
        if (_current == null || _current.Data == null) return;
        var dropItem = _current.Data.DropItem;

        int qty = (dropItem != null && _current.Inventory != null)
            ? _current.Inventory.GetTotalQuantity(dropItem)
            : 0;

        if (stockIcon != null)
        {
            if (dropItem != null && dropItem.Icon != null)
            {
                stockIcon.sprite = dropItem.Icon;
                stockIcon.preserveAspect = true;
                stockIcon.color = qty > 0 ? Color.white : emptyIconTint;
            }
            else
            {
                stockIcon.sprite = null;
                stockIcon.color = emptyIconTint;
            }

            // 아이템 W/H에 비례한 크기로 표시
            if (dropItem != null)
                stockIcon.rectTransform.sizeDelta =
                    new Vector2(dropItem.Width * cellSize, dropItem.Height * cellSize);
        }

        if (stockCount != null)
            stockCount.text = qty > 0 ? $"×{qty}" : "";
    }
}

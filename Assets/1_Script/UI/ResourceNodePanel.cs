using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 자원 노드 선택 시 정보 패널 표시.
// 노드는 한 종류 자원만 쌓이므로 그리드 인벤토리 대신 단일 아이콘 + 수량으로 표시.
public class ResourceNodePanel : MonoBehaviour
{
    [Header("씬 참조")]
    [Tooltip("노드 선택 시 활성화될 패널 루트")]
    [SerializeField] GameObject panel;

    [Tooltip("(선택) 노드 이름 표시 텍스트")]
    [SerializeField] TextMeshProUGUI nameText;

    [Tooltip("(선택) 게이지 진행도 텍스트")]
    [SerializeField] TextMeshProUGUI gaugeText;

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

    [Header("이송")]
    [Tooltip("노드 → 선택된 폰으로 재고를 이송하는 버튼")]
    [SerializeField] Button transferButton;

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
    }

    void OnDisable()
    {
        SelectionManager.OnNodeSelected  -= OnNodeSelected;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        Unsubscribe();
    }

    void OnBuildModeChanged(bool active) { if (active && panel != null) panel.SetActive(false); }

    void Update()
    {
        if (_current != null && gaugeText != null)
            gaugeText.text = $"{_current.GaugeProgress * 100f:F0}%";
    }

    void OnNodeSelected(ResourceNode node)
    {
        if (node == null) return;

        Unsubscribe();
        _current = node;

        if (panel == null) return;
        panel.SetActive(true);

        if (nameText != null && node.Data != null)
            nameText.text = node.Data.ResourceType.DisplayName();

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

    void AssignSelected()
    {
        if (_current == null || _selection == null) return;
        int count = 0;
        foreach (var pawn in _selection.Selected)
        {
            var brain = pawn.GetComponent<UnitBrain>();
            if (brain == null) continue;
            _current.Assign(brain);
            count++;
        }
        if (count > 0) RefreshAssigned();
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
        if (assignedListRoot == null || pawnEntryPrefab == null) return;

        // 기존 엔트리 제거
        for (int i = assignedListRoot.childCount - 1; i >= 0; i--)
            Destroy(assignedListRoot.GetChild(i).gameObject);

        // 배정된 폰들 엔트리 생성
        foreach (var brain in _current.AssignedPawns)
        {
            if (brain == null) continue;
            var entry = Instantiate(pawnEntryPrefab, assignedListRoot);
            entry.Setup(brain, _current, RefreshAssigned);
        }
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

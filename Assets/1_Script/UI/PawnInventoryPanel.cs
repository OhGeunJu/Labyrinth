using UnityEngine;
using UnityEngine.InputSystem;

// I 키로 토글되는 인벤토리 패널 컨트롤러.
// 기존 씬의 패널 GameObject와 그리드 컨테이너를 인스펙터에서 연결해서 사용.
public class PawnInventoryPanel : MonoBehaviour
{
    [Header("씬 참조")]
    [Tooltip("토글할 패널 루트 (활성/비활성 대상)")]
    [SerializeField] GameObject panel;

    [Tooltip("그리드가 그려질 컨테이너. InventoryGridUI 컴포넌트가 없으면 자동 추가됨.")]
    [SerializeField] RectTransform gridContainer;

    [Header("입력")]
    [SerializeField] Key toggleKey = Key.I;

    InventoryGridUI  _gridUI;
    SelectionManager _selection;

    void Awake()
    {
        if (gridContainer != null)
        {
            _gridUI = gridContainer.GetComponent<InventoryGridUI>();
            if (_gridUI == null) _gridUI = gridContainer.gameObject.AddComponent<InventoryGridUI>();
        }
        if (panel != null) panel.SetActive(false);
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy() => BuildModeManager.OnActiveChanged -= OnBuildModeChanged;

    void OnBuildModeChanged(bool active) { if (active && panel != null) panel.SetActive(false); }

    void Start()
    {
        _selection = FindFirstObjectByType<SelectionManager>();
    }

    void Update()
    {
        if (panel == null || Keyboard.current == null) return;
        if (Keyboard.current[toggleKey].wasPressedThisFrame) Toggle();
    }

    void Toggle()
    {
        bool open = !panel.activeSelf;
        panel.SetActive(open);
        if (open) BindToSelected();
    }

    void BindToSelected()
    {
        if (_gridUI == null) return;
        if (_selection == null || _selection.Selected.Count == 0)
        {
            _gridUI.Bind(null);
            return;
        }
        var pawn = _selection.Selected[0];
        var inv  = pawn.GetComponent<Inventory>();
        _gridUI.Bind(inv);
    }
}

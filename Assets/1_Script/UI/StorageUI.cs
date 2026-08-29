using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 창고 패널 — 내용물 그리드 + 용량 + 입고 필터.
//
// 필터는 **묶음(석재/원석/보석/주괴/목재/화폐)** 단위다. 자원을 낱개로 늘어놓으면 버튼이
// 스무 개를 넘어 아무도 안 쓴다. 묶음은 `ResourceItemData.Group`이 직접 들고 있다 —
// 2026-08-02 전에는 `ResourceType` enum을 훑어 묶음을 되짚었는데, 그러면 **모드가 넣은 자원이
// 어느 묶음에도 안 들어갔다**(enum에 없으니까). 이제 아이템이 자기 묶음을 말하므로 그냥 딸려 온다.
//
// 필터 버튼은 **씬의 `filterTemplate`을 복제**해 채운다 — 모양은 씬이, 코드는 데이터만
// (`docs/editor_creators.md`). 코드가 색·여백을 만들면 편집 화면과 게임 화면이 어긋난다.
public class StorageUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] RectTransform   gridContainer;

    [Header("용량 / 필터")]
    [Tooltip("'12 / 40 칸' 형태로 표시")]
    [SerializeField] TextMeshProUGUI capacityText;
    [Tooltip("필터 버튼이 들어갈 부모 (가로 레이아웃)")]
    [SerializeField] Transform       filterContainer;
    [Tooltip("복제해서 쓸 버튼 하나. 씬에 꺼둔 채로 둔다.")]
    [SerializeField] Button          filterTemplate;

    [Header("필터 버튼 색")]
    [SerializeField] Color onColor  = new(0.30f, 0.52f, 0.34f);
    [SerializeField] Color offColor = new(0.22f, 0.22f, 0.25f);

    InventoryGridUI _gridUI;
    Storage         _current;

    readonly List<(ResourceGroup group, Button btn, Image bg)> _filterBtns = new();

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (filterTemplate != null) filterTemplate.gameObject.SetActive(false);

        if (gridContainer != null)
        {
            _gridUI = gridContainer.GetComponent<InventoryGridUI>();
            if (_gridUI == null) _gridUI = gridContainer.gameObject.AddComponent<InventoryGridUI>();
        }

        BuildFilterButtons();

        Storage.OnSelected               += Open;
        Storage.OnChanged                += RefreshCapacity;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        Storage.OnSelected               -= Open;
        Storage.OnChanged                -= RefreshCapacity;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel != null && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(Storage storage)
    {
        _current = storage;
        WindowRaiser.Open(panel);
        if (titleText != null) titleText.text = storage.BuildingName;

        if (_gridUI != null)
        {
            _gridUI.ReceiveFilter = storage.CanAccept;
            _gridUI.Bind(storage.Inventory);
        }

        RefreshFilters();
        RefreshCapacity();
    }

    public void Close()
    {
        _current = null;
        if (_gridUI != null)
        {
            _gridUI.ReceiveFilter = null;
            _gridUI.Bind(null);
        }
        WindowRaiser.Close(panel);
    }

    // ── 용량 ─────────────────────────────────────────────────────────────────
    // 몇 칸이 찼는지. 격자 인벤토리라 "개수"가 아니라 **칸**이 한계라는 걸 보여야 한다.
    void RefreshCapacity()
    {
        if (capacityText == null) return;
        if (_current == null || _current.Inventory == null) { capacityText.text = ""; return; }

        var inv   = _current.Inventory;
        int total = inv.Width * inv.Height;
        int used  = inv.UsedCellCount;

        float ratio = total > 0 ? used / (float)total : 0f;
        string col  = ratio >= 0.95f ? "#F04040" : ratio >= 0.75f ? "#F0A030" : "#B8C4B8";
        capacityText.text = $"<color={col}>{used} / {total} 칸</color>";
    }

    // ── 필터 ─────────────────────────────────────────────────────────────────
    void BuildFilterButtons()
    {
        _filterBtns.Clear();
        if (filterContainer == null || filterTemplate == null) return;

        foreach (var group in ResourceGroupExt.All)
        {
            var btn = Instantiate(filterTemplate, filterContainer);
            btn.gameObject.SetActive(true);
            btn.gameObject.name = $"Filter_{group}";

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = group.DisplayName();

            var g = group;   // 클로저 캡처
            btn.onClick.AddListener(() => ToggleGroup(g));

            _filterBtns.Add((group, btn, btn.GetComponent<Image>()));
        }
    }

    // 묶음을 통째로 켜고 끈다.
    //
    // `Storage`의 규칙은 "**목록이 비면 전부 허용**"이다. 그래서 아무것도 안 켠 상태에서
    // 하나를 끄려고 하면 안 된다 — 먼저 전부 켠 것으로 실체화한 뒤 그 묶음만 뺀다.
    // 안 그러면 "전체 허용"에서 석재를 끄려는 순간 석재만 허용되는 반대 결과가 나온다.
    void ToggleGroup(ResourceGroup group)
    {
        if (_current == null) return;

        bool allowAll = _current.AllowsEverything;
        bool on       = allowAll || GroupEnabled(group);

        if (on && allowAll)
        {
            // 전부 허용 상태에서 하나를 끄는 중 → 나머지를 명시적으로 켠다.
            // 그냥 하나를 빼려 하면 안 된다("빈 목록 = 전부 허용"이라 뺄 것이 없다).
            foreach (var g in ResourceGroupExt.All)
                if (g != group) _current.AddFilter(g);
        }
        else if (on)
        {
            _current.RemoveFilter(group);
            // 전부 꺼졌으면 "빈 목록 = 전부 허용"으로 돌아가 버린다 → 그건 의도와 반대다.
            // 하나도 안 남으면 방금 끈 묶음만 되살려 "최소 하나는 켜짐"을 유지한다.
            if (_current.AllowsEverything) _current.AddFilter(group);
        }
        else
        {
            _current.AddFilter(group);
            // 전부 켜졌으면 목록을 비워 "전부 허용"의 정규형으로 되돌린다.
            if (AllGroupsEnabled()) _current.SetAllowAll();
        }

        // 필터가 바뀌면 그리드의 드롭 허용도 따라가야 한다.
        if (_gridUI != null) _gridUI.ReceiveFilter = _current.CanAccept;
        RefreshFilters();
    }

    bool GroupEnabled(ResourceGroup group)
    {
        if (_current == null) return false;
        if (_current.AllowsEverything) return true;   // 빈 목록 = 전부 허용
        return _current.HasFilter(group);
    }

    bool AllGroupsEnabled()
    {
        foreach (var g in ResourceGroupExt.All)
            if (!GroupEnabled(g)) return false;
        return true;
    }

    void RefreshFilters()
    {
        foreach (var (group, _, bg) in _filterBtns)
            if (bg != null) bg.color = GroupEnabled(group) ? onColor : offColor;
    }
}

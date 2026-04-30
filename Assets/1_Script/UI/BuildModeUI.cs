using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildModeUI : MonoBehaviour
{
    [Header("패널 루트")]
    [SerializeField] GameObject panel;

    [Header("카테고리 (GridLayoutGroup, 버튼 미리 생성됨)")]
    [SerializeField] Transform categoryContainer;

    [Header("건물 목록 (ScrollRect Content)")]
    [SerializeField] Transform       buildingContainer;
    [SerializeField] BuildingItemUI  buildingItemPrefab;

    [Header("색상")]
    [SerializeField] Color colCatNormal    = new(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] Color colCatSelected  = new(0.25f, 0.50f, 0.25f, 1f);
    [SerializeField] Color colItemNormal   = new(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] Color colItemSelected = new(0.20f, 0.48f, 1.00f, 1f);
    [SerializeField] Color colItemNoFunds  = new(0.45f, 0.12f, 0.12f, 1f);

    static readonly BuildCategory[] AllCategories =
    {
        BuildCategory.Resource,
        BuildCategory.Defense,
        BuildCategory.Building,
        BuildCategory.Misc,
    };

    readonly List<(Image bg, BuildCategory cat)> _catBtns  = new();
    readonly List<(BuildingItemUI item, int idx)> _itemBtns = new();
    BuildCategory _selectedCat = BuildCategory.Building;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        BuildModeManager.OnActiveChanged += OnActiveChanged;
    }

    void Start() => WireCategories();

    void OnDestroy() => BuildModeManager.OnActiveChanged -= OnActiveChanged;

    void OnActiveChanged(bool active)
    {
        if (panel != null) panel.SetActive(active);
        if (active) ShowCategory(_selectedCat);
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        RefreshCatColors();
        RefreshItemColors();
    }

    // ── 카테고리 버튼 이벤트 연결 ─────────────────────────────────────────────

    void WireCategories()
    {
        if (categoryContainer == null) return;
        for (int i = 0; i < categoryContainer.childCount && i < AllCategories.Length; i++)
        {
            var child    = categoryContainer.GetChild(i).gameObject;
            var cat      = AllCategories[i];
            var captured = cat;
            child.GetComponent<Button>()?.onClick.AddListener(() => ShowCategory(captured));
            _catBtns.Add((child.GetComponent<Image>(), cat));
        }
    }

    // ── 건물 버튼 생성 ────────────────────────────────────────────────────────

    void ShowCategory(BuildCategory cat)
    {
        _selectedCat = cat;
        foreach (Transform child in buildingContainer) Destroy(child.gameObject);
        _itemBtns.Clear();

        var mgr = BuildModeManager.Instance;
        if (mgr == null || buildingItemPrefab == null) return;

        for (int i = 0; i < mgr.TotalBuildingCount; i++)
        {
            BuildCategory itemCat;
            string name, cost;

            if (i < mgr.NodeBuildingCount)
            {
                itemCat = BuildCategory.Resource;
                var nd  = mgr.GetNode(i);
                name    = nd != null ? nd.ResourceType.ToString() : "?";
                cost    = "";
            }
            else
            {
                var sc  = mgr.GetSubCore(i - mgr.NodeBuildingCount);
                itemCat = sc?.Category ?? BuildCategory.Building;
                name    = sc?.CoreName ?? "?";
                cost    = (sc != null && sc.CostAmount > 0) ? $"{sc.CostType} ×{sc.CostAmount}" : "";
            }

            if (itemCat != cat) continue;

            int captured = i;
            var item = Instantiate(buildingItemPrefab, buildingContainer);
            item.Setup(name, cost, () => mgr.SelectBuilding(captured));
            _itemBtns.Add((item, i));
        }
    }

    // ── 색 갱신 ───────────────────────────────────────────────────────────────

    void RefreshCatColors()
    {
        foreach (var (bg, cat) in _catBtns)
            if (bg != null) bg.color = cat == _selectedCat ? colCatSelected : colCatNormal;
    }

    void RefreshItemColors()
    {
        var mgr = BuildModeManager.Instance;
        if (mgr == null) return;
        foreach (var (item, idx) in _itemBtns)
        {
            if (item == null) continue;
            bool sel   = idx == mgr.SelectedIndex;
            bool funds = CanAfford(mgr, idx);
            item.SetColor(sel ? colItemSelected : funds ? colItemNormal : colItemNoFunds);
        }
    }

    bool CanAfford(BuildModeManager mgr, int idx)
    {
        if (idx < mgr.NodeBuildingCount) return true;
        var sc = mgr.GetSubCore(idx - mgr.NodeBuildingCount);
        if (sc == null || sc.CostAmount <= 0) return true;
        return Storage.GetTotal(sc.CostType) >= sc.CostAmount;
    }
}

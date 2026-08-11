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
    [SerializeField] Transform      buildingContainer;
    [SerializeField] BuildingItemUI buildingItemPrefab;

    [Header("색상")]
    [SerializeField] Color colCatNormal    = new(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] Color colCatSelected  = new(0.25f, 0.50f, 0.25f, 1f);
    [SerializeField] Color colItemNormal   = new(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] Color colItemSelected = new(0.20f, 0.48f, 1.00f, 1f);
    [SerializeField] Color colItemNoFunds  = new(0.45f, 0.12f, 0.12f, 1f);
    [Tooltip("연구가 안 끝나 못 짓는 건물. 자원 부족(붉은색)과 **다른 색이어야** 한다 — " +
             "돈이 없는 것과 아직 못 배운 것은 할 일이 다르다.")]
    [SerializeField] Color colItemLocked   = new(0.16f, 0.16f, 0.20f, 1f);

    static readonly BuildCategory[] AllCategories =
    {
        BuildCategory.Resource,
        BuildCategory.Defense,
        BuildCategory.Building,
        BuildCategory.Misc,
    };

    readonly List<(Image bg, BuildCategory cat)>  _catBtns  = new();
    readonly List<(BuildingItemUI item, int idx)> _itemBtns = new();
    BuildCategory _selectedCat = BuildCategory.Building;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        BuildModeManager.OnActiveChanged += OnActiveChanged;

        // 연구가 끝나면 **목록을 다시 짓는다.** 색만 바꾸면 `[잠김] 목공술 연구 필요`가
        // 그대로 남는다 — 잠금 문구는 목록을 지을 때 한 번 적히기 때문이다.
        ResearchManager.OnResearchCompleted += OnResearchDone;
    }

    void Start() => WireCategories();

    void OnDestroy()
    {
        BuildModeManager.OnActiveChanged    -= OnActiveChanged;
        ResearchManager.OnResearchCompleted -= OnResearchDone;
    }

    void OnResearchDone(ResearchData _)
    {
        if (panel != null && panel.activeSelf) ShowCategory(_selectedCat);
    }

    void OnActiveChanged(bool active)
    {
        if (panel != null) panel.SetActive(active);
        if (active) ShowCategory(_selectedCat);
    }

    // **자금 색은 매 프레임 다시 계산할 것이 아니다.** `CanAfford`가 건물마다
    // `MaterialPool.AvailableAnywhere`를 부르는데, 그건 창고 집계 + **플레이어 폰 전부의
    // 배낭**을 훑는 일이다 — 목록에 서른세 개가 있으니 프레임마다 서른세 번이 된다.
    //
    // 고른 것이 바뀌는 것은 즉시 보여야 하므로 **선택 표시는 매 프레임**, 자금 색만 늦춘다.
    [SerializeField] float fundsInterval = 0.25f;
    float _fundsTimer;

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        _fundsTimer -= Time.unscaledDeltaTime;
        if (_fundsTimer <= 0f) { _fundsTimer = fundsInterval; RefreshFunds(); }

        RefreshCatColors();
        RefreshItemColors();
    }

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

    void ShowCategory(BuildCategory cat)
    {
        _selectedCat = cat;
        foreach (Transform child in buildingContainer) Destroy(child.gameObject);
        _itemBtns.Clear();

        // 목록을 새로 지었으면 자금 캐시도 **곧바로** 다시 잰다 — 다음 주기까지 기다리면
        // 카테고리를 바꾼 순간 지난 목록의 색이 잠깐 남는다(항목 번호가 그대로 재사용된다).
        _fundsTimer = 0f;

        var mgr = BuildModeManager.Instance;
        if (mgr == null || buildingItemPrefab == null) return;

        for (int i = 0; i < mgr.TotalBuildingCount; i++)
        {
            var bd = mgr.GetBuilding(i);
            if (bd == null || bd.Category != cat) continue;

            // **잠긴 건물도 목록에 남긴다.** 안 보이면 "이 게임에 제재소가 있긴 한가"를 알 수 없고,
            // 무엇을 목표로 연구할지도 정할 수 없다. 대신 **무엇을 뚫어야 하는지**를 그 자리에 적는다.
            bool locked = !bd.IsUnlocked;

            string costLabel;
            if (locked)
                costLabel = $"[잠김] {bd.RequiredResearch.ResearchName} 연구 필요";
            else
                // 비용 이름이 **한글로 나온다.** 예전엔 enum 값이 그대로 찍혀 "IronOre ×20"이 보였다.
                costLabel = bd.CostAmount > 0 && bd.CostItem != null
                          ? $"{bd.CostItem.ItemName} ×{bd.CostAmount}" : "";

            int captured = i;
            var item = Instantiate(buildingItemPrefab, buildingContainer);
            // 잠긴 것은 눌러도 아무 일도 안 일어난다 — `BuildModeManager`가 배치도 막지만,
            // 여기서 막아야 "골랐는데 고스트가 안 뜨는" 상태가 안 생긴다.
            item.Setup(bd.BuildingName, costLabel,
                       locked ? (System.Action)null : () => mgr.SelectBuilding(captured));
            _itemBtns.Add((item, i));
        }
    }

    void RefreshCatColors()
    {
        foreach (var (bg, cat) in _catBtns)
            if (bg != null) bg.color = cat == _selectedCat ? colCatSelected : colCatNormal;
    }

    // 자금이 되는가 — **비싼 쪽만 캐시한다.** 색칠 자체는 매 프레임 해야 고른 항목이 곧바로
    // 파래지는데, 그 안의 `CanAfford`는 목록 전체를 훑으므로 늦춰 둔다.
    readonly HashSet<int> _unaffordable = new();

    void RefreshFunds()
    {
        var mgr = BuildModeManager.Instance;
        if (mgr == null) return;

        _unaffordable.Clear();
        foreach (var (_, idx) in _itemBtns)
            if (!CanAfford(mgr, idx)) _unaffordable.Add(idx);
    }

    void RefreshItemColors()
    {
        var mgr = BuildModeManager.Instance;
        if (mgr == null) return;
        foreach (var (item, idx) in _itemBtns)
        {
            if (item == null) continue;

            // 잠김이 제일 강한 상태다 — 자원이 남아돌아도 못 짓는다.
            var bd = mgr.GetBuilding(idx);
            if (bd != null && !bd.IsUnlocked) { item.SetColor(colItemLocked); continue; }

            bool sel   = idx == mgr.SelectedIndex;
            bool funds = !_unaffordable.Contains(idx);
            item.SetColor(sel ? colItemSelected : funds ? colItemNormal : colItemNoFunds);
        }
    }

    // **자재가 부족해도 놓을 수는 있다** — 청사진은 계획이고, 모자라면 폰이 안 갈 뿐이다.
    // 이 색은 막는 표시가 아니라 "지금 지어지진 않는다"는 알림이다.
    //
    // 세는 축은 `MaterialPool` — 창고만 세면 폰이 사 온 목재를 안 보고 붉게 물든다.
    bool CanAfford(BuildModeManager mgr, int idx)
    {
        var bd = mgr.GetBuilding(idx);
        return bd == null || bd.CostAmount <= 0
            || MaterialPool.AvailableAnywhere(bd.CostItem) >= bd.CostAmount;
    }
}

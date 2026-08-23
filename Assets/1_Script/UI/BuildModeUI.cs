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

    readonly List<(Image bg, BuildCategory cat)>  _catBtns  = new();
    readonly List<(BuildingItemUI item, int idx)> _itemBtns = new();
    BuildCategory _selectedCat = BuildCategory.Floor;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        BuildModeManager.OnActiveChanged += OnActiveChanged;

        // 연구가 끝나면 **목록을 다시 짓는다.** 색만 바꾸면 `[잠김] 목공술 연구 필요`가
        // 그대로 남는다 — 잠금 문구는 목록을 지을 때 한 번 적히기 때문이다.
        ResearchManager.OnResearchCompleted += OnResearchDone;
    }

    void OnDestroy()
    {
        BuildModeManager.OnActiveChanged    -= OnActiveChanged;
        ResearchManager.OnResearchCompleted -= OnResearchDone;
    }

    void OnResearchDone(ResearchData _) => Rebuild();

    void Rebuild()
    {
        if (panel != null && panel.activeSelf) ShowCategory(_selectedCat);
    }

    void OnActiveChanged(bool active)
    {
        if (panel != null) panel.SetActive(active);
        if (!active) return;

        // **처음 열 때 짓는다 — `Start`가 아니라.** 갈래마다 건물이 있는지 물으려면
        // `BuildModeManager`가 목록을 다 실은 뒤여야 하는데, `Start`끼리의 순서는 보장이
        // 없어서 *"아직 안 섰다"*로 새면 **빈 갈래까지 전부 뜬다**.
        if (_catBtns.Count == 0) WireCategories();
        ShowCategory(_selectedCat);
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

    // **갈래 버튼은 씬의 첫 자식을 본떠 찍는다.**
    //
    // 전에는 씬에 미리 만들어 둔 자식을 순서대로 집어 갈래에 짝지었다 — 그러면 갈래를 하나
    // 더할 때마다 **씬에서 버튼을 손으로 복제해야 하고**, 빠뜨리면 그 갈래의 건물이 통째로
    // 안 보인다(코드는 `childCount`까지만 짝지었다). 목록·격자를 템플릿 복제로 짓는 이 저장소의
    // 규약을 갈래만 안 따르고 있었다(`docs/editor_creators.md` "모양은 씬이, 코드는 데이터만").
    //
    // **모양은 그대로 씬이 갖는다** — 여기서 정하는 것은 몇 개를 찍고 무엇이라 적을지뿐이다.
    //
    // **비어 있는 갈래는 안 띄운다.** 눌러도 빈 목록만 나오는 칸은 고를 것을 가릴 뿐이다
    // (모드가 갈래를 안 채웠을 때도 저절로 접힌다).
    void WireCategories()
    {
        if (categoryContainer == null || categoryContainer.childCount == 0) return;

        var template = categoryContainer.GetChild(0).gameObject;
        template.SetActive(false);

        // 옛 버튼들(씬에 남아 있는 자원·방어·건물·기타)을 걷어낸다 — 본만 남긴다.
        for (int i = categoryContainer.childCount - 1; i >= 1; i--)
            Destroy(categoryContainer.GetChild(i).gameObject);

        foreach (var cat in BuildCategoryExt.Order)
        {
            if (!HasAny(cat)) continue;

            var go = Instantiate(template, categoryContainer);
            go.name = cat.DisplayName();
            go.SetActive(true);

            var label = go.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
            if (label != null) label.text = cat.DisplayName();

            var captured = cat;
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();   // 본에 걸려 있던 옛 연결이 딸려 온다
                btn.onClick.AddListener(() => ShowCategory(captured));
            }
            _catBtns.Add((go.GetComponent<Image>(), cat));
        }

        // 처음 열었을 때 **빈 칸을 보고 있지 않게** 한다.
        if (_catBtns.Count > 0 && !HasAny(_selectedCat)) _selectedCat = _catBtns[0].cat;
    }

    static bool HasAny(BuildCategory cat)
    {
        var mgr = BuildModeManager.Instance;
        if (mgr == null) return true;   // 아직 안 섰다 — 숨기는 쪽으로 넘겨짚지 않는다

        for (int i = 0; i < mgr.TotalBuildingCount; i++)
        {
            var bd = mgr.GetBuilding(i);
            if (bd != null && bd.Category == cat) return true;
        }
        return false;
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
            // 무엇을 목표로 연구할지도 정할 수 없다. 대신 **무엇을 뚫어야 하는지**를 설명에 적는다.
            bool locked = !bd.IsUnlocked;

            int captured = i;
            var data     = bd;
            var item     = Instantiate(buildingItemPrefab, buildingContainer);

            // 잠긴 것은 눌러도 아무 일도 안 일어난다 — `BuildModeManager`가 배치도 막지만,
            // 여기서 막아야 "골랐는데 고스트가 안 뜨는" 상태가 안 생긴다.
            item.Setup(bd.BuildingName, () => Describe(data),
                       locked ? (System.Action)null : () => mgr.SelectBuilding(captured));
            _itemBtns.Add((item, i));
        }
    }

    // 마우스를 올렸을 때 뜨는 설명. **올릴 때마다 새로 짓는다** — 가진 재료가 들어가는데
    // 목록을 지을 때 한 번 구워 두면 창고가 바뀌어도 안 따라온다.
    //
    // 순서는 **무엇인가 → 얼마인가 → 왜 못 짓나**. 못 짓는 이유가 맨 아래인 것은 그것이
    // 대개 없는 줄이기 때문이다 — 있을 때만 나타나는 줄을 가운데 끼우면 줄 위치가 흔들린다.
    static string Describe(BuildingData bd)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(bd.Description))
            sb.AppendLine(bd.Description);

        if (bd.Size.x > 0)
            sb.AppendLine($"<color=#888>크기 {Mathf.Max(1, bd.Size.x)}×{Mathf.Max(1, bd.Size.y)}</color>");

        // 비용 이름이 **한글로 나온다.** 예전엔 enum 값이 그대로 찍혀 "IronOre ×20"이 보였다.
        // **가진 것을 같이 적는다** — 붉게 물든 타일을 보고 "얼마나 모자란가"를 물을 자리가
        // 여기뿐이다(청사진 살펴보기 창이 같은 답을 쓴다).
        if (bd.CostAmount > 0 && bd.CostItem != null)
        {
            int have = MaterialPool.AvailableAnywhere(bd.CostItem);
            string col = have >= bd.CostAmount ? "#CCC" : "#F66";
            sb.AppendLine($"<color={col}>{bd.CostItem.ItemName} {have}/{bd.CostAmount}</color>");
        }
        else sb.AppendLine("<color=#888>무료</color>");

        // **사유는 `BuildingData.LockReason` 하나가 답한다** — 잠그는 축이 둘이 되면서
        // (연구 · 가져와 봤는가) 여기서 조립하면 창마다 다른 말이 뜬다.
        if (!bd.IsUnlocked)
        {
            var why = bd.LockReason;
            if (!string.IsNullOrEmpty(why))
                sb.AppendLine($"<color=#F90>[잠김] {why}</color>");
        }

        return sb.ToString().TrimEnd();
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

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class CraftingTableUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject    panel;
    [SerializeField] Transform     recipeContainer;  // ScrollView의 Content
    [SerializeField] RecipeEntryUI recipeEntryPrefab;
    [SerializeField] TextMeshProUGUI tableTitleText;

    [Header("디테일 패널")]
    [SerializeField] GameObject      detailPanel;
    [SerializeField] Image           resultIcon;
    [SerializeField] TextMeshProUGUI resultNameText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI ingredientsText;
    [SerializeField] TextMeshProUGUI craftTimeText;
    [SerializeField] Button          craftButton;

    [Tooltip("한 번에 걸 개수. 기본 1 — 한 개만 만들 때는 손댈 필요가 없다.")]
    [SerializeField] TMP_InputField  craftQuantity;

    [Tooltip("수량 빼기 버튼. 그냥 1씩 · Ctrl 5씩 · Shift 10씩")]
    [SerializeField] Button          craftMinusButton;

    [Tooltip("수량 더하기 버튼. 그냥 1씩 · Ctrl 5씩 · Shift 10씩")]
    [SerializeField] Button          craftPlusButton;

    [Tooltip("만들 수 있는 최대치를 수량 칸에 넣는 버튼")]
    [SerializeField] Button          craftMaxButton;

    [Tooltip("`최대 8` 처럼 상한을 알려 주는 텍스트")]
    [SerializeField] TextMeshProUGUI craftMaxText;

    [Header("제작자 지정 (디테일 패널)")]
    [SerializeField] Button          crafterButton;
    [SerializeField] TextMeshProUGUI crafterText;

    [Header("제작자 고르기 패널")]
    // 목록은 씬의 `RowTemplate`을 복제해 채운다 — 모양은 씬이, 코드는 데이터만.
    [SerializeField] GameObject      crafterPickerPanel;
    [SerializeField] Transform       crafterPickerContent;
    [SerializeField] GameObject      crafterRowTemplate;

    [Header("제작 진행")]
    [SerializeField] GameObject      progressRow;
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] Image           progressFill;

    [Header("연료 (연료를 쓰는 작업대에서만 보임)")]
    [SerializeField] GameObject      fuelRow;
    [SerializeField] TextMeshProUGUI fuelText;
    [SerializeField] Image           fuelFill;

    [Header("산출물")]
    // 씬에는 빈 컨테이너만 두고 `InventoryGridUI`는 런타임에 붙인다(`StorageUI`와 같은 방식).
    [SerializeField] RectTransform   outputGridContainer;
    [SerializeField] Button          haulToggleButton;
    [SerializeField] TextMeshProUGUI haulToggleText;

    InventoryGridUI _outputGrid;

    // 게이지 색 — 넉넉/부족/바닥. 숫자를 안 읽어도 상태가 보이게.
    static readonly Color FuelOk  = new Color(0.35f, 0.75f, 0.35f);
    static readonly Color FuelLow = new Color(0.85f, 0.7f,  0.25f);
    static readonly Color FuelOut = new Color(0.8f,  0.3f,  0.3f);

    CraftingTable _currentTable;
    RecipeData    _selectedRecipe;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        CraftingTable.OnSelected         += Open;
        CraftingTable.OnChanged          += OnTableChanged;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        // 재료 수량·최대 개수는 **창고가 바뀔 때만** 바뀐다. 매 프레임 다시 세면
        // 창고 전부를 훑고 문자열을 새로 만들고 TMP 메시까지 다시 굽는다(같은 값이어도).
        Storage.OnChanged                += OnStorageChanged;

        if (haulToggleButton != null)
            haulToggleButton.onClick.AddListener(ToggleHaulOut);
        if (crafterButton != null)
            crafterButton.onClick.AddListener(OpenCrafterPicker);
        if (craftMaxButton != null)
            craftMaxButton.onClick.AddListener(FillMaxQuantity);
        if (craftMinusButton != null)
            craftMinusButton.onClick.AddListener(() => NudgeQuantity(-1));
        if (craftPlusButton != null)
            craftPlusButton.onClick.AddListener(() => NudgeQuantity(+1));
        // 기본값 1 — 한 개만 만들 때는 칸을 건드릴 필요가 없어야 한다.
        if (craftQuantity != null) craftQuantity.text = "1";
        if (crafterPickerPanel != null)
            crafterPickerPanel.SetActive(false);
    }

    void OnDestroy()
    {
        CraftingTable.OnSelected         -= Open;
        CraftingTable.OnChanged          -= OnTableChanged;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        Storage.OnChanged                -= OnStorageChanged;
    }

    // 연료·산출물이 바뀐 작업대가 **지금 열려 있는 그것일 때만** 갱신한다.
    void OnTableChanged(CraftingTable table)
    {
        if (table != _currentTable) return;
        RefreshFuel();
        RefreshOutput();
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    // 창고가 바뀌었다 — 폰이 철광석을 날라 오면 그 자리에서 `최대 8`이 늘어난다.
    // **닫혀 있거나 고른 레시피가 없으면 아무것도 안 한다** — 이 이벤트는 운반 때마다 울린다.
    void OnStorageChanged()
    {
        if (panel == null || !panel.activeSelf || _selectedRecipe == null) return;
        RefreshIngredients(_selectedRecipe);
        RefreshCraftButton(_selectedRecipe);
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // 고르는 패널이 열려 있으면 그것부터 닫는다 — 한 번에 하나씩 물러난다.
            if (crafterPickerPanel != null && crafterPickerPanel.activeSelf) CloseCrafterPicker();
            else                                                            Close();
            return;
        }

        // 연료·제작 진행은 **시간에 따라 계속 변하는 값**이라 매 프레임 읽는다.
        // 이벤트만 쓰면 게이지가 시작·끝에만 툭 움직여 "돌고 있다"가 안 읽힌다.
        if (_currentTable == null) return;
        if (_currentTable.RequiresFuel) RefreshFuel();
        RefreshProgress();
    }

    // ── 제작 진행 ─────────────────────────────────────────────────────────
    // 폰이 작업대에 **붙어 있는 동안만** 잡힌다. 걸어오는 중이거나 대기 중이면 줄이 사라진다 —
    // 그 자체가 "지금 아무도 안 만들고 있다"는 정보다.
    void RefreshProgress()
    {
        if (_currentTable == null)
        {
            if (progressRow != null) progressRow.SetActive(false);
            return;
        }

        // ① 폰이 붙어 지금 만드는 중
        var recipe = _currentTable.ActiveRecipe;
        if (recipe != null)
        {
            float p = _currentTable.ActiveProgress;
            if (progressRow  != null) progressRow.SetActive(true);
            if (progressFill != null) progressFill.fillAmount = p;
            SetText(progressText, $"제작 중  {recipe.RecipeName}  {p * 100f:F0}%");
            return;
        }

        // ② 아무도 안 붙어 있지만 **만들다 만 것**이 있는 상태.
        //    폰이 밥 먹으러 간 동안 "저거 80%까지 왔는데" 를 볼 수 있어야 한다.
        if (_selectedRecipe != null)
        {
            float done = _currentTable.GetPartialSeconds(_selectedRecipe);
            if (done > 0f)
            {
                float p     = Mathf.Clamp01(done / Mathf.Max(0.01f, _selectedRecipe.CraftTime));
                var   owner = _currentTable.GetPartialOwner(_selectedRecipe);
                string who  = owner != null ? $" · {owner.UnitName}" : "";

                if (progressRow  != null) progressRow.SetActive(true);
                if (progressFill != null) progressFill.fillAmount = p;
                SetText(progressText,
                        $"{_selectedRecipe.RecipeName}  {p * 100f:F0}% <color=#FFAA55>(멈춤{who})</color>");
                return;
            }
        }

        if (progressRow != null) progressRow.SetActive(false);
    }

    void Open(CraftingTable table)
    {
        _currentTable   = table;
        _selectedRecipe = null;
        if (panel != null) panel.SetActive(true);
        if (detailPanel != null) detailPanel.SetActive(false);
        if (tableTitleText != null) tableTitleText.text = table.TableType.DisplayName();
        RebuildList();
        RefreshFuel();
        RefreshOutput();
        RefreshProgress();
    }

    public void Close()
    {
        CloseCrafterPicker();
        _currentTable = null;
        if (_outputGrid != null) _outputGrid.Bind(null);   // 닫힌 패널이 남의 인벤토리를 붙들지 않게
        if (panel != null) panel.SetActive(false);
    }

    // ── 연료 ──────────────────────────────────────────────────────────────
    // 연료를 쓰는 작업대는 8종 중 용광로 하나뿐이라, 나머지에서는 줄 자체를 숨긴다.
    void RefreshFuel()
    {
        bool uses = _currentTable != null && _currentTable.RequiresFuel;
        if (fuelRow != null) fuelRow.SetActive(uses);
        if (!uses) return;

        float amount   = _currentTable.FuelAmount;
        float capacity = Mathf.Max(1f, _currentTable.FuelCapacity);
        float ratio    = Mathf.Clamp01(amount / capacity);

        if (fuelFill != null)
        {
            fuelFill.fillAmount = ratio;
            fuelFill.color      = amount <= 0f ? FuelOut : ratio < 0.3f ? FuelLow : FuelOk;
        }

        if (fuelText == null) return;

        // 무엇을 넣어야 하는지까지 적는다 — "연료 0"만 보면 뭘 채워야 할지 알 수 없다.
        string fuelName = null;
        foreach (var entry in _currentTable.AcceptedFuels)
            if (entry.Item != null) { fuelName = entry.Item.ItemName; break; }

        string label = fuelName != null ? $"연료 ({fuelName})" : "연료";
        fuelText.text = amount <= 0f
            ? $"{label}  <color=#FF6666>없음 — 창고에 보충하세요</color>"
            : $"{label}  {amount:F0} / {capacity:F0}";
    }

    // ── 산출물 ────────────────────────────────────────────────────────────
    void RefreshOutput()
    {
        if (outputGridContainer != null && _currentTable != null)
        {
            if (_outputGrid == null)
                _outputGrid = outputGridContainer.GetComponent<InventoryGridUI>()
                           ?? outputGridContainer.gameObject.AddComponent<InventoryGridUI>();
            _outputGrid.Bind(_currentTable.Inventory);
        }

        if (haulToggleText == null) return;

        bool on = _currentTable != null && _currentTable.AllowHaulOut;
        haulToggleText.text = on
            ? "<color=#88FF88>운반 켜짐</color>  창고로 옮김"
            : "<color=#FFAA55>운반 꺼짐</color>  여기 쌓아 둠";
    }

    void ToggleHaulOut()
    {
        if (_currentTable == null) return;
        _currentTable.SetAllowHaulOut(!_currentTable.AllowHaulOut);
        RefreshOutput();
    }

    // ── 제작자 지정 ───────────────────────────────────────────────────────
    // 목록을 띄우는 대신 **버튼 하나를 눌러 돌린다**(아무나 → 폰1 → 폰2 → … → 아무나).
    // 폰이 몇 명뿐이라 목록 UI를 새로 짓는 것보다 이쪽이 빠르고, 씬에 붙일 것도 버튼 하나뿐이다.
    static List<UnitBrain> EligibleCrafters()
    {
        var list = new List<UnitBrain>();
        foreach (var brain in Object.FindObjectsByType<UnitBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var faction = brain.GetComponent<Faction>();
            if (faction == null || !faction.IsPlayer) continue;
            if (brain.Work != null && !brain.Work.IsEnabled(JobCategory.Crafting)) continue;
            list.Add(brain);
        }
        // 실행마다 순서가 달라지면 같은 버튼이 다른 폰을 가리킨다 — 이름으로 못 박는다.
        list.Sort((a, b) => string.Compare(a.UnitName, b.UnitName, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    // 버튼을 누르면 **고르는 패널**이 열린다. 돌려 가며 맞추는 방식은 폰이 늘면
    // 원하는 사람을 지나쳐 다시 한 바퀴 돌아야 해서, 누가 후보인지도 안 보인다.
    void OpenCrafterPicker()
    {
        if (crafterPickerPanel == null || crafterPickerContent == null || crafterRowTemplate == null) return;
        if (_currentTable == null || _selectedRecipe == null) return;

        // 만들다 만 장비는 그 폰으로 고정이라 고를 것이 없다.
        if (_currentTable.GetPartialOwner(_selectedRecipe) != null) return;

        crafterRowTemplate.SetActive(false);
        foreach (Transform child in crafterPickerContent)
            if (child.gameObject != crafterRowTemplate) Destroy(child.gameObject);

        var current = _currentTable.GetAssignedCrafter(_selectedRecipe);

        AddCrafterRow("아무나", current == null, null);
        foreach (var pawn in EligibleCrafters())
        {
            // 스킬을 함께 보여준다 — 등급 있는 장비는 이 숫자가 결과를 정하므로 고르는 유일한 근거다.
            float skill = pawn.Stats != null ? pawn.Stats.GetCraftSkill(_selectedRecipe.RequiredSkillType) : 0f;
            AddCrafterRow($"{pawn.UnitName}   {_selectedRecipe.RequiredSkillType.DisplayName()} {skill:F0}",
                          current == pawn, pawn);
        }

        crafterPickerPanel.SetActive(true);
    }

    void AddCrafterRow(string label, bool selected, UnitBrain pawn)
    {
        var row = Instantiate(crafterRowTemplate, crafterPickerContent);
        row.SetActive(true);

        var text = row.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = selected ? $"<color=#88FF88>{label}</color>" : label;

        var btn = row.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ChooseCrafter(pawn));
        }
    }

    void ChooseCrafter(UnitBrain pawn)
    {
        if (_currentTable != null && _selectedRecipe != null)
            _currentTable.SetAssignedCrafter(_selectedRecipe, pawn);

        CloseCrafterPicker();
        RefreshCrafter();
    }

    void CloseCrafterPicker()
    {
        if (crafterPickerPanel != null) crafterPickerPanel.SetActive(false);
    }

    void RefreshCrafter()
    {
        if (crafterText == null) return;

        if (_currentTable == null || _selectedRecipe == null) { crafterText.text = "제작자: —"; return; }

        var assigned = _currentTable.GetAssignedCrafter(_selectedRecipe);
        var owner    = _currentTable.GetPartialOwner(_selectedRecipe);

        // 만들다 만 장비가 있으면 **그 폰으로 고정**이라는 사실이 지정보다 우선해서 보여야 한다.
        if (owner != null)
        {
            crafterText.text = $"제작자: {owner.UnitName} <color=#FFAA55>(만들던 중)</color>";
            return;
        }
        crafterText.text = assigned == null ? "제작자: 아무나" : $"제작자: {assigned.UnitName}";
    }

    void RebuildList()
    {
        if (recipeContainer == null || recipeEntryPrefab == null) return;

        foreach (Transform child in recipeContainer)
            Destroy(child.gameObject);

        if (_currentTable == null) return;

        foreach (var recipe in _currentTable.Recipes)
        {
            bool unlocked = ResearchManager.Instance == null
                         || ResearchManager.Instance.IsUnlocked(recipe);

            var entry = Instantiate(recipeEntryPrefab, recipeContainer);
            entry.Setup(recipe, unlocked);
            entry.OnSelected += SelectRecipe;   // 잠겨도 눌러서 내용은 볼 수 있게
        }
    }

    void SelectRecipe(RecipeData recipe)
    {
        _selectedRecipe = recipe;
        CloseCrafterPicker();   // 다른 레시피를 고르면 이전 레시피용 목록은 의미가 없다
        ShowDetail(recipe);
    }

    void ShowDetail(RecipeData recipe)
    {
        if (detailPanel != null) detailPanel.SetActive(true);

        if (resultIcon != null)
        {
            resultIcon.sprite = recipe.Result?.Icon;
            resultIcon.color  = recipe.Result?.Icon != null ? Color.white : new Color(1,1,1,0);
        }
        if (resultNameText != null) resultNameText.text = recipe.Result?.ItemName ?? recipe.RecipeName;
        if (craftTimeText  != null) craftTimeText.text  = $"제작 시간: {recipe.CraftTime}s";

        // 잠긴 레시피는 **왜 잠겼는지**를 설명 맨 위에 붙인다.
        // 목록에서 `[잠김]`만 보면 무슨 연구를 해야 하는지 알 수 없다.
        bool locked = ResearchManager.Instance != null && !ResearchManager.Instance.IsUnlocked(recipe);
        if (descriptionText != null)
        {
            var sb = new StringBuilder();
            if (locked)
            {
                var need = ResearchManager.Instance.GetRequiredResearch(recipe);
                sb.AppendLine($"<color=#FF9060>[잠김] {(need != null ? need.ResearchName : "연구")} 연구가 필요합니다.</color>");
                sb.AppendLine();
            }
            sb.Append(recipe.Result?.Description ?? "");
            descriptionText.text = sb.ToString();
        }

        RefreshIngredients(recipe);
        RefreshCrafter();

        if (craftButton != null)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(() => OnCraftClicked(recipe));
            // 잠금·재료·수량 표시를 한 곳에서 정한다(`RefreshCraftButton`).
            RefreshCraftButton(recipe);
        }
    }

    void RefreshIngredients(RecipeData recipe)
    {
        if (ingredientsText == null) return;
        var sb = new StringBuilder();
        foreach (var ing in recipe.Ingredients)
        {
            // 판정(`CanCraft`)과 **같은 기준**으로 센다. 다르면 화면이 두 답을 낸다 —
            // 재료가 3/3인데 버튼은 비활성이거나, 그 반대.
            int have   = Storage.GetTotalByItem(ing.Item);
            string col = have >= ing.Amount ? "#88FF88" : "#FF6666";
            sb.AppendLine($"<color={col}>{ing.Item?.ItemName}  {have} / {ing.Amount}</color>");
        }
        ingredientsText.text = sb.ToString();
    }

    // 수량은 **입력칸**이 정한다. 기본값 1이라 한 개만 만들 때는 그냥 누르면 되고,
    // 여러 개가 필요할 때만 칸을 고친다(상인 구매창과 같은 모양 — 칸 + [최대]).
    // 칸이 씬에 없으면 1개씩 — 조작이 죽는 것보다 낫다.
    int ReadCount(int max)
    {
        if (max < 1) return 0;
        return Mathf.Clamp(RawCount(), 1, max);
    }

    // 칸에 적힌 수 그대로(상한 적용 전). 비었거나 숫자가 아니면 1.
    int RawCount()
    {
        if (craftQuantity == null || !int.TryParse(craftQuantity.text, out int n)) return 1;
        return n;
    }

    // -/+ 한 번에 움직이는 폭. **보조키로 성큼성큼** — 20개를 맞추려고 스무 번 누르게 하지 않는다.
    // 버튼이 있으니 보조키를 몰라도 조작은 되고, 알면 빨라진다(몰라서 막히는 것이 없다).
    static int StepAmount()
    {
        var kb = Keyboard.current;
        if (kb == null) return 1;
        if (kb.shiftKey.isPressed) return 10;
        if (kb.ctrlKey.isPressed)  return 5;
        return 1;
    }

    void NudgeQuantity(int sign)
    {
        if (craftQuantity == null || _currentTable == null || _selectedRecipe == null) return;

        int max = Mathf.Max(1, _currentTable.MaxCraftable(_selectedRecipe));
        craftQuantity.text = Mathf.Clamp(RawCount() + sign * StepAmount(), 1, max).ToString();
    }

    void FillMaxQuantity()
    {
        if (craftQuantity == null || _selectedRecipe == null || _currentTable == null) return;
        craftQuantity.text = Mathf.Max(1, _currentTable.MaxCraftable(_selectedRecipe)).ToString();
    }

    void OnCraftClicked(RecipeData recipe)
    {
        if (_currentTable == null) return;

        int count = ReadCount(_currentTable.MaxCraftable(recipe));
        if (count <= 0) return;

        _currentTable.QueueOrder(recipe, count);

        // 건 만큼은 소진됐으니 칸을 1로 되돌린다 — 10을 걸어 두고 잊으면 다음 클릭에 또 10이 걸린다.
        if (craftQuantity != null) craftQuantity.text = "1";

        RefreshIngredients(recipe);
        RefreshCraftButton(recipe);
    }

    // 잠금·재료·수량 표시를 한 곳에서 정한다.
    void RefreshCraftButton(RecipeData recipe)
    {
        if (_currentTable == null) return;

        int  max    = _currentTable.MaxCraftable(recipe);
        bool locked = IsLocked(recipe);

        if (craftButton != null) craftButton.interactable = !locked && max > 0;
        if (craftMaxButton   != null) craftMaxButton.interactable   = !locked && max > 0;
        if (craftMinusButton != null) craftMinusButton.interactable = !locked && max > 0;
        if (craftPlusButton  != null) craftPlusButton.interactable  = !locked && max > 0;

        // **몇 개까지 걸 수 있는지 보여 준다.** 칸에 아무 수나 적어도 이걸 못 넘으므로,
        // 안 적어 두면 "20을 적었는데 8만 걸렸다"가 설명되지 않는다.
        SetText(craftMaxText, locked ? "" : $"최대 {max}");
    }

    // **값이 같으면 안 넣는다.** TMP는 같은 글자를 대입해도 메시를 다시 굽는다 —
    // 창고 이벤트는 운반마다 울리므로, 이 한 줄이 대부분의 헛일을 걷어낸다.
    static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    bool IsLocked(RecipeData recipe) =>
        ResearchManager.Instance != null && !ResearchManager.Instance.IsUnlocked(recipe);


}

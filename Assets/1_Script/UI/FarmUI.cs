using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 밭 패널 — 상태 · 성장/수분 게이지 · 씨앗 고르기.
//
// **모양은 씬이, 코드는 데이터만.** 씨앗 줄은 씬의 `SeedTemplate`을 복제해 채운다.
// 예전엔 `RefreshSeedButtons`가 `new GameObject`로 버튼을 통째로 만들었고, 그걸
// **`Update`마다** 지웠다 다시 만들었다 — 모양이 코드에 박혀 편집 화면과 어긋나는 데다,
// 패널을 열어 두는 내내 매 프레임 UI를 새로 짓고 있었다.
public class FarmUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI stateText;

    [Header("게이지")]
    [Tooltip("Image Type=Filled, Fill Method=Horizontal")]
    [SerializeField] Image           growthFill;
    [SerializeField] TextMeshProUGUI growthText;
    [SerializeField] Image           moistureFill;
    [SerializeField] TextMeshProUGUI moistureText;

    [Header("씨앗")]
    [Tooltip("씨앗 줄이 쌓일 부모 (세로 레이아웃)")]
    [SerializeField] Transform       seedContainer;
    [Tooltip("복제해서 쓸 줄 하나. 씬에 꺼둔 채로 둔다.")]
    [SerializeField] Button          seedTemplate;
    [SerializeField] Button          clearSeedButton;

    [Header("색")]
    [SerializeField] Color selectedColor  = new(0.24f, 0.46f, 0.26f);
    [SerializeField] Color availableColor = new(0.20f, 0.20f, 0.24f);
    [SerializeField] Color emptyColor     = new(0.14f, 0.14f, 0.16f);   // 창고에 없는 씨앗

    // 줄 하나가 들고 있는 것들. 매 프레임 다시 만들지 않고 **글자·색만 갈아 끼운다.**
    struct Row
    {
        public SeedData        Seed;
        public GameObject      Go;
        public Image           Bg;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI CountText;
    }

    readonly List<Row> _rows = new();
    FarmPlot _current;
    int      _builtForSeedCount = -1;   // 목록을 다시 지어야 하는지 판단용

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (seedTemplate != null) seedTemplate.gameObject.SetActive(false);

        FarmPlot.OnSelected              += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        clearSeedButton?.onClick.AddListener(ClearSeed);
    }

    void OnDestroy()
    {
        FarmPlot.OnSelected              -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
        Refresh();
    }

    void Open(FarmPlot plot)
    {
        _current           = plot;
        _builtForSeedCount = -1;   // 다른 밭일 수 있으니 목록을 새로 짓게 한다
        WindowRaiser.Open(panel);
        Refresh();
    }

    public void Close()
    {
        _current = null;
        if (panel != null) panel.SetActive(false);
    }

    void Refresh()
    {
        if (_current == null) return;

        // 제목은 건물 표시명(`밭`)과 같은 말을 쓴다 — 같은 것을 두 이름으로 부르지 않게.
        if (titleText != null)
            titleText.text = _current.SelectedSeed != null
                ? $"밭 — {_current.SelectedSeed.ItemName}"
                : "밭";

        if (stateText != null)
            stateText.text = _current.State switch
            {
                FarmState.Empty          => _current.SelectedSeed == null
                                          ? "씨앗을 고르세요"
                                          : "심을 차례 — 폰을 기다리는 중",
                FarmState.Planted        => _current.Moisture > 0f ? "자라는 중" : "수분 부족 — 성장 멈춤",
                FarmState.ReadyToHarvest => "수확할 수 있음",
                _                        => "",
            };

        SetGauge(growthFill, growthText, _current.GrowthProgress, $"성장 {_current.GrowthProgress * 100f:F0}%");

        float maxMoisture = _current.SelectedSeed != null ? _current.SelectedSeed.WaterFillAmount : 100f;
        float wet         = maxMoisture > 0f ? _current.Moisture / maxMoisture : 0f;
        SetGauge(moistureFill, moistureText, wet, $"수분 {_current.Moisture:F0} / {maxMoisture:F0}");

        RefreshSeedRows();

        if (clearSeedButton != null)
            clearSeedButton.interactable = _current.State == FarmState.Empty && _current.SelectedSeed != null;
    }

    static void SetGauge(Image fill, TextMeshProUGUI label, float ratio01, string text)
    {
        if (fill  != null) fill.fillAmount = Mathf.Clamp01(ratio01);
        if (label != null) label.text      = text;
    }

    // ── 씨앗 줄 ──────────────────────────────────────────────────────────
    void RefreshSeedRows()
    {
        if (seedContainer == null || seedTemplate == null) return;

        var seeds = _current.AvailableSeeds;

        // 심고 나면 고를 일이 없다 — 목록을 통째로 숨긴다.
        bool show = _current.State == FarmState.Empty && seeds != null && seeds.Length > 0;
        seedContainer.gameObject.SetActive(show);
        if (!show) return;

        // **줄은 씨앗 종류가 바뀔 때만 다시 짓는다.** 매 프레임 지웠다 만들면
        // 패널을 열어 두는 내내 UI를 새로 짓는 셈이고, 클릭도 씹힌다.
        if (_builtForSeedCount != seeds.Length) BuildSeedRows(seeds);

        foreach (var row in _rows)
        {
            // 창고 + 폰 배낭까지 센다 — 상점에서 막 사 온 씨앗은 배낭에 있다.
            int have = MaterialPool.AvailableAnywhere(row.Seed);

            if (row.CountText != null) row.CountText.text = have.ToString();
            if (row.Bg != null)
                row.Bg.color = _current.SelectedSeed == row.Seed ? selectedColor
                             : have > 0                          ? availableColor
                                                                 : emptyColor;
        }
    }

    void BuildSeedRows(SeedData[] seeds)
    {
        foreach (var row in _rows) if (row.Go != null) Destroy(row.Go);
        _rows.Clear();

        foreach (var seed in seeds)
        {
            var btn = Instantiate(seedTemplate, seedContainer);
            btn.gameObject.SetActive(true);

            // 이름 칸과 수량 칸은 템플릿의 자식 이름으로 찾는다 — 코드가 만들지 않으므로
            // 폭·폰트·여백은 전부 씬이 갖는다.
            var nameText  = FindText(btn.transform, "Name");
            var countText = FindText(btn.transform, "Count");
            if (nameText != null) nameText.text = seed.ItemName;

            var captured = seed;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => { _current?.SetSeed(captured); Refresh(); });

            _rows.Add(new Row
            {
                Seed      = seed,
                Go        = btn.gameObject,
                Bg        = btn.GetComponent<Image>(),
                NameText  = nameText,
                CountText = countText,
            });
        }
        _builtForSeedCount = seeds.Length;
    }

    static TextMeshProUGUI FindText(Transform root, string childName)
    {
        var t = root.Find(childName);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    void ClearSeed()
    {
        _current?.ClearSeed();
        Refresh();
    }
}

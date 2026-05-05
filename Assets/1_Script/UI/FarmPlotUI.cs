using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FarmPlotUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI stateText;
    [SerializeField] TextMeshProUGUI growthText;
    [SerializeField] TextMeshProUGUI moistureText;
    [SerializeField] Transform       seedButtonParent;
    [SerializeField] Button          clearSeedButton;

    FarmPlot _current;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
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
        _current = plot;
        panel.SetActive(true);
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

        if (titleText != null)
            titleText.text = _current.SelectedSeed != null
                ? $"농장 — {_current.SelectedSeed.ItemName}"
                : "농장";

        if (stateText != null)
            stateText.text = _current.State switch
            {
                FarmState.Empty          => "비어있음",
                FarmState.Planted        => _current.Moisture > 0f ? "성장 중" : "수분 부족 (성장 멈춤)",
                FarmState.ReadyToHarvest => "수확 가능",
                _                        => "",
            };

        if (growthText != null)
            growthText.text = $"성장: {_current.GrowthProgress * 100f:F0}%";

        if (moistureText != null)
            moistureText.text = $"수분: {_current.Moisture:F0} / {(_current.SelectedSeed != null ? _current.SelectedSeed.WaterFillAmount : 100f):F0}";

        RefreshSeedButtons();

        if (clearSeedButton != null)
            clearSeedButton.interactable = _current.State == FarmState.Empty && _current.SelectedSeed != null;
    }

    void RefreshSeedButtons()
    {
        if (seedButtonParent == null) return;

        foreach (Transform child in seedButtonParent)
            Destroy(child.gameObject);

        if (_current.State != FarmState.Empty || _current.AvailableSeeds == null) return;

        foreach (var seed in _current.AvailableSeeds)
        {
            var go  = new GameObject(seed.ItemName, typeof(RectTransform));
            go.transform.SetParent(seedButtonParent, false);

            var img = go.AddComponent<Image>();
            img.color = _current.SelectedSeed == seed
                ? new Color(0.3f, 0.7f, 0.3f)
                : new Color(0.75f, 0.75f, 0.75f);

            go.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 30f);

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var tmp       = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = seed.ItemName;
            tmp.fontSize  = 12f;
            tmp.alignment = TextAlignmentOptions.Center;
            var trt       = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var btn         = go.AddComponent<Button>();
            var capturedSeed = seed;
            btn.onClick.AddListener(() =>
            {
                _current.SetSeed(capturedSeed);
                Refresh();
            });
        }
    }

    void ClearSeed() => _current?.ClearSeed();
}

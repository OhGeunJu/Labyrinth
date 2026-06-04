using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// 아이템 상세 툴팁. 인벤토리 hover + 상인 클릭 양쪽에서 사용.
// ItemInfoPanel.Show(item, quality)        — 인벤토리 (가격 없음)
// ItemInfoPanel.Show(item, quality, price) — 상점 (상점가 표시)
// ItemInfoPanel.Hide()
public class ItemInfoPanel : MonoBehaviour
{
    public static ItemInfoPanel Instance { get; private set; }

    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI displayNameText;
    [SerializeField] TextMeshProUGUI qualityText;

    [SerializeField] GameObject      statsGroup;
    [SerializeField] TextMeshProUGUI statDamageRow;
    [SerializeField] TextMeshProUGUI statDefenseRow;
    [SerializeField] TextMeshProUGUI statWeightRow;
    [SerializeField] TextMeshProUGUI statExtraRow;

    [SerializeField] TextMeshProUGUI descText;

    [SerializeField] GameObject      priceRow;
    [SerializeField] TextMeshProUGUI priceText;

    [SerializeField] Vector2 cursorOffset = new(16f, -16f);

    RectTransform _rt;
    Canvas        _canvas;

    void Awake()
    {
        Instance = this;
        _rt      = GetComponent<RectTransform>();
        _canvas  = GetComponentInParent<Canvas>();
        gameObject.SetActive(false);
    }

    // priceLabel: null이면 "상점가"/"가치" 자동 결정
    public static void Show(ItemData item, ItemQuality quality, int price = -1, string priceLabel = null)
        => Instance?.ShowInternal(item, quality, price, priceLabel);

    public static void Hide()
        => Instance?.gameObject.SetActive(false);

    void ShowInternal(ItemData item, ItemQuality quality, int price, string priceLabel = null)
    {
        gameObject.SetActive(true);

        if (icon)
        {
            icon.sprite = item.Icon;
            icon.color  = item.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        }

        string qualName = quality.DisplayName();
        if (displayNameText) displayNameText.text = $"[{qualName}] {item.ItemName}";
        if (qualityText)
        {
            qualityText.text  = qualName;
            qualityText.color = QualityColor(quality);
        }

        bool hasStats = FillStats(item, quality);
        if (statsGroup) statsGroup.SetActive(hasStats);

        if (descText) descText.text = item.Description ?? "";

        // price >= 0 → 상점가 표시 / price < 0이지만 BaseValue > 0 → 아이템 기준 가치 표시
        if (price >= 0)
        {
            if (priceRow)  priceRow.SetActive(true);
            if (priceText) priceText.text = $"{priceLabel ?? "상점가"}: {price} G";
        }
        else if (item.BaseValue > 0)
        {
            int baseVal = Mathf.RoundToInt(item.BaseValue * quality.StatMultiplier());
            if (priceRow)  priceRow.SetActive(true);
            if (priceText) priceText.text = $"가치: {baseVal} G";
        }
        else
        {
            if (priceRow) priceRow.SetActive(false);
        }

        FollowCursor();
    }

    bool FillStats(ItemData item, ItemQuality quality)
    {
        float mult = quality.StatMultiplier();
        if (item is WeaponData wd)
        {
            SetRow(statDamageRow,  $"공격력: {wd.BaseDamage * mult:F1}");
            SetRow(statDefenseRow, null);
            SetRow(statWeightRow,  $"무게: {wd.Weight:F1}");
            SetRow(statExtraRow,   $"사거리: {wd.AttackRange:F1}  간격: {wd.BaseAttackInterval:F1}s");
            return true;
        }
        if (item is ArmorData ad)
        {
            SetRow(statDamageRow,  null);
            SetRow(statDefenseRow, $"방어: {ad.DamageReduction * mult:F1}");
            SetRow(statWeightRow,  $"무게: {ad.Weight:F1}");
            SetRow(statExtraRow,   $"슬롯: {ad.Slot}");
            return true;
        }
        if (item is FoodData fd)
        {
            SetRow(statDamageRow,  null);
            SetRow(statDefenseRow, null);
            SetRow(statWeightRow,  $"무게: {fd.Weight:F1}");
            SetRow(statExtraRow,   $"영양: {fd.NutritionValue:F0}");
            return true;
        }
        SetRow(statDamageRow,  null);
        SetRow(statDefenseRow, null);
        SetRow(statWeightRow,  item.Weight > 0 ? $"무게: {item.Weight:F1}" : null);
        SetRow(statExtraRow,   null);
        return item.Weight > 0;
    }

    void LateUpdate()
    {
        if (gameObject.activeSelf) FollowCursor();
    }

    void FollowCursor()
    {
        if (_rt == null || _canvas == null || Mouse.current == null) return;
        Camera    cam      = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        var       cRt      = (RectTransform)_canvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cRt, Mouse.current.position.ReadValue(), cam, out var local)) return;

        Vector2 pos   = local + cursorOffset;
        Vector2 cSize = cRt.sizeDelta;
        Vector2 pSize = _rt.sizeDelta;
        pos.x = Mathf.Clamp(pos.x, -cSize.x * 0.5f, cSize.x * 0.5f - pSize.x);
        pos.y = Mathf.Clamp(pos.y, -cSize.y * 0.5f + pSize.y, cSize.y * 0.5f);
        _rt.anchoredPosition = pos;
    }

    static void SetRow(TextMeshProUGUI txt, string value)
    {
        if (txt == null) return;
        bool show = !string.IsNullOrEmpty(value);
        txt.gameObject.SetActive(show);
        if (show) txt.text = value;
    }

    public static Color QualityColor(ItemQuality q) => q switch
    {
        ItemQuality.Common    => new Color(0.80f, 0.80f, 0.80f),
        ItemQuality.Uncommon  => new Color(0.20f, 0.80f, 0.20f),
        ItemQuality.Rare      => new Color(0.20f, 0.55f, 1.00f),
        ItemQuality.Unique    => new Color(0.65f, 0.20f, 0.90f),
        ItemQuality.Epic      => new Color(1.00f, 0.50f, 0.10f),
        ItemQuality.Legendary => new Color(1.00f, 0.85f, 0.00f),
        _                     => Color.white,
    };
}

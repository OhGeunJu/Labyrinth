using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// 아이템 상세 툴팁. 인벤토리 hover + 상인 클릭 양쪽에서 사용.
// ItemInfoUI.Show(item, quality)        — 인벤토리 (가격 없음)
// ItemInfoUI.Show(item, quality, price) — 상점 (상점가 표시)
// ItemInfoUI.Hide()
public class ItemInfoUI : MonoBehaviour
{
    public static ItemInfoUI Instance { get; private set; }

    // **이 컴포넌트는 늘 켜져 있는 부모(`ItemInfo_UI`)에 살고, 보였다 사라지는 건 자식인
    // `panel`이다.** 이 프로젝트의 다른 창이 전부 그렇다(`EventNotificationUI`·`MerchantUI`·
    // `InventoryUI` 모두 `panel` 필드를 토글한다).
    //
    // 예전엔 컴포넌트가 **꺼지는 그 오브젝트에** 붙어 있었다. 씬에 `m_IsActive: 0`으로
    // 저장돼 있었고, 유니티는 꺼진 오브젝트의 `Awake`를 부르지 않으므로 `Instance`가 영영
    // null이었다 — 툴팁이 **한 번도 뜨지 않았다.** 부모를 하나 세워 두면 `Awake`가 정상적으로
    // 돌아 배선이 `Find` 없이 성립한다(런타임 탐색은 이 게임에서 매 hover마다 돌 수 있다).
    [Header("씬 참조")]
    [Tooltip("실제로 보였다 사라지는 상자. 이 컴포넌트가 붙은 오브젝트가 아니라 **자식**이어야 한다")]
    [SerializeField] GameObject      panel;

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
        _canvas  = GetComponentInParent<Canvas>();

        if (panel == null)
        {
            Debug.LogError($"[ItemInfoUI] {name}: panel이 비어 있어 툴팁이 뜨지 않습니다. " +
                           "DungeonColony → Fix Window Structure 를 실행하세요.");
            return;
        }

        _rt = panel.GetComponent<RectTransform>();
        panel.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    const string LockedNote = "<color=#FAC759>잠김 — 자동 운반·재료·판매·버리기에서 빠집니다</color>";

    // **띄운 놈이 사라지면 툴팁도 사라진다.**
    //
    // `OnPointerExit`는 **오브젝트가 파괴되면 오지 않는다.** 그래서 커서를 얹은 채로 격자
    // 아이콘이나 상점 행이 없어지면(동전 더블클릭으로 잔고에 넣기, 구매, 판매, 목록 갱신)
    // 툴팁이 그 자리에 영영 남았다.
    //
    // **띄우는 쪽마다 `OnDestroy`를 다는 대신 여기서 지켜본다** — 부르는 곳이 넷이고 앞으로
    // 늘 텐데, 하나가 빠뜨리면 그 경로에서만 툴팁이 남아 원인을 찾기 어렵다.
    Component _owner;

    // priceLabel: null이면 "상점가"/"가치" 자동 결정
    // owner: 이 툴팁을 띄운 컴포넌트. 넘기면 그게 사라질 때 툴팁도 같이 닫힌다.
    // left: 열린 하나의 남은 양(`ItemInstance.Left`). 1이면 안 적는다.
    public static void Show(ItemData item, ItemQuality quality, int price = -1,
                            string priceLabel = null, bool locked = false,
                            Component owner = null, float left = 1f)
    {
        if (Instance == null) return;
        Instance._owner = owner;
        Instance._left  = left;
        Instance.ShowInternal(item, quality, price, priceLabel, locked);
    }

    float _left = 1f;

    // **당장 안 끈다 — 프레임 끝에 판정한다**(2026-08-29). 근거는 `HoverHold`에 있다:
    // 격자에서 옆 칸으로 곧장 옮기면 Enter와 Exit이 같은 프레임에 오는데 순서가 보장되지
    // 않아, 즉시 끄면 **방금 띄운 옆 칸의 툴팁을 지나온 칸이 꺼 버린다.**
    public static void Hide()
    {
        if (Instance == null) return;
        Instance._owner = null;
        Instance._hold.HideAsked();
    }

    HoverHold _hold;

    void ShowInternal(ItemData item, ItemQuality quality, int price, string priceLabel, bool locked)
    {
        if (item == null || panel == null) return;

        WindowRaiser.Open(panel);
        _hold.Shown();

        if (icon)
        {
            icon.sprite = item.Icon;
            icon.color  = item.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        }

        // 이름줄엔 **이름만**. 등급은 바로 아랫줄에 색까지 입혀 나오므로, 이름 앞에도 붙이면
        // 같은 말이 두 번이고 정작 긴 이름이 잘린다.
        if (displayNameText) displayNameText.text = item.ItemName;

        // 아랫줄 = **등급 · 종류**. 등급만으로는 "희귀한 무엇인가"를 알 수 없다 —
        // 종류가 같이 있어야 목록에서 집어 든 것이 무엇인지 한 줄로 읽힌다.
        if (qualityText)
        {
            qualityText.text  = $"{quality.DisplayName()} · {item.TypeName}";
            // 쓰다 만 것 — 횃불은 탈 시간, 약은 남은 몫으로 읽는다.
            if (_left < 1f - ItemInstance.Epsilon)
                qualityText.text += item.BurnDays > 0f
                    ? $" · {_left * item.BurnDays * 24f:0.#}시간 남음"
                    : $" · {Mathf.CeilToInt(_left * 100f)}% 남음";
            qualityText.color = QualityColor(quality);
        }

        bool hasStats = FillStats(item, quality);
        if (statsGroup) statsGroup.SetActive(hasStats);

        if (descText) descText.text = Josa.Fix(item.Description) ?? "";

        // 잠긴 뭉치는 **왜 안 팔리는지**를 여기서 말한다. 격자의 `잠금` 글자는 상태만
        // 보여 줄 뿐이라, 팔려다 거절당한 사람은 그게 원인인 줄 모른다.
        if (locked && descText)
            descText.text = string.IsNullOrEmpty(descText.text)
                          ? LockedNote
                          : $"{descText.text}\n{LockedNote}";

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

    // 불빛 한 줄 — 램프와 횃불이 같은 줄을 쓴다(`ItemData.LightRadius`·`BurnDays`).
    static string LightLine(ItemData item)
    {
        if (!item.GivesLight) return null;
        string burn = item.BurnDays > 0f
            ? $"{item.BurnDays * 24f:0}시간 탄다"
            : "꺼지지 않는다";
        return $"불빛: {item.LightRadius:0.#}m  ({burn})";
    }

    bool FillStats(ItemData item, ItemQuality quality)
    {
        float mult = quality.StatMultiplier();
        if (item is WeaponData wd)
        {
            // **횃불은 휘두르는 무기가 아니다** — 주 무기 타격에 얹는 불이고, 사거리·간격은 주 무기의 것이다.
            if (wd.Type == WeaponType.Torch)
            {
                SetRow(statDamageRow,  $"불 피해: {wd.BaseDamage * mult:F1}  (근접 타격마다)");
                SetRow(statDefenseRow, null);
                SetRow(statWeightRow,  $"무게: {wd.Weight:F1}");
                SetRow(statExtraRow,   LightLine(wd));
                return true;
            }
            SetRow(statDamageRow,  $"공격력: {wd.BaseDamage * mult:F1}");
            SetRow(statDefenseRow, null);
            SetRow(statWeightRow,  $"무게: {wd.Weight:F1}");
            SetRow(statExtraRow,   $"사거리: {wd.AttackRange:F1}  간격: {wd.BaseAttackInterval:F1}s");
            return true;
        }
        if (item is ArmorData ad)
        {
            // 막는 것이 없는 허리 램프에 `방어: 0.0`을 적으면 고장으로 읽힌다.
            SetRow(statDamageRow,  null);
            SetRow(statDefenseRow, ad.DamageReduction > 0f ? $"방어: {ad.DamageReduction * mult:F1}" : null);
            SetRow(statWeightRow,  $"무게: {ad.Weight:F1}");
            // 어느 칸에 입는지는 위 종류줄("몸통 방어구")이 이미 말한다 — 여기 또 적지 않는다.
            SetRow(statExtraRow,   ad.GivesLight ? LightLine(ad)
                                 : !Mathf.Approximately(ad.MoveSpeedMult, 1f) ? $"이동 속도: {(ad.MoveSpeedMult - 1f) * 100f:+0;-0}%"
                                 : null);
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
        // **끄는 판정이 먼저다** — 아래의 조기 반환 뒤에 두면 창이 꺼져 있을 때 밀린 요청이
        // 영영 안 소모되어, 다음에 띄운 툴팁이 **뜨자마자 꺼진다.**
        if (_hold.ShouldHide()) { WindowRaiser.Close(panel); return; }

        if (panel == null || !panel.activeSelf) return;

        // 띄운 놈이 파괴됐거나(유니티 null) 꺼졌으면 거둔다.
        // **`_owner`를 안 넘긴 곳은 예전 그대로** 스스로 `Hide()`를 부른다(장비칸처럼 사라지지
        // 않는 것들) — 안 넘겼다고 곧바로 닫으면 그쪽이 통째로 못 뜨게 된다.
        // **`ReferenceEquals`로 "안 넘겼다"를 가른다.** 유니티의 `== null`은 파괴된 것에도
        // 참이라, 그걸로 물으면 "안 넘긴 것"과 "죽은 것"이 같아 보인다.
        bool hadOwner = !ReferenceEquals(_owner, null);
        if (hadOwner && (_owner == null || !_owner.gameObject.activeInHierarchy))
        {
            Hide();
            return;
        }

        FollowCursor();
    }

    // 자리 계산은 **`CursorTooltip` 한 벌**이다 — 커서 툴팁이 둘인데 규칙이 갈리면
    // 한쪽만 화면 끝에서 가리키던 것을 덮는다(실제로 `HoverTooltipUI`가 그랬다).
    void FollowCursor() => CursorTooltip.Place(_rt, _canvas, cursorOffset);

    static void SetRow(TextMeshProUGUI txt, string value)
    {
        if (txt == null) return;
        bool show = !string.IsNullOrEmpty(value);
        txt.gameObject.SetActive(show);
        if (show) txt.text = value;
    }

    // 색 자체는 `ItemQualityExt.Color()`가 정본 — 바닥에 떨어진 아이템도 같은 색을 쓴다.
    // 이 메서드는 기존 호출부를 위해 남긴다.
    public static Color QualityColor(ItemQuality q) => q.Color();
}

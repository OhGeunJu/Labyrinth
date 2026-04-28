using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Inventory를 그리드로 시각화. 셀(배경)은 에디터에서 미리 배치, 아이템 아이콘만 동적 생성.
// 부모 RectTransform의 좌상단 (0, sizeDelta.y/2 위치)이 그리드 (0,0).
public class InventoryGridUI : MonoBehaviour
{
    [Header("셀 크기 (셀 1칸의 픽셀 크기)")]
    [Tooltip("미리 배치한 셀들의 크기와 동일해야 함")]
    [SerializeField] float cellSize = 48f;

    [Header("색상")]
    [SerializeField] Color itemBgColor   = new Color(0.25f, 0.35f, 0.25f, 0.85f);
    [SerializeField] Color qtyTextColor  = Color.white;
    [SerializeField] Color nameTextColor = Color.white;

    [Header("텍스트 폰트 (한글용)")]
    [SerializeField] TMP_FontAsset font;

    Inventory _inventory;
    RectTransform _itemsRoot;

    public float CellSize => cellSize;
    public Inventory Inventory => _inventory;

    void Awake() => EnsureItemsRoot();

    void OnDestroy() => Unbind();

    public void Bind(Inventory inv)
    {
        Unbind();
        _inventory = inv;
        if (_inventory != null)
        {
            _inventory.OnChanged += Refresh;
            Refresh();
        }
        else
        {
            ClearItems();
        }
    }

    void Unbind()
    {
        if (_inventory != null) _inventory.OnChanged -= Refresh;
        _inventory = null;
    }

    void EnsureItemsRoot()
    {
        if (_itemsRoot != null) return;
        var existing = transform.Find("_Items");
        if (existing != null)
        {
            _itemsRoot = (RectTransform)existing;
            EnsureIgnoreLayout(_itemsRoot.gameObject);
            return;
        }

        var go = new GameObject("_Items", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _itemsRoot = (RectTransform)go.transform;
        _itemsRoot.anchorMin = new Vector2(0, 1);
        _itemsRoot.anchorMax = new Vector2(0, 1);
        _itemsRoot.pivot = new Vector2(0, 1);
        _itemsRoot.anchoredPosition = Vector2.zero;
        _itemsRoot.sizeDelta = Vector2.zero;
        EnsureIgnoreLayout(go);
    }

    // 부모에 GridLayoutGroup/HorizontalLayoutGroup 같은 게 있어도 _Items가 무시되도록.
    static void EnsureIgnoreLayout(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    // 화면 좌표 → 그리드 셀 좌표 (그리드 영역 밖이면 음수/초과 가능)
    public Vector2Int ScreenToGrid(Vector2 screenPos)
    {
        var rt = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var local))
            return new Vector2Int(int.MinValue, int.MinValue);

        // pivot (0, 1) 가정: local.x 0 = 좌측, local.y 0 = 상단(아래로 가면 음수)
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(-local.y / cellSize);
        return new Vector2Int(x, y);
    }

    public void RefreshItems() => Refresh();

    void Refresh()
    {
        ClearItems();
        if (_inventory == null) return;
        EnsureItemsRoot();
        foreach (var inst in _inventory.Items) CreateItemIcon(inst);
    }

    void ClearItems()
    {
        if (_itemsRoot == null) return;
        for (int i = _itemsRoot.childCount - 1; i >= 0; i--)
            Destroy(_itemsRoot.GetChild(i).gameObject);
    }

    void CreateItemIcon(ItemInstance inst)
    {
        var go = new GameObject($"Item_{inst.Data.ItemName}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_itemsRoot, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = true; // 드래그 가능

        var drag = go.AddComponent<DraggableItem>();
        drag.Init(inst, this);

        if (inst.Data.Icon != null)
        {
            img.sprite = inst.Data.Icon;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
        {
            img.color = itemBgColor;
        }

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        int ew = inst.EffectiveWidth, eh = inst.EffectiveHeight;
        rt.sizeDelta = new Vector2(ew * cellSize - 4, eh * cellSize - 4);
        rt.anchoredPosition = new Vector2(inst.GridX * cellSize + 2, -inst.GridY * cellSize - 2);

        if (inst.Data.Icon == null)
            AddLabel(go.transform, inst.Data.ItemName, 12, TextAlignmentOptions.Center, nameTextColor);

        if (inst.Quantity > 1)
            AddLabel(go.transform, $"x{inst.Quantity}", 14, TextAlignmentOptions.BottomRight, qtyTextColor);
    }

    void AddLabel(Transform parent, string text, float fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        if (font != null) txt.font = font;
        txt.text = text;
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = color;
        txt.raycastTarget = false;

        var rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2, 2);
        rt.offsetMax = new Vector2(-2, -2);
    }
}

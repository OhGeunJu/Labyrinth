using System.Collections.Generic;
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

    [Header("빈 칸 격자")]
    [Tooltip("빈 칸 바탕. 아무것도 안 들었을 때 몇 칸짜리인지 보이게 한다.")]
    [SerializeField] Color emptyCellColor = new Color(0.16f, 0.16f, 0.19f, 0.65f);
    [Tooltip("칸 사이 선")]
    [SerializeField] Color cellLineColor  = new Color(0.34f, 0.34f, 0.40f, 1f);
    [Tooltip("드래그 중 놓일 자리 표시. 된다/안 된다를 색으로 가르지 않는다 — 자리만 보여준다.")]
    [SerializeField] Color previewColor    = new Color(0.85f, 0.85f, 0.95f, 0.30f);

    [Header("아이템 색상")]
    [SerializeField] Color itemBgColor   = new Color(0.25f, 0.35f, 0.25f, 0.85f);
    [SerializeField] Color qtyTextColor  = Color.white;
    [SerializeField] Color nameTextColor = Color.white;

    [Header("텍스트 폰트 (한글용)")]
    [SerializeField] TMP_FontAsset font;

    Inventory _inventory;
    RectTransform _itemsRoot;
    RectTransform _previewRoot;

    public float CellSize => cellSize;
    public Inventory Inventory => _inventory;

    // 외부에서 설정하는 아이템 수신 필터 (null이면 모두 허용)
    public System.Func<ItemData, bool> ReceiveFilter;

    public bool CanReceiveItem(ItemData item) =>
        ReceiveFilter == null || ReceiveFilter(item);

    // 활성 그리드 목록 — 커서 아래 그리드 탐색용
    static readonly List<InventoryGridUI> _activeGrids = new();

    public static InventoryGridUI FindUnderCursor(Vector2 screenPos, Camera uiCam)
    {
        foreach (var grid in _activeGrids)
        {
            if (grid == null || !grid.isActiveAndEnabled) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)grid.transform, screenPos, uiCam))
                return grid;
        }
        return null;
    }

    void Awake() => EnsureItemsRoot();

    void OnEnable()  { if (!_activeGrids.Contains(this)) _activeGrids.Add(this); }
    void OnDisable() => _activeGrids.Remove(this);
    void OnDestroy() { _activeGrids.Remove(this); Unbind(); }

    public void Bind(Inventory inv)
    {
        Unbind();
        _inventory = inv;
        if (_inventory != null)
        {
            _inventory.OnChanged += Refresh;
            BuildCellGrid();
            Refresh();
        }
        else
        {
            ClearItems();
            ClearCellGrid();
        }
    }

    // ── 빈 칸 격자 ────────────────────────────────────────────────────────
    // **아무것도 안 들었을 때 칸이 몇 개인지 보이게 한다.** 예전엔 아이템만 그려서
    // 빈 배낭이 그냥 검은 사각형이었다 — 몇 칸짜리인지, 2×4 대검이 들어갈 자리가 있는지
    // 알 방법이 없었다. 격자 인벤토리는 **빈 칸이 보여야** 자리 계획을 세울 수 있다.
    //
    // 아이템·미리보기 층보다 **먼저** 만들어 뒤에 깔린다(형제 순서 = 그리는 순서).
    RectTransform _cellRoot;
    Vector2Int    _builtFor = new Vector2Int(-1, -1);

    void BuildCellGrid()
    {
        if (_inventory == null) return;
        var size = new Vector2Int(_inventory.Width, _inventory.Height);
        if (_cellRoot != null && _builtFor == size) return;   // 같은 크기면 다시 안 짓는다

        ClearCellGrid();

        var go = new GameObject("_Cells", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();                     // 제일 뒤에 깔린다
        _cellRoot = (RectTransform)go.transform;
        _cellRoot.anchorMin = _cellRoot.anchorMax = _cellRoot.pivot = new Vector2(0, 1);
        _cellRoot.anchoredPosition = Vector2.zero;
        _cellRoot.sizeDelta = Vector2.zero;
        EnsureIgnoreLayout(go);

        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            var cell = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(_cellRoot, false);

            var rt = (RectTransform)cell.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);
            rt.sizeDelta = new Vector2(cellSize, cellSize);

            var img = cell.GetComponent<Image>();
            img.color = emptyCellColor;
            img.raycastTarget = false;   // 드롭 판정은 격자 본체가 한다

            var line = cell.AddComponent<Outline>();
            line.effectColor     = cellLineColor;
            line.effectDistance  = new Vector2(1f, 1f);
            line.useGraphicAlpha = false;   // 옅은 칸 위에도 선이 보이게
        }
        _builtFor = size;
    }

    void ClearCellGrid()
    {
        if (_cellRoot != null) Destroy(_cellRoot.gameObject);
        _cellRoot = null;
        _builtFor = new Vector2Int(-1, -1);
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

    void EnsurePreviewRoot()
    {
        if (_previewRoot != null) return;
        var go = new GameObject("_Preview", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _previewRoot = (RectTransform)go.transform;
        _previewRoot.anchorMin = new Vector2(0, 1);
        _previewRoot.anchorMax = new Vector2(0, 1);
        _previewRoot.pivot     = new Vector2(0, 1);
        _previewRoot.anchoredPosition = Vector2.zero;
        _previewRoot.sizeDelta = Vector2.zero;
        EnsureIgnoreLayout(go);
        // 아이템 아이콘 위에 렌더링되도록
        _previewRoot.SetSiblingIndex(_itemsRoot != null ? _itemsRoot.GetSiblingIndex() + 1 : 0);
    }

    // 부모에 GridLayoutGroup/HorizontalLayoutGroup 같은 게 있어도 _Items가 무시되도록.
    static void EnsureIgnoreLayout(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    // 화면 좌표 → 그리드 셀 좌표 (그리드 영역 밖이면 음수/초과 가능)
    // _itemsRoot 기준으로 변환: 아이콘 배치와 동일한 좌표계 사용
    public Vector2Int ScreenToGrid(Vector2 screenPos)
    {
        EnsureItemsRoot();
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_itemsRoot, screenPos, cam, out var local))
            return new Vector2Int(int.MinValue, int.MinValue);

        // _itemsRoot는 pivot(0,1) 고정: local(0,0) = 좌상단 = 그리드 (0,0) 시작점
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(-local.y / cellSize);
        return new Vector2Int(x, y);
    }

    // `isExternal`은 더 이상 안 쓴다(된다/안 된다를 색으로 가르지 않으므로).
    // 부르는 쪽을 다 고치는 대신 인자만 남겨 뒀다 — 판정이 필요해지면 여기서 되살린다.
    public void ShowPreview(ItemInstance inst, int newX, int newY, bool rotated, bool isExternal = false)
    {
        ClearPreview();
        EnsurePreviewRoot();

        // **된다/안 된다를 색으로 말하지 않는다.** 초록/빨강으로 갈랐더니 두 가지가 문제였다:
        //   · 합칠 수 있는 같은 아이템 위에서도 "빈 칸이 아니다"라는 이유로 빨갛게 떴다
        //     (`CanPlaceItem`은 병합을 모른다)
        //   · 이제 겹치면 **자리를 바꾸므로** 사실상 안 되는 자리가 거의 없다.
        // 미리보기는 **어디에 얼마만큼 놓이는가**만 보여주면 된다.
        var color = previewColor;

        int w = rotated ? inst.Data.Height : inst.Data.Width;
        int h = rotated ? inst.Data.Width  : inst.Data.Height;
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            var cell = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(_previewRoot, false);
            var img = cell.GetComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            var rt = (RectTransform)cell.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot         = new Vector2(0, 1);
            rt.sizeDelta     = new Vector2(cellSize - 2, cellSize - 2);
            rt.anchoredPosition = new Vector2((newX + dx) * cellSize + 1, -(newY + dy) * cellSize - 1);
        }
    }

    public void ClearPreview()
    {
        if (_previewRoot == null) return;
        for (int i = _previewRoot.childCount - 1; i >= 0; i--)
        {
            var child = _previewRoot.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }

    public void RefreshItems() => Refresh();

    void Refresh()
    {
        // 창고 크기가 바뀌었을 수 있다(`Inventory.Resize`) — 같은 크기면 안 다시 짓는다.
        BuildCellGrid();

        ClearItems();
        if (_inventory == null) return;
        EnsureItemsRoot();
        foreach (var inst in _inventory.Items) CreateItemIcon(inst);
    }

    void ClearItems()
    {
        if (_itemsRoot == null) return;
        for (int i = _itemsRoot.childCount - 1; i >= 0; i--)
        {
            var child = _itemsRoot.GetChild(i);
            child.SetParent(null); // 즉시 부모에서 제거해 새 아이콘과 겹치지 않도록
            Destroy(child.gameObject);
        }
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

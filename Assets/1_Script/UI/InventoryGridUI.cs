using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Inventory를 그리드로 시각화. 셀(배경)은 에디터에서 미리 배치, 아이템 아이콘만 동적 생성.
// 부모 RectTransform의 좌상단 (0, sizeDelta.y/2 위치)이 그리드 (0,0).
public class InventoryGridUI : MonoBehaviour
{
    [Header("셀 크기 (셀 1칸의 픽셀 크기)")]
    [Tooltip("미리 배치한 셀들의 크기와 동일해야 함")]
    [SerializeField] float cellSize = DefaultCellSize;

    // **장비칸도 이 칸을 쓴다**(2026-10-18, 사용자: *"인벤토리에 쓰는 칸이랑 동일한 걸 사용해"*) — 격자는
    // 런타임에 붙으므로(`InventoryUI`가 `AddComponent`) 씬 값이 없고 이 기본값이 곧 실제 값이다. 장비칸을
    // 짓는 쪽이 같은 값과 같은 문(`BuildCells`)을 쓴다.
    public const float DefaultCellSize = 48f;
    public static readonly Color DefaultEmptyCell = new(0.16f, 0.16f, 0.19f, 0.65f);
    public static readonly Color DefaultCellLine  = new(0.34f, 0.34f, 0.40f, 1f);

    [Header("빈 칸 격자")]
    [Tooltip("빈 칸 바탕. 아무것도 안 들었을 때 몇 칸짜리인지 보이게 한다.")]
    [SerializeField] Color emptyCellColor = DefaultEmptyCell;
    [Tooltip("칸 사이 선")]
    [SerializeField] Color cellLineColor  = DefaultCellLine;
    [Tooltip("드래그 중 놓일 자리 표시. 된다/안 된다를 색으로 가르지 않는다 — 자리만 보여준다.")]
    [SerializeField] Color previewColor    = new Color(0.85f, 0.85f, 0.95f, 0.30f);

    // **장비칸도 같은 흰색을 쓴다**(`EquipSlotUI.Hint`) — 놓일 자리를 알리는 색이 격자와
    // 장비칸에서 다르면, 같은 조작인데 어디에 올렸느냐로 다른 말처럼 보인다.
    public Color PreviewColor => previewColor;

    [Header("아이템 색상")]
    [SerializeField] Color itemBgColor   = new Color(0.25f, 0.35f, 0.25f, 0.85f);
    [SerializeField] Color qtyTextColor  = Color.white;
    [SerializeField] Color nameTextColor = Color.white;

    [Tooltip("자동 운반에서 뺀 뭉치 표시. 수량과 다른 색이어야 한 눈에 갈린다.")]
    [SerializeField] Color lockTextColor = new Color(0.98f, 0.78f, 0.35f, 1f);

    [Header("텍스트 폰트 (한글용)")]
    [SerializeField] TMP_FontAsset font;

    Inventory _inventory;
    RectTransform _itemsRoot;
    RectTransform _previewRoot;

    public float CellSize => cellSize;
    public Inventory Inventory => _inventory;

    // 런타임에 붙이는 격자(상점 진열)가 **배낭과 같은 칸 크기**를 쓰게 하는 통로.
    // 인스펙터 값이 정본이고 이건 그것을 베끼는 자리다 — 두 격자가 나란히 떠 있는데
    // 칸 크기가 다르면 같은 물건이 다른 크기로 보인다.
    public void CopyCellSize(float size)
    {
        if (size <= 0f || Mathf.Approximately(size, cellSize)) return;
        cellSize = size;
        _builtFor = new Vector2Int(-1, -1);   // 칸 배경을 다시 짓게 한다
    }

    // 외부에서 설정하는 아이템 수신 필터 (null이면 모두 허용)
    public System.Func<ItemData, bool> ReceiveFilter;

    // ── 상점 진열 ─────────────────────────────────────────────────────────────
    //
    // **상점 재고를 배낭과 똑같은 격자로 보여줄 때 켠다**(`MerchantUI`의 칸 보기). 그러면
    // 2×4 갑옷이 자리를 얼마나 먹는지가 사는 순간이 아니라 **사기 전에** 보인다 — 그게 격자
    // 인벤토리를 쓰는 이유이자, 상점만 목록이었을 때 안 보이던 것이다.
    //
    // **끌 수는 있되 공짜로는 못 가져간다.** 한때 아이콘에 `DraggableItem`을 아예 안 붙였는데
    // (`ReadOnly`), 그러면 **좌판을 정리할 길이 없다** — 1×8로 남은 틈에 2×2를 넣으려면 1×1을
    // 옆으로 밀어야 하는데 그걸 할 수가 없었다. 지금은 붙이되 **격자를 건너가는 순간 이동이
    // 아니라 거래**가 된다(`DraggableItem.TryPlace` → `MerchantUI.HandleCarryTrade`).
    public bool IsShop;

    public System.Action<ItemInstance>    OnIconClicked;
    public System.Func<ItemInstance, int> IconPrice;   // 툴팁에 적을 값(진열만)

    // 고른 칸 표시. **색을 칠하지 않고 테두리를 두른다** — 아이콘이 곧 그림이라 위에 색을
    // 얹으면 무슨 물건인지 알아보기 어려워진다.
    static readonly Color SelectColor = new(1f, 0.85f, 0.30f, 1f);

    readonly List<(ItemInstance inst, GameObject go)> _icons = new();

    // 고른 칸에 테두리. 아이템이 아니라 **인스턴스**로 찾는다 — 같은 물건이 여러 뭉치일 수 있다.
    public void HighlightInstance(ItemInstance inst)
    {
        foreach (var (i, go) in _icons)
        {
            if (go == null) continue;

            bool on = i == inst;
            var  mark = go.GetComponent<Outline>();

            // 필요할 때만 만든다 — 칸 수만큼 미리 붙이면 안 쓰는 컴포넌트가 그만큼 는다.
            if (mark == null)
            {
                if (!on) continue;
                mark = go.AddComponent<Outline>();
                mark.effectDistance  = new Vector2(2f, 2f);
                mark.useGraphicAlpha = false;
            }
            mark.effectColor = SelectColor;
            mark.enabled     = on;
        }
    }

    public bool CanReceiveItem(ItemData item) =>
        ReceiveFilter == null || ReceiveFilter(item);

    // **놓일 칸 — 격자 밖으로는 안 나간다.**
    //
    // 미리보기와 실제 놓기가 **이 하나를 같이 쓴다**(격자 아이콘 드래그·장비칸 드래그).
    // 각자 `cursorCell - grab`을 쓰던 시절엔 가장자리에서 음수가 나와 **격자 밖에 밝은 칸이
    // 그려지고** 실제로는 딴 자리에 놓였다 — 보이는 자리와 놓이는 자리가 다른 것이 가장 나쁘다.
    // 안쪽으로 당기면 둘이 같아지고, 덤으로 가장자리에 걸친 드롭이 그냥 된다.
    //
    // `grab`은 **아이템 안에서 집은 칸**이다(장비칸에서 끌어온 것은 그런 게 없어 0,0).
    public Vector2Int PlacementCell(Vector2Int cursorCell, ItemData data, bool rotated, Vector2Int grab)
    {
        int x = cursorCell.x - grab.x;
        int y = cursorCell.y - grab.y;

        if (_inventory == null || data == null) return new Vector2Int(x, y);

        int w = rotated ? data.Height : data.Width;
        int h = rotated ? data.Width  : data.Height;

        return new Vector2Int(Mathf.Clamp(x, 0, Mathf.Max(0, _inventory.Width  - w)),
                              Mathf.Clamp(y, 0, Mathf.Max(0, _inventory.Height - h)));
    }

    // 활성 그리드 목록 — 커서 아래 그리드 탐색용
    static readonly List<InventoryGridUI> _activeGrids = new();

    // **지금 같이 열려 있는 반대편 통.** 우클릭 한 번으로 물건을 건너보내는 데 쓴다
    // (배낭 ↔ 창고·작업대·시체). 없으면 `null` — 그때 우클릭은 다른 뜻이 된다(장비 착용).
    //
    // "무엇이 반대편인가"를 부르는 쪽마다 따지게 하면(창고 패널이 배낭을 알고, 배낭이 창고를
    // 알고…) 통이 하나 늘 때마다 짝이 제곱으로 는다. **열려 있는 격자 목록이 이미 있으므로**
    // 그중 내가 아닌 것을 고르면 된다.
    public static InventoryGridUI FindCounterpart(InventoryGridUI mine, ItemData item)
    {
        if (mine == null || mine.Inventory == null || item == null) return null;

        foreach (var grid in _activeGrids)
        {
            if (grid == null || grid == mine || !grid.isActiveAndEnabled) continue;
            if (grid.IsShop) continue;                // 진열대는 그냥 받는 통이 아니다(거래다)
            if (grid.Inventory == null || grid.Inventory == mine.Inventory) continue;
            if (!grid.CanReceiveItem(item)) continue;
            return grid;
        }
        return null;
    }

    // **진열대도 여기 걸린다** — 끌어다 놓을 수 있는 곳이기 때문이다(놓으면 거래가 된다).
    // 커서 아래 격자. **겹쳐 있으면 맨 위에 그려진 것**을 고른다.
    //
    // 전엔 `_activeGrids`에서 **먼저 걸리는 것**을 돌려줬다. 창이 겹치지 않을 때는 티가 안
    // 났지만, 창고 창을 배낭 위에 얹어 놓으면 **눈에 보이는 창이 아니라 등록 순서가 이겨서**
    // 배낭에 놓으려던 물건이 창고로 들어갔다(등록 순서는 창을 연 순서라 사람이 알 길이 없다).
    //
    // uGUI의 그리기 순서는 **계층의 형제 순서**다 — 뒤에 있는 형제가 위에 그려진다. 그래서
    // 공통 조상까지 거슬러 올라가 거기서의 형제 번호를 비교하면 "어느 것이 위인가"가 나온다.
    public static InventoryGridUI FindUnderCursor(Vector2 screenPos, Camera uiCam)
    {
        InventoryGridUI best = null;

        foreach (var grid in _activeGrids)
        {
            if (grid == null || !grid.isActiveAndEnabled) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)grid.transform, screenPos, uiCam)) continue;

            if (best == null || DrawsAbove(grid.transform, best.transform)) best = grid;
        }
        return best;
    }

    // a가 b보다 위에 그려지나. 공통 조상에서의 형제 번호로 잰다(뒤가 위).
    static bool DrawsAbove(Transform a, Transform b)
    {
        var ca = Chain(a);
        var cb = Chain(b);

        int n = Mathf.Min(ca.Count, cb.Count);
        for (int i = 0; i < n; i++)
        {
            if (ca[i] == cb[i]) continue;
            return ca[i].GetSiblingIndex() > cb[i].GetSiblingIndex();
        }

        // 한쪽이 다른 쪽의 자식이다 — 자식이 위에 그려진다.
        return ca.Count > cb.Count;
    }

    // 루트 → 자기까지의 경로.
    static List<Transform> Chain(Transform t)
    {
        var list = new List<Transform>();
        for (var cur = t; cur != null; cur = cur.parent) list.Add(cur);
        list.Reverse();
        return list;
    }

    // ── 얹어 놓고 Q/E로 돌린다 ────────────────────────────────────────────────
    //
    // 회전은 원래 **끌고 있는 동안에만** 됐다(손에 든 동안). 시스템은 있는데 **꺼낼
    // 길이 없어서** 1×3을 3×1로 눕히려면 일단 집어 들어야 했다 — 자리를 보고 나서 돌리고 싶은
    // 것이 보통인데 순서가 거꾸로였다.
    //
    // **커서 아래를 그때그때 찾는다 — 무엇에 얹혀 있는지 기억하지 않는다.** 돌리면 격자가
    // 아이콘을 다시 만드는데, 얹힘을 상태로 들고 있으면 그 아이콘이 파괴되면서 상태가 끊겨
    // **두 번째 Q가 안 먹는다**(`OnPointerExit`는 파괴될 때 안 온다 — `ItemInfoUI`가 겪은 그것).
    // 누를 때 한 번 찾으면 그 함정이 아예 없다.
    //
    // 폴링은 **커서 아래 격자 하나만** 일한다(열려 있어야 두어 개다).
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || _inventory == null) return;
        if (!kb.qKey.wasPressedThisFrame && !kb.eKey.wasPressedThisFrame) return;

        // 손에 들었으면 그쪽이 받는다(`DraggableItem`) — 둘 다 돌면 두 번 돈다.
        if (DraggableItem.CarriedInstance != null) return;

        // **건설 모드가 켜져 있으면 Q/E는 그쪽 것이다.** 같은 키를 같은 뜻으로 쓰는 대신
        // 치러야 하는 값이다 — 둘이 같이 떠 있으면 고스트와 배낭이 한 번에 돈다.
        if (BuildModeManager.IsActive) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                   ? canvas.worldCamera : null;

        Vector2 pos = mouse.position.ReadValue();
        if (FindUnderCursor(pos, cam) != this) return;

        var cell = ScreenToGrid(pos);
        if (cell.x == int.MinValue) return;

        var inst = _inventory.GetAt(cell.x, cell.y);
        if (inst != null) _inventory.RotateItem(inst);
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

        BuildCells(_cellRoot, size.x, size.y, cellSize, emptyCellColor, cellLineColor);
        _builtFor = size;
    }

    // **빈 칸 눈금 — 한 벌이다.** 배낭 격자와 장비칸(`EquipSlotUI`의 `_Cells`, 에디터에서 깐다)이 같이 쓴다.
    public static void BuildCells(RectTransform root, int cols, int rows, float cellSize, Color fill, Color lineColor)
    {
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            var cell = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(root, false);

            var rt = (RectTransform)cell.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);
            rt.sizeDelta = new Vector2(cellSize, cellSize);

            var img = cell.GetComponent<Image>();
            img.color = fill;
            img.raycastTarget = false;   // 드롭 판정은 격자 본체(장비칸이면 칸 본체)가 한다

            var line = cell.AddComponent<Outline>();
            line.effectColor     = lineColor;
            line.effectDistance  = new Vector2(1f, 1f);
            line.useGraphicAlpha = false;   // 옅은 칸 위에도 선이 보이게
        }
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
        foreach (var inst in _inventory.Items)
        {
            // **손에 든 것은 안 그린다.** 그 아이콘은 이미 캔버스 위에서 커서를 따라가고 있다 —
            // 안 거르면 뭉치를 쪼개는 순간 같은 것이 격자에도 하나 더 생겨 둘로 보인다.
            // (드래그 중에 격자를 다시 그리는 것은 **쪼갠 나머지를 바로 보여 주기 위해서**다.)
            if (inst == DraggableItem.CarriedInstance) continue;
            CreateItemIcon(inst);
        }
    }

    void ClearItems()
    {
        _icons.Clear();
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

        // **진열도 끌 수 있다** — 다만 격자를 건너가면 이동이 아니라 거래다(위 `IsShop` 주석).
        var drag = go.AddComponent<DraggableItem>();
        drag.Init(inst, this);

        _icons.Add((inst, go));

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

        Decorate(go.transform, inst);
    }

    // **아이콘에 붙는 글자는 여기 한 곳에서 붙인다** — 이름·개수·잠금.
    //
    // 손에 든 아이콘도 이걸 쓴다(`DraggableItem.CarryInstead`). 밀어낸 물건으로 갈아 끼울 때
    // 글자를 손으로 만지다가 **그림 없는 물건의 이름을 개수로 덮어썼다** — 개수가 1이면
    // 빈 글자라, 이름이 통째로 사라져 무엇을 들고 있는지 알 수가 없었다.
    public void Decorate(Transform icon, ItemInstance inst)
    {
        // 갈아 끼울 때를 위해 먼저 비운다(새로 만든 아이콘은 어차피 자식이 없다).
        for (int i = icon.childCount - 1; i >= 0; i--) Destroy(icon.GetChild(i).gameObject);

        if (inst.Data.Icon == null)
            AddLabel(icon, inst.Data.ItemName, 12, TextAlignmentOptions.Center, nameTextColor);

        if (inst.Quantity > 1)
            AddLabel(icon, $"x{inst.Quantity}", 14, TextAlignmentOptions.BottomRight, qtyTextColor);

        // 잠긴 뭉치는 **글자로** 말한다. 자물쇠 기호를 쓰면 폰트 폴백에 기대게 되고
        // (`docs/editor_creators.md` "폰트 체인"), 색만 바꾸면 아이콘 제 색과 섞여 안 보인다.
        // 수량이 오른쪽 아래에 있으므로 왼쪽 위 — 서로 겹치지 않는다.
        if (inst.Locked)
            AddLabel(icon, "잠금", 10, TextAlignmentOptions.TopLeft, lockTextColor);

        // **쓰다 만 것이 남은 양**(`ItemInstance.Left`) — 오른쪽 위. 뭉치면 그중 열린 하나의 것이다.
        if (inst.IsPartial)
            AddLabel(icon, $"{Mathf.CeilToInt(inst.Left * 100f)}%", 10, TextAlignmentOptions.TopRight, qtyTextColor);
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

using System;
using System.Collections.Generic;
using UnityEngine;

// 타르코프식 그리드 인벤토리.
// - W×H 셀 배열. 각 아이템은 자신의 너비/높이만큼 셀 점유.
// - 같은 ItemData는 MaxStack 한도까지 한 인스턴스에 누적, 넘으면 새 인스턴스 생성.
// - 회전 지원 (1×3 ↔ 3×1).
public class Inventory : MonoBehaviour
{
    [SerializeField] int width  = 8;
    [SerializeField] int height = 10;

    ItemInstance[,] _cells;
    readonly List<ItemInstance> _items = new();

    public int Width  => width;
    public int Height => height;
    public IReadOnlyList<ItemInstance> Items => _items;

    // 차지한 **칸** 수. 격자 인벤토리라 한계는 개수가 아니라 칸이다 —
    // 큰 아이템 하나가 여러 칸을 먹으므로 `Items.Count`로는 남은 자리를 알 수 없다.
    public int UsedCellCount
    {
        get
        {
            int n = 0;
            foreach (var inst in _items)
                n += inst.EffectiveWidth * inst.EffectiveHeight;
            return n;
        }
    }
    public event Action OnChanged;

    // 인스턴스의 `Quantity`를 **밖에서 직접 고친 뒤** 부른다(드래그로 스택을 합칠 때).
    // 이벤트는 선언한 클래스에서만 발행할 수 있어서 통로가 필요하다 —
    // 안 울리면 창고 집계와 다른 패널이 옛 수량을 계속 보여준다.
    public void NotifyChanged() => OnChanged?.Invoke();

    // 격자 크기를 다시 잡는다. **비어 있을 때만** — 아이템이 든 채로 줄이면 밖으로 밀려난 것이 증발한다.
    // 런타임에 `AddComponent`로 붙이는 쪽(작업대 산출물 등)은 `Awake` 전에 값을 넣을 방법이 없어 필요하다.
    public bool Resize(int w, int h)
    {
        if (_items.Count > 0) return false;
        width  = Mathf.Max(1, w);
        height = Mathf.Max(1, h);
        _cells = new ItemInstance[width, height];
        OnChanged?.Invoke();
        return true;
    }

    void Awake()
    {
        _cells = new ItemInstance[width, height];
    }

    // ── 배치 ──────────────────────────────────────────────────────────────────

    // 빈 자리를 자동 탐색하여 추가. 일부만 들어가면 그만큼만. 반환: 실제 추가 수량.
    public int TryAdd(ItemData data, int amount, ItemQuality quality = ItemQuality.Common)
    {
        if (data == null || amount <= 0) return 0;
        int remaining = amount;

        // 1) 기존 인스턴스에 스택 채우기 (같은 등급끼리만)
        //    **잠근 뭉치도 받는다.** 잠금은 "이 자리를 비워 두지 마라"는 뜻이지 봉인이 아니다 —
        //    비상 식량을 잠가 뒀는데 주워 온 것이 옆 칸에 따로 쌓이면, 잠글 때마다 칸이
        //    쪼개져 배낭이 금세 조각난다. 합쳐진 것은 같이 잠긴 채로 남는다(그게 의도다).
        foreach (var inst in _items)
        {
            if (inst.Data != data || inst.Quality != quality) continue;
            int canAdd = data.MaxStack - inst.Quantity;
            if (canAdd <= 0) continue;
            int take = Math.Min(remaining, canAdd);
            inst.Quantity += take;
            remaining -= take;
            if (remaining <= 0) break;
        }

        // 2) 새 인스턴스를 빈 자리에 배치
        while (remaining > 0)
        {
            int batch = Math.Min(remaining, data.MaxStack);
            var slot = FindEmptySlot(data.Width, data.Height, out bool rotated);
            if (slot == null) break;

            var inst = new ItemInstance(data, batch, slot.Value.x, slot.Value.y, rotated, quality);
            PlaceCells(inst);
            _items.Add(inst);
            remaining -= batch;
        }

        if (remaining < amount) OnChanged?.Invoke();
        return amount - remaining;
    }

    // **넣어 보지 않고** 들어갈 자리가 있는지만 잰다. 운반이 "받아 줄 곳"을 고를 때 쓴다 —
    // 만적인 곳을 골라 놓고 도착해서 못 넣으면 짐을 바닥에 떨구게 되고, 그 짐이 다시
    // 같은 곳으로 가는 일감이 되어 폰이 줍고 떨구기를 반복한다.
    //
    // 기존 스택의 여유부터 세고, 모자라면 빈 칸에 몇 뭉치가 더 들어가는지 센다.
    // `TryAdd`와 **같은 순서**로 세야 답이 어긋나지 않는다.
    //
    // **등급도 `TryAdd`와 같이 본다.** 스택은 같은 등급끼리만 합쳐지므로(위 1단계),
    // 등급을 안 보고 여유를 세면 `일반 빵 30/99`가 든 만적 창고가 `희귀 빵`도 받는다고
    // 답한다 — 폰이 거기까지 가서 못 넣고, 되돌린 짐이 다시 같은 일감이 되어 왕복한다.
    public bool CanFit(ItemData data, int amount = 1, ItemQuality quality = ItemQuality.Common)
    {
        if (data == null || amount <= 0) return false;

        int remaining = amount;

        foreach (var inst in _items)
        {
            // `TryAdd`와 같은 조건 — 등급만 본다(잠긴 뭉치도 받으므로 여유에 센다).
            if (inst.Data != data || inst.Quality != quality) continue;
            remaining -= Math.Max(0, data.MaxStack - inst.Quantity);
            if (remaining <= 0) return true;
        }

        // 빈 칸 수로 환산 — 실제 배치를 흉내 내지 않고 넓이만 본다.
        // 격자 조각남 때문에 실제보다 후하게 나올 수 있지만, 이 판정은
        // "여기로 보내도 되나"의 관문이라 **모자란 쪽으로 틀리는 것**이 더 나쁘다
        // (자리가 있는데 없다고 하면 운반이 영영 안 뜬다).
        int cellsPerStack = Math.Max(1, data.Width * data.Height);
        int freeCells     = width * height - UsedCellCount;
        int stacksThatFit = freeCells / cellsPerStack;

        return remaining <= stacksThatFit * data.MaxStack;
    }

    // 특정 위치에 강제 배치. 충돌하면 false.
    //
    // **등급·잠금을 받는다.** 예전엔 `ItemData`와 개수만 받아서, 다른 격자로 끌어다 놓으면
    // 새 인스턴스가 기본값(일반·안 잠김)으로 태어났다 — 희귀 철검을 창고로 드래그하면
    // 조용히 일반이 됐다. 옮기는 건 **같은 물건**이므로 딸린 것도 같이 가야 한다.
    public bool TryAddAt(ItemData data, int amount, int x, int y, bool rotated,
                         ItemQuality quality = ItemQuality.Common, bool locked = false)
    {
        if (data == null || amount <= 0) return false;
        int w = rotated ? data.Height : data.Width;
        int h = rotated ? data.Width  : data.Height;
        if (!CanPlace(x, y, w, h, null)) return false;

        var inst = new ItemInstance(data, Math.Min(amount, data.MaxStack), x, y, rotated, quality)
        {
            Locked = locked,
        };
        PlaceCells(inst);
        _items.Add(inst);
        OnChanged?.Invoke();
        return true;
    }

    // 외부 인벤토리 출신 아이템이 이 인벤토리 특정 위치에 들어갈 수 있는지 확인
    public bool CanPlaceExtern(ItemData data, int x, int y, bool rotated)
    {
        if (data == null) return false;
        int w = rotated ? data.Height : data.Width;
        int h = rotated ? data.Width  : data.Height;
        return CanPlace(x, y, w, h, null);
    }

    public bool CanPlaceItem(ItemInstance inst, int x, int y, bool rotated)
    {
        if (inst == null || !_items.Contains(inst)) return false;
        int w = rotated ? inst.Data.Height : inst.Data.Width;
        int h = rotated ? inst.Data.Width  : inst.Data.Height;
        return CanPlace(x, y, w, h, inst);
    }

    // 두 아이템의 **자리를 맞바꾼다.** 겹치는 곳에 놓으려 할 때 그냥 실패시키면
    // 좁은 배낭에서 배치를 바꿀 방법이 없다 — 한 칸씩 빼내 옮겨야 한다.
    //
    // **둘 다 서로의 자리에 들어갈 때만** 바꾼다. 크기가 다르면(2×4 ↔ 1×1) 한쪽이
    // 안 들어갈 수 있는데, 그때 반만 옮기면 아이템이 사라지거나 겹친다.
    public bool SwapItems(ItemInstance a, ItemInstance b)
    {
        if (a == null || b == null || a == b) return false;
        if (!_items.Contains(a) || !_items.Contains(b)) return false;

        int ax = a.GridX, ay = a.GridY; bool ar = a.Rotated;
        int bx = b.GridX, by = b.GridY; bool br = b.Rotated;

        // 서로를 비켜 놓고 판정해야 한다 — 안 그러면 상대가 자기 자리를 막고 있다고 나온다.
        ClearCells(a); ClearCells(b);

        int aw = ar ? a.Data.Height : a.Data.Width,  ah = ar ? a.Data.Width : a.Data.Height;
        int bw = br ? b.Data.Height : b.Data.Width,  bh = br ? b.Data.Width : b.Data.Height;

        bool ok = CanPlace(bx, by, aw, ah, a) && CanPlace(ax, ay, bw, bh, b);
        if (!ok)
        {
            PlaceCells(a); PlaceCells(b);   // 원래대로 되돌린다
            return false;
        }

        a.GridX = bx; a.GridY = by;
        b.GridX = ax; b.GridY = ay;
        PlaceCells(a); PlaceCells(b);
        OnChanged?.Invoke();
        return true;
    }

    public bool MoveItem(ItemInstance inst, int newX, int newY, bool newRotated)
    {
        if (inst == null || !_items.Contains(inst)) return false;
        int w = newRotated ? inst.Data.Height : inst.Data.Width;
        int h = newRotated ? inst.Data.Width  : inst.Data.Height;
        if (!CanPlace(newX, newY, w, h, inst)) return false;

        ClearCells(inst);
        inst.GridX = newX; inst.GridY = newY; inst.Rotated = newRotated;
        PlaceCells(inst);
        OnChanged?.Invoke();
        return true;
    }

    // ── 제거 ──────────────────────────────────────────────────────────────────

    public void RemoveInstance(ItemInstance inst)
    {
        if (!_items.Remove(inst)) return;
        ClearCells(inst);
        OnChanged?.Invoke();
    }

    // 지정 인스턴스에서 1개만 제거. 수량 0이 되면 인스턴스 자체 삭제.
    public bool RemoveOne(ItemInstance inst)
    {
        if (inst == null || !_items.Contains(inst)) return false;
        inst.Quantity--;
        if (inst.Quantity <= 0)
        {
            ClearCells(inst);
            _items.Remove(inst);
        }
        OnChanged?.Invoke();
        return true;
    }

    // ── 제거: 걸러내는 축이 둘 ──────────────────────────────────────────────
    //  · 잠금  — 자동(운반·제작)은 잠근 뭉치를 못 건드린다. 손으로 하는 일은 다 건드린다.
    //  · 등급  — 운반은 **집은 그 등급 그대로** 내려놓아야 한다. 등급을 안 가리고 빼면
    //            희귀 철검을 집어 일반으로 넣게 된다(스택이 등급별이라 그대로 새 등급이 된다).
    // 네 조합을 따로 쓰면 같은 루프가 넷이 된다 — 핵심 하나에 얇은 이름을 붙인다.
    int RemoveCore(ItemData data, int amount, bool skipLocked, bool matchQuality, ItemQuality quality)
    {
        if (data == null || amount <= 0) return 0;
        int remaining = amount;
        for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var inst = _items[i];
            if (inst.Data != data) continue;
            if (skipLocked   && inst.Locked)          continue;
            if (matchQuality && inst.Quality != quality) continue;

            int take = Math.Min(remaining, inst.Quantity);
            inst.Quantity -= take;
            remaining -= take;
            if (inst.Quantity <= 0)
            {
                ClearCells(inst);
                _items.RemoveAt(i);
            }
        }
        if (remaining < amount) OnChanged?.Invoke();
        return amount - remaining;
    }

    // 지정 ItemData를 amount만큼 제거(등급·잠금 무관). 반환: 실제 제거 수량.
    public int Remove(ItemData data, int amount)
        => RemoveCore(data, amount, false, false, default);

    // 그 등급에서만 제거. 운반이 집고 내려놓을 때 쓴다.
    public int Remove(ItemData data, int amount, ItemQuality quality)
        => RemoveCore(data, amount, false, true, quality);

    // 잠기지 않은 뭉치에서만 제거. **자동 전용** — 손으로 하는 일은 `Remove`를 쓴다.
    public int RemoveUnlocked(ItemData data, int amount)
        => RemoveCore(data, amount, true, false, default);

    public int RemoveUnlocked(ItemData data, int amount, ItemQuality quality)
        => RemoveCore(data, amount, true, true, quality);

    public void Clear()
    {
        _items.Clear();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _cells[x, y] = null;
        OnChanged?.Invoke();
    }

    // ── 조회 ──────────────────────────────────────────────────────────────────

    public ItemInstance GetAt(int x, int y) =>
        InBounds(x, y) ? _cells[x, y] : null;

    int CountCore(ItemData data, bool skipLocked, bool matchQuality, ItemQuality quality)
    {
        int total = 0;
        foreach (var inst in _items)
        {
            if (inst.Data != data) continue;
            if (skipLocked   && inst.Locked)          continue;
            if (matchQuality && inst.Quality != quality) continue;
            total += inst.Quantity;
        }
        return total;
    }

    public int GetTotalQuantity(ItemData data)
        => CountCore(data, false, false, default);

    public int GetTotalQuantity(ItemData data, ItemQuality quality)
        => CountCore(data, false, true, quality);

    // 잠기지 않은 것만 센다. **자동이 물어보는 수량** — 전체를 세면 잠근 다섯 개까지
    // 내가겠다고 일감을 걸고, 도착해서는 잠기지 않은 것만 빼 수량이 어긋난다.
    public int GetUnlockedQuantity(ItemData data)
        => CountCore(data, true, false, default);

    public int GetUnlockedQuantity(ItemData data, ItemQuality quality)
        => CountCore(data, true, true, quality);

    public float GetTotalWeight()
    {
        float total = 0f;
        foreach (var inst in _items)
            total += inst.Data.Weight * inst.Quantity;
        return total;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────────

    bool CanPlace(int x, int y, int w, int h, ItemInstance ignore)
    {
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int cx = x + dx, cy = y + dy;
            if (!InBounds(cx, cy)) return false;
            var occ = _cells[cx, cy];
            if (occ != null && occ != ignore) return false;
        }
        return true;
    }

    void PlaceCells(ItemInstance inst)
    {
        int w = inst.EffectiveWidth, h = inst.EffectiveHeight;
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
            _cells[inst.GridX + dx, inst.GridY + dy] = inst;
    }

    void ClearCells(ItemInstance inst)
    {
        int w = inst.EffectiveWidth, h = inst.EffectiveHeight;
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
            _cells[inst.GridX + dx, inst.GridY + dy] = null;
    }

    Vector2Int? FindEmptySlot(int w, int h, out bool rotated)
    {
        rotated = false;
        for (int y = 0; y <= height - h; y++)
        for (int x = 0; x <= width  - w; x++)
            if (CanPlace(x, y, w, h, null)) return new Vector2Int(x, y);

        if (w == h) return null;

        rotated = true;
        for (int y = 0; y <= height - w; y++)
        for (int x = 0; x <= width  - h; x++)
            if (CanPlace(x, y, h, w, null)) return new Vector2Int(x, y);

        rotated = false;
        return null;
    }

    bool InBounds(int x, int y) =>
        x >= 0 && x < width && y >= 0 && y < height;
}

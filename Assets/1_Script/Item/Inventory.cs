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
    public event Action OnChanged;

    void Awake()
    {
        _cells = new ItemInstance[width, height];
    }

    // ── 배치 ──────────────────────────────────────────────────────────────────

    // 빈 자리를 자동 탐색하여 추가. 일부만 들어가면 그만큼만. 반환: 실제 추가 수량.
    public int TryAdd(ItemData data, int amount, ItemQuality quality = ItemQuality.Normal)
    {
        if (data == null || amount <= 0) return 0;
        int remaining = amount;

        // 1) 기존 인스턴스에 스택 채우기 (같은 등급끼리만)
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

    // 특정 위치에 강제 배치. 충돌하면 false.
    public bool TryAddAt(ItemData data, int amount, int x, int y, bool rotated)
    {
        if (data == null || amount <= 0) return false;
        int w = rotated ? data.Height : data.Width;
        int h = rotated ? data.Width  : data.Height;
        if (!CanPlace(x, y, w, h, null)) return false;

        var inst = new ItemInstance(data, Math.Min(amount, data.MaxStack), x, y, rotated);
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

    // 지정 ItemData를 amount만큼 제거. 부족하면 가능한 만큼만. 반환: 실제 제거 수량.
    public int Remove(ItemData data, int amount)
    {
        if (data == null || amount <= 0) return 0;
        int remaining = amount;
        for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var inst = _items[i];
            if (inst.Data != data) continue;
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

    // ResourceType별 일괄 제거
    public int RemoveByType(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        int remaining = amount;
        for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var inst = _items[i];
            if (inst.Data is not ResourceItemData r || r.ResourceType != type) continue;
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

    public int GetTotalQuantity(ItemData data)
    {
        int total = 0;
        foreach (var inst in _items)
            if (inst.Data == data) total += inst.Quantity;
        return total;
    }

    public int GetTotalByType(ResourceType type)
    {
        int total = 0;
        foreach (var inst in _items)
            if (inst.Data is ResourceItemData r && r.ResourceType == type) total += inst.Quantity;
        return total;
    }

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

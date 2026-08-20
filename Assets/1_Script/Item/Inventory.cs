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

    // **등급을 갖지 않는 물건은 등급을 지운다**(`ItemData.HasQuality` — 장비만 참).
    //
    // 스택은 같은 등급끼리만 합쳐지므로, 저작 실수 하나로(상인 목록에 "희귀 붕대", 전리품
    // 표에 "고급 빵") **같은 물건이 두 뭉치로 갈라진다** — 창고가 조각나고, 창고에서 꺼내
    // 넣는 코드가 등급을 안 넘기면 그 뭉치는 영영 안 쓰인다.
    //
    // **들어오는 문에서 한 번 누른다.** 읽는 쪽마다 "이 물건은 등급이 있나"를 묻게 하면
    // 반드시 한 곳이 빠진다.
    static ItemQuality Normalize(ItemData data, ItemQuality quality)
        => data != null && data.HasQuality ? quality : ItemQuality.Common;

    // 빈 자리를 자동 탐색하여 추가. 일부만 들어가면 그만큼만. 반환: 실제 추가 수량.
    public int TryAdd(ItemData data, int amount, ItemQuality quality = ItemQuality.Common)
    {
        if (data == null || amount <= 0) return 0;
        quality = Normalize(data, quality);

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
        quality = Normalize(data, quality);   // `TryAdd`와 같은 답을 해야 한다

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
        quality = Normalize(data, quality);   // `TryAdd`와 같은 문이어야 한다 — 아래 참고

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

    // 그 자리에 **놓는다. 하나가 걸리면 그것을 밀어낸다**(밀려난 것은 손에 들린다).
    //
    // 격자에서 하는 일은 결국 이것 하나다 — 빈자리면 놓고, 뭔가 걸리면 그것과 바뀐다.
    // 예전엔 `MoveItem` 실패 → `SwapItems` 두 단계였고, 그 사이에서 규칙이 갈렸다.
    //
    // ── 규칙 셋 ──────────────────────────────────────────────────────────────
    //  · 발자국이 **비었으면** 그냥 놓는다.
    //  · 걸리는 것이 **딱 하나면** 그것을 **통에서 빼서 돌려준다**(`displaced`). 부르는 쪽이
    //    손에 들려 준다 — 자리를 맞바꾸지 않는 이유는, 크기가 다르면 상대가 내 자리에
    //    못 들어가는 일이 흔하고 그때마다 "왜 안 바뀌지"가 되기 때문이다. 손에 들면
    //    **어디에 놓을지는 사람이 정한다.**
    //  · **둘 이상이면 아무것도 안 한다** — 어느 쪽을 밀어낼지 말할 수 없고, 하나만 밀어내면
    //    나머지와 겹친 채로 남는다.
    //
    // `inst`가 이 통에 **없어도 된다** — 손에 든 것·다른 통에서 건너온 것은 그대로 꽂는다.
    //
    // ── 예전 `SwapItems`가 물건을 겹쳐 놓던 자리 ──────────────────────────────
    //
    // 그쪽은 **둘을 서로 비켜 놓고 각각** 판정했다(`CanPlace(bx,by,…,a) && CanPlace(ax,ay,…,b)`).
    // 서로의 자리를 막고 있다고 나오는 것을 피하려던 것인데, 그 바람에 **A의 새 발자국과
    // B의 새 발자국이 겹치는지를 아무도 안 봤다** — 크기가 다른 둘(3×1 검 ↔ 2×3 옷)을 비비면
    // 둘 다 통과해 **겹친 채로 놓였다.** 밀어내는 쪽은 통에서 아예 빠지므로 그 구멍이 없다.
    //
    // 그리고 상대를 **커서 칸이 아니라 발자국 전체**에서 찾는다 — 3×1로 돌린 검은 커서 칸이
    // 비어 있어도 옆 칸에서 남의 물건에 걸친다(그게 "가끔 겹친다"의 정체였다).
    public bool TryPlaceOverAt(ItemInstance inst, int x, int y, bool rotated,
                               out ItemInstance displaced)
    {
        displaced = null;
        if (inst == null || inst.Data == null) return false;

        int w = rotated ? inst.Data.Height : inst.Data.Width;
        int h = rotated ? inst.Data.Width  : inst.Data.Height;

        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
            if (!InBounds(x + dx, y + dy)) return false;

        // 발자국에 걸리는 것 — **딱 하나일 때만** 밀어낸다.
        ItemInstance other = null;
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            var occ = _cells[x + dx, y + dy];
            if (occ == null || occ == inst) continue;
            if (other == null)  other = occ;
            else if (other != occ) return false;   // 둘 이상 — 아무것도 안 한다
        }

        if (other != null)
        {
            ClearCells(other);
            _items.Remove(other);
            displaced = other;
        }

        bool mine = _items.Contains(inst);
        if (mine) ClearCells(inst);

        inst.GridX = x; inst.GridY = y; inst.Rotated = rotated;
        PlaceCells(inst);
        if (!mine) _items.Add(inst);

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

    // ── 뭉치 쪼개기 ───────────────────────────────────────────────────────────
    //
    // **왜 필요한가.** 스택은 같은 것끼리 저절로 합쳐지는데(`TryAdd`), 한 번 합쳐지면 도로
    // 가를 길이 없었다. 그래서 "빵 20개 중 5개만 저 폰에게" 같은 일을 하려면 창고를 거쳐
    // 하나씩 옮겨야 했다 — 상인에게 일부만 팔거나, 원정대에 나눠 싣거나, 바닥에 조금만
    // 내려놓는 것이 전부 같은 벽에 막혔다.
    //
    // 갈라 나온 **새 뭉치를 돌려준다**(자리가 없으면 `null`이고 **아무것도 안 바뀐다** —
    // 반쯤 갈라진 상태로 두면 개수가 조용히 새는 것과 같다).
    // (격자를 다시 그리게 하는 `NotifyChanged`는 이미 위에 있다 — `notify: false`로 쪼갠 뒤
    //  드래그가 끝나면 그것으로 갚는다.)

    // `notify: false` — **끌고 있는 동안에는 알리지 않는다.** `InventoryGridUI`가 `OnChanged`에서
    // 아이콘을 전부 다시 만드는데, 그러면 **지금 끌고 있는 아이콘이 파괴되어** 드롭이 통째로
    // 사라진다(놓을 곳에 아무 일도 안 일어난다). 드래그가 끝난 뒤 `NotifyChanged`로 갚는다.
    public ItemInstance SplitInstance(ItemInstance inst, int take, bool notify = true)
    {
        if (inst == null || inst.Data == null || !_items.Contains(inst)) return null;

        // 전부를 떼는 것은 쪼개는 것이 아니다(원래 자리가 빈 뭉치로 남는다).
        if (take <= 0 || take >= inst.Quantity) return null;

        var slot = FindEmptySlot(inst.Data.Width, inst.Data.Height, out bool rotated);
        if (slot == null) return null;

        inst.Quantity -= take;

        var half = new ItemInstance(inst.Data, take, slot.Value.x, slot.Value.y, rotated, inst.Quality)
        {
            // **잠금은 뭉치가 아니라 그 물건에 걸린 뜻이다** — 쪼갠 쪽만 자동 처분 대상이
            // 되면 "잠갔는데 절반이 실려 갔다"가 된다.
            Locked = inst.Locked,
        };

        PlaceCells(half);
        _items.Add(half);
        if (notify) OnChanged?.Invoke();
        return half;
    }

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

    // 제자리에서 돌린다(1×3 ↔ 3×1). 돌렸으면 true.
    //
    // **격자를 벗어나면 안쪽으로 당겨서 돌린다.** 가장자리에 붙은 1×3을 3×1로 돌리면 오른쪽이
    // 격자 밖으로 나가는데, 그때 그냥 실패시키면 **가장자리 물건은 영영 못 돌린다** — 자리가
    // 좁아 돌리려는 것인데 좁다고 거절하는 꼴이다. 당겨도 다른 것과 겹치면 그때는 안 돌아간다.
    //
    // 정사각은 돌릴 것이 없다(돌려도 같은 칸을 먹는다).
    public bool RotateItem(ItemInstance inst)
    {
        if (inst == null || !_items.Contains(inst)) return false;
        if (inst.Data.Width == inst.Data.Height) return false;

        bool rot = !inst.Rotated;
        int  w   = rot ? inst.Data.Height : inst.Data.Width;
        int  h   = rot ? inst.Data.Width  : inst.Data.Height;

        int x = Mathf.Clamp(inst.GridX, 0, Mathf.Max(0, width  - w));
        int y = Mathf.Clamp(inst.GridY, 0, Mathf.Max(0, height - h));

        return MoveItem(inst, x, y, rot);   // `CanPlace`가 자기 자신은 안 막는다
    }

    // ── 정리 ──────────────────────────────────────────────────────────────────
    //
    // **칸을 다시 채워 넣는다** — 같은 것끼리 합치고, 큰 것부터 위에서부터 빈틈없이.
    //
    // 격자 인벤토리는 쓰다 보면 반드시 조각난다: 가운데 것을 팔면 그 자리가 구멍으로 남고,
    // 칸 수로는 넉넉한데 **2×4 갑옷이 들어갈 연속된 자리가 없어** 거절당한다. 그때 손으로
    // 물건을 하나씩 끌어 옮기는 것은 놀이가 아니라 잡일이다.
    //
    // **큰 것부터 놓는다**(면적 내림차순). 작은 것을 먼저 흩뿌리면 큰 것이 들어갈 연속 공간이
    // 남지 않는다 — 같은 물건들을 한 덩어리로 모으는 효과도 곁따라 온다.
    //
    // **같은 물건·같은 등급은 합친다.** 30/99짜리 뭉치가 셋이면 칸을 셋 먹는데, 그건 조각남
    // 그 자체다. 잠금은 **하나라도 잠겨 있으면 합친 뭉치가 잠긴다** — 잠금은 "이건 건드리지
    // 마라"라서, 합치다 풀리면 자동 운반이 조용히 집어 간다.
    //
    // 자리를 못 찾은 것은 **그대로 둔다**(있을 수 없는 일이지만, 잃는 것보다 낫다).
    // **다 못 넣으면 아무것도 안 바꾼다**(`false`). 첫 자리부터 큰 것부터 채우는 방식은
    // 원래 들어가 있던 배치를 다시 만든다는 보장이 없다 — 그때 **반쯤 정리된 격자를 남기는
    // 것이 가장 나쁘다**(물건이 겹치거나 사라진다).
    public bool Compact()
    {
        if (_items.Count == 0) return true;

        var items = new List<ItemInstance>(_items);

        // 1) 같은 물건·같은 등급 합치기
        for (int i = 0; i < items.Count; i++)
        {
            var a = items[i];
            for (int j = items.Count - 1; j > i; j--)
            {
                var b = items[j];
                if (b.Data != a.Data || b.Quality != a.Quality) continue;

                int room = a.Data.MaxStack - a.Quantity;
                if (room <= 0) continue;

                int move = Math.Min(room, b.Quantity);
                a.Quantity += move;
                b.Quantity -= move;
                if (move > 0 && b.Locked) a.Locked = true;

                if (b.Quantity <= 0) items.RemoveAt(j);
            }
        }

        // 2) 큰 것부터 — 같은 크기면 이름순(정리할 때마다 자리가 바뀌면 눈이 매번 다시 찾는다)
        items.Sort((x, y) =>
        {
            int ax = x.Data.Width * x.Data.Height, ay = y.Data.Width * y.Data.Height;
            if (ax != ay) return ay.CompareTo(ax);
            return string.CompareOrdinal(x.Data.ItemName, y.Data.ItemName);
        });

        // 3) 빈 격자에 다시 놓는다 — **되돌릴 수 있게 옛 자리를 적어 둔다.**
        var before = new List<(ItemInstance inst, int x, int y, bool rot)>(_items.Count);
        foreach (var inst in _items) before.Add((inst, inst.GridX, inst.GridY, inst.Rotated));

        Array.Clear(_cells, 0, _cells.Length);
        _items.Clear();

        foreach (var inst in items)
        {
            inst.Rotated = false;
            var slot = FindEmptySlot(inst.Data.Width, inst.Data.Height, out bool rotated);
            if (slot == null)
            {
                // **자리를 못 찾았다.** 한때 "원래 자리로 되돌린다(그 자리는 아직 비어 있다)"고
                // 여기서 `PlaceCells`를 불렀는데, **그 말이 참이 아니다**: 격자를 이미 비우고
                // (`Array.Clear`) 큰 것부터 다시 채우는 중이라 그 칸은 남이 가져갔을 수 있다.
                // 게다가 `inst.Rotated`를 방금 꺼서 **발자국 크기가 달라졌다** — 돌려 놓았던
                // 물건은 옛 좌표에서 범위를 넘고(`IndexOutOfRangeException`), 안 넘으면
                // **남의 칸을 말없이 덮어써** 두 물건이 같은 칸을 갖는다.
                //
                // 못 넣으면 **돌려서 한 번 더 본다.** 그래도 안 되면 이 정리는 성립하지 않는
                // 것이므로 **통째로 되돌린다**(아래) — 반쯤 정리된 격자를 남기는 것이 가장 나쁘다.
                var slotR = FindEmptySlot(inst.Data.Height, inst.Data.Width, out _);
                if (slotR == null) return Restore(before);

                inst.GridX   = slotR.Value.x;
                inst.GridY   = slotR.Value.y;
                inst.Rotated = true;
                PlaceCells(inst);
                _items.Add(inst);
                continue;
            }

            inst.GridX   = slot.Value.x;
            inst.GridY   = slot.Value.y;
            inst.Rotated = rotated;
            PlaceCells(inst);
            _items.Add(inst);
        }

        OnChanged?.Invoke();
        return true;
    }

    // 정리가 실패했을 때 옛 배치로 되돌린다. **합치기(1단계)는 안 되돌린다** — 같은 물건을
    // 한 뭉치로 모으는 것은 자리를 늘리는 일이라 실패의 원인이 아니고, 되돌리면 오히려
    // 조각이 늘어난다. 되돌리는 것은 **자리**뿐이다.
    bool Restore(List<(ItemInstance inst, int x, int y, bool rot)> before)
    {
        Array.Clear(_cells, 0, _cells.Length);
        _items.Clear();

        foreach (var (inst, x, y, rot) in before)
        {
            if (inst.Quantity <= 0) continue;   // 1단계에서 다른 뭉치로 합쳐졌다

            inst.GridX = x; inst.GridY = y; inst.Rotated = rot;
            PlaceCells(inst);
            _items.Add(inst);
        }

        OnChanged?.Invoke();
        return false;
    }

    // **이 물건·이 등급의 뭉치 하나.** 방금 넣은 것을 되돌릴 때, *어느 뭉치에 들어갔나*를
    // 알아야 남의 뭉치를 빼지 않는다(`Shop.TrySell`의 롤백).
    public ItemInstance FindStack(ItemData data, ItemQuality quality)
    {
        foreach (var inst in _items)
            if (inst.Data == data && inst.Quality == quality && inst.Quantity > 0) return inst;
        return null;
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

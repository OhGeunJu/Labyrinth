using System.Collections.Generic;
using UnityEngine;

// **배낭에 든 잉여 물건을 창고에 부린다.**
//
// 왜 필요한가: 상인에게 산 재료는 폰 배낭으로 들어가는데, 거기서 창고로 가는 길이 없었다.
// 운반 일감은 자원 노드·작업대 산출물·바닥에 떨어진 것에서만 나오기 때문이다.
// 그래서 산 철광석은 **그 폰이 직접 쓰지 않는 한 죽은 재고**였다 — 제작 화면에는
// 창고 기준 수량이 뜨므로 "분명히 샀는데 못 만든다"가 된다.
//
// 다른 운반과 달리 **남이 대신 해 줄 수 없다** — 짐은 그 폰의 배낭에 있다.
// 그래서 `CanAssignTo`가 주인만 통과시킨다(큐에는 올라가되 임자가 정해져 있다).
public class UnloadJob : JobBase
{
    public override JobCategory Category => JobCategory.Unload;

    enum Phase { GoToStorage, Done }

    readonly UnitBrain _owner;
    Storage     _storage;
    ItemData    _item;
    ItemQuality _quality;   // 집은 등급 그대로 내려놓는다 (`HaulJob`과 같은 규칙)
    int         _amount;

    public UnloadJob(UnitBrain owner, ItemData item, ItemQuality quality, int amount, Storage storage)
    {
        _owner   = owner;
        _item    = item;
        _quality = quality;
        _amount  = amount;
        _storage = storage;
        // 창고 자기 칸이 아니라 **폰이 서는 자리**(`WorkSpot`) — 창고가 칸을 막게 되면
        // 자기 칸은 설 수 없는 자리가 된다.
        TileCoord = MapManager.Instance != null
                  ? WorkSpot.TileFor(storage)
                  : default;
        Priority = 1;   // 남이 시킨 일보다 뒤 — 할 일이 없을 때 짐을 정리하는 것이다
    }

    // 자기 짐만 부린다. 남의 배낭은 건드릴 수 없다.
    public override bool CanAssignTo(UnitBrain unit) => unit == _owner;

    // 상자에 넣는 일이라 **손만 닿으면 된다** — 거기 서 있어야 할 이유가 없다.
    public override float Reach => HandReach;

    // 그 사이 창고가 차거나 짐을 이미 썼거나 **플레이어가 잠갔을** 수 있다.
    public override bool IsReady =>
        _owner != null && _item != null && _storage != null &&
        _owner.Inventory != null && _owner.Inventory.GetUnlockedQuantity(_item, _quality) > 0 &&
        _storage.HasRoomFor(_item, 1, _quality);

    protected override void Execute(UnitBrain unit)
    {
        Status = JobStatus.Done;
        if (_owner == null || _storage == null) return;

        // 맡은 것부터 부린다. 도착 시점에 다시 잰다 — 오는 동안 쓰였거나 창고가 찼거나
        // **잠겼을** 수 있다. 오가는 사이에 잠근 것은 반드시 존중한다.
        Deposit(unit, _item, _quality, _amount);

        // **온 김에 이 창고가 받아 줄 나머지도 전부 부린다.**
        //
        // 전에는 맡은 `(아이템, 등급)` 하나만 내려놓고 끝냈다. 그래서 배낭에 다섯 종류가
        // 들어 있으면 **같은 창고를 다섯 번 왕복했다** — 걸어가는 것이 이 일의 값의 전부인데
        // 그걸 다섯 번 치른 셈이다. 이미 창고 앞에 서 있으므로 나머지를 부리는 값은 0이다.
        //
        // 맡는 단위를 하나로 둔 것은 **어디로 갈지**를 정하기 위해서지(창고마다 필터가 달라
        // 물건마다 갈 곳이 다르다) 거기서 하나만 내려놓으라는 뜻이 아니었다.
        // 이 창고가 안 받는 것은 그대로 남고, 다음 `UpdateUnload`가 그것을 받아 줄 창고로 건다.
        SweepRest(unit);
    }

    // 배낭을 훑어 이 창고가 받아 주는 것을 전부 부린다.
    //
    // **먼저 훑어 목록을 만들고 나서 뺀다** — 빼면서 훑으면 `Inventory.Items`가 그 자리에서
    // 바뀌어 순회가 깨진다.
    void SweepRest(UnitBrain unit)
    {
        var inv = unit.Inventory;
        if (inv == null) return;

        var pairs = new List<(ItemData item, ItemQuality quality)>();
        foreach (var inst in inv.Items)
        {
            if (inst.Data == null || inst.Locked) continue;      // 잠근 것은 짐이 아니다
            var key = (inst.Data, inst.Quality);
            if (!pairs.Contains(key)) pairs.Add(key);
        }

        foreach (var (item, quality) in pairs)
            Deposit(unit, item, quality, inv.GetUnlockedQuantity(item, quality));
    }

    // 한 종류를 부린다. **필터는 `Storage.TryAdd`가 본다** — `Inventory`를 직접 만지면
    // 창고가 안 받기로 한 물건이 그대로 들어가 분류가 무너진다.
    void Deposit(UnitBrain unit, ItemData item, ItemQuality quality, int want)
    {
        if (item == null || want <= 0) return;

        int have = Mathf.Min(want, unit.Inventory.GetUnlockedQuantity(item, quality));
        if (have <= 0) return;

        int removed = unit.Inventory.RemoveUnlocked(item, have, quality);
        if (removed <= 0) return;

        int stored = _storage.TryAdd(item, removed, quality);

        // 못 넣은 만큼은 **배낭으로 되돌린다.** 방금 뺀 자리라 반드시 들어간다 —
        // 여기서 흘리면 산 물건이 조용히 사라진다.
        int leftover = removed - stored;
        if (leftover > 0) unit.Inventory.TryAdd(item, leftover, quality);
    }
}

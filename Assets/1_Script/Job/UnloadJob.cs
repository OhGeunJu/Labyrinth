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
        TileCoord = MapManager.Instance != null
                  ? MapManager.Instance.WorldToTile(storage.transform.position)
                  : default;
        Priority = 1;   // 남이 시킨 일보다 뒤 — 할 일이 없을 때 짐을 정리하는 것이다
    }

    // 자기 짐만 부린다. 남의 배낭은 건드릴 수 없다.
    public override bool CanAssignTo(UnitBrain unit) => unit == _owner;

    // 그 사이 창고가 차거나 짐을 이미 썼거나 **플레이어가 잠갔을** 수 있다.
    public override bool IsReady =>
        _owner != null && _item != null && _storage != null &&
        _owner.Inventory != null && _owner.Inventory.GetUnlockedQuantity(_item, _quality) > 0 &&
        _storage.HasRoomFor(_item, 1, _quality);

    protected override void Execute(UnitBrain unit)
    {
        Status = JobStatus.Done;
        if (_owner == null || _item == null || _storage == null) return;

        // 도착 시점에 다시 잰다 — 오는 동안 쓰였거나 창고가 찼거나 **잠겼을** 수 있다.
        // 오가는 사이에 잠근 것은 반드시 존중한다: 세는 것도 빼는 것도 잠금을 지나친다.
        int have = unit.Inventory.GetUnlockedQuantity(_item, _quality);
        int want = Mathf.Min(_amount, have);
        if (want <= 0) return;

        int removed = unit.Inventory.RemoveUnlocked(_item, want, _quality);
        if (removed <= 0) return;

        int stored = _storage.Inventory.TryAdd(_item, removed, _quality);

        // 못 넣은 만큼은 **배낭으로 되돌린다.** 방금 뺀 자리라 반드시 들어간다 —
        // 여기서 흘리면 산 물건이 조용히 사라진다.
        int leftover = removed - stored;
        if (leftover > 0) unit.Inventory.TryAdd(_item, leftover, _quality);
    }
}

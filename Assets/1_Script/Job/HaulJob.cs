using System;
using UnityEngine;

public class HaulJob : JobBase
{
    public override JobCategory Category => JobCategory.Hauling;

    enum Phase { GoToSource, GoToStorage }

    // 출처는 **`IHaulSource`** — 자원 노드든 작업대 산출물이든 운반 규칙은 같다.
    IHaulSource  _source;
    ItemData     _itemData;
    // **집은 등급 그대로 내려놓는다.** 스택은 등급별이라, 안 가리고 빼서 안 가리고 넣으면
    // 희귀 철검이 창고에서 일반이 된다 — 물건이 바뀌는 셈이다.
    ItemQuality  _quality;
    // **실제로 실은 수량.** 창고에 부릴 때 이 값을 쓴다. 출처에서 집어 올린 뒤에 정해지므로
    // 그 전에는 0이다 — 등록 시점의 수량을 여기 미리 넣어 두면 그게 상한 노릇을 한다.
    int          _amount;
    int          _claim;       // 출처에서 아직 맡고 있는 수량 (돌려주지 않은 만큼)
    Storage      _storage;
    Phase        _phase = Phase.GoToSource;
    Action<int>  _release;     // 맡은 것 중 이만큼 놓는다고 출처에 알린다
    UnitBrain    _carrier;     // 짐을 들고 있는 폰 (취소 시 내려놓기용)

    // `amount` = 등록 시점에 맡아 둔 재고. **이 일감이 실어 갈 양이 아니라**, 같은 재고에
    // 일감이 겹쳐 등록되지 않게 세는 장부다. 누가 올지 모르므로 미배정 재고를 통째로 맡고,
    // 폰이 정해지는 순간(`OnReserved`) 그 폰이 못 질 만큼을 놓아 다음 일감이 되게 한다.
    // 실어 갈 양은 도착해서 정한다(`Execute`).
    // **임계값도 같이 들고 간다**(2026-08-25). 발행할 때만 재면 **도착했을 때는 안 재게 되고**,
    // 그러면 플레이어가 걸어 둔 "이만큼 쌓이면 나른다"가 조용히 풀린다 — 아래 `Execute` 참조.
    int _threshold;

    public HaulJob(IHaulSource source, ItemData itemData, ItemQuality quality, int amount,
                   Storage storage, int threshold = 1, Action<int> release = null)
    {
        _source    = source;
        _itemData  = itemData;
        _quality   = quality;
        _claim     = amount;
        _storage   = storage;
        _threshold = Mathf.Max(1, threshold);
        _release   = release;
        TileCoord = source.TileCoord;
        Priority  = 5;
    }

    // **집고 내려놓는 일은 손만 닿으면 된다** — 출처(바위·나무·작업대 산출물)도 목적지(창고)도
    // 폰이 그 자리에 서 있어야 할 이유가 없다. 좁게 잡으면 같은 창고를 여럿이 쓸 때 서로
    // 비켜서느라 오래 걸린다(자원 노드를 둘러서서 캐게 한 것과 같은 축).
    public override float Reach => HandReach;

    // 질 자리가 없는 폰에게는 주지 않는다. 배정해 놓고 도착해서 못 싣는 것보다
    // 애초에 안 주는 편이 낫다 — 헛걸음이 없고, 일감은 큐에 남아 여유 있는 폰이 가져간다.
    // **무게와 격자를 둘 다 본다** — 무게가 남아도 격자가 꽉 차면 한 개도 못 싣는다.
    public override bool CanAssignTo(UnitBrain unit)
        => unit != null && unit.Inventory != null
        && CarryCapacity.RoomFor(unit, _itemData) > 0
        && unit.Inventory.CanFit(_itemData, 1, _quality);

    // 누가 할지 정해졌으니 **맡아 둔 것 중 이 폰이 못 질 만큼을 놓는다** — 놓은 만큼은
    // 곧바로 다음 일감이 되어 다른 폰이 붙는다. (여기서 정하는 건 "남은 재고에 일감을
    // 더 낼까"이지 이 폰이 실을 양이 아니다. 실을 양은 도착해서 다시 잰다.)
    protected override void OnReserved(UnitBrain unit)
    {
        int room = CarryCapacity.RoomFor(unit, _itemData);
        if (room < _claim) Release(_claim - room);
    }

    protected override void Execute(UnitBrain unit)
    {
        _carrier = unit;

        if (_phase == Phase.GoToSource)
        {
            int available = _source != null
                          ? _source.Inventory.GetTotalQuantity(_itemData, _quality) : 0;

            // **맡은 몫(`_claim`)은 상한이 아니다.** 그것은 등록 시점의 재고를 찍은 값이라,
            // 임계값 10에 일감이 뜨면 10이 박힌다 — 그걸 상한으로 쓰면 27개를 질 수 있는 폰도
            // 영영 10개씩만 나른다(실제로 그랬다). 몫은 "이 재고에 일감을 몇 개나 낼까"를
            // 세는 장부일 뿐이고, **실어 가는 양은 지금 있는 재고와 폰의 적재가 정한다.**
            //
            // 맡은 것보다 많이 실으면 다른 일감의 몫을 먹지만, 그 일감도 도착해서 다시 재므로
            // 있는 만큼만 가져간다. 넘친 장부는 `HaulDispatcher`가 재고를 넘는 맡음을 깎아 맞춘다.
            // **도착해서 임계값을 다시 잰다.**
            //
            // 발행할 때만 재면 플레이어가 걸어 둔 *"이만큼 쌓이면 나른다"*가 **여럿이 붙는 순간
            // 풀린다**: 재고 30·임계값 10이면 일감이 여럿 나가는데, 힘센 폰이 먼저 28을 쓸어
            // 가면 **뒤에 도착한 폰이 남은 2를 그냥 집어 간다.** 그 폰에게는 "있는 만큼"이
            // 전부였고, 임계값은 아무도 다시 안 봤다.
            //
            // 여기서 물러서면 잔량은 출처에 그대로 남는다 — `HaulDispatcher`도 임계값 아래로는
            // 일감을 안 내므로 **두 곳의 답이 같아진다.** 헛걸음 한 번이 값이지만, 그러라고
            // 걸어 둔 숫자다.
            if (available < _threshold)
            {
                Status = JobStatus.Done;
                ReleaseAll();
                return;
            }

            int want = Mathf.Min(available, CarryCapacity.RoomFor(unit, _itemData));
            if (want <= 0)
            {
                Status = JobStatus.Done;
                ReleaseAll();
                return;
            }

            int toTake = _source.Inventory.Remove(_itemData, want, _quality);
            int added  = unit.Inventory.TryAdd(_itemData, toTake, _quality);
            if (added < toTake)
            {
                // 인벤에 다 안 들어감 — 나머지는 노드에 되돌림 (증발 방지)
                int back = _source.Inventory.TryAdd(_itemData, toTake - added, _quality);
                int lost = toTake - added - back;
                if (lost > 0)
                    DroppedItem.Spawn(_itemData, lost, _source.transform.position, _quality);
            }
            _amount = added;

            // **여기서 맡은 몫을 전부 놓는다.** 맡음(claim)이 지키는 건 "출처에 아직 있는데
            // 내가 가져갈 재고"뿐이다 — 배낭에 실은 순간 그 수량은 출처에서 이미 빠졌으므로,
            // 계속 맡고 있으면 그만큼이 이중으로 빠져 남은 재고에 일감이 안 붙는다.
            ReleaseAll();

            if (_amount <= 0)
            {
                Status = JobStatus.Done;
                return;
            }

            // **목적지가 사라졌으면 그냥 들고 있는다.** 이 일감은 받아 줄 창고가 있어야만
            // 발행되므로(`HaulDispatcher`), 여기서 없다는 건 폰이 걸어오는 사이에 철거됐다는
            // 뜻이다 — 이미 실은 것을 도로 내려놓을 이유가 없다. 짐은 배낭에 남고, 창고가
            // 생기면 짐내림(`UnloadJob`)이 알아서 옮긴다.
            //
            // (`transform`을 만지기 전에 반드시 확인할 것 — 파괴된 컴포넌트를 건드리면
            //  `MissingReferenceException`이 `UnitBrain.Update`까지 올라가 BT가 통째로 멎는다.)
            _storage = Storage.Rebind(_storage, unit.transform.position, _itemData, _amount, _quality);
            if (_storage == null) { Status = JobStatus.Done; return; }

            _phase    = Phase.GoToStorage;
            TileCoord = WorkSpot.TileFor(_storage);   // 창고 자기 칸이 아니라 폰이 서는 자리
        }
        else
        {
            // 실제 꺼낸 수량 기준으로 입고. 못 들어간 잔량은 다른 창고 → 배낭 → 바닥 순으로
            // 보존한다 — 그 사슬은 `Storage.Deliver` 한 곳이 돈다(운반·수확이 같은 것을 쓴다).
            int removed = unit.Inventory.Remove(_itemData, _amount, _quality);
            Storage.Deliver(_storage, _itemData, removed, _quality, unit, unit.transform.position);

            Status = JobStatus.Done;
            ReleaseAll();
        }
    }

    // 맡은 몫을 출처에 돌려준다. **맡고 있는 양을 넘겨 놓지 않는다** — 두 번 놓으면
    // 출처의 장부가 음수로 새고, 그러면 있지도 않은 재고에 일감이 붙는다.
    void Release(int amount)
    {
        if (amount <= 0 || _claim <= 0) return;
        int give = Mathf.Min(amount, _claim);
        _claim -= give;
        _release?.Invoke(give);
    }

    void ReleaseAll() => Release(_claim);

    // 취소 시 들고 있던 짐을 그 자리에 내려놓는다.
    // 안 그러면 운반 도중 취소된 물건이 폰 인벤토리에 갇힌다(이동 명령 취소가 잦아지며 드러난 경로).
    // 드롭되면 GroundHaulJob이 자동 등록돼 다른 폰이 창고로 옮긴다.
    protected override void OnCancelled()
    {
        ReleaseAll();

        if (_phase != Phase.GoToStorage || _carrier == null || _itemData == null || _amount <= 0) return;

        int removed = _carrier.Inventory.Remove(_itemData, _amount, _quality);
        if (removed > 0)
            DroppedItem.Spawn(_itemData, removed, _carrier.transform.position, _quality);
    }
}

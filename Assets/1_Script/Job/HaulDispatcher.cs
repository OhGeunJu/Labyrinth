using System;
using System.Collections.Generic;
using UnityEngine;

// 운반 일감 발행기 — 출처(자원 노드·작업대 산출물)가 재고를 일감으로 내놓는다.
//
// **두 숫자를 헷갈리지 않는다.**
//   - 임계값(`haulThreshold`) = "이만큼 쌓이면 나른다". 발행 **조건**이지 실어 가는 양이 아니다.
//   - 실어 가는 양 = 그 폰이 질 수 있는 무게(`CarryCapacity`). 폰마다 다르다.
// 예전엔 등록 시점의 재고로 몫을 못 박아서, 임계값이 10이면 힘센 폰도 영영 10개씩만 날랐다.
//
// **그래서 몫을 미리 쪼개지 않는다.** 미배정 재고를 통째로 맡긴 일감 하나를 내고,
// 폰이 정해지는 순간(`HaulJob.OnReserved`) 그 폰이 질 만큼만 남기고 **나머지를 즉시 놓는다** —
// 놓인 만큼은 바로 다음 일감이 되어 다른 폰이 붙는다. 몇 명이 붙을지를 미리 맞힐 필요가 없다.
//
// **장부는 `(아이템, 등급)` 단위다.** 작업대는 같은 철검을 일반·희귀로 섞어 쌓는데, 아이템만
// 세면 희귀 셋과 일반 다섯이 여덟 개 한 덩어리가 된다 — 그 일감이 등급을 안 가리고 집으면
// 희귀가 일반으로 창고에 들어간다. 등급까지 키에 넣어야 집은 것을 그대로 내려놓을 수 있다.
public class HaulDispatcher
{
    // 장부 열쇠. 같은 아이템이라도 등급이 다르면 다른 몫이다.
    readonly struct Lot : IEquatable<Lot>
    {
        public readonly ItemData    Item;
        public readonly ItemQuality Quality;

        public Lot(ItemData item, ItemQuality quality) { Item = item; Quality = quality; }

        public bool Equals(Lot o) => Item == o.Item && Quality == o.Quality;
        public override bool Equals(object o) => o is Lot l && Equals(l);
        public override int GetHashCode() =>
            (Item != null ? Item.GetHashCode() : 0) * 397 ^ (int)Quality;
    }

    readonly IHaulSource _source;
    readonly Dictionary<Lot, int> _claimed = new();
    readonly List<Lot> _lots = new();

    // 몫을 놓았을 때 곧바로 다시 발행하려면 조건을 기억해야 한다(주기 점검을 2초 기다리지 않게).
    readonly Dictionary<Lot, int> _thresholds = new();

    // 재진입 가드. **삼키지 않고 미뤄 둔다** — 발행 중에 들어온 요청을 그냥 버리면
    // 그 재고는 다음 주기 점검(2초)까지 일감 없이 놀고, 그 사이 폰들은 할 일이 없다.
    bool _dispatching;
    readonly List<Lot> _deferred = new();

    // 미룬 것을 소화하는 동안 또 미뤄질 수 있다. 한 번 발행하면 그 몫은 맡음으로 잡혀
    // `free`가 0이 되므로 실제로는 몇 바퀴 안에 잦아들지만, 데이터가 이상할 때
    // 프레임을 통째로 잡아먹지 않게 상한을 둔다.
    const int MaxDrainPasses = 16;

    public HaulDispatcher(IHaulSource source) => _source = source;

    // threshold: 이만큼 쌓여야 나른다. 인벤토리에 있는 **모든 (아이템, 등급)** 을 훑는다.
    public void Dispatch(int threshold)
    {
        if (!Usable()) return;

        CollectLots(null);
        for (int i = 0; i < _lots.Count; i++)
            DispatchItem(_lots[i], Mathf.Max(1, threshold));
    }

    // 특정 아이템만 발행 (자원 노드처럼 산출이 정해진 곳에서 쓴다).
    // 그 아이템도 **등급이 여럿일 수 있으므로** 등급별로 나눠 낸다.
    public void Dispatch(ItemData item, int threshold)
    {
        if (item == null || !Usable()) return;

        CollectLots(item);
        for (int i = 0; i < _lots.Count; i++)
            DispatchItem(_lots[i], Mathf.Max(1, threshold));
    }

    bool Usable() =>
        _source != null && _source.Inventory != null && JobManager.Instance != null;

    // 종류를 먼저 모은다 — 등록 도중 인벤토리가 바뀌면 순회가 깨진다.
    // `only`가 있으면 그 아이템의 등급들만.
    void CollectLots(ItemData only)
    {
        _lots.Clear();
        foreach (var inst in _source.Inventory.Items)
        {
            if (inst.Data == null) continue;
            if (only != null && inst.Data != only) continue;

            var lot = new Lot(inst.Data, inst.Quality);
            if (!_lots.Contains(lot)) _lots.Add(lot);
        }
    }

    void DispatchItem(Lot lot, int threshold)
    {
        _thresholds[lot] = threshold;

        if (_dispatching)
        {
            if (!_deferred.Contains(lot)) _deferred.Add(lot);
            return;
        }

        _dispatching = true;
        try
        {
            DispatchOnce(lot, threshold);

            // 발행 도중 들어온 요청을 여기서 소화한다 — 놓인 몫이 곧바로 다음 일감이 되는 길.
            for (int pass = 0; pass < MaxDrainPasses && _deferred.Count > 0; pass++)
            {
                var next = _deferred[0];
                _deferred.RemoveAt(0);
                if (_thresholds.TryGetValue(next, out int t)) DispatchOnce(next, t);
            }
        }
        finally
        {
            _deferred.Clear();
            _dispatching = false;
        }
    }

    void DispatchOnce(Lot lot, int threshold)
    {
        int total = _source.Inventory.GetTotalQuantity(lot.Item, lot.Quality);

        _claimed.TryGetValue(lot, out int claimed);
        // 밖에서 빠져나갔을 수 있다(패널에서 직접 꺼내기 등) — 재고보다 많이 맡고 있으면 줄인다.
        if (claimed > total) { claimed = total; _claimed[lot] = claimed; }

        int free = total - claimed;
        if (free < threshold) return;

        // 내보낼 양(`free`)만큼 받아 줄 곳을 먼저 찾는다 — `FindNearest`가 통째로 받는 곳이
        // 없으면 한 개라도 받는 곳으로 물러서므로, 넉넉히 물어도 일감이 사라지지 않는다.
        var storage = Storage.FindNearest(_source.transform.position, lot.Item, free, lot.Quality);
        if (storage == null) return;      // 받아 줄 창고가 없으면 그대로 쌓아 둔다

        _claimed[lot] = claimed + free;

        var forLot = lot;   // 클로저 캡처용 — 반복 변수를 그대로 넘기지 않는다
        JobManager.Instance.RegisterJob(
            new HaulJob(_source, lot.Item, lot.Quality, free, storage, n => Release(forLot, n)));
    }

    // 일감이 "이만큼은 내 몫이 아니다"라고 돌려준 것. 곧바로 다시 발행해 본다 —
    // 폰이 배정되며 놓은 잔량이 다음 폰의 일감이 되는 길이 이것이다.
    void Release(Lot lot, int amount)
    {
        if (lot.Item == null || amount <= 0) return;
        if (!_claimed.TryGetValue(lot, out int claimed)) return;

        claimed -= amount;
        if (claimed <= 0) _claimed.Remove(lot);
        else              _claimed[lot] = claimed;

        if (_thresholds.TryGetValue(lot, out int threshold))
            DispatchItem(lot, threshold);
    }
}

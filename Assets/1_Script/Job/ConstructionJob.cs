using UnityEngine;

// 자재를 챙겨 설계도에 납품하는 2-phase 작업.
//
// **자재는 창고에만 있는 것이 아니다.** 예전엔 `Storage`만 봤다. 그래서 둘이 따라왔다:
//  · 상점에서 목재를 사 온 폰이 **그 목재로 집을 못 지었다**(배낭은 안 셌다).
//  · 창고가 하나도 없으면 **첫 창고를 못 지었다** — 창고도 청사진이라, 자재를 가지러 갈
//    창고가 있어야 창고를 짓는 순환이 된다. 새 정착지가 정확히 그 상태로 시작한다.
//    즉시 배치가 28종이던 동안엔 그 길로 우회됐지만, 13종을 청사진으로 돌리면서 드러났다.
//
// 재료를 어디서 세고 어디서 꺼내는지는 이미 `MaterialPool`이 정해 두었다(배낭 먼저, 그다음
// 창고) — `SeedJob`·`CraftJob`·`ResearchJob`이 그 축을 쓰는데 건설만 밖에 있었다.
public class ConstructionJob : JobBase
{
    public override JobCategory Category => JobCategory.Construction;

    enum Phase { Fetch, Deliver }

    readonly BuildingBlueprint _blueprint;
    readonly ItemData          _material;
    readonly int               _amount;

    Storage _source;
    int     _carried;
    Phase   _phase = Phase.Deliver;

    public ConstructionJob(BuildingBlueprint blueprint, ItemData material, int amount)
    {
        _blueprint = blueprint;
        _material  = material;
        _amount    = amount;
        Priority   = 3;

        // **건물 자리가 아니라 폰이 서는 자리다.** 전에는 청사진 중심 칸을 그대로 목표로 삼았는데,
        // 청사진은 칸을 안 막으므로 그 칸에 설 수 있었다 — 폰이 3×2 건물 **한복판**에 서서 짓고,
        // 완성되는 순간 발밑이 통행 불가가 되어 그대로 갇혔다.
        SetSite(blueprint);
    }

    // **단계마다 다르다.** 창고에서 자재를 챙길 때는 손만 닿으면 되지만, 청사진 앞에서는
    // 저작된 자리에 서야 한다 — 짓는 폰이 두 칸 떨어져 허공에 대고 망치질하면 안 된다.
    public override float Reach => _phase == Phase.Fetch ? HandReach : StandReach;

    // 일감이 준비됐나 — 등록 시점엔 누가 할지 모르므로 넓게 본다(`MaterialPool` 규약).
    public override bool IsReady => MaterialPool.AvailableNear(_material, new Vector3(TileCoord.x + 0.5f, 0f, TileCoord.y + 0.5f)) > 0;

    // 이 폰이 할 수 있나 — 창고 + **그 폰의** 배낭. 게이트가 없으면 자재에 닿지 못하는 폰이
    // 일감을 집어 `OnStart`에서 `Done`으로 끝내 버려, 예약 자체가 조용히 사라진다.
    public override bool CanAssignTo(UnitBrain unit) => MaterialPool.Available(_material, unit) > 0;

    protected override void OnStart()
    {
        if (_blueprint == null || _material == null) { Fail(); return; }

        // **배낭에 있으면 그대로 지고 간다** — 손에 든 목재를 두고 창고에 들를 이유가 없다
        // (`TreatJob`이 약을 다루는 것과 같은 규약).
        var builder = ReservedBy;
        int inHand   = builder != null && builder.Inventory != null
                     ? builder.Inventory.GetUnlockedQuantity(_material) : 0;
        if (inHand > 0)
        {
            _carried = Mathf.Min(_amount, inHand);
            BeginDeliver();
            return;
        }

        // **짓는 사람 기준으로 가장 가까운 창고.** 청사진 기준이 아니다 — 걷는 것은 폰이고,
        // 지금 서 있는 자리에서 재야 왕복이 줄어든다(못 물으면 청사진 옆에서 잰다).
        var origin = builder != null ? builder.transform.position : _blueprint.transform.position;
        _source = Storage.FindStorageWithItem(_material, origin);
        if (_source == null)
        {
            // 어디에도 없다 — 청사진이 `OnJobEnded`에서 다시 건다(취소가 아니라 대기).
            Debug.Log($"[ConstructionJob] {_material.ItemName}을(를) 어디서도 못 찾음 — 대기");
            Fail();
            return;
        }

        // 창고도 자기 칸은 통행 불가다 — `WorkSpot`이 그 앞의 설 수 있는 칸을 준다.
        _phase    = Phase.Fetch;
        TileCoord = WorkSpot.TileFor(_source);
        Debug.Log($"[ConstructionJob] 건설 자재 수거 시작 — {_material.ItemName} ×{_amount}");
    }

    void BeginDeliver()
    {
        _phase    = Phase.Deliver;
        TileCoord = WorkSpot.TileFor(_blueprint);   // 발자국 밖 — 다 지어져도 갇히지 않는다
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_blueprint == null) { Fail(); return; }

        if (_phase == Phase.Fetch)
        {
            // 그새 남이 가져갔을 수 있다.
            int have   = _source != null ? _source.Inventory.GetUnlockedQuantity(_material) : 0;
            int toTake = Mathf.Min(_amount, have);
            if (toTake <= 0) { Fail(); return; }

            _source.Inventory.RemoveUnlocked(_material, toTake);
            int got = unit.Inventory.TryAdd(_material, toTake);
            // 배낭에 다 못 넣었으면 남은 것은 창고에 돌려놓는다 — 증발시키지 않는다.
            if (got < toTake) _source.Inventory.TryAdd(_material, toTake - got);
            if (got <= 0) { Fail(); return; }

            _carried = got;
            BeginDeliver();
            return;
        }

        // **실제로 빠진 만큼만 입고한다.** 배낭에서 곧장 지고 온 경우, 여기 닿기까지 폰이
        // 그것을 내려놓았을 수 있다(짐내림·플레이어 조작). 반환값을 안 보면 없는 자재가 입고된다.
        int delivered = unit.Inventory.RemoveUnlocked(_material, _carried);
        if (delivered > 0) _blueprint.Deliver(_material, delivered);

        _blueprint.OnJobEnded();
        Status = JobStatus.Done;
    }

    protected override void OnCancelled() => _blueprint?.OnJobEnded();

    void Fail()
    {
        _blueprint?.OnJobEnded();
        Status = JobStatus.Done;
    }
}

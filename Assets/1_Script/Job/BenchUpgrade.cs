using System.Collections.Generic;
using UnityEngine;

// **등급을 든 작업대.** 작업대와 연구대가 같은 축을 쓰므로(`BenchTier`) 올리는 문도 하나다.
public interface IUpgradableBench
{
    int  Tier { get; }
    void SetTier(int tier);

    // 등급을 뺀 이름 — `연구대 II`의 `연구대`. 화면이 `BenchTier.NameOf`로 붙인다.
    string BenchName { get; }
}

// **제자리 업그레이드 — 헐지 않고 등급만 올린다**
// (2026-09-17, 사용자 결정: *"새로 설치할 필요 없이 원래 쓰던 거에서 바로 업글할 수 있게"*).
//
// ## 왜 별도 건물이 아닌가
//
// 처음엔 `개량 연구대`를 **다른 `BuildingData`**로 냈는데, 그러면 올리는 일이 **헐고 다시
// 짓는 일**이 된다 — 자리·산출물 칸·지정한 제작자·쌓아 둔 주문이 통째로 날아가고,
// 건설 목록이 **등급 수만큼 부푼다**(작업대 열하나 × 다섯 = 쉰다섯 줄).
//
// ## 자재는 폰이 날라 온다
//
// 사용자 결정(*"폰이 날라 온다"*). 건설과 **같은 규칙**이라 *재료가 모자랄 때 클릭이 그냥
// 씹히는* 일이 없다 — 누르면 **예정**이 되고 폰이 지고 온다. `ConstructionJob`을 **그대로
// 쓴다**(`IConstructionSite`): 그 안에 *남이 먼저 가져갔을 때 · 배낭에 다 못 넣었을 때 ·
// 도중에 짐을 내려놓았을 때*가 이미 들어 있어서, 복사하면 그중 하나가 반드시 뒤처진다.
//
// ## 어기면 조용히 깨지는 것
//
// - **올리는 동안에도 작업대는 쓸 수 있다** — 막으면 *재료를 모으는 동안 기지가 멎는다*.
//   등급이 오르는 것은 **다 모인 순간 한 번**이다.
// - **취소는 전액 환불이다**(청사진과 같은 규칙) — 아직 아무것도 안 바뀌었으므로 깎을
//   손실이 없다. 깎으면 *올려 볼까 하다 마는 일*에 벌칙이 붙는다.
// - **건물이 사라지면 같이 사라진다** — 철거·이전에 딸려 간다(`OnDestroy`가 환불한다).
public class BenchUpgrade : MonoBehaviour, IConstructionSite
{
    IUpgradableBench _bench;

    readonly List<RecipeData.Ingredient> _costs     = new();
    readonly List<int>                   _delivered = new();

    int  _target;          // 올리려는 등급 (0이면 대기 중이 아니다)
    bool _jobPending;

    ConstructionJob _job;

    public bool     InProgress => _target > 0;
    public int      Target     => _target;
    public WorkAnim BuildAnim  => WorkAnim.HammerTable;

    public IEnumerable<(ItemData Item, int Delivered, int Required)> Progress
    {
        get
        {
            for (int i = 0; i < _costs.Count; i++)
                yield return (_costs[i].Item, _delivered[i], _costs[i].Amount);
        }
    }

    public bool IsComplete
    {
        get
        {
            for (int i = 0; i < _costs.Count; i++)
                if (_delivered[i] < _costs[i].Amount) return false;
            return true;
        }
    }

    // ── 붙이고 찾기 ──────────────────────────────────────────────────────────
    //
    // **런타임에 붙인다** — 프리팹 열하나에 손으로 달면 새 작업대를 만드는 날 하나가
    // 빠지고, 그건 *그 작업대만 못 올린다*로만 나타난다(`BuildingDormancy.Ensure`와 같은 규칙).
    public static BenchUpgrade Ensure(Component bench)
    {
        if (bench == null) return null;
        var have = bench.GetComponent<BenchUpgrade>();
        if (have != null) return have;

        // **`Awake`가 이미 채운다** — 여기서 다시 대입하면 `PlacedBuilding`을 넘겼을 때
        // (`as`가 null이라) **방금 찾은 작업대를 null로 덮어쓴다.**
        return bench.gameObject.AddComponent<BenchUpgrade>();
    }

    void Awake() => _bench ??= GetComponent<IUpgradableBench>();

    // ── 다음 등급이 무엇이고 무엇이 드나 ────────────────────────────────────

    public int NextTier => _bench == null ? 0
                         : BenchTier.CanRaiseTo(_bench.Tier + 1) ? _bench.Tier + 1
                         : 0;

    // **비용 표는 `BenchTier.CostTo` 하나가 든다** — 여기서 또 적으면 작업대와 연구대가
    // 다른 값을 요구하게 되고, 그건 화면에 안 적힌다.
    public static List<RecipeData.Ingredient> CostFor(int tier)
    {
        var list = new List<RecipeData.Ingredient>();
        if (!BenchTier.CostTo.TryGetValue(tier, out var rows)) return list;

        foreach (var (id, amount) in rows)
        {
            var item = DataManager.GetItem(id);
            if (item == null)
            {
                // **조용히 빼지 않는다** — 재료 한 줄이 사라지면 그 등급만 싸지는데,
                // 화면에는 아무 표시도 안 난다.
                Debug.LogWarning($"[작업대 등급] 재료를 못 찾았습니다 — {id} (등급 {tier})");
                continue;
            }
            list.Add(new RecipeData.Ingredient { Item = item, Amount = amount });
        }
        return list;
    }

    // ── 시작·취소 ───────────────────────────────────────────────────────────

    public bool Request()
    {
        if (InProgress || _bench == null) return false;

        int next = NextTier;
        if (next == 0) return false;

        _costs.Clear();
        _delivered.Clear();
        foreach (var c in CostFor(next))
        {
            if (c.Item == null || c.Amount <= 0) continue;
            _costs.Add(c);
            _delivered.Add(0);
        }

        // **재료를 하나도 못 찾았으면 올리지 않는다** — 곧바로 올리면 표의 오타가 *공짜 등급*이
        // 된다. 못 찾은 줄은 `CostFor`가 이미 경고했다.
        if (_costs.Count == 0) return false;

        _target = next;

        Storage.OnChanged += OnStorageChanged;
        TryRegisterJob();
        return true;
    }

    public void Cancel()
    {
        if (!InProgress) return;

        DropJob();
        Storage.OnChanged -= OnStorageChanged;

        for (int i = 0; i < _costs.Count; i++)
        {
            if (_delivered[i] <= 0) continue;
            // **창고 먼저**(`MaterialPool.Refund`) — 바닥에 떨구면 그 짐이 다시 운반 일감이
            // 되고, 나르는 동안 집계에서 빠져 같은 자재를 기다리던 작업이 죽는다.
            MaterialPool.Refund(_costs[i].Item, _delivered[i], transform.position);
        }

        _costs.Clear();
        _delivered.Clear();
        _target = 0;
    }

    void OnDestroy()
    {
        Storage.OnChanged -= OnStorageChanged;

        // **헐리면 모아 둔 자재를 돌려준다** — 안 돌려주면 철거가 조용히 자재를 먹는다.
        for (int i = 0; i < _costs.Count; i++)
            if (_delivered[i] > 0)
                MaterialPool.Refund(_costs[i].Item, _delivered[i], transform.position);
    }

    // ── 일감 ────────────────────────────────────────────────────────────────

    void OnStorageChanged() => TryRegisterJob();

    const float RetryInterval = 2f;
    float _retryTimer;

    void Update()
    {
        if (!InProgress || _jobPending) return;

        _retryTimer -= Time.deltaTime;
        if (_retryTimer > 0f) return;
        _retryTimer = RetryInterval;

        TryRegisterJob();
    }

    void TryRegisterJob()
    {
        if (!InProgress || _jobPending || IsComplete || JobManager.Instance == null) return;

        // **모자란 재료에서 멈추지 않는다** — 판자는 창고에 있는데 금속이 없다고 돌아가면
        // 구할 수 있는 절반조차 안 들어오고, 화면에는 *"아무도 안 온다"*로만 보인다.
        for (int i = 0; i < _costs.Count; i++)
        {
            int need = _costs[i].Amount - _delivered[i];
            if (need <= 0) continue;

            int available = MaterialPool.AvailableNear(_costs[i].Item, transform.position);
            if (available <= 0) continue;

            _jobPending = true;
            _job        = new ConstructionJob(this, _costs[i].Item, Mathf.Min(need, available));
            JobManager.Instance.RegisterJob(_job);
            return;
        }
    }

    void DropJob()
    {
        if (_job == null) return;
        JobManager.Instance?.RemoveJob(_job);
        if (_job.Status is JobStatus.Available or JobStatus.Reserved or JobStatus.InProgress)
            _job.Cancel();
        _job = null;
    }

    public void OnJobEnded()
    {
        _jobPending = false;
        TryRegisterJob();
    }

    public void Deliver(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || !InProgress) return;

        for (int i = 0; i < _costs.Count; i++)
        {
            if (_costs[i].Item != item) continue;

            _delivered[i] = Mathf.Min(_costs[i].Amount, _delivered[i] + amount);
            if (IsComplete) Apply();
            return;
        }
    }

    void Apply()
    {
        int tier = _target;

        Storage.OnChanged -= OnStorageChanged;
        _costs.Clear();
        _delivered.Clear();
        _target = 0;

        _bench?.SetTier(tier);
        Debug.Log($"[작업대 등급] {_bench?.BenchName} 이(가) {BenchTier.Roman(tier)} 등급이 됐습니다.");
    }
}

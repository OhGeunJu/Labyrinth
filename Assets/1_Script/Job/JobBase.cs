using UnityEngine;

public enum JobStatus
{
    Available,
    Reserved,
    InProgress,
    Done,
    Cancelled
}

public abstract class JobBase
{
    public Vector2Int TileCoord { get; protected set; }
    public JobStatus Status { get; protected set; } = JobStatus.Available;
    public UnitBrain ReservedBy { get; private set; }
    public int Priority { get; protected set; } = 0;
    public virtual JobCategory Category => JobCategory.None;

    // **지금 어떤 동작으로 일하나** — 애니메이터와 손에 드는 도구가 읽는다(`WorkAnim`). 단계가
    // 있는 일감은 **자리에서 일하는 단계에서만** 답한다(창고에서 재료를 집는 동안 망치질을
    // 하면 안 된다). 기본값은 없음 — 동작이 없는 일(치료·연구·운반)은 그대로 둔다.
    public virtual WorkAnim Anim => WorkAnim.None;

    // 지금 이 작업을 실제로 수행할 수 있는가 (예: 제작 재료 보유).
    // false면 JobManager가 배정을 미루고 큐에 대기 — 조건 충족 시 자동 재개.
    public virtual bool IsReady => true;

    // **누가 집을 수 있는가.** `IsReady`(일감 자체가 준비됐나)와 다른 축이다 —
    // 준비는 됐는데 **이 폰은 안 되는** 경우가 있다(지정 제작자, 만들던 사람이 이어서 해야 하는 장비).
    public virtual bool CanAssignTo(UnitBrain unit) => true;

    // **어느 건물의 일감인가**(2026-09-29 — 배치 칸 `WorkPosts`가 *"이 작업대의 것만"*을 거른다).
    // 작업대·연구대·도축대는 제 대, 밭 일감은 그 밭이 답한다. **건물에 매이지 않은 일은 `null`**(운반·건설…).
    // 일감마다 제 건물을 필드로 이미 들고 있었다 — 밖에서 물을 문이 없었을 뿐이다.
    public virtual Component Site => null;

    // ── 얼마나 가까이 가야 "도착"인가 ──────────────────────────────────
    //
    // **두 규약이 있다.**
    //
    // `StandReach` — 작업대·연구대·침대·건설은 **저작된 자리에 실제로 서야** 그림이 맞는다.
    // 칸 하나가 1m이므로 그 칸 위에 서 있다는 것은 중심에서 반대각선(≈0.71) 안쪽이라는 뜻이고,
    // 0.9면 넉넉하다. 넓게 잡으면 폰이 작업대에서 두 칸 떨어져 허공에 대고 제작한다.
    //
    // `HandReach` — **창고에 넣고 꺼내는 것은 손만 닿으면 되는 일**이다. 거기 서 있어야 할
    // 이유가 없는데 한 칸에 붙이면, 여럿이 같은 창고를 쓸 때 서로 비켜서느라 오래 걸린다
    // (자원 노드를 `NodeReach`로 둘러서서 캐게 한 것과 **같은 축**이다).
    //
    // 단계마다 다를 수 있으므로 상수가 아니라 프로퍼티다 — `ConstructionJob`은 창고에
    // 들를 때는 손만 닿으면 되고, 청사진 앞에서는 그 자리에 서야 한다.
    public const float StandReach = 0.9f;
    public const float HandReach  = 2.0f;

    // **자원 노드는 둘러서서 캔다** — 작업대와 달리 *저작된 설 자리*가 없고 `MaxWorkers`만큼
    // 여럿이 붙으므로, 좁게 잡으면 한 칸을 두고 서로 비켜서다 아무도 못 캔다.
    // **`BTLeafNodes`의 같은 이름과 값이 같아야 한다** — 갈리면 *배정해서 캘 때*와
    // *찍어서 캘 때*가 다른 거리에서 선다.
    public const float NodeReach  = 2.0f;

    public virtual float Reach => StandReach;

    // **어디서 재고 · 어디로 가고 · 어디를 보나** — 기본은 목표 칸 하나다. 몸이 여러 칸인
    // 것(자원 노드)은 **몸에서 재고 폰 쪽 둘레로 간다**(`ResourceNode.ReachFrom`).
    public virtual float DistanceFrom(Vector3 pos)
    {
        var c = MapManager.Instance.TileToWorld(TileCoord);
        return Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(c.x, c.z));
    }
    public virtual Vector3? StandPointFor(Vector3 from) => null;   // null이면 목표 칸 둘레
    public virtual Vector3  FacePoint => MapManager.Instance.TileToWorld(TileCoord);

    // ── 작업 현장(건물) ────────────────────────────────────────────────
    // 이 작업이 매달린 건물. 핵 영역 밖으로 나가 휴면 중이면 배정하지 않는다.
    // 취소가 아니라 대기 — 영역이 복구되면 주문/일감이 그대로 재개된다(재료 부족 대기와 같은 정책).
    BuildingDormancy _siteDormancy;

    protected void SetSite(Component site)
    {
        _siteDormancy = site != null ? site.GetComponentInParent<BuildingDormancy>() : null;

        // **목표 칸도 여기서 정한다.** 전에는 각 Job이 `WorldToTile(건물 위치)`를 직접 넣었는데,
        // 그 칸은 대개 통행 불가라 폰이 도착 판정에 못 걸렸다. `WorkSpot`이 저작된 자리 또는
        // 주변의 설 수 있는 칸을 돌려주므로, 한 곳에서 고치면 모든 작업이 같이 고쳐진다.
        if (site != null) TileCoord = WorkSpot.TileFor(site);
    }

    public bool IsSiteUsable => _siteDormancy == null || !_siteDormancy.IsDormant;

    // ── 예약 ──────────────────────────────────────────────────────────
    public bool Reserve(UnitBrain unit)
    {
        if (Status != JobStatus.Available) return false;
        ReservedBy = unit;
        Status = JobStatus.Reserved;
        OnReserved(unit);
        return true;
    }

    // **누가 할지 정해지는 순간.** 폰마다 다른 값으로 일감을 다시 재야 할 때 쓴다 —
    // 등록 시점엔 누가 올지 모르므로 넉넉히 잡아 두고, 여기서 그 폰에 맞춰 줄인다
    // (운반이 실을 양을 폰의 적재 한도로 자르는 데 쓴다).
    protected virtual void OnReserved(UnitBrain unit) { }

    public void ReleaseReservation()
    {
        ReservedBy = null;
        Status = JobStatus.Available;
    }

    // ── 진행 ──────────────────────────────────────────────────────────
    public void Start()
    {
        Status = JobStatus.InProgress;
        OnStart();
    }

    public void Tick(UnitBrain unit)
    {
        if (Status != JobStatus.InProgress) return;
        Execute(unit);
    }

    public void Cancel()
    {
        ReleaseReservation();
        Status = JobStatus.Cancelled;
        OnCancelled();
    }

    public bool IsComplete() => Status == JobStatus.Done;

    // ── 파생 클래스 구현 ───────────────────────────────────────────────
    protected abstract void Execute(UnitBrain unit);
    protected virtual void OnStart() { }
    protected virtual void OnCancelled() { }
}
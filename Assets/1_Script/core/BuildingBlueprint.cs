using System.Collections.Generic;
using UnityEngine;

// 건설 예비 설계. 자재가 다 모이면 실제 건물을 스폰하고 자신은 파괴됨.
//
// **청사진은 칸을 막지 않는다** — 짓는 동안 폰이 지나다녀야 하고, 자재를 지고 오는 폰도
// 통과한다. 대신 자기가 앞으로 깔고 앉을 칸을 `Footprint`로 알려 준다: 그래야 건설 자리가
// 발자국 밖으로 잡히고(`WorkSpot.TileFor`), 완성되는 순간 지은 폰이 벽 안에 갇히지 않는다.
public class BuildingBlueprint : MonoBehaviour, IFootprintSite
{
    public static readonly List<BuildingBlueprint> All = new();

    BuildingData _data;
    ItemData     _costItem;
    int          _costRequired;
    int          _costDelivered;
    bool         _jobPending;
    bool         _cancelled;

    ConstructionJob _job;      // 지금 걸려 있는 일감 — 취소할 때 거둔다
    Material        _ghostMat;
    Renderer[]      _renderers;
    bool            _highlighted;

    public bool         IsComplete   => _costDelivered >= _costRequired;
    public int          Remaining    => Mathf.Max(0, _costRequired - _costDelivered);
    public ItemData     CostItem     => _costItem;
    public BuildingData Data         => _data;
    public int          Delivered    => _costDelivered;
    public int          CostRequired => _costRequired;

    // 완성되면 막을 칸들 — 계산은 `PlacedBuilding`과 같은 것을 쓴다(답이 두 벌이면 어긋난다).
    // 칸을 안 막는 건물(`OccupiesTile` false)은 발자국도 비운다 — 그 위에 서도 갇히지 않는다.
    public IEnumerable<Vector2Int> Footprint =>
        _data != null && _data.OccupiesTile
            ? PlacedBuilding.FootprintAt(transform.position, _data.Size, transform.rotation)
            : System.Array.Empty<Vector2Int>();

    // **커서로 집을 수 있는 칸들 — `Footprint`와 다른 축이다.** 발자국은 "완성되면 막을 칸"이라
    // 창고·침대·농지처럼 안 막는 건물은 비어 있는데, 그 칸에 놓인 청사진도 집을 수 있어야 한다.
    public IEnumerable<Vector2Int> Area =>
        _data != null
            ? PlacedBuilding.FootprintAt(transform.position, _data.Size, transform.rotation)
            : System.Array.Empty<Vector2Int>();

    public void Init(BuildingData data)
    {
        _data         = data;
        _costItem     = data.CostItem;
        _costRequired = data.CostAmount;
        BuildGhostVisual();
        Debug.Log($"[Blueprint] 예비 설계 배치 — {data.BuildingName} ({_costItem?.ItemName} ×{_costRequired} 필요)");
    }

    void BuildGhostVisual()
    {
        if (_data?.Prefab == null) return;

        var ghost = Instantiate(_data.Prefab);
        ghost.transform.SetParent(transform, false);

        // **루트 로컬 위치를 실제 배치와 같게 맞춘다.**
        //
        // `SetParent(transform, false)`는 프리팹 루트의 `localPosition`을 **그대로 물려받는다.**
        // 그런데 완성될 때는 `Instantiate(prefab, pos, rot)`라 그 값이 **버려진다** — 그래서
        // 루트가 0이 아닌 프리팹은 청사진과 완성품이 그만큼 어긋나 보였다
        // (`Bed.prefab` 루트가 z = 0.5라, 침대가 청사진보다 반 칸 뒤에 놓였다).
        //
        // 부양 높이만 다시 얹는다 — `SpawnOnSurface`가 배치 때 하는 것과 같다.
        // 배치 미리보기(`BuildModeManager.EnsureGhost`)는 처음부터 이렇게 하고 있었다.
        float hover = BuildModeManager.AuthoredHover(_data.Prefab);
        ghost.transform.localPosition = new Vector3(0f, hover, 0f);
        ghost.transform.localRotation = Quaternion.identity;

        // 완성 후 모습과 같은 높이로 — 부양 프리팹이 아니면 렌더러 바닥면을 표면에 맞춘다.
        if (MapManager.Instance != null && hover <= 0f)
            BuildModeManager.SnapOnSurface(
                ghost, MapManager.Instance.SurfaceHeight(MapManager.Instance.WorldToTile(transform.position)));

        foreach (var c in ghost.GetComponentsInChildren<Collider>())     c.enabled = false;
        foreach (var b in ghost.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;

        _ghostMat = CreateBlueprintMaterial();
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            r.material = _ghostMat;
    }

    Material CreateBlueprintMaterial()
    {
        // URP → Standard 순으로 시도
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = new Color(0.3f, 0.6f, 1f, 0.4f) };

        // URP Lit
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        // Built-in Standard
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3f);
            mat.EnableKeyword("_ALPHABLEND_ON");
        }

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        return mat;
    }

    void Start()
    {
        All.Add(this);
        if (_costRequired <= 0) { Complete(); return; }
        Storage.OnChanged += OnStorageChanged;
        TryRegisterJob();
    }

    void OnDestroy()
    {
        All.Remove(this);
        Storage.OnChanged -= OnStorageChanged;
        if (_ghostMat != null) Destroy(_ghostMat);
    }

    void OnStorageChanged() => TryRegisterJob();

    // **배낭이 바뀌는 것은 이벤트로 못 듣는다.** `Storage.OnChanged`는 static이지만
    // `Inventory.OnChanged`는 인스턴스 단위라, 청사진마다 플레이어 폰 전부를 구독했다
    // 끊어야 한다(폰은 늘어나고 죽고 원정을 떠난다). 자재를 사 오거나 주워 오는 건 드문
    // 일이므로 주기 점검으로 족하다 — `ResourceNode`가 잔량을 다시 집는 것과 같은 방식.
    const float RetryInterval = 2f;
    float _retryTimer;

    void Update()
    {
        if (_jobPending || IsComplete) return;

        _retryTimer -= Time.deltaTime;
        if (_retryTimer > 0f) return;
        _retryTimer = RetryInterval;

        TryRegisterJob();
    }

    void TryRegisterJob()
    {
        // **취소된 뒤에도 불린다.** 일감을 거두면 `OnCancelled`가 `OnJobEnded`로 돌아오는데,
        // `IsComplete`는 미완이라 그대로 두면 사라지는 청사진이 새 일감을 또 등록한다.
        if (_cancelled) return;
        if (_jobPending || IsComplete || JobManager.Instance == null || _costItem == null) return;

        // **창고만 세면 손에 든 자재가 없는 것이 된다.** `MaterialPool`이 답하는 것과 같은
        // 축으로 본다 — 창고 + 플레이어 폰들의 배낭. 등록 시점엔 누가 지을지 모르므로
        // 넓게 보고, 폰별 판정은 `ConstructionJob.CanAssignTo`가 다시 한다.
        int available = MaterialPool.AvailableAnywhere(_costItem);
        if (available <= 0) return;

        _jobPending = true;
        _job        = new ConstructionJob(this, _costItem, Mathf.Min(Remaining, available));
        JobManager.Instance.RegisterJob(_job);
    }

    public void OnJobEnded()
    {
        _jobPending = false;
        TryRegisterJob();
    }

    // ── 취소 ────────────────────────────────────────────────────────────────
    //
    // **취소는 철거가 아니다 — 납품된 자재를 전액 돌려준다.** 철거가 75%만 주는 건 지어진
    // 것을 부수기 때문이고, 청사진은 아직 아무것도 아니라 깎을 손실이 없다. 깎으면
    // **계획을 세우는 일에 벌칙이 붙는다** — 놓아 보고 아니면 물리는 것이 청사진의 쓸모다.
    //
    // 돌아가는 곳은 **창고 먼저**(`MaterialPool.Refund`). 바닥에 떨구면 그 짐이 다시 운반
    // 일감이 되고, 나르는 동안 창고 집계에서 빠져 있어 다른 작업이 "재료 부족"으로 죽는다.
    public void Cancel()
    {
        if (_cancelled) return;
        _cancelled = true;

        DropJob();

        if (_costItem != null && _costDelivered > 0)
        {
            MaterialPool.Refund(_costItem, _costDelivered, transform.position);
            Debug.Log($"[Blueprint] 취소 — {_costItem.ItemName} ×{_costDelivered} 환불");
        }
        _costDelivered = 0;

        Destroy(gameObject);
    }

    // **하던 일감도 거둔다** — `Bed`는 진행 중인 치료를 안 뺏지만(약만 버려진다) 여기는
    // 청사진 자체가 사라진다. 그냥 두면 폰이 끝까지 걸어가 `Execute`에서 실패할 뿐이라,
    // 지금 놓게 하는 편이 헛걸음을 던다.
    //
    // **이미 배낭에 실은 자재는 안 건드린다.** 그건 폰의 짐이고 짐내림(`UnloadJob`)이
    // 창고로 돌려놓는다 — 여기서 뺏으면 어느 인벤토리에서 사라졌는지가 화면에 안 남는다.
    void DropJob()
    {
        if (_job == null) return;
        JobManager.Instance?.RemoveJob(_job);
        if (_job.Status is JobStatus.Available or JobStatus.Reserved or JobStatus.InProgress)
            _job.Cancel();
        _job = null;
    }

    // 그 칸에 걸쳐 있는 청사진 — **콜라이더가 아니라 타일로 집는다.** 고스트는 콜라이더를
    // 일부러 다 꺼 두었다(청사진은 통행을 안 막고, 켜면 선택·이동 명령의 레이캐스트에 걸린다).
    public static BuildingBlueprint PickAt(Vector2Int tile)
    {
        foreach (var bp in All)
        {
            if (bp == null) continue;
            foreach (var t in bp.Area)
                if (t == tile) return bp;
        }
        return null;
    }

    // 철거 모드의 빨간 표시 — 완성 건물과 **같은 표시**를 쓴다(색이 다르면 무엇이 잡혔는지
    // 눈으로 못 가른다). 고스트가 반투명이라 알파만 낮춰 넘긴다.
    static readonly Color PendingColor = new(1f, 0.3f, 0.3f, 0.45f);

    public void SetPendingHighlight(bool on)
    {
        if (on == _highlighted) return;
        _highlighted = on;

        _renderers ??= GetComponentsInChildren<Renderer>();
        PlacedBuilding.ApplyTint(_renderers, on, PendingColor);
    }

    public void Deliver(ItemData item, int amount)
    {
        if (item != _costItem || amount <= 0) return;
        _costDelivered = Mathf.Min(_costRequired, _costDelivered + amount);
        Debug.Log($"[Blueprint] 자재 입고 — {item?.ItemName} {_costDelivered}/{_costRequired}");
        if (IsComplete) Complete();
    }

    void Complete()
    {
        Debug.Log($"[Blueprint] 건설 완료 — {(_data != null ? _data.BuildingName : name)}");

        if (_data?.Prefab != null)
        {
            // 청사진 위치의 표면 위에 얹기 — 즉시 배치와 동일 규칙(피벗 스냅 / 저작 부양 높이 유지)
            var surfacePos = MapManager.Instance != null
                ? new Vector3(transform.position.x,
                              MapManager.Instance.SurfaceHeight(MapManager.Instance.WorldToTile(transform.position)),
                              transform.position.z)
                : transform.position;
            var go = BuildModeManager.SpawnOnSurface(_data.Prefab, surfacePos, transform.rotation);
            // SubCore는 프리팹에 설정된 값 사용, 추가 Init 불필요
            // 핵 영역 이탈 시 휴면/재가동 (설계: design_core_creep.md step 3)
            BuildingDormancy.Ensure(go);
            // 철거/환불용 메타 보관
            var placed = PlacedBuilding.Attach(go, _data);
            // 타일 점유(통행 차단) 건물이면 footprint를 통행 불가로 등록 (철거 시 대칭 해제).
            // **크기만큼 막는다** — 예전엔 중심 한 칸만 막아서 3×2 건물의 나머지 다섯 칸을
            // 폰이 그냥 통과했다(즉시 배치는 처음부터 footprint를 넘겼는데 청사진만 빠져 있었다).
            placed.OccupyFootprint();
        }

        // 벽 등 장애물이면 메소 격자에 반영 (CheckBox가 Wall 콜라이더 감지)
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(transform.position);

        Destroy(gameObject);
    }
}

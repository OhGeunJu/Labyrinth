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

    Material _ghostMat;

    public bool     IsComplete => _costDelivered >= _costRequired;
    public int      Remaining  => Mathf.Max(0, _costRequired - _costDelivered);
    public ItemData CostItem   => _costItem;

    // 완성되면 막을 칸들 — 계산은 `PlacedBuilding`과 같은 것을 쓴다(답이 두 벌이면 어긋난다).
    // 칸을 안 막는 건물(`OccupiesTile` false)은 발자국도 비운다 — 그 위에 서도 갇히지 않는다.
    public IEnumerable<Vector2Int> Footprint =>
        _data != null && _data.OccupiesTile
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

    void TryRegisterJob()
    {
        if (_jobPending || IsComplete || JobManager.Instance == null || _costItem == null) return;
        int available = Storage.GetTotalByItem(_costItem);
        if (available <= 0) return;

        _jobPending = true;
        JobManager.Instance.RegisterJob(
            new ConstructionJob(this, _costItem, Mathf.Min(Remaining, available)));
    }

    public void OnJobEnded()
    {
        _jobPending = false;
        TryRegisterJob();
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

using System.Collections.Generic;
using UnityEngine;

// 건설 예비 설계. 자재가 다 모이면 실제 건물을 스폰하고 자신은 파괴됨.
public class BuildingBlueprint : MonoBehaviour
{
    public static readonly List<BuildingBlueprint> All = new();

    BuildingData _data;
    ResourceType _costType;
    int          _costRequired;
    int          _costDelivered;
    bool         _jobPending;

    Material _ghostMat;

    public bool         IsComplete => _costDelivered >= _costRequired;
    public int          Remaining  => Mathf.Max(0, _costRequired - _costDelivered);
    public ResourceType CostType   => _costType;

    public void Init(BuildingData data)
    {
        _data         = data;
        _costType     = data.CostType;
        _costRequired = data.CostAmount;
        BuildGhostVisual();
        Debug.Log($"[Blueprint] 예비 설계 배치 — {data.BuildingName} ({_costType} ×{_costRequired} 필요)");
    }

    void BuildGhostVisual()
    {
        if (_data?.Prefab == null) return;

        var ghost = Instantiate(_data.Prefab);
        ghost.transform.SetParent(transform, false);

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
        if (_jobPending || IsComplete || JobManager.Instance == null) return;
        int available = Storage.GetTotal(_costType);
        if (available <= 0) return;

        _jobPending = true;
        JobManager.Instance.RegisterJob(
            new ConstructionJob(this, _costType, Mathf.Min(Remaining, available)));
    }

    public void OnJobEnded()
    {
        _jobPending = false;
        TryRegisterJob();
    }

    public void Deliver(ResourceType type, int amount)
    {
        if (type != _costType || amount <= 0) return;
        _costDelivered = Mathf.Min(_costRequired, _costDelivered + amount);
        Debug.Log($"[Blueprint] 자재 입고 — {type} {_costDelivered}/{_costRequired}");
        if (IsComplete) Complete();
    }

    void Complete()
    {
        Debug.Log($"[Blueprint] 건설 완료 — {(_data != null ? _data.BuildingName : name)}");

        if (_data?.Prefab != null)
        {
            var go = Instantiate(_data.Prefab, transform.position, transform.rotation);
            // SubCore는 프리팹에 설정된 값 사용, 추가 Init 불필요
            // 핵 영역 이탈 시 휴면/재가동 (설계: design_core_creep.md step 3)
            BuildingDormancy.Ensure(go);
            // 철거/환불용 메타 보관
            PlacedBuilding.Attach(go, _data);
        }

        // 벽 등 장애물이면 메소 격자에 반영 (CheckBox가 Wall 콜라이더 감지)
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(transform.position);

        Destroy(gameObject);
    }
}

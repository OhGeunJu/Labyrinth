using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DungeonCore : MonoBehaviour
{
    public static DungeonCore Instance { get; private set; }

    [SerializeField] DungeonCoreData data;
    [SerializeField] Color rangeColor = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] int rangeSegments = 64;

    public DungeonCoreData Data => data;
    public bool IsSelected { get; private set; }

    public static event Action<DungeonCore> OnSelected;

    // ── 하수인 캡 ────────────────────────────────────────────────────────────
    readonly List<SubCore> _subCores = new();
    int _currentMinionCount;

    public int CurrentMinionCount => _currentMinionCount;
    public int TotalMinionCap => (data != null ? data.BaseMinionCap : 5)
                                 + GetSubCoreMinionBonus();

    int GetSubCoreMinionBonus()
    {
        int bonus = 0;
        foreach (var sc in _subCores) if (sc != null) bonus += sc.MinionCapBonus;
        return bonus;
    }

    public void RegisterSubCore(SubCore sc)   { if (!_subCores.Contains(sc)) _subCores.Add(sc); }
    public void UnregisterSubCore(SubCore sc) { _subCores.Remove(sc); }

    public IReadOnlyList<SubCore> SubCores => _subCores;

    [SerializeField] Color selectedColor = new Color(1f, 0.9f, 0.2f, 0.8f);

    Material _mat;

    void Awake()
    {
        Instance = this;
        UnitBrain.OnUnitSpawned += OnUnitSpawned;
        UnitBrain.OnUnitDied    += OnUnitDied;
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",    (int)CullMode.Off);
        _mat.SetInt("_ZWrite", 0);
    }

    void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    void OnDestroy()
    {
        UnitBrain.OnUnitSpawned -= OnUnitSpawned;
        UnitBrain.OnUnitDied    -= OnUnitDied;
        Destroy(_mat);
    }

    void OnUnitSpawned(UnitBrain unit)
    {
        if (unit.Data is MinionData) _currentMinionCount++;
    }

    void OnUnitDied(UnitBrain unit)
    {
        if (unit.Data is MinionData) _currentMinionCount = Mathf.Max(0, _currentMinionCount - 1);
    }

    void Start()
    {
        // 핵의 현재 위치에 빌드존을 정렬
        if (MapManager.Instance != null)
            MapManager.Instance.RecenterAt(transform.position);
    }

    public void Select()
    {
        IsSelected = true;
        OnSelected?.Invoke(this);
    }

    public void Deselect() { IsSelected = false; }

    [System.Serializable]
    public struct MinionEntry
    {
        public MinionData Data;
        public GameObject Prefab;
    }

    [Header("소환 가능한 하수인 목록")]
    [SerializeField] MinionEntry[] minionEntries;

    public MinionEntry[] MinionEntries => minionEntries;

    // 하수인 소환 — 캡 확인 → 자원 소비 → 프리팹 스폰
    public bool Summon(MinionData minionData, Vector3 spawnPosition)
    {
        if (minionData == null) return false;

        if (_currentMinionCount >= TotalMinionCap)
        {
            Debug.Log($"[DungeonCore] 하수인 한도 초과 ({_currentMinionCount}/{TotalMinionCap})");
            return false;
        }

        GameObject prefab = null;
        foreach (var entry in minionEntries)
            if (entry.Data == minionData) { prefab = entry.Prefab; break; }

        if (prefab == null)
        {
            Debug.LogWarning($"[DungeonCore] {minionData.RaceName}에 연결된 프리팹이 없습니다.");
            return false;
        }

        if (!Storage.Consume(minionData.SummonCostType, minionData.SummonCostAmount))
        {
            Debug.Log($"[DungeonCore] 자원 부족 — {minionData.SummonCostType} ×{minionData.SummonCostAmount} 필요");
            return false;
        }

        Instantiate(prefab, spawnPosition, Quaternion.identity);
        Debug.Log($"[DungeonCore] {minionData.RaceName} 소환 ({_currentMinionCount + 1}/{TotalMinionCap})");
        return true;
    }

    // 메인 코어 또는 서브 코어 범위 내 여부
    public bool IsWithinAnyRange(Vector2Int tile)
    {
        if (IsWithinRange(tile)) return true;
        foreach (var sc in _subCores)
            if (sc != null && sc.IsWithinRange(tile)) return true;
        return false;
    }

    public bool IsWithinAnyRangeWorld(Vector3 worldPos)
    {
        if (MapManager.Instance == null) return false;
        var tile = MapManager.Instance.WorldToTile(worldPos);
        if (IsWithinRange(tile)) return true;
        foreach (var sc in _subCores)
            if (sc != null && sc.IsWithinRangeWorld(worldPos)) return true;
        return false;
    }

    // 코어 주변 랜덤 위치 반환
    public Vector3 GetSpawnPosition()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float dist  = 2f;
        return transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    public bool IsWithinRange(Vector2Int tile)
    {
        if (data == null) return true;
        if (MapManager.Instance == null) return false;
        var wp = MapManager.Instance.TileToWorld(tile);
        var dx = wp.x - transform.position.x;
        var dz = wp.z - transform.position.z;
        return dx * dx + dz * dz <= data.BaseRadius * data.BaseRadius;
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main) return;
        DrawRange();
    }

    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline != null) return;
        DrawRange();
    }

    void DrawRange()
    {
        if (data == null) return;

        float radius = data.BaseRadius;
        Vector3 center = new Vector3(transform.position.x, 0.02f, transform.position.z);

        _mat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINE_STRIP);
        GL.Color(IsSelected ? selectedColor : rangeColor);

        for (int i = 0; i <= rangeSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / rangeSegments;
            GL.Vertex3(center.x + Mathf.Cos(angle) * radius,
                       center.y,
                       center.z + Mathf.Sin(angle) * radius);
        }

        GL.End();
        GL.PopMatrix();
    }
}

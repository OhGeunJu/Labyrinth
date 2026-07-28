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

    // 영역(핵 union) 변경 시 발동 — 핵 이동/서브코어 등록·해제. 건물 휴면 재평가 트리거.
    public static event Action OnTerritoryChanged;
    public static void RaiseTerritoryChanged() => OnTerritoryChanged?.Invoke();

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

    public void RegisterSubCore(SubCore sc)   { if (!_subCores.Contains(sc)) { _subCores.Add(sc); RaiseTerritoryChanged(); } }
    public void UnregisterSubCore(SubCore sc) { if (_subCores.Remove(sc)) RaiseTerritoryChanged(); }

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

    // 씬에 저작된 부양 높이(표면 위로 얼마나 떠 있는가). 핵 이전 시 이 높이를 유지한다.
    float _hoverOffset;

    void Start()
    {
        // 핵의 현재 위치에 빌드존을 정렬
        if (MapManager.Instance != null)
            MapManager.Instance.RecenterAt(transform.position);

        // Floor는 OnEnable에서 등록되므로 이 시점엔 바닥 높이가 준비돼 있다.
        _hoverOffset = transform.position.y - SurfaceHeightAt(transform.position);
        // 초기 영역 확정 → 핵 생성 전에 OnEnable한 건물들이 휴면 여부 재평가하도록.
        RaiseTerritoryChanged();
    }

    // 메인 핵 이전(relocate). 정착지 필드맵은 고정(200×200)이므로 그리드를 recenter하지 않는다.
    // 코어는 고정 필드 안에서만 이동하며, 영역(핵 radius union)만 바뀐다 → 건물 휴면 자동 재평가.
    // 점유(_occupied)는 절대좌표라 이동해도 건물·점유가 리셋되지 않는다.
    // (UI/입력 트리거는 후속 — 이 메서드만 호출하면 됨)
    public void RelocateTo(Vector3 worldPosition)
    {
        // Y는 목적지 표면 + 저작 부양 높이 — 일부러 띄워둔 핵이 이전 후 바닥에 박히지 않게.
        float y = SurfaceHeightAt(worldPosition) + _hoverOffset;
        transform.position = new Vector3(worldPosition.x, y, worldPosition.z);
        RaiseTerritoryChanged();
    }

    // 해당 위치 타일의 표면 높이(바닥 윗면 ∥ 지형). 맵이 없으면 그 위치의 Y를 그대로 본다.
    static float SurfaceHeightAt(Vector3 worldPos)
    {
        var map = MapManager.Instance;
        return map != null ? map.SurfaceHeight(map.WorldToTile(worldPos)) : worldPos.y;
    }

    // 정착지 전환 시 이 코어를 활성 메인 코어로 재지정 (SettlementManager가 호출).
    // MapManager.SetAsPlayerZone과 짝 — 활성 정착지의 코어/존 싱글톤을 일치시킨다.
    public void SetAsActive()
    {
        Instance = this;
        RaiseTerritoryChanged();
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

        // 바이옴 콘텐츠 게이팅(step 7). BiomeContentData 미설정 바이옴은 제한 없음 → 기존 씬 무영향.
        if (SettlementManager.Instance != null &&
            !BiomeContent.IsMinionAllowed(SettlementManager.Instance.ActiveBiome, minionData))
        {
            Debug.Log($"[DungeonCore] {minionData.RaceName}은(는) 현재 바이옴에서 소환 불가");
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

    // 메인 핵이 candidateCorePos에 있다고 가정했을 때 worldPos가 영역(메인+서브 union) 안인지.
    // 핵 이전 미리보기(CoreRelocator)에서 휴면/재가동 예정 건물을 세는 데 쓴다.
    // 판정은 IsWithinAnyRangeWorld와 동일하게 타일 중심 기준으로 맞춘다(경계에서 미리보기 ≠ 실제 방지).
    public bool WouldBeInRangeAt(Vector3 candidateCorePos, Vector3 worldPos)
    {
        if (data == null) return true;                 // 반경 미설정 = 무제한 (IsWithinRange 관례)
        if (MapManager.Instance == null) return false;

        var wp = MapManager.Instance.TileToWorld(MapManager.Instance.WorldToTile(worldPos));
        var dx = wp.x - candidateCorePos.x;
        var dz = wp.z - candidateCorePos.z;
        if (dx * dx + dz * dz <= data.BaseRadius * data.BaseRadius) return true;

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

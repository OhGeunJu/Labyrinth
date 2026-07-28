using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 메인 던전 코어 이전(relocate) 액션 — 설계: docs/design_core_creep.md step 5.
// DungeonCoreUI의 [핵 이전] 버튼 → Begin() → 커서 위치 미리보기(타일 유효성 + 이전 후 영역 원 +
// 휴면/재가동 예정 건물 수) → 좌클릭 확정(DungeonCore.RelocateTo) / 우클릭·Esc 취소.
//
// 씬 배치 불필요: Begin이 전용 GameObject를 만들고, 확정/취소 시 스스로 파괴한다.
// 이전 자체는 RelocateTo가 담당(위치 이동 + RaiseTerritoryChanged) → BuildingDormancy가 자동 재평가.
// 그리드는 recenter하지 않는다(정착지 필드맵 고정, buildZoneSize=200이 전체를 덮음).
public class CoreRelocator : MonoBehaviour
{
    public static CoreRelocator Instance { get; private set; }
    public static bool IsActive => Instance != null;

    DungeonCore  _core;
    GridRenderer _grid;
    Camera       _cam;
    Material     _mat;

    readonly List<Vector2Int> _footprint = new() { Vector2Int.zero };

    Vector2Int _tile;
    Vector3    _targetPos;
    bool       _hasTarget;
    bool       _valid;
    int        _dormantCount;   // 이전 시 새로 휴면될 건물 수
    int        _wakeCount;      // 이전 시 재가동될 건물 수

    // ── 진입/종료 ──────────────────────────────────────────────────────────────

    public static void Begin(DungeonCore core)
    {
        if (core == null || BuildModeManager.IsActive) return;

        // 이미 이전 모드면 대상만 갈아끼운다(재생성 시 파괴 지연으로 미리보기가 꼬임).
        if (Instance != null) { Instance._core = core; Instance.ClearTarget(); return; }

        var go = new GameObject("[CoreRelocator]");
        var relocator = go.AddComponent<CoreRelocator>();
        relocator._core = core;
    }

    // Instance는 여기서 비우지 않는다(OnDestroy에서 정리) — Destroy는 프레임 끝에 반영되므로
    // 확정/취소한 그 프레임 동안 IsActive가 유지돼야 같은 클릭이 유닛 선택 등으로 새어나가지 않는다.
    public static void Cancel()
    {
        if (Instance != null) Destroy(Instance.gameObject);
    }

    void Awake()
    {
        Instance = this;
        _cam  = Camera.main;
        _grid = FindFirstObjectByType<GridRenderer>();

        _mat = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",     (int)CullMode.Off);
        _mat.SetInt("_ZWrite",   0);

        // 건설 모드로 들어가면 이전 모드는 종료(입력 충돌 방지)
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDestroy()
    {
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        // `?.`가 아니라 Unity 널 비교 — 씬 언로드 시 이미 파괴된 GridRenderer를 건드리면 예외.
        if (_grid != null) _grid.HidePreview();
        if (_mat != null) Destroy(_mat);
        if (Instance == this) Instance = null;
    }

    void OnBuildModeChanged(bool active) { if (active) Cancel(); }

    // ── 입력/미리보기 ──────────────────────────────────────────────────────────

    void Update()
    {
        if (_core == null || MapManager.Instance == null) { Cancel(); return; }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        if (Mouse.current == null) return;
        if (Mouse.current.rightButton.wasPressedThisFrame) { Cancel(); return; }

        UpdateTarget();

        if (_valid && Mouse.current.leftButton.wasPressedThisFrame)
            Confirm();
    }

    void UpdateTarget()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearTarget();
            return;
        }

        if (_cam == null) _cam = Camera.main;   // Awake 시점에 메인 카메라가 없었던 경우
        if (_cam == null) { ClearTarget(); return; }

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 point;
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            point = hit.point;
        // 공허(던전 씬): 콜라이더가 없어도 y=0 평면 교차로 조준 — BuildModeManager와 동일 폴백.
        else if (new Plane(Vector3.up, 0f).Raycast(ray, out float enter) && enter <= 500f)
            point = ray.GetPoint(enter);
        else { ClearTarget(); return; }

        var map  = MapManager.Instance;
        var tile = map.WorldToTile(point);
        if (_hasTarget && tile == _tile) return;   // 같은 타일이면 재계산 생략

        _tile      = tile;
        _hasTarget = true;
        _targetPos = map.TileToGround(tile);
        _valid     = IsValidTarget(tile);

        if (_grid != null) _grid.ShowPreview(Footprint(tile), _valid);
        RefreshImpact(_targetPos);
    }

    void ClearTarget()
    {
        if (!_hasTarget) return;
        _hasTarget = false;
        _valid     = false;
        if (_grid != null) _grid.HidePreview();
    }

    List<Vector2Int> Footprint(Vector2Int tile)
    {
        _footprint[0] = tile;
        return _footprint;
    }

    // 이전 대상 타일 유효성 — 건물 배치와 같은 기준(존/바닥/점유/평탄) + 기존 건물·청사진 위 금지.
    bool IsValidTarget(Vector2Int tile)
    {
        var map = MapManager.Instance;
        if (!map.IsBuildable(Footprint(tile))) return false;

        foreach (var bp in BuildingBlueprint.All)
            if (bp != null && map.WorldToTile(bp.transform.position) == tile) return false;

        // 바닥(substrate)은 밑에 깔리는 것이므로 겹쳐도 무방.
        foreach (var pb in PlacedBuilding.All)
            if (pb != null && (pb.Data == null || !pb.Data.IsFloor)
                && map.WorldToTile(pb.transform.position) == tile) return false;

        return true;
    }

    // 후보 위치로 옮겼을 때 휴면/재가동될 건물 수 — 이전 전에 영향 범위를 보여준다.
    void RefreshImpact(Vector3 candidate)
    {
        _dormantCount = 0;
        _wakeCount    = 0;

        foreach (var pb in PlacedBuilding.All)
        {
            if (pb == null || (pb.Data != null && pb.Data.IsFloor)) continue;
            var dorm = pb.GetComponent<BuildingDormancy>();
            if (dorm == null) continue;   // 휴면 대상이 아닌 건물(사전 배치 등)은 집계 제외

            bool willBeIn = _core.WouldBeInRangeAt(candidate, pb.transform.position);
            if (willBeIn  && dorm.IsDormant)  _wakeCount++;
            if (!willBeIn && !dorm.IsDormant) _dormantCount++;
        }
    }

    void Confirm()
    {
        _core.RelocateTo(_targetPos);   // 위치 이동 + RaiseTerritoryChanged → 휴면 재평가
        Debug.Log($"[CoreRelocator] 핵 이전 — {_tile} (휴면 {_dormantCount} / 재가동 {_wakeCount})");
        Cancel();
    }

    // ── 표시 ───────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        string impact = _hasTarget
            ? (_valid ? $"  휴면 예정 {_dormantCount} / 재가동 {_wakeCount}" : "  (배치 불가 지점)")
            : "";
        GUI.color = _valid ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.7f, 0.7f);
        GUI.Label(new Rect(10, 10, 700, 24),
            $"[핵 이전] 좌클릭=확정  우클릭/Esc=취소{impact}");
        GUI.color = Color.white;
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main) return;
        DrawPreviewRange();
    }

    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline != null) return;
        DrawPreviewRange();
    }

    // 이전 후 메인 핵 영역(반경)을 후보 위치에 미리 그린다 — 어떤 건물이 들고 나는지 눈으로 확인용.
    void DrawPreviewRange()
    {
        if (!_hasTarget || _core == null || _core.Data == null || _mat == null) return;

        float radius = _core.Data.BaseRadius;
        var   center = new Vector3(_targetPos.x, 0.03f, _targetPos.z);
        var   color  = _valid ? new Color(0.4f, 1f, 0.5f, 0.8f) : new Color(1f, 0.4f, 0.4f, 0.8f);

        _mat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINE_STRIP);
        GL.Color(color);
        const int segments = 64;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            GL.Vertex3(center.x + Mathf.Cos(angle) * radius, center.y, center.z + Mathf.Sin(angle) * radius);
        }
        GL.End();
        GL.PopMatrix();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 다 지은 건물을 다른 자리로 옮긴다. `UnitHPUI`의 [이동한다] → `Begin(pb)` →
// 커서 위치 미리보기(발자국 + 유효성) → 좌클릭 확정 / 우클릭·Esc 취소.
//
// **`CoreRelocator`와 나란한 물건이지 그것의 일반화가 아니다.** 핵은 한 칸짜리고 옮기면
// **영역**이 따라 움직여(휴면/재가동 예정 수를 미리 세어 보여준다) 옮기는 값이 전혀 다르다.
// 여기서 재는 것은 발자국이 들어가느냐 하나뿐이라, 억지로 한 클래스에 넣으면 둘 다 읽기 어려워진다.
//
// 씬 배치 불필요: `Begin`이 전용 GameObject를 만들고 확정/취소 시 스스로 파괴한다.
public class BuildingRelocator : MonoBehaviour
{
    public static BuildingRelocator Instance { get; private set; }
    public static bool IsActive => Instance != null;

    PlacedBuilding _target;
    GridRenderer   _grid;
    Camera         _cam;

    readonly List<Vector2Int> _preview = new();

    Vector2Int _tile;
    Vector3    _targetPos;
    Quaternion _rot;
    bool       _hasTarget;
    bool       _valid;
    string     _reason = "";

    // ── 진입/종료 ──────────────────────────────────────────────────────────────

    public static void Begin(PlacedBuilding pb)
    {
        if (pb == null || pb.Data == null || BuildModeManager.IsActive) return;

        // 이미 이전 모드면 대상만 갈아끼운다(재생성 시 파괴 지연으로 미리보기가 꼬인다).
        if (Instance != null) { Instance._target = pb; Instance.ClearTarget(); return; }

        var go = new GameObject("[BuildingRelocator]");
        go.AddComponent<BuildingRelocator>()._target = pb;
    }

    // `Instance`는 여기서 안 비운다(`OnDestroy`가 정리) — `Destroy`는 프레임 끝에 반영되므로
    // 확정/취소한 그 프레임 동안 `IsActive`가 유지돼야 같은 클릭이 유닛 선택으로 새지 않는다.
    public static void Cancel()
    {
        if (Instance != null) Destroy(Instance.gameObject);
    }

    void Awake()
    {
        Instance = this;
        _cam  = Camera.main;
        _grid = FindFirstObjectByType<GridRenderer>();
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        // `?.`가 아니라 유니티 널 비교 — 씬 언로드 시 이미 파괴된 것을 건드리면 예외.
        if (_grid != null) _grid.HidePreview();
        if (Instance == this) Instance = null;
    }

    void OnBuildModeChanged(bool active) { if (active) Cancel(); }

    // ── 입력/미리보기 ──────────────────────────────────────────────────────────

    void Update()
    {
        // 옮기려던 건물이 그새 없어졌으면(철거·휴면 파괴) 조용히 나간다.
        if (_target == null || _target.Data == null || MapManager.Instance == null) { Cancel(); return; }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        if (Mouse.current == null) return;
        if (Mouse.current.rightButton.wasPressedThisFrame) { Cancel(); return; }

        UpdateTarget();

        if (_valid && Mouse.current.leftButton.wasPressedThisFrame) Confirm();
    }

    void UpdateTarget()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearTarget();
            return;
        }

        if (_cam == null) _cam = Camera.main;      // Awake 시점에 메인 카메라가 없었던 경우
        if (_cam == null) { ClearTarget(); return; }

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 point;
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            point = hit.point;
        // 공허(던전 씬): 콜라이더가 없어도 y=0 평면 교차 — `BuildModeManager`와 같은 폴백.
        else if (new Plane(Vector3.up, 0f).Raycast(ray, out float enter) && enter <= 500f)
            point = ray.GetPoint(enter);
        else { ClearTarget(); return; }

        var map  = MapManager.Instance;
        var tile = map.WorldToTile(point);
        if (_hasTarget && tile == _tile) return;   // 같은 타일이면 재계산 생략

        _tile      = tile;
        _hasTarget = true;
        _rot       = _target.transform.rotation;   // 돌리기는 아직 없다 — 있던 방향 그대로 옮긴다

        // **격자 규약을 그대로 따른다** — 홀수 크기 축은 `.5`, 짝수 축은 `.0`.
        // 안 맞추면 모델이 반 칸 밀려 옆 칸의 작업 자리를 먹는다.
        _targetPos = SnapCenter(tile, _target.Data.Size, _rot);
        _valid     = Validate(out _reason);

        if (_grid != null) _grid.ShowPreview(_preview, _valid);
    }

    void ClearTarget()
    {
        if (!_hasTarget) return;
        _hasTarget = false;
        _valid     = false;
        if (_grid != null) _grid.HidePreview();
    }

    // 커서가 가리키는 칸이 발자국의 **좌하단**이 되게 중심을 잡는다.
    static Vector3 SnapCenter(Vector2Int tile, Vector2Int size, Quaternion rot)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        int quarter = Mathf.RoundToInt(rot.eulerAngles.y / 90f) & 3;
        if (quarter % 2 == 1) (w, h) = (h, w);

        var map = MapManager.Instance;
        float x = tile.x + w * 0.5f;
        float z = tile.y + h * 0.5f;
        return new Vector3(x, map != null ? map.SurfaceHeight(tile) : 0f, z);
    }

    // 옮겨 갈 자리가 되는가. **자기 자신은 장애물로 안 센다** — 지금 서 있는 자리와 겹치는
    // 이동(한 칸 옆으로)이 제 발자국에 걸려 막히면 미세 조정을 아예 못 한다.
    bool Validate(out string reason)
    {
        reason = "";
        var map = MapManager.Instance;

        _preview.Clear();
        foreach (var t in PlacedBuilding.FootprintAt(_targetPos, _target.Data.Size, _rot))
            _preview.Add(t);

        // 자기 칸을 잠시 비워 놓고 묻는다 — `IsBuildable`은 점유를 보는데, 안 비우면 제자리
        // 근처로는 한 칸도 못 옮긴다.
        var mine = new HashSet<Vector2Int>();
        foreach (var t in PlacedBuilding.FootprintAt(_target.transform.position, _target.Data.Size,
                                                     _target.transform.rotation))
            mine.Add(t);

        bool ok = true;
        foreach (var t in _preview)
        {
            if (mine.Contains(t)) continue;
            if (!map.IsBuildable(Single(t))) { ok = false; break; }
        }
        if (!ok) { reason = "배치 불가 지점"; return false; }

        // 다른 건물·청사진 위에는 못 놓는다. 바닥(substrate)은 밑에 깔리는 것이라 겹쳐도 된다.
        foreach (var t in _preview)
        {
            if (mine.Contains(t)) continue;

            if (BuildingBlueprint.PickAt(t) != null) { reason = "청사진이 있다"; return false; }

            foreach (var pb in PlacedBuilding.All)
            {
                if (pb == null || pb == _target) continue;
                if (pb.Data != null && pb.Data.IsFloor) continue;
                foreach (var o in pb.Footprint)
                    if (o == t) { reason = $"{(pb.Data != null ? pb.Data.BuildingName : pb.name)}이(가) 있다"; return false; }
            }
        }

        // **핵 영역 밖으로는 옮길 수 있다** — 그러면 휴면할 뿐이고, 그건 배치 규칙이 아니라
        // 결과다(건설은 영역 안만 되지만, 이미 지은 것을 옮기는 건 다른 축이다).
        return true;
    }

    static readonly List<Vector2Int> _one = new() { Vector2Int.zero };
    static List<Vector2Int> Single(Vector2Int t) { _one[0] = t; return _one; }

    void Confirm()
    {
        _target.MoveTo(_targetPos, _rot);
        Cancel();
    }

    // ── 표시 ───────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        string name = _target != null && _target.Data != null ? _target.Data.BuildingName : "건물";
        string tail = _hasTarget ? (_valid ? "" : $"  ({_reason})") : "";
        GUI.color = _valid ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.7f, 0.7f);
        GUI.Label(new Rect(10, 10, 700, 24), $"[{name} 이전] 좌클릭=확정  우클릭/Esc=취소{tail}");
        GUI.color = Color.white;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance._active;
    public static event Action<bool> OnActiveChanged;


    public enum PlacementMode { Grid, Free }

    [Header("참조")]
    [SerializeField] GridOverlay  gridOverlay;
    [SerializeField] GridRenderer gridRenderer;

    [Header("배치")]
    [SerializeField] GameObject    fallbackPrefab;

    [Tooltip("비우면 Resources/Data/Buildings 의 **전부**를 쓴다(기본). " +
             "채우면 그게 이긴다 — 특정 씬에서 일부만 짓게 하고 싶을 때의 큐레이션용.")]
    [SerializeField] BuildingData[] placeableBuildings;

    [SerializeField] LayerMask     groundMask = ~0;

    [Header("회전")]
    [SerializeField] float freeYawStep = 15f;
    [SerializeField] float gridYawStep = 90f;

    [Header("자유 배치")]
    [SerializeField] Vector3 overlapHalfExtents = new Vector3(0.45f, 0.5f, 0.45f);

    bool          _active;
    bool          _previewShown;
    PlacementMode _mode = PlacementMode.Grid;
    Vector2Int    _hoveredTile;
    int           _selectedIndex;
    float         _freeYaw;
    float         _gridYaw;
    readonly List<Vector2Int> _footprint = new() { Vector2Int.zero };
    Camera _cam;

    // ── 철거 모드 ──────────────────────────────────────────────────────────────
    bool    _demolish;
    bool    _dragging;
    Vector2 _dragStart;
    readonly HashSet<PlacedBuilding>    _pending   = new();
    // **청사진도 같은 모드로 지운다** — 지우는 길이 둘이면 하나가 어긋난다. 다만 결과는
    // 다르다: 완성 건물은 부수는 것이라 75%, 청사진은 무효화라 전액 환불(`Cancel`).
    readonly HashSet<BuildingBlueprint> _pendingBp = new();

    GameObject _ghost;
    GameObject _ghostSourcePrefab;
    float      _ghostHover;   // 현재 고스트 프리팹의 저작 부양 높이

    // ── 건설 목록 ─────────────────────────────────────────────────────────────
    // **목록은 레지스트리에서 끌어온다.** 예전엔 이 씬 배열이 정본이었는데 두 가지가 나빴다:
    //   ① 씬에 사는 목록이라 에디터 도구가 등록해도 **씬을 저장 안 하면 조용히 날아갔다**
    //      (2026-07-31에 15건이 그렇게 사라졌다).
    //   ② 모드가 건물을 추가할 진입점이 없었다.
    // `Resources/Data/Buildings/`에 에셋을 두면 그것만으로 목록에 오른다.
    //
    // 인스펙터 배열을 채우면 그게 이긴다 — `ResearchTable.availableResearch`와 같은
    // "데이터가 없으면 기본 동작" 관례(큐레이션용 오버라이드).
    IReadOnlyList<BuildingData> Buildings =>
        (placeableBuildings != null && placeableBuildings.Length > 0)
            ? placeableBuildings
            : DataManager.AllBuildings;

    int TotalCount => Buildings.Count;

    // ── UI용 공개 API ─────────────────────────────────────────────────────────
    public int          TotalBuildingCount => TotalCount;
    public int          SelectedIndex      => _selectedIndex;
    public BuildingData GetBuilding(int i)
    {
        var list = Buildings;
        return (i >= 0 && i < list.Count) ? list[i] : null;
    }

    public void SelectBuilding(int index)
    {
        _selectedIndex = Mathf.Clamp(index, 0, TotalCount - 1);
        _previewShown  = false;
    }

    BuildingData SelectedBuilding => GetBuilding(_selectedIndex);

    string SelectedDisplayName => SelectedBuilding?.BuildingName ?? "?";

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_ghost != null) Destroy(_ghost);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // 글자 단축키(B · X · Tab · Q/E · [ ])는 이름 칸·모달 화면에서 안 받는다(`InputFocus`) — Esc는 받는다.
        bool keys = !InputFocus.KeysBlocked();

        if (keys && Keyboard.current.bKey.wasPressedThisFrame)
            SetActive(!_active);

        if (Keyboard.current.escapeKey.wasPressedThisFrame && _active)
            SetActive(false);

        if (!_active) return;

        if (keys && Keyboard.current.xKey.wasPressedThisFrame)
            ToggleDemolish();

        if (_demolish) { UpdateDemolish(); return; }

        if (keys)
        {
            if (Keyboard.current.tabKey.wasPressedThisFrame)
                TogglePlacementMode();

            if (_mode == PlacementMode.Free)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) _freeYaw -= freeYawStep;
                if (Keyboard.current.eKey.wasPressedThisFrame) _freeYaw += freeYawStep;
            }
            else
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) _gridYaw -= gridYawStep;
                if (Keyboard.current.eKey.wasPressedThisFrame) _gridYaw += gridYawStep;
            }

            UpdateLevelKeys();
        }
        UpdatePreview();
    }

    // 격자에 스냅시킬 수 있나. **세계 원점 기준으로 줄을 세우지 않는다**(2026-08-27) —
    // 근거가 없고 무엇보다 **거점이 제 격자에 안 맞게 된다.**
    //
    // 처음엔 그래서 *"핵을 꽂기 전에는 자유 배치뿐"*으로 두었는데, 그것은 **두 번 클릭으로
    // 줄·면을 한 번에 까는 길까지 같이 막았다**(2026-08-28에 되돌렸다) — 바닥 한 장을 깔려고
    // 백 번을 누르게 되는 쪽이 훨씬 큰 불편이다. 이제 커서가 위상을 **제안**하고
    // **첫 격자 조작이 그것을 붙든다**(`MapManager.ProposeGrid`/`ClaimGrid`).
    public static bool GridAvailable =>
        MapManager.Instance == null || MapManager.Instance.HasGrid;

    // **버튼도 이 문으로 온다**(`ModeBarUI`).
    // **이름이 물음에 답해야 한다**(2026-09-02). `ToggleBuildMode`는 이름만 보면
    // *"건설 모드를 켜고 끈다"*인데 실제로는 **격자/자유 배치를 뒤집는다** — 그래서
    // `ModeBarUI`의 `건설` 버튼이 이걸 불렀고, **눌러도 아무 일도 안 일어났다**
    // (게다가 `GridAvailable`이 아니면 그대로 돌아간다). 이름을 물음에 맞춘다.
    // **`Tab`이 하는 그것** — 한때 `ToggleMode`라는 안쪽 이름에 이 이름의 별칭만 얹혀 있었다.
    public void TogglePlacementMode()
    {
        if (!GridAvailable)
        {
            _mode = PlacementMode.Free;
            return;   // 바꿀 데가 없다 — 알림은 HUD가 이미 하고 있다
        }
        _mode = _mode == PlacementMode.Grid ? PlacementMode.Free : PlacementMode.Grid;
        _previewShown = false;
        ClearLineAnchor();
        gridRenderer.HidePreview();
    }

    // 건설 모드를 켜고 끈다 — **`B`가 하는 그것**(`Update`의 `bKey`와 같은 문).
    // 버튼과 키가 같은 상태를 만져야 *"버튼으로는 켜지는데 키로는 안 꺼진다"*가 안 생긴다.
    public void ToggleBuildMode() => SetActive(!_active);

    // **줄 긋기 기준점은 모드를 벗어나면 버린다.**
    //
    // 안 버리면 기준을 찍어 둔 채 Esc로 나갔다 B로 다시 들어왔을 때, **첫 클릭이 옛 기준부터
    // 지금 커서까지 통째로 짓는다** — 시킨 적 없는 스무 칸이 한 번에 선다. 건설 모드를
    // 껐다 켜는 것·철거로 넘어가는 것·고른 건물을 바꾸는 것 모두 "그 줄은 없던 일"이다.
    // 긋던 줄을 버린다 — 기준 칸과 **그 배치에 딸린 높이 둘**을 같이 놓는다.
    //
    // `_levelLock`(`[` `]`로 고른 단)도 여기서 푼다. 안 풀었더니 **한 번 올린 단이 계속
    // 따라다녀서**, 저 멀리 다른 자리에 깔 때도 그 높이로 깔렸다. 높이를 고르는 일은
    // *"지금 놓는 이것을 어디에 붙일까"*라서 그 배치보다 오래 살 이유가 없다 —
    // 이어서 옆에 더 깔 때는 **자동 규칙이 방금 깐 것을 물려받으므로** 다시 누를 일도 없다.
    void ClearLineAnchor()
    {
        _lineAnchor  = null;
        _levelFrozen = null;
        _levelLock   = null;
    }

    // ── 철거 모드 ──────────────────────────────────────────────────────────────

    void ToggleDemolish()
    {
        _demolish = !_demolish;
        ClearPending();
        ClearLineAnchor();
        _dragging = false;
        HideGhost();
        gridRenderer.HidePreview();
        _previewShown = false;
    }

    void UpdateDemolish()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // UI 위에서는 철거 안 함
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearPending();
            return;
        }

        Vector2 cur = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragStart = cur;
            _dragging  = false;
        }

        if (mouse.leftButton.isPressed && !_dragging &&
            Vector2.Distance(_dragStart, cur) > 8f)
            _dragging = true;

        // 예정 표시 갱신: 드래그 중이면 박스 내부, 아니면 커서 위 단일.
        RefreshPending(cur);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            ExecuteDemolish();
            _dragging = false;
        }
    }

    // 헐릴 예정인 바닥 칸. **바닥은 오브젝트가 아니라 칸이라** 건물·청사진과 따로 든다.
    readonly List<Vector2Int> _pendingFloor = new();

    void RefreshPending(Vector2 cur)
    {
        var next   = new HashSet<PlacedBuilding>();
        var nextBp = new HashSet<BuildingBlueprint>();

        if (_dragging)
        {
            float minX = Mathf.Min(_dragStart.x, cur.x), maxX = Mathf.Max(_dragStart.x, cur.x);
            float minY = Mathf.Min(_dragStart.y, cur.y), maxY = Mathf.Max(_dragStart.y, cur.y);

            bool InBox(Vector3 world)
            {
                var sp = _cam.WorldToScreenPoint(world);
                return sp.z >= 0f && sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY;
            }

            foreach (var pb in PlacedBuilding.All)
                if (pb != null && InBox(pb.transform.position)) next.Add(pb);

            foreach (var bp in BuildingBlueprint.All)
                if (bp != null && InBox(bp.transform.position)) nextBp.Add(bp);
        }
        else
        {
            var hit = PlacedBuilding.PickUnderCursor(_cam, cur);
            if (hit != null) next.Add(hit);

            // **청사진은 콜라이더로 못 집는다** — 고스트가 콜라이더를 다 꺼 두었기 때문이다
            // (청사진은 통행을 안 막고, 켜면 선택·이동 명령의 레이캐스트에 걸린다).
            // 커서 아래 지면 지점을 타일로 바꿔 그 칸의 청사진을 찾는다.
            //
            // 완성 건물이 잡혔으면 안 찾는다 — 다 지어진 건물이 청사진 자리에 겹쳐 있을 리
            // 없지만, 겹쳤다면 눈에 보이는 쪽(실체)이 이기는 게 맞다.
            if (hit == null && TryCursorGround(cur, out var point) && MapManager.Instance != null)
            {
                var bp = BuildingBlueprint.PickAt(MapManager.Instance.WorldToTile(point));
                if (bp != null) nextBp.Add(bp);
            }
        }

        // diff: 빠진 건 해제, 새로 든 건 표시
        foreach (var pb in _pending)
            if (pb != null && !next.Contains(pb)) pb.SetPendingHighlight(false);
        foreach (var pb in next)
            if (!_pending.Contains(pb)) pb.SetPendingHighlight(true);

        foreach (var bp in _pendingBp)
            if (bp != null && !nextBp.Contains(bp)) bp.SetPendingHighlight(false);
        foreach (var bp in nextBp)
            if (!_pendingBp.Contains(bp)) bp.SetPendingHighlight(true);

        _pending.Clear();
        foreach (var pb in next) _pending.Add(pb);

        _pendingBp.Clear();
        foreach (var bp in nextBp) _pendingBp.Add(bp);

        // **건물을 집었으면 그 밑 바닥은 안 건드린다** — 한 번 누른 것이 두 가지를 헐면
        // 되돌릴 수 없는 쪽이 딸려 온다. 박스로 끌 때는 둘 다 헌다(그게 박스의 뜻이다).
        RefreshPendingFloor(cur, skip: !_dragging && next.Count > 0);
    }

    // 커서·박스 아래의 바닥 칸을 고른다.
    //
    // **건물처럼 화면 좌표로 훑을 수가 없다** — 바닥은 오브젝트가 아니라서 훑을 목록이
    // `_floored` 전체이고, 그건 수천 칸이 될 수 있다. 대신 **박스 네 귀퉁이를 지면으로
    // 쏘아 월드 범위를 얻고** 그 안의 칸만 본다 — 훑는 양이 화면에 든 만큼으로 묶인다.
    void RefreshPendingFloor(Vector2 cur, bool skip)
    {
        bool had = _pendingFloor.Count > 0;
        _pendingFloor.Clear();
        if (had) gridRenderer.HidePreview();

        var map = MapManager.Instance;
        if (map == null || skip) return;

        if (!_dragging)
        {
            if (TryCursorGround(cur, out var p))
            {
                var t = map.WorldToTile(p);
                if (map.IsFloored(t) && !Floor.IsAuthored(t)) _pendingFloor.Add(t);
            }
        }
        else
        {
            float minX = Mathf.Min(_dragStart.x, cur.x), maxX = Mathf.Max(_dragStart.x, cur.x);
            float minY = Mathf.Min(_dragStart.y, cur.y), maxY = Mathf.Max(_dragStart.y, cur.y);

            // 네 귀퉁이를 지면으로 쏜다. 하나라도 허공이면(하늘을 물고 있으면) 포기한다 —
            // 짐작해서 범위를 넓히면 화면 밖 바닥까지 헐린다.
            if (!TryCursorGround(new Vector2(minX, minY), out var a) ||
                !TryCursorGround(new Vector2(maxX, minY), out var b) ||
                !TryCursorGround(new Vector2(minX, maxY), out var c) ||
                !TryCursorGround(new Vector2(maxX, maxY), out var d)) return;

            var lo = map.WorldToTile(new Vector3(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)), 0f,
                                                 Mathf.Min(Mathf.Min(a.z, b.z), Mathf.Min(c.z, d.z))));
            var hi = map.WorldToTile(new Vector3(Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)), 0f,
                                                 Mathf.Max(Mathf.Max(a.z, b.z), Mathf.Max(c.z, d.z))));

            for (int x = lo.x; x <= hi.x; x++)
            for (int z = lo.y; z <= hi.y; z++)
            {
                var t = new Vector2Int(x, z);
                if (map.IsFloored(t) && !Floor.IsAuthored(t)) _pendingFloor.Add(t);
            }
        }

        // 헐릴 칸을 빨갛게. 배치 미리보기와 **같은 표시**를 쓴다 — 두 벌을 만들면
        // 색과 높이가 갈린다.
        if (_pendingFloor.Count > 0) gridRenderer.ShowPreview(_pendingFloor, false);
    }

    void ExecuteDemolish()
    {
        foreach (var pb in _pending)
            if (pb != null) pb.Demolish(); // 파괴되면서 All/하이라이트도 정리됨
        _pending.Clear();

        foreach (var bp in _pendingBp)
            if (bp != null) bp.Cancel();   // 납품된 자재는 전액 창고로
        _pendingBp.Clear();

        // **바닥은 칸을 지우는 것이 곧 철거다.** `FloorRenderer`가 그 청크만 다시 굽는다.
        var map = MapManager.Instance;
        if (map != null)
            foreach (var t in _pendingFloor) map.UnregisterFloor(t);
        _pendingFloor.Clear();
        gridRenderer.HidePreview();
    }

    void ClearPending()
    {
        if (_pendingFloor.Count > 0) { _pendingFloor.Clear(); gridRenderer.HidePreview(); }

        foreach (var pb in _pending)
            if (pb != null) pb.SetPendingHighlight(false);
        _pending.Clear();

        foreach (var bp in _pendingBp)
            if (bp != null) bp.SetPendingHighlight(false);
        _pendingBp.Clear();
    }

    void OnGUI()
    {
        if (!_active) return;

        if (_demolish)
        {
            GUI.color = new Color(1f, 0.7f, 0.7f, 1f);
            GUI.Label(new Rect(10, 10, 600, 24),
                "[철거] 클릭=단일 / 드래그=일괄 (자재 전액 환불)  X=철거 끄기  Esc=나가기");
            GUI.color = Color.white;
            DrawDemolishBox();
            return;
        }

        bool grid = _mode == PlacementMode.Grid && GridAvailable;
        float yaw = grid ? _gridYaw : _freeYaw;
        bool locked = MapManager.Instance == null || MapManager.Instance.GridLocked;
        string modeLabel = grid ? (locked ? $"그리드 ({yaw:F0}°)"
                                          : $"그리드 ({yaw:F0}°) — 처음 놓는 자리가 격자를 정한다")
                                : $"자유 ({yaw:F0}°)";
        string capInfo   = "";
        var core = DungeonCore.Instance;
        if (core != null)
            capInfo = $"  하수인: {core.CurrentMinionCount}/{core.TotalMinionCap}";
        // **기단은 높이가 곧 결정이다** — 어느 높이에 깔릴지가 화면에 없으면 `[` `]`를
        // 눌러도 무엇이 달라졌는지 알 수 없다. 잡았는지 자동인지도 같이 띄운다.
        string levelInfo = LevelFloorSelected
            ? $"  {(StairsSelected ? "계단 꼭대기" : "기단")} {LevelY:0.00}m " +
              $"({(_levelLock.HasValue ? "고정" : _levelFrozen.HasValue ? "기준" : "자동")}" +
              $"{(_levelNear.Count > 1 ? $", 이웃 {_levelNear.Count}단" : "")}, [ ]=높이 Backspace=자동)" +
              (StairsSelected ? $"  ↓{StairRise:0.00}m/칸 — 높은 쪽에서 끌 것" : "")
            : "";

        GUI.Label(new Rect(10, 10, 1100, 24),
            $"[건설] {SelectedDisplayName} ({_selectedIndex + 1}/{TotalCount})  모드: {modeLabel}  Tab=모드 Q/E=회전 X=철거{levelInfo}{capInfo}");

        DrawCursorHint();
    }

    // ── 값과 거절 이유는 **커서 옆에** 띄운다 ────────────────────────────────
    //
    // 화면 왼쪽 위에 적어 두었는데, **눈은 커서에 가 있다**(사용자: 림월드는 커서 쪽에
    // 빨갛게 뜬다). 놓을 자리를 고르는 내내 시선이 지도 한복판인데 답은 구석에 있으면,
    // 있어도 못 읽는 안내다.
    //
    // **타일 색을 바꾸지 않는다** — 목록의 아이콘은 *무엇인가*를 말하는 자리다. 거기에
    // *지금 살 수 있나*까지 얹으면 진짜 아이콘 그림이 들어오는 날 그림에 색이 곱해지고,
    // 무엇보다 **커서에서 눈을 떼야 알 수 있다.**
    void DrawCursorHint()
    {
        var bd = SelectedBuilding;
        if (bd == null || Event.current == null) return;

        var at = Event.current.mousePosition + new Vector2(20f, 18f);

        // 값 — 모자라면 빨갛게. **얼마나 있는지도 같이 적는다**(모자란 줄만 알면 얼마를
        // 더 모아야 하는지를 또 찾아야 한다).
        // **재료마다 한 줄이다** — 한 줄로 합치면 넷이 드는 건물에서 *무엇이* 모자란지가
        // 안 보인다(모자란 것만 빨갛게 하려면 줄이 갈려 있어야 한다).
        foreach (var c in bd.Costs)
        {
            if (c.Item == null || c.Amount <= 0) continue;

            int have  = MaterialPool.AvailableHere(c.Item);
            bool poor = have < c.Amount;

            var was = GUI.color;
            GUI.color = poor ? new Color(1f, 0.35f, 0.30f) : new Color(0.92f, 0.92f, 0.92f);
            GUI.Label(new Rect(at.x, at.y, 420f, 22f),
                      $"{c.Item.ItemName} {c.Amount}" +
                      (poor ? $" (재료가 충분하지 않음 — {have})" : ""));
            GUI.color = was;
            at.y += 18f;
        }

        // **거절한 이유도 같은 자리다.** 빨간 격자만으로는 손잡이를 어디로 돌릴지 모른다.
        if (_whyNot != null)
        {
            var was = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.45f);
            GUI.Label(new Rect(at.x, at.y, 520f, 22f), $"못 짓는다: {_whyNot}");
            GUI.color = was;
        }
    }

    void DrawDemolishBox()
    {
        if (!_dragging) return;
        Vector2 cur = Mouse.current.position.ReadValue();
        Vector2 s = new(_dragStart.x, Screen.height - _dragStart.y);
        Vector2 e = new(cur.x,        Screen.height - cur.y);
        Rect rect = new(Mathf.Min(s.x, e.x), Mathf.Min(s.y, e.y),
                        Mathf.Abs(s.x - e.x), Mathf.Abs(s.y - e.y));
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
    }

    // 외부에서 건설 모드를 강제 종료 (월드맵 전환 등 — 고스트/미리보기가 남지 않게).
    public void Deactivate()
    {
        if (_active) SetActive(false);
    }

    // **한 번에 하나다** — 근거는 `ExclusiveMode`. 건설도 드래그의 뜻을 바꾸는 상태라
    // 채굴·운반·구역과 같은 자리에 선다.
    void OnEnable()  => ExclusiveMode.Claimed += OnOtherClaimed;
    void OnDisable() => ExclusiveMode.Claimed -= OnOtherClaimed;

    void OnOtherClaimed(object who) { if (!ReferenceEquals(who, this) && _active) SetActive(false); }

    void SetActive(bool active)
    {
        _active = active;
        if (active) ExclusiveMode.Claim(this);
        OnActiveChanged?.Invoke(active);
        gridOverlay.SetVisible(active);
        if (!active)
        {
            gridRenderer.HidePreview();
            _previewShown = false;
            HideGhost();
            ClearPending();
            ClearLineAnchor();
            _demolish = false;
            _dragging = false;
            _levelLock = null;
        }
    }

    // ── 표면 스냅 ──────────────────────────────────────────────────────────────

    // 프리팹 피벗이 중앙/임의 위치여도 **렌더러 바닥면**이 표면(surfaceY)에 닿도록 Y를 보정한다.
    // 피벗 규칙을 프리팹마다 맞추지 않아도 되고, 바닥 슬래브 두께도 surfaceY에 이미 반영돼 있음.
    // (surfaceY = MapManager.SurfaceHeight — 바닥이 있으면 그 윗면, 없으면 지형 높이)
    // 프리팹에 저작된 "부양 높이" — 루트 localPosition.y. 서브코어처럼 일부러 띄워 만든 프리팹은
    // 이 값이 양수다. Instantiate(prefab, pos, rot)는 이 Y를 덮어써 버리므로 배치 시 다시 얹어준다.
    public static float AuthoredHover(GameObject prefab) =>
        prefab != null ? Mathf.Max(0f, prefab.transform.localPosition.y) : 0f;

    // 표면 위에 스폰. 저작된 부양 높이가 있으면 그만큼 띄우고(그 프리팹은 바닥 스냅 안 함),
    // 없으면 렌더러 바닥면을 표면에 맞춘다.
    public static GameObject SpawnOnSurface(GameObject prefab, Vector3 surfacePos, Quaternion rot)
    {
        float hover = AuthoredHover(prefab);
        if (hover > 0f)
            return Instantiate(prefab, surfacePos + Vector3.up * hover, rot);

        var go = Instantiate(prefab, surfacePos, rot);
        SnapOnSurface(go, surfacePos.y);
        return go;
    }

    public static void SnapOnSurface(GameObject go, float surfaceY)
    {
        if (go == null) return;

        bool  found = false;
        var   bounds = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            // 파티클/트레일 등은 바운드가 실제 형상과 무관해 제외 — 메시 계열만 사용.
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        if (!found) return;   // 렌더러가 없으면 피벗 그대로 (보정 근거 없음)

        // 올리기만 한다 — 메시가 피벗보다 위에 저작된(일부러 띄운) 프리팹을 끌어내리지 않도록.
        float dy = surfaceY - bounds.min.y;
        if (dy > 0.0001f)
            go.transform.position += Vector3.up * dy;
    }

    // 렌더러 **윗면**을 주어진 Y에 맞춘다 — `SnapOnSurface`의 짝이다(저쪽은 아랫면).
    //
    // **양방향으로 움직인다**(저쪽은 올리기만 한다) — 평탄 기단은 이어 붙은 칸이 **정확히
    // 같은 윗면**을 가져야 하고, 그 윗면은 지면보다 아래일 수도 위일 수도 있다. 두께를
    // 알 필요가 없는 것이 요점이다: 슬래브가 얼마나 두꺼운지는 프리팹이 알고, 우리는
    // **딛는 면이 어디인가**만 안다.
    public static void AlignTopTo(GameObject go, float top)
    {
        if (!TryMeshBounds(go, out var bounds)) return;
        go.transform.position += Vector3.up * (top - bounds.max.y);
    }

    // 프리팹의 재질. 바닥 청크 메시가 이것을 쓴다.
    static Material MaterialOf(GameObject prefab)
    {
        if (prefab == null) return null;
        foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterial != null) return r.sharedMaterial;
        return null;
    }

    // 슬래브 두께(m). **딛는 면은 지면이 아니라 슬래브 윗면**이라, 등록할 높이에 이만큼
    // 얹어야 그 위에 놓는 건물이 안 파묻힌다. 프리팹이 답하므로 상수로 박지 않는다.
    static float SlabThickness(GameObject prefab)
    {
        if (prefab == null) return 0f;

        float best = 0f;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            // 프리팹 에셋이라 `Renderer.bounds`(월드)는 못 믿는다 — 메시 크기에 스케일을 곱한다.
            float h = mf.sharedMesh.bounds.size.y * mf.transform.lossyScale.y;
            if (h > best) best = h;
        }
        return best;
    }

    // 파티클/트레일은 바운드가 실제 형상과 무관하다 — 메시 계열만 센다.
    static bool TryMeshBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        if (go == null) return false;

        bool found = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    // ── 고스트 미리보기 ────────────────────────────────────────────────────────

    void EnsureGhost()
    {
        var prefab = ResolvePrefab();
        if (prefab == null) { HideGhost(); return; }
        if (_ghost != null && _ghostSourcePrefab == prefab) return;

        if (_ghost != null) Destroy(_ghost);
        ClearLineGhosts();   // 여벌은 이것의 복제라 원본이 바뀌면 같이 버린다
        ClearLineAnchor();   // 다른 건물을 고른 것은 긋던 줄을 버린다는 뜻이다
        _levelLock = null;   // 잡아 둔 높이도 그 건물의 것이었다

        // 비활성 컨테이너 아래에서 인스턴스화 → 자식의 Awake/OnEnable이 유예된다(Unity는 비활성
        // 계층의 스크립트를 깨우지 않음). 이 상태에서 스크립트를 제거하면, 이후 ShowGhost가
        // 활성화해도 창고 등록·이벤트 구독 등 부수효과가 전혀 실행되지 않는 '순수 시각용' 고스트가 된다.
        // (기존엔 활성 프리팹을 Instantiate → Awake/OnEnable이 한 프레임 실행된 뒤에야 비활성화.)
        _ghost = new GameObject("[BuildGhost]");
        _ghost.SetActive(false);

        var model = Instantiate(prefab, _ghost.transform);
        // 저작된 부양 높이를 그대로 재현 — 미리보기와 실제 배치 높이를 일치시킨다.
        _ghostHover = AuthoredHover(prefab);
        model.transform.localPosition = new Vector3(0f, _ghostHover, 0f);
        model.transform.localRotation = Quaternion.identity;
        _ghostSourcePrefab = prefab;

        // 비활성이라 아직 Awake 전 — 지금 제거하면 활성화돼도 영영 실행되지 않는다(렌더러/메시는 남음).
        StripScripts(model);
        foreach (var c in model.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        ShowWorkSpotOnGhost(model);
    }

    // **스크립트를 뗀다 — 다만 순서를 지키고, 모양을 정하는 것은 남긴다**(2026-09-04).
    //
    // 전에는 *"배치 대상 건물 컴포넌트는 상호 RequireComponent가 없어 제거 순서 무관"*이라고
    // 적고 그냥 훑었다. **그 전제는 아무도 안 지키고 있었고, 벽걸이 횃불에서 깨졌다** —
    // 유니티가 *"Can't remove LightSource because WallTorch depends on it"*을 매 프레임 뱉었다
    // (고스트는 커서를 움직일 때마다 다시 만들어진다). 전제를 적어 두는 것으로는 안 지켜진다.
    //
    // **요구받는 것은 나중에 지운다** — 요구하는 쪽이 먼저 사라지면 그때는 지울 수 있다.
    // 진전이 없으면 멈춘다(서로 요구하는 고리는 그냥 남긴다 — 지우려다 에러를 뱉느니
    // 남는 편이 낫고, 어차피 그 아래에서 콜라이더가 꺼지고 활성화도 이 뒤다).
    //
    // **`IPlacementPose`는 지우지 않고 끈다** — 놓일 자리에 따라 모양이 갈리는 건물은
    // 미리보기에서도 그 모양이어야 한다(그게 안 되면 *짓고 나서야 어느 쪽인지 알게 된다*).
    // 꺼 두므로 `OnEnable`이 안 돌고, 그래서 **부수효과를 막는다는 이 함수의 뜻은 그대로다.**
    //
    // **`keep`** — 남길 것을 부르는 쪽이 고른다(2026-09-20, 캐릭터 생성 화면의 미리보기 폰: 모습을 정하는 `PawnLook`·
    // 얼굴 판은 남기고 뇌·이동은 뗀다). 같은 일을 두 벌로 짓지 않으려고 여기를 넓혔다 — 순서 규칙이 한 곳에 있다.
    public static void StripScripts(GameObject model, System.Func<MonoBehaviour, bool> keep = null)
    {
        var left = new List<MonoBehaviour>();
        foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b == null) continue;
            if (keep != null && keep(b)) continue;
            if (b is IPlacementPose) { b.enabled = false; continue; }
            left.Add(b);
        }

        bool progress = true;
        while (left.Count > 0 && progress)
        {
            progress = false;
            for (int i = left.Count - 1; i >= 0; i--)
            {
                var b = left[i];
                if (b != null && IsRequiredBySibling(b)) continue;

                if (b != null) DestroyImmediate(b);
                left.RemoveAt(i);
                progress = true;
            }
        }
    }

    // 같은 오브젝트의 다른 컴포넌트가 `[RequireComponent]`로 이것을 붙들고 있나.
    static bool IsRequiredBySibling(MonoBehaviour b)
    {
        var mine = b.GetType();
        foreach (var other in b.GetComponents<Component>())
        {
            if (other == null || other == b) continue;

            foreach (RequireComponent rq in
                     other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                if (rq.m_Type0 != null && rq.m_Type0.IsAssignableFrom(mine)) return true;
                if (rq.m_Type1 != null && rq.m_Type1.IsAssignableFrom(mine)) return true;
                if (rq.m_Type2 != null && rq.m_Type2.IsAssignableFrom(mine)) return true;
            }
        }
        return false;
    }

    // **놓일 자리에 따라 모양이 갈리는 건물에게 그 칸을 알려 준다.** 고스트가 옮겨 갈 때마다
    // 물어야 한다 — 한 번만 물으면 커서를 옮겨도 처음 칸의 모양 그대로다.
    static void PoseAt(GameObject root, Vector2Int tile)
    {
        if (root == null) return;
        foreach (var p in root.GetComponentsInChildren<IPlacementPose>(true))
            p.PoseAt(tile);
    }

    // 고스트에서도 **작업 자리 화살표**를 보여준다.
    //
    // 스크립트를 다 떼기 때문에 `WorkSpot`이 스스로 켤 수 없다 — 그런데 정작 표식이 제일
    // 필요한 순간이 **놓기 직전**이다. 완성된 건물에서만 보이면 "막고 나서 알게 되는" 것과 같다.
    // 그래서 이름으로 찾아 직접 켠다(컴포넌트가 없어도 계층은 그대로 남아 있다).
    static void ShowWorkSpotOnGhost(GameObject model)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "WorkSpot") continue;
            t.gameObject.SetActive(true);
            foreach (Transform child in t) child.gameObject.SetActive(true);
        }
    }

    void HideGhost()
    {
        if (_ghost != null) _ghost.SetActive(false);
        HideLineGhosts();
    }

    // ── 줄 긋기의 여벌 고스트 ────────────────────────────────────────────────
    //
    // **지어질 자리마다 모델이 서야 한다.** 초록 격자만으로는 몇 칸인지는 알아도 어떤 모양이
    // 어느 방향으로 설지는 안 보인다 — 담을 두르는 일은 그림이 맞는지가 전부다.
    //
    // 커서 쪽 고스트(`_ghost`) 하나를 **복제해 늘려 쓴다.** 프리팹에서 새로 만들면 스크립트를
    // 떼고 콜라이더를 끄는 준비를 매번 다시 해야 하는데, 그 준비가 끝난 것이 이미 있다.
    readonly List<GameObject> _lineGhosts = new();

    void HideLineGhosts()
    {
        foreach (var g in _lineGhosts)
            if (g != null) g.SetActive(false);
    }

    void ShowLineGhosts(List<Vector2Int> tiles, Quaternion rot)
    {
        EnsureGhost();
        if (_ghost == null || tiles == null) { HideLineGhosts(); return; }

        // 첫 칸은 커서 고스트가 맡는다 — 여벌은 그 나머지(단 `MaxLineGhosts`까지).
        int need = Mathf.Clamp(tiles.Count - 1, 0, MaxLineGhosts);

        while (_lineGhosts.Count < need)
        {
            var clone = Instantiate(_ghost, transform);
            clone.name = $"[BuildGhost.Line{_lineGhosts.Count}]";
            _lineGhosts.Add(clone);
        }

        for (int i = 0; i < _lineGhosts.Count; i++)
        {
            var g = _lineGhosts[i];
            if (g == null) continue;

            if (i >= need) { g.SetActive(false); continue; }

            g.SetActive(true);
            var tile = tiles[i + 1];
            var pos  = FootprintCenter(GetFootprint(tile));
            g.transform.SetPositionAndRotation(pos, rot);
            if (_ghostHover <= 0f) SeatOnLevel(g, pos.y, tile);

            // **여벌도 제 칸을 묻는다** — 안 물으면 줄 전체가 커서 칸의 모양으로 선다
            // (벽을 따라 그으면 끝의 하나만 맞고 나머지는 거짓말이 된다).
            PoseAt(g, tile);
        }
    }

    // 고른 건물이 바뀌면 여벌도 버린다 — 안 버리면 지난 건물의 모델이 줄에 섞인다.
    void ClearLineGhosts()
    {
        foreach (var g in _lineGhosts)
            if (g != null) Destroy(g);
        _lineGhosts.Clear();
    }

    void ShowGhost(Vector3 pos, Quaternion rot, Vector2Int? tile = null)
    {
        EnsureGhost();
        if (_ghost == null) return;
        _ghost.SetActive(true);
        _ghost.transform.SetPositionAndRotation(pos, rot);
        // 부양 프리팹은 이미 모델을 띄워 붙였으므로 스냅하지 않는다(실제 배치와 동일 규칙).
        if (_ghostHover <= 0f) SeatOnLevel(_ghost, pos.y, tile);

        // **자리를 잡은 뒤에 모양을 묻는다** — 회전을 트랜스폼에서 읽으므로 순서가 뒤집히면
        // 지난 프레임의 방향으로 벽을 고른다. 자유 배치에는 칸이 없으니 자리에서 낸다.
        var map = MapManager.Instance;
        if (map != null) PoseAt(_ghost, tile ?? map.WorldToTile(pos));
    }

    // 커서 아래의 지면 지점. 배치 미리보기와 철거 모드의 청사진 픽킹이 **같은 조준**을 쓴다 —
    // 답이 두 벌이면 미리보기가 가리키는 칸과 실제로 집히는 칸이 어긋난다.
    bool TryCursorGround(Vector2 screenPos, out Vector3 point)
    {
        point = default;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(screenPos);

        // **바위는 건너뛴다**(2026-09-02). 바위 콜라이더는 `Ground` 레이어이고(커서가 짚어야
        // 캘 자리를 고른다) 높이가 9.6m다 — 비스듬히 보면 **커서와 바닥 사이의 벽 윗면에
        // 레이가 먼저 맞아**, 눈에는 빈 바닥을 가리키는데 찍히는 칸은 그 벽이 된다
        // (사용자: *"내가 찍는 곳이 정확히 안 찍히고 좀 느리달까"* — 칸이 멀리 튀니 느리게도
        // 보인다). **바위 위에는 어차피 못 짓는다** — 여기서 알고 싶은 것은 *바닥*이다.
        var hits = Physics.RaycastAll(ray, 500f, groundMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (RockRenderer.IsRockCollider(hits[i].collider)) continue;
                point = hits[i].point;
                return true;
            }
        }

        // 공허(던전 씬): 콜라이더가 없어도 y=0 평면 교차로 조준 — 바닥을 공허에 깔 수 있어야 함.
        if (new Plane(Vector3.up, 0f).Raycast(ray, out float enter) && enter <= 500f)
        {
            point = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    void UpdatePreview()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            HideGhost();
            _whyNot = null;   // 미리보기를 거두면 이유도 거둔다 — 안 지우면 옛 이유가 화면에 남는다
            return;
        }

        if (!TryCursorGround(Mouse.current.position.ReadValue(), out var point))
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            HideGhost();
            return;
        }

        // **핵을 놓기 전에도 격자로 짓는다**(2026-08-28). 위상을 첫 핵이 정하기로 한 뒤로
        // 그전에는 격자가 아예 없어서 **자유 배치뿐**이었고, 그러면 두 번 클릭으로 줄·면을
        // 한 번에 까는 길이 통째로 막힌다 — 바닥 한 장을 깔려고 백 번을 눌러야 한다.
        //
        // 그래서 **커서가 정할 위상을 계속 제안해 둔다.** 붙들리기 전까지는 격자가 커서를
        // 따라오지만(어차피 아직 아무것도 안 놓았으니 맞출 것이 없다), **첫 격자 조작이
        // 그 자리에 못을 박는다**(`ClaimGrid` — 아래 두 곳). 그 뒤로는 안 움직인다.
        var map0 = MapManager.Instance;
        if (map0 != null && !map0.GridLocked) map0.ProposeGrid(point);

        // 바닥은 타일 정렬이 필수 — 자유 모드여도 그리드 배치로 강제.
        bool forceGrid = SelectedBuilding != null && SelectedBuilding.IsFloor && GridAvailable;
        if ((_mode == PlacementMode.Grid && GridAvailable) || forceGrid) UpdateGridPreview(point);
        else                                                            UpdateFreePreview(point);
    }

    // 초록 격자를 마지막으로 그렸을 때의 조건. **타일만 보면 안 된다** — 발자국은
    // 회전(3×2 → 2×3)과 고른 건물에 따라서도 바뀐다.
    float _previewYaw;
    int   _previewIndex = -1;

    // ── 두 번 클릭으로 줄·면을 한 번에 ────────────────────────────────────────
    //
    // **한 번 눌러 기준을 잡고, 늘였다 줄였다 하다가, 다시 눌러 짓는다.**
    //
    // 처음엔 좌클릭을 누른 채 끄는 페인트였는데, 그건 **커서가 지나간 자리**를 칠할 뿐이라
    // 곧게 긋기가 어렵고 얼마나 지어질지 놓기 전에는 알 수 없다. 담을 두르는 일은 대개
    // "여기서 저기까지"라 **두 점을 찍는 것**이 맞는 조작이다.
    //
    // **벽은 줄, 바닥은 면.** 벽을 사각으로 채우면 속이 꽉 찬 덩어리가 되어 쓸 일이 없고,
    // 바닥을 줄로만 깔면 방 하나에 열 번을 그어야 한다. 줄은 **더 많이 움직인 축**으로만
    // 곧게 간다(대각선은 벽으로 안 쓴다 — 칸이 모서리로만 붙어 사람이 새어 나간다).
    //
    // **저작이 정한다**(`BuildingData.DragBuild`). 크기로 가르면 우리·작업대·서브코어처럼
    // **잇대어 쓰지 않는 한 칸짜리까지** 줄로 그어진다 — "한 칸이냐"와 "잇대어 쓰느냐"는
    // 다른 물음이다. 여러 칸짜리는 발자국이 겹쳐 어디까지 지어질지 예측이 안 되므로 함께 막는다.
    Vector2Int? _lineAnchor;

    bool CanDragBuild => SelectedBuilding != null
                      && SelectedBuilding.DragBuild
                      && SelectedBuilding.Size.x == 1 && SelectedBuilding.Size.y == 1;

    // 바닥·밭은 면, 그 밖(벽·목책·바리케이드)은 줄. **계단도 줄이다** — 계단은 방향이 있는
    // 물건이라 면으로 그으면 한 줄이 통째로 같은 단이 되어 뜻이 없다.
    //
    // **`IsFloor`가 이 물음을 겸하고 있었다**(2026-09-02에 갈랐다) — 그건 *"바닥 데이터로
    // 깔리는가"*라는 다른 물음이다. 밭은 바닥이 아니지만 면으로 깐다(`AreaDrag`).
    // **잇대어 긋는 것은 줄 방향을 따라 돈다**(2026-09-18, 사용자: *"목책이 한쪽 방향은
    // 맞는데 다른쪽 방향은 회전이 안돼"*).
    //
    // 전에는 줄을 어느 쪽으로 긋든 `_gridYaw`(사람이 `R`로 돌려 둔 각) 하나였다 — 가로로
    // 그으면 맞고 **세로로 그으면 담이 옆을 보고 선다.** 벽이 큐브였을 때는 티가 안 났는데,
    // 울타리처럼 **앞뒤가 있는 모델**이 들어오자 그대로 드러났다.
    //
    // **면으로 까는 것(`RectDrag`)과 회전이 없는 것은 안 건드린다** — 바닥·밭은 방향이 없고,
    // 면에는 *줄 방향*이랄 것도 없다.
    float DragYaw
    {
        get
        {
            if (_lineTiles.Count < 2 || RectDrag) return _gridYaw;

            var a = _lineTiles[0];
            var b = _lineTiles[_lineTiles.Count - 1];

            // 세로로 그었으면 한 번 더 돌린다. **사람이 돌려 둔 각에 더한다** — 덮어쓰면
            // `R`로 맞춰 둔 방향이 줄을 그을 때마다 사라진다.
            bool vertical = Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x);
            return vertical ? _gridYaw + 90f : _gridYaw;
        }
    }

    bool RectDrag => SelectedBuilding != null && !SelectedBuilding.Stairs
                  && (SelectedBuilding.IsFloor || SelectedBuilding.AreaDrag);

    // ── 평탄 기단의 높이 ──────────────────────────────────────────────────────
    //
    // **경사를 따르는 포장과 경사를 무시하는 기단은 높이를 정하는 규칙이 다르다.**
    // 포장은 칸마다 제 지면에 앉으므로 정할 것이 없고, 기단은 **덩어리 하나에 높이 하나**라
    // 그 하나를 누가 정하는지가 전부다. 규칙은 둘이고 순서가 있다:
    //
    //   ① **이어 붙는 쪽이 이긴다.** 옆 칸에 이미 바닥이 있으면 그 윗면을 그대로 쓴다 —
    //      이게 *"따닥따닥 붙는다"*의 뜻이다. 안 하면 이어 붙일 때마다 단이 생겨서,
    //      넓은 자리를 만들려고 여러 번 나눠 까는 순간 계단이 된다.
    //      **여럿이면 가장 높은 것** — 낮은 쪽에 맞추면 높은 이웃이 새 기단을 뚫고 올라온다.
    //   ② 이웃이 없으면 **범위 안 지면의 최고점**에 앉는다. 최저점이나 처음 찍은 칸에
    //      맞추면 언덕 쪽 흙이 슬래브를 뚫고 올라와, 평평하게 만들려던 것이 반쯤 파묻힌다.
    //
    // 정해진 높이는 **윗면(딛는 면)**이다 — 두께는 프리팹이 알고 우리는 안 센다
    // (`AlignTopTo`). ②로 앉힌 첫 칸만은 아직 윗면을 모르므로 지면(`_levelSeed`)에
    // 앉히고, 그 결과로 나온 윗면이 덩어리의 높이가 된다.
    float? _levelTop;
    float  _levelSeed;

    // 지금 발자국이 닿아 있는 이웃 바닥들의 높이(오름차순, 중복 접음). `[` `]`가 여기를 딛는다.
    readonly List<float> _levelNear = new();

    // 두 높이를 같은 것으로 볼 간격(m). 슬래브 두께보다 작아야 한다.
    const float LevelSame = 0.01f;

    // ── 손으로 잡은 높이 ──────────────────────────────────────────────────
    //
    // **자동 규칙만으로는 단을 못 만든다.** 이어 붙는 쪽이 이기므로, 이미 깔린 기단 옆에
    // 붙이면 **언제나 그 높이가 되고**(먼저 만든 것에만 붙는다) 한 단 낮은 마당이나 한 단
    // 높은 단은 만들 방법이 없었다. 자동 규칙이 틀린 것은 아니다 — 대개 이어 붙이고 싶은
    // 것이 맞다. 없던 것은 **그 규칙을 거절하는 길**이다.
    //
    // `[` `]`로 잡는다. **처음 누른 순간 자동 높이에서 시작해 그대로 고정된다** — 상대
    // 보정으로 두면 커서가 이웃을 스칠 때마다 기준이 바뀌어 같은 오프셋이 자리마다 다른
    // 높이를 뜻하게 된다. 고정이면 화면의 숫자가 곧 지어질 높이다.
    //
    // **그 배치가 끝나면 풀린다**(`ClearLineAnchor`) — `Backspace`로 먼저 풀 수도 있다.
    float? _levelLock;

    [Tooltip("`[` `]`로 기단 높이를 조절하는 눈금(m). 둘레에 맞출 기단이 없을 때만 쓴다.")]
    [SerializeField] float levelStep = 0.25f;

    // **계단 한 단은 폰이 오를 수 있는 만큼이다** — 저작하지 않는다. 따로 적으면 언젠가
    // `StepUp`보다 커지고, 그때 **계단은 놓이는데 폰이 못 오르는** 상태가 조용히 생긴다.
    // (`levelStep`과 다른 물음이다: 저쪽은 *"기단 높이를 얼마씩 다듬을까"*이고
    //  이쪽은 *"한 걸음에 얼마나 오를 수 있나"*다.)
    static float StairRise => PawnMover.StepUp;

    [Tooltip("`[` `]`가 높이를 맞춰 줄 기단을 찾는 범위(칸). 발자국 둘레로 이만큼 훑는다.")]
    [SerializeField] int levelSnapRange = 3;

    // 판정과 화면이 함께 쓰는 높이. **한 번 정해지면 그 배치가 끝날 때까지 안 움직인다.**
    //
    // 전에는 `_levelLock ?? _levelTop ?? _levelSeed`를 그때그때 읽었는데, `_levelTop`이
    // **첫 칸을 놓는 순간 지면에서 슬래브 윗면으로 바뀐다**(두께 5cm만큼 올라간다).
    // 그래서 미리보기에서 아슬아슬하게 빨갛던 칸이 배치 도중에는 통과해 **"빨간데
    // 만들어지는"** 일이 생겼다. 검사는 `ResolveLevel`이 뜬 값 하나만 본다.
    float _levelCheck;
    float LevelY => _levelCheck;

    // **기준 칸을 찍는 순간의 높이를 붙든다.**
    //
    // 안 붙들면 자동 규칙이 매 프레임 다시 돈다 — 높은 쪽으로 끌면 *"범위 안 지면의
    // 최고점"*이 따라 올라가고, 다른 기단에 닿으면 그 높이를 물려받는다. 그래서 **끄는
    // 동안 바닥 전체가 위로 자랐다.** 자동 규칙은 *어디서 시작할지*를 고르는 데 쓰는
    // 것이지, 끄는 내내 답을 바꾸라는 뜻이 아니었다.
    //
    // 찍기 전에는 그대로 커서를 따라간다(그게 곧 미리보기다). `[` `]`는 이걸 덮는다.
    float? _levelFrozen;

    void UpdateLevelKeys()
    {
        if (!LevelFloorSelected) { _levelLock = null; return; }

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.backspaceKey.wasPressedThisFrame) { _levelLock = null; return; }

        // **잡는 순간의 기준은 지금 화면에 보이는 높이다** — 안 그러면 첫 한 번이 엉뚱한
        // 데서 출발해 몇 번을 눌러야 할지 알 수 없다.
        if (kb.leftBracketKey.wasPressedThisFrame)  _levelLock = Step(LevelY, down: true);
        if (kb.rightBracketKey.wasPressedThisFrame) _levelLock = Step(LevelY, down: false);
    }

    // 한 칸 옮긴 높이.
    //
    // **닿아 있는 이웃 기단이 있으면 그 높이에 정확히 내려앉는다** — 이으려는 것이므로
    // 눈금으로 더듬어 맞추면 안 된다(0.25m씩 옮겨서는 이웃과 딱 맞을 이유가 없고, 1cm
    // 어긋난 기단은 잇지 않은 것과 같다). 그 방향에 이웃이 없을 때만 눈금으로 움직인다 —
    // 빈 땅에 한 단 낮은 마당을 만드는 것도 같은 키로 되어야 한다.
    float Step(float from, bool down)
    {
        for (int i = down ? _levelNear.Count - 1 : 0;
             i >= 0 && i < _levelNear.Count;
             i += down ? -1 : 1)
        {
            float y = _levelNear[i];
            if (down ? y < from - LevelSame : y > from + LevelSame) return y;
        }
        return down ? from - levelStep : from + levelStep;
    }

    bool LevelFloorSelected => SelectedBuilding != null
                            && SelectedBuilding.IsFloor && SelectedBuilding.LevelFloor;

    bool StairsSelected => LevelFloorSelected && SelectedBuilding.Stairs;

    // 그 칸이 앉을 높이. **계단만 칸마다 다르다** — 기준 칸에서 한 칸 멀어질 때마다 한 단씩
    // 낮아진다. 거리는 기준에서 재므로 **칸을 훑는 순서와 무관하다**(줄은 좌표 순으로
    // 만들어지지 기준에서부터 만들어지지 않는다 — 순서에 기대면 방향이 뒤집힌다).
    //
    // **높은 쪽에서 시작해 낮은 쪽으로 끈다.** 내려가는 쪽만 있는 것이 요점이다 —
    // 올라가는 계단은 위에서부터 그으면 그대로 나온다.
    float LevelForTile(Vector2Int t)
    {
        if (!StairsSelected || !_lineAnchor.HasValue) return LevelY;

        var a = _lineAnchor.Value;
        int d = Mathf.Abs(t.x - a.x) + Mathf.Abs(t.y - a.y);
        return LevelY - d * StairRise;
    }

    // 이만큼 파묻히는 것은 봐준다 — 잔기복까지 거절하면 비탈에서 한 칸도 못 깐다.
    const float LevelBury = 0.15f;

    static readonly Vector2Int[] Neighbors4 =
        { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    void ResolveLevel(List<Vector2Int> tiles)
    {
        // 붙들렸어도 **이웃 목록은 계속 모은다** — 끄는 동안에도 `[` `]`로 옆 기단에
        // 맞출 수 있어야 한다.
        ResolveLevelCore(tiles);
        _levelCheck = _levelLock ?? _levelFrozen ?? _levelTop ?? _levelSeed;
    }

    void ResolveLevelCore(List<Vector2Int> tiles)
    {
        _levelTop  = null;
        _levelSeed = 0f;

        if (!LevelFloorSelected) return;
        var map = MapManager.Instance;
        if (map == null || tiles == null || tiles.Count == 0) return;

        // ① **고를 수 있는 단들을 먼저 모은다.** 발자국 둘레 `levelSnapRange` 칸 안의
        //    바닥 높이를 중복 없이 담는다 — `[` `]`가 딛고 다닐 눈금이다.
        //
        //    닿아 있는 것만 모으면 **한 칸 이상 떨어진 기단은 고를 수가 없다**(사이가 두 칸
        //    벌어진 두 기단을 이으려면, 첫 칸을 놓는 순간에는 아직 한쪽에만 닿아 있다).
        //    범위는 발자국의 경계 상자를 넓혀서 훑으므로 **칸 수에 제곱으로 안 는다.**
        _levelNear.Clear();

        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minZ) minZ = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxZ) maxZ = t.y;
        }

        int r = Mathf.Max(1, levelSnapRange);
        for (int x = minX - r; x <= maxX + r; x++)
        for (int z = minZ - r; z <= maxZ + r; z++)
        {
            if (!map.TryGetFloorSurface(new Vector2Int(x, z), out float y)) continue;
            if (!_levelNear.Exists(v => Mathf.Abs(v - y) < LevelSame)) _levelNear.Add(y);
        }
        _levelNear.Sort();

        // ② 이어 붙는 쪽이 기본값을 정한다. **여기는 맞닿은 칸만 본다** — 물려받는다는 것은
        //    붙어 있다는 뜻이고, 세 칸 건너 기단의 높이가 저 혼자 기본값이 되면 안 된다.
        //    **덩어리 안쪽은 걸러낼 필요가 없다**(아직 안 깔린 칸이라 조회가 애초에 false다).
        float top = float.MinValue;
        bool  found = false;
        foreach (var t in tiles)
            foreach (var d in Neighbors4)
                if (map.TryGetFloorSurface(t + d, out float y) && y > top) { top = y; found = true; }

        if (found) { _levelTop = top; return; }

        // ② 새 덩어리 — 범위 안 지면의 최고점.
        //
        // **여기에도 슬래브 두께를 얹는다.** 이 축이 뜻하는 것은 언제나 **딛는 면(윗면)**이다
        // — 이웃에서 물려받는 값(`_levelTop`)이 윗면이므로 새로 앉히는 값만 지면이면,
        // 이어 붙일 때마다 두께 한 장씩 어긋난다.
        float hi = float.MinValue;
        foreach (var t in tiles)
        {
            var w = map.TileToWorld(t);
            float g = map.SampleGroundHeight(w.x, w.z);
            if (g > hi) hi = g;
        }
        _levelSeed = hi + SlabThickness(SelectedBuilding != null ? SelectedBuilding.Prefab : null);
    }

    // 그 칸이 이 높이를 받아들일 수 있나. **거절도 숫자로 말한다** — 빨간 칸만 보이면
    // 어느 쪽으로 옮겨야 하는지를 알 수 없다.
    string WhyNotLevel(List<Vector2Int> footprint)
    {
        if (!LevelFloorSelected) return null;
        var map = MapManager.Instance;
        if (map == null) return null;

        float max = SelectedBuilding.LevelMaxStep;   // 0 = 한도 없음

        foreach (var t in footprint)
        {
            float level = LevelForTile(t);   // 계단은 칸마다 다르다
            var w = map.TileToWorld(t);
            float g = map.SampleGroundHeight(w.x, w.z);

            // **파묻히는 쪽만 막는다.** 뜨는 것은 치마가 받쳐 주므로 눈에 이상하지 않지만,
            // 흙이 슬래브 위로 올라온 칸은 **깔아도 안 보인다** — 지은 줄 알았는데 없다.
            if (g - level > LevelBury)
                return $"땅이 기단보다 {g - level:0.0}m 높다 — 높은 쪽부터 깔 것";
            if (max > 0f && level - g > max)
                return $"기단이 {level - g:0.0}m 뜬다 (허용 {max:0.0}m)";
        }
        return null;
    }

    // 미리보기 칸을 그릴 높이. 기단일 때만 값이 있다(그 밖에는 칸마다의 표면이 맞다).
    float? PreviewY => LevelFloorSelected ? LevelY : (float?)null;

    // 기단이면 정해진 높이에, 아니면 지면에 앉힌다. 고스트와 실제 배치가 **같은 함수**를
    // 지나야 미리보기가 거짓말을 안 한다.
    void SeatOnLevel(GameObject go, float groundY, Vector2Int? tile = null)
    {
        // `LevelY`(와 계단의 칸별 높이)는 언제나 **윗면**이다.
        if (LevelFloorSelected)
            AlignTopTo(go, tile.HasValue ? LevelForTile(tile.Value) : LevelY);
        else
            SnapOnSurface(go, groundY);
    }

    // **한 번에 그을 수 있는 칸 수.** 넉넉히 둔다 — 진짜 한도는 나중에 맵 크기가 정할 일이고,
    // 여기서 좁게 잡으면 큰 방을 한 번에 못 깐다. 이 값은 **폭주 방지선**이지 규칙이 아니다.
    const int MaxDragTiles = 5000;

    // **비싼 것은 칸이 아니라 고스트다.** 칸 재기는 한 칸에 몇 번의 비교지만, 고스트는
    // 건물 모델을 통째로 `Instantiate`한다 — 5000개면 화면이 멎는다.
    //
    // 그래서 **둘을 따로 자른다**: 초록 격자는 그을 수 있는 만큼 전부 보여 주고(어디까지
    // 지어질지는 그것이 답한다), 모델은 앞쪽 얼마까지만 세운다. 모델의 몫은 "어떤 모양이
    // 어느 방향으로 서나"를 보여 주는 것이라 몇 개만 있어도 그 답이 나온다.
    const int MaxLineGhosts = 200;

    static void LineOrRect(Vector2Int a, Vector2Int b, bool rect, List<Vector2Int> into)
    {
        into.Clear();

        if (rect)
        {
            // 기준에서 가까운 쪽부터 채우도록 범위를 먼저 자른다 — 잘린 뒤에도 **기준이
            // 모서리로 남아야** 어느 방향으로 늘어나는지가 그대로 보인다.
            int spanX = Mathf.Abs(b.x - a.x) + 1;
            int spanY = Mathf.Abs(b.y - a.y) + 1;

            if ((long)spanX * spanY > MaxDragTiles)
            {
                int limX = Mathf.Max(1, Mathf.Min(spanX, Mathf.FloorToInt(Mathf.Sqrt(MaxDragTiles))));
                int limY = Mathf.Max(1, MaxDragTiles / limX);

                b.x = a.x + Mathf.Clamp(b.x - a.x, -(limX - 1), limX - 1);
                b.y = a.y + Mathf.Clamp(b.y - a.y, -(limY - 1), limY - 1);
            }
        }

        if (rect)
        {
            for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                into.Add(new Vector2Int(x, y));
            return;
        }

        // **더 많이 움직인 축으로만** 곧게 간다.
        if (Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y))
        {
            b.x = a.x + Mathf.Clamp(b.x - a.x, -(MaxDragTiles - 1), MaxDragTiles - 1);
            for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
                into.Add(new Vector2Int(x, a.y));
        }
        else
        {
            b.y = a.y + Mathf.Clamp(b.y - a.y, -(MaxDragTiles - 1), MaxDragTiles - 1);
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                into.Add(new Vector2Int(a.x, y));
        }
    }

    readonly List<Vector2Int> _lineTiles = new();
    readonly List<bool>       _lineOk    = new();   // 칸마다의 판정 — 미리보기 색이 이걸 쓴다
    readonly List<float>      _lineY     = new();   // 칸마다의 높이 — 계단은 칸마다 다르다
    readonly List<Vector2Int> _oneTile   = new();   // 한 칸씩 물어볼 때 돌려 쓰는 그릇

    void UpdateGridPreview(Vector3 worldPoint)
    {
        var tile = MapManager.Instance.WorldToTile(worldPoint);

        if (CanDragBuild) { UpdateLineBuild(tile); return; }

        // **Q/E로 돌리면 그 자리에서 바로 다시 그린다.** 전엔 타일이 바뀔 때만 그려서,
        // 회전이 고스트 모델에는 즉시 붙는데 초록 격자는 **한 칸 옮겨야** 따라왔다
        // (모델과 격자가 서로 다른 자리를 가리키는 동안 어디에 지어질지 알 수 없다).
        bool stale = tile != _hoveredTile
                  || !_previewShown
                  || !Mathf.Approximately(_previewYaw, _gridYaw)
                  || _previewIndex != _selectedIndex;

        if (stale)
        {
            _hoveredTile   = tile;
            _previewShown  = true;
            _previewYaw    = _gridYaw;
            _previewIndex  = _selectedIndex;
            var fp = GetFootprint(tile);
            ResolveLevel(fp);
            _whyNot = WhyNotGrid(fp);
            gridRenderer.ShowPreview(fp, _whyNot == null, PreviewY);
        }

        // 고스트도 차지한 칸 전체의 중앙에 — 미리보기 격자와 모델이 같은 자리를 가리켜야 한다.
        var snapWorld = FootprintCenter(GetFootprint(tile));
        ShowGhost(snapWorld, Quaternion.Euler(0, _gridYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPlaceGrid(tile);
    }

    // 한 칸짜리 — 두 번 클릭으로 줄(벽)이나 면(바닥)을 한 번에.
    void UpdateLineBuild(Vector2Int tile)
    {
        // 기준을 안 잡았으면 평소처럼 한 칸 미리보기.
        var from = _lineAnchor ?? tile;
        LineOrRect(from, tile, RectDrag, _lineTiles);

        // **높이를 먼저 정한다** — 칸마다의 판정이 그 높이를 기준으로 갈리므로,
        // 검사보다 뒤에 정하면 미리보기가 아직 모르는 값을 두고 초록·빨강을 칠한다.
        ResolveLevel(_lineTiles);

        // **지어질 칸 전체를 미리 보여 준다.** 늘였다 줄였다 하는 동안 몇 칸이 될지가
        // 보이지 않으면 두 번 클릭의 값이 없다(그냥 여러 번 누르는 것과 같다).
        // **리스트를 칸마다 새로 만들지 않는다** — 5000칸이면 한 프레임에 5000개가 GC로 간다.
        // **칸마다 따로 잰다.** 하나라도 막히면 통째로 빨갛게 칠하던 시절엔, 실제로는
        // 막힌 칸만 건너뛰고 나머지가 지어져서 **"빨간데 만들어지는"** 일이 생겼다.
        // 이제 빨간 칸은 안 지어지는 칸 그것 하나를 뜻한다.
        //
        // **먼저 막힌 데서 멈추지 않는다** — 멈추면 그 뒤 칸들이 색을 못 받는다.
        _lineOk.Clear();
        _lineY.Clear();
        _whyNot = null;
        foreach (var t in _lineTiles)
        {
            _oneTile.Clear();
            _oneTile.Add(t);
            string why = WhyNotGrid(_oneTile);
            _lineOk.Add(why == null);
            _lineY.Add(LevelForTile(t));
            _whyNot ??= why;   // 이유는 처음 막힌 칸의 것을 띄운다
        }

        gridRenderer.ShowPreview(_lineTiles, _lineOk, _lineTiles.Count > 0, PreviewY,
                                 LevelFloorSelected ? _lineY : null);
        _previewShown = true;
        _hoveredTile  = tile;

        // **지어질 자리마다 모델을 세운다.** 초록 격자만으로는 몇 칸인지는 알아도 어떤 모양이
        // 어느 방향으로 설지 안 보인다 — 담이 그림대로 이어지는지가 놓기 전에 보여야 한다.
        var rot = Quaternion.Euler(0, DragYaw, 0);
        var head = _lineTiles.Count > 0 ? _lineTiles[0] : tile;
        ShowGhost(FootprintCenter(GetFootprint(head)), rot, head);
        ShowLineGhosts(_lineTiles, rot);

        var mouse = Mouse.current;

        // 우클릭·Esc = 무르기(`Update`의 Esc는 건설 모드를 통째로 닫으므로 여기서 먼저 먹는다).
        if (_lineAnchor.HasValue && mouse.rightButton.wasPressedThisFrame)
        {
            ClearLineAnchor();
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame) return;

        // 첫 클릭 = 기준. **여기서 격자를 못 박는다** — 안 그러면 줄을 긋는 동안 위상이
        // 커서를 따라 계속 움직여 기준 칸이 발밑에서 미끄러진다.
        if (!_lineAnchor.HasValue)
        {
            MapManager.Instance?.ClaimGrid();
            _lineAnchor  = tile;
            _levelFrozen = _levelCheck;   // 이 자리의 높이로 끝까지 간다
            return;
        }

        // 두 번째 클릭 = 짓는다. **못 짓는 칸은 건너뛴다** — 스무 칸을 그었는데 한 칸이
        // 막혔다고 전부 취소하면, 어디가 막혔는지도 모른 채 처음부터 다시 그어야 한다.
        // **줄 방향이 회전을 정한다** — 미리보기와 같은 값을 써야 *보이는 대로* 선다.
        float was = _gridYaw;
        _gridYaw = DragYaw;
        foreach (var t in _lineTiles) TryPlaceGrid(t);
        _gridYaw = was;

        ClearLineAnchor();
    }

    void UpdateFreePreview(Vector3 worldPoint)
    {
        _whyNot = WhyNotFree(worldPoint, _freeYaw);
        bool placeable = _whyNot == null;
        var snapTile   = MapManager.Instance.WorldToTile(worldPoint);
        _hoveredTile   = snapTile;
        _previewShown  = true;
        gridRenderer.ShowPreview(GetFootprint(snapTile), placeable);

        // XZ는 자유, Y는 바닥 윗면으로 — 레이가 어디에 맞았든 슬래브 위에 얹힌다.
        var surfacePoint = OnSurface(worldPoint);
        ShowGhost(surfacePoint, Quaternion.Euler(0, _freeYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame && placeable)
            TryPlaceFree(surfacePoint, _freeYaw);
    }

    // 바닥이 깔린 타일이면 Y를 슬래브 윗면으로 교체. 바닥이 없으면(지형 맵) 레이 높이 그대로.
    Vector3 OnSurface(Vector3 p)
    {
        var map = MapManager.Instance;
        if (map != null && map.TryGetFloorSurface(map.WorldToTile(p), out float y)) p.y = y;
        return p;
    }

    // ── 검증 ───────────────────────────────────────────────────────────────────

    bool IsBuildableGrid(List<Vector2Int> footprint) => WhyNotGrid(footprint) == null;

    // **왜 안 되는지를 말로 들고 다닌다**(2026-08-27). 빨간 격자만으로는 *"평지인데 왜 안
    // 되지"*를 풀 수가 없다 — 땅이 기운 것인지, 무언가 겹친 것인지, 빌드존 밖인지가
    // 화면에 하나도 안 나온다. **판정과 설명은 같은 코드여야 한다**(따로 두면 어긋난다).
    string _whyNot;

    string WhyNotGrid(List<Vector2Int> footprint)
    {
        var map = MapManager.Instance;
        var sel = SelectedBuilding;
        // 바닥은 바닥요구를 우회(공허에 깖), 건물은 바닥 위+점유+평탄(IsBuildable로 일원화).
        string why = sel != null && sel.IsFloor
            ? (map.CanPlaceFloor(footprint, LevelFloorSelected) ? null : "바닥을 깔 수 없는 자리")
            : map.WhyNotBuildable(footprint);
        if (why != null) return why;

        // 기단은 평탄 검사를 안 받는 대신 **얼마나 뜨고 얼마나 묻히는지**를 받는다.
        why = WhyNotLevel(footprint);
        if (why != null) return why;

        foreach (var bp in BuildingBlueprint.All)
            if (footprint.Contains(map.WorldToTile(bp.transform.position)))
                return "이미 청사진이 있다";
        // 완성된 건물과도 중복 금지 (점유 플래그 무관 — 창고/침대라도 겹쳐 쌓지 못하게). 바닥은 밑에 깔리므로 제외.
        if (sel == null || !sel.IsFloor)
            foreach (var pb in PlacedBuilding.All)
                if (pb.Data != null && !pb.Data.IsFloor
                    && footprint.Contains(map.WorldToTile(pb.transform.position)))
                    return "이미 건물이 있다";
        return null;
    }

    bool IsBuildableFree(Vector3 worldPoint, float yaw) => WhyNotFree(worldPoint, yaw) == null;

    // **자유 배치에도 이유가 있어야 한다**(2026-08-27). 처음엔 그리드에만 붙였는데,
    // **핵이 없으면 자유가 강제**라 정작 *"첫 핵이 왜 안 놓이지"*에는 아무 말도 안 나왔다.
    // 화면에 이유가 뜨는 자리는 **두 모드 다**여야 한다.
    string WhyNotFree(Vector3 worldPoint, float yaw)
    {
        var map = MapManager.Instance;
        if (!map.IsInBuildZone(worldPoint)) return "빌드존 밖 — 핵에서 너무 멀다";

        // 모델 A: 코어 union은 건설을 차단하지 않음(union 밖은 BuildingDormancy가 휴면 처리).

        var rotation = Quaternion.Euler(0, yaw, 0);
        var center   = worldPoint + Vector3.up * overlapHalfExtents.y;
        var hits     = Physics.OverlapBox(center, overlapHalfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.GetComponentInParent<ResourceNode>() != null) return "자원 노드가 있다";
            if (h.GetComponentInParent<DungeonCore>()  != null) return "핵이 있다";
        }
        foreach (var bp in BuildingBlueprint.All)
            if (Vector3.Distance(worldPoint, bp.transform.position) < 1f) return "이미 청사진이 있다";
        // 완성된 건물과도 중복 금지 (바닥 제외 — 건물은 바닥 위에 배치).
        foreach (var pb in PlacedBuilding.All)
            if (pb.Data != null && !pb.Data.IsFloor
                && Vector3.Distance(worldPoint, pb.transform.position) < 1f) return "이미 건물이 있다";
        return null;
    }

    // ── 배치 ───────────────────────────────────────────────────────────────────

    void TryPlaceGrid(Vector2Int tile)
    {
        var footprint = GetFootprint(tile);
        if (!IsBuildableGrid(footprint)) return;

        // **격자에 무언가를 놓는 순간 그 격자가 진짜가 된다.** 핵이 아니어도 그렇다 —
        // 놓인 물건이 곧 그 위상의 근거다.
        MapManager.Instance?.ClaimGrid();

        Place(FootprintCenter(footprint), Quaternion.Euler(0, _gridYaw, 0), footprint);
        _previewShown = false;
    }

    void TryPlaceFree(Vector3 worldPoint, float yaw)
    {
        Place(worldPoint, Quaternion.Euler(0, yaw, 0));
        _previewShown = false;
    }

    // `footprint`는 격자 배치일 때만 온다(자유 배치는 칸 개념이 없어 중심 한 칸을 쓴다).
    void Place(Vector3 pos, Quaternion rot, List<Vector2Int> footprint = null)
    {
        var data = SelectedBuilding;
        if (data == null) return;

        var prefab = data.Prefab != null ? data.Prefab : fallbackPrefab;
        if (prefab == null) return;

        // UI에서 걸러도 **여기서 한 번 더 막는다** — 목록이 갱신되기 전에 연구가 풀리기 전
        // 상태로 배치가 들어올 수 있고, 배치 경로가 UI 하나뿐이라는 보장도 없다.
        if (!data.IsUnlocked)
        {
            Debug.Log($"[Build] 연구가 필요합니다 — {data.RequiredResearch?.ResearchName}");
            return;
        }

        // ── 바닥은 오브젝트를 안 만든다 ────────────────────────────────────
        //
        // **칸을 `MapManager`에 등록하는 것이 곧 배치다**(2026-08-28). 그리는 것은
        // `FloorRenderer`가 청크 메시로 하고, 여기서는 데이터만 남긴다 — 칸마다
        // 프리팹을 세우면 마당 하나가 GameObject 수백 개가 되고, 그중 무엇도
        // `_floored`가 이미 아는 것 이상을 들고 있지 않다.
        //
        // 그래서 `PlacedBuilding`도 안 붙는다 — 철거는 칸으로 한다(`DemolishFloor`).
        if (data.IsFloor)
        {
            var map = MapManager.Instance;
            if (map == null || footprint == null) return;

            // 재질은 프리팹이 들고 있다 — 저작은 저기, 그리기는 여기.
            FloorRenderer.Ensure(MaterialOf(prefab));

            // 등록하는 값은 **딛는 면**이다. 기단은 `LevelForTile`이 이미 그것이고(계단이면
            // 칸마다 한 단씩 낮다), 경사를 따르는 바닥은 칸마다 제 지면 위에 슬래브를 얹은 높이다.
            float ground = map.SampleGroundHeight(pos.x, pos.z) + SlabThickness(prefab);
            foreach (var t in footprint)
                map.RegisterFloor(t, data.LevelFloor ? LevelForTile(t) : ground);
            return;
        }

        // **청사진은 여기서 자재를 안 낸다** — 폰이 나중에 지고 와 납품한다(`ConstructionJob`).
        // 전엔 배치 시점에 한 번 빼고 청사진이 또 걷어 **두 번 냈다.** 즉시 배치가 28종이던
        // 동안엔 비용 있는 건물이 대부분 그 길로 갔고, 청사진 다섯(침대·밭·창고·서브코어·물)에서만
        // 나던 일이라 눈에 덜 띄었다 — 13종을 청사진으로 돌리면서 전면화됐다.
        //
        // 자재가 없어도 놓을 수 있다. 청사진은 **계획**이고, 모자라면 폰이 안 갈 뿐
        // 자재가 들어오는 순간 저절로 재개된다(`BuildingBlueprint.TryRegisterJob`).
        if (data.UsesBlueprint)
        {
            var bp = new GameObject($"[Blueprint] {data.BuildingName}");
            bp.transform.SetPositionAndRotation(pos, rot);
            bp.AddComponent<BuildingBlueprint>().Init(data);
            return;
        }

        // 즉시 배치만 그 자리에서 값을 치른다 — 그리고 **모자라면 아무 일도 안 일어난다.**
        //
        // **그래서 값이 붙은 건물은 청사진이어야 한다**(2026-08-24). 광맥 열셋이 08-23에 값을
        // 얻고도 즉시 배치로 남아 있어, 자원이 모자라면 콘솔에 한 줄 남기고 **화면에서는
        // 클릭이 그냥 씹혔다** — *안 지어지는 것*과 *못 놓는 것*이 구별되지 않았다. 청사진이면
        // 파란 계획이 서고 자재가 들어오는 순간 폰이 저절로 간다.
        // **자리에서 낸다** — 세계가 이어져 거점이 여럿이면 29km 밖 창고의 목재로 여기를
        // 짓게 된다(`Territory`).
        //
        // **여럿이 들면 하나라도 모자랄 때 아무것도 안 뺀다** — 앞의 둘만 빼고 셋째에서
        // 멈추면 그 자재가 **아무 일도 안 하고 사라진다**(화면에는 *"눌렀는데 안 지어졌다"*로만
        // 보인다). 뺀 것을 적어 두었다가 되돌린다.
        if (!ConsumeAll(data, pos)) return;

        // 표면 위에 얹기 — 피벗이 중앙이면 바닥면 스냅, 저작된 부양 높이(서브코어 등)가 있으면 그만큼 띄움.
        var instance = SpawnOnSurface(prefab, pos, rot);
        instance.GetComponent<Storage>()?.Init(data);
        instance.GetComponent<Bed>()?.Init(data);
        instance.GetComponent<FarmPlot>()?.Init(data);
        instance.GetComponent<WaterSource>()?.Init(data);
        // 핵 영역 이탈 시 휴면/재가동 (설계: design_core_creep.md step 3)
        BuildingDormancy.Ensure(instance);
        // 철거/환불용 메타 보관
        var placed = PlacedBuilding.Attach(instance, data);
        // 타일 점유(통행 차단) 건물이면 footprint를 통행 불가로 등록 (철거 시 대칭 해제)
        if (data.OccupiesTile && MapManager.Instance != null)
            placed.OccupyTiles(footprint != null
                ? footprint.ToArray()
                : new[] { MapManager.Instance.WorldToTile(pos) });
        // 벽 등 장애물이면 코스 격자에 반영 (CheckBox가 Wall 콜라이더 감지)
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);
        Debug.Log($"[Build] 배치 완료 — {data.BuildingName}");
    }

    // **전부 되거나 아무것도 안 된다.** 재료가 여럿일 수 있게 된 뒤로(2026-09-17) 중간에서
    // 멈추면 앞서 뺀 자재가 증발하므로, 뺀 것을 적어 두었다가 되돌린다.
    //
    // 되돌리는 문은 `MaterialPool.Refund`다 — **창고 먼저**로 돌아간다(바닥에 떨구면 그 짐이
    // 다시 운반 일감이 되고, 나르는 동안 집계에서 빠져 다른 작업이 재료 부족으로 죽는다).
    static bool ConsumeAll(BuildingData data, Vector3 pos)
    {
        var taken = new List<RecipeData.Ingredient>();

        foreach (var c in data.Costs)
        {
            if (c.Item == null || c.Amount <= 0) continue;

            // **자리에서 낸다** — 세계가 이어져 거점이 여럿이면 29km 밖 창고의 목재로
            // 여기를 짓게 된다(`Territory`).
            if (Storage.ConsumeByItem(c.Item, c.Amount, pos)) { taken.Add(c); continue; }

            foreach (var back in taken) MaterialPool.Refund(back.Item, back.Amount, pos);
            Debug.Log($"[Build] 자원 부족 — {c.Item.ItemName} ×{c.Amount}");
            return false;
        }
        return true;
    }

    GameObject ResolvePrefab()
    {
        var bd = SelectedBuilding;
        return (bd != null && bd.Prefab != null) ? bd.Prefab : fallbackPrefab;
    }

    // 차지할 칸 목록. **회전하면 가로·세로가 바뀐다.**
    // 홀수 크기는 기준 칸을 중앙에 두고, 짝수 크기는 +방향으로 치우친다(어느 쪽이든 하나로 정해야
    // 미리보기와 실제 배치가 어긋나지 않는다).
    List<Vector2Int> GetFootprint(Vector2Int anchor)
    {
        var size = FootprintSize();

        _footprint.Clear();
        int ox = (size.x - 1) / 2;
        int oy = (size.y - 1) / 2;
        for (int x = 0; x < size.x; x++)
        for (int y = 0; y < size.y; y++)
            _footprint.Add(new Vector2Int(anchor.x - ox + x, anchor.y - oy + y));

        return _footprint;
    }

    // **지금 미리보기가 쓰는 각도.** 자유 모드는 `_freeYaw`로 돌리는데 발자국은 `_gridYaw`만
    // 보고 있었다 — 그래서 자유 모드에서 Q/E로 침대를 돌려도 **밑의 초록색 미리보기가 그대로**였다
    // (1×2가 2×1이 되지 않는다). 바닥은 자유 모드여도 그리드로 강제되므로 그때는 `_gridYaw`다.
    float PreviewYaw =>
        _mode == PlacementMode.Grid || (SelectedBuilding != null && SelectedBuilding.IsFloor)
            ? _gridYaw : _freeYaw;

    Vector2Int FootprintSize()
    {
        var s = SelectedBuilding != null ? SelectedBuilding.Size : Vector2Int.one;
        s = new Vector2Int(Mathf.Max(1, s.x), Mathf.Max(1, s.y));

        // 90°/270° 회전이면 축이 바뀐다. **자유 모드는 15° 단위**라 반올림으로 가장 가까운
        // 직각에 붙인다 — 45° 근처에서는 어느 쪽으로 재도 반 칸씩 어긋나므로, 어긋나더라도
        // 눈에 보이는 모델과 같은 쪽으로 어긋나는 편이 낫다.
        int quarter = Mathf.RoundToInt(PreviewYaw / 90f) & 3;
        return (quarter % 2 == 1) ? new Vector2Int(s.y, s.x) : s;
    }

    // 모델은 **차지한 칸 전체의 중앙**에 놓는다. 기준 칸에 그냥 두면 짝수 크기 건물이
    // 반 칸씩 삐져나온다(미리보기 격자와 모델이 어긋나 보인다).
    Vector3 FootprintCenter(List<Vector2Int> footprint)
    {
        var map = MapManager.Instance;
        if (map == null || footprint.Count == 0) return Vector3.zero;

        var min = footprint[0];
        var max = footprint[0];
        foreach (var t in footprint)
        {
            min = Vector2Int.Min(min, t);
            max = Vector2Int.Max(max, t);
        }

        var a = map.TileToGround(min);
        var b = map.TileToGround(max);
        return new Vector3((a.x + b.x) * 0.5f, Mathf.Max(a.y, b.y), (a.z + b.z) * 0.5f);
    }
}

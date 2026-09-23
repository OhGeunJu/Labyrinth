using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// **땅에 "고르라"를 찍는 모드**(2026-09-24). 켜는 키는 `CommandMode`가 든다(`V`).
//
// **`MineDesignator`와 같은 조작이다**: 클릭=한 칸 / 드래그=박스 · 찍힐 것이 미리 보이고
// **우클릭이 반대로 지운다.** 새 조작을 배우게 하지 않는다 — 다른 것은 색과, 아래 한 줄이다.
//
// ── 목표 높이는 처음 찍은 칸이 정한다 ──────────────────────────────────────
//
// 채굴에는 없는 축이다(파면 없어지니 목표가 없다). 여기는 *"어느 높이로"*가 필요한데 **창을
// 띄우지 않는다** — 바닥이 1m 계단이므로 **밟고 선 단이 곧 고를 높이**이고, 그러면 값을
// 고르는 일이 *"어느 칸에서 끌기 시작하나"*로 녹아든다(기단이 *범위 안 최고점*을 쓰는 것과
// 같은 자리이되, 그쪽은 얹는 물건이라 묻히지 않으려고 최고점인 것이다).
//
// ── 씬 배선이 없다 ─────────────────────────────────────────────────────────
//
// `MineDesignator`·`HaulDesignator`와 같은 자리 — 순수 입력기라 스스로 선다.
//
// ── 표시도 여기가 그린다 ───────────────────────────────────────────────────
//
// 채굴 표시는 `RockRenderer`가 그린다(바위 메시의 두 번째 서브메시) — 땅은 얹을 메시가 없다.
// **미리보기와 확정 표시가 같은 그림**이라 한 곳이 그리는 것이 맞고, 도구를 꺼도 표시는
// 남아야 하므로 `IsActive`와 무관하게 그린다.
public class LevelDesignator : MonoBehaviour
{
    const float DragThreshold = 8f;

    public static bool IsActive      { get; private set; }
    public static bool IsDesignating { get; private set; }

    public enum Tool { Designate, Cancel }

    public static Tool Current { get; private set; } = Tool.Designate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        _instance = SelfStanding.Create<LevelDesignator>("LevelDesignator");
    }

    static LevelDesignator _instance;

    Camera _cam;

    // 드래그 기준점은 **월드로 든다** — 화면 좌표로 들면 카메라를 밀 때 기준점이 화면에
    // 붙어 따라와 박스가 통째로 미끄러진다(`MineDesignator`가 먼저 겪었다).
    Vector3 _dragStartWorld;
    bool    _hasAnchor;

    Vector2 _dragStart;
    bool    _dragging;
    bool    _rightDrag;

    // **이번 드래그의 목표 높이** — 처음 찍은 칸에서 한 번 재고 붙든다. 매 프레임 다시 재면
    // 커서가 지나가는 칸마다 목표가 바뀌어 **찍히는 높이가 손을 떠난다.**
    float _target;

    // 이번 드래그가 실제로 찍은 칸. **원래 찍혀 있던 칸은 안 들어간다** — 그래야 상자가
    // 지나가도 남의 지시를 안 무른다(`GroundLevel.Mark`가 참을 돌려준 것만 담는다).
    readonly HashSet<Vector2Int> _preview = new();
    readonly HashSet<Vector2Int> _next    = new();
    readonly List<Vector2Int>    _undo    = new();

    // ── 목표와 양을 커서 옆에 적는다 ────────────────────────────────────────
    //
    // **이 도구의 약점을 메우는 자리다.** 목표 높이를 창으로 안 묻는 대신 *처음 찍은 칸*에서
    // 가져오므로 **그 값이 화면에 아무 데도 없다** — 테두리 색이 방향(깎기/메우기)만 말하고
    // *얼마나*는 못 말한다. 화면 구석에 적으면 눈은 커서에 가 있어 못 읽으므로 **커서 옆**이다
    // (`MineDesignator`가 무너짐 경고를 그렇게 띄우고, 건설이 값과 거절 사유를 그렇게 띄운다).
    //
    // **메울 돌이 모자라면 붉게 적는다** — 그게 없으면 일감이 아예 안 나고(`LevelGroundJob.IsReady`)
    // 화면에는 *"찍었는데 아무도 안 온다"*로만 보인다.
    Vector2Int? _hintAt;
    string      _hint;
    bool        _hintBad;
    bool        _hintHadTarget;   // 목표가 확정되는 순간에도 다시 낸다(같은 칸이어도)

    void UpdateHint(Vector2Int? tile)
    {
        if (tile == null) { _hintAt = null; _hint = null; return; }
        if (_hintAt == tile && _hint != null && _hintHadTarget == _hasTarget) return;

        _hintHadTarget = _hasTarget;
        _hintAt  = tile;
        _hint    = null;
        _hintBad = false;

        var at = tile.Value;
        if (!GroundLevel.CanLevel(at))
        {
            _hint    = "여기는 고를 수 없습니다 (벽 · 건물 · 바닥)";
            _hintBad = true;
            return;
        }

        // 아직 누르지 않았으면 *그 칸이 목표*다 — 누르면 무엇이 될지를 미리 말한다.
        float target = _hasTarget ? _target : SurfaceOf(at);
        float need   = GroundLevel.SignedRemaining(at, target);

        if (Mathf.Abs(need) < 0.01f)
        {
            _hint = $"목표 {target:0.0}m — 이미 평평합니다";
            return;
        }

        var item = GroundLevel.Spoil;
        int n    = GroundLevel.SpoilFor(need);

        if (need > 0f)
        {
            _hint = $"목표 {target:0.0}m — {need:0.0}m 깎습니다 (" +
                    (item != null ? $"{item.ItemName} {n}개 나옵니다)" : "산출 없음)");
            return;
        }

        if (item == null) { _hint = $"목표 {target:0.0}m — {-need:0.0}m 메웁니다"; return; }

        var map = MapManager.Instance;
        Vector3 w = map != null ? map.TileToWorld(at) : new Vector3(at.x + 0.5f, 0f, at.y + 0.5f);
        int have = MaterialPool.AvailableNear(item, w);

        _hint    = $"목표 {target:0.0}m — {-need:0.0}m 메웁니다 ({item.ItemName} {n}개 필요, 있음 {have})";
        _hintBad = have < n;
    }

    void OnGUI()
    {
        if (!IsActive || _hint == null || Event.current == null) return;

        var at  = Event.current.mousePosition + new Vector2(20f, 18f);
        var was = GUI.color;
        GUI.color = _hintBad ? new Color(1f, 0.45f, 0.4f) : new Color(0.95f, 0.95f, 0.7f);
        GUI.Label(new Rect(at.x, at.y, 520f, 22f), _hint);
        GUI.color = was;
    }

    void Update()
    {
        if (!IsActive) { IsDesignating = false; _hint = null; return; }

        var kb = Keyboard.current;

        if (BuildModeManager.IsActive || MineDesignator.IsActive || HaulDesignator.IsActive)
        { SetActive(false); return; }
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { SetActive(false); return; }

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearPreview();
            return;
        }

        Vector2 cur = mouse.position.ReadValue();

        // **우클릭은 반대로 지운다 — 누르면 한 칸, 끌면 상자째.**
        if (mouse.rightButton.wasPressedThisFrame)
        {
            _rightDrag = true;
            _dragStart = cur;
            _dragging  = false;
            _hasAnchor = DragPoint(cur, out _dragStartWorld);
        }
        if (_rightDrag)
        {
            ReanchorTo(cur);
            if (mouse.rightButton.isPressed)
            {
                if (_dragging) UndoBox(_dragStart, cur);
                return;
            }
            if (!_dragging) UndoOne(cur);
            _rightDrag = false;
            _dragging  = false;
            _hasAnchor = false;
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragStart = cur;
            _dragging  = false;
            _hasAnchor = DragPoint(cur, out _dragStartWorld);

            // **누른 칸의 지금 높이가 목표다.** 못 짚었으면 그 프레임은 아무것도 안 한다 —
            // 0으로 떨어뜨리면 **세계 전체가 해수면으로 깎인다.**
            _hasTarget = TileUnder(cur, out var t0);
            if (_hasTarget) _target = SurfaceOf(t0);
        }

        if (mouse.leftButton.isPressed) ReanchorTo(cur);

        if (mouse.leftButton.isPressed && !_dragging &&
            (cur - _dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            _dragging = true;

        // **취소 도구는 되돌리기가 없다** — 지운 것을 다시 찍어 줄 이유가 없다.
        if (mouse.leftButton.isPressed && Current == Tool.Cancel)
        {
            if (_dragging) UndoBox(_dragStart, cur);
            else           UndoOne(cur);
            return;
        }

        IsDesignating = mouse.leftButton.isPressed;

        if (mouse.leftButton.isPressed)
        {
            if (!_hasTarget) return;

            _next.Clear();
            if (_dragging) CollectBox(_dragStart, cur, _next);
            else if (TileUnder(cur, out var t) && GroundLevel.CanLevel(t)) _next.Add(t);

            // **커서가 짚은 한 칸만 잰다** — 드래그 중에 상자 안을 전부 재면 칸마다 정점
            // 아홉을 훑어 프레임이 죽는다. 무리한지는 한 칸으로도 드러난다.
            UpdateHint(_dragging ? null : TileUnder(cur, out var h) ? h : (Vector2Int?)null);

            // **바뀐 것만 고친다** — 매 프레임 전부 지우고 다시 찍으면 `OnChanged`가 칸
            // 수만큼 터져 표시 메시가 쉬지 않고 다시 구워진다.
            _undo.Clear();
            foreach (var t in _preview)
                if (!_next.Contains(t)) { GroundLevel.Unmark(t); _undo.Add(t); }
            foreach (var t in _undo) _preview.Remove(t);

            foreach (var t in _next)
                if (!_preview.Contains(t) && GroundLevel.Mark(t, _target)) _preview.Add(t);

            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            _hasAnchor    = false;
            _dragging     = false;
            IsDesignating = false;

            if (Current == Tool.Cancel) return;

            // **놓는 순간 일감이 난다.** 누르는 동안 내면 취소된 일감이 큐에 쌓인다.
            _preview.Clear();
            LevelJobBoard.Nudge();
            _hasTarget = false;   // 다음 드래그는 그때 짚은 칸이 목표다
            return;
        }

        // **손을 떼고 있을 때도 적는다** — *누르면 무엇이 될지*를 미리 말하는 자리다
        // (목표가 커서 아래 칸이므로, 이때가 값을 고르는 순간이다).
        UpdateHint(TileUnder(cur, out var idle) ? idle : (Vector2Int?)null);
    }

    bool _hasTarget;

    // 카메라가 움직였으면 기준점의 **화면 자리**를 다시 낸다(월드 자리는 그대로).
    void ReanchorTo(Vector2 cur)
    {
        if (!_hasAnchor || _cam == null) return;
        var sp = _cam.WorldToScreenPoint(_dragStartWorld);
        if (sp.z > 0f) _dragStart = new Vector2(sp.x, sp.y);
        if (!_dragging && (cur - _dragStart).sqrMagnitude > DragThreshold * DragThreshold) _dragging = true;
    }

    // 그 칸의 지금 땅 높이. **`MapManager`를 지난다** — 함수를 직접 부르면 격자 위상이
    // 얹히지 않아 반 칸 어긋난 자리를 잰다.
    static float SurfaceOf(Vector2Int tile)
    {
        var map = MapManager.Instance;
        return map != null ? map.SurfaceHeight(tile)
                           : TerrainHeight.At(tile.x + 0.5f, tile.y + 0.5f);
    }

    // ── 찍기 ────────────────────────────────────────────────────────────────

    void CollectBox(Vector2 a, Vector2 b, HashSet<Vector2Int> into)
    {
        // **두 점이면 된다 — 처음 찍은 칸과 지금 칸.** 화면 사각형의 네 귀퉁이를 떨어뜨리면
        // 비스듬한 시점에서 지면에서 사다리꼴이 되어 **보이는 것보다 넓게 찍힌다.**
        if (!DragPoint(a, out var p0)) return;
        if (!DragPoint(b, out var p1)) return;

        var map = MapManager.Instance;
        if (map == null) return;

        var t0 = map.WorldToTile(p0);
        var t1 = map.WorldToTile(p1);

        int minX = Mathf.Min(t0.x, t1.x), maxX = Mathf.Max(t0.x, t1.x);
        int minY = Mathf.Min(t0.y, t1.y), maxY = Mathf.Max(t0.y, t1.y);

        // **한 번에 너무 넓게 찍는 것을 막는다** — 자를 때 **기준점 쪽을 남긴다**(그냥 `min`
        // 쪽을 남기면 위나 왼쪽으로 끌 때 처음 찍은 자리가 잘려 박스가 미끄러진다).
        const int MaxSpan = 40;
        if (maxX - minX > MaxSpan)
        {
            if (t0.x - minX <= maxX - t0.x) maxX = minX + MaxSpan;
            else                            minX = maxX - MaxSpan;
        }
        if (maxY - minY > MaxSpan)
        {
            if (t0.y - minY <= maxY - t0.y) maxY = minY + MaxSpan;
            else                            minY = maxY - MaxSpan;
        }

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var tile = new Vector2Int(x, y);
            if (GroundLevel.CanLevel(tile)) into.Add(tile);
        }
    }

    void ClearPreview()
    {
        foreach (var t in _preview) GroundLevel.Unmark(t);
        _preview.Clear();
        IsDesignating = false;
    }

    void UndoBox(Vector2 a, Vector2 b)
    {
        _next.Clear();
        CollectBox(a, b, _next);
        bool any = false;
        foreach (var t in _next) any |= GroundLevel.Unmark(t);
        if (any) LevelJobBoard.Nudge();
    }

    void UndoOne(Vector2 p)
    {
        if (TileUnder(p, out var t) && GroundLevel.Unmark(t)) LevelJobBoard.Nudge();
    }

    // **`CommandMode`의 취소 도구가 여기도 무른다** — 도구마다 제 것만 무르면 *"취소를
    // 눌렀는데 고르기 표시가 남는다"*가 된다(채굴·채집·운반이 이미 서로를 무르고 있다).
    public static void UnmarkRect(Vector2 a, Vector2 b)
    {
        if (_instance == null) return;
        _instance.UndoBox(a, b);
    }

    public static void UnmarkAt(Vector2 p)
    {
        if (_instance != null) _instance.UndoOne(p);
    }

    // ── 커서 아래 ───────────────────────────────────────────────────────────

    bool TileUnder(Vector2 screen, out Vector2Int tile)
    {
        tile = default;
        if (!Ground(screen, out var world)) return false;

        var map = MapManager.Instance;
        if (map == null) return false;

        tile = map.WorldToTile(world);
        return true;
    }

    bool DragPoint(Vector2 screen, out Vector3 world)
        => Ground(screen, out world) || PlaneHit(screen, out world);

    // 바닥 평면과의 교점 — 박스 귀퉁이가 허공이어도 답이 나온다.
    bool PlaneHit(Vector2 screen, out Vector3 world)
    {
        world = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        var map = MapManager.Instance;
        float y = map != null ? map.SampleGroundHeight(_cam.transform.position) : 0f;

        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        var ray   = _cam.ScreenPointToRay(screen);
        if (!plane.Raycast(ray, out float dist)) return false;

        world = ray.GetPoint(dist);
        return true;
    }

    // **보이는 것을 짚는다**(`VisiblePick`) — 그냥 쏘면 잘려서 화면에 없는 벽 윗부분에 맞는다.
    bool Ground(Vector2 screen, out Vector3 world)
    {
        world = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        var ray = _cam.ScreenPointToRay(screen);
        if (!VisiblePick.First(ray, out var pick, 5000f)) return false;

        // **맞은 면 안쪽으로 5cm 밀어 넣는다** — 벽 옆면을 누르면 맞은 점이 정확히 칸
        // 경계에 떨어져 어느 칸인지가 면의 방향에 따라 갈린다.
        world = pick.point - pick.normal * 0.05f;
        return true;
    }

    // ── 켜고 끄기 ───────────────────────────────────────────────────────────

    public static void Open(Tool t)
    {
        Current = t;
        if (_instance != null) _instance.SetActive(true);
    }

    public static void Close() { if (_instance != null) _instance.SetActive(false); }

    void OnEnable()  => ExclusiveMode.Claimed += OnOtherClaimed;
    void OnDisable() => ExclusiveMode.Claimed -= OnOtherClaimed;

    void OnOtherClaimed(object who) { if (!ReferenceEquals(who, this)) SetActive(false); }

    void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;
        if (!on) { ClearPreview(); _dragging = false; _rightDrag = false; }
        if (on) ExclusiveMode.Claim(this);
    }
}

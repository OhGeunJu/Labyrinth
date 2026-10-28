using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// **땅을 한 단 올리거나 내리거나 평탄하게 하라고 찍는 모드**(2026-09-24 · 09-25에 올리기/내리기/평탄화로 ·
// 같은 날 2m 덩어리로). 켜는 키는 `CommandMode`가 든다(`V` 올리기 · `N` 내리기 · `T` 평탄화).
//
// **`MineDesignator`와 같은 조작이다**: 클릭=한 덩어리 / 드래그=박스 · 찍힐 것이 미리 보이고
// **우클릭이 반대로 지운다.** 새 조작을 배우게 하지 않는다 — 다른 것은 색과, 아래 한 줄이다.
//
// ── 찍는 단위는 2m 덩어리다 ────────────────────────────────────────────────
//
// 땅 높이가 2m 정점으로만 있어서 1m 칸으로 찍으면 **선과 실제로 움직인 땅이 안 맞았다**
// (근거는 `GroundLevel` 머리). 드래그는 **덩어리**를 모으고, 찍는 것은 **그 귀퉁이 정점**이다.
//
// ── 목표는 정점마다 "지금 단 ± 한 단"이다 ─────────────────────────────────────
//
// 처음(09-24)에는 **처음 찍은 칸의 높이로 고르는** 도구 하나였다. 창을 안 띄우려고 목표를
// *어느 칸에서 끌기 시작하나*에 숨겼는데, 그러면 **같은 드래그가 시작점에 따라 깎기도 메우기도
// 되어** 누르기 전에는 무엇이 될지 알 수 없었다(사용자: *"사용하기가 어려워 · 내리기 올리기
// 이렇게 있으면"*). 이제 **방향은 도구가 정하고 양은 한 단(`TerraceStep`)이다** — 어디서
// 끌든 같은 일이 난다. 평평한 단을 통째로 찍으면 그 단이 그대로 한 층 오르내린다.
//
// **이미 찍힌 곳을 다시 끌어도 한 단 더 가지 않는다**(2026-09-25) — 한때 *"찍힌 목표에서 한 단 더"*로
// 쌓았는데, 찍어 둔 자리 위로 상자를 다시 끄는 일이 흔해서 **같은 손짓이 어떤 칸은 한 단 어떤 칸은
// 두 단**이 됐다(사용자: *"왜 내리기가 되어있는곳에 드래그하면 또 내리기가 되는지"*). 목표는 늘
// **지금 땅에서 한 단**이다 — 두 단을 원하면 첫 단이 끝난 뒤 다시 찍는다(그때의 지금 땅에서 한 단).
// 반대 도구로 끌면 그쪽으로 덮어쓴다.
//
// ── 평탄화는 목표가 하나다 ─────────────────────────────────────────────────
//
// 사용자가 올리기·내리기와 **따로** 달라고 했다(2026-09-25). 목표는 **누른 자리의 높이를 단에 맞춘 값**
// 하나이고 구역 전체가 그 높이로 깎이거나 메워진다. 처음(09-24)의 고르기와 같은 규칙인데 그때 헷갈렸던
// 것은 *무엇이 될지가 안 보인 것*이었다 — 이제 **누르기 전부터 커서 옆에 목표 높이**가 뜨고, 방향이
// 도구 이름에 있는 올리기·내리기가 따로 있어 *한 방향으로만* 하고 싶을 때는 그쪽을 쓴다.
//
// **기준은 지금 높이를 단에 맞춘 값이다** — 단 경계의 비탈에 걸친 정점은 높이가 어중간할 수 있어,
// 그대로 ±1m를 하면 단 사이 어딘가에 선다. 반올림하면 올린 자리가 **이웃 단과 이어진다.**
//
// ── 씬 배선이 없다 ─────────────────────────────────────────────────────────
//
// `MineDesignator`·`HaulDesignator`와 같은 자리 — 순수 입력기라 스스로 선다.
public class LevelDesignator : MonoBehaviour
{
    const float DragThreshold = 8f;

    public static bool IsActive      { get; private set; }
    public static bool IsDesignating { get; private set; }

    public enum Tool { Raise, Lower, Cancel, Flatten }

    public static Tool Current { get; private set; } = Tool.Raise;

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

    // 이번 드래그가 실제로 찍은 정점(`GroundLevel.Mark`가 참을 돌려준 것만).
    readonly HashSet<Vector2Int> _preview = new();

    // **이번 드래그가 덮어쓴 옛 표시.** 이미 찍힌 정점을 다시 찍으면 한 단 더 가는데, 상자를 줄이며
    // 그 정점이 빠지면 **지우는 것이 아니라 옛 목표로 되돌려야** 남의 지시가 안 사라진다.
    readonly Dictionary<Vector2Int, float> _prev = new();

    // **평탄화의 목표** — 누를 때 한 번 재고 드래그 내내 붙든다(매 프레임 다시 재면 커서가 지나가는
    // 자리마다 목표가 바뀐다). 누르기 전에는 커서 아래에서 매번 다시 잰다(누르면 무엇이 될지).
    float _flatTarget;
    bool  _hasFlat;

    // 이번 드래그가 찍은 덩어리 — 상자를 줄이면 여기서 빠진 것을 무른다.
    readonly HashSet<Vector2Int> _previewBlocks = new();

    readonly HashSet<Vector2Int> _blocks    = new();
    readonly HashSet<Vector2Int> _nextVerts = new();
    readonly List<Vector2Int>    _undo      = new();

    // ── 목표와 양을 커서 옆에 적는다 ────────────────────────────────────────
    //
    // 화면 구석에 적으면 눈은 커서에 가 있어 못 읽으므로 **커서 옆**이다(`MineDesignator`가
    // 무너짐 경고를 그렇게 띄우고, 건설이 값과 거절 사유를 그렇게 띄운다).
    //
    // **올릴 돌이 모자라면 붉게 적는다** — 그게 없으면 일감이 아예 안 나고(`LevelGroundJob.IsReady`)
    // 화면에는 *"찍었는데 아무도 안 온다"*로만 보인다.
    Vector2Int? _hintAt;
    string      _hint;
    bool        _hintBad;
    int         _hintVersion = -1;   // 표시가 바뀌면(방금 찍었다) 같은 덩어리여도 다시 낸다
    Tool        _hintTool;

    void UpdateHint(Vector2Int? block)
    {
        if (block == null) { _hintAt = null; _hint = null; return; }
        if (_hintAt == block && _hint != null && _hintVersion == GroundLevel.Version && _hintTool == Current) return;

        _hintVersion = GroundLevel.Version;
        _hintTool    = Current;
        _hintAt      = block;
        _hint        = null;
        _hintBad     = false;

        var b = block.Value;
        string why = GroundLevel.WhyNotBlock(b);
        if (why != null)
        {
            _hint    = $"여기는 고를 수 없습니다 — {why}";
            _hintBad = true;
            return;
        }

        if (Current == Tool.Flatten && !IsDesignating) _flatTarget = FlatTargetAt(b);

        // 귀퉁이 넷을 더한다. 이번 드래그가 이미 찍은 정점이면 **찍힌 목표**를, 아니면 *누르면
        // 무엇이 될지*를 말한다. 이웃 덩어리와 나눠 쓰는 귀퉁이까지 세므로 **"약"**이다.
        float lo = float.MaxValue, hi = float.MinValue;
        int   cut = 0, fill = 0, pinned = 0;
        foreach (var v in GroundLevel.CornersOf(b))
        {
            // **건물에 닿는 귀퉁이는 제자리다** — 그 덩어리는 건물 쪽으로 비탈이 진다.
            if (!GroundLevel.CanMoveVertex(v))
            {
                pinned++;
                float h0 = GroundLevel.Height(v);
                lo = Mathf.Min(lo, h0); hi = Mathf.Max(hi, h0);
                continue;
            }
            float target = _preview.Contains(v) && GroundLevel.TryTarget(v, out float marked) ? marked : TargetFor(v);
            float need   = GroundLevel.SignedRemaining(v, target);
            lo = Mathf.Min(lo, target); hi = Mathf.Max(hi, target);
            if (need > 0.01f) cut += GroundLevel.SpoilFor(need); else if (need < -0.01f) fill += GroundLevel.SpoilFor(need);
        }

        string goal = Mathf.Abs(hi - lo) < 0.05f ? $"목표 {lo:0.0}m" : $"목표 {lo:0.0}~{hi:0.0}m";
        if (pinned > 0) goal += " (건물 쪽 모서리는 그대로)";

        if (cut == 0 && fill == 0)
        {
            // 절벽 바닥 한계에 걸려 못 내린 것이면 **그렇다고 말한다** — "이미 그 높이"는 거짓말이다.
            bool floor = Current != Tool.Raise && Chasm.Any && lo <= Chasm.MinLevelY + 0.01f;
            _hint = floor ? $"{goal} — 더 내릴 수 없습니다 (절벽 바닥이 가깝습니다)" : $"{goal} — 이미 그 높이입니다";
            _hintBad = floor;
            return;
        }

        var item = GroundLevel.Spoil;
        string verb = Current == Tool.Flatten ? "평탄하게 합니다" : fill > 0 ? "올립니다" : "내립니다";
        if (item == null) { _hint = $"{goal} — {verb}"; return; }

        if (fill == 0)
        {
            _hint = $"{goal} — {verb} ({item.ItemName} 약 {cut}개 나옵니다)";
            return;
        }

        int have = MaterialPool.AvailableNear(item, BlockCenter(b));
        _hint    = cut > 0
            ? $"{goal} — {verb} ({item.ItemName} 약 {cut}개 나오고 {fill}개 필요, 있음 {have})"
            : $"{goal} — {verb} ({item.ItemName} 약 {fill}개 필요, 있음 {have})";
        _hintBad = have + cut < fill;
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

        // **우클릭은 반대로 지운다 — 누르면 한 덩어리, 끌면 상자째.**
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

            // **평탄화는 누른 자리가 목표다.** 못 짚었으면 그 드래그는 아무것도 안 한다 —
            // 0으로 떨어뜨리면 **구역 전체가 0m로 깎인다.**
            _hasFlat = BlockUnder(cur, out var fb);
            if (_hasFlat) _flatTarget = FlatTargetAt(fb);
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
            if (Current == Tool.Flatten && !_hasFlat) return;

            _blocks.Clear();
            if (_dragging) CollectBox(_dragStart, cur, _blocks);
            else if (BlockUnder(cur, out var b) && GroundLevel.CanLevelBlock(b)) _blocks.Add(b);

            // **커서가 짚은 덩어리 하나만 잰다** — 상자 안을 전부 재면 프레임이 죽는다.
            UpdateHint(_dragging ? null : BlockUnder(cur, out var h) ? h : (Vector2Int?)null);

            _nextVerts.Clear();
            foreach (var blk in _blocks)
                foreach (var v in GroundLevel.CornersOf(blk)) _nextVerts.Add(v);

            // **바뀐 것만 고친다** — 매 프레임 전부 지우고 다시 찍으면 `OnChanged`가 정점
            // 수만큼 터져 표시 메시가 쉬지 않고 다시 구워진다.
            _undo.Clear();
            foreach (var blk in _previewBlocks)
                if (!_blocks.Contains(blk)) { GroundLevel.UnmarkBlock(blk); _undo.Add(blk); }
            foreach (var blk in _undo) _previewBlocks.Remove(blk);

            _undo.Clear();
            foreach (var v in _preview)
                if (!_nextVerts.Contains(v)) { Restore(v); _undo.Add(v); }
            foreach (var v in _undo) _preview.Remove(v);

            foreach (var v in _nextVerts)
            {
                if (_preview.Contains(v)) continue;
                float target = TargetFor(v);
                // **이미 그 높이인 귀퉁이는 안 찍는다** — 찍으면 일감 판이 곧바로 풀어 버려
                // 덩어리 선이 한 프레임 떴다 사라진다(평탄화에서 흔하다).
                if (!GroundLevel.IsMarked(v) && GroundLevel.IsSettled(v, target)) continue;
                bool had = GroundLevel.TryTarget(v, out float old);
                if (!GroundLevel.Mark(v, target)) continue;
                _preview.Add(v);
                if (had) _prev[v] = old;
            }

            // 덩어리는 정점 **뒤에** — 앞에 두면 귀퉁이가 아직 안 찍혀 선이 한 프레임 비어 보인다.
            foreach (var blk in _blocks)
                if (!_previewBlocks.Contains(blk) && GroundLevel.MarkBlock(blk)) _previewBlocks.Add(blk);

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
            _previewBlocks.Clear();
            _prev.Clear();
            _hasFlat = false;
            LevelJobBoard.Nudge();
            return;
        }

        // **손을 떼고 있을 때도 적는다** — *누르면 무엇이 될지*를 미리 말하는 자리다.
        UpdateHint(BlockUnder(cur, out var idle) ? idle : (Vector2Int?)null);
    }

    // 카메라가 움직였으면 기준점의 **화면 자리**를 다시 낸다(월드 자리는 그대로).
    void ReanchorTo(Vector2 cur)
    {
        if (!_hasAnchor || _cam == null) return;
        var sp = _cam.WorldToScreenPoint(_dragStartWorld);
        if (sp.z > 0f) _dragStart = new Vector2(sp.x, sp.y);
        if (!_dragging && (cur - _dragStart).sqrMagnitude > DragThreshold * DragThreshold) _dragging = true;
    }

    // **그 정점을 어디로 보낼까** — 지금 높이를 단에 맞춘 값에서 한 단. **찍힌 목표에서 쌓지 않는다**
    // (근거는 파일 머리).
    float TargetFor(Vector2Int v)
    {
        if (Current == Tool.Flatten) return GroundLevel.ClampTarget(_flatTarget);

        float step = Step;
        float from = Mathf.Round(GroundLevel.Height(v) / step) * step;
        return GroundLevel.ClampTarget(from + (Current == Tool.Lower ? -step : step));
    }

    // 계단을 끈 세계에서도 1m씩 움직인다.
    static float Step
    {
        get
        {
            float step = TerrainHeight.Profile.TerraceStep;
            return step <= 0.0001f ? 1f : step;
        }
    }

    // 평탄화 목표 — 그 덩어리 한가운데의 땅 높이를 **단에 맞춘다**(반쯤 걸친 비탈 높이로 고르면
    // 새 판이 이웃 단 어디와도 안 이어진다).
    static float FlatTargetAt(Vector2Int block) =>
        Mathf.Round(TerrainHeight.At(BlockCenter(block)) / Step) * Step;

    // 미리보기에서 빠진 정점을 드래그 전으로 — 옛 표시가 있었으면 그것으로, 없었으면 지운다.
    void Restore(Vector2Int v)
    {
        if (_prev.TryGetValue(v, out float old)) { GroundLevel.Mark(v, old); _prev.Remove(v); }
        else GroundLevel.Unmark(v);
    }

    static Vector3 BlockCenter(Vector2Int b)
    {
        float s = TerrainGrid.VertexSpacing;
        return new Vector3((b.x + 0.5f) * s, 0f, (b.y + 0.5f) * s);
    }

    // ── 찍기 ────────────────────────────────────────────────────────────────

    void CollectBox(Vector2 a, Vector2 b, HashSet<Vector2Int> into)
    {
        // **두 점이면 된다 — 처음 찍은 자리와 지금 자리.** 화면 사각형의 네 귀퉁이를 떨어뜨리면
        // 비스듬한 시점에서 지면에서 사다리꼴이 되어 **보이는 것보다 넓게 찍힌다.**
        if (!DragPoint(a, out var p0)) return;
        if (!DragPoint(b, out var p1)) return;

        var b0 = GroundLevel.BlockOfWorld(p0);
        var b1 = GroundLevel.BlockOfWorld(p1);

        int minX = Mathf.Min(b0.x, b1.x), maxX = Mathf.Max(b0.x, b1.x);
        int minY = Mathf.Min(b0.y, b1.y), maxY = Mathf.Max(b0.y, b1.y);

        // **한 번에 너무 넓게 찍는 것을 막는다** — 자를 때 **기준점 쪽을 남긴다**.
        const int MaxSpan = 20;   // 덩어리 — 40m
        if (maxX - minX > MaxSpan)
        {
            if (b0.x - minX <= maxX - b0.x) maxX = minX + MaxSpan;
            else                            minX = maxX - MaxSpan;
        }
        if (maxY - minY > MaxSpan)
        {
            if (b0.y - minY <= maxY - b0.y) maxY = minY + MaxSpan;
            else                            minY = maxY - MaxSpan;
        }

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var blk = new Vector2Int(x, y);
            if (GroundLevel.CanLevelBlock(blk)) into.Add(blk);
        }
    }

    void ClearPreview()
    {
        foreach (var blk in _previewBlocks) GroundLevel.UnmarkBlock(blk);
        _previewBlocks.Clear();
        foreach (var v in _preview) Restore(v);
        _preview.Clear();
        _prev.Clear();
        IsDesignating = false;
    }

    // **덩어리를 무른다.** 귀퉁이는 이웃 덩어리와 나눠 쓰므로, **남아 있는 찍힌 덩어리의
    // 귀퉁이는 남긴다** — 안 그러면 옆 덩어리가 귀퉁이를 잃어 선 안이 한쪽으로 기운다.
    void UnmarkBlocks(HashSet<Vector2Int> blocks)
    {
        bool any = false;
        foreach (var blk in blocks) any |= GroundLevel.UnmarkBlock(blk);

        foreach (var blk in blocks)
        foreach (var v in GroundLevel.CornersOf(blk))
        {
            if (!GroundLevel.IsMarked(v)) continue;

            bool keep = false;
            for (int dz = -1; dz <= 0 && !keep; dz++)
            for (int dx = -1; dx <= 0 && !keep; dx++)
                if (GroundLevel.IsBlockMarked(new Vector2Int(v.x + dx, v.y + dz))) keep = true;
            if (!keep) any |= GroundLevel.Unmark(v);
        }
        if (any) LevelJobBoard.Nudge();
    }

    void UndoBox(Vector2 a, Vector2 b)
    {
        _blocks.Clear();
        if (!DragPoint(a, out var p0) || !DragPoint(b, out var p1)) return;
        var b0 = GroundLevel.BlockOfWorld(p0);
        var b1 = GroundLevel.BlockOfWorld(p1);
        for (int y = Mathf.Min(b0.y, b1.y); y <= Mathf.Max(b0.y, b1.y); y++)
        for (int x = Mathf.Min(b0.x, b1.x); x <= Mathf.Max(b0.x, b1.x); x++)
            _blocks.Add(new Vector2Int(x, y));
        UnmarkBlocks(_blocks);
    }

    void UndoOne(Vector2 p)
    {
        if (!BlockUnder(p, out var blk)) return;
        _blocks.Clear();
        _blocks.Add(blk);
        UnmarkBlocks(_blocks);
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

    bool BlockUnder(Vector2 screen, out Vector2Int block)
    {
        block = default;
        if (!Ground(screen, out var world)) return false;
        block = GroundLevel.BlockOfWorld(world);
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

        // **맞은 면 안쪽으로 5cm 밀어 넣는다** — 벽 옆면을 누르면 맞은 점이 정확히 경계에
        // 떨어져 어느 덩어리인지가 면의 방향에 따라 갈린다.
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

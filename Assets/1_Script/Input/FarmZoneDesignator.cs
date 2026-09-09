using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// **구역을 손으로 가른다 — `Z`**(2026-09-02).
//
// 기본은 *맞닿으면 한 구역*이다(`FarmZones`). 대부분은 그걸로 끝나지만, **한 덩어리로 붙어
// 있는 밭을 둘로 나눠 다른 것을 심고 싶을 때**가 있다 — 그때 손이 닿는 문이 여기다.
//
// ── 조작 ──────────────────────────────────────────────────────────────────
//
// ── 도구 둘 ───────────────────────────────────────────────────────────────
//
//   **확장**   끌어서 지운 밭을 **선택한 구역**에 넣는다.
//              고른 구역이 없으면 **새 구역**을 만든다 — 그래서 버튼 이름이 그때는 `새 구역`이다.
//              **없는 것을 이름으로 감추지 않는다**: *"확장"*이라고 적어 놓고 새것을 만들면,
//              이미 다 묶인 세계에서 **새 구역을 만들 길이 없는 것처럼 보인다**(사용자가 짚었다).
//   **삭제**   손으로 묶은 것을 푼다 — 다시 *맞닿으면 한 구역*(자동)으로 돌아간다.
//
// **확장의 대상은 드래그를 시작한 칸의 구역이다** — 고르는 조작을 따로 두면 *"무엇이 커질지"*가
// 화면에 안 적힌 상태에 숨는다. **누른 데가 커진다.**
//
// **단축키를 안 준다**(2026-09-02, 사용자 결정) — 켜는 문은 **모드 바 버튼 하나**다. 키가
// 늘수록 *있는 줄 모르는 조작*이 늘고, 이건 자주 쓰는 것도 아니다.
//
// **끄는 길을 같은 조작에 둔다**(채굴·운반 지정과 같은 규약) — 묶는 것만 있고 푸는 것이
// 없으면, 한 번 잘못 묶은 밭은 헐어야만 되돌릴 수 있다.
//
// **씬 배선이 없다**(`MineDesignator`·`HaulDesignator`와 같은 자리) — 아무 상태도 안 들고
// 저작할 것도 없는 순수 입력기라 스스로 선다.
public class FarmZoneDesignator : MonoBehaviour
{
    const float DragThreshold = 8f;

    public static bool IsActive { get; private set; }

    static FarmZoneDesignator _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        _instance = SelfStanding.Create<FarmZoneDesignator>("FarmZoneDesignator");
    }

    // **버튼도 이 문으로 온다**(`ModeBarUI`) — 키로 켠 것과 같은 상태여야 한다.
    public static void Toggle() { if (_instance != null) _instance.SetActive(!IsActive); }

    Camera  _cam;

    // **기준점은 월드로 든다** — 화면 좌표로 들면 누른 채 카메라를 밀 때 상자가 커지는 게
    // 아니라 통째로 미끄러진다(`MineDesignator`가 먼저 겪었다).
    Vector2Int _anchorTile;
    bool       _hasAnchor;
    Vector2    _dragStart;   // 끌기 시작 판정에만 쓴다(화면 거리)
    bool       _dragging;

    // ── 도구 ────────────────────────────────────────────────────────────────

    // **`새 구역`과 `확장`을 가른다**(2026-09-02, 사용자). 하나로 두고 *"고른 게 없으면
    // 새 구역"*으로 했더니, **무엇이 될지가 고른 상태에 숨어 있어** 누르기 전에는 알 수 없었다.
    public enum Tool { New, Extend, Erase }

    public static Tool Current { get; private set; } = Tool.New;
    public static void Use(Tool t) => Current = t;

    // **확장은 "처음 누른 칸의 구역"을 키운다**(2026-09-02, 사용자 결정).
    //
    // 전에는 *마지막으로 클릭해 연 밭*의 구역을 썼다(`FarmPlot.OnSelected`). 그러면 끌기 전에
    // **밭을 한 번 눌러 고르는 단계가 따로** 있고, 그 상태가 화면에 안 적혀 있어 **무엇이
    // 커질지를 끌어 봐야 알았다.** 시작 칸이 곧 답이면 그 물음이 사라진다 — **누른 데가
    // 커진다.**
    //
    // 시작 칸에 밭이 없으면 **상자에 처음 걸린 밭**의 구역을 쓴다(빈 땅에서 끌기 시작해
    // 구역 위로 지나가는 흔한 손놀림).
    FarmPlot _anchor;

    readonly List<FarmPlot> _hit = new();

    // **드래그하는 동안 테두리가 따라온다**(사용자: *"드래그하면 테두리 연결되도록"*).
    // 놓고 나서야 보이면 **무엇을 묶는지 모르는 채로 놓게 된다** — 범위는 놓기 전에 보여야 한다.
    //
    // 세트로 들고 **바뀔 때만 번호를 올린다**(`PreviewVersion`) — 오버레이가 그 번호로
    // *다시 지을 때*를 안다(매 프레임 다시 지으면 밭 수만큼이 프레임에 붙는다).
    public static readonly HashSet<FarmPlot> Preview = new();
    public static int PreviewVersion { get; private set; }

    static void SetPreview(List<FarmPlot> now)
    {
        bool same = now != null && now.Count == Preview.Count;
        if (same) foreach (var p in now) if (!Preview.Contains(p)) { same = false; break; }
        if (same) return;

        Preview.Clear();
        if (now != null) foreach (var p in now) Preview.Add(p);
        PreviewVersion++;
    }

    void Update()
    {
        if (!IsActive) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { SetActive(false); return; }

        var mouse = Mouse.current;
        if (mouse == null) return;

        var cur = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
        {
            _dragStart = cur;
            _dragging  = false;

            // **기준은 화면이 아니라 칸이다** — 카메라가 움직여도 안 미끄러지고, 비스듬한
            // 시점에서도 상자가 사다리꼴이 되지 않는다(건설의 줄 긋기와 같은 방식).
            _hasAnchor = TileUnder(cur, out _anchorTile);
            _anchor    = PlotAt(_anchorTile);
        }

        if ((mouse.leftButton.isPressed || mouse.rightButton.isPressed) && !_dragging &&
            (cur - _dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            _dragging = true;

        // 끌고 있는 동안 무엇이 잡히는지 그린다.
        if (_hasAnchor && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
        {
            CollectTo(cur);
            SetPreview(_hit);
        }

        if (mouse.leftButton.wasReleasedThisFrame)  Apply(cur, primary: true);
        if (mouse.rightButton.wasReleasedThisFrame) Apply(cur, primary: false);
    }

    // 기준 칸에서 지금 커서 칸까지. 커서가 밭 밖이면 **기준 칸 하나**로 둔다 — 상자가
    // 화면 밖으로 튀는 것보다 아무것도 안 잡히는 편이 낫다.
    void CollectTo(Vector2 cur)
    {
        if (!_hasAnchor) { _hit.Clear(); return; }
        Collect(_anchorTile, TileUnder(cur, out var t) ? t : _anchorTile);
    }

    static FarmPlot PlotAt(Vector2Int tile)
    {
        foreach (var p in FarmPlot.All)
            if (p != null && p.TileCoord == tile) return p;
        return null;
    }

    // 상자(또는 클릭 한 점) 안의 밭에 지금 도구를 건다.
    void Apply(Vector2 cur, bool primary)
    {
        CollectTo(cur);
        _dragging  = false;
        _hasAnchor = false;
        SetPreview(null);

        if (!primary) return;   // 우클릭은 지금 하는 일이 없다 — 도구가 답한다
        if (_hit.Count == 0) return;

        if (Current == Tool.Erase)
        {
            // **구역에서 뺀다** — 번호 0은 *구역 없음*이고, 그 밭은 저 혼자가 된다.
            foreach (var p in _hit) p.SetGroup(0);
            return;
        }

        // **새 구역은 시작 칸을 안 본다** — 그게 `확장`과 갈리는 전부다.
        int id = Current == Tool.New ? NewZoneId() : ZoneIdFor(_anchor ?? _hit[0]);
        foreach (var p in _hit) p.SetGroup(id);
    }

    // 확장이 쓸 번호. **고른 구역이 자동(번호 0)이면 여기서 번호를 준다** — 안 주면
    // *"확장했는데 옆 밭까지 딸려 왔다"*가 된다(자동은 맞닿은 것 전부가 한 덩어리다).
    static int ZoneIdFor(FarmPlot anchor)
    {
        if (anchor != null && anchor.GroupId != 0) return anchor.GroupId;

        int id = NewZoneId();

        // 고른 것이 자동 구역이면 **그 구역 전체를 통째로 옮긴다** — 대표 밭 하나만 번호를
        // 받으면 나머지가 뒤에 남아, 눈에는 한 덩어리인데 씨앗은 따로 노는 구역이 된다.
        if (anchor != null)
            foreach (var p in FarmZones.Of(anchor)) if (p != null) p.SetGroup(id);

        return id;
    }

    // **쓰고 있지 않은 번호를 고른다** — 지운 구역의 번호를 다시 쓰면 남아 있던 밭과
    // 조용히 합쳐진다.
    static int NewZoneId()
    {
        int max = 0;
        foreach (var p in FarmPlot.All) if (p != null && p.GroupId > max) max = p.GroupId;
        return max + 1;
    }

    // **칸 좌표의 사각형으로 잡는다**(2026-09-02, 사용자 제안).
    //
    // 전에는 **화면 사각형**에 밭의 화면 좌표가 드는지를 봤다. 그러면 비스듬한 시점에서
    // 화면의 네모가 지면에서는 **사다리꼴**이라, 눈에 보이는 것과 실제로 잡히는 것이 다르다
    // (채굴이 시점을 위에서 내려다보게 눕히는 이유가 그것이다).
    //
    // **처음 누른 칸을 기준으로 상하좌우로 편다.** 그러면 카메라 각도·해상도가 무엇이든
    // *"이 칸에서 저 칸까지"*가 그대로 답이고, 레이캐스트는 **칸 하나를 고를 때만** 쓴다.
    void Collect(Vector2Int a, Vector2Int b)
    {
        _hit.Clear();

        int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);

        foreach (var p in FarmPlot.All)
        {
            if (p == null) continue;
            var t = p.TileCoord;
            if (t.x < x0 || t.x > x1 || t.y < y0 || t.y > y1) continue;
            _hit.Add(p);
        }
    }

    // 커서 아래의 칸. **바위는 건너뛴다** — 바위 콜라이더는 `Ground` 레이어에 9.6m로 서서,
    // 비스듬히 보면 커서와 바닥 사이의 벽 윗면에 레이가 먼저 맞는다(건설 커서와 같은 자리).
    bool TileUnder(Vector2 screen, out Vector2Int tile)
    {
        tile = default;
        if (!Ground(screen, out var world)) return false;

        var map = MapManager.Instance;
        tile = map != null ? map.WorldToTile(world)
                           : new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));
        return true;
    }

    bool Ground(Vector2 screen, out Vector3 world)
    {
        world = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        var ray  = _cam.ScreenPointToRay(screen);
        var hits = Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Collide);
        if (hits.Length == 0) return false;

        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (RockRenderer.IsRockCollider(hits[i].collider)) continue;
            world = hits[i].point;
            return true;
        }
        return false;
    }

    void OnEnable()
    {
        ExclusiveMode.Claimed += OnOtherClaimed;
    }

    void OnDisable()
    {
        ExclusiveMode.Claimed -= OnOtherClaimed;
    }



    // 남이 켰으면 나는 꺼진다. **켜는 쪽은 자기만 알고, 끄는 판단은 각자 한다.**
    void OnOtherClaimed(object who) { if (!ReferenceEquals(who, this)) SetActive(false); }

    void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive   = on;
        _dragging  = false;
        _hasAnchor = false;
        SetPreview(null);

        if (on) ExclusiveMode.Claim(this);
    }
}

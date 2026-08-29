using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// **바위에 "파라"를 찍는 모드.** `K`키로 켠다(2026-08-29).
//
// 지하는 사방이 바위라 **파는 것이 곧 확장이자 탐험**이다. 그러니 찍는 조작이 편해야
// 하는데, 하나씩 클릭하게 하면 방 하나를 파려고 수백 번을 눌러야 한다.
//
// **운반 지정(`H`)·철거(`X`)와 같은 조작이다**: 클릭=한 칸 / 드래그=박스, 찍힐 것이 미리
// 보이고, **우클릭이 반대로 지운다.** 새 조작을 배우게 하지 않는다 — 다른 것은 색뿐이다.
//
// ── 씬 배선이 없다 ─────────────────────────────────────────────────────────
//
// `HaulDesignator`·`DraftController`와 같은 자리 — 아무 상태도 안 들고 저작할 것도 없는
// **순수 입력기**라 스스로 선다.
public class MineDesignator : MonoBehaviour
{
    const float DragThreshold = 8f;

    public static bool IsActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("MineDesignator");
        _instance = go.AddComponent<MineDesignator>();
        DontDestroyOnLoad(go);
    }

    Camera  _cam;
    Vector2 _dragStart;
    bool    _dragging;

    // 지금 찍힐 칸들 — 미리 찍어 두고, 무르면 되돌린다.
    readonly HashSet<Vector2Int> _preview = new();
    readonly HashSet<Vector2Int> _next    = new();   // 이번 프레임에 찍혀야 할 것

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.kKey.wasPressedThisFrame) Toggle();

        if (!IsActive) return;

        // 다른 모드가 켜지면 물러난다 — 드래그가 겹치면 무엇이 찍히는지 알 수 없다.
        if (BuildModeManager.IsActive || HaulDesignator.IsActive) { SetActive(false); return; }
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { SetActive(false); return; }

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearPreview();
            return;
        }

        Vector2 cur = mouse.position.ReadValue();

        // **우클릭은 반대로 지운다.**
        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (TileUnder(cur, out var t)) RockMap.Mark(t, false);
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame) { _dragStart = cur; _dragging = false; }

        if (mouse.leftButton.isPressed && !_dragging &&
            (cur - _dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            _dragging = true;

        if (mouse.leftButton.isPressed)
        {
            // **바뀐 것만 고친다**(2026-08-29). 전에는 매 프레임 **전부 지우고 전부 다시**
            // 찍었는데, 그때마다 `RockMap.OnChanged`가 칸 수만큼 터져 **바위 청크가 쉬지 않고
            // 다시 구워졌다.** 다시 굽는 것은 프레임당 두 장뿐이라(예산) 어떤 청크는 표시가
            // 있고 어떤 청크는 없는 상태가 번갈아 보인다 — 화면에서는 **벽이 밀리는 것**처럼
            // 보인다(사용자가 본 그것이다).
            _next.Clear();
            if (_dragging) CollectBox(_dragStart, cur, _next);
            else if (TileUnder(cur, out var t) && RockMap.IsRock(t)) _next.Add(t);

            foreach (var t in _preview)
                if (!_next.Contains(t)) RockMap.Mark(t, false);

            foreach (var t in _next)
                if (!_preview.Contains(t)) RockMap.Mark(t, true);

            _preview.Clear();
            foreach (var t in _next) _preview.Add(t);
            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            // **놓는 순간 일감이 난다.** 누르는 동안 내는 것이 아니라 — 드래그 중에는
            // 박스가 계속 바뀌므로 그때마다 등록하면 **취소된 일감이 큐에 쌓인다.**
            Commit();
            _dragging = false;
        }
    }

    // ── 찍기 ────────────────────────────────────────────────────────────────

    // **찍지 않고 모으기만 한다** — 실제로 찍고 무르는 것은 부르는 쪽이 **바뀐 것만**
    // 골라서 한다(그래야 청크가 헛돌지 않는다).
    void CollectBox(Vector2 a, Vector2 b, HashSet<Vector2Int> into)
    {
        // **네 귀퉁이를 바닥 평면에 떨어뜨려 월드 범위를 얻는다** — 화면 좌표로 훑을 목록이
        // 없다(바위는 오브젝트가 아니라 칸 데이터다).
        //
        // **물리 레이가 아니라 평면이다**(2026-08-29). 콜라이더로 잡으면 귀퉁이 하나가
        // 허공으로 나가는 순간 `false`가 되어 **통째로 아무것도 안 찍힌다** — 화면에서는
        // *"드래그했는데 아무 일도 안 일어난다"*로만 보인다. 바닥이 완전 평지라 평면과의
        // 교점은 **언제나 있고 정확하다.**
        if (!PlaneHit(new Vector2(a.x, a.y), out var p0)) return;
        if (!PlaneHit(new Vector2(b.x, b.y), out var p1)) return;
        if (!PlaneHit(new Vector2(a.x, b.y), out var p2)) return;
        if (!PlaneHit(new Vector2(b.x, a.y), out var p3)) return;

        var map = MapManager.Instance;
        if (map == null) return;

        var t0 = map.WorldToTile(p0); var t1 = map.WorldToTile(p1);
        var t2 = map.WorldToTile(p2); var t3 = map.WorldToTile(p3);

        int minX = Mathf.Min(Mathf.Min(t0.x, t1.x), Mathf.Min(t2.x, t3.x));
        int maxX = Mathf.Max(Mathf.Max(t0.x, t1.x), Mathf.Max(t2.x, t3.x));
        int minY = Mathf.Min(Mathf.Min(t0.y, t1.y), Mathf.Min(t2.y, t3.y));
        int maxY = Mathf.Max(Mathf.Max(t0.y, t1.y), Mathf.Max(t2.y, t3.y));

        // **한 번에 너무 넓게 찍는 것을 막는다** — 화면 전체를 드래그하면 수만 칸이 되고,
        // 그만큼의 일감이 큐에 들어가면 배정이 그 자리에서 멎는다.
        const int MaxSpan = 60;
        if (maxX - minX > MaxSpan) maxX = minX + MaxSpan;
        if (maxY - minY > MaxSpan) maxY = minY + MaxSpan;

        // **사각형이 아니라 사다리꼴 안만 찍는다**(2026-08-29).
        //
        // 카메라가 눕혀 있으면 화면의 사각형이 지면에서는 **사다리꼴**이 된다 — 화면 위쪽이
        // 멀어서 넓게 퍼지기 때문이다. 그 바깥 상자를 찍으면 **위쪽이 훨씬 넓게 찍혀**,
        // 드래그한 것보다 위가 더 파인다(사용자가 본 그것이다).
        //
        // 네 점을 **화면에서 그린 순서대로** 이어 다각형으로 보고, 칸 중심이 그 안에 드는
        // 것만 찍는다. 바깥 상자는 훑을 범위를 좁히는 데만 쓴다.
        Vector2 q0 = new(p0.x, p0.z), q1 = new(p3.x, p3.z),
                q2 = new(p1.x, p1.z), q3 = new(p2.x, p2.z);

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var tile = new Vector2Int(x, y);
            Vector3 c = map.TileToWorld(tile);
            if (!InQuad(new Vector2(c.x, c.z), q0, q1, q2, q3)) continue;
            if (!RockMap.IsRock(tile)) continue;
            into.Add(tile);
        }
    }

    // 볼록 사각형 안인가. **네 변에 대해 같은 쪽인지**만 보면 된다 — 화면 사각형을 지면에
    // 투영한 것이라 언제나 볼록하다(오목해질 수가 없다).
    static bool InQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float s0 = Cross(a, b, p);
        float s1 = Cross(b, c, p);
        float s2 = Cross(c, d, p);
        float s3 = Cross(d, a, p);

        bool neg = s0 < 0f || s1 < 0f || s2 < 0f || s3 < 0f;
        bool pos = s0 > 0f || s1 > 0f || s2 > 0f || s3 > 0f;
        return !(neg && pos);   // 부호가 안 섞이면 안쪽
    }

    static float Cross(Vector2 a, Vector2 b, Vector2 p) =>
        (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

    void ClearPreview()
    {
        foreach (var t in _preview) RockMap.Mark(t, false);
        _preview.Clear();
    }

    // 찍기를 끝낸다. **일감은 여기서 안 낸다**(2026-08-29).
    //
    // 전에는 찍을 때 한 번 내고 어느 칸에 냈는지를 여기서 기억했는데, 그러면 **그 일감이
    // 취소되는 순간 그 칸은 영영 안 파인다** — 표시는 남았는데 *"이미 냈다"*는 기억 때문에
    // 다시 안 낸다. 폰이 못 가서 포기하는 것은 흔한 일이고(아직 길이 안 뚫렸다), 대개
    // **앞을 파고 나면 갈 수 있다.**
    //
    // 그래서 **찍은 것만 여기서 하고 일감은 `MineJobBoard`가 표시에서 파생시킨다.**
    // 어디서 취소되든 저절로 되돌아온다.
    void Commit()
    {
        _preview.Clear();
        MineJobBoard.Nudge();   // 다음 훑기를 기다리면 *"먹었나?"*가 된다
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

    // **바닥 평면과의 교점.** 박스를 칠 때 쓴다 — 귀퉁이가 허공이어도 답이 나온다.
    //
    // **한 번 클릭할 때는 이걸 안 쓴다** — 벽 윗면을 눌렀을 때 평면까지 뚫고 내려가면
    // **눈에 보이는 벽이 아니라 그 뒤의 칸**이 찍힌다. 그쪽은 콜라이더가 맞다.
    bool PlaneHit(Vector2 screen, out Vector3 world)
    {
        world = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        // **바닥이 아니라 바위 윗면에 떨어뜨린다**(2026-08-29).
        //
        // 바닥 평면(30m)에 투영하고 있었는데, 사람이 눈으로 겨누는 것은 **바위 윗면**
        // (30 + 9.6 = 39.6m)이다. 시점이 조금만 기울어도 9.6m 차이가 화면에서는 **수십 칸**이
        // 되어, 겨눈 곳보다 **한참 위(먼 쪽)에 찍힌다** — 사용자가 본 그것이다.
        //
        // 벽이 높을수록 어긋남이 커지므로 `RockMap.Height`를 3배로 올린 날 같이 드러났다.
        var map = MapManager.Instance;
        float ground = map != null ? map.SampleGroundHeight(_cam.transform.position) : 0f;
        float y = ground + RockMap.Height;

        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        var ray   = _cam.ScreenPointToRay(screen);
        if (!plane.Raycast(ray, out float dist)) return false;

        world = ray.GetPoint(dist);
        return true;
    }

    // **바위 윗면도 지면이다** — 바위에 콜라이더가 있으므로 레이가 거기 맞는다. 맞은 자리의
    // XZ만 쓰면 그 칸이 나온다(높이는 안 본다). **한 칸을 누를 때 쓴다.**
    bool Ground(Vector2 screen, out Vector3 world)
    {
        world = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        var ray = _cam.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out var hit, 5000f)) return false;

        world = hit.point;
        return true;
    }

    // ── 켜고 끄기 ───────────────────────────────────────────────────────────

    // **버튼도 이 문으로 온다**(`ModeBarUI`) — 키로 켠 것과 같은 상태여야 한다.
    public static void Toggle() { if (_instance != null) _instance.SetActive(!IsActive); }

    static MineDesignator _instance;

    // 채굴 모드에 들어오기 전의 각도. **여기서 기억한다** — 카메라가 들면 다른 모드가
    // 겹쳐 눌렀을 때 어느 것으로 돌아갈지가 또 규칙이 된다.
    float _prevPitch;
    bool  _pitchSaved;

    void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;
        if (!on) { ClearPreview(); _dragging = false; }

        // **채굴할 때는 위에서 내려다본다**(2026-08-29, 사용자). 눕힌 시점에서는 화면
        // 사각형이 지면에서 사다리꼴이라 **무엇이 찍힐지를 눈으로 못 잰다** — 위에서 보면
        // 화면의 사각형이 곧 지면의 사각형이다.
        //
        // **원래 각도로 돌려준다** — 채굴하러 한 번 들어갔다고 시점이 영영 바뀌면, 그건
        // 도와주는 것이 아니라 빼앗는 것이다.
        var cam = CameraController.Active;
        if (cam == null) return;

        if (on)
        {
            _prevPitch  = cam.Pitch;
            _pitchSaved = true;
            cam.LerpToPitch(cam.TopDownPitch);
        }
        else if (_pitchSaved)
        {
            cam.LerpToPitch(_prevPitch);
            _pitchSaved = false;
        }
    }
}

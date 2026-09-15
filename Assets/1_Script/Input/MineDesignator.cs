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

    // ── 도구 둘 ─────────────────────────────────────────────────────────────
    //
    // **지정 / 취소.** 우클릭으로도 지울 수 있지만 그건 **한 칸씩**이라, 넓게 찍은 것을
    // 넓게 무를 길이 없었다. 도구를 바꾸면 **같은 드래그가 반대로 돈다** — 찍을 때 쓰던
    // 손놀림 그대로 지운다.
    public enum Tool { Designate, Cancel }

    // **찍는 중인가**(2026-09-06, 사용자: *"드래그를 풀어야지 채굴을 하러가야지 … 내가
    // 채굴위치를 고민중인데 걍 바로 가서 채굴해버리니깐"*).
    //
    // `Commit`은 놓을 때만 부르는데도 폰이 먼저 갔다 — **`MineJobBoard`가 1초마다 찍힌 칸을
    // 훑어서 일감을 내기 때문**이다. 드래그 중의 표시는 *미리보기*인데 그것을 **결정으로
    // 읽고 있었다.** 미리보기와 결정이 같은 자료(`RockMap.IsMarked`)를 쓰는 한, **읽는 쪽이
    // "아직 손을 안 뗐다"를 알아야 한다.**
    public static bool IsDesignating { get; private set; }

    public static Tool Current { get; private set; } = Tool.Designate;
    public static void Use(Tool t) => Current = t;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        _instance = SelfStanding.Create<MineDesignator>("MineDesignator");
    }

    Camera  _cam;

    // **드래그의 기준점은 월드다**(2026-08-30). 화면 좌표로 들고 있으면 **카메라가 움직일 때
    // 그 점이 가리키는 월드 자리가 같이 움직인다** — 왼쪽 버튼을 누른 채 `WASD`로 밀면
    // 처음 찍은 자리가 **화면에 붙어 따라와서**, 박스가 커지는 게 아니라 통째로 미끄러진다.
    // 그래서 **화면에 보이는 것보다 넓게 찍을 수가 없었다**(사용자).
    //
    // 월드로 들고 **매 프레임 화면 좌표로 되돌린다** — 그러면 기준점이 화면 밖으로 나가도
    // 사다리꼴 계산이 그대로 성립한다(`WorldToScreenPoint`는 뷰포트 밖 좌표도 준다).
    Vector3 _dragStartWorld;

    bool    _hasAnchor;

    Vector2 _dragStart;
    bool    _dragging;

    // 지금 찍힐 칸들 — 미리 찍어 두고, 무르면 되돌린다.
    // **이번 드래그가 실제로 찍은 칸.** 원래 찍혀 있던 칸은 여기 안 들어간다 — 그래야
    // 상자가 지나가도 남의 지시를 안 무른다.
    readonly HashSet<Vector2Int> _preview = new();
    readonly HashSet<Vector2Int> _next    = new();   // 이번 프레임에 찍혀야 할 것
    readonly List<Vector2Int>    _undo    = new();   // 훑는 도중에 고치지 않으려고

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

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragStart = cur;
            _dragging  = false;

            // **누른 그 순간의 월드 자리를 붙든다.** 콜라이더가 아니라 평면이라 허공을
            // 눌러도 답이 나온다 — 박스의 귀퉁이가 굴 밖이어도 돼야 한다.
            _hasAnchor = DragPoint(cur, out _dragStartWorld);
        }

        // 카메라가 움직였으면 기준점의 **화면 자리**를 다시 낸다. 월드 자리는 그대로다.
        if (mouse.leftButton.isPressed && _hasAnchor && _cam != null)
        {
            var sp = _cam.WorldToScreenPoint(_dragStartWorld);

            // **카메라 뒤로 넘어가면 안 쓴다** — `z`가 음수면 화면 좌표가 뒤집혀 나와
            // 박스가 반대쪽으로 뻗는다. 그때는 마지막으로 멀쩡했던 자리를 그대로 둔다.
            if (sp.z > 0f) _dragStart = new Vector2(sp.x, sp.y);
        }

        if (mouse.leftButton.isPressed && !_dragging &&
            (cur - _dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            _dragging = true;

        // **취소 도구는 되돌리기가 없다** — 지운 것을 다시 찍어 줄 이유가 없으니
        // 미리보기 장부(`_preview`)도 안 쓴다. 상자 안의 찍힌 칸을 그대로 지운다.
        if (mouse.leftButton.isPressed && Current == Tool.Cancel)
        {
            _next.Clear();
            if (_dragging) CollectBox(_dragStart, cur, _next);
            else if (TileUnder(cur, out var one)) _next.Add(one);

            foreach (var t in _next) RockMap.Mark(t, false);
            return;
        }

        IsDesignating = mouse.leftButton.isPressed;

        if (mouse.leftButton.isPressed)
        {
            // **바뀐 것만 고친다**(2026-08-29). 전에는 매 프레임 **전부 지우고 전부 다시**
            // 찍었는데, 그때마다 `RockMap.OnChanged`가 칸 수만큼 터져 **바위 청크가 쉬지 않고
            // 다시 구워졌다.** 다시 굽는 것은 프레임당 두 장뿐이라(예산) 어떤 청크는 표시가
            // 있고 어떤 청크는 없는 상태가 번갈아 보인다 — 화면에서는 **벽이 밀리는 것**처럼
            // 보인다(사용자가 본 그것이다).
            _next.Clear();
            if (_dragging) CollectBox(_dragStart, cur, _next);
            else if (TileUnder(cur, out var t) && RockMap.IsRock(t) && !RockMap.IsBedrock(t)) _next.Add(t);

            // **무를 수 있는 것은 이번 드래그가 찍은 것뿐이다**(2026-08-30).
            //
            // 전에는 상자 안의 칸을 **전부** `_preview`에 넣었다. 그러면 **이미 찍혀 있던
            // 칸**까지 거기 들어가고, 상자가 그 칸을 벗어나는 순간 **남의 지시를 무른다** —
            // 이미 찍어 둔 데 위로 드래그가 한 번 지나가기만 해도 그게 지워졌다
            // (사용자: *"채굴 지시가 사라져"*).
            //
            // **`Mark`가 참을 돌려준 것만 기억한다** — 참은 *"내가 이번에 바꿨다"*는 뜻이고,
            // 이미 찍혀 있던 칸에서는 거짓이라 목록에 안 들어간다. 그래서 무를 것도 없다.
            _undo.Clear();
            foreach (var t in _preview)
                if (!_next.Contains(t)) { RockMap.Mark(t, false); _undo.Add(t); }
            foreach (var t in _undo) _preview.Remove(t);

            foreach (var t in _next)
                if (!_preview.Contains(t) && RockMap.Mark(t, true)) _preview.Add(t);

            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            _hasAnchor    = false;
            _dragging     = false;
            IsDesignating = false;

            // **취소로 놓았으면 일감을 안 낸다** — 지우려고 끈 드래그가 도로 일감을 내면
            // 지운 것이 아니다.
            if (Current == Tool.Cancel) return;

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
        // **두 점이면 된다 — 처음 찍은 칸과 지금 칸**(2026-09-02, 사용자 제안).
        //
        // 전에는 **화면 사각형의 네 귀퉁이**를 떨어뜨려 그 바깥 상자를 썼다. 화면의 네모는
        // 비스듬한 시점에서 지면에서 **사다리꼴**이라, 그 바깥 상자는 **눈에 보이는 것보다
        // 넓다** — 위아래 모서리 쪽에서 한 줄씩 더 찍힌다(*"찍는 곳이 정확히 안 찍힌다"*).
        //
        // 기준 칸에서 지금 칸까지 **상하좌우로 펴면** 각도와 무관하게 정확하고, 건설의 줄
        // 긋기와 같은 손맛이 된다.
        if (!DragPoint(new Vector2(a.x, a.y), out var p0)) return;
        if (!DragPoint(new Vector2(b.x, b.y), out var p1)) return;

        var map = MapManager.Instance;
        if (map == null) return;

        var t0 = map.WorldToTile(p0);
        var t1 = map.WorldToTile(p1);

        int minX = Mathf.Min(t0.x, t1.x), maxX = Mathf.Max(t0.x, t1.x);
        int minY = Mathf.Min(t0.y, t1.y), maxY = Mathf.Max(t0.y, t1.y);

        // **한 번에 너무 넓게 찍는 것을 막는다** — 화면 전체를 드래그하면 수만 칸이 되고,
        // 그만큼의 일감이 큐에 들어가면 배정이 그 자리에서 멎는다.
        //
        // **자를 때 기준점 쪽을 남긴다**(2026-08-30). 그냥 `min` 쪽을 남기면, 위나 왼쪽으로
        // 끌 때 **잘려 나가는 것이 처음 찍은 자리**라 박스가 커커지는 게 아니라 미끄러진다 —
        // 카메라를 밀어 가며 길게 찍을 수 있게 된 뒤로 이게 눈에 보인다.
        const int MaxSpan = 60;
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

        // **이제는 진짜 사각형이다.** 예전에는 화면 사각형이 지면에서 사다리꼴이 되는 것을
        // 다각형 검사로 눌렀는데(2026-08-29), 기준을 **칸**으로 옮긴 뒤로는 애초에 사다리꼴이
        // 안 생긴다 — 검사도 같이 걷어낸다.
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var tile = new Vector2Int(x, y);
            if (!RockMap.IsRock(tile)) continue;

            // **기반암은 안 모은다** — `Mark`가 어차피 거절하지만, 모아 두면 매 프레임
            // 거절당하는 칸이 수백 개다.
            if (RockMap.IsBedrock(tile)) continue;
            into.Add(tile);
        }
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

    // 드래그의 한 점. **콜라이더를 먼저 묻고 없으면 평면으로 떨어뜨린다**(2026-09-07).
    //
    // 전에는 평면만 썼다. 그 이유는 옳았다 — *"콜라이더로 잡으면 귀퉁이 하나가 허공으로
    // 나가는 순간 통째로 아무것도 안 찍힌다."* **틀린 것은 둘 중 하나만 고른 것이다.**
    //
    // 평면만 쓰면 **벽의 옆면을 겨눴을 때 어긋난다**: 평면은 *윗면*의 높이인데 눈이 짚은
    // 것은 그보다 낮은 옆면이라, 그 차이가 화면에서 몇 칸이 된다. 9.6m일 때는 옆면이
    // 화면에서 작아 티가 덜 났지만 **24m가 되면 화면의 벽은 거의 전부 옆면이다.**
    //
    // 콜라이더가 답하면 그것이 **정확하다**(맞은 자리가 곧 그 기둥이고, 옆면은 안쪽으로
    // 밀어 넣어 칸까지 맞다). 못 맞히는 것은 허공뿐이고 거기서만 평면이 답한다.
    bool DragPoint(Vector2 screen, out Vector3 world)
        => Ground(screen, out world) || PlaneHit(screen, out world);

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
        // **이제는 허공을 겨눴을 때의 폴백이다** — 벽 위에서는 `DragPoint`가 콜라이더로 답한다.
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

        // **맞은 면 안쪽으로 조금 밀어 넣는다**(2026-08-30). 벽의 **옆면**을 누르면 맞은 점이
        // 정확히 **칸 경계**에 떨어지는데, `WorldToTile`은 `Floor`라 그 경계가 어느 칸으로
        // 가는지가 **면의 방향에 따라 갈린다** — 서쪽 면을 누르면 그 벽이, 동쪽 면을 누르면
        // **벽 앞의 빈 칸**이 찍힌다(바위가 아니라서 조용히 아무 일도 안 일어난다).
        //
        // 법선 반대로 5cm면 언제나 맞은 물체 **안**이다. 윗면·바닥은 법선이 위라 y만 내려가고
        // x·z는 그대로다 — 그쪽 판정은 한 줄도 안 바뀐다.
        world = hit.point - hit.normal * 0.05f;
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


    // **한 번에 하나다** — 근거는 `ExclusiveMode`. 켤 때 *"내가 켰다"*만 알리고, 끄는 판단은
    // 각자 한다(여기서 남을 직접 끄면 모드가 늘 때마다 이 줄이 늘어난다).
    void OnEnable()  => ExclusiveMode.Claimed += OnOtherClaimed;
    void OnDisable() => ExclusiveMode.Claimed -= OnOtherClaimed;

    void OnOtherClaimed(object who) { if (!ReferenceEquals(who, this)) SetActive(false); }

    void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;
        if (!on) { ClearPreview(); _dragging = false; }

        if (on) ExclusiveMode.Claim(this);

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

            // **높이도 같이 올린다**(2026-09-07). 벽이 24m가 된 뒤로 각도만 돌리면
            // **벽 사이에서 천장을 보는 꼴**이라, 무엇을 찍는지가 여전히 안 보인다.
            // 내려 주지는 않는다 — 멀리서 보며 찍는 것을 모드가 거절할 이유가 없다.
            cam.RiseTo(cam.MineViewHeight);
        }
        else if (_pitchSaved)
        {
            cam.LerpToPitch(_prevPitch);
            _pitchSaved = false;
        }
    }
}

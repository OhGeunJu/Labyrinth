using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// **"저건 가져와라"를 찍는 모드.** H키로 켠다.
//
// **찍는 대상이 둘이다**(2026-09-06) — 바닥의 물건(`DroppedItem` → 운반)과 채집물
// (`Forageable` → 채집). **플레이어에게 그 둘은 같은 것이다**: *저기 있는 가져올 것*.
// 채집 모드를 따로 내면 **같은 손놀림을 두 번 배우게 되고**(드래그로 네모, 우클릭으로 무르기 —
// 조작이 한 글자도 안 다르다) 모드가 하나 늘 때마다 `ExclusiveMode` 식구·모드 바 칸·단축키가
// 같이 는다. 갈리는 것은 **찍힌 뒤에 무슨 일감이 나가느냐**뿐이고 그건 각자가 안다
// (`IGatherTarget`).
//
// 바닥의 물건은 아무도 안 건드리고 체크한 것만 실려 간다(`DroppedItem.HaulAllowed`).
// 그런데 **켜는 길이 우클릭 하나뿐이면 쓸 수가 없다** — 전투가 끝나거나 발견 이벤트가
// 흩뿌리고 나면 물건이 열 몇 개인데 하나씩 눌러야 한다.
//
// **철거 모드와 같은 조작이다**(`BuildModeManager`): 클릭=단일 / 드래그=일괄, 지정될 것이
// 미리 보인다. 새 조작을 배우게 하지 않으려고 일부러 같은 모양으로 뒀다 — 다른 것은
// 색(초록)과, **우클릭이 반대로 지운다**는 것뿐이다.
//
// ── 씬 배선이 필요 없다 ────────────────────────────────────────────────────────
//
// `RuntimeInitializeOnLoadMethod`로 스스로 선다. 이 프로젝트의 기본은 씬 오브젝트 + 생성기지만
// (`docs/editor_creators.md`), 이건 **아무 상태도 안 들고 저작할 것도 없는 순수 입력기**라
// 씬에 둘 이유가 없다 — 정착지가 여럿 생겨도 맵마다 배선을 반복하지 않는다
// (`DroppedItem`이 프리팹 대신 `Resources` 폴백을 쓰는 것과 같은 기준: 없으면 안 되는 것).
public class HaulDesignator : MonoBehaviour
{
    // 드래그로 치는가 — 이만큼 움직여야 박스로 본다(철거 모드와 같은 값).
    const float DragThreshold = 8f;

    public static bool IsActive { get; private set; }

    // ── 도구 둘 ─────────────────────────────────────────────────────────────
    //
    // **지정 / 취소.** 우클릭으로도 지울 수 있지만 그건 **한 칸씩**이라, 넓게 찍은 것을
    // 넓게 무를 길이 없었다. 도구를 바꾸면 **같은 드래그가 반대로 돈다** — 찍을 때 쓰던
    // 손놀림 그대로 지운다.
    public enum Tool { Designate, Cancel }

    public static Tool Current { get; private set; } = Tool.Designate;
    public static void Use(Tool t) => Current = t;


    static readonly Color Fill = new(0.35f, 0.95f, 0.45f, 0.22f);
    static readonly Color Line = new(0.35f, 0.95f, 0.45f, 0.9f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        _instance = SelfStanding.Create<HaulDesignator>("HaulDesignator");
    }

    Camera _cam;
    Vector2 _dragStart;
    bool    _dragging;

    // 지금 지정될 것들 — 미리 보여 주고, 놓는 순간 그대로 적용한다.
    readonly HashSet<IGatherTarget> _pending = new();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.hKey.wasPressedThisFrame) Toggle();

        if (!IsActive) return;

        // 다른 모드가 켜지면 물러난다 — 건설·철거와 드래그가 겹치면 무엇이 찍히는지 알 수 없다.
        if (BuildModeManager.IsActive) { SetActive(false); return; }
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { SetActive(false); return; }

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearPending();
            return;
        }

        Vector2 cur = mouse.position.ReadValue();

        // **우클릭은 반대로 지운다.** 켜는 것과 끄는 것이 같은 모드 안에 있어야 "찍었다 무른다"가
        // 한 흐름이 된다(모드를 또 만들면 조작이 하나 더 는다).
        if (mouse.rightButton.wasPressedThisFrame)
        {
            var under = PickUnder(cur);
            under?.SetDesignated(false);
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame) { _dragStart = cur; _dragging = false; }

        if (mouse.leftButton.isPressed && !_dragging &&
            Vector2.Distance(_dragStart, cur) > DragThreshold)
            _dragging = true;

        RefreshPending(cur);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            // **예정 표식을 반드시 끈다.** 안 끄면 `_pendingMark`가 참인 채로 남아, 나중에
            // 운반을 꺼도 초록 구슬이 계속 떠 있다 — 구슬이 곧 "실려 간다"는 말이므로
            // 그때부터 화면이 거짓말을 한다(구슬을 붙인 이유가 바로 그것이었다).
            foreach (var d in _pending)
            {
                if (d == null) continue;
                d.SetPendingHighlight(false);
                d.SetDesignated(Current == Tool.Designate);
            }

            _pending.Clear();
            _dragging = false;
        }
    }

    // **버튼도 이 문으로 온다**(`ModeBarUI`) — 키로 켠 것과 같은 상태여야 한다.
    public static void Toggle() { if (_instance != null) _instance.SetActive(!IsActive); }

    static HaulDesignator _instance;


    // **한 번에 하나다** — 근거는 `ExclusiveMode`. 켤 때 *"내가 켰다"*만 알리고, 끄는 판단은
    // 각자 한다(여기서 남을 직접 끄면 모드가 늘 때마다 이 줄이 늘어난다).
    void OnEnable()  => ExclusiveMode.Claimed += OnOtherClaimed;
    void OnDisable() => ExclusiveMode.Claimed -= OnOtherClaimed;

    void OnOtherClaimed(object who) { if (!ReferenceEquals(who, this)) SetActive(false); }

    void SetActive(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;
        ClearPending();
        _dragging = false;

        if (on) ExclusiveMode.Claim(this);
    }

    // 커서 아래의 **찍을 수 있는 것**. 드롭도 채집물도 트리거 콜라이더 하나를 갖는다.
    // **부모까지 본다** — 채집물은 콜라이더가 루트에 있지만, 앞으로 자식에 달린 것이
    // 생기면 `GetComponent`만으로는 조용히 안 잡힌다.
    IGatherTarget PickUnder(Vector2 screen)
    {
        var cam = Cam();
        if (cam == null) return null;

        var ray = cam.ScreenPointToRay(screen);
        foreach (var hit in Physics.RaycastAll(ray, 500f))
        {
            var t = hit.collider.GetComponentInParent<IGatherTarget>();
            if (t != null && t.CanDesignate) return t;
        }
        return null;
    }

    void RefreshPending(Vector2 cur)
    {
        var next = new HashSet<IGatherTarget>();

        if (_dragging)
        {
            var cam = Cam();
            if (cam != null)
            {
                float minX = Mathf.Min(_dragStart.x, cur.x), maxX = Mathf.Max(_dragStart.x, cur.x);
                float minY = Mathf.Min(_dragStart.y, cur.y), maxY = Mathf.Max(_dragStart.y, cur.y);

                // **이미 켜진 것은 안 담는다** — 담아도 결과는 같지만 미리보기가 "이번에
                // 새로 찍히는 것"만 보여 주는 편이 무엇을 하는지 분명하다.
                // **둘을 같이 훑는다.** `FindObjectsByType`은 인터페이스를 못 받으므로
                // 종류마다 한 번씩 부른다 — 목록을 따로 들면 등록·해제가 또 규칙이 된다.
                Collect<DroppedItem>(cam, next, minX, maxX, minY, maxY);
                Collect<Forageable>(cam, next, minX, maxX, minY, maxY);

                // **야생 자원 노드도 같은 모드가 찍는다**(2026-09-06) — 지은 것은
                // `CanDesignate`가 거짓이라 저절로 빠진다.
                Collect<ResourceNode>(cam, next, minX, maxX, minY, maxY);
            }
        }
        else
        {
            var under = PickUnder(cur);
            if (under != null && under.Designated != (Current == Tool.Designate)) next.Add(under);
        }

        foreach (var d in _pending)
            if (d != null && !next.Contains(d)) d.SetPendingHighlight(false);
        foreach (var d in next)
            if (!_pending.Contains(d)) d.SetPendingHighlight(true);

        _pending.Clear();
        foreach (var d in next) _pending.Add(d);
    }

    void Collect<T>(Camera cam, HashSet<IGatherTarget> into,
                    float minX, float maxX, float minY, float maxY)
        where T : Component
    {
        foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            // **취소 도구는 반대를 모은다** — 이미 찍힌 것만 잡아야 지울 것이 생긴다.
            if (c is not IGatherTarget t || !t.CanDesignate) continue;
            if (t.Designated == (Current == Tool.Designate)) continue;

            var sp = cam.WorldToScreenPoint(t.Where.position);
            if (sp.z >= 0f && sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY)
                into.Add(t);
        }
    }

    void ClearPending()
    {
        foreach (var d in _pending)
            if (d != null) d.SetPendingHighlight(false);
        _pending.Clear();
    }

    Camera Cam()
    {
        // 월드맵에 가면 필드 카메라가 꺼진다 — 매번 다시 묻는다(`PawnContextMenuUI`와 같은 함정).
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        return _cam;
    }

    void OnGUI()
    {
        if (!IsActive) return;

        GUI.color = new Color(0.75f, 1f, 0.8f);
        GUI.Label(new Rect(10, 34, 720, 24),
                  "[가져오기 지정] 바닥 물건·버섯 · 클릭=단일 / 드래그=일괄 · 우클릭=해제  H=끄기  Esc=나가기");
        GUI.color = Color.white;

        if (!_dragging || Mouse.current == null) return;

        Vector2 cur = Mouse.current.position.ReadValue();
        Vector2 s = new(_dragStart.x, Screen.height - _dragStart.y);
        Vector2 e = new(cur.x,        Screen.height - cur.y);
        Rect rect = new(Mathf.Min(s.x, e.x), Mathf.Min(s.y, e.y),
                        Mathf.Abs(s.x - e.x), Mathf.Abs(s.y - e.y));

        GUI.color = Fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Line;
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
    }
}

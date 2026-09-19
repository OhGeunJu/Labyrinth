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

    // ── 무엇을 찍나 — 운반 ↔ 채집 (2026-09-19) ──────────────────────────────
    //
    // (사용자: *"운반처럼 채집도 한번에 드래그 할 수 있게 해야지 … 채굴 채집 운반 세 버튼을 명령으로
    //  합쳐서"*.) 이 모드는 원래 바닥 물건·채집물·야생 광맥을 **한꺼번에** 찍었다 — 채집도 이미 드래그로
    // 되고 있었는데 **버튼 이름이 `운반`이라 아무도 몰랐다.** 명령 도구 바가 둘을 가르면서 여기도 가른다:
    // **모드는 하나이고 대상만 바뀐다**(조작이 한 글자도 안 다르다는 위의 근거는 그대로다).
    //   · 운반 = 바닥의 물건(`DroppedItem`)
    //   · 채집 = 채집물(`Forageable`)과 야생 광맥(`ResourceNode` — 지은 것은 `CanDesignate`가 뺀다)
    public enum Target { Haul, Gather }
    public static Target Kind { get; private set; } = Target.Haul;

    // 명령 도구 바가 부른다 — 대상을 바꾸고 켠다(이미 켜져 있으면 대상만 바뀐다).
    public static void Open(Target kind)
    {
        Kind    = kind;
        Current = Tool.Designate;
        if (_instance == null) return;
        _instance.ClearPending();
        _instance.SetActive(true);
    }

    static bool Accepts(IGatherTarget t) => Kind == Target.Haul
        ? t is DroppedItem
        : t is Forageable || t is ResourceNode;

    // 무를 때는 **무엇이든** 무른다 — 취소는 도구를 안 가린다(`CommandMode` 머리말).
    bool Takes(IGatherTarget t) => !Designating || Accepts(t);

    public static void Close() { if (_instance != null) _instance.SetActive(false); }

    // **다른 지정기가 무를 때 이쪽 지정도 같이 무르는 문**(채굴 지정기의 취소 · 우클릭 드래그).
    public static void UndesignateRect(Vector2 a, Vector2 b)
    {
        if (_instance == null) return;
        var cam = _instance.Cam();
        if (cam == null) return;
        float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
        float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
        UndoIn<DroppedItem>(cam, minX, maxX, minY, maxY);
        UndoIn<Forageable>(cam, minX, maxX, minY, maxY);
        UndoIn<ResourceNode>(cam, minX, maxX, minY, maxY);
    }

    public static void UndesignateAt(Vector2 p)
    {
        if (_instance == null) return;
        var cam = _instance.Cam();
        if (cam == null) return;
        foreach (var hit in VisiblePick.All(cam.ScreenPointToRay(p), 500f))
        {
            var t = hit.collider.GetComponentInParent<IGatherTarget>();
            if (t != null && t.CanDesignate && t.Designated) { t.SetDesignated(false); return; }
        }
    }

    static void UndoIn<T>(Camera cam, float minX, float maxX, float minY, float maxY) where T : Component
    {
        foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (c is not IGatherTarget t || !t.CanDesignate || !t.Designated) continue;
            var sp = cam.WorldToScreenPoint(t.Where.position);
            if (sp.z >= 0f && sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY)
                t.SetDesignated(false);
        }
    }


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
        var kb = Keyboard.current;   // 켜는 키는 `CommandMode`가 듣는다(J 채집 · K 운반)

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

        // **우클릭은 반대로 지운다 — 누르면 하나, 끌면 상자째**(2026-09-19). 켜는 것과 끄는 것이 같은 모드
        // 안에 있어야 "찍었다 무른다"가 한 흐름이 된다. 명령 도구 바에서 `취소` 타일이 빠지면서(세 칸이
        // 채굴·채집·운반이 됐다) 넓게 무르는 길을 **오른쪽 드래그**가 맡는다.
        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
        {
            _dragStart  = cur;
            _dragging   = false;
            _rightHeld  = mouse.rightButton.wasPressedThisFrame;
        }

        bool held     = _rightHeld ? mouse.rightButton.isPressed : mouse.leftButton.isPressed;
        bool released = _rightHeld ? mouse.rightButton.wasReleasedThisFrame : mouse.leftButton.wasReleasedThisFrame;

        if (held && !_dragging && Vector2.Distance(_dragStart, cur) > DragThreshold)
            _dragging = true;

        // 오른쪽을 안 끌고 놓으면 **하나만** 무른다(예전 우클릭 그대로).
        if (released && _rightHeld && !_dragging)
        {
            PickUnder(cur)?.SetDesignated(false);
            MineDesignator.UnmarkAt(cur);   // 무르는 것은 무엇이든 — 캘 벽 표시도
            ClearPending();
            _rightHeld = false;
            return;
        }

        RefreshPending(cur);

        if (released)
        {
            // 상자째 무를 때는 **캘 벽 표시도** 같이 무른다(취소는 도구를 안 가린다).
            if (_dragging && !Designating) MineDesignator.UnmarkRect(_dragStart, cur);

            // **예정 표식을 반드시 끈다.** 안 끄면 `_pendingMark`가 참인 채로 남아, 나중에
            // 운반을 꺼도 초록 구슬이 계속 떠 있다 — 구슬이 곧 "실려 간다"는 말이므로
            // 그때부터 화면이 거짓말을 한다(구슬을 붙인 이유가 바로 그것이었다).
            foreach (var d in _pending)
            {
                if (d == null) continue;
                d.SetPendingHighlight(false);
                d.SetDesignated(Designating);
            }

            _pending.Clear();
            _dragging  = false;
            _rightHeld = false;
        }
    }

    // **버튼도 이 문으로 온다**(`ModeBarUI`) — 키로 켠 것과 같은 상태여야 한다.
    // `H`로 켜면 **운반**이다(채집은 명령 도구 바에서 고른다).
    public static void Toggle()
    {
        if (_instance == null) return;
        if (!IsActive) Kind = Target.Haul;
        _instance.SetActive(!IsActive);
    }

    static HaulDesignator _instance;

    // 지금 드래그가 **찍는가**(왼쪽 · 찍기 도구) **무르는가**(오른쪽 · 또는 취소 도구).
    bool _rightHeld;
    bool Designating => !_rightHeld && Current == Tool.Designate;


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
        foreach (var hit in VisiblePick.All(ray, 500f))   // 짚는 문은 한 벌(`VisiblePick`)
        {
            var t = hit.collider.GetComponentInParent<IGatherTarget>();
            if (t != null && t.CanDesignate && Takes(t)) return t;
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
            if (under != null && under.Designated != Designating) next.Add(under);
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
            if (c is not IGatherTarget t || !t.CanDesignate || !Takes(t)) continue;
            if (t.Designated == Designating) continue;

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

        // 왼쪽 위의 안내 글은 뺐다(2026-09-19, 사용자: *"왼쪽위에 초록 글씨가 뜨는데 채굴은 그런게 없어 다
        // 없애버려"*) — 지금 무슨 도구인지는 도구 바가 답한다(`ModeToolBarUI`). 드래그 상자만 그린다.

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

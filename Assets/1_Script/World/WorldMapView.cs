using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 마운트앤블레이드식 실시간 월드맵 뷰 (같은 씬, 모드 전환).
// **여기는 "무엇을 하는가"만 담당한다** — 입력(M/클릭/Enter), 파티 토큰 이동, 거점 진입.
// 원정대 편성은 `ExpeditionUI`(G) + `ExpeditionFormation`(집결) 몫이다.
// 좌표 변환·격자선·오브젝트 배치는 `WorldMapGrid` 몫이다.
//
// - **지형은 씬에 직접 만들어 붙인다**(Unity Terrain이든 모델링한 메시든). 코드는 지형을 만들지 않는다.
//   격자는 그 위에 얹힌 논리 레이어다. 안개 없음(2026-06-20 결정) — 처음부터 전부 보인다.
// - **원정대는 여럿일 수 있다**(2026-07-30). 나가 있는 팀마다 토큰이 하나씩 서고,
//   나가 있는 팀이 없으면 토큰도 없다. 선택 방식은 **필드맵과 같다** —
//   좌클릭 = 단일 선택, 드래그 = 사각 다중 선택, 빈 곳 클릭 = 해제. 우클릭 = 선택한 팀만 이동.
// - 파티 토큰은 **격자에 매이지 않고 자유롭게** 이동한다 — 우클릭한 지점으로 직진하며 지형 표면을 탄다.
//   격자는 이동 제약이 아니라 "지금 어느 지역에 있나"(바이옴·정착지 판정)를 읽는 논리 레이어일 뿐이다.
// - **도착 메뉴는 마운트앤블레이드식** — **어느 칸이든 도착하면** `WorldMenuUI`가 저절로 열린다
//   (빈 야생 포함 — 아무 데나 조사하고 자리를 잡을 수 있어야 한다). Enter는 선택한 팀의 메뉴 재진입.
// - **매장 자원은 조사해야 보인다**(안개는 없다 — 가려지는 건 세부 수치뿐).
//   `WorldMap.IsSurveyed` / `DepositsAt`, 값 자체는 `WorldDeposits`가 칸 좌표에서 결정적으로 뽑는다.
// - M키로 필드↔월드 전환.
//
// **뷰 전환 자체는 이 스크립트가 하지 않는다** — 화면·입력을 어느 쪽에 줄지는 `GameModeManager` 몫이고
// 여기선 모드를 요청한다. **거점 시뮬은 월드맵을 보는 동안에도 계속 돈다**(2026-07-30 결정).
[DefaultExecutionOrder(-40)]
public class WorldMapView : MonoBehaviour
{
    [SerializeField] Camera worldCamera;     // 월드맵 전용 카메라 (기본 비활성)
    [SerializeField] Transform mapRoot;      // 월드 측 루트 — **저작한 지형과 격자·토큰이 여기 아래**
    [SerializeField] WorldMapGrid grid;      // 비우면 mapRoot에서 탐색·부착
    [SerializeField] WorldMenuUI menuPanel;     // 도착 메뉴 — 비우면 씬에서 탐색
    [SerializeField] WorldTileVisualData visuals;  // 도착 메뉴의 지역 그림 출처(선택)
    [SerializeField] float partySpeed = 5f;  // 월드유닛/초
    [SerializeField] float tokenHover = 0.4f; // 지형 표면에서 토큰을 띄우는 높이
    [SerializeField] Key toggleKey = Key.M;

    [Header("팀 선택 (필드맵과 같은 조작)")]
    [SerializeField] float dragThreshold = 5f;      // 이보다 끌면 사각 선택
    [SerializeField] Color tokenColor    = new(1f, 0.85f, 0.2f);
    [SerializeField] Color selectedColor = new(1f, 1f, 0.75f);
    [SerializeField] Color boxColor      = new(0.6f, 0.9f, 1f, 0.25f);

    // GameModeManager가 배선 없이 집어갈 수 있도록 노출 (월드 측 카메라/루트의 단일 출처).
    public Camera    WorldCamera => worldCamera;
    public Transform MapRoot     => mapRoot;

    // 지도 위의 **다른 것들**(무리)이 격자와 원정대 위치를 물어볼 창구.
    // `WorldBandManager`가 자기 토큰을 같은 지형에 앉히고, 쫓을 원정대를 찾는 데 쓴다.
    public static WorldMapView Instance { get; private set; }
    public WorldMapGrid Grid => grid;

    // 무리에게 다가가서 **무엇을 할 작정인가.** 필드의 우클릭 선택지와 같은 축이다 —
    // 고르는 순간 정해지고, 다다르면 그대로 벌어진다.
    public enum BandIntent
    {
        None,      // 작정 없음 — 그 자리로 가기만 한다. **중립은 멈춰 세우지 않는다**
        Talk,      // 가서 말을 건다 — 맞닿으면 조우 메뉴가 뜬다(뭘 할지 그때 고른다)
        Attack,    // 다다르는 즉시 싸운다
        Trade,     // 다다르는 즉시 상점을 연다
        Follow,    // 계속 따라다닌다. 중립이면 조용히 뒤를 잇고, 적대면 저쪽이 먼저 붙는다
    }

    // 월드맵 위의 원정대 하나 — 논리(파티)와 화면(토큰)과 이동 상태를 묶는다.
    class PartyToken
    {
        public ExpeditionParty Party;
        public GameObject      Go;
        public Renderer        Rend;
        public Vector3         MoveTarget;
        public bool            Moving;
        public bool            Selected;

        // **쫓아가는 중인 무리.** 목적지를 클릭한 순간의 좌표로 굳히면 안 된다 — 무리는 우리가
        // 다가가는 동안에도 걷고 있어서, 도착해 보면 몇 미터 옆에 서 있다(중립 무리는 우리가
        // `waitRadius` 안에 들어야 멈춘다). 그러면 맞닿지 못해 말을 걸 수가 없다.
        public WorldBand       Approaching;

        // **가서 무엇을 할 것인가.** 필드에서 우클릭으로 "공격한다"를 고르면 폰이 걸어가서
        // 때리는 것과 같다 — 다다른 뒤에 창을 한 번 더 띄우면 같은 결정을 두 번 하게 된다.
        public BandIntent      Intent;
    }

    readonly List<PartyToken> _parties  = new();
    readonly List<PartyToken> _selected = new();
    readonly Dictionary<GameObject, PartyToken> _byGo = new();   // 레이캐스트 → 팀

    PartyToken _menuParty;      // 지금 열린 도착 메뉴가 누구 것인가

    // 지금 열린 창이 **무리에 관한 것**인가(정보·조우·전투 결과). 조우는 칸 메뉴를 밀어내도
    // 되지만 다른 무리가 끼어들어 결과창을 덮으면 무슨 일이 있었는지 못 읽는다.
    bool _bandMenuOpen;
    Vector2 _dragStart;
    bool    _dragging;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        if (mapRoot == null || WorldMap.Instance == null)
        {
            Debug.LogWarning("[WorldMapView] mapRoot / WorldMap.Instance 미설정");
            return;
        }

        EnsureGrid();
        if (menuPanel == null) menuPanel = FindFirstObjectByType<WorldMenuUI>(FindObjectsInactive.Include);
        if (menuPanel != null) menuPanel.Closed += OnMenuClosed;

        grid.Build();
        WorldMap.OnNodeChanged += OnNodeChanged;
        // **토큰은 여기서 만들지 않는다** — 나가 있는 팀이 생겼을 때만 선다(`AddMembers`).
        // 초기 모드(필드) 적용은 GameModeManager.Start가 한다.
    }

    void EnsureGrid()
    {
        if (grid != null) return;
        grid = mapRoot.GetComponentInChildren<WorldMapGrid>(true)
            ?? mapRoot.gameObject.AddComponent<WorldMapGrid>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        WorldMap.OnNodeChanged -= OnNodeChanged;
        if (menuPanel != null) menuPanel.Closed -= OnMenuClosed;
    }

    // 그 지점에서 반경 안에 있는 **가장 가까운 원정대**의 위치. 무리가 쫓을 대상을 찾을 때
    // 쓴다 — 원정대 토큰의 위치를 아는 것은 여기뿐이므로, 목록을 통째로 내주는 대신
    // 물음에만 답한다.
    public bool TryNearestParty(Vector3 from, float radius, out Vector3 pos)
    {
        pos = Vector3.zero;
        float best = radius * radius;
        bool found = false;

        foreach (var pt in _parties)
        {
            if (pt.Go == null) continue;
            var p = pt.Go.transform.position;
            float d = (p - from).sqrMagnitude;
            if (d >= best) continue;
            best  = d;
            pos   = p;
            found = true;
        }
        return found;
    }

    void OnMenuClosed()
    {
        _menuParty    = null;
        _bandMenuOpen = false;
        grid?.ClearHighlight();
    }

    // **세계가 이어지면 이 화면은 갈 곳이 없다**(2026-08-26).
    //
    // 이 창은 *"필드맵이 200m 한 장"*이라는 전제 위에 서 있었다 — 그래서 지도가 **이동하는
    // 곳**이었고, 3D로 그려서 원정대 토큰을 걷게 했다. 세계가 16,384m로 이어지면서 **진짜로
    // 걸어 다니는 곳은 필드**가 되었고, 지도는 `WorldMapPanel`(2D 텍스처 · M키)이 대신한다.
    //
    // **아직 지우지는 않는다.** 이 파일을 가리키는 곳이 **40군데 / 15파일**이고
    // (`GameModeManager` 26곳, 원정 편성 37곳이 더 있다) 씬 배선까지 걸려 있다.
    // **진입로만 끊고 코드는 남긴다** — `WorldSite`(마을·소굴을 실제 좌표에)와
    // `BandMaterializer`(무리 실체화)가 화면에서 확인되기 전에 지우면 돌아갈 데가 없다.
    [Tooltip("옛 3D 월드맵 화면. 세계가 이어진 뒤로는 `WorldMapPanel`(M키)이 대신한다. " +
             "켜면 옛 화면이 M키로 다시 열린다 — 지도가 둘 뜬다.")]
    [SerializeField] bool legacyWorldScreen = false;

    void Update()
    {
        var kb = Keyboard.current;
        if (legacyWorldScreen && kb != null && kb[toggleKey].wasPressedThisFrame)
        {
            CloseMenu();
            GameModeManager.Instance?.Toggle();
            return;   // 전환한 프레임에 반대편 입력까지 처리하지 않게
        }

        // **조우는 지도를 보고 있지 않아도 일어난다.** 지도가 계속 도는 이상 무리도 계속
        // 걷고, 나가 있는 원정대는 그 자리에 있다. 다만 필드를 보는 중엔 **메뉴를 열지 않고
        // 알리기만 한다** — 조작 없이 판정해 버리면 싸울지 도망칠지를 뺏는 것이고,
        // 조용히 넘기면 돌아왔을 때 원정대가 왜 다쳤는지 알 수가 없다.
        CheckEncounters();

        if (!GameModeManager.IsWorld) return;

        HandleSelection();
        HandleMoveOrder();
        HandleEnter(kb);
        if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseMenu();
        MoveParties();
    }

    void OnNodeChanged(Vector2Int node) => grid?.RefreshNode(node);

    // ── 팀 선택 (필드맵 `SelectionManager`와 같은 규칙) ────────────────────────

    void HandleSelection()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (OverUI()) return;
            _dragStart = mouse.position.ReadValue();
            _dragging  = false;
        }

        if (mouse.leftButton.isPressed && !OverUI())
        {
            if (!_dragging && Vector2.Distance(_dragStart, mouse.position.ReadValue()) > dragThreshold)
                _dragging = true;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (OverUI()) { _dragging = false; return; }

            if (_dragging) SelectByRect();
            else           SelectSingle();

            _dragging = false;
        }
    }

    void SelectSingle()
    {
        // **무리는 선택을 지우지 않는다.** 들여다보는 것이지 고르는 것이 아니라서, 데려갈 팀을
        // 고른 채로 상대를 살펴볼 수 있어야 한다(바닥의 드롭·자원 노드와 같은 규칙).
        if (TryPickToken(out var picked, out var band))
        {
            if (band != null) { OpenBandInfo(band); return; }
            ClearSelection();
            Select(picked);
            return;
        }

        ClearSelection();

        // 팀이 아니라 땅을 눌렀다 — **내 지역이면 원정대 없이도 정보를 연다.**
        // 채집기지에 얼마나 쌓였는지 보려고 매번 팀을 보낼 순 없다.
        // 남의 땅·야생은 예전대로 가봐야 안다(M&B식 "도착해야 열린다").
        if (grid == null || !TryPickTerrain(out var hit) || !grid.TryNodeAt(hit, out var node))
        {
            CloseMenu();
            return;
        }

        var tile = WorldMap.Instance.GetTile(node);
        if (tile.Content != WorldNodeContent.MySettlement && tile.Content != WorldNodeContent.Outpost)
        {
            CloseMenu();
            return;
        }

        // 그 칸에 서 있는 팀이 있으면 그 팀의 메뉴로 — 없으면 파티 없이(정보만).
        OpenMenu(PartyAt(node), node);
    }

    // 그 칸에 멈춰 있는 팀 하나. 이동 중인 팀은 "거기 있다"고 보지 않는다.
    PartyToken PartyAt(Vector2Int node)
    {
        foreach (var p in _parties)
            if (!p.Moving && p.Party.CurrentNode == node) return p;
        return null;
    }

    void SelectByRect()
    {
        ClearSelection();

        var rect = ScreenRect(_dragStart, Mouse.current.position.ReadValue());
        foreach (var pt in _parties)
        {
            if (pt.Go == null) continue;
            var sp = worldCamera.WorldToScreenPoint(pt.Go.transform.position);
            if (sp.z > 0f && rect.Contains(sp, true)) Select(pt);
        }
    }

    void Select(PartyToken pt)
    {
        if (pt.Selected) return;
        pt.Selected = true;
        _selected.Add(pt);
        Tint(pt);
    }

    void ClearSelection()
    {
        foreach (var pt in _selected) { pt.Selected = false; Tint(pt); }
        _selected.Clear();
    }

    void Tint(PartyToken pt)
    {
        if (pt.Rend == null) return;
        SetColor(pt.Rend.material, pt.Selected ? selectedColor : tokenColor);
    }

    // 드래그 사각형. 필드의 `SelectionBox`는 필드 전용이라 월드에선 꺼져 있어 여기서 직접 그린다.
    void OnGUI()
    {
        if (!_dragging || !GameModeManager.IsWorld) return;

        var cur = Mouse.current.position.ReadValue();
        // GUI는 Y축이 반대다.
        var s = new Vector2(_dragStart.x, Screen.height - _dragStart.y);
        var e = new Vector2(cur.x,        Screen.height - cur.y);
        var r = new Rect(Mathf.Min(s.x, e.x), Mathf.Min(s.y, e.y),
                         Mathf.Abs(s.x - e.x), Mathf.Abs(s.y - e.y));

        var prev = GUI.color;
        GUI.color = boxColor;
        GUI.Box(r, GUIContent.none);
        GUI.color = prev;
    }

    static Rect ScreenRect(Vector2 a, Vector2 b) =>
        new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
            Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    static bool OverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // ── 레이캐스트 ────────────────────────────────────────────────────────────
    // 토큰에도 콜라이더가 있으므로 **한 번에 다 받아서 골라낸다** —
    // 단순 Raycast면 팀을 고르려는 클릭이 지형에 먼저 맞거나, 이동 명령이 토큰에 막힌다.

    RaycastHit[] SortedHits()
    {
        if (worldCamera == null || Mouse.current == null) return System.Array.Empty<RaycastHit>();
        var ray = worldCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        // 토큰이 트리거이므로 **명시적으로** 트리거까지 받는다(프로젝트의 전역 설정에 기대지 않는다).
        var hits = Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        return hits;
    }

    // 토큰(원정대 ∥ 무리) 중 **가장 가까이 맞은 하나**. 둘을 따로 훑으면 겹쳐 있을 때
    // 뒤에 있는 것이 먼저 잡힌다 — 눈에 보이는 것과 눌리는 것이 어긋난다.
    bool TryPickToken(out PartyToken pt, out WorldBand band)
    {
        pt   = null;
        band = null;
        if (OverUI()) return false;

        var mgr = WorldBandManager.Instance;
        foreach (var h in SortedHits())
        {
            if (h.collider == null) continue;
            var go = h.collider.gameObject;

            if (_byGo.TryGetValue(go, out pt)) return true;

            var b = mgr != null ? mgr.BandFor(go) : null;
            if (b != null) { band = b; return true; }
        }

        pt = null;
        return false;
    }

    // 토큰도 트리거도 아닌 첫 충돌 = 지형.
    // 지형은 솔리드 콜라이더이므로 트리거를 걸러내면 마커·토큰 같은 장식에 이동 명령이 막히지 않는다.
    bool TryPickTerrain(out RaycastHit hit)
    {
        hit = default;
        if (OverUI()) return false;

        foreach (var h in SortedHits())
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (_byGo.ContainsKey(h.collider.gameObject)) continue;
            hit = h;
            return true;
        }
        return false;
    }

    // ── 이동 ─────────────────────────────────────────────────────────────────

    // 우클릭 = **선택한 팀만** 이동. 필드에서 폰에게 이동을 명령하는 버튼과 같다.
    void HandleMoveOrder()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;

        // 명령 창이 떠 있으면 이번 우클릭은 **창을 닫는 데만** 쓴다(창 쪽이 닫는다).
        // 여기서도 처리하면 창을 닫으면서 동시에 명령이 나간다 — `MoveController`와 같은 규약.
        if (PawnContextMenuUI.Instance != null && PawnContextMenuUI.Instance.IsOpen) return;
        if (_selected.Count == 0) return;

        // **무리를 겨눴으면 명령 창이 뜬다** — 필드에서 유닛을 우클릭하는 것과 같은 규약이다.
        // 우클릭 하나에 뜻이 여럿(치러 간다 / 사러 간다 / 따라간다)일 때 그 갈림길을 화면에 낸다.
        if (TryPickToken(out _, out var band) && band != null) { OpenBandOrders(band); return; }

        if (!TryPickTerrain(out var hit)) return;

        var spots = Formation(hit.point, _selected.Count);
        for (int i = 0; i < _selected.Count; i++)
        {
            _selected[i].MoveTarget  = spots[i];
            _selected[i].Moving      = true;
            _selected[i].Approaching = null;   // 딴 데로 보내는 것은 쫓기를 그만두는 뜻이다
            _selected[i].Intent      = BandIntent.None;
        }
    }

    // 여러 팀을 한 점으로 보내면 겹친다 — 격자로 벌려 세운다.
    Vector3[] Formation(Vector3 center, int count)
    {
        var result = new Vector3[count];
        if (count == 1) { result[0] = center; return result; }

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        float gap = grid.NodeSpacing * 0.5f;
        for (int i = 0; i < count; i++)
        {
            int row = i / cols, col = i % cols;
            result[i] = center + new Vector3((col - (cols - 1) * 0.5f) * gap, 0f,
                                             (row - (Mathf.CeilToInt(count / (float)cols) - 1) * 0.5f) * gap);
        }
        return result;
    }

    void MoveParties()
    {
        for (int i = _parties.Count - 1; i >= 0; i--)
        {
            var pt = _parties[i];

            // 전투에서 죽은 사람을 걷어낸다 — 인원수가 토큰 크기·등짐 한도·조우 창의
            // "우리 N명"에 그대로 쓰이는데, 파괴된 참조가 남아 그 숫자가 거짓말을 했다.
            pt.Party?.PruneDead();

            if (!pt.Moving || pt.Go == null) continue;

            // 쫓는 중이면 **매 프레임 다시 겨눈다.** 놓친 무리(돌아갔거나 죽은)는 쫓기를 접는다.
            if (pt.Approaching != null)
            {
                var mgr = WorldBandManager.Instance;
                if (mgr != null && mgr.Has(pt.Approaching)) pt.MoveTarget = pt.Approaching.Position;
                else                                        pt.Approaching = null;
            }

            var pos  = pt.Go.transform.position;
            var from = new Vector3(pos.x, 0f, pos.z);
            var to   = new Vector3(pt.MoveTarget.x, 0f, pt.MoveTarget.z);

            var next = Vector3.MoveTowards(from, to, partySpeed * Time.deltaTime);
            pt.Go.transform.position = GroundedTokenPos(next, pt);

            // 지나온 자리의 칸을 계속 갱신 — 어느 지역에 있는지는 여기서 나온다.
            if (grid.TryNodeAt(next, out var node)) pt.Party.CurrentNode = node;

            if ((next - to).sqrMagnitude >= 0.0025f) continue;

            // **맞닿은 무리가 있으면 그쪽이 이긴다.** 예전엔 무조건 칸 메뉴를 열었는데,
            // 무리 위로 걸어가면 `빈 지역 / 탐험한다 / 떠난다`가 뜨고 정작 눈앞의 무리는
            // 말을 걸 수가 없었다 — 게다가 그 메뉴가 열려 있는 동안은 `CheckEncounters`가
            // 통째로 쉬어서, 닫기 전까지 조우가 영영 안 났다.
            var band = WorldBandManager.Instance?.FindContact(pt.Go.transform.position,
                                                              b => Interrupts(pt, b));
            if (band != null)
            {
                // 따라다니는 중이면 **멈추지 않는다** — 상대가 다시 걸으면 같이 걸어야 한다.
                if (pt.Approaching == band && pt.Intent == BandIntent.Follow && !IsHostile(band)) continue;

                pt.Moving = false;
                ResolveContact(pt, band);
                continue;
            }

            pt.Moving      = false;
            pt.Approaching = null;
            pt.Intent      = BandIntent.None;

            // **도착하면 메뉴가 저절로 열린다**(M&B 방식).
            // 빈 야생에서도 연다 — 아무 칸에나 자리를 잡을 수 있어야 하므로 거기서도 할 일이 있다.
            OpenMenu(pt, pt.Party.CurrentNode);
        }
    }

    Vector3 GroundedTokenPos(Vector3 worldXZ, PartyToken pt)
    {
        float size = TokenSize(pt);
        var   surf = grid.SurfaceAtWorld(worldXZ);
        return new Vector3(worldXZ.x, surf.y + tokenHover + size, worldXZ.z);
    }

    float TokenSize(PartyToken pt) =>
        grid.NodeSpacing * (0.35f + Mathf.Min(pt.Party.Members.Count, 8) * 0.03f);

    // ── 조우 ─────────────────────────────────────────────────────────────────
    //
    // 마운트앤블레이드식이다: **길 위에서 맞닿으면 메뉴가 뜬다.** 무엇을 고를 수 있는지는
    // 세력 관계가 정한다 — 적대면 싸우거나 도망치고, 중립 상단이면 거래하거나 덮친다.
    //
    // 판정이 여기 있는 이유: 조우는 **원정대의 이동을 멈추고 창을 여는 일**이고, 그 둘의
    // 주인이 이 스크립트다. 무리를 움직이고 무리끼리 붙이는 것은 `WorldBandManager` 몫이라
    // 서로 자기가 가진 것에만 답한다.

    void CheckEncounters()
    {
        var mgr = WorldBandManager.Instance;
        if (mgr == null || _parties.Count == 0) return;

        // **조우는 칸 메뉴를 밀어낸다.** 예전엔 창이 열려 있으면 무조건 쉬었는데, 도착 메뉴가
        // 저절로 뜨는 구조라 그 창을 닫기 전까지 조우가 영영 안 났다.
        // 다만 이미 무리에 관한 창(정보·조우·전투 결과)이 떠 있으면 건드리지 않는다 —
        // 다른 무리가 끼어들어 결과창을 덮으면 무슨 일이 있었는지 못 읽는다.
        if (_bandMenuOpen && menuPanel != null && menuPanel.IsOpen) return;

        foreach (var pt in _parties)
        {
            if (pt.Go == null || pt.Party.Members.Count == 0) continue;

            var band = mgr.FindContact(pt.Go.transform.position, b => Interrupts(pt, b));
            if (band == null) continue;

            // 필드를 보는 중이면 알리기만 하고 붙들어 둔다. 유예를 걸어야 매 프레임
            // 같은 알림이 쏟아지지 않는다 — 지도로 넘어오면 그때 메뉴가 뜬다.
            if (!GameModeManager.IsWorld)
            {
                mgr.PushApart(band);
                EventNotificationUI.Notify("조우",
                    $"원정대가 {band.Summary}와(과) 마주쳤습니다. M키로 지도를 여세요.",
                    holdWhenHidden: true);
                return;
            }

            // 따라다니는 중인 **중립** 무리와는 매번 창을 띄우지 않는다 — 따라가기는
            // 붙어 다니는 것이지 말을 거는 것이 아니다. 적대는 저쪽이 가만두지 않으므로 뜬다.
            if (pt.Approaching == band && pt.Intent == BandIntent.Follow && !IsHostile(band)) continue;

            pt.Moving = false;   // 지나쳐 가면서 창만 뜨면 대응할 새가 없다
            ResolveContact(pt, band);
            return;              // 한 번에 하나만 — 창은 하나뿐이다
        }
    }

    // 이 무리가 우리를 **멈춰 세우는가.**
    //
    // **적만 그렇다.** 적은 지나칠 수가 없다 — 싸우든 도망치든 항복하든 무엇이든 해야 한다.
    // 중립은 길에서 스쳐도 아무 일이 아니고, 매번 창이 뜨면 지도를 가로지르는 것 자체가
    // 번거로워진다(마을 앞 순찰대를 지날 때마다 걸린다). 중립과 할 일이 있으면 **우클릭으로
    // 작정하고 간다** — 그 작정은 여기서도 받아 준다.
    bool Interrupts(PartyToken pt, WorldBand band) =>
        IsHostile(band) || (pt.Approaching == band && pt.Intent != BandIntent.None);

    // 맞닿았다 — **작정하고 온 것이면 그대로 벌어지고, 아니면 무엇을 할지 그때 고른다.**
    //
    // 작정이 그 무리를 향한 것일 때만 실행한다: 상단을 사러 가던 길에 도적떼와 부딪혔는데
    // 상점이 열리면 안 된다.
    void ResolveContact(PartyToken pt, WorldBand band)
    {
        var intent = pt.Approaching == band ? pt.Intent : BandIntent.None;

        // 쫓기는 여기서 끝난다(따라가기만 빼고). 안 지우면 [도망친다]를 고른 다음 프레임에
        // 목표를 다시 겨눠 **도망치자마자 제 발로 쫓아간다.**
        if (intent != BandIntent.Follow)
        {
            pt.Approaching = null;
            pt.Intent      = BandIntent.None;
        }

        switch (intent)
        {
            case BandIntent.Attack:
                Fight(pt, band);
                return;

            case BandIntent.Trade when band.Shop != null && band.Shop.Data != null:
                TradeWithBand(pt, band);
                return;
        }

        // 작정이 없거나(우연히 마주쳤다) 그새 사정이 바뀌었으면(팔 것이 없어졌다) 물어본다.
        OpenEncounterMenu(pt, band);
    }

    // 이 무리가 우리에게 적인가. **방향은 둘 다 본다** — 한쪽만 적대인 관계가 성립하므로
    // (산적 → 마을사람 적대 / 마을사람 → 산적 중립), 어느 쪽이든 적대면 평화롭게 지나갈 수 없다.
    static bool IsHostile(WorldBand band)
    {
        var mine = Faction.Player != null ? Faction.Player.Data : null;
        if (mine == null || band.Faction == null) return false;

        // **우호도를 태운다**(`StanceBetween`) — `FactionData.IsHostileTo`를 직접 부르면
        // 저작한 표만 봐서, 포로를 풀어 주고 관계를 끌어올려도 지도에서는 그대로다.
        return FactionRelations.IsHostileBetween(mine, band.Faction)
            || FactionRelations.IsHostileBetween(band.Faction, mine);
    }

    // ── 무리에게 내리는 명령 (우클릭) ─────────────────────────────────────────
    //
    // **필드의 우클릭 창을 그대로 쓴다**(`PawnContextMenuUI`). 우클릭이 "무엇을 하러 가느냐"로
    // 갈리는 것은 두 화면에서 같은 일이고, 규약이 둘이면 지도에 나갈 때마다 조작을 새로 배워야 한다.
    //
    // **고르는 순간 정해지고, 가서 그대로 벌어진다.** 다다른 뒤에 조우 메뉴를 한 번 더 띄우면
    // 같은 결정을 두 번 하게 된다 — 필드에서 [공격한다]를 고른 폰이 도착해서 다시 묻지 않는 것과 같다.
    void OpenBandOrders(WorldBand band)
    {
        var menu = PawnContextMenuUI.Instance;
        if (menu == null) { OpenBandInfo(band); return; }   // 창이 없으면 정보라도 보여준다

        var options = new List<PawnMenuOption>();

        if (!IsHostile(band))
        {
            if (band.Shop != null && band.Shop.Data != null)
                options.Add(("거래한다", () => Order(band, BandIntent.Trade)));

            // 중립은 스쳐도 아무 일이 안 나므로(`Interrupts`), **말을 걸려면 작정하고 가야 한다.**
            // 적에게는 안 띄운다 — 맞닿기만 해도 창이 뜨니 같은 말이 두 줄이 된다.
            options.Add(("말을 건다", () => Order(band, BandIntent.Talk)));
        }

        options.Add(("공격한다",   () => Order(band, BandIntent.Attack)));
        options.Add(("따라간다",   () => Order(band, BandIntent.Follow)));
        // **[살펴본다]는 없다** — 좌클릭이 이미 정보 창이고, 우클릭은 "무엇을 하러 가느냐"의
        // 자리다. 보는 일까지 여기 두면 같은 것을 여는 길이 둘이 된다.

        // 필드가 늘 마지막에 [여기로 이동]을 두는 것과 같다 — 무리와 상관없이 그 자리로만
        // 가고 싶을 때가 있고, 그 길이 없으면 무리 근처에는 아무 명령도 못 내린다.
        options.Add(("이 자리로 이동", () =>
        {
            var spots = Formation(band.Position, _selected.Count);
            for (int i = 0; i < _selected.Count; i++)
            {
                _selected[i].MoveTarget  = spots[i];
                _selected[i].Moving      = true;
                _selected[i].Approaching = null;
                _selected[i].Intent      = BandIntent.None;
            }
        }));

        menu.Open(band.Summary, options, band.Position, worldCamera);
    }

    // 고른 팀 전부에게 같은 작정을 준다. **자리를 벌리지 않는다** — 맞닿는 것이 목적이라
    // 흩어져 서면 아무도 조우 거리에 못 든다.
    void Order(WorldBand band, BandIntent intent)
    {
        foreach (var sel in _selected)
        {
            sel.MoveTarget  = band.Position;
            sel.Moving      = true;
            sel.Approaching = band;
            sel.Intent      = intent;
        }
        CloseMenu();
    }

    // 무리를 **눌러서** 들여다본다. 조우 전에도 "저게 뭔가"를 알 수 있어야 다가갈지 피할지를
    // 정한다 — 처음엔 토큰에 콜라이더도 안 뒀는데, 그러면 지도 위의 무리가 눌러도 아무 반응이
    // 없는 그림이 된다.
    //
    // **여기서 할 수 있는 건 다가가는 것뿐이다.** 거래·대화는 맞닿아야 열린다(마을에 도착해야
    // 시장이 열리는 것과 같은 규약) — 멀리서 다 되면 지도 위를 오갈 이유가 없다.
    void OpenBandInfo(WorldBand band)
    {
        if (menuPanel == null) return;

        _menuParty = null;

        var options = new List<WorldMenuOption>();

        if (_selected.Count == 0)
            options.Add(WorldMenuOption.Blocked("다가간다", "고른 원정대가 없다"));
        else
            options.Add(WorldMenuOption.Ready("다가간다", () =>
            {
                // 다가가는 것은 곧 **말을 걸러 가는 것**이다 — 맞닿으면 조우 메뉴가 떠서
                // 그때 고른다. `None`으로 두면 중립은 멈춰 세우지 않아(`Interrupts`)
                // 옆까지 걸어가 놓고 아무 일도 안 일어난다.
                Order(band, BandIntent.Talk);
            }));

        options.Add(WorldMenuOption.Ready("떠난다", CloseMenu));

        var tile = WorldMap.Instance.GetTile(band.Node);
        grid.SetHighlight(band.Node);
        _bandMenuOpen = true;
        menuPanel.Open(band.Name, BandInfo(band), MenuArtFor(tile), band.Data.TokenColor, options);
    }

    string BandInfo(WorldBand band)
    {
        var sb = new System.Text.StringBuilder();

        sb.Append($"{band.Count}명 · ");
        sb.Append(IsHostile(band) ? "<color=#FF8080>적대</color>" : "중립");
        sb.Append($" · ({band.Node.x}, {band.Node.y})\n");

        sb.Append(band.Behavior switch
        {
            WorldBandBehavior.Roam    => band.Returning
                                       ? "<color=#FFFFFF80>근거지로 돌아가는 중이다</color>"
                                       : "<color=#FFFFFF80>이 근방을 떠돌고 있다</color>",
            WorldBandBehavior.Caravan => "<color=#FFFFFF80>마을 사이를 오간다</color>",
            _                         => "<color=#FFFFFF80>마을 둘레를 돈다</color>",
        });

        // **끌고 다니는 사람이 있으면 그게 가장 중요한 정보다** — 쫓아갈 이유가 그것이다.
        // 흥정이 되는지도 같이 적는다: 되면 돈을 챙겨 가고, 안 되면 병력을 챙겨 가야 한다.
        if (band.Captives.Count > 0)
        {
            sb.Append($"\n<color=#FFD060>포로 {band.Captives.Count}명을 끌고 간다</color>");
            sb.Append(CaptiveHint(band.Faction, band.Captives, SelectedHaggler()));
        }

        if (_selected.Count == 1)
        {
            float mine = WorldBattle.PowerOf(_selected[0].Party.Members);
            sb.Append(mine >= band.Power
                ? "\n<color=#FFFFFF80>고른 원정대가 더 세 보인다</color>"
                : "\n<color=#FFFFFF80>고른 원정대로는 버거워 보인다</color>");
        }

        return sb.ToString();
    }

    void OpenEncounterMenu(PartyToken pt, WorldBand band)
    {
        if (menuPanel == null) return;

        _menuParty = pt;

        var options = new List<WorldMenuOption>();
        bool hostile = IsHostile(band);

        if (hostile)
        {
            // 붙잡힌 사람이 있으면 **되찾는 얘기가 먼저다** — 싸우다 죽일 수도 있는 마당에
            // 흥정 줄이 아래에 묻혀 있으면 안 된다.
            if (band.Captives.Count > 0)
                options.Add(RansomOption(pt, band.Faction, band.Captives,
                                         () => FreeInto(band.Captives, pt)));

            options.Add(WorldMenuOption.Ready("싸운다",   () => Fight(pt, band)));
            options.Add(WorldMenuOption.Ready("도망친다", () => Flee(pt, band)));
            options.Add(WorldMenuOption.Ready("항복한다", () => Surrender(pt, band)));
        }
        else
        {
            if (band.Shop != null && band.Shop.Data != null)
                options.Add(WorldMenuOption.Ready("거래한다", () => TradeWithBand(pt, band)));
            else if (band.Behavior == WorldBandBehavior.Caravan)
                // 골드 설정이 없다고 적었던 자리 — `MerchantData.GoldItem`은 2026-08-10에
                // 없앴다(지갑이 전역이라). 이제 막히는 이유는 하나뿐이다: 이 상단의
                // `WorldBandData`에 파는 물건(`merchant`)이 안 물려 있다.
                options.Add(WorldMenuOption.Blocked("거래한다", "이 상단은 물건을 안 판다"));

            // **순찰대에게도 할 일이 있어야 한다.** 지나치는 것밖에 못 하면 지도 위의 중립
            // 무리는 장식이다. 이 지역을 도는 사람들이니 근처 소굴 사정을 안다 —
            // 정찰(`탐험한다`)과 **같은 결과**를 주므로 규칙이 하나 더 생기지는 않는다.
            if (band.Behavior == WorldBandBehavior.Patrol)
                options.Add(AskDirectionsOption(band));

            // **중립을 덮치는 길은 열어 둔다.** 못 하게 막으면 지도 위의 선택이 "적이면 싸운다"
            // 하나뿐이 된다. 대가는 `Fight`가 문다 — 그 세력 우호도가 떨어지고, 계속하면
            // 적대로 돌아서서 습격을 더 자주 보낸다.
            options.Add(WorldMenuOption.Ready("습격한다", () => Fight(pt, band)));
            options.Add(WorldMenuOption.Ready("지나친다", () => Leave(band)));
        }

        grid.SetHighlight(pt.Party.CurrentNode);
        _bandMenuOpen = true;
        menuPanel.Open(band.Name, EncounterInfo(pt, band), MenuArtFor(TileAt(pt)),
                       band.Data.TokenColor, options);
    }

    WorldTile TileAt(PartyToken pt) => WorldMap.Instance.GetTile(pt.Party.CurrentNode);

    // **전력을 숨기지 않는다.** 마운트앤블레이드도 상대 인원을 보여준다 — 붙을지 말지가
    // 선택이 되려면 재료가 있어야 하고, 없으면 매번 도망치는 것이 정답이 된다.
    string EncounterInfo(PartyToken pt, WorldBand band)
    {
        float mine   = WorldBattle.PowerOf(pt.Party.Members);
        float theirs = band.Power;

        string odds = theirs <= 0f      ? "상대가 되지 않는다"
                    : mine >= theirs * 1.5f ? "우리가 훨씬 우세하다"
                    : mine >= theirs        ? "우리가 조금 낫다"
                    : mine * 1.5f >= theirs ? "만만치 않다"
                    :                         "상대가 훨씬 강하다";

        var sb = new System.Text.StringBuilder();
        sb.Append($"{band.Name} · {band.Count}명\n");
        sb.Append($"우리 {pt.Party.Members.Count}명 · {odds}\n");
        sb.Append(band.Data.Speed >= partySpeed
            ? "<color=#FFFFFF80>우리보다 발이 빠르다 — 도망쳐도 따라잡힌다</color>"
            : "<color=#FFFFFF80>우리보다 느리다 — 뿌리칠 수 있다</color>");
        return sb.ToString();
    }

    // 순찰대가 아는 것 — **가장 가까운, 아직 안 살펴본 소굴**. 하나를 조사한 것으로 표시한다.
    // 여러 개를 한꺼번에 열어 주지 않는다: 한 번 물어 지도가 통째로 밝아지면 발품을 팔 이유가
    // 사라지고, 순찰대를 만날 때마다 하나씩 열려야 다시 만날 이유도 생긴다.
    WorldMenuOption AskDirectionsOption(WorldBand band)
    {
        const string Label = "이 근방을 묻는다";

        var wm = WorldMap.Instance;
        Vector2Int found = default;
        int bestDist = int.MaxValue;

        for (int x = 0; x < wm.Width;  x++)
        for (int y = 0; y < wm.Height; y++)
        {
            var n = new Vector2Int(x, y);
            if (wm.GetTile(n).Content != WorldNodeContent.EnemyLair) continue;
            if (wm.IsSurveyed(n)) continue;

            int d = WorldMap.Distance(band.Node, n);
            if (d >= bestDist) continue;
            bestDist = d;
            found    = n;
        }

        if (bestDist == int.MaxValue)
            return WorldMenuOption.Blocked(Label, "이 사람들이 아는 소굴은 다 살펴봤다");

        var node = found;
        return WorldMenuOption.Ready(Label, () =>
        {
            wm.MarkSurveyed(node);
            CloseMenu();

            var lair = WorldLairs.At(node);
            EventNotificationUI.Notify("소문",
                lair != null
                    ? $"({node.x}, {node.y})에 {lair.Name}이(가) 있다고 합니다 — {lair.ThreatLabel}."
                    : $"({node.x}, {node.y})의 소굴 이야기를 들었습니다.");
        });
    }

    void Fight(PartyToken pt, WorldBand band)
    {
        // **먼저 덮친 것에는 값이 든다.** 중립을 치는 길을 열어 둔 자리에 오래 비어 있던
        // 대가다 — 없는 동안 `습격한다`는 위험만 있고 잃을 것이 없는 선택이었다.
        //
        // **판정보다 먼저 묻는다** — 싸움이 끝난 뒤엔 이 무리가 원래 적이었는지 알 길이
        // 없다(우호도가 −50을 넘어가면 그 세력이 통째로 적대로 바뀐다). 적대를 치는 것은
        // 사건이 아니라 전쟁이라 안 센다(`UnitBrain.Die`가 쓰는 규칙 그대로).
        //
        // 지도 위 전투는 폰을 죽이는 게 아니라 피해를 나눠 주는 것이라 `Die`의 −12가 안
        // 걸린다. 그래서 여기 한 번이 **먼저 친 값의 전부**다.
        if (!IsHostile(band) && band.Faction != null)
            FactionRelations.Add(band.Faction, FactionRelations.RaidPenalty, "무리를 습격함");

        var result = WorldBattle.Resolve(pt.Party, band);
        var mgr    = WorldBandManager.Instance;

        // **순서가 셋 다 중요하다.**
        //  ① 이겼으면 포로부터 넘겨받는다 — 무리를 치운 뒤면 `Remove`가 "탈출"로 먼저
        //     풀어 버려서, 구해 놓고도 따로 노는 팀이 하나 생긴다.
        //  ② 결과창은 붙잡히기 전에 띄운다 — 띄우는 데 `pt`가 필요하다.
        //  ③ 무리를 치우는 것은 맨 마지막 — 포로를 다 옮긴 뒤라야 안전하다.
        if (result.PlayerWon) FreeInto(band.Captives, pt);

        mgr?.PushApart(band);
        ShowBattleResult(pt, result);

        if (!result.PlayerWon) Capture(pt, band.Captives, band.Name);
        mgr?.RemoveIfDead(band);
    }

    // 이긴 자리에서 풀려난 사람들은 **그 원정대에 합류한다.** 새 팀으로 세우면 같은 자리에
    // 토큰이 둘 겹쳐서, 구해 놓고도 따로 데리고 다녀야 한다.
    void FreeInto(List<UnitBrain> captives, PartyToken pt)
    {
        if (captives == null || captives.Count == 0) return;

        int n = 0;
        foreach (var m in captives)
        {
            if (m == null) continue;
            pt.Party.Members.Add(m);
            n++;
        }
        captives.Clear();

        if (n > 0) EventNotificationUI.Notify("구출", $"붙잡혀 있던 {n}명이 합류했습니다.");
    }

    // **진 원정대는 포로가 되어 끌려간다.**
    //
    // 전에는 가장 가까운 정착지로 밀어냈다. 그건 "졌지만 아무 일도 없었다"에 가까워서, 지는
    // 것이 사건이 되지 못했다. 붙잡히면 지도에서 사라지고 **되찾아야 한다** — 그 무리를 이겨
    // 구출하거나, 소굴로 끌려간 뒤엔 소굴을 치거나, 저희끼리 도망쳐 나오기를 기다리거나.
    //
    // 폰은 지우지 않는다. 꺼진 채로 무리가 들고 다니므로 이름·스탯·특성이 그대로 살아 있고,
    // 풀려나면 그 사람 그대로 돌아온다.
    // **붙잡는 쪽을 인자로 받는다.** 길이 둘이다 — 길에서 무리에게 지거나, 소굴을 치다 지거나.
    // 무리에게 지면 그 무리가 데려가고(근거지에 닿으면 소굴로 넘어간다), **소굴 앞에서 지면
    // 소굴이 바로 가둔다.**
    //
    // 전에는 소굴전도 `Capture(pt, band)`를 불렀는데, 그 `band`는 `Assault`가 굴려 만든
    // **로컬 개체라 `WorldBandManager`에 등록되지 않는다.** 거기 넣은 사람은 `RollEscapes`도
    // `Remove`도 `FindContact`도 못 보고, 그 함수가 끝나는 순간 함께 사라졌다 —
    // **되찾는 길 다섯이 전부 막힌 채 원정대가 통째로 증발했다.**
    void Capture(PartyToken pt, List<UnitBrain> into, string captorName)
    {
        foreach (var m in pt.Party.Members)
            if (m != null) into.Add(m);

        pt.Party.Members.Clear();

        // 방금 띄운 전투 결과창을 지키려고 먼저 끊는다 — `RemoveParty`는 그 팀의 창을 닫는데,
        // 무슨 일이 있었는지 못 읽고 팀만 사라지면 영문을 알 수 없다.
        _menuParty = null;
        RemoveParty(pt);

        EventNotificationUI.Notify("포로",
            $"원정대가 {captorName}에게 붙잡혀 끌려갔습니다.", holdWhenHidden: true);
    }

    // 풀려난 사람들을 **새 원정대로** 지도에 세운다. 탈출·구출·소굴 함락이 전부 여기로 모인다 —
    // 되돌아오는 길이 셋인데 처리가 셋이면 반드시 하나가 어긋난다.
    //
    // **등짐은 없다.** 붙잡힐 때 통째로 빼앗겼고, 풀려나며 되찾는 것은 사람뿐이다.
    public void RestoreParty(List<UnitBrain> members, Vector3 at, string title, string message)
    {
        if (members == null || grid == null) return;

        var alive = new List<UnitBrain>();
        foreach (var m in members) if (m != null) alive.Add(m);
        if (alive.Count == 0) return;

        var party = new ExpeditionParty(grid.TryNodeAt(at, out var node) ? node : StartNode());
        foreach (var m in alive)
        {
            party.Members.Add(m);
            m.gameObject.SetActive(false);   // 지도 위에 있는 동안은 필드에서 빠져 있다
        }

        var pt = SpawnToken(party);
        pt.Go.transform.position = GroundedTokenPos(new Vector3(at.x, 0f, at.z), pt);

        if (!string.IsNullOrEmpty(title))
            EventNotificationUI.Notify(title, message, holdWhenHidden: true);
    }

    // **도망은 발이 정한다.** 우리가 빠르면 뿌리치고, 느리면 붙잡혀 그대로 싸운다 —
    // 무조건 도망칠 수 있으면 조우가 아무 일도 아니게 되고, 무조건 못 도망치면 선택지가 아니다.
    void Flee(PartyToken pt, WorldBand band)
    {
        float mine = partySpeed;
        float them = band.Data.Speed;

        // 같은 속도 근처에서는 굴린다 — 딱 잘라 갈리면 무리 하나를 만나본 뒤로는
        // 결과가 뻔해서 고를 이유가 없어진다.
        bool escaped = Random.value < Mathf.Clamp01(0.15f + (mine - them) * 0.35f);

        if (escaped)
        {
            WorldBandManager.Instance?.PushApart(band);
            CloseMenu();
            EventNotificationUI.Notify("도주", $"{band.Name}을(를) 뿌리쳤습니다.");
            return;
        }

        // 잡혔다 — 등을 보인 채 붙었으므로 그대로 전투로 넘어간다.
        EventNotificationUI.Notify("추격", $"{band.Name}에게 따라잡혔습니다.");
        Fight(pt, band);
    }

    // 포로 옆에 붙는 한 줄 — **흥정이 되는가, 얼마인가.** 값을 미리 알아야 돈을 챙겨 갈지
    // 병력을 챙겨 갈지 정할 수 있다. 안 되는 상대라면 값은 적어 봐야 소용이 없다.
    static string CaptiveHint(FactionData faction, List<UnitBrain> captives, UnitBrain haggler)
    {
        if (faction != null && !faction.AcceptsRansom)
            return "\n<color=#FFFFFF80>몸값을 받지 않는 자들이다 — 쳐들어가는 수밖에 없다</color>";

        return $"\n<color=#FFFFFF80>몸값 {WorldRansom.PriceFor(captives, haggler)} G</color>";
    }

    // **지도 위에서 흥정하는 사람 — 그 원정대에서 제일 말 잘하는 사람.**
    //
    // 거점에 남은 폰은 안 센다(거기 없다). 답을 여기 하나로 두는 것은 **미리 보는 값과
    // 실제로 치르는 값이 같아야** 하기 때문이다 — 안내문과 선택지가 각자 구하면 어긋난다.
    static UnitBrain HagglerOf(PartyToken pt) => Trading.BestOf(pt?.Party?.Members);

    // 무리 정보창처럼 원정대를 안 받는 자리용 — **고른 팀 중 첫째가 간다고 본다.**
    //
    // 전엔 정확히 하나일 때만 답했는데, 그러면 둘을 고르거나 아무것도 안 골랐을 때 안내문이
    // **흥정 안 붙은 값**을 적고 실제 메뉴는 깎인 값을 적어 같은 포로가 두 값으로 보였다.
    // 여럿일 때 어느 팀이 갈지는 알 수 없지만, 첫째로 답하는 편이 "둘 다 틀림"보다 낫다.
    UnitBrain SelectedHaggler() => _selected.Count > 0 ? HagglerOf(_selected[0]) : null;

    // ── 몸값 ─────────────────────────────────────────────────────────────────
    //
    // 쳐들어가 구하는 것과 **나란한 길**이다 — 어느 쪽이 싼가가 판단거리가 된다.
    // 다만 아무와나 흥정이 되지는 않는다: 세력이 그걸 정한다(`FactionData.AcceptsRansom`).
    //
    // `release`를 밖에서 받는 이유: 풀려나는 자리가 무리일 수도 소굴일 수도 있는데,
    // 여기서 그걸 갈라 물으면 되찾는 길이 늘 때마다 이 함수를 고쳐야 한다.
    WorldMenuOption RansomOption(PartyToken pt, FactionData faction,
                                 List<UnitBrain> captives, System.Action release)
    {
        var haggler = HagglerOf(pt);
        int price   = WorldRansom.PriceFor(captives, haggler);
        string label = $"몸값을 치른다 — {price} G ({captives.Count}명)";

        if (faction != null && !faction.AcceptsRansom)
            return WorldMenuOption.Blocked(label, "몸값을 받지 않는 자들이다");

        int have = WorldRansom.Available();
        if (have < price)
            return WorldMenuOption.Blocked(label, $"골드 부족 ({have}/{price})");

        return WorldMenuOption.Ready(label, () =>
        {
            // **먼저 값을 치르고 놓아준다.** 순서가 뒤집히면 값을 못 치렀을 때 이미 풀린
            // 사람을 도로 넣어야 하고, 그건 되돌릴 곳을 모르는 상태다(`TradeAccount`와 같은 규칙).
            if (!WorldRansom.TryPay(price))
            {
                EventNotificationUI.Notify("몸값", "값을 치르지 못했습니다.");
                return;
            }
            release?.Invoke();
            CloseMenu();
        });
    }

    // 항복 — 짐을 내주고 풀려난다. **유예를 길게 준다**: 얻을 것을 다 얻은 무리가
    // 몇 초 뒤에 또 붙어 오면 항복이 아무것도 해결하지 못한 것이 된다.
    const float SurrenderGrace = 90f;

    void Surrender(PartyToken pt, WorldBand band)
    {
        var result = WorldBattle.Surrender(pt.Party, band);
        WorldBandManager.Instance?.PushApart(band, SurrenderGrace);
        ShowBattleResult(pt, result, "항복");
    }

    void Leave(WorldBand band)
    {
        WorldBandManager.Instance?.PushApart(band);
        CloseMenu();
    }

    // 길 위의 거래 — 마을 시장과 **같은 창**을 쓴다. 지갑도 창고도 원정대 배낭이다.
    void TradeWithBand(PartyToken pt, WorldBand band)
    {
        CloseMenu();   // 상점 창이 메뉴를 가린다

        var members = pt.Party.Members;
        MerchantUI.OpenFor(band.Shop,
                           new PartyAccount(members),
                           members,
                           $"{band.Name} · 길 위의 거래");

        WorldBandManager.Instance?.PushApart(band);
    }

    // 싸운 뒤 — 결과를 그 자리에서 읽히게 한다. 알림으로만 띄우면 사라진 뒤에는
    // 누가 다쳤는지 확인할 길이 없다.
    void ShowBattleResult(PartyToken pt, WorldBattleResult result, string title = null)
    {
        if (menuPanel == null) return;

        var options = new List<WorldMenuOption>
        {
            WorldMenuOption.Ready("계속한다", CloseMenu),
        };

        var tile = TileAt(pt);
        _bandMenuOpen = true;
        // 항복은 진 것이 아니라 **안 싸운 것**이라 제목이 달라야 한다 — "패배"라고 쓰면
        // 사람이 다쳤을 거라고 읽힌다.
        menuPanel.Open(title ?? (result.PlayerWon ? "승리" : "패배"), result.Summary,
                       MenuArtFor(tile), tile.Biome.Color(), options);
    }

    // ── 도착 메뉴 ─────────────────────────────────────────────────────────────

    // **선택지의 내용과 가능 여부를 정하는 건 여기다** — 메뉴는 받은 목록을 그리기만 한다.
    // **모든 칸에서 열린다.** 빈 야생이라도 조사하고 자리를 잡을 수 있어야 하므로 할 일이 있다.
    //
    // `pt`가 null이면 **원정대 없이 들여다보는 것**(내 정착지·채집기지를 클릭한 경우).
    // 행동은 전부 막히고 정보만 보인다 — 기지에 얼마나 쌓였는지 보러 매번 팀을 보낼 순 없다.
    void OpenMenu(PartyToken pt, Vector2Int node)
    {
        if (menuPanel == null) return;

        _menuParty  = pt;
        var wm      = WorldMap.Instance;
        var tile    = wm.GetTile(node);
        var options = new List<WorldMenuOption>();

        switch (tile.Content)
        {
            case WorldNodeContent.MySettlement:
                if (pt == null)
                    options.Add(WorldMenuOption.Blocked("거점으로 들어간다", "여기 온 원정대가 없다"));
                else
                    options.Add(WorldMenuOption.Ready("거점으로 들어간다",
                        () => { var p = _menuParty; CloseMenu(); EnterSettlement(p, node); }));
                break;

            case WorldNodeContent.Town:
                options.Add(VillageOption(pt, node));
                options.Add(MarketOption(pt, node));
                break;

            case WorldNodeContent.EnemyLair:
            {
                var lair = WorldLairs.At(node);
                if (pt != null && lair != null && lair.Captives.Count > 0)
                    options.Add(RansomOption(pt, lair.Band != null ? lair.Band.Faction : null,
                                             lair.Captives, () => FreeInto(lair.Captives, pt)));
                options.Add(ScoutOption(pt, node));
                options.Add(AssaultOption(pt, node));
                break;
            }

            case WorldNodeContent.Outpost:
                options.Add(LoadOption(pt, node));
                break;
        }

        // 조사하고 자리를 잡는 건 **임자 없는 야생에서만**.
        // 예전엔 "내 것이 아니면 전부"였는데, 그러면 남의 마을과 적 소굴에서도
        // `탐험한다` · `여기에 채집기지를 세운다`가 떴다 — 남의 땅에 기지를 박는 얘기가 된다.
        // 늘어나는 건 `WorldNodeContent`뿐이므로 **넣을 곳을 나열**한다(빼는 목록이 아니라).
        if (IsWildland(tile.Content))
        {
            options.Add(SurveyOption(pt, node));
            options.Add(BuildEntryOption(pt, node));
        }

        // M&B의 메뉴는 항상 "떠난다"로 빠져나온다 — 닫는 길이 메뉴 안에 있어야 한다.
        options.Add(WorldMenuOption.Ready("떠난다", CloseMenu));

        grid.SetHighlight(node);
        menuPanel.Open(
            TitleFor(tile),
            InfoFor(pt, node, tile),
            MenuArtFor(tile),
            tile.Biome.Color(),
            options);
    }

    // 임자 없는 땅 = 조사·기지 건설이 가능한 칸.
    // 마을(Town)·소굴(EnemyLair)은 남의 땅이고, 내 정착지·채집기지는 이미 자리를 잡은 곳이다.
    static bool IsWildland(WorldNodeContent c) => c == WorldNodeContent.Empty;

    static string TitleFor(WorldTile tile) =>
        tile.Content == WorldNodeContent.Empty
            ? tile.Biome.DisplayName()                    // 빈 야생은 "빈 지역"보다 바이옴 이름이 낫다
            : tile.Content.DisplayName();

    // 지역 정보 + **매장 자원**. 조사 전이면 무엇이 묻혔는지 감춘다.
    string InfoFor(PartyToken pt, Vector2Int node, WorldTile tile)
    {
        var wm = WorldMap.Instance;
        var sb = new System.Text.StringBuilder();

        // **위험도를 먼저 적는다**(2026-08-23). 오픈월드라 갈 수는 있지만 대개 죽는데,
        // 말해 주지 않으면 그건 선택이 아니라 사고다 — *"모르게 굴리지 않는다"*가 걸리는 자리.
        int tier = BiomeTier.Of(tile.Biome);
        sb.Append($"{tile.Biome.DisplayName()} · ({node.x}, {node.y}) · ");
        sb.Append($"<color={BiomeTier.Color(tier)}>{BiomeTier.Label(tier)}</color> · ");
        // 원정대 없이 들여다보는 중이면 인원·등짐 대신 그 사실을 적는다.
        sb.Append(pt == null
            ? "<color=#FFFFFF80>원정대 없음</color>"
            : $"{pt.Party.Members.Count}명 · 등짐 {pt.Party.CarriedWeight:0}/{pt.Party.Capacity:0}kg");

        if (tile.Content == WorldNodeContent.MySettlement) return sb.ToString();

        // 채집기지는 "얼마나 쌓였고 얼마나 남았나"가 핵심 정보다.
        if (tile.Content == WorldNodeContent.Outpost)
        {
            var outpost = OutpostManager.Instance?.At(node);
            if (outpost == null) return sb.ToString();

            sb.Append('\n');
            if (outpost.Stock.Count == 0) sb.Append("<color=#FFFFFF80>비축 없음</color>");
            else
            {
                sb.Append("비축 — ");
                bool first = true;
                foreach (var kv in outpost.Stock)
                {
                    if (!first) sb.Append(" · ");
                    sb.Append($"{OutpostManager.ItemName(kv.Key)} {kv.Value}");
                    first = false;
                }
            }
            sb.Append(outpost.IsDepleted
                ? "\n<color=#FFFFFF80>매장 고갈</color>"
                : $"\n<color=#FFFFFF80>남은 매장 {outpost.RemainingCount}</color>");
            return sb.ToString();
        }

        // 소굴은 매장이 아니라 **누가 얼마나 있는가**가 볼 것이다. 정찰 전에는 감춘다 —
        // 그걸 알아내는 것이 곧 정찰이라 미리 보여주면 갈 이유가 없어진다.
        if (tile.Content == WorldNodeContent.EnemyLair)
        {
            var lair = WorldLairs.At(node);
            sb.Append('\n');

            if (lair == null)               sb.Append("<color=#FFFFFF80>비어 있다</color>");
            else if (!wm.IsSurveyed(node))  sb.Append($"{lair.Name}\n<color=#FFFFFF80>얼마나 있는지는 가봐야 안다</color>");
            else                            sb.Append($"{lair.Name}\n병력 {lair.Strength}명 — {lair.ThreatLabel}");

            // **예고된 습격은 조사와 무관하게 보인다.** 습격은 저쪽이 알려온 것이지 우리가
            // 알아낸 것이 아니다. 그리고 알림은 사라지므로 **여기 계속 남아 있어야** 대비가
            // 된다 — "며칠 뒤 온다"를 다시 확인할 데가 없으면 예고가 아니라 한 번의 소음이다.
            // 갇혀 있는 사람은 **정찰 여부와 무관하게** 보인다. 우리 사람이 어디 있는지는
            // 끌려가는 것을 봐서 아는 것이지 소굴을 뒤져서 아는 것이 아니다.
            if (lair != null && lair.Captives.Count > 0)
            {
                sb.Append($"\n<color=#FFD060>우리 쪽 {lair.Captives.Count}명이 갇혀 있다</color>");
                sb.Append(CaptiveHint(lair.Band != null ? lair.Band.Faction : null, lair.Captives,
                                      HagglerOf(pt)));
            }

            var mgr = WorldBandManager.Instance;
            if (mgr != null && mgr.TryGetRaidFrom(node, out int raidCount, out float left))
            {
                int days = Mathf.Max(1, Mathf.CeilToInt(left / GameTimeManager.DayDuration));
                sb.Append($"\n<color=#FF8080>{raidCount}명이 {days}일 뒤 습격해 온다</color>");
            }

            return sb.ToString();
        }

        sb.Append('\n');
        if (!wm.IsSurveyed(node))
        {
            sb.Append("<color=#FFFFFF80>매장 자원 — 미조사</color>");
            return sb.ToString();
        }

        var deposits = wm.DepositsAt(node);
        if (deposits.Count == 0)
        {
            sb.Append("<color=#FFFFFF80>묻혀 있는 자원 없음</color>");
            return sb.ToString();
        }

        for (int i = 0; i < deposits.Count; i++)
        {
            sb.Append(i == 0 ? "매장 — " : " · ");
            sb.Append($"{deposits[i].DisplayName} {deposits[i].Amount}");
        }
        return sb.ToString();
    }

    // ── 마을 둘러보기 ─────────────────────────────────────────────────────────

    const string VillageLabel = "마을을 둘러본다";

    WorldMenuOption VillageOption(PartyToken pt, Vector2Int node)
    {
        if (pt == null || pt.Party.Members.Count == 0)
            return WorldMenuOption.Blocked(VillageLabel, "여기 온 원정대가 없다");

        return WorldMenuOption.Ready(VillageLabel, () => OpenVillageMenu(pt, node));
    }

    // 마을 안 — **메뉴 안의 메뉴**(채집기지 건설과 같은 꼴). 지금 여기서 하는 일은 인력 영입이고,
    // 나중에 소문·의뢰가 붙어도 이 자리에 줄이 늘 뿐이다.
    //
    // 왜 시장은 여기 안 넣었나: 시장은 **밖에서 바로 가는 곳**이다(M&B도 마을 메뉴에서
    // "시장에 간다"가 나란히 있다). 한 단계 더 들어가게 하면 자주 하는 일이 더 멀어진다.
    void OpenVillageMenu(PartyToken pt, Vector2Int node)
    {
        if (menuPanel == null) return;

        _menuParty = pt;

        var wm      = WorldMap.Instance;
        var tile    = wm.GetTile(node);
        var party   = pt.Party;
        var account = new PartyAccount(party.Members);
        var options = new List<WorldMenuOption>();

        var recruits = TownRecruits.At(node);
        int standing = 0;

        foreach (var r in recruits)
        {
            if (r.Hired) continue;
            standing++;

            // **부르는 값에 흥정이 얹힌다** — 사람을 사는 것도 값을 다투는 일이다.
            // 목록에 적는 값과 `Hire`가 빼는 값이 **같은 함수**에서 나와야 갈리지 않는다.
            int cost = RecruitCost(pt, r);

            string label = $"{r.Name}을(를) 고용한다 — {r.Summary} · {cost:N0}G";

            if (account.GoldAmount < cost)
                options.Add(WorldMenuOption.Blocked(label, $"골드 부족 ({account.GoldAmount}/{cost})"));
            else
            {
                var captured = r;
                options.Add(WorldMenuOption.Ready(label, () => Hire(pt, node, captured, account)));
            }
        }

        if (standing == 0)
            options.Add(WorldMenuOption.Blocked("사람을 구한다", "지금은 나설 사람이 없다"));

        options.Add(WorldMenuOption.Ready("마을을 나선다", () => OpenMenu(pt, node)));

        var sb = new System.Text.StringBuilder();
        sb.Append($"{tile.Content.DisplayName()} · {tile.Biome.DisplayName()} · ({node.x}, {node.y})\n");
        // 지갑이 하나뿐이라 "이 마을엔 오가는 돈이 없다"가 성립하지 않는다.
        sb.Append($"골드 — {account.GoldAmount} G\n");
        sb.Append(standing > 0
            ? $"<color=#FFFFFF80>나설 사람 {standing}명 — 하루가 지나면 바뀐다</color>"
            : "<color=#FFFFFF80>오늘은 나설 사람이 없다 — 하루가 지나면 바뀐다</color>");

        grid.SetHighlight(node);
        menuPanel.Open(VillageLabel, sb.ToString(), MenuArtFor(tile), tile.Biome.Color(), options);
    }

    // 고용 — **돈을 먼저 뺀다.** 폰을 먼저 찍으면 값을 못 치렀을 때 지울 것이 생기고,
    // 그 사이에 다른 것이 그 폰을 집을 수 있다(`OnUnitSpawned`은 활성화 때 도는데,
    // 여기선 꺼진 채로 만들므로 아직 아무도 모른다 — 그래도 순서를 헷갈리게 둘 이유는 없다).
    // 실제로 치를 값 — **부르는 값(`recruit.Cost`)에 흥정이 얹힌 것.** 목록을 그릴 때와
    // 값을 뺄 때가 이 함수 하나를 부르므로 갈릴 데가 없다.
    static int RecruitCost(PartyToken pt, TownRecruit r)
        => r == null ? 0 : Trading.Haggle(r.Cost, HagglerOf(pt), buying: true);

    void Hire(PartyToken pt, Vector2Int node, TownRecruit recruit, PartyAccount account)
    {
        if (recruit == null || recruit.Prefab == null || account == null) return;

        int cost = RecruitCost(pt, recruit);

        if (account.TakeGold(cost) < cost)
        {
            EventNotificationUI.Notify("고용 실패", "원정대에 골드가 모자랍니다.");
            OpenVillageMenu(pt, node);
            return;
        }

        // **꺼진 채로 만든다.** 원정대 멤버는 필드맵에서 빠져 있는 상태가 정상이고
        // (`WorldMapView.AddMembers`가 그렇게 한다), 거점에 들어갈 때 `EnterSettlement`가
        // 자리를 잡아 켠다 — 새로 온 사람도 같은 길을 타야 한 곳에서만 살아난다.
        var go = Instantiate(recruit.Prefab);
        go.name = recruit.Prefab.name;
        go.SetActive(false);

        var brain = go.GetComponent<UnitBrain>();
        if (brain == null)
        {
            Debug.LogError($"[마을] 고용한 프리팹에 UnitBrain이 없습니다 — {recruit.Prefab.name}");
            Destroy(go);
            account.GiveGold(cost);   // 값을 돌려준다
            return;
        }

        // **메뉴에서 본 그 사람**을 입힌다 — 이름·스탯·몸집이 명세에 다 적혀 있다.
        // 안 입히면 `Awake`가 굴린 딴 사람이 합류한다.
        brain.ApplySpec(recruit.Spec);
        pt.Party.Members.Add(brain);
        recruit.Hired = true;

        EventNotificationUI.Notify("합류",
            $"{recruit.Name}이(가) 원정대에 합류했습니다. ({cost:N0}G)");

        OpenVillageMenu(pt, node);   // 목록을 그 자리에서 갱신
    }

    // ── 마을 시장 ─────────────────────────────────────────────────────────────

    const string MarketLabel = "시장에 간다";

    WorldMenuOption MarketOption(PartyToken pt, Vector2Int node)
    {
        if (pt == null || pt.Party.Members.Count == 0)
            return WorldMenuOption.Blocked(MarketLabel, "여기 온 원정대가 없다");

        if (TownMarket.ShopsAt(node).Count == 0)
            return WorldMenuOption.Blocked(MarketLabel, "이 마을엔 시장이 없다");

        return WorldMenuOption.Ready(MarketLabel, () => OpenMarketMenu(pt, node));
    }

    // 시장 안 — **상인마다 한 줄**(재료·식료·무기·방어구). 마을 둘러보기와 같은 꼴이다.
    //
    // 전엔 마을에 상인이 하나뿐이라 [시장에 간다]가 곧 그 상인이었다. 종류를 나누면
    // "이 마을엔 무기 상인이 있나"가 지도를 도는 이유가 되고, 어느 마을에서 무엇을 파는지가
    // 기억할 만한 정보가 된다.
    void OpenMarketMenu(PartyToken pt, Vector2Int node)
    {
        if (menuPanel == null) return;

        _menuParty = pt;

        var wm      = WorldMap.Instance;
        var tile    = wm.GetTile(node);
        var party   = pt.Party;
        var shops   = TownMarket.ShopsAt(node);
        var account = new PartyAccount(party.Members);
        var options = new List<WorldMenuOption>();

        foreach (var shop in shops)
        {
            string label = $"{shop.DisplayName}에게 간다";

            if (shop.Data == null) continue;

            var captured = shop;
            options.Add(WorldMenuOption.Ready(label, () =>
            {
                CloseMenu();   // 상점 창이 메뉴를 가린다 — 둘을 겹쳐 두면 뒤가 안 보인다

                MerchantUI.OpenFor(
                    captured,
                    new PartyAccount(party.Members),
                    party.Members,
                    $"{captured.DisplayName} · ({node.x}, {node.y})");
            }));
        }

        options.Add(WorldMenuOption.Ready("시장을 나선다", () => OpenMenu(pt, node)));

        var sb = new System.Text.StringBuilder();
        sb.Append($"{tile.Content.DisplayName()} 시장 · {tile.Biome.DisplayName()} · ({node.x}, {node.y})\n");
        // 지갑이 하나뿐이라 "이 마을엔 오가는 돈이 없다"가 성립하지 않는다.
        sb.Append($"골드 — {account.GoldAmount} G\n");
        sb.Append($"<color=#FFFFFF80>상인 {shops.Count}명 — 진열은 하루가 지나면 바뀐다</color>");

        grid.SetHighlight(node);
        menuPanel.Open(MarketLabel, sb.ToString(), MenuArtFor(tile), tile.Biome.Color(), options);
    }

    // ── 적 소굴 ───────────────────────────────────────────────────────────────
    //
    // **정찰과 습격을 나눈 이유**: 소굴은 "보이지만 못 감"의 자리다(안개 대신 위험도로 거는
    // 게이팅). 얼마나 있는지 모르는 채로 뛰어들면 판단이 아니라 도박이 되고, 처음부터 다
    // 보이면 소굴 앞까지 가 볼 이유가 없다. 그래서 **가야 알 수 있게** 하고, 알고 나서
    // 붙을지 고르게 한다 — 매장 자원의 `탐험한다`와 같은 규칙이다.

    WorldMenuOption ScoutOption(PartyToken pt, Vector2Int node)
    {
        var lair = WorldLairs.At(node);
        if (lair == null)                return WorldMenuOption.Blocked("소굴을 정찰한다", "빈 소굴이다");
        if (pt == null || pt.Party.Members.Count == 0)
            return WorldMenuOption.Blocked("소굴을 정찰한다", "여기 온 원정대가 없다");
        if (WorldMap.Instance.IsSurveyed(node))
            return WorldMenuOption.Blocked("소굴을 정찰한다", "이미 살펴봤다");

        return WorldMenuOption.Ready("소굴을 정찰한다", () =>
        {
            WorldMap.Instance.MarkSurveyed(node);
            OpenMenu(pt, node);   // 알아낸 것을 그 자리에서 보여준다
        });
    }

    WorldMenuOption AssaultOption(PartyToken pt, Vector2Int node)
    {
        var lair = WorldLairs.At(node);
        if (lair == null) return WorldMenuOption.Blocked("습격한다", "빈 소굴이다");
        if (pt == null || pt.Party.Members.Count == 0)
            return WorldMenuOption.Blocked("습격한다", "여기 온 원정대가 없다");

        return WorldMenuOption.Ready("습격한다", () => Assault(pt, lair));
    }

    // 소굴 습격 — **길 위의 조우와 같은 판정**을 쓴다. 규칙이 둘이면 "밖에서 잡는 게 나은가
    // 소굴을 치는 게 나은가"가 계산이 아니라 눈치가 된다.
    //
    // 소굴이 내놓는 것은 그 소굴에 사는 무리와 같지만, **남은 병력만큼** 한 번에 나온다 —
    // 지도에서 만나는 한 무리보다 크고, 그래서 소굴은 마지막에 치는 곳이 된다.
    void Assault(PartyToken pt, WorldLair lair)
    {
        var band = WorldBand.Roll(lair.Band, lair.Node, unchecked(lair.Node.x * 7919 + lair.Node.y * 104729
                                                               + GameTimeManager.CurrentDay));
        if (band == null)
        {
            WorldLairs.Clear(lair);   // 세울 무리가 없는 소굴은 빈 소굴이다
            CloseMenu();
            return;
        }

        // 남은 병력이 정원보다 적으면 그만큼만 나오고, 많으면 여러 겹으로 채운다 —
        // 병력 30인 소굴이 다섯만 내보내면 정찰로 본 숫자가 거짓말이 된다.
        // 겹쳐 채우는 데 상한을 둔다. 저작 실수로 병력이 수백인 소굴이 생기면 명세를 그만큼
        // 굴리느라 한 프레임이 멈춘다(한 사람당 스탯 51개를 굴린다).
        const int MaxAssaultRolls = 40;
        for (int guard = 0; guard < MaxAssaultRolls && band.Count < lair.Strength; guard++)
        {
            var more = WorldBand.Roll(lair.Band, lair.Node,
                                      unchecked(band.Count * 31 + lair.Node.x * 6151));
            if (more == null) break;
            band.Members.AddRange(more.Members);
        }
        if (band.Count > lair.Strength) band.TakeLosses(band.Count - lair.Strength);

        var result = WorldBattle.Resolve(pt.Party, band);

        // **소굴의 병력이 실제로 준다.** 결과가 상태로 남지 않으면 습격은 알림일 뿐이고,
        // 몇 번을 쳐도 다음 날 같은 무리가 또 나온다.
        lair.Strength = band.Count;

        ShowBattleResult(pt, result);

        if (!result.PlayerWon)
        {
            // **소굴이 가둔다** — 위의 `band`는 등록되지 않은 로컬 개체라 거기 넣으면 사라진다.
            Capture(pt, lair.Captives, lair.Name);
            return;
        }

        // **이겼으면 갇혀 있던 사람들이 풀려난다.** 소굴이 아직 안 무너졌어도 그렇다 —
        // 문을 열고 들어간 마당에 사람을 두고 나올 이유가 없다.
        FreeInto(lair.Captives, pt);
        if (lair.IsCleared) WorldLairs.Clear(lair);
    }

    // ── 탐험(조사) ────────────────────────────────────────────────────────────

    // 조사해야 그 칸에 무엇이 얼마나 묻혔는지 드러난다. 안개는 없지만 **세부는 가봐야 안다**.
    WorldMenuOption SurveyOption(PartyToken pt, Vector2Int node)
    {
        if (WorldMap.Instance.IsSurveyed(node))
            return WorldMenuOption.Blocked("탐험한다", "이미 조사함");

        if (pt == null || pt.Party.Members.Count == 0)
            return WorldMenuOption.Blocked("탐험한다", "동행 없음");

        return WorldMenuOption.Ready("탐험한다", () =>
        {
            WorldMap.Instance.MarkSurveyed(node);

            var found = WorldMap.Instance.DepositsAt(node);
            Debug.Log(found.Count == 0
                ? $"[WorldMap] ({node.x},{node.y}) 조사 완료 — 쓸 만한 매장이 없다."
                : $"[WorldMap] ({node.x},{node.y}) 조사 완료 — {found.Count}종 발견.");

            OpenMenu(pt, node);   // 조사 결과를 그 자리에서 보여준다
        });
    }

    // ── 채집기지 ──────────────────────────────────────────────────────────────

    const string BuildLabel = "여기에 채집기지를 세운다";

    // 루트 메뉴의 진입점. 누르면 **건설 방식을 고르는 메뉴로 갈아탄다**(M&B의 메뉴 안 메뉴).
    WorldMenuOption BuildEntryOption(PartyToken pt, Vector2Int node)
    {
        var wm = WorldMap.Instance;

        if (pt == null)                     return WorldMenuOption.Blocked(BuildLabel, "여기 온 원정대가 없다");
        if (!wm.IsSurveyed(node))           return WorldMenuOption.Blocked(BuildLabel, "먼저 탐험해야");
        if (wm.DepositsAt(node).Count == 0) return WorldMenuOption.Blocked(BuildLabel, "캘 것이 없다");
        if (OutpostManager.Instance == null) return WorldMenuOption.Blocked(BuildLabel, "OutpostManager 없음");

        return WorldMenuOption.Ready(BuildLabel, () => OpenBuildMenu(pt, node));
    }

    // 건설 방식 선택 — 자동이냐 직접이냐.
    //  자동: 들어가지 않아도 알아서 캐 준다. 대신 배치의 자유가 없다.
    //  직접: 필드맵에 들어가 내가 짓는다. 아직 필드맵 생성이 없어 막혀 있다.
    void OpenBuildMenu(PartyToken pt, Vector2Int node)
    {
        if (menuPanel == null) return;

        var mgr     = OutpostManager.Instance;
        var wm      = WorldMap.Instance;
        var tile    = wm.GetTile(node);
        var options = new List<WorldMenuOption>();

        if (mgr != null && !mgr.CanAfford(out var missing))
            options.Add(WorldMenuOption.Blocked("자동 건설", $"{missing} 부족"));
        else
            options.Add(WorldMenuOption.Ready("자동 건설", () =>
            {
                if (mgr != null && mgr.Build(node) != null) OpenMenu(pt, node);
            }));

        options.Add(WorldMenuOption.Blocked("직접 건설", "준비 중 · 필드맵 생성 필요"));
        options.Add(WorldMenuOption.Ready("돌아가기", () => OpenMenu(pt, node)));

        // 정보 영역에 **무엇이 드는지**와 무엇이 묻혔는지를 같이 보여준다 — 고르기 전에 알아야 한다.
        var sb = new System.Text.StringBuilder();
        sb.Append($"채집기지 · ({node.x}, {node.y})\n");
        sb.Append($"비용 — {(mgr != null ? mgr.CostSummary() : "?")}\n");

        var deposits = wm.DepositsAt(node);
        for (int i = 0; i < deposits.Count; i++)
        {
            sb.Append(i == 0 ? "매장 — " : " · ");
            sb.Append($"{deposits[i].DisplayName} {deposits[i].Amount}");
        }

        sb.Append("\n<color=#FFFFFF80>자동 건설은 들어가지 않아도 매일 캐서 쌓아둡니다. " +
                  "쌓인 것은 들러서 실어가야 합니다.</color>");

        grid.SetHighlight(node);
        menuPanel.Open("건설 방식", sb.ToString(), MenuArtFor(tile), tile.Biome.Color(), options);
    }

    // 기지에 쌓인 것을 등짐으로.
    WorldMenuOption LoadOption(PartyToken pt, Vector2Int node)
    {
        var outpost = OutpostManager.Instance?.At(node);
        if (outpost == null)          return WorldMenuOption.Blocked("비축분을 싣는다", "기지 정보 없음");
        if (outpost.StockCount == 0)  return WorldMenuOption.Blocked("비축분을 싣는다", "아직 쌓인 게 없다");
        if (pt == null)               return WorldMenuOption.Blocked("비축분을 싣는다", "여기 온 원정대가 없다");
        if (pt.Party.Members.Count == 0) return WorldMenuOption.Blocked("비축분을 싣는다", "동행 없음");
        if (!pt.Party.HasRoom)        return WorldMenuOption.Blocked("비축분을 싣는다", "등짐 가득");

        return WorldMenuOption.Ready("비축분을 싣는다", () =>
        {
            OutpostManager.Instance.LoadInto(outpost, pt.Party);
            OpenMenu(pt, node);
        });
    }

    void CloseMenu()
    {
        if (menuPanel != null) menuPanel.Close();
        _menuParty    = null;
        _bandMenuOpen = false;
        grid?.ClearHighlight();
    }

    // 선택한 팀이 서 있는 칸의 메뉴를 다시 연다 — "떠난다"로 닫은 뒤 되돌아갈 길.
    void HandleEnter(Keyboard kb)
    {
        if (kb == null || !kb.enterKey.wasPressedThisFrame) return;
        if (menuPanel != null && menuPanel.IsOpen) return;
        if (_selected.Count != 1) return;          // 누구 메뉴인지 모호하면 열지 않는다

        var pt = _selected[0];
        if (pt.Moving) return;

        OpenMenu(pt, pt.Party.CurrentNode);
    }

    // ── 등짐 ─────────────────────────────────────────────────────────────────
    // 채집기지에 쌓인 것을 실으면 여기 담기고, 거점에 들어가면 창고로 비워진다.
    // **본거지로 순간이동하지 않는다** — 실어 나르는 것이 원정대의 일이다.

    // 등짐을 거점에 푼다. **어떤 경우에도 사라지지 않게** 세 단계로 받아낸다:
    //   ① 창고  ② 돌아온 폰들의 인벤토리  ③ 핵 앞 바닥에 드롭
    //
    // 예전엔 창고가 못 받은 양을 `party.Cargo`에 되돌려 놓았는데, 호출자가 바로 뒤에서
    // `RemoveParty`로 그 파티를 버렸다 — "조용히 증발시키지 않는다"는 의도가 끝까지 이어지지
    // 않아 실제로는 통째로 사라졌다. 창고가 없거나 가득하거나 **필터가 그 아이템을 안 받으면**
    // 바로 걸리는 길이다(새로 만든 자원 아이템이 기존 창고 필터에 없을 수 있다).
    void UnloadCargo(ExpeditionParty party)
    {
        if (party == null || party.Cargo.Count == 0) return;

        var core = DungeonCore.Instance;
        var at   = core != null ? core.transform.position : Vector3.zero;

        // 가까운 창고부터 채운다. `FindNearest`를 반복하면 **가장 가까운 창고가 가득일 때
        // 매번 같은 창고를 다시 집어** 남은 양이 갈 곳을 못 찾으므로, 목록을 한 번 정렬해 훑는다.
        var storages = new List<Storage>();
        foreach (var s in Storage.All)
            if (s != null && s.IsActive) storages.Add(s);
        storages.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - at)
                        .CompareTo(Vector3.SqrMagnitude(b.transform.position - at)));

        int moved = 0, toPawns = 0, dropped = 0;
        Vector3 dropAt = core != null ? core.GetSpawnPosition() : at;

        foreach (var kv in party.Cargo)
        {
            int remaining = kv.Value;

            // ① 창고 — 필터(CanAccept)와 남은 칸은 여기서 걸린다.
            foreach (var s in storages)
            {
                if (remaining <= 0) break;
                int added = s.TryAdd(kv.Key, remaining);
                remaining -= added;
                moved     += added;
            }

            // ② 돌아온 폰들의 인벤토리 — 창고 필터에 없는 새 아이템이 여기서 살아남는다.
            //    실제로 지고 온 사람들이므로 손에 남는 게 자연스럽다.
            foreach (var m in party.Members)
            {
                if (remaining <= 0) break;
                var inv = m != null ? m.Inventory : null;
                if (inv == null) continue;
                int added = inv.TryAdd(kv.Key, remaining);
                remaining -= added;
                toPawns   += added;
            }

            // ③ 그래도 남으면 핵 앞에 떨군다. 운반 작업이 알아서 주워 간다 —
            //    제작 취소 환불(`CraftJob`/`ResearchJob`)이 쓰는 것과 같은 마지막 안전망.
            if (remaining > 0)
            {
                DroppedItem.Spawn(kv.Key, remaining, dropAt + Vector3.up * 0.3f);
                dropped += remaining;
            }
        }

        party.ClearCargo();   // 전량이 셋 중 하나로 갔다 — 이제 비워도 잃는 것이 없다

        var sb = new System.Text.StringBuilder("[WorldMap] 등짐 하역 —");
        if (moved   > 0) sb.Append($" 창고 {moved}개");
        if (toPawns > 0) sb.Append($" · 인벤토리 {toPawns}개");
        if (dropped > 0) sb.Append($" · 바닥 {dropped}개");
        Debug.Log(sb.ToString());

        if (dropped > 0)
            Debug.LogWarning($"[WorldMap] {dropped}개가 창고·인벤토리에 들어가지 못해 핵 앞에 떨어졌습니다 " +
                             "(창고 공간이나 아이템 필터를 확인하세요).");
    }

    Sprite MenuArtFor(WorldTile tile)
    {
        if (visuals == null) return null;
        var c = visuals.GetContent(tile.Content);
        if (c != null && c.MenuArt != null) return c.MenuArt;
        var b = visuals.GetVisual(tile.Biome);
        return b != null ? b.MenuArt : null;
    }

    // ── 팀 생성 / 복귀 ────────────────────────────────────────────────────────
    // **편성은 여기서 하지 않는다** — `ExpeditionUI`(G키)에서 고르고
    // `ExpeditionFormation`이 집결까지 마친 뒤 아래 진입점으로 넘긴다.

    // 집결을 마친 폰들로 **새 원정대 하나**를 만든다. 나갈 때마다 팀이 하나씩 늘어난다.
    public void AddMembers(IEnumerable<UnitBrain> units)
    {
        if (units == null || grid == null) return;

        var members = new List<UnitBrain>();
        foreach (var u in units) if (u != null) members.Add(u);
        if (members.Count == 0) return;

        var party = new ExpeditionParty(StartNode());
        foreach (var m in members)
        {
            party.Members.Add(m);
            m.gameObject.SetActive(false);   // 거점 필드맵에서 빠짐(노동력 손실)
        }

        var pt = SpawnToken(party);
        ClearSelection();
        Select(pt);                          // 방금 떠난 팀을 바로 조작할 수 있게

        Debug.Log($"[WorldMap] 원정대 편성 — {members.Count}명 (나가 있는 팀 {_parties.Count})");
    }

    // 출발 지점 = 지금 활성 거점의 노드. 없으면 월드맵의 시작 노드.
    Vector2Int StartNode()
    {
        var active = SettlementManager.Instance?.Active;
        if (active != null) return active.WorldNode;

        var wm = WorldMap.Instance;
        return wm.Settlements.Count > 0 ? wm.Settlements[0] : wm.StartNode;
    }

    PartyToken SpawnToken(ExpeditionParty party)
    {
        var pt = new PartyToken { Party = party };

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "PartyToken";
        go.transform.SetParent(mapRoot, false);

        // 콜라이더는 **남기되 트리거로** 둔다.
        //   남기는 이유: 좌클릭으로 팀을 고르려면 맞을 것이 있어야 한다.
        //   트리거인 이유: `WorldMapGrid`의 표면 레이캐스트는 위에서 아래로 쏘는데,
        //   솔리드 콜라이더면 **토큰이 자기 자신을 맞아** 매 프레임 제 머리 위로 올라간다
        //   (토큰이 하늘로 날아가던 버그). 그 레이캐스트는 트리거를 무시하므로 이걸로 끊긴다.
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        pt.Go   = go;
        pt.Rend = go.GetComponent<Renderer>();
        pt.Rend.material = UnlitMat();

        go.transform.localScale = Vector3.one * TokenSize(pt);
        go.transform.position   = GroundedTokenPos(grid.SurfacePoint(party.CurrentNode), pt);

        _parties.Add(pt);
        _byGo[go] = pt;
        Tint(pt);
        return pt;
    }

    void RemoveParty(PartyToken pt)
    {
        if (pt == null) return;

        if (_menuParty == pt) CloseMenu();
        _selected.Remove(pt);
        _parties.Remove(pt);

        if (pt.Go != null)
        {
            _byGo.Remove(pt.Go);
            Destroy(pt.Go);
        }
    }

    // 정착지 진입: 대상 거점 전환(여기서 그 루트가 켜진다) → 화면을 필드로 → 원정대 멤버 복귀.
    // 순서 중요 — 대상 루트가 켜진 뒤에 멤버를 되살려야 스폰 위치/부모가 유효하다.
    void EnterSettlement(PartyToken pt, Vector2Int node)
    {
        if (pt == null) return;

        SettlementManager.Instance?.SwitchToNode(node);
        GameModeManager.Instance?.SwitchTo(GameMode.Field);

        var root = SettlementManager.Instance?.Active?.Root;
        var core = DungeonCore.Instance;   // SwitchTo가 SetAsActive로 갱신
        foreach (var m in pt.Party.Members)
        {
            if (m == null) continue;
            // 출발 거점과 다른 곳에 들어왔다면 그 거점 루트로 옮긴다 —
            // 안 옮기면 출발 거점 루트가 꺼질 때 같이 사라진다.
            if (root != null) m.transform.SetParent(root.transform, true);
            m.gameObject.SetActive(true);
            if (core != null) m.transform.position = core.GetSpawnPosition();
        }

        // 밖에서 실어온 것을 푼다. 순서가 둘 다 중요하다 —
        //   · **멤버를 되살린 뒤**라야 창고가 레지스트리에 올라 있고,
        //   · **멤버를 비우기 전**이라야 창고가 안 받는 것을 폰 인벤토리로 넘길 수 있다.
        UnloadCargo(pt.Party);
        pt.Party.Members.Clear();

        // 돌아온 팀은 더 이상 지도 위에 없다 → 토큰도 사라진다.
        RemoveParty(pt);
    }

    // ── 머티리얼 ──────────────────────────────────────────────────────────────
    // 파티 토큰용(항상 눈에 띄어야 하므로 조명 무시). 격자선·마커 머티리얼은 `WorldMapGrid` 몫.

    static Material UnlitMat()
    {
        var sh = RuntimeShaders.Find(RuntimeShaders.Unlit, RuntimeShaders.UnlitTexture,
                                     RuntimeShaders.SpritesDefault);
        return new Material(sh);
    }

    static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }
}

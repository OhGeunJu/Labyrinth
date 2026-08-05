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

    // 월드맵 위의 원정대 하나 — 논리(파티)와 화면(토큰)과 이동 상태를 묶는다.
    class PartyToken
    {
        public ExpeditionParty Party;
        public GameObject      Go;
        public Renderer        Rend;
        public Vector3         MoveTarget;
        public bool            Moving;
        public bool            Selected;
    }

    readonly List<PartyToken> _parties  = new();
    readonly List<PartyToken> _selected = new();
    readonly Dictionary<GameObject, PartyToken> _byGo = new();   // 레이캐스트 → 팀

    PartyToken _menuParty;      // 지금 열린 도착 메뉴가 누구 것인가
    Vector2 _dragStart;
    bool    _dragging;

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
        WorldMap.OnNodeChanged -= OnNodeChanged;
        if (menuPanel != null) menuPanel.Closed -= OnMenuClosed;
    }

    void OnMenuClosed()
    {
        _menuParty = null;
        grid?.ClearHighlight();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
        {
            CloseMenu();
            GameModeManager.Instance?.Toggle();
            return;   // 전환한 프레임에 반대편 입력까지 처리하지 않게
        }

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
        ClearSelection();
        if (TryPickParty(out var pt)) { Select(pt); return; }

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

    bool TryPickParty(out PartyToken pt)
    {
        pt = null;
        if (OverUI()) return false;

        foreach (var h in SortedHits())
            if (h.collider != null && _byGo.TryGetValue(h.collider.gameObject, out pt)) return true;

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
        if (_selected.Count == 0 || !TryPickTerrain(out var hit)) return;

        var spots = Formation(hit.point, _selected.Count);
        for (int i = 0; i < _selected.Count; i++)
        {
            _selected[i].MoveTarget = spots[i];
            _selected[i].Moving     = true;
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
            if (!pt.Moving || pt.Go == null) continue;

            var pos  = pt.Go.transform.position;
            var from = new Vector3(pos.x, 0f, pos.z);
            var to   = new Vector3(pt.MoveTarget.x, 0f, pt.MoveTarget.z);

            var next = Vector3.MoveTowards(from, to, partySpeed * Time.deltaTime);
            pt.Go.transform.position = GroundedTokenPos(next, pt);

            // 지나온 자리의 칸을 계속 갱신 — 어느 지역에 있는지는 여기서 나온다.
            if (grid.TryNodeAt(next, out var node)) pt.Party.CurrentNode = node;

            if ((next - to).sqrMagnitude >= 0.0025f) continue;

            pt.Moving = false;

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
                options.Add(WorldMenuOption.Blocked("마을을 둘러본다", "준비 중"));
                options.Add(WorldMenuOption.Blocked("시장에 간다",     "준비 중"));
                break;

            case WorldNodeContent.EnemyLair:
                options.Add(WorldMenuOption.Blocked("소굴을 정찰한다", "준비 중"));
                options.Add(WorldMenuOption.Blocked("습격한다",        "준비 중"));
                break;

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
    static bool IsWildland(WorldNodeContent c) =>
        c == WorldNodeContent.Empty || c == WorldNodeContent.ResourceSite;

    static string TitleFor(WorldTile tile) =>
        tile.Content == WorldNodeContent.Empty
            ? tile.Biome.DisplayName()                    // 빈 야생은 "빈 지역"보다 바이옴 이름이 낫다
            : tile.Content.DisplayName();

    // 지역 정보 + **매장 자원**. 조사 전이면 무엇이 묻혔는지 감춘다.
    string InfoFor(PartyToken pt, Vector2Int node, WorldTile tile)
    {
        var wm = WorldMap.Instance;
        var sb = new System.Text.StringBuilder();

        sb.Append($"{tile.Biome.DisplayName()} · ({node.x}, {node.y}) · ");
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
        _menuParty = null;
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
        var sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Texture")
                 ?? Shader.Find("Sprites/Default");
        return new Material(sh);
    }

    static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }
}

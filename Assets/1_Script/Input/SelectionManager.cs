using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [Header("레이어")]
    [SerializeField] LayerMask unitMask;

    [Header("드래그 최소 거리 (px)")]
    [SerializeField] float dragThreshold = 5f;

    Camera _cam;
    Vector2 _dragStart;
    bool _isDragging;
    bool _pressOverUI;   // 이번 클릭이 UI 위에서 시작됐는가 — 놓을 때까지 월드 선택을 막는다

    readonly List<PawnMover> _selected = new();
    public IReadOnlyList<PawnMover> Selected => _selected;

    public Action<PawnMover, bool> OnSelectionChanged;
    public static event Action<ResourceNode> OnNodeSelected;
    public static event Action<Corpse> OnCorpseSelected;

    // 조작 불가 유닛(적/NPC)을 클릭했을 때 — 명령 대상은 아니지만 정보는 보여준다.
    // 다른 것을 클릭하거나 빈 곳을 클릭하면 null로 해제된다.
    public static event Action<UnitBrain> OnPawnInspected;

    // **짓는 중인 청사진.** 다른 건물은 저마다 패널이 있는데(창고·침대·농지·작업대·연구대)
    // 청사진만 없었다 — 콜라이더가 없어 클릭 자체가 안 됐기 때문이다.
    // 폰 정보 패널(`UnitHPUI`)이 이걸 받아 같은 자리에 진행 상황을 띄운다.
    public static event Action<BuildingBlueprint> OnBlueprintInspected;

    // **다 지은 건물.** 저마다의 패널(창고·침대·농지…)을 **대신하지 않고 곁들인다** —
    // 그쪽은 그 건물로 무엇을 하는지를 다루고, 여기는 **건물 자체**를 다룬다(이름·이전·분해).
    // 그래서 둘이 같이 뜬다. 벽처럼 제 패널이 없는 건물은 이것만 뜬다.
    public static event Action<PlacedBuilding> OnBuildingInspected;

    // **던전 핵은 `PlacedBuilding`이 아니다** — 배치로 지은 물건이 아니라 씬에 놓인 축이라
    // 분해도 환불도 없다. 그래도 "이게 무엇이고 무엇을 할 수 있는가"는 같은 자리에서 답한다.
    public static event Action<DungeonCore> OnCoreInspected;

    // 좌클릭 경로 **밖에서** 같은 패널을 열어야 할 때 쓰는 통로(우클릭 선택지 창 등).
    // 이벤트는 선언한 클래스에서만 발행할 수 있어서 이렇게 문을 내야 한다 —
    // 부르는 쪽이 자기 이벤트를 따로 만들면 패널을 여는 길이 둘이 되어 어긋난다.
    public static void InspectNode(ResourceNode node)   => OnNodeSelected?.Invoke(node);
    public static void InspectCorpse(Corpse corpse)     => OnCorpseSelected?.Invoke(corpse);

    void Awake()
    {
        _cam = Camera.main;
        OnSelectionChanged += (pawn, selected) =>
        {
            if (pawn == null) return; // 선택 중 파괴된 유닛
            var indicator = pawn.GetComponent<SelectionIndicator>();
            if (indicator != null) indicator.SetSelected(selected);
        };
    }

    void Update()
    {
        PruneSelection();
        HandleSelection();
    }

    // **선택에서 사라진 폰을 걷어낸다.**
    //
    // 선택이 지속 상태가 되면서(빈 곳 클릭이 더는 안 지운다) 청소할 곳이 필요해졌다 —
    // 전에는 땅을 한 번 누르는 것이 사실상 청소였다.
    //
    // 걷어내는 것 둘: **파괴된 폰**(죽었다)과 **꺼진 폰**(원정을 떠나 GameObject가 비활성).
    // 꺼진 것은 **`== null`로 안 걸린다** — 그래서 원정 나간 폰이 선택에 남아 우클릭 명령이
    // 지도 밖의 누군가에게 조용히 나갔다.
    void PruneSelection()
    {
        for (int i = _selected.Count - 1; i >= 0; i--)
        {
            var pawn = _selected[i];
            if (pawn != null && pawn.gameObject.activeInHierarchy) continue;

            _selected.RemoveAt(i);
            OnSelectionChanged?.Invoke(pawn, false);   // 표식 해제(파괴된 것은 구독자가 거른다)
        }
    }

    void HandleSelection()
    {
        // 건설 모드·핵 이전 모드 중에는 클릭이 그쪽 전용 (선택으로 새어나가지 않게)
        if (BuildModeManager.IsActive || CoreRelocator.IsActive) return;

        // **UI에서 시작한 클릭은 놓는 순간까지 통째로 무시한다.**
        // 예전엔 누를 때만 UI를 보고 걸렀는데, 그러면 배낭에서 바닥으로 **드래그해 물건을
        // 버릴 때** 누른 곳(UI)은 걸러지고 놓은 곳(월드)은 안 걸러진다. 그 사이 `_dragStart`가
        // 지난번 값 그대로라 커서와 멀어져 있어 사각 선택으로 잡히고, `SelectByRect`가
        // 엉뚱한 사각형으로 **선택을 통째로 지웠다** — 물건 하나 버릴 때마다 무리가 풀렸다.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _pressOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (_pressOverUI) return;

            _dragStart = Mouse.current.position.ReadValue();
            _isDragging = false;
        }

        if (_pressOverUI)
        {
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _pressOverUI = false;
                _isDragging  = false;
            }
            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 cur = Mouse.current.position.ReadValue();
            if (!_isDragging && Vector2.Distance(_dragStart, cur) > dragThreshold)
                _isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _isDragging = false;
                return;
            }

            if (_isDragging)
                SelectByRect();
            else
                SelectSingle();

            _isDragging = false;
        }
    }

    void SelectSingle()
    {
        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        // 건물·청사진 조사는 **한 클릭짜리**다 — 여기서 먼저 지우고, 실제로 눌렀을 때만 다시
        // 켠다. 분기마다 해제를 적으면 새 분기가 생길 때 하나가 반드시 빠진다
        // (`OnPawnInspected`가 지금 그 꼴이다 — 노드 분기에서는 안 지운다).
        OnBlueprintInspected?.Invoke(null);
        OnBuildingInspected?.Invoke(null);
        OnCoreInspected?.Invoke(null);

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 500f);
        if (hasHit)
        {
            var core = hit.collider.GetComponentInParent<DungeonCore>();
            if (core != null)
            {
                // 핵도 선택을 유지한다 — 소환·핵 이전을 고른 폰과 함께 다루게 된다.
                OnNodeSelected?.Invoke(null);
                OnCoreInspected?.Invoke(core);   // 정보 패널에도 "이게 무엇인가"를 띄운다
                core.Select();
                return;
            }

            var node = hit.collider.GetComponent<ResourceNode>();
            if (node != null)
            {
                // **폰 선택을 유지한 채** 노드 패널을 연다 — "폰을 고른다 → 노드를 클릭한다
                // → [배정]"이 자연스러운 순서인데, 여기서 선택을 지우면 배정 버튼이 항상
                // 빈 목록을 받는다(`ResourceNodeUI.AssignSelected`가 Selected를 읽는다).
                // 시체 클릭도 같은 이유로 선택을 유지한다.
                OnNodeSelected?.Invoke(node);
                return;
            }

            var corpse = hit.collider.GetComponent<Corpse>();
            if (corpse != null)
            {
                OnCorpseSelected?.Invoke(corpse);
                return;
            }

            // 바닥에 떨어진 물건도 **선택을 유지**한다(노드·시체와 같은 이유). 이걸로 할 일은
            // 우클릭 메뉴에 있고, 그건 고른 폰에게 시키는 것이라 여기서 선택을 지우면
            // 물건을 한 번 누를 때마다 폰을 다시 골라야 한다.
            if (hit.collider.GetComponent<DroppedItem>() != null) return;

            var pawn = hit.collider.GetComponent<PawnMover>();
            var faction = hit.collider.GetComponent<Faction>();
            if (pawn != null && faction != null && faction.IsPlayer)
            {
                ClearSelection();
                DungeonCore.Instance?.Deselect();
                OnNodeSelected?.Invoke(null);
                OnPawnInspected?.Invoke(null);
                AddToSelection(pawn);
                return;
            }

            // **유닛이면서 클릭 대상이기도 한 것**(상인)은 그쪽이 이긴다.
            // 상인이 제대로 된 NPC 폰이 되면서 아래 `PawnMover` 분기에 먼저 걸려,
            // 좌클릭하면 상점 대신 유닛 정보가 떴다 — 상점을 열 방법이 없어진 것이다.
            // 유닛으로서의 정보는 우클릭 메뉴와 상점 창에서 볼 수 있다.
            if (pawn != null && hit.collider.GetComponent<IWorldClickable>() is { } pawnClickable
                             && AcceptsClick(pawnClickable))
            {
                DungeonCore.Instance?.Deselect();
                OnNodeSelected?.Invoke(null);
                OnPawnInspected?.Invoke(null);
                pawnClickable.OnWorldClick();
                return;
            }

            // 조작 불가 유닛(적/NPC) — 선택(명령)은 안 되지만 정보 패널은 띄운다.
            //
            // **폰 선택은 유지한다**(노드·시체·드롭·건물과 같은 규칙). 적을 눌러 보는 이유는
            // 대개 **그 다음에 무언가를 시키려는 것**이다 — 살펴보고 우클릭으로 친다.
            // 여기서 선택을 지우면 적을 한 번 볼 때마다 폰을 다시 골라야 한다.
            // 정보 패널은 `UnitHPUI`가 "조사 중인 것이 있으면 그쪽"으로 가려 준다.
            if (pawn != null)
            {
                DungeonCore.Instance?.Deselect();
                OnNodeSelected?.Invoke(null);
                OnPawnInspected?.Invoke(pawn.GetComponent<UnitBrain>());
                return;
            }

            // 클릭 가능한 건물(창고/침대/농지/상인/작업대/연구대/순찰구역) — 단일 레이캐스트로 라우팅.
            //
            // **폰 선택은 유지한다.** 이 건물들의 패널은 대개 선택한 폰을 상대로 뭔가를 한다 —
            // 창고에 폰 인벤토리의 물건을 옮기고, 상인에게 팔고, 침대를 배정한다.
            // 클릭할 때마다 선택이 풀리면 "폰 고르기 → 건물 열기"를 매번 반대로 해야 한다.
            // (노드·시체도 같은 이유로 유지한다.)
            var clickable = hit.collider.GetComponent<IWorldClickable>();
            if (clickable != null && AcceptsClick(clickable))
            {
                DungeonCore.Instance?.Deselect();
                OnNodeSelected?.Invoke(null);
                OnPawnInspected?.Invoke(null);

                // **건물 자체를 다루는 줄은 늘 뜬다** — 휴면이라 제 패널을 못 열어도
                // 옮기거나 분해할 수는 있어야 한다(오히려 그때 제일 필요하다).
                OnBuildingInspected?.Invoke(hit.collider.GetComponentInParent<PlacedBuilding>());

                // 휴면(핵 영역 밖) 건물은 사용 불가 → 그 건물의 패널은 열지 않는다.
                if (BuildingDormancy.IsDormantAt(hit.collider))
                {
                    Debug.Log("[Selection] 핵 영역 밖 건물은 사용할 수 없습니다(휴면).");
                    return;
                }
                clickable.OnWorldClick();
                return;
            }

            // **제 패널이 없는 건물**(벽 등) — 지금까지는 눌러도 아무 일도 안 일어났다.
            // 이름·이전·분해는 어느 건물에나 있는 일이라 여기서 받는다.
            //
            // **바닥은 뺀다.** 바닥도 `PlacedBuilding`이지만 그건 밟고 다니는 substrate라,
            // 세면 **빈 땅을 누를 때마다 건물 창이 뜬다** — 가장 잦은 클릭이 망가진다.
            // 바닥을 걷어내는 길은 건설 모드의 철거가 이미 맡고 있다.
            var plain = hit.collider.GetComponentInParent<PlacedBuilding>();
            if (plain != null && (plain.Data == null || !plain.Data.IsFloor))
            {
                DungeonCore.Instance?.Deselect();
                OnNodeSelected?.Invoke(null);
                OnPawnInspected?.Invoke(null);
                OnBuildingInspected?.Invoke(plain);
                return;
            }
        }

        // **청사진은 콜라이더가 없어 위의 어느 분기에도 안 걸린다** — 고스트는 콜라이더를
        // 일부러 다 껐다(통행을 안 막아야 하고, 켜면 그 위로 폰을 보낼 수가 없다).
        // 레이는 밑의 지형을 맞았을 테니, 그 지점을 타일로 바꿔 그 칸의 청사진을 찾는다.
        //
        // **폰 선택은 유지한다** — 다른 건물·노드·시체와 같은 규칙이다.
        if (hasHit && MapManager.Instance != null)
        {
            var bp = BuildingBlueprint.PickAt(MapManager.Instance.WorldToTile(hit.point));
            if (bp != null)
            {
                DungeonCore.Instance?.Deselect();
                OnNodeSelected?.Invoke(null);
                OnPawnInspected?.Invoke(null);
                OnBlueprintInspected?.Invoke(bp);
                return;
            }
        }

        // 빈 곳 클릭 — **패널만 닫고 폰 선택은 그대로 둔다**(켄시식).
        //
        // 선택은 "지금 누구에게 명령하고 있나"라는 **지속 상태**다. 빈 땅을 눌렀다고 그게
        // 바뀔 이유가 없고, 실제로는 반대다 — 카메라를 돌리다 땅을 한 번 스치면 데리고
        // 다니던 팀이 통째로 풀려서, 우클릭 한 번 하려고 매번 다시 고르게 된다.
        //
        // 그래서 **선택을 푸는 별도 조작은 두지 않는다.** 바꾸는 길(다른 폰 클릭 · 사각 선택 ·
        // 로스터)이 이미 있고, 켄시도 그렇다. "아무도 안 고른 상태"가 필요한 곳은 없다.
        DungeonCore.Instance?.Deselect();
        OnNodeSelected?.Invoke(null);
        OnPawnInspected?.Invoke(null);
    }

    // 지금 이 클릭을 받을 수 있는가(`IClickGate`). 못 받으면 아래 분기로 흘러간다 —
    // 쓰러진 상인은 상점 대신 폰 정보를 띄운다.
    static bool AcceptsClick(IWorldClickable clickable)
        => clickable is not IClickGate gate || gate.CanClick;

    void SelectByRect()
    {
        Vector2 end = Mouse.current.position.ReadValue();
        Rect screenRect = GetScreenRect(_dragStart, end);

        // **먼저 훑고 나서 바꾼다.** 예전엔 여기서 `ClearSelection()`을 하고 담았는데,
        // 그러면 **빈 땅을 훑는 순간 선택이 통째로 날아간다** — 빈 곳 클릭과 같은 사고이고
        // 드래그는 더 자주 빗나간다(사각형이 조금만 어긋나도 아무도 안 잡힌다).
        var caught = new List<PawnMover>();
        UnitBrain inspected = null;   // 우리 폰이 하나도 안 잡혔을 때 대신 보여줄 유닛

        foreach (var pawn in FindObjectsByType<PawnMover>(FindObjectsSortMode.None))
        {
            Vector3 screenPos = _cam.WorldToScreenPoint(pawn.transform.position);
            if (screenPos.z <= 0f || !screenRect.Contains((Vector2)screenPos, true)) continue;

            var faction = pawn.GetComponent<Faction>();
            if (faction != null && faction.IsPlayer) { caught.Add(pawn); continue; }

            if (inspected == null) inspected = pawn.GetComponent<UnitBrain>();
        }

        // **잡힌 것이 있을 때만 갈아치운다.** 하나도 못 잡았으면 하던 선택을 그대로 둔다.
        if (caught.Count > 0)
        {
            ClearSelection();
            foreach (var pawn in caught) AddToSelection(pawn);
        }

        // **드래그로 남의 유닛을 훑으면 스탯을 보여준다.**
        // 좌클릭은 상인에게서 상점을 여는 데 쓰이므로(그쪽이 더 자주 하는 일),
        // 유닛으로서의 정보를 볼 길이 드래그에 남는다. 우리 폰이 하나라도 잡혔으면
        // 그건 명령을 위한 선택이므로 정보 패널을 띄우지 않는다.
        OnPawnInspected?.Invoke(caught.Count == 0 ? inspected : null);
    }

    void AddToSelection(PawnMover pawn)
    {
        if (pawn == null || _selected.Contains(pawn)) return;   // 중복 선택 방지
        _selected.Add(pawn);
        OnSelectionChanged?.Invoke(pawn, true);
    }

    public void SelectPawn(PawnMover pawn)
    {
        ClearSelection();
        AddToSelection(pawn);
    }

    // ── 로스터(목록 UI)용 진입점 ──────────────────────────────────────────
    // 월드 클릭과 달리 목록에서는 Ctrl로 하나씩 더하고 드래그로 범위를 잡는다.
    // 선택 상태의 주인은 여기 하나뿐이므로 목록도 이 메서드들을 거쳐야 한다 —
    // UI가 제 목록을 따로 들면 월드 선택과 어긋난다.

    public void AddPawn(PawnMover pawn) => AddToSelection(pawn);

    // Ctrl+클릭: 이미 있으면 빼고 없으면 더한다.
    public void TogglePawn(PawnMover pawn)
    {
        if (pawn == null) return;
        if (_selected.Remove(pawn)) OnSelectionChanged?.Invoke(pawn, false);
        else                        AddToSelection(pawn);
    }

    // 드래그 범위: 넘긴 것들'만' 선택된 상태로 만든다.
    public void SelectOnly(IEnumerable<PawnMover> pawns)
    {
        ClearSelection();
        if (pawns == null) return;
        foreach (var p in pawns) AddToSelection(p);
    }

    public void ClearPawnSelection() => ClearSelection();

    void ClearSelection()
    {
        foreach (var p in _selected)
            OnSelectionChanged?.Invoke(p, false);
        _selected.Clear();
    }

    static Rect GetScreenRect(Vector2 a, Vector2 b) =>
        new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                 Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
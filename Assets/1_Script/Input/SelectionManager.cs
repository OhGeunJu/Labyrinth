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
        HandleSelection();
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

        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var core = hit.collider.GetComponent<DungeonCore>();
            if (core != null)
            {
                // 핵도 선택을 유지한다 — 소환·핵 이전을 고른 폰과 함께 다루게 된다.
                OnNodeSelected?.Invoke(null);
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
            if (pawn != null)
            {
                ClearSelection();
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
                // 휴면(핵 영역 밖) 건물은 사용 불가 → 패널도 열지 않는다.
                if (BuildingDormancy.IsDormantAt(hit.collider))
                {
                    Debug.Log("[Selection] 핵 영역 밖 건물은 사용할 수 없습니다(휴면).");
                    return;
                }
                clickable.OnWorldClick();
                return;
            }
        }

        // 빈 곳 클릭 — 전부 해제
        ClearSelection();
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
        ClearSelection();

        UnitBrain inspected = null;   // 우리 폰이 하나도 안 잡혔을 때 대신 보여줄 유닛

        foreach (var pawn in FindObjectsByType<PawnMover>(FindObjectsSortMode.None))
        {
            Vector3 screenPos = _cam.WorldToScreenPoint(pawn.transform.position);
            if (screenPos.z <= 0f || !screenRect.Contains((Vector2)screenPos, true)) continue;

            var faction = pawn.GetComponent<Faction>();
            if (faction != null && faction.IsPlayer) { AddToSelection(pawn); continue; }

            if (inspected == null) inspected = pawn.GetComponent<UnitBrain>();
        }

        // **드래그로 남의 유닛을 훑으면 스탯을 보여준다.**
        // 좌클릭은 상인에게서 상점을 여는 데 쓰이므로(그쪽이 더 자주 하는 일),
        // 유닛으로서의 정보를 볼 길이 드래그에 남는다. 우리 폰이 하나라도 잡혔으면
        // 그건 명령을 위한 선택이므로 정보 패널을 띄우지 않는다.
        OnPawnInspected?.Invoke(_selected.Count == 0 ? inspected : null);
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
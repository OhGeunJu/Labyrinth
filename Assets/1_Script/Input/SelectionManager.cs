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

    readonly List<PawnMover> _selected = new();
    public IReadOnlyList<PawnMover> Selected => _selected;

    public Action<PawnMover, bool> OnSelectionChanged;
    public static event Action<ResourceNode> OnNodeSelected;
    public static event Action<Corpse> OnCorpseSelected;

    // 조작 불가 유닛(적/NPC)을 클릭했을 때 — 명령 대상은 아니지만 정보는 보여준다.
    // 다른 것을 클릭하거나 빈 곳을 클릭하면 null로 해제된다.
    public static event Action<UnitBrain> OnPawnInspected;

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

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            _dragStart = Mouse.current.position.ReadValue();
            _isDragging = false;
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
                // 빈 목록을 받는다(`ResourceNodePanel.AssignSelected`가 Selected를 읽는다).
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
            if (clickable != null)
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

    void SelectByRect()
    {
        Vector2 end = Mouse.current.position.ReadValue();
        Rect screenRect = GetScreenRect(_dragStart, end);
        ClearSelection();

        foreach (var pawn in FindObjectsByType<PawnMover>(FindObjectsSortMode.None))
        {
            var faction = pawn.GetComponent<Faction>();
            if (faction == null || !faction.IsPlayer) continue;

            Vector3 screenPos = _cam.WorldToScreenPoint(pawn.transform.position);
            if (screenPos.z > 0f && screenRect.Contains((Vector2)screenPos, true))
                AddToSelection(pawn);
        }
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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [Header("레이어")]
    [SerializeField] LayerMask unitMask;
    [SerializeField] LayerMask groundMask = ~0;

    [Header("드래그 최소 거리 (px)")]
    [SerializeField] float dragThreshold = 5f;

    Camera _cam;
    Vector2 _dragStart;
    bool _isDragging;

    readonly List<PawnMover> _selected = new();
    public IReadOnlyList<PawnMover> Selected => _selected;

    // 선택 표시 연결 포인트 (나중에 구현)
    public System.Action<PawnMover, bool> OnSelectionChanged;

    void Awake() => _cam = Camera.main;

    void Update()
    {
        HandleSelection();
        HandleMove();
    }

    // ── 선택 ────────────────────────────────────────
    void HandleSelection()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
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
        ClearSelection();

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, unitMask))
        {
            var pawn = hit.collider.GetComponent<PawnMover>();
            if (pawn != null) AddToSelection(pawn);
        }
    }

    void SelectByRect()
    {
        Vector2 end = Mouse.current.position.ReadValue();
        Rect screenRect = GetScreenRect(_dragStart, end);
        ClearSelection();

        foreach (var pawn in FindObjectsByType<PawnMover>(FindObjectsSortMode.None))
        {
            Vector2 screenPos = _cam.WorldToScreenPoint(pawn.transform.position);
            if (screenRect.Contains(screenPos, true))
                AddToSelection(pawn);
        }
    }

    // ── 이동 명령 ────────────────────────────────────
    void HandleMove()
    {
        if (_selected.Count == 0) return;
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            // 여러 유닛이면 대형 간격 적용
            Vector3[] positions = CalcFormation(hit.point, _selected.Count);
            for (int i = 0; i < _selected.Count; i++)
                _selected[i].SetMoveTarget(positions[i]);
        }
    }

    // ── 대형 계산 (간단 격자) ────────────────────────
    static Vector3[] CalcFormation(Vector3 center, int count)
    {
        var positions = new Vector3[count];
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        float spacing = 1.2f;

        for (int i = 0; i < count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            float ox = (col - (cols - 1) * 0.5f) * spacing;
            float oz = (row - (Mathf.CeilToInt((float)count / cols) - 1) * 0.5f) * spacing;
            positions[i] = center + new Vector3(ox, 0f, oz);
        }
        return positions;
    }

    // ── 유틸 ────────────────────────────────────────
    void AddToSelection(PawnMover pawn)
    {
        _selected.Add(pawn);
        OnSelectionChanged?.Invoke(pawn, true);
    }

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
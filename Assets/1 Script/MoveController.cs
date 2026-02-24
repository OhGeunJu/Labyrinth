using UnityEngine;
using UnityEngine.InputSystem;

public class MoveController : MonoBehaviour
{
    public PawnMover pawn;
    public LayerMask groundMask = ~0;
    [SerializeField] InputActionAsset actions;

    Camera cam;
    InputAction move;
    InputAction pos;

    void Awake()
    {
        cam = Camera.main;

        if (actions == null)
        {
            Debug.LogError("[MoveController] actions가 비어있습니다. InputActionAsset을 넣어주세요.");
            return;
        }

        var map = actions.FindActionMap("Gameplay", true);
        move = map.FindAction("MouseMove", true);
        pos = map.FindAction("MousePos", true);
    }

    void OnEnable()
    {
        if (move == null || pos == null) return;

        move.Enable();
        pos.Enable();
        move.performed += OnMove;
    }

    void OnDisable()
    {
        if (move == null || pos == null) return;

        move.performed -= OnMove;
        move.Disable();
        pos.Disable();
    }

    void OnMove(InputAction.CallbackContext _)
    {
        if (pawn == null || cam == null) return;

        Vector2 p = pos.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(p);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
            pawn.SetMoveTarget(hit.point);
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 10f;

    [Header("회전")]
    [SerializeField] float rotateSpeed = 3f;

    [Header("줌")]
    [SerializeField] float zoomSpeed = 5f;
    [SerializeField] float minZoom = 5f;
    [SerializeField] float maxZoom = 50f;

    Camera _cam;
    bool _isRotating;
    Vector2 _lastMousePos;

    void Awake() => _cam = GetComponent<Camera>();

    void Update()
    {
        HandleMove();
        HandleRotate();
        HandleZoom();
    }

    void HandleMove()
    {
        Vector3 dir = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) dir += transform.forward;
        if (Keyboard.current.sKey.isPressed) dir -= transform.forward;
        if (Keyboard.current.aKey.isPressed) dir -= transform.right;
        if (Keyboard.current.dKey.isPressed) dir += transform.right;

        dir.y = 0f;

        if (dir != Vector3.zero)
            transform.position += dir.normalized * moveSpeed * Time.deltaTime;
    }

    void HandleRotate()
    {
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            _isRotating = true;
            _lastMousePos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame)
            _isRotating = false;

        if (!_isRotating) return;

        Vector2 curMousePos = Mouse.current.position.ReadValue();
        Vector2 delta = curMousePos - _lastMousePos;
        _lastMousePos = curMousePos;

        transform.eulerAngles += new Vector3(-delta.y * rotateSpeed * Time.deltaTime,
                                              delta.x * rotateSpeed * Time.deltaTime, 0f);
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - scroll * zoomSpeed * Time.deltaTime,
                                        minZoom, maxZoom);
    }
}
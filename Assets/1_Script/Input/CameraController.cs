using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 10f;

    [Header("회전")]
    [SerializeField] float rotateSpeed = 3f;
    [SerializeField] float minPitch = 5f;
    [SerializeField] float maxPitch = 89f;

    [Header("줌 (커서 방향 이동)")]
    [SerializeField] float zoomSpeed = 20f;
    [SerializeField] float minHeight = 3f;
    [SerializeField] float maxHeight = 80f;

    [Header("앵글 프리셋 (PageUp/Down)")]
    [SerializeField] float topDownPitch = 85f;
    [SerializeField] float lowAnglePitch = 10f;
    [SerializeField] float angleLerpSpeed = 5f;

    Camera _cam;
    bool _isRotating;
    Vector2 _lastMousePos;
    bool _lerpingToPitch;
    float _targetPitch;
    float _yaw;
    float _pitch;

    void Awake() => _cam = GetComponent<Camera>();

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    void Update()
    {
        HandleMove();
        HandleRotate();
        HandleZoom();
        HandleAnglePreset();
        ApplyRotation();
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

        _lerpingToPitch = false;

        Vector2 curMousePos = Mouse.current.position.ReadValue();
        Vector2 delta = curMousePos - _lastMousePos;
        _lastMousePos = curMousePos;

        _yaw += delta.x * rotateSpeed * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch - delta.y * rotateSpeed * Time.deltaTime, minPitch, maxPitch);
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        Vector3 zoomDir;
        if (groundPlane.Raycast(ray, out float dist))
            zoomDir = (ray.GetPoint(dist) - transform.position).normalized;
        else
            zoomDir = transform.forward;

        Vector3 newPos = transform.position + zoomDir * scroll * zoomSpeed * Time.deltaTime;
        newPos.y = Mathf.Clamp(newPos.y, minHeight, maxHeight);
        transform.position = newPos;
    }

    void HandleAnglePreset()
    {
        if (Keyboard.current.pageUpKey.wasPressedThisFrame)
        {
            _targetPitch = topDownPitch;
            _lerpingToPitch = true;
        }
        else if (Keyboard.current.pageDownKey.wasPressedThisFrame)
        {
            _targetPitch = lowAnglePitch;
            _lerpingToPitch = true;
        }

        if (!_lerpingToPitch) return;

        _pitch = Mathf.LerpAngle(_pitch, _targetPitch, angleLerpSpeed * Time.deltaTime);

        if (Mathf.Abs(Mathf.DeltaAngle(_pitch, _targetPitch)) < 0.5f)
        {
            _pitch = _targetPitch;
            _lerpingToPitch = false;
        }
    }

    void ApplyRotation()
    {
        transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);
    }
}

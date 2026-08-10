using UnityEngine;
using UnityEngine.EventSystems;
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

    // ── 특정 지점 비추기 ─────────────────────────────────────────────────
    // 목록에서 폰을 더블클릭했을 때처럼 **화면 밖에 있는 것을 보여줘야** 할 때 쓴다.
    //
    // **높이와 각도는 안 건드린다.** 지금 보고 있는 시점을 그대로 두고 XZ만 옮긴다 —
    // 줌·기울기를 같이 바꾸면 "어디로 튀었지"가 되어 오히려 위치를 잃는다.
    public void FocusOn(Vector3 worldPoint)
    {
        // 카메라가 기울어 있으므로 지금 화면 한가운데가 가리키는 지면과의 **차이**만큼 민다.
        // 그래야 대상이 화면 정중앙에 온다(그냥 대상 좌표로 가면 화면 위쪽에 걸린다).
        Vector3 look = GroundUnderCenter();
        Vector3 delta = new Vector3(worldPoint.x - look.x, 0f, worldPoint.z - look.z);
        transform.position += delta;
    }

    // 화면 한가운데 광선이 y=대상높이 평면과 만나는 곳. 지형 콜라이더에 기대지 않는다
    // (지형이 없는 던전 씬에서도 동작해야 한다).
    Vector3 GroundUnderCenter()
    {
        var cam = _cam != null ? _cam : GetComponent<Camera>();
        if (cam == null) return transform.position;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : transform.position;
    }

    // 씬에 여럿(필드·월드)이 있으므로 **켜져 있는 것**을 고른다.
    public static CameraController Active
    {
        get
        {
            foreach (var c in FindObjectsByType<CameraController>(FindObjectsSortMode.None))
                if (c != null && c.isActiveAndEnabled) return c;
            return null;
        }
    }

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    // **카메라는 게임 시계가 아니라 실시간으로 움직인다.**
    //
    // `GameTimeManager.SetSpeed`가 `Time.timeScale`을 직접 만지므로, 멈추면(`Speed = 0`)
    // `Time.deltaTime`이 **0이 되어 카메라가 통째로 얼어붙는다** — 이동·회전·줌이 전부 죽는다.
    // 그런데 멈춰 세우는 이유의 절반은 **차분히 둘러보려고**다. 배속도 같은 문제다:
    // 3배속이면 카메라가 세 배로 튄다(조작 감각이 게임 속도를 따라갈 이유가 없다).
    static float Dt => Time.unscaledDeltaTime;

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
            transform.position += dir.normalized * moveSpeed * Dt;
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

        _yaw += delta.x * rotateSpeed * Dt;
        _pitch = Mathf.Clamp(_pitch - delta.y * rotateSpeed * Dt, minPitch, maxPitch);
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll == 0f) return;

        // 커서가 UI 위면 휠은 그 패널의 스크롤 몫 — 카메라가 가로채지 않는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        Vector3 zoomDir;
        if (groundPlane.Raycast(ray, out float dist))
            zoomDir = (ray.GetPoint(dist) - transform.position).normalized;
        else
            zoomDir = transform.forward;

        Vector3 newPos = transform.position + zoomDir * scroll * zoomSpeed * Dt;
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

        _pitch = Mathf.LerpAngle(_pitch, _targetPitch, angleLerpSpeed * Dt);

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

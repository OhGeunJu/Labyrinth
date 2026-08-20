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

    [Header("비추기 (로스터 더블클릭)")]
    [Tooltip("잡아 줄 때 대상에서 이만큼까지 다가간다(m, 카메라~대상 직선거리). 이미 더 가까우면 " +
             "그대로 둔다 — 멀어지지는 않는다. 바닥(minHeight)에 닿으면 거기서 멈춘다.")]
    [Min(1f)] [SerializeField] float focusDistance = 15f;

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
    //
    // **다만 "화면 한가운데"가 곧 "가까이"는 아니다.** 위에서 내려다볼 때는 둘이 같지만,
    // 카메라를 **눕히면** 화면 한가운데 광선이 지면과 만나는 자리가 아득히 멀어진다 —
    // 높이 20 · 각도 5도면 **228m 앞**이다. 폰은 정확히 정중앙에 오는데 지평선의 점이라,
    // 잡아 준 것이 아니라 잃어버린 것처럼 보인다. 그래서 가운데에 놓은 뒤 **다가간다**.
    //
    // **높이는 결국 건드린다**(위 문단은 원래 "안 건드린다"였다). 수평으로만 당겨 봤더니
    // **높이가 곧 하한**이라 20m 위에 있으면 바로 위에서 봐도 20m 밖이고, 눕힌 채로는 대상이
    // 화면 아래로 밀려나기만 했다 — 가까이 가는 것은 **낮아지는 것**과 같은 일이다.
    //
    // 대신 **각도는 안 건드리고 시선을 따라서만** 움직인다. 그러면 대상이 화면 한가운데에
    // **그대로 머문 채** 커지기만 해서, 원래 걱정했던 *"어디로 튀었지"*가 안 생긴다. 이건
    // 휠 줌(`HandleZoom`)이 이미 하는 동작 그대로다 — 잡아 주기와 줌이 같은 규칙을 쓴다.
    public void FocusOn(Vector3 worldPoint)
    {
        // 카메라가 기울어 있으므로 지금 화면 한가운데가 가리키는 지면과의 **차이**만큼 민다.
        // 그래야 대상이 화면 정중앙에 온다(그냥 대상 좌표로 가면 화면 위쪽에 걸린다).
        Vector3 look = GroundUnderCenter(worldPoint.y);
        Vector3 delta = new Vector3(worldPoint.x - look.x, 0f, worldPoint.z - look.z);
        transform.position += delta;

        DollyIn(worldPoint);
    }

    // **멀면 시선을 따라 다가간다** — 대상이 화면 한가운데에 그대로 있으므로 커지기만 한다.
    //
    // 가까울 때는 **아무것도 안 한다**: 잡아 주기는 보여 주는 일이지 시점을 정해 주는 일이
    // 아니라, 이미 코앞인 것을 뒤로 물릴 이유가 없다.
    void DollyIn(Vector3 worldPoint)
    {
        Vector3 fwd  = transform.forward;
        float   dist = Vector3.Distance(transform.position, worldPoint);

        float step = dist - focusDistance;
        if (step <= 0f) return;

        // **바닥에서 멈춘다.** 눕힌 시점에서 다가가는 것은 곧 낮아지는 것이라, 안 막으면
        // 카메라가 지면을 뚫고 건물 속으로 들어간다. 한계는 휠 줌이 쓰는 것과 **같은
        // `minHeight`**다 — 잡아 주기만 더 내려갈 수 있으면 규칙이 두 벌이 된다.
        if (fwd.y < -0.001f)
            step = Mathf.Min(step, (transform.position.y - minHeight) / -fwd.y);

        if (step <= 0f) return;   // 이미 바닥이다 — 눕힌 채로는 여기가 가장 가깝다
        transform.position += fwd * step;
    }

    // 화면 한가운데 광선이 **대상 높이 평면**과 만나는 곳. 지형 콜라이더에 기대지 않는다
    // (지형이 없는 던전 씬에서도 동작해야 한다).
    //
    // **y=0으로 재면 안 된다** — 주석은 *"y=대상높이 평면"*이라고 적어 두고 실제로는 원점
    // 평면을 쓰고 있었다. 언덕 위에 선 폰은 그만큼 어긋난 자리에 잡힌다.
    Vector3 GroundUnderCenter(float y)
    {
        var cam = _cam != null ? _cam : GetComponent<Camera>();
        if (cam == null) return transform.position;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
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

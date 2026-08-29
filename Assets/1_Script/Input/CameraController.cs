using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 10f;

    // **높이에 비례해 빨라진다**(2026-08-26). 세계가 200m 한 장이던 시절에는 10m/s가 맞았다 —
    // 열아홉 초면 맵을 가로질렀으니까. **16,384m 세계에서는 27분**이 걸린다.
    //
    // 그렇다고 상수를 올리면 **가까이서 폰을 볼 때 손을 못 댄다** — 한 번 누르면 화면 밖으로
    // 날아간다. 높이가 곧 *"얼마나 넓게 보고 있나"*이므로 거기 비례시키면 **어느 배율에서도
    // 화면 기준으로는 같은 속도**로 느껴진다(RTS 카메라의 관례).
    [Tooltip("이 높이에서 moveSpeed 그대로. 위로 갈수록 비례해 빨라진다.")]
    [SerializeField] float speedRefHeight = 20f;

    [Tooltip("높이 배율의 천장. 없으면 최대 줌아웃에서 화면 밖으로 날아간다.")]
    [SerializeField] float maxSpeedMult = 30f;

    [Header("회전")]
    [SerializeField] float rotateSpeed = 3f;
    [SerializeField] float minPitch = 5f;
    [SerializeField] float maxPitch = 89f;

    [Header("줌 (커서 방향 이동)")]
    // **줌도 높이를 탄다** — 상수면 높은 데서 한 번 굴려도 거의 안 움직이고, 낮은 데서는 훅 꺼진다.
    [SerializeField] float zoomSpeed = 20f;
    [SerializeField] float minHeight = 3f;

    // **스트리밍 반경을 넘어가면 안 된다.** 지금 지형은 뷰어 둘레 `radius`청크(기본 1,280m)만
    // 서 있어서, 그보다 넓게 보면 **가장자리 너머가 통째로 비어 보인다.** 60도 화각에서
    // 높이 h가 대략 1.15h를 보여 주므로 600m면 ~690m — 반경 안에 든다.
    // 더 멀리 보고 싶으면 `TerrainStreamer.radius`를 같이 올릴 것.
    [Tooltip("최대 줌아웃 높이. TerrainStreamer의 반경보다 넓게 보면 가장자리가 빈다.")]
    [SerializeField] float maxHeight = 600f;

    // **높이는 지면 기준이다 — 절대 Y가 아니다**(2026-08-26).
    //
    // 필드맵이 평평하던 시절에는 지면이 Y=0이라 둘이 같았다. 바이옴이 지형을 정하면서
    // **산악 지역이 150m 넘게 솟는다** — 그러면 절대 Y로 자른 3~600이 그 산에서는
    // *"땅속 147m부터 산 위 450m까지"*가 되어, **가까이 가면 카메라가 산에 파묻히고
    // 꼭대기 위로는 조금밖에 못 올라간다.**
    //
    // 지면에서 재면 어느 고도에서도 **같은 배율 범위**가 나온다.
    float GroundBelow()
    {
        var map = MapManager.Instance;
        var p = transform.position;
        return map != null ? map.SampleGroundHeight(p.x, p.z) : 0f;
    }

    // 지면 위 높이를 범위 안으로 — 이동·줌·비추기가 **같은 한 곳**을 쓴다.
    void ClampHeight()
    {
        float g = GroundBelow();
        var p = transform.position;
        p.y = Mathf.Clamp(p.y, g + minHeight, g + maxHeight);
        transform.position = p;
    }

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
        Vector3 from = transform.position;

        // 카메라가 기울어 있으므로 지금 화면 한가운데가 가리키는 지면과의 **차이**만큼 민다.
        // 그래야 대상이 화면 정중앙에 온다(그냥 대상 좌표로 가면 화면 위쪽에 걸린다).
        Vector3 look = GroundUnderCenter(worldPoint.y);
        Vector3 delta = new Vector3(worldPoint.x - look.x, 0f, worldPoint.z - look.z);
        transform.position += delta;

        DollyIn(worldPoint);

        // ── 순간이동이 아니라 미끄러져 간다 (2026-08-29) ────────────────────
        //
        // **셈은 그대로 두고 결과만 트윈으로 옮긴다.** 도착점을 먼저 실제로 계산해 두고
        // (위의 두 줄과 `DollyIn`이 하던 그대로) 자리를 되돌린 뒤 거기까지 미끄러진다 —
        // 그러면 잡아 주기의 규칙(화면 정중앙 · 시선을 따라 다가감 · 지면에서 멈춤)을
        // **한 줄도 안 건드린다.**
        //
        // 순간이동하면 **어디로 갔는지를 눈이 못 좇는다** — 명부에서 폰을 눌렀을 때 화면이
        // 통째로 갈리면 그 폰이 화면 가운데 있다는 것을 다시 찾아 확인해야 한다.
        Vector3 to = transform.position;
        transform.position = from;

        DOTween.Kill(transform);
        transform.DOMove(to, focusTime)
                 .SetEase(Ease.OutCubic)
                 .SetUpdate(true)        // `GameTimeManager`가 `timeScale`을 만진다 — `Dt`와 같은 이유
                 .SetLink(gameObject);
    }

    [Tooltip("잡아 주기가 미끄러지는 시간(초). 배속과 무관한 실제 시간.")]
    [SerializeField] float focusTime = 0.35f;

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
        {
            // **손이 이긴다**(2026-08-29). 잡아 주기가 미끄러지는 중에 WASD를 누르면 트윈이
            // 매 프레임 자리를 도로 끌어당겨 **카메라가 안 움직이거나 부들거린다.** 조작이
            // 들어온 순간 연출은 포기한다 — 보여 주는 일이 조종을 이길 수는 없다.
            DOTween.Kill(transform);
            transform.position += dir.normalized * ScaledMoveSpeed * Dt;
        }

        ClampToWorld();

        // **옆으로 움직여도 지면을 뚫지 않는다** — 산비탈로 밀고 들어가면 지면이 올라오므로,
        // 줌에서만 자르면 카메라가 언덕 속으로 들어간다.
        ClampHeight();
    }

    // 지금 높이에서의 이동 속도. **지면 위 높이로 잰다** — 절대 Y로 재면 고원 위에서는
    // 코앞을 보고 있어도 초고속이 된다.
    float ScaledMoveSpeed => moveSpeed * HeightMult;

    float HeightMult => Mathf.Clamp((transform.position.y - GroundBelow()) / Mathf.Max(1f, speedRefHeight),
                                    1f, maxSpeedMult);

    // **세계 밖으로 못 나간다.** 나가면 지형 청크가 없어 아무것도 안 보이고, 돌아오는 길을
    // 눈으로 찾을 수가 없다(사방이 똑같이 비어 있다).
    void ClampToWorld()
    {
        var p = transform.position;
        p.x = Mathf.Clamp(p.x, 0f, TerrainGrid.WorldSize);
        p.z = Mathf.Clamp(p.z, 0f, TerrainGrid.WorldSize);
        transform.position = p;
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
        // **커서 아래 지면 높이의 평면을 쓴다** — Y=0으로 고정하면 산 위에서 스크롤할 때
        // 겨냥한 자리가 발밑이 아니라 **산 아래 허공**이 되어, 줌이 엉뚱한 데로 끌고 간다.
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, GroundBelow(), 0f));

        Vector3 zoomDir;
        if (groundPlane.Raycast(ray, out float dist))
            zoomDir = (ray.GetPoint(dist) - transform.position).normalized;
        else
            zoomDir = transform.forward;

        // 줌도 높이에 비례 — 높은 데서는 성큼, 낮은 데서는 조금씩.
        Vector3 newPos = transform.position + zoomDir * scroll * zoomSpeed * HeightMult * Dt;
        transform.position = newPos;
        ClampToWorld();
        ClampHeight();
    }

    // **밖에서 시점을 바꾸는 문**(2026-08-29). 채굴 지정이 이걸 쓴다 — 눕힌 시점에서는
    // 화면 사각형이 지면에서 **사다리꼴**이 되어, 무엇이 찍힐지를 눈으로 못 잰다.
    //
    // **PageUp이 하던 일을 그대로 부른다** — 새 경로를 만들면 *"버튼으로 눕힌 것과 키로 눕힌
    // 것"*이 다른 상태가 된다. 돌아올 각도는 부르는 쪽이 기억한다(여기서 들면 두 곳이
    // 겹쳐 눌렀을 때 어느 것으로 돌아갈지가 규칙이 된다).
    public float Pitch => _pitch;

    public void LerpToPitch(float pitch)
    {
        _targetPitch    = pitch;
        _lerpingToPitch = true;
    }

    public float TopDownPitch => topDownPitch;

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

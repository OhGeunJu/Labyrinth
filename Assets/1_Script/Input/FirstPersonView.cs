using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// **1인칭 — 고른 폰의 눈으로 본다**(2026-10-28, 사용자: *"천장을 보이게 하자는 건데 1인칭 카메라를 만들어서"*).
//
// 굴은 위에서 내려다보는 화면을 위해 지어졌다 — 벽 위를 잘라 내고(`RockCut`) 천장은 아예 없다. 눈높이로
// 내려오면 그 둘이 다 드러나므로 **이 모드가 켜진 동안만** 자르기를 끄고 천장을 켠다(`RockRenderer`).
//
// **보기만 바꾼다 — 조종은 그대로다.** 폰은 여전히 클릭으로 부린다(우클릭 이동은 `Camera.main`에서 광선을
// 쏘므로 눈높이에서도 같은 문이다). 둘러보기는 가운데 버튼 끌기 — 위에서 볼 때 돌리던 그 손이다.
//
// **카메라는 하나다** — 따로 세우면 `Camera.main`을 묻는 곳(스트리밍 · 안개 · 층 · 커서)이 전부 갈린다.
// 같은 카메라를 이것이 붙들고 있는 동안 `CameraController`는 손을 뗀다(`On`). 나갈 때는 들어오기 전 자리로
// 돌려놓고 폰을 비춘다 — 그동안 폰이 걸어간 만큼 따라간다.
[RequireComponent(typeof(Camera))]
public class FirstPersonView : MonoBehaviour
{
    // 켜져 있나 — 카메라 · 바위(자르기 · 천장)가 묻는다.
    public static bool On { get; private set; }

    // 켜지고 꺼질 때 — 천장이 듣는다.
    public static event System.Action<bool> Changed;

    [Tooltip("머리 관절에서 눈까지 올리는 높이(m). 관절은 목 언저리에 있다.")]
    [SerializeField] float eyeLift = 0.12f;

    // 치비 머리는 몸에 비해 크다 — 머리 속에 눈을 두면 제 머리 안쪽이 화면을 채운다. 그래서 제 몸은
    // **그리기만 끈다**(`forceRenderingOff`) — `enabled`·활성은 다른 축(시체 · 전용 모델 · 장비)이 쓰므로 안 만진다.
    [Tooltip("관절이 없는 몸(짐승 · 골렘)은 발에서 이만큼 위가 눈이다(m).")]
    [SerializeField] float fallbackEye = 1.4f;

    [SerializeField] float lookSpeed = 0.15f;   // 픽셀당 도(°)
    [SerializeField] float fov       = 70f;
    [SerializeField] float nearClip  = 0.05f;

    Camera     _cam;
    PawnMover  _pawn;
    Transform  _eye;
    float      _yaw, _pitch;
    bool       _looking;

    // 들어오기 전의 것 — 나갈 때 그대로 돌려준다.
    Vector3    _backPos;
    Quaternion _backRot;
    float      _backFov, _backNear;

    readonly List<Renderer> _hidden = new();
    readonly List<Renderer> _scratch = new();

    void Awake() => _cam = GetComponent<Camera>();

    void OnDisable() { if (On) Exit(false); }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame && !InputFocus.KeysBlocked())
        {
            if (On) Exit(true);
            else    TryEnter();
        }

        if (!On) return;

        // **폰이 사라지거나 쓰러지면 나온다** — 시체의 눈으로 남아 있을 이유가 없다.
        if (_pawn == null || !_pawn.isActiveAndEnabled
            || (_pawn.TryGetComponent(out UnitBrain brain) && brain.IsDead))
        {
            Exit(true);
            return;
        }

        Look();
    }

    // **움직임이 끝난 뒤에 따라간다** — `Update`에서 붙으면 폰이 이번 프레임에 걸은 만큼 한 박자 늦어 흔들린다.
    void LateUpdate()
    {
        if (!On || _pawn == null) return;
        transform.SetPositionAndRotation(EyePoint(), Quaternion.Euler(_pitch, _yaw, 0f));
        HideBody();
    }

    void TryEnter()
    {
        var sel = FindFirstObjectByType<SelectionManager>();
        if (sel == null || sel.Selected.Count == 0)
        {
            Debug.Log("[1인칭] 폰을 하나 고르고 F를 누를 것.");
            return;
        }

        _pawn = sel.Selected[0];
        _eye  = _pawn.TryGetComponent(out PawnRig rig) && rig.Head != null ? rig.Head : null;
        if (_eye == null)
        {
            rig = _pawn.GetComponentInChildren<PawnRig>();
            if (rig != null) _eye = rig.Head;
        }

        _backPos  = transform.position;
        _backRot  = transform.rotation;
        _backFov  = _cam.fieldOfView;
        _backNear = _cam.nearClipPlane;

        // 폰이 보는 쪽으로 시작한다 — 위에서 보던 각을 물려받으면 발밑부터 보인다.
        _yaw   = _pawn.transform.eulerAngles.y;
        _pitch = 0f;

        _cam.fieldOfView   = fov;
        _cam.nearClipPlane = nearClip;

        On = true;
        Changed?.Invoke(true);
    }

    void Exit(bool refocus)
    {
        ShowBody();

        On = false;
        _looking = false;

        _cam.fieldOfView   = _backFov;
        _cam.nearClipPlane = _backNear;
        transform.SetPositionAndRotation(_backPos, _backRot);

        Changed?.Invoke(false);

        // 들어간 동안 폰이 걸어갔으면 그 자리를 비춘다(`FocusOn`이 미끄러져 간다).
        if (refocus && _pawn != null && TryGetComponent(out CameraController cc))
            cc.FocusOn(_pawn.transform.position);

        _pawn = null;
        _eye  = null;
    }

    Vector3 EyePoint()
    {
        if (_eye != null) return _eye.position + Vector3.up * eyeLift;
        return _pawn.transform.position + Vector3.up * fallbackEye;
    }

    void Look()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.middleButton.wasPressedThisFrame)  _looking = true;
        if (mouse.middleButton.wasReleasedThisFrame) _looking = false;
        if (!_looking) return;

        // **델타는 픽셀이라 시간을 안 곱한다** — 곱하면 프레임이 튈 때 시선도 튄다.
        Vector2 d = mouse.delta.ReadValue();
        _yaw  += d.x * lookSpeed;
        _pitch = Mathf.Clamp(_pitch - d.y * lookSpeed, -85f, 85f);
    }

    // 장비를 갈아 끼우면 렌더러가 새로 생긴다 — 매 프레임 다시 모은다(폰 하나라 싸다).
    void HideBody()
    {
        _scratch.Clear();
        _pawn.GetComponentsInChildren(true, _scratch);
        for (int i = 0; i < _scratch.Count; i++)
        {
            var r = _scratch[i];
            if (r.forceRenderingOff) continue;
            r.forceRenderingOff = true;
            _hidden.Add(r);
        }
    }

    void ShowBody()
    {
        for (int i = 0; i < _hidden.Count; i++)
            if (_hidden[i] != null) _hidden[i].forceRenderingOff = false;
        _hidden.Clear();
    }
}

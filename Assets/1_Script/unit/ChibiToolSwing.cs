using UnityEngine;

// **치비의 곡괭이질 — 받은 클립 대신 손 공을 직접 움직인다**(2026-09-19).
//
// (사용자: *"캐는 모션을 원래 있던 거 말고 그냥 새로 만드는 게 좋겠는데 머리 위로 손을 들어올렸다가
//  찍는 게 좋을 거 같아"* — 그림 두 장: ① 두 손이 머리 위, 곡괭이는 위로 들린다 ·
//  ② 두 손이 가슴 앞, 자루가 앞으로 뻗어 날이 땅을 찍는다.)
//
// 받은 클립은 **사람 팔**을 움직인다. 치비에는 팔이 안 보이고 **손 공만 떠 있어서**, 팔을 돌려 손을
// 옮기는 셈이 치비 비례에서는 손이 엉뚱한 데로 간다(두 손이 안 모인다 · 몸이 통째로 젖혀진다).
// 떠 있는 손은 **손 자리를 바로 적는 편이 정확하다** — 림월드가 그렇게 그린다.
//
// - **애니메이터 뒤 · `PawnProps` 앞에 돈다**(실행 순서 -5 · `ChibiPoseDamp`는 -10). 도구는
//   `IToolPose`로 이 자세를 받아 간다 — 손만 옮기고 곡괭이는 옛 셈(팔뚝 방향)으로 두면 둘이 따로 논다.
// - **흔드는 동안 몸은 클립이 아니라 곡괭이를 따른다** — 들 때 젖히고 찍을 때 숙이며 주저앉는다. 머리는 클립 대신
//   **한 박자 늦게 끄덕인다**(목을 클립에 맡기면 도리질을 치고, 몸에 딱 붙이면 몸통의 일부처럼 보인다).
// - **자세는 폰 기준(발밑 원점 · +z 앞)이다** — 폰이 돌면 같이 돈다(`PawnMover.FaceTowards`가 뿌리를 돌린다).
// - **시간은 게임 시간이다**(`Time.deltaTime`) — 멈추면 멈추고 배속이면 빨라진다(일 자체가 그렇다).
// - **지금은 채굴뿐이다** — 벌목은 받은 클립 그대로다. 늘리려면 `_active`를 가르는 줄에 동작을 더하고
//   두 자세를 동작마다 갈라 둔다.
// - 자세 손잡이: `RaiseFromNeck`·`RaisePitch`(그림 ①) · `StrikeAt`·`StrikePitch`(그림 ②) · 박자는 `Period`.
[DefaultExecutionOrder(-5)]
public class ChibiToolSwing : MonoBehaviour, IToolPose
{
    [Tooltip("한 번 찍는 데 걸리는 시간(초).")]
    public float Period = 1.2f;

    // 두 자세 — 자루 밑동(아래 손)의 자리와 자루가 뻗는 쪽의 기울기(도, + = 위).
    // 머리 위 높이는 **목에서** 잰다(머리형마다 키가 다르지만 목은 관절이 답한다).
    // **머리 앞으로 뺀다**(2026-09-19, 사용자: *"머리에 손이 자꾸 겹치는 것도 좀 거슬리긴 하는데"*) — 머리 위에 두면
    // 내려다보는 카메라에서 손 공이 **얼굴을 덮는다.** 머리 반지름(≈0.37) + 손 반지름(0.15)보다 앞(0.58)이면
    // 위에서 봐도 머리 둘레 밖이다.
    static readonly Vector3 RaiseFromNeck = new(0.12f, 0.55f, 0.58f);   // 얼굴 앞 위 — 그림 ①
    // **들었을 때 곡괭이는 위로 선다**(2026-09-19, 사용자: *"손을 높이 들었을 때는 곡괭이도 지금 위에 높이
    // 들려있다가 찍으면서 아래로 내려가야하는데 곡괭이가 계속 아래를 향해있어"*). 처음엔 -30°(앞으로 숙임)라
    // 손만 오르내리고 자루는 늘 땅을 봤다 — 찍는 힘은 **자루가 그리는 호**에서 읽힌다.
    const float RaisePitch  = 70f;
    static readonly Vector3 StrikeAt      = new(0.10f, 0.55f,  0.42f);   // 가슴 앞 — 그림 ②
    const float StrikePitch = -15f;
    const float HandGap     = 0.14f;

    // **몸이 따라 움직인다**(2026-09-19, 사용자: *"머리는 엄청 흔들어대는데 몸은 아예 안 움직이네"*). 전에는 척추만
    // 꼿꼿이 묶고 **목은 풀어 둬서** 받은 클립의 고갯짓이 그대로 남았다 — 몸통은 멈췄는데 머리만 흔들렸다.
    // 이제 머리는 몸에 붙이고(기준 자세), 몸이 곡괭이를 따라 젖혔다 숙인다(도, + = 앞으로).
    const float RaiseLean  = -6f;    // 들 때 살짝 젖힌다
    const float StrikeLean = 12f;    // 찍을 때 숙인다
    const float StrikeDip  = 0.04f;  // 찍을 때 주저앉는 깊이(m)

    // **머리는 몸보다 한 박자 늦게 끄덕인다**(2026-09-19, 사용자: *"머리도 조금은 움직여야지 너무 몸이랑
    // 고정되어있잖아"*). 몸에 딱 붙이면 머리가 몸통의 일부처럼 보이고, 클립에 맡기면 도리질을 친다.
    // 곡괭이를 따라 들 때 올려다보고 찍을 때 내려다보되, **늦게 따라가는 것**이 머리로 읽히게 한다.
    const float HeadRaise  = -8f;    // 들 때 올려다본다
    const float HeadStrike = 10f;    // 찍을 때 내려다본다
    const float HeadLag    = 10f;    // 따라가는 빠르기(클수록 몸에 붙는다)   // 위 손이 자루를 따라 얼마나 위를 쥐나(두 공이 겹쳐 보인다)

    UnitBrain  _brain;
    PawnRig    _rig;
    Vector3    _hipsPos;
    Quaternion _hipsRot, _spineRot, _headRot;
    float      _phase, _headK = 1f;

    bool    _active;
    Vector3 _grip, _handle, _edge;

    void Awake()
    {
        _brain = GetComponent<UnitBrain>();
        _rig   = GetComponentInChildren<PawnRig>(true);
        if (_rig == null || _rig.HandR == null || _rig.HandL == null || _rig.Head == null) { enabled = false; return; }
        if (_rig.Hips  != null) { _hipsPos = _rig.Hips.localPosition; _hipsRot = _rig.Hips.localRotation; }
        if (_rig.Spine != null) _spineRot = _rig.Spine.localRotation;
        _headRot = _rig.Head.localRotation;
    }

    void LateUpdate()
    {
        var bb = _brain != null ? _brain.Blackboard : null;
        _active = bb != null && bb.Working == WorkAnim.Mining;
        if (!_active) { _phase = 0f; _headK = 1f; return; }

        _phase = (_phase + Time.deltaTime / Mathf.Max(0.1f, Period)) % 1f;

        // 박자: 천천히 들어 올리고 → 잠깐 멈추고 → 빠르게 찍고 → 찍은 채 잠깐.
        float k;   // 0 = 찍은 자세 · 1 = 든 자세
        float p = _phase;
        if      (p < 0.45f) k = Mathf.SmoothStep(0f, 1f, p / 0.45f);
        else if (p < 0.55f) k = 1f;
        else if (p < 0.68f) { float t = (p - 0.55f) / 0.13f; k = 1f - t * t; }   // 가속하며 내려친다
        else                k = 0f;

        // 몸: 클립의 몸짓은 버리고 곡괭이를 따라 젖혔다 숙인다 · 머리는 몸에 붙인다.
        if (_rig.Hips != null)
        {
            _rig.Hips.localPosition = _hipsPos + Vector3.down * (StrikeDip * (1f - k));
            _rig.Hips.localRotation = _hipsRot;
        }
        if (_rig.Spine != null)
        {
            var axis = _rig.Spine.parent != null ? _rig.Spine.parent.InverseTransformDirection(transform.right) : Vector3.right;
            _rig.Spine.localRotation = Quaternion.AngleAxis(Mathf.Lerp(StrikeLean, RaiseLean, k), axis) * _spineRot;
        }
        _headK = Mathf.Lerp(_headK, k, 1f - Mathf.Exp(-HeadLag * Time.deltaTime));
        var headAxis = _rig.Head.parent != null ? _rig.Head.parent.InverseTransformDirection(transform.right) : Vector3.right;
        _rig.Head.localRotation = Quaternion.AngleAxis(Mathf.Lerp(HeadStrike, HeadRaise, _headK), headAxis) * _headRot;

        float neckY = transform.InverseTransformPoint(_rig.Head.position).y;
        var raise  = new Vector3(RaiseFromNeck.x, neckY + RaiseFromNeck.y, RaiseFromNeck.z);

        // 손은 어깨 높이를 중심으로 **호를 그리며** 오간다(곧게 옮기면 머리를 뚫고 지나간다).
        var pivot = new Vector3(RaiseFromNeck.x, neckY - 0.1f, 0f);   // 어깨 높이 · 몸 한가운데
        Vector3 a = StrikeAt - pivot, b = raise - pivot;
        var butt  = pivot + Vector3.Slerp(a, b, k);

        float pitch = Mathf.Lerp(StrikePitch, RaisePitch, k) * Mathf.Deg2Rad;
        var handleL = new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch));
        // 날은 **자루와 같이 돈다** — 휘두르는 면 안에서 자루에 직교한 "앞·아래" 쪽. 들었을 때는 앞을,
        // 찍을 때는 땅을 본다. (늘 *아래*로 두면 자루가 설 때 아래가 자루와 겹쳐 날이 뒤집힌다.)
        var edgeL   = new Vector3(0f, -Mathf.Cos(pitch), Mathf.Sin(pitch));

        _grip   = transform.TransformPoint(butt);
        _handle = transform.TransformDirection(handleL).normalized;
        _edge   = transform.TransformDirection(edgeL).normalized;

        _rig.HandL.position = _grip;
        _rig.HandR.position = _grip + _handle * HandGap;
    }

    // `PawnProps`가 묻는다 — 흔드는 중이면 곡괭이를 이 자세로 둔다.
    public bool TryGetToolPose(out Vector3 grip, out Vector3 handle, out Vector3 edge)
    {
        grip = _grip; handle = _handle; edge = _edge;
        return _active && enabled;
    }
}

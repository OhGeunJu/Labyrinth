using UnityEngine;

// **치비 폰은 몸통이 크게 안 흔들린다 — 팔만 클립을 따른다**(2026-09-19).
//
// (사용자: *"캘때 몸이 너무 뒤로 빠지고"*.) 받은 휴머노이드 클립은 **사람 비례**로 만든 것이라, 곡괭이를
// 들어 올리며 허리를 젖히고 엉덩이를 뒤로 뺀다. 사람 몸에서는 그게 자연스럽지만 치비는 **머리와 몸통
// 전부가 척추 관절 하나에 얹혀** 있어서, 같은 각이 몸 전체를 통째로 뒤로 눕힌다.
//
// 그래서 애니메이터가 뼈를 움직인 **뒤에** 몸통의 움직임만 덜어 낸다 — 엉덩이는 제자리(수평 이동을 버린다),
// 엉덩이·척추의 회전은 `BodyFollow`만큼만 따른다. 팔·손은 그대로라 휘두르는 동작은 남는다.
//
// - **`PawnProps`보다 먼저 돈다**(실행 순서 -10) — 곡괭이는 손 자리에서 내므로, 덜어 내기 전의 손을
//   보면 도구가 몸과 따로 논다.
// - **누움과 다투지 않는다** — 누움은 루트 회전이고(`PawnMover`) 관절은 대기 자세로 얼어 있어 덜어 낼 것이 없다.
// - **치비에만 붙는다**(`ChibiPawnCreator`) — 블록 인형·고블린은 몸이 관절마다 나뉘어 있어 클립 그대로가 맞다.
[DefaultExecutionOrder(-10)]
public class ChibiPoseDamp : MonoBehaviour
{
    [Tooltip("엉덩이·척추 회전을 클립에서 얼마나 따르나(0 = 꼿꼿이 · 1 = 클립 그대로).")]
    [Range(0f, 1f)] public float BodyFollow = 0.3f;   // 머리에도 같이 건다

    Transform  _hips, _spine, _head;
    Vector3    _hipsPos;
    Quaternion _hipsRot, _spineRot, _headRot;
    bool       _ready;

    void Awake() => Capture();

    // **기준 자세를 잡는다 — 애니메이터가 돌기 전에 한 번**(돈 뒤에 잡으면 클립의 자세가 기준이 된다).
    // 판에서는 `Awake`가 부르고, 플레이 밖에서 자세를 찍는 도구(`PawnPoseSheet`)는 세운 직후 직접 부른다.
    public void Capture()
    {
        if (_ready) return;
        var rig = GetComponentInChildren<PawnRig>(true);
        if (rig == null || rig.Hips == null || rig.Spine == null) { enabled = false; return; }
        _hips  = rig.Hips;  _hipsPos  = _hips.localPosition; _hipsRot = _hips.localRotation;
        _spine = rig.Spine; _spineRot = _spine.localRotation;
        _head  = rig.Head;  if (_head != null) _headRot = _head.localRotation;
        _ready = true;
    }

    void LateUpdate() => Apply();

    // 애니메이터가 뼈를 움직인 **뒤에** 부른다 — 판에서는 `LateUpdate`, 자세 찍기 도구는 `Animator.Update` 다음.
    public void Apply()
    {
        if (!_ready) return;
        var p = _hips.localPosition;
        _hips.localPosition = new Vector3(_hipsPos.x, p.y, _hipsPos.z);   // 높이(쭈그리기)는 남긴다
        _hips.localRotation  = Quaternion.Slerp(_hipsRot,  _hips.localRotation,  BodyFollow);
        _spine.localRotation = Quaternion.Slerp(_spineRot, _spine.localRotation, BodyFollow);
        // **머리도 같이 덜어 낸다** — 치비 머리는 몸통만큼 크다. 몸만 묶고 목을 풀면 고갯짓만 남아 머리가 흔들린다.
        if (_head != null) _head.localRotation = Quaternion.Slerp(_headRot, _head.localRotation, BodyFollow);
    }
}

using UnityEngine;

// **손에 무엇을 보여 주나.** 일하는 중이면 그 동작의 도구, 싸우거나 소집됐으면 든 무기(와 방패),
// 아니면 빈손.
//
// **판정을 안 만든다** — 일하는지는 블랙보드의 `Working`(일감이 적는다), 무기는 `Equipment`,
// 싸우는지는 `CombatHandler`·소집이 답한다. 여기서 제 판단을 더하면 *화면은 곡괭이인데 판정은
// 검*인 폰이 생긴다(`PawnAnimator`와 같은 규칙).
//
// **손은 아바타가 답한다** — 휴머노이드면 `GetBoneTransform`(우리 블록 관절도, 골렘도 같은 문),
// 아니면 `PawnRig`. 네발은 손이 없다.
//
// 무엇을 들지는 0.2초에 한 번 본다(초에 몇 번 바뀌면 많은 것이다). **두 손 도구의 자세만 매
// 프레임** — 애니메이터가 뼈를 움직인 뒤(`LateUpdate`) 양손에서 자루 방향을 낸다(`HandProp` 참고).
// **두 손 도구의 자세를 손 동작이 직접 줄 때**(치비의 곡괭이질 — `ChibiToolSwing`). 참을 돌려주는 동안은
// 팔뚝에서 자세를 내지 않고 이 값을 쓴다 — 손은 그쪽이 옮겼으니, 여기서 다시 셈하면 손과 자루가 갈린다.
public interface IToolPose
{
    // grip = 자루 밑동 자리 · handle = 밑동에서 머리로 · edge = 날이 향하는 쪽(월드)
    bool TryGetToolPose(out Vector3 grip, out Vector3 handle, out Vector3 edge);
}

[RequireComponent(typeof(PawnMover))]
public class PawnProps : MonoBehaviour
{
    const float Interval = 0.2f;
    const float TwoHandSpan = 0.3f;   // 두 손이 이보다 벌어지면 한 손(오른손)이 쥔다

    Animator      _anim;
    UnitBrain     _brain;
    Equipment     _equip;
    CombatHandler _combat;
    IToolPose     _pose;
    Transform     _handR, _handL, _chest, _elbowR;
    bool          _handsResolved;

    GameObject _right, _left;
    GameObject _rightSrc, _leftSrc;   // 무엇에서 세웠나 — 같으면 안 갈아 끼운다
    HandProp   _rightGrip;
    float      _rightButt;            // 자루 밑동이 모델 로컬에서 `HeadAxis`로 얼마나 떨어졌나
    float      _next;

    void Awake()
    {
        _anim   = GetComponentInChildren<Animator>(true);
        _brain  = GetComponent<UnitBrain>();
        _equip  = GetComponent<Equipment>();
        _combat = GetComponent<CombatHandler>();
        _pose   = GetComponent<IToolPose>();
    }

    void OnDisable()
    {
        Show(ref _right, ref _rightSrc, null, _handR);
        Show(ref _left,  ref _leftSrc,  null, _handL);
        _rightGrip = null;
    }

    void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + Interval;

        if (!_handsResolved) ResolveHands();
        if (_handR == null) return;

        var set = HandPropSet.Instance;
        if (set == null) return;

        var bb = _brain != null ? _brain.Blackboard : null;
        GameObject wantR = null, wantL = null;

        if (bb != null && bb.Working != WorkAnim.None)
            wantR = set.ToolPrefab(bb.Working);
        else if (_equip != null && (IsDrafted || (_combat != null && _combat.IsInCombat)))
        {
            if (_equip.MainHand != null) wantR = set.WeaponPrefab(_equip.MainHand.Type);
            if (_equip.OffHand  != null) wantL = set.WeaponPrefab(_equip.OffHand.Type);
        }

        if (Show(ref _right, ref _rightSrc, wantR, _handR))
        {
            _rightGrip = _right != null ? _right.GetComponent<HandProp>() : null;
            _rightButt = _right != null && _rightGrip != null
                       ? ButtAlong(_right.transform, _rightGrip.HeadAxis) : 0f;
        }
        Show(ref _left, ref _leftSrc, wantL, _handL);
    }

    // 두 손 도구: **자루는 팔의 연장이다.** 밑동을 두 손 사이에 두고 팔뚝이 뻗는 쪽으로 머리를
    // 보낸다. 날은 몸(가슴)에서 먼 쪽.
    //
    // **자루 방향을 두 손을 잇는 선으로 내지 않는다**(2026-09-10에 바꿨다 — 사용자:
    // *"곡괭이가 돌을 쳐야하는데 걍 두손으로 들고만 있는 느낌이야"*). 클립은 원래 뼈대에서 두 손을
    // 자루 위에 모아 쥐지만, **우리 블록 뼈대는 팔 길이가 달라 손이 그만큼 안 모인다** — 벌어진 두
    // 손을 이으면 자루가 **몸을 가로질러 눕고**, 아무리 휘둘러도 머리가 땅에 안 닿는다.
    //
    // 팔뚝(팔꿈치 → 손)은 그 문제가 없다. 내리찍으면 팔뚝이 아래를 향하므로 곡괭이도 같이 내려가
    // 바위를 친다. **쥐는 자리는 자루의 밑동이지 모델의 피벗이 아니다** — 피벗을 손에 두면 팩마다
    // 피벗 규약이 달라 머리가 폰 머리 위로 솟는다(밑동은 메시 경계에서 낸다, `ButtAlong`).
    void LateUpdate()
    {
        if (_right == null || _rightGrip == null || !_rightGrip.TwoHanded) return;

        if (_pose != null && _pose.TryGetToolPose(out var pGrip, out var pHandle, out var pEdge))
        {
            Place(pGrip, pHandle, pEdge);
            return;
        }

        // 자루 방향 — 팔뚝의 연장. 팔꿈치가 없으면(블록 뼈대가 아닌 경우) 두 손을 잇는다.
        Vector3 handle = _elbowR != null ? _handR.position - _elbowR.position
                       : _handL != null  ? _handR.position - _handL.position
                       : transform.forward;
        if (handle.sqrMagnitude < 1e-4f) return;
        handle.Normalize();

        // 날은 자루에 직교하면서 가슴에서 멀어지는 쪽. 한 줄이면 폰의 앞.
        Vector3 chest = _chest != null ? _chest.position : transform.position + Vector3.up;
        Vector3 away  = Vector3.ProjectOnPlane(_handR.position - chest, handle);
        if (away.sqrMagnitude < 1e-4f) away = Vector3.ProjectOnPlane(transform.forward, handle);
        if (away.sqrMagnitude < 1e-4f) return;

        // 밑동을 **두 손 사이**에 둔다 — 둘 다 자루를 쥔 것처럼 보이고, 머리는 팔이 뻗는 쪽이다.
        // **다만 두 손이 자루 한 뼘 안에 모일 때만이다**(2026-09-19, 사용자: *"손으로 곡괭이를 잡지도
        // 않아"*). 치비는 두 손이 넓은 몸통 양옆에 떠 있어 클립이 모아도 안 모이고, 그 한가운데는
        // **몸 앞의 허공**이다. 그때는 오른손이 쥔다 — 떠 있는 손 공 하나가 자루를 잡은 그림이 림월드의 그것이다.
        bool together = _handL != null && (_handR.position - _handL.position).sqrMagnitude <= TwoHandSpan * TwoHandSpan;
        Place(together ? (_handR.position + _handL.position) * 0.5f : _handR.position, handle, away.normalized);
    }

    // 자루 밑동을 `grip`에, 머리를 `handle` 쪽으로, 날을 `edge` 쪽으로.
    void Place(Vector3 grip, Vector3 handle, Vector3 edge)
    {
        // 도구 로컬 (EdgeAxis, HeadAxis) → 월드 (edge, handle).
        var world = Quaternion.LookRotation(edge, handle);
        var local = Quaternion.LookRotation(_rightGrip.EdgeAxis, _rightGrip.HeadAxis);
        var rot   = world * Quaternion.Inverse(local);

        Vector3 buttLocal = _rightGrip.HeadAxis.normalized * _rightButt;

        _right.transform.rotation = rot;
        _right.transform.position = grip + rot * (_rightGrip.GripOffset - buttLocal);
    }

    // 자루 밑동이 `HeadAxis` 축으로 얼마나 떨어져 있나 — **메시 경계에서 낸다.** 피벗이 밑동이든
    // 한복판이든 머리 쪽이든 같은 답이 나오므로, 팩마다 피벗 규약을 외울 필요가 없다.
    static float ButtAlong(Transform root, Vector3 axis)
    {
        axis = axis.normalized;
        float min = float.PositiveInfinity;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;

            var b = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                         (i & 2) == 0 ? b.min.y : b.max.y,
                                         (i & 4) == 0 ? b.min.z : b.max.z);
                var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                min = Mathf.Min(min, Vector3.Dot(p, axis));
            }
        }
        return float.IsInfinity(min) ? 0f : min;
    }

    bool IsDrafted => _brain != null && _brain.IsDrafted;

    void ResolveHands()
    {
        _handsResolved = true;
        if (_anim != null && _anim.isHuman)
        {
            _handR  = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            _handL  = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
            _elbowR = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _chest  = _anim.GetBoneTransform(HumanBodyBones.Chest) ?? _anim.GetBoneTransform(HumanBodyBones.Spine);
            return;
        }
        var rig = GetComponentInChildren<PawnRig>(true);
        if (rig != null) { _handR = rig.HandR; _handL = rig.HandL; _elbowR = rig.LowerArmR; _chest = rig.Spine; }
    }

    // 돌려주는 것은 *갈아 끼웠나*.
    static bool Show(ref GameObject current, ref GameObject source, GameObject prefab, Transform hand)
    {
        if (source == prefab) return false;
        if (current != null) Destroy(current);
        current = null;
        source  = prefab;
        if (prefab == null || hand == null) return true;

        current = Instantiate(prefab, hand);
        var grip = current.GetComponent<HandProp>();
        current.transform.localPosition = grip != null && !grip.TwoHanded ? grip.GripOffset : Vector3.zero;
        current.transform.localRotation = grip != null && !grip.TwoHanded ? Quaternion.Euler(grip.GripEuler) : Quaternion.identity;
        current.transform.localScale    = Vector3.one;

        // 커서 레이캐스트에 안 잡히게 — 폰을 고르려다 손에 든 검이 잡힌다(`PawnMark`와 같은 규칙).
        foreach (var c in current.GetComponentsInChildren<Collider>(true)) Destroy(c);
        SetLayer(current.transform, hand.gameObject.layer);
        return true;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }
}

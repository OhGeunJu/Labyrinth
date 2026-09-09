using UnityEngine;

// **폰의 상태를 애니메이터에 옮긴다.** 걷나(`Speed`) · 휘두르나(`Attack`) · 누웠나(`Lying`).
//
// 애니메이터 자체는 `Model`에 있고 컨트롤러·클립은 에셋이다(`Assets/Controller/`). 이 컴포넌트가
// 하는 일은 **판정을 안 만드는 것**이다 — 걷는지는 실제 변위로, 휘두르는지는 `CombatHandler`의
// `OnStrike`로, 누웠는지는 `PawnMover`·블랙보드로 안다. 여기서 제 판단을 하나라도 더하면 *화면은
// 걷는데 판정은 서 있는* 폰이 생긴다.
//
// **속도는 m/s 그대로 넣는다.** 걷기 상태가 `Speed`로 재생 속도를 곱하므로(4m/s에서 1배)
// 절뚝이거나 짐이 무거워 느려지면 걸음도 그만큼 느려진다 — 따로 적을 것이 없다.
//
// **시체·업힘·기절·잠은 전부 "누움"이다.** 자세(뒤집힌 회전)는 `PawnMover`가 루트에서 잡고,
// 여기서는 관절만 쉬게 한다 — 안 그러면 누운 채로 숨 쉬듯 몸통이 들썩인다.
[RequireComponent(typeof(PawnMover))]
public class PawnAnimator : MonoBehaviour
{
    static readonly int SpeedId  = Animator.StringToHash("Speed");
    static readonly int AttackId = Animator.StringToHash("Attack");
    static readonly int LyingId  = Animator.StringToHash("Lying");
    static readonly int WorkId   = Animator.StringToHash("Work");

    // 네발 컨트롤러에는 `Work`가 없다 — 없는 매개변수를 넣으면 유니티가 매 프레임 경고한다.
    bool _hasWork;

    Animator      _anim;
    PawnMover     _mover;
    UnitBrain     _brain;
    CombatHandler _combat;
    Vector3       _last;
    float         _speed;

    void Awake()
    {
        _anim   = GetComponentInChildren<Animator>(true);
        _mover  = GetComponent<PawnMover>();
        _brain  = GetComponent<UnitBrain>();
        _combat = GetComponent<CombatHandler>();
        _last   = transform.position;

        if (_anim != null)
            foreach (var p in _anim.parameters)
                if (p.nameHash == WorkId) { _hasWork = true; break; }
    }

    void OnEnable()  { if (_combat != null) _combat.OnStrike += OnStrike; }
    void OnDisable() { if (_combat != null) _combat.OnStrike -= OnStrike; }

    void OnStrike()
    {
        if (_anim != null && _anim.isActiveAndEnabled) _anim.SetTrigger(AttackId);
    }

    void Update()
    {
        if (_anim == null || !_anim.isActiveAndEnabled) return;

        var p = transform.position;
        var d = p - _last;
        d.y   = 0f;
        _last = p;

        // 한 프레임 변위로 재되 조금 매끄럽게 — 배속에서 프레임이 튀면 값이 요동친다.
        float raw = Time.deltaTime > 0.0001f ? d.magnitude / Time.deltaTime : 0f;
        _speed = Mathf.Lerp(_speed, raw, 1f - Mathf.Exp(-12f * Time.deltaTime));

        var  bb    = _brain != null ? _brain.Blackboard : null;
        bool lying = !_mover.enabled                       // 시체(`BecomeCorpse`가 끈다)
                  || _mover.IsSleeping
                  || (bb != null && (bb.IsKnockedOut || bb.CarriedBy != null));

        _anim.SetBool (LyingId, lying);
        _anim.SetFloat(SpeedId, lying ? 0f : _speed);
        if (_hasWork) _anim.SetInteger(WorkId, lying || bb == null ? 0 : (int)bb.Working);
    }
}

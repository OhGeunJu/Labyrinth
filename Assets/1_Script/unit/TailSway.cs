using UnityEngine;

// **꼬리 마디 흔들기**(2026-11-18 — 괴물쥐: 사용자 *"꼬리에 나중에 애니메이션이 들어갈 거 같은데 알아서"*).
//
// 네발 클립(`PawnRigBuilder` — `Quadruped_*`)은 꼬리 뿌리 관절(`Body/Tail`) 하나만 돌린다. 긴 꼬리는 그 아래에 마디 사슬
// (`Tail/Tail1/Tail2/…` — `ChibiPawnCreator.Quad`가 세운다)이 있고, 여기서 **마디마다 늦게 따라오는 좌우 물결**을 얹는다.
//   - 쉬는 자세는 좌우 대칭(미리보기에서 곧게 뻗는다) — 휘는 것은 전부 여기 몫.
//   - 걸으면 빠르고 크게, 서 있으면 느리고 작게(움직임은 제 자리 변화로 잰다 — 이동기에 묻지 않는다).
//   - 누워 있으면(애니메이터 `Lying` — 기절 · 죽음) 잦아든다.
//   - 화면 밖이면 안 돈다(애니메이터처럼).
// 회전은 쉬는 자세 위에 매 프레임 새로 적는다(쌓지 않는다) · 위상은 델타를 곱해 나아간다(배속에서도 같은 박자).
public class TailSway : MonoBehaviour
{
    [Tooltip("뿌리에서 끝으로 — 첫 마디는 클립이 돌리는 `Tail`의 자식부터.")]
    public Transform[] Links;
    [SerializeField] float idleAmplitude = 10f;     // 끝 마디의 최대 각(도) — 뿌리 쪽은 줄어든다
    [SerializeField] float moveAmplitude = 20f;
    [SerializeField] float idleFrequency = 0.7f;    // 한 번 흔드는 데(Hz)
    [SerializeField] float moveFrequency = 1.8f;
    [SerializeField] float lag = 0.6f;              // 마디마다 늦는 위상(라디안)

    static readonly int LyingId = Animator.StringToHash("Lying");

    Quaternion[] _rest;
    float _phase, _moving, _alive = 1f;
    Vector3 _lastPos;
    Renderer _renderer;
    Animator _anim;

    void Awake()
    {
        if (Links == null) Links = new Transform[0];
        _rest = new Quaternion[Links.Length];
        for (int i = 0; i < Links.Length; i++) _rest[i] = Links[i] != null ? Links[i].localRotation : Quaternion.identity;
        _lastPos = transform.position;
        _renderer = GetComponentInChildren<Renderer>();
        _anim = GetComponentInChildren<Animator>();
    }

    void LateUpdate()
    {
        if (Links.Length == 0) return;
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        if (dt <= 0f) return;
        if (_renderer != null && !_renderer.isVisible) { _lastPos = pos; return; }

        float speed = (pos - _lastPos).magnitude / dt;
        _lastPos = pos;
        _moving = Mathf.MoveTowards(_moving, speed > 0.2f ? 1f : 0f, dt * 3f);
        bool lying = _anim != null && _anim.isActiveAndEnabled && _anim.runtimeAnimatorController != null && _anim.GetBool(LyingId);
        _alive = Mathf.MoveTowards(_alive, lying ? 0f : 1f, dt * 2f);

        float freq = Mathf.Lerp(idleFrequency, moveFrequency, _moving);
        float amp = Mathf.Lerp(idleAmplitude, moveAmplitude, _moving) * _alive;
        _phase = Mathf.Repeat(_phase + dt * freq * Mathf.PI * 2f, Mathf.PI * 2f);

        for (int i = 0; i < Links.Length; i++)
        {
            if (Links[i] == null) continue;
            float w = (i + 1f) / Links.Length;
            Links[i].localRotation = _rest[i] * Quaternion.Euler(0f, Mathf.Sin(_phase - i * lag) * amp * w, 0f);
        }
    }
}

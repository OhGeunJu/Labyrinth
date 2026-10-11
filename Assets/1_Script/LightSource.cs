using System.Collections.Generic;
using UnityEngine;

// **불빛이 닿는 자리에서는 밤이 걷힌다.**
//
// 밤에 건 것은 셋이었다 — 어두워지고(`DayNightLighting`) · 덜 보이고(`Vision`) · 습격이
// 몰린다(`GameEventData.NightWeight`). 그런데 그 셋 어디에도 **플레이어가 대응할 자리가
// 없었다.** 밤은 견디는 시간이지 다루는 시간이 아니었고, 그러면 그건 선택이 아니라 벌칙이다
// (밤을 좋게 만드는 손잡이가 하나도 없으면 "밤에 뭘 할까"라는 물음 자체가 안 생긴다).
//
// 불은 **앞의 둘에 답한다.** 습격에는 답하지 않는다 — 그건 벽·순찰·소집이 하는 일이고,
// 하나가 셋을 다 되돌리면 그것을 짓는 것 말고 다른 결정이 안 남는다.
//
// ## 세력을 안 가린다
//
// 불은 누구에게나 밝다. 우리 화톳불 옆에 선 산적도 그만큼 잘 본다 — **그것이 거점을 밝히는
// 값이다.** 가리기 시작하면 켤 때마다 "누구의 불인가"를 물어야 하고, 무엇보다 눈에 보이는
// 것(밝은 자리)과 판정이 갈린다.
//
// ## 답은 `Vision` 한 벌로 간다
//
// 여기는 **얼마나 밝은가만 답한다**(0~1). 그것을 감지 범위로 바꾸는 식은 `Vision`에 있다 —
// 교전 탐지와 도주 탐지가 이미 그 한 벌을 쓰고 있어서, 불빛이 제 식을 따로 들면 *밤에 도망만
// 멀쩡한* 그 사고가 축만 바꿔 돌아온다.
//
// ## 레지스트리다
//
// `Storage.All`·`Faction.All`·`Corpse.All`과 같은 자리 — 타입이 제 인스턴스를 들고 있을
// 뿐이라 구독을 놓쳐 새는 데가 없다. **낮에는 아예 안 훑는다**(`Vision.FactorAt`가 배율이
// 1이면 곧바로 돌아선다) — 탐지는 폰마다 0.1초에 한 번 도는 자리라 그 게이트가 값을 한다.
public class LightSource : MonoBehaviour
{
    public static readonly List<LightSource> All = new();

    [Tooltip("불빛이 닿는 거리(m). 이 안에서는 어둠 배율이 걷힌다.")]
    [SerializeField] float radius = 8f;

    [Tooltip("얼마나 걷어 내는가. 1이면 그 안은 낮처럼 본다. 밤눈(UnitData.NightVision)과 같은 축이다.")]
    [Range(0f, 1f)] [SerializeField] float strength = 1f;

    [Tooltip("같이 켜고 끌 등. 비우면 자식에서 찾는다 — **화면이 안 밝아지면 시야만 도는 거짓말이 된다.**")]
    [SerializeField] Light lamp;

    [Tooltip("같이 켜고 끌 불꽃 모형. 등만 켜면 빛은 도는데 불이 안 보인다.")]
    [SerializeField] GameObject flames;

    // **어두울수록 불이 세진다**(2026-10-01, 사용자: *"저녁에는 더 어두워서 횃불이나 불빛이 더
    // 잘보였으면"*). 주변이 내려앉는데 등이 그대로면 둘레의 *대비*만 커지고 불빛 자리는 오히려
    // 칙칙해진다 — 사람 눈이 어둠에 적응하는 것을 등 쪽에서 흉내 낸다. **세기만 올리고 닿는 거리는
    // 안 늘린다** — 화면에 밝은 원이 `radius`(시야가 걷히는 자리)보다 넓어지면 *"밝아 보이는데 안 보인다"*가 된다.
    public const float NightBoost = 1.8f;

    [Tooltip("불꽃이 흔들리는 폭(등 세기 대비). 0이면 가만히 탄다.")]
    [Range(0f, 0.5f)] [SerializeField] float flicker = 0.15f;

    // **불이 아니라 빛나는 것도 있다**(2026-09-06, 발광 버섯).
    //
    // 지금까지 이 컴포넌트는 *불*만 알았다 — 켜는 때를 `Campfire`가 정하고(저녁에 켜고
    // 아침에 끈다) 아무도 안 켜면 영영 어둡다. **버섯은 켜는 사람이 없다**: 그냥 빛난다.
    //
    // **`Vision`이 이걸 그대로 읽는 것이 요점이다** — 발광 버섯 지대에 들어서면 화면이
    // 밝아지는 만큼 **폰도 실제로 더 본다.** 순수한 장식 조명으로 두면 *"밝아 보이는데
    // 안 보인다"*가 생긴다(**화면과 판정은 같은 수를 봐야 한다**).
    [Tooltip("켜는 사람 없이 늘 빛난다(발광 버섯 등). 화톳불은 끈다 — 그건 때가 정한다.")]
    [SerializeField] bool alwaysLit;

    float   _baseIntensity;
    Vector3 _flameScale;
    float   _noiseSeed;
    bool    _lit;

    public bool  AlwaysLit { get => alwaysLit; set => alwaysLit = value; }

    // 쓰는 쪽은 크기가 칸마다 다른 것뿐이다(`Skylight` — 구멍이 크면 볕도 넓다).
    public float Radius { get => radius; set => radius = value; }
    public float Strength => strength;
    public bool  Lit      => _lit;

    // **불빛이 화면에서 퍼지는 거리의 배율**(2026-10-12, 사용자: *"빛이 퍼지는 범위를 두배정도 늘려도 될 거
    // 같고"*). 굴의 주변광을 거의 0으로 내린 뒤로는 등이 닿는 데까지만 보인다 — 프리팹에 적힌 범위는 주변광이
    // 받쳐 주던 때의 값이라 웅덩이가 너무 좁다.
    //
    // **세기는 그대로다** — 빛은 거리 제곱으로 줄므로 범위만 늘리면 가장자리가 어둑하게 멀리 번진다.
    // **`radius`(시야가 걷히는 거리)는 안 늘린다** — 그건 판정이고, 사용자가 바꾸라고 한 것은 화면이다.
    public const float Reach = 2f;

    void Awake()
    {
        if (lamp == null) lamp = GetComponentInChildren<Light>(includeInactive: true);

        // 횃불은 등이 여럿이다 — 전부 같이 늘린다.
        foreach (var l in GetComponentsInChildren<Light>(includeInactive: true))
            l.range *= Reach;
        if (lamp   != null) _baseIntensity = lamp.intensity;
        if (flames != null) _flameScale    = flames.transform.localScale;

        // **불마다 다른 박자로 흔들린다** — 씨앗이 같으면 나란히 선 화톳불 셋이 한 몸처럼
        // 맥동해서, 불이 아니라 무언가의 신호로 보인다.
        _noiseSeed = Random.value * 100f;
    }

    void OnEnable()
    {
        All.Add(this);
        if (alwaysLit) _lit = true;
        Apply();
    }

    // **꺼질 때는 불도 끈다.** 휴면(`BuildingDormancy`)은 컴포넌트를 `enabled = false`로
    // 재우는데, 등은 자식 오브젝트라 그것만으로는 계속 타고 있다 — 영역 밖에서 홀로 밝은
    // 화톳불이 남는다.
    void OnDisable()
    {
        All.Remove(this);
        _lit = false;
        Apply();
    }

    public void SetLit(bool on)
    {
        if (_lit == on) return;
        _lit = on;
        Apply();
    }

    void Apply()
    {
        if (lamp   != null) lamp.enabled = _lit;
        if (flames != null) flames.SetActive(_lit);
    }

    // 불꽃은 가만히 있지 않는다. **세기와 불꽃 크기를 흔들되 색은 안 흔든다** — 색까지
    // 건드리면 밤 조명 전체가 물결친다. 모형을 같이 흔드는 것이 값이 크다: 등만 깜빡이면
    // 불이 흔들리는 게 아니라 **전구가 나가는 것처럼** 보인다.
    //
    // **두 축이 다른 박자로 논다**(노이즈 좌표를 달리한다) — 같이 부풀었다 줄면 그건
    // 불꽃이 아니라 숨쉬기다.
    void Update()
    {
        if (!_lit) return;

        float boost = Mathf.Lerp(1f, NightBoost, UnderworldLighting.Darkness);

        if (lamp != null)
        {
            float n = flicker > 0f ? Mathf.PerlinNoise(_noiseSeed, Time.time * 3.5f) - 0.5f : 0f;
            lamp.intensity = _baseIntensity * boost * (1f + flicker * 2f * n);
        }

        if (flicker <= 0f) return;

        if (flames != null && _flameScale != Vector3.zero)
        {
            float m = Mathf.PerlinNoise(_noiseSeed + 31.7f, Time.time * 2.6f) - 0.5f;
            // 위아래로 더 크게 늘어난다 — 불은 옆으로 부푸는 것보다 위로 솟는다.
            flames.transform.localScale = new Vector3(
                _flameScale.x * (1f + flicker * 0.7f * m),
                _flameScale.y * (1f + flicker * 1.6f * m),
                _flameScale.z * (1f + flicker * 0.7f * m));
        }
    }

    // 이 자리가 얼마나 밝은가(0~1).
    //
    // **겹쳐도 더 밝아지지 않는다 — 가장 밝은 것 하나가 답한다.** 합치면 화톳불 둘로 낮을
    // 넘어서고, 그 위쪽은 눈금이 없는 축이 된다("얼마나 밝은가"에 상한이 사라진다).
    //
    // **가장자리에서 뚝 끊긴다.** 부드럽게 줄이면 "어디까지가 불빛인가"를 눈으로 잴 수 없어
    // 화톳불을 어디에 세울지가 짐작이 된다 — 점광원의 반경과 눈으로 맞출 수 있는 편이 낫다.
    public static float BrightestAt(Vector3 pos)
    {
        float best = 0f;
        for (int i = 0; i < All.Count; i++)
        {
            var l = All[i];
            if (l == null || !l._lit || l.strength <= best) continue;
            if ((l.transform.position - pos).sqrMagnitude > l.radius * l.radius) continue;

            best = l.strength;
            if (best >= 1f) break;
        }
        return best;
    }
}

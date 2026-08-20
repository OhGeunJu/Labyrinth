using UnityEngine;

// **하루를 눈으로 보이게 한다.**
//
// 하루를 다섯 때로 갈라 놓고도 그것을 읽는 곳은 시계 글자 하나뿐이었다(`TimeHUD`) —
// 화면은 스무 시간 내내 똑같이 밝아서, 밤이 온 것을 **글자를 읽어야만** 알았다. 그러면
// 밤에 뭘 걸어도 그건 밸런스 표의 숫자일 뿐 플레이어에게는 아무 일도 안 일어난 것과 같다.
// **밤이 값을 가지려면 밤처럼 보여야 한다.**
//
// ## 시계는 하나다
//
// 여기는 제 시간을 세지 않는다 — `GameTimeManager`가 정한 때(`TimePhase`)와 시각(`Hour`)을
// 그대로 읽는다. 조명이 제 축을 들면 **"화면은 저녁인데 판정은 밤"**이 생기고, 그건 시계
// 글자와 어둠이 따로 노는 것이라 어느 쪽도 못 믿게 된다.
//
// ## 광원은 하나다 — 낮에는 해, 밤에는 달
//
// 씬의 방향광 하나를 하루에 걸쳐 **한 바퀴 돌린다**(05시 일출 → 21시 일몰 → 지평 아래 8시간).
// 달을 위해 광원을 하나 더 두지 않는 이유: 세기·색·그림자를 두 곳에서 맞춰야 하고, 둘이
// 겹치는 새벽·저녁에 무엇이 이기는지가 또 규칙이 된다. **밤빛은 같은 등을 푸르게 낮춘 것**이다.
//
// 지평 아래로 내려가면 빛이 밑에서 올라와 그림자가 뒤집히므로 **어두운 때에는 그림자를 끈다**.
// 어차피 그 세기(0.06)에서는 그림자가 보이지도 않는다. 덤으로 절차적 하늘(기본 스카이박스)이
// 광원 방향을 따라가므로 **하늘도 저 혼자 저문다** — 스카이박스 머티리얼을 건드리지 않는다
// (런타임에 고치면 그 에셋이 실제로 바뀌어 플레이를 나가도 남는다).
//
// ## 색은 때가 들고, 경계에서만 섞인다
//
// 값을 때마다 한 벌씩 저작한다 — 시각별 키프레임 표를 따로 두면 그 표와 `GameTimeManager`의
// 경계 상수가 **두 벌**이 되어 반드시 어긋난다. 대신 때가 바뀌는 그 시각부터 `blendHours`
// 동안 이웃으로 넘어간다: 라벨이 `저녁`으로 바뀌는 순간 빛도 물들기 시작하고, 한 시간 반 뒤
// 저녁 색에 닿는다. **끊기지 않되 시계와 어긋나지도 않는다.**
//
// ## 밤은 어둡되 검지 않다
//
// 주변광(`ambientLight`)이 바닥을 든다. 방향광만 낮추면 그늘이 완전한 검정이 되어 밤이
// **어두운 것이 아니라 안 보이는 것**이 된다 — 그건 분위기가 아니라 조작 불능이다.
public class DayNightLighting : MonoBehaviour
{
    [Tooltip("하루에 걸쳐 돌릴 방향광. 비우면 이 오브젝트의 Light → 씬의 방향광 순으로 찾는다.")]
    [SerializeField] Light sun;

    [Tooltip("때가 바뀌고 몇 시간에 걸쳐 다음 색으로 넘어가는가. 0이면 경계에서 뚝 끊긴다.")]
    [Range(0f, 4f)] [SerializeField] float blendHours = 1.5f;

    [Tooltip("어두운 때(밤·새벽)에는 그림자를 끈다 — 광원이 지평 아래라 그림자가 뒤집힌다.")]
    [SerializeField] bool disableShadowsAtNight = true;

    // 다섯 때의 빛. **순서는 `TimePhase`와 같다**(Dawn·Morning·Day·Evening·Night) —
    // 인스펙터에서 위아래로 읽히는 순서가 곧 하루의 순서라야 저작하면서 헷갈리지 않는다.
    [System.Serializable]
    public struct PhaseLight
    {
        public Color sun;        // 방향광 색
        public float intensity;  // 방향광 세기
        public Color ambient;    // 주변광 — 그늘의 바닥
    }

    [Header("새벽 00~04")]
    [SerializeField] PhaseLight dawn = new()
    {
        sun       = new Color(0.55f, 0.60f, 0.85f),
        intensity = 0.18f,
        ambient   = new Color(0.16f, 0.18f, 0.26f),
    };

    [Header("아침 05~09")]
    [SerializeField] PhaseLight morning = new()
    {
        sun       = new Color(1f, 0.87f, 0.70f),
        intensity = 0.95f,
        ambient   = new Color(0.42f, 0.43f, 0.46f),
    };

    [Header("낮 10~16")]
    [SerializeField] PhaseLight day = new()
    {
        sun       = new Color(1f, 0.97f, 0.90f),
        intensity = 1.25f,
        ambient   = new Color(0.52f, 0.54f, 0.58f),
    };

    [Header("저녁 17~20")]
    [SerializeField] PhaseLight evening = new()
    {
        sun       = new Color(1f, 0.70f, 0.45f),
        intensity = 0.70f,
        ambient   = new Color(0.34f, 0.30f, 0.30f),
    };

    [Header("밤 21~23")]
    [SerializeField] PhaseLight night = new()
    {
        sun       = new Color(0.45f, 0.52f, 0.80f),
        intensity = 0.06f,
        ambient   = new Color(0.12f, 0.14f, 0.22f),
    };

    // 방위각은 **저작한 것을 지킨다** — 해가 어느 쪽에서 뜨는지는 씬의 몫이고, 여기가 정하는
    // 것은 "얼마나 높이 떴나" 하나다. `Awake`에서 잡아 두지 않으면 첫 프레임에 덮어써서
    // 씬에서 맞춰 둔 각도가 소리 없이 사라진다.
    float        _yaw;
    LightShadows _shadows;   // 낮에 되돌릴 것 — 그림자 품질도 씬이 저작한 값이다
    bool         _bound;

    void OnEnable()
    {
        Bind();
        GameTimeManager.OnTimeChanged += Apply;
        Apply(GameTimeManager.NormalizedTime);   // 첫 프레임부터 맞는 밝기로 시작한다
    }

    void OnDisable() => GameTimeManager.OnTimeChanged -= Apply;

    void Bind()
    {
        if (sun == null) sun = GetComponent<Light>();
        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null)
        {
            Debug.LogWarning("[DayNightLighting] 방향광을 못 찾았다 — 하늘과 그림자는 그대로 있는다.", this);
            return;
        }
        if (!_bound)
        {
            _yaw     = sun.transform.eulerAngles.y;
            _shadows = sun.shadows;
            _bound   = true;
        }
    }

    // ── 하루를 빛에 얹는다 ───────────────────────────────────────────────────
    void Apply(float normalized)
    {
        float hour = Mathf.Repeat(normalized * 24f, 24f);
        var   lit  = Sample(hour);

        // 주변광은 광원이 없어도 걸린다 — 방향광을 못 찾은 씬에서도 밤은 어두워야 한다.
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = lit.ambient;

        if (sun == null) return;

        sun.color     = lit.sun;
        sun.intensity = lit.intensity;
        sun.transform.rotation = Quaternion.Euler(Elevation(hour), _yaw, 0f);

        // 낮에는 **저작한 값으로** 되돌린다 — 여기서 `Soft`로 못 박으면 씬에서 끄거나 Hard로
        // 맞춰 둔 것이 소리 없이 덮인다(방위각을 지키는 것과 같은 자리).
        if (disableShadowsAtNight)
            sun.shadows = GameTimeManager.IsDay ? _shadows : LightShadows.None;
    }

    // 해의 고도(= 방향광의 X 회전). 0 = 지평선, 90 = 머리 위, 180 = 반대쪽 지평선.
    //
    // **밝은 때 16시간에 위쪽 반 바퀴, 어두운 때 8시간에 아래쪽 반 바퀴**를 쓴다 —
    // 그래야 05시에 뜨고 21시에 지는 것이 `GameTimeManager`의 경계와 정확히 맞는다.
    // 길이가 다른 두 구간에 같은 180도를 나눠 주므로 **밤에는 해가 두 배 빨리 지나간다**.
    static float Elevation(float hour)
    {
        const float bright = GameTimeManager.NightHour - GameTimeManager.MorningHour;   // 16
        const float dark   = 24f - bright;                                              // 8

        if (hour >= GameTimeManager.MorningHour && hour < GameTimeManager.NightHour)
            return 180f * ((hour - GameTimeManager.MorningHour) / bright);

        float since = Mathf.Repeat(hour - GameTimeManager.NightHour, 24f);   // 일몰 후 몇 시간
        return 180f + 180f * (since / dark);
    }

    // 지금 시각의 빛 — 때의 값에서 시작하되, 경계 직후 `blendHours` 동안은 이전 때에서 넘어온다.
    PhaseLight Sample(float hour)
    {
        var   phase = GameTimeManager.PhaseAt(Mathf.FloorToInt(hour));
        var   cur   = Of(phase);
        float since = Mathf.Repeat(hour - StartOf(phase), 24f);   // 이 때가 시작되고 몇 시간

        if (blendHours <= 0f || since >= blendHours) return cur;

        var   prev = Of(Previous(phase));
        float t    = since / blendHours;
        return new PhaseLight
        {
            sun       = Color.Lerp(prev.sun,       cur.sun,       t),
            intensity = Mathf.Lerp(prev.intensity, cur.intensity, t),
            ambient   = Color.Lerp(prev.ambient,   cur.ambient,   t),
        };
    }

    PhaseLight Of(TimePhase p) => p switch
    {
        TimePhase.Dawn    => dawn,
        TimePhase.Morning => morning,
        TimePhase.Day     => day,
        TimePhase.Evening => evening,
        _                 => night,
    };

    // 경계는 **`GameTimeManager`의 상수 그대로**다 — 여기 숫자를 다시 적으면 하루의 눈금이
    // 두 벌이 되어, 시각표를 고쳤을 때 빛만 옛날 시각에 바뀐다.
    static float StartOf(TimePhase p) => p switch
    {
        TimePhase.Dawn    => 0f,
        TimePhase.Morning => GameTimeManager.MorningHour,
        TimePhase.Day     => GameTimeManager.DayHour,
        TimePhase.Evening => GameTimeManager.EveningHour,
        _                 => GameTimeManager.NightHour,
    };

    static TimePhase Previous(TimePhase p) => p switch
    {
        TimePhase.Dawn    => TimePhase.Night,
        TimePhase.Morning => TimePhase.Dawn,
        TimePhase.Day     => TimePhase.Morning,
        TimePhase.Evening => TimePhase.Day,
        _                 => TimePhase.Evening,
    };
}

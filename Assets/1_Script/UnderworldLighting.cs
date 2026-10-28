using UnityEngine;

// **지하의 하루 — 어둠이 밀물처럼 든다**(2026-08-29, 설계 2단계).
//
// 지하에는 해가 없다. 그래서 `DayNightLighting`(방향광 한 바퀴)은 재웠는데, 그러고 나니
// **하루가 통째로 사라졌다** — 화면이 언제나 같고, `GameEventData.NightWeight`(밤에 습격이
// 몰린다)가 화면에 아무 근거도 없이 굴러간다.
//
// ## 셋이 같이 있어야 뜻이 선다
//
// 지상의 밤에 대해 이미 못 박은 규칙이다: **어둡다 · 덜 보인다 · 습격이 몰린다.** 하나만
// 남으면 어둡기만 하면 불편이고, 시야만 줄면 표의 숫자고, 습격만 몰리면 **왜 그때 몰려오는지가
// 화면에 없다.** 지하에서도 셋이 그대로 필요하고, **바뀌는 것은 이름과 그림뿐이다.**
//
//   지상            지하
//   해가 진다   →   **어둠이 밀물처럼 든다**
//   방향광      →   주변광이 내려앉는다(해가 없으니 방향이 없다)
//   `Vision`    →   그대로
//   `NightWeight` →  그대로
//
// ## 시계는 하나다
//
// **`GameTimeManager`의 때를 그대로 읽는다.** 지하라고 제 시계를 들면 *"화면은 밀물인데
// 판정은 썰물"*이 생겨 어느 쪽도 못 믿게 된다 — 밤 조명이 이미 겪고 못 박은 자리다.
// 허기·피로·작물·연구가 전부 그 축에 매달려 있기도 하다.
//
// ## 밝을 때도 어둡다
//
// 지상의 낮은 배율 1이지만 **지하의 썰물은 1이 아니다.** 물이 빠져도 여기는 땅속이라,
// 가장 밝을 때가 지상의 저녁만 하다. **그래서 불이 장식이 아니게 된다** — 언제 켜든
// 둘레가 밝아지고, 밀물 때는 그 차이가 커진다.
[DefaultExecutionOrder(-40)]
public class UnderworldLighting : MonoBehaviour
{
    // 때마다의 어둠. **1이 없는 것이 요점이다** — 어느 때에도 지하는 지상의 낮이 아니다.
    //
    // 지상 값(낮 1.0 · 저녁 0.7 · 새벽 0.5 · 밤 0.4)과 견주면 **한 칸씩 아래**다.
    //
    // **2026-10-01에 저녁·밤을 더 내렸다**(사용자: *"저녁에는 더 어두워서 횃불이나 불빛이 더
    // 잘보였으면"*) — 0.48/0.38/0.30 → 0.40/0.32/0.22. 시야도 같은 수를 읽으므로(`Vision`)
    // 불 없는 밤은 정말로 덜 보이고, **불빛 안은 그대로 낮처럼 본다** — 그 차이가 불을 세우는 이유다.
    //
    // **2026-10-11에 화면만 더 내렸다**(사용자: *"폰의 시야를 줄일필요는 없고 전체 기본 밝기를 내리라는 말이야"*) —
    // 이 값들은 그대로고 `gloom`가 주변광을 한 번 더 누른다. 길을 읽는 것은 불빛과 채광 구멍(`Skylight`)이다.
    public const float LowTide  = 0.62f;   // 낮·아침 — 가장 밝을 때
    public const float Slack    = 0.40f;   // 저녁
    public const float Rising   = 0.32f;   // 새벽
    public const float HighTide = 0.22f;   // 밤 — 밀물이 가장 깊다

    // 지금의 어둠. **`Vision`이 이 값을 쓴다** — 시야와 화면이 같은 수를 봐야
    // *"어두워 보이는데 잘 보인다"*가 안 생긴다.
    public static float Tide => GameTimeManager.Phase switch
    {
        TimePhase.Evening => Slack,
        TimePhase.Dawn    => Rising,
        TimePhase.Night   => HighTide,
        _                 => LowTide,
    };

    // 폰의 최저 밝기(주변광이 씬 값 그대로일 때). 셰이더의 `_PawnFloor`가 읽는다.
    public const float PawnFloor = 0.35f;
    static readonly int PawnFloorId = Shader.PropertyToID("_PawnFloor");

    // **이 컴포넌트가 없는 자리(편집 중 · 지상)에서는 옛 값이다** — 전역 값은 아무도 안 적으면 0이다.
    // **에디터를 켤 때도 적는다** — 안 적으면 Play를 한 번 돌기 전까지 0이라, 그 사이에 구운
    // 프리팹 미리보기와 씬 뷰의 폰이 검게 나온다(2026-10-24).
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPawnFloor() => Shader.SetGlobalFloat(PawnFloorId, PawnFloor);

    // 켜져 있나 — `Vision`이 묻는다. **컴포넌트가 아니라 세계가 지하인지로 답한다**(`RockMap`).
    // 씬에 이 컴포넌트를 안 붙였다고 시야만 지상 규칙으로 돌면 그게 가장 나쁘다.
    public static bool Active => RockMap.Enabled;

    [Header("주변광 — 밀물이 가장 깊을 때")]
    // **이름을 `darkest`에서 바꿨다**(2026-10-01) — 씬이 든 옛 값(0.45)이 코드 기본값을 이겨서,
    // 기본값만 내리면 아무 일도 안 일어난다. 이름이 바뀌면 씬 값이 버려지고 아래 값으로 선다.
    [Tooltip("배율 1(썰물)일 때의 색에 이 배율이 곱해진다. 0에 가까울수록 밤이 깊다.")]
    [SerializeField, Range(0.05f, 1f)] float deepest = 0.2f;

    // 씬이 든 주변광(`WorldStreamingSetup`)은 *"무엇이 어디 있는지 보인다"*에 맞춘 값이다. 그 위에 한 번 더
    // 곱한다 — 씬 값을 고치면 지하를 다시 세울 때마다 되돌아간다.
    [Header("주변광 — 썰물일 때")]
    // **이름을 `brightest`에서 바꿨다**(같은 날) — 처음 값 0.6이 씬에 저장돼 있으면 기본값을 내려도 안 바뀐다.
    // 사용자: *"빛이 안비추는 곳은 엄청 새카맣게 어두웠으면 … 진짜 잘봐야지 실루엣이 보일정도"*.
    [Tooltip("가장 밝을 때의 주변광에 곱하는 배율. 1이면 씬에 적힌 색 그대로, 0에 가까울수록 불빛 밖이 새카맣다.")]
    [SerializeField, Range(0.01f, 1f)] float gloom = 0.06f;

    [Tooltip("색이 옮겨 가는 속도. 때가 바뀌는 순간 뚝 끊기면 화면이 튄다.")]
    [SerializeField] float lerpSpeed = 0.6f;

    // 씬에 저작된 값이 **썰물 기준**이다 — 도구가 써 둔 그 색에서 어두워지기만 한다.
    Color _sky, _equator, _ground;
    float _fog;
    float _cur = 1f;

    // 지금 화면이 얼마나 어두운가(0 = 썰물 · 1 = 밀물이 가장 깊을 때). **옮겨 가는 중의 값이다** —
    // 불빛(`LightSource`)이 이것으로 세기를 올린다. 때(`Tide`)를 바로 읽으면 화면은 아직 저녁인데
    // 불만 한밤의 세기로 뛴다.
    public static float Darkness { get; private set; }

    void OnEnable()
    {
        _sky     = RenderSettings.ambientSkyColor;
        _equator = RenderSettings.ambientEquatorColor;
        _ground  = RenderSettings.ambientGroundColor;
        _fog = RenderSettings.fogDensity;
        _cur = Norm(Tide);
        Apply(_cur);
    }

    // **떠날 때 되돌린다** — 안 되돌리면 저작한 값이 *마지막으로 어두웠던 값*으로 굳어,
    // 다음에 씬을 열면 조금씩 더 어두워진다(에디터에서 특히 나쁘다).
    void OnDisable()
    {
        Darkness = 0f;
        Shader.SetGlobalFloat(PawnFloorId, PawnFloor);
        RenderSettings.ambientSkyColor     = _sky;
        RenderSettings.ambientEquatorColor = _equator;
        RenderSettings.ambientGroundColor  = _ground;
        RenderSettings.fogDensity          = _fog;
    }

    // 썰물(가장 밝을 때)을 1로 본 상대값.
    static float Norm(float tide) => Mathf.InverseLerp(HighTide, LowTide, tide);

    void Update()
    {
        // **`unscaledDeltaTime`이다** — `GameTimeManager`가 `timeScale`을 만지므로, 멈춰 세우면
        // 색이 옮겨 가다 그 자리에 얼어붙는다(카메라가 같은 이유로 실시간을 쓴다).
        _cur = Mathf.MoveTowards(_cur, Norm(Tide), lerpSpeed * Time.unscaledDeltaTime);
        Apply(_cur);
    }

    void Apply(float t)
    {
        Darkness = 1f - t;
        float k = Mathf.Lerp(deepest, 1f, t) * gloom;

        // **폰도 같이 어두워진다** — 폰 셰이더(`ToonOutline`·`ChibiFace`)는 최저 밝기를 따로 든다. 그것이
        // 상수였을 때는 굴만 검어지고 폰은 그대로 떠서, 어두운 것이 아니라 *바닥만 검게 칠한 낮*으로 보였다.
        // **제곱근으로 따라간다** — 굴과 똑같이 내리면 불빛 밖의 폰이 아예 안 보여 집을 수가 없다.
        Shader.SetGlobalFloat(PawnFloorId, PawnFloor * Mathf.Sqrt(k));
        RenderSettings.ambientSkyColor     = _sky     * k;
        RenderSettings.ambientEquatorColor = _equator * k;
        RenderSettings.ambientGroundColor  = _ground  * k;

        // **안개는 반대로 짙어진다** — 어두워지는 것과 멀리 안 보이게 되는 것은 같은 일이다.
        //
        // **켜는 것은 여기가 아니다**(2026-09-07). 씬에서 안개가 꺼져 있어(`m_Fog: 0`) 이
        // 줄은 실제로 아무 일도 안 한다 — 켜 봤더니 **지금 필요가 없었다**(사용자: *"청크가
        // 그렇게 많이 로드되는 것도 아닌데 안개로 가려버릴 필요는 없다"*). 쓸 날이 오면
        // 씬에서 켜면 되고, 그때 이 줄이 저절로 살아난다.
        RenderSettings.fogDensity = Mathf.Lerp(_fog * 1.8f, _fog, t);
    }
}

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
    public const float LowTide  = 0.62f;   // 낮·아침 — 가장 밝을 때
    public const float Slack    = 0.48f;   // 저녁
    public const float Rising   = 0.38f;   // 새벽
    public const float HighTide = 0.30f;   // 밤 — 밀물이 가장 깊다

    // 지금의 어둠. **`Vision`이 이 값을 쓴다** — 시야와 화면이 같은 수를 봐야
    // *"어두워 보이는데 잘 보인다"*가 안 생긴다.
    public static float Tide => GameTimeManager.Phase switch
    {
        TimePhase.Evening => Slack,
        TimePhase.Dawn    => Rising,
        TimePhase.Night   => HighTide,
        _                 => LowTide,
    };

    // 켜져 있나 — `Vision`이 묻는다. **컴포넌트가 아니라 세계가 지하인지로 답한다**(`RockMap`).
    // 씬에 이 컴포넌트를 안 붙였다고 시야만 지상 규칙으로 돌면 그게 가장 나쁘다.
    public static bool Active => RockMap.Enabled;

    [Header("주변광 — 밀물이 가장 깊을 때")]
    [Tooltip("배율 1(썰물)일 때의 색에 이 배율이 곱해진다. 0에 가까울수록 밤이 깊다.")]
    [SerializeField, Range(0.1f, 1f)] float darkest = 0.45f;

    [Tooltip("색이 옮겨 가는 속도. 때가 바뀌는 순간 뚝 끊기면 화면이 튄다.")]
    [SerializeField] float lerpSpeed = 0.6f;

    // 씬에 저작된 값이 **썰물 기준**이다 — 도구가 써 둔 그 색에서 어두워지기만 한다.
    Color _sky, _equator, _ground;
    float _fog;
    float _cur = 1f;

    void OnEnable()
    {
        _sky     = RenderSettings.ambientSkyColor;
        _equator = RenderSettings.ambientEquatorColor;
        _ground  = RenderSettings.ambientGroundColor;
        _fog     = RenderSettings.fogDensity;
        _cur     = Norm(Tide);
        Apply(_cur);
    }

    // **떠날 때 되돌린다** — 안 되돌리면 저작한 값이 *마지막으로 어두웠던 값*으로 굳어,
    // 다음에 씬을 열면 조금씩 더 어두워진다(에디터에서 특히 나쁘다).
    void OnDisable()
    {
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
        float k = Mathf.Lerp(darkest, 1f, t);
        RenderSettings.ambientSkyColor     = _sky     * k;
        RenderSettings.ambientEquatorColor = _equator * k;
        RenderSettings.ambientGroundColor  = _ground  * k;

        // **안개는 반대로 짙어진다** — 어두워지는 것과 멀리 안 보이게 되는 것은 같은 일이다.
        RenderSettings.fogDensity = Mathf.Lerp(_fog * 1.8f, _fog, t);
    }
}

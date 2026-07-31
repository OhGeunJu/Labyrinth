using System;
using UnityEngine;

// 씬에 하나 배치. 낮 20분 + 밤 10분 = 1일(실제 30분).
// NormalizedTime: 0.0=새벽, dayFraction=황혼, 1.0=다음날 새벽
//
// 밤을 낮보다 짧게 둔 이유: 폰이 실제로 일하는 시간이 낮이고, 밤은 자고 회복하는
// 구간이라 같은 길이면 "아무 일도 안 일어나는 절반"이 된다.
public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    [Tooltip("1일 전체 길이(초). 기본 1800 = 실제 30분.")]
    [SerializeField] float dayDuration = 1800f;

    [Tooltip("하루 중 낮이 차지하는 비율. 기본 2/3 = 낮 20분 + 밤 10분.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float dayFraction = 2f / 3f;

    public static float NormalizedTime { get; private set; }
    public static int   CurrentDay     { get; private set; }
    public static bool  IsDay          => NormalizedTime < DayFraction;

    // 인스턴스 필드를 static에서 읽기 위한 통로. 씬에 매니저가 없으면 절반으로 가정.
    static float DayFraction => Instance != null ? Instance.dayFraction : 0.5f;

    // 자정(새벽) 마다 발동 — 상인 재고 갱신 등
    public static event Action        OnDayChanged;
    // 매 프레임 0-1 시간 전달 — 조명, 야간 오브젝트 등
    public static event Action<float> OnTimeChanged;

    // ── 시각 표시 ────────────────────────────────────────────────────────────
    // **6시를 하루의 시작**으로 잡는다. 0을 0시로 두면 "낮인데 새벽 3시"가 된다.
    // 시계는 선형이므로 낮/밤 경계는 dayFraction을 따라간다 —
    // 기본값 2/3이면 낮 06:00~22:00 / 밤 22:00~06:00(여름날처럼 읽힌다).
    // 시계 눈금을 06:00~18:00에 묶으려면 낮과 밤의 시침 속도를 달리해야 하는데,
    // 그러면 "한 시간"이 때에 따라 다른 길이가 된다 — 선형 쪽을 택했다.
    const float DayStartHour = 6f;

    public static int Hour   => Mathf.FloorToInt((NormalizedTime * 24f + DayStartHour) % 24f);
    public static int Minute => Mathf.FloorToInt(NormalizedTime * 24f * 60f % 60f);

    // ── 배속 ─────────────────────────────────────────────────────────────────
    // `Time.timeScale`을 직접 만진다 — 시간의 주인이 여기이기 때문이다.
    // 폰 행동·건설·채굴이 전부 deltaTime을 따르므로 배속이 게임 전체에 똑같이 걸린다.
    public static float Speed { get; private set; } = 1f;
    public static bool  IsPaused => Speed <= 0f;
    public static event Action<float> OnSpeedChanged;

    public static void SetSpeed(float speed)
    {
        Speed = Mathf.Max(0f, speed);
        Time.timeScale = Speed;
        OnSpeedChanged?.Invoke(Speed);
    }

    float _elapsed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        SetSpeed(1f);   // 이전 플레이에서 멈춰둔 채 나갔을 수 있다
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Time.timeScale = 1f;   // 멈춘 채로 씬을 떠나면 에디터가 얼어붙은 것처럼 보인다
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float prev    = NormalizedTime;
        NormalizedTime = _elapsed % dayDuration / dayDuration;

        OnTimeChanged?.Invoke(NormalizedTime);

        // wrap-around 감지 → 새 날
        if (prev > 0.9f && NormalizedTime < 0.1f)
        {
            CurrentDay++;
            OnDayChanged?.Invoke();
        }
    }
}

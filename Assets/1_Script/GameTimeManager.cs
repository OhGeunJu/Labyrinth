using System;
using UnityEngine;

// 씬에 하나 배치. 낮 30분 + 밤 30분 = 1일(실제 60분).
// NormalizedTime: 0.0=새벽, 0.5=황혼, 1.0=다음날 새벽
public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    [Tooltip("1일 전체 길이(초). 기본 3600 = 실제 60분.")]
    [SerializeField] float dayDuration = 3600f;

    public static float NormalizedTime { get; private set; }
    public static int   CurrentDay     { get; private set; }
    public static bool  IsDay          => NormalizedTime < 0.5f;

    // 자정(새벽) 마다 발동 — 상인 재고 갱신 등
    public static event Action        OnDayChanged;
    // 매 프레임 0-1 시간 전달 — 조명, 야간 오브젝트 등
    public static event Action<float> OnTimeChanged;

    // ── 시각 표시 ────────────────────────────────────────────────────────────
    // 하루의 전반(0~0.5)이 낮이므로 **6시를 하루의 시작**으로 잡는다 →
    // 낮 06:00~18:00 / 밤 18:00~06:00. 0을 0시로 두면 "낮인데 새벽 3시"가 된다.
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

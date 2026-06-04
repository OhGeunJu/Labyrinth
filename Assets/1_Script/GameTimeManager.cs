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

    float _elapsed;

    void Awake() => Instance = this;

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

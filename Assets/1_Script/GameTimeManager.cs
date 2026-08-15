using System;
using UnityEngine;

// **하루를 다섯 때로 나눈다** — 새벽·아침·낮·저녁·밤.
//
// 전에는 낮/밤 두 자뿐이었고 낮이 `dayFraction`(2/3)만큼, 즉 **실시간 20분 내내 '낮' 한 글자**가
// 떠 있었다. 하루가 흐르는 것이 화면에 안 보여서 "낮이 너무 길다"로 나타났는데, 실은 길이보다
// **눈금이 둘뿐인 것**이 문제였다. 다섯이면 20분 동안 넉 자가 바뀐다.
//
// **때는 시각(`Hour`)이 정한다** — `NormalizedTime` 위에 비율로 얹지 않는다. "저녁 다섯 시"는
// 사람이 시계를 보고 아는 말이라, 비율로 적으면 시계와 라벨이 따로 놀 수 있다.
//
// 어두운 때(밤+새벽) = 21시~04시 **8시간**, 밝은 때(아침~저녁) = 05시~20시 **16시간**.
// 옛 `dayFraction` 2/3과 **정확히 같은 비율**이라 이 갈이로 밸런스는 안 움직인다.
// 그래서 그 필드는 없앴다 — 남겨 두면 돌려도 아무 일이 안 일어나는 손잡이가 된다.
public enum TimePhase { Dawn, Morning, Day, Evening, Night }

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    [Tooltip("1일 전체 길이(초). 기본 1800 = 실제 30분.")]
    [SerializeField] float dayDuration = 1800f;

    public static float NormalizedTime { get; private set; }
    public static int   CurrentDay     { get; private set; }

    // **밝은 때인가** — 아침·낮·저녁. 새벽과 밤은 어둡다.
    //
    // 시간대에서 뽑는다: 답이 두 벌이면 "화면은 저녁인데 판정은 밤"이 생긴다.
    public static bool IsDay => Phase != TimePhase.Night && Phase != TimePhase.Dawn;

    // 게임이 시작하고 흐른 **게임 내 시간(초)**. 하루보다 짧은 주기를 재는 것들이 쓴다
    // (소굴 출격 간격 등) — `OnDayChanged`만으로는 하루에 한 번밖에 못 재고, `Time.time`을
    // 쓰면 배속·일시정지가 반영되지 않아 멈춰 둔 사이에도 시계가 도는 축이 하나 더 생긴다.
    public static float Elapsed { get; private set; }

    // 하루 길이(초). **모든 지속시간 데이터의 기준 축**이라 밖에서도 물을 수 있어야 한다 —
    // "반나절마다"를 900이라고 적으면 하루 길이를 바꿨을 때 같이 안 따라온다.
    public static float DayDuration => Instance != null ? Instance.dayDuration : 1800f;

    // 자정 마다 발동 — 상인 재고 갱신 등
    public static event Action        OnDayChanged;
    // 매 프레임 0-1 시간 전달 — 조명, 야간 오브젝트 등
    public static event Action<float> OnTimeChanged;

    // ── 시각 표시 ────────────────────────────────────────────────────────────
    //
    // **하루는 자정에 바뀐다**(`NormalizedTime` 0 = 00:00 = 새벽의 첫 시각).
    //
    // 한동안 6시를 시작으로 잡고 있었는데, 그 이유는 *"0을 0시로 두면 낮인데 새벽 3시가 된다"*
    // 였다 — **새벽이 이름 없는 때였기 때문에** 생긴 문제다. 다섯으로 나누고 나니 00시는
    // 그냥 새벽이라 그 이유가 없어졌고, 덤으로 `OnDayChanged`가 **진짜 자정에** 돈다
    // (상인 재고 갱신을 "매일 자정"이라 적어 두고 실제로는 아침 6시에 돌고 있었다).
    //
    // 시계는 선형이다 — 낮과 밤의 시침 속도를 달리하면 "한 시간"이 때에 따라 다른 길이가 된다.
    public static int Hour   => Mathf.FloorToInt(NormalizedTime * 24f) % 24;
    public static int Minute => Mathf.FloorToInt(NormalizedTime * 24f * 60f % 60f);

    // ── 시간대 ───────────────────────────────────────────────────────────────
    //
    // 경계는 **시작 시각**이다(끝은 다음 때가 정한다). 표로 두는 것은 여기 한 벌뿐이라야
    // 라벨·색·판정이 같은 답을 쓴다.
    //
    //   새벽 00~04 · 아침 05~09 · 낮 10~16 · 저녁 17~20 · 밤 21~23
    //
    // 낮이 가장 긴 것(7시간)은 폰이 실제로 일하는 때라서고, 밤이 가장 짧은 것(3시간)은
    // 같은 길이면 "아무 일도 안 일어나는 구간"이 그만큼 길어지기 때문이다. 어두운 때 전체는
    // 새벽까지 합쳐 8시간이다.
    public const int MorningHour = 5;
    public const int DayHour     = 10;
    public const int EveningHour = 17;
    public const int NightHour   = 21;

    public static TimePhase Phase => PhaseAt(Hour);

    public static TimePhase PhaseAt(int hour) =>
        hour >= NightHour   ? TimePhase.Night   :
        hour >= EveningHour ? TimePhase.Evening :
        hour >= DayHour     ? TimePhase.Day     :
        hour >= MorningHour ? TimePhase.Morning :
                              TimePhase.Dawn;

    // 표시명. **여기 하나뿐이다** — 시계·툴팁·나중에 붙을 것들이 같은 말을 쓰게.
    public static string PhaseName(TimePhase p) => p switch
    {
        TimePhase.Dawn    => "새벽",
        TimePhase.Morning => "아침",
        TimePhase.Day     => "낮",
        TimePhase.Evening => "저녁",
        _                 => "밤",
    };

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
        Elapsed   = _elapsed;
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

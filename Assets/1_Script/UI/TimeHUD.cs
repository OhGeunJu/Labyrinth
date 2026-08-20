using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 화면 상단의 시계 — **지금 몇 일차 몇 시인지, 시간이 흐르고 있는지**를 보여준다.
//
// 이게 없으면 "하루 뒤에 자원이 쌓인다" 같은 규칙을 확인할 방법이 없다.
// 하루가 1800초(실제 30분)라 배속 없이는 하루를 기다리기 어려워서 배속 버튼을 함께 둔다.
//
// 배속은 `GameTimeManager.SetSpeed`(= `Time.timeScale`)라 게임 전체에 똑같이 걸린다 —
// 폰 행동·건설·채굴이 전부 deltaTime을 따르므로 시계만 빨라지는 일은 없다.
public class TimeHUD : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI clockText;   // "3일차  14:20  낮"
    [Tooltip("배속 버튼들. 아래 speeds와 같은 순서로 연결한다.")]
    [SerializeField] List<Button> speedButtons = new();
    [SerializeField] List<float>  speeds = new() { 0f, 1f, 3f, 10f };

    [Header("색")]
    [SerializeField] Color activeSpeed   = new(0.35f, 0.55f, 0.75f);
    [SerializeField] Color inactiveSpeed = new(0.20f, 0.20f, 0.24f);
    [SerializeField] Color dayColor      = new(1f, 0.95f, 0.75f);
    [SerializeField] Color nightColor    = new(0.62f, 0.70f, 0.92f);

    [Header("단축키")]
    [SerializeField] Key pauseKey = Key.Space;

    void OnEnable()
    {
        GameTimeManager.OnTimeChanged  += OnTime;
        GameTimeManager.OnSpeedChanged += OnSpeed;
    }

    void OnDisable()
    {
        GameTimeManager.OnTimeChanged  -= OnTime;
        GameTimeManager.OnSpeedChanged -= OnSpeed;
    }

    void Start()
    {
        for (int i = 0; i < speedButtons.Count && i < speeds.Count; i++)
        {
            var speed = speeds[i];                       // 클로저가 마지막 값을 잡지 않게 복사
            if (speedButtons[i] == null) continue;
            speedButtons[i].onClick.AddListener(() => GameTimeManager.SetSpeed(speed));
        }

        Refresh();
        OnSpeed(GameTimeManager.Speed);
    }

    // 스페이스 = 일시정지 토글. 멈췄다 풀면 **직전 배속으로** 돌아간다.
    float _lastRunning = 1f;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb[pauseKey].wasPressedThisFrame) return;

        if (GameTimeManager.IsPaused) GameTimeManager.SetSpeed(_lastRunning);
        else                          { _lastRunning = GameTimeManager.Speed; GameTimeManager.SetSpeed(0f); }
    }

    void OnTime(float _) => Refresh();

    void Refresh()
    {
        if (clockText == null) return;

        // **때를 적는다**(새벽·아침·낮·저녁·밤). 낮/밤 두 자만 띄우면 실시간 20분 동안
        // 글자가 한 번도 안 바뀌어 하루가 흐르는 것이 화면에 안 보인다.
        bool day = GameTimeManager.IsDay;
        clockText.text  = $"{GameTimeManager.CurrentDay + 1}일차   " +
                          $"{GameTimeManager.Hour:00}:{GameTimeManager.Minute:00}   " +
                          GameTimeManager.PhaseName(GameTimeManager.Phase) +
                          (GameTimeManager.IsPaused ? "   (일시정지)" : "");
        clockText.color = day ? dayColor : nightColor;
    }

    // 지금 어느 배속인지 버튼 색으로 — 숫자만 있으면 뭐가 눌린 상태인지 모른다.
    void OnSpeed(float speed)
    {
        for (int i = 0; i < speedButtons.Count && i < speeds.Count; i++)
        {
            var img = speedButtons[i] != null ? speedButtons[i].GetComponent<Image>() : null;
            if (img != null) img.color = Mathf.Approximately(speeds[i], speed) ? activeSpeed : inactiveSpeed;
        }
        Refresh();
    }
}

using UnityEngine;

// 이동 막힘 계측 — 어느 탈출 단계가 실제로 발동하는지 숫자로 본다.
// 완충장치(비켜서기 / 차단 기억 / 재계획 / 포기)를 정리하기 전에 "무엇이 쓰이는가"를
// 추측이 아니라 관측으로 판단하려고 넣었다.
//
// 씬 작업 불필요 — 요약 출력용 오브젝트를 자동 설치한다.
// 끄기: DungeonColony → Toggle Movement Diagnostics (플레이 중 토글 가능)
public static class MovementDiagnostics
{
    public static bool  Enabled         = true;   // 전체 on/off (끄면 카운트도 안 함)
    public static bool  LogEvents       = true;   // 개별 이벤트 줄 (폰당 1초에 1줄로 제한)
    public static float SummaryInterval   = 10f;   // 집계 주기
    public static float HeartbeatInterval = 60f;   // 문제가 없을 때의 생존 신고 주기

    // 막힘 단계별 발동 횟수
    static int _stage1;   // 비켜서기
    static int _stage2;   // 차단 기억 + 로컬 재계획
    static int _stage3;   // 차단 기억 + 전역 재계획
    static int _stage4;   // 포기(정지)

    static int _blockMemoryHits;   // 베이크에서 PathBlockMemory 때문에 막힌 셀 수
    static int _pawnBakeBlocks;    // 베이크에서 다른 폰 때문에 막힌 셀 수
    static int _occupiedBlocks;    // 베이크에서 건물 점유 타일 때문에 막힌 셀 수
    static int _bakes;             // 로컬 윈도우 베이크 횟수 (위 셀 수를 나눠 볼 기준)

    static float _nextSummary;
    static float _nextHeartbeat;

    public static void RecordStage(int stage, string pawnName, Vector3 pos, ref float lastLogTime)
    {
        if (!Enabled) return;

        switch (stage)
        {
            case 1:  _stage1++; break;
            case 2:  _stage2++; break;
            case 3:  _stage3++; break;
            default: _stage4++; break;
        }

        // 1단계(비켜서기)는 흔하고 대개 무해하다 — 개별 줄은 2단계부터만 찍어 콘솔을 읽을 수 있게 둔다.
        if (!LogEvents || stage < 2) return;
        if (Time.time - lastLogTime < 1f) return;   // 폰당 1초 제한 (콘솔 폭주 방지)
        lastLogTime = Time.time;

        Debug.Log($"[이동진단] {pawnName} 막힘 {Mathf.Min(stage, 4)}단계({StageName(stage)}) " +
                  $"@ ({pos.x:F1}, {pos.z:F1})");
    }

    // 포기(4단계)만 상세 진단 — 왜 못 갔는지는 여기서 갈린다.
    // "목적지 칸 자체가 통행 불가"면 경로가 아예 안 나와 직선 폴백으로 물체에 처박는다.
    public static void LogGiveUp(string pawnName, Vector3 pos, Vector3 goal, bool hasLocalPath)
    {
        if (!Enabled || !LogEvents) return;

        float dist = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(goal.x, 0f, goal.z));

        Debug.LogWarning($"[이동진단] {pawnName} 포기 @ ({pos.x:F1}, {pos.z:F1}) → " +
                         $"목표 ({goal.x:F1}, {goal.z:F1}) 거리 {dist:F1}m · " +
                         $"로컬경로 {(hasLocalPath ? "있음" : "없음(직선폴백)")} · " +
                         $"목표칸 {DescribeTile(goal)}");
    }

    // 그 좌표의 타일이 어떤 상태인지 — 통행/바닥/차단기억
    static string DescribeTile(Vector3 world)
    {
        var map = MapManager.GetForPosition(world);
        if (map == null) return "맵없음";

        var tile = map.WorldToTile(world);
        return $"[{tile.x},{tile.y}] " +
               $"{(map.GetTile(tile.x, tile.y).IsPassable ? "통행가능" : "점유됨")}/" +
               $"{(map.IsStandable(tile) ? "바닥있음" : "바닥없음")}" +
               $"{(PathBlockMemory.IsBlocked(tile) ? "/차단기억됨" : "")}";
    }

    public static void RecordBlockMemoryHit() { if (Enabled) _blockMemoryHits++; }
    public static void RecordPawnBakeBlock()  { if (Enabled) _pawnBakeBlocks++; }
    public static void RecordOccupiedBlock()  { if (Enabled) _occupiedBlocks++; }
    public static void RecordBake()           { if (Enabled) _bakes++; }

    static string StageName(int stage) => stage switch
    {
        1 => "비켜서기",
        2 => "차단기억+로컬재계획",
        3 => "차단기억+전역재계획",
        _ => "포기",
    };

    // 자동 설치된 티커가 매 프레임 호출.
    public static void Tick()
    {
        if (!Enabled) return;
        if (Time.time < _nextSummary) return;
        _nextSummary = Time.time + SummaryInterval;

        int total    = _stage1 + _stage2 + _stage3 + _stage4;
        int serious  = _stage2 + _stage3 + _stage4;
        bool beat    = Time.time >= _nextHeartbeat;

        // 1단계(스치는 정체)만 몇 번 있는 건 **정상 상태**다 — 매번 찍으면 콘솔만 더럽다.
        // 실질 문제(2단계 이상)가 있을 때만 찍고, 그 외에는 1분에 한 번 생존 신고만 한다.
        if (serious == 0 && !beat) { Reset(); return; }
        if (beat) _nextHeartbeat = Time.time + HeartbeatInterval;

        // 베이크당 평균 차단 셀 — 절대 수치는 베이크 횟수에 비례하므로 나눠 봐야 의미가 산다.
        float per = _bakes > 0 ? 1f / _bakes : 0f;

        Debug.Log($"[이동진단] 최근 {SummaryInterval:F0}초 — 막힘 {total}회 " +
                  $"(비켜서기 {_stage1} / 로컬재계획 {_stage2} / 전역재계획 {_stage3} / 포기 {_stage4}) · " +
                  $"베이크 {_bakes}회, 회당 차단 셀: 점유 {_occupiedBlocks * per:F1}, " +
                  $"차단기억 {_blockMemoryHits * per:F1}, 폰 {_pawnBakeBlocks * per:F1} · " +
                  $"폰 {PawnMover.All.Count}");

        Reset();
    }

    static void Reset()
    {
        _stage1 = _stage2 = _stage3 = _stage4 = 0;
        _blockMemoryHits = _pawnBakeBlocks = _occupiedBlocks = _bakes = 0;
    }

    // 플레이 시작 시 카운터 초기화 + 요약 티커 설치 (도메인 리로드를 꺼둔 프로젝트 대비).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        Reset();
        _nextSummary   = Time.time + SummaryInterval;
        _nextHeartbeat = Time.time + HeartbeatInterval;

        var go = new GameObject("~MovementDiagnostics") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<MovementDiagnosticsTicker>();

        if (Enabled)
            Debug.Log($"[이동진단] 켜짐 — {SummaryInterval:F0}초마다 요약. " +
                      "끄기: DungeonColony → Toggle Movement Diagnostics");
    }
}

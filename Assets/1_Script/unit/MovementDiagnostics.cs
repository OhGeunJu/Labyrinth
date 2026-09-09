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

    // 포기 상세 로그의 간격(초, 실시간). 억눌린 것은 요약에 숫자로 남는다.
    const float GiveUpLogInterval = 1f;
    static float _lastGiveUpLog = -999f;
    static int   _giveUpSuppressed;

    static int _blockMemoryHits;   // 베이크에서 PathBlockMemory 때문에 막힌 셀 수
    static int _pawnBakeBlocks;    // 베이크에서 다른 폰 때문에 막힌 셀 수
    static int _occupiedBlocks;    // 베이크에서 점유·바위(광역 1m 표) 때문에 막힌 셀 수
    static int _bakes;             // 로컬 윈도우 베이크 횟수 (위 셀 수를 나눠 볼 기준)

    static float _nextSummary;
    static float _nextHeartbeat;

    // **떠 있나 — 숫자로 찍는다**(2026-08-30). 화면으로는 *"떠 있다/닿아 있다"*를 두고 서로
    // 다르게 볼 수 있고, 실제로 그렇게 한 번 어긋났다. 지면에서 몇 m인지와 `isGrounded`를
    // 같이 내면 다툴 것이 없다. **`SampleGroundHeight`로 잰다** — 콜라이더가 아니라 함수라
    // *"무엇에 올라서 있나"*와 무관하게 **진짜 지면**을 답한다.
    public static string DescribeFooting(Vector3 pos, CharacterController cc)
    {
        var map = MapManager.GetForPosition(pos);
        if (map == null) return "";

        float ground = map.SampleGroundHeight(pos);
        float above  = pos.y - ground;
        string g = cc != null ? (cc.isGrounded ? "접지" : "**공중**") : "?";
        return $" · 지면 {above:+0.00;-0.00}m/{g}";
    }

    public static void RecordStage(int stage, string pawnName, Vector3 pos, ref float lastLogTime,
                                   CharacterController cc = null)
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
                  $"@ ({pos.x:F1}, {pos.z:F1}){DescribeFooting(pos, cc)}");
    }

    // 포기(4단계)만 상세 진단 — 왜 못 갔는지는 여기서 갈린다.
    // "목적지 칸 자체가 통행 불가"면 경로가 아예 안 나와 직선 폴백으로 물체에 처박는다.
    // `self` — 자기 콜라이더를 걸러내는 기준. 이름으로 거르면 같은 이름의 **다른 폰**이
    // 막고 있을 때 그게 지워져 진짜 범인이 사라진다.
    public static void LogGiveUp(string pawnName, Vector3 pos, Vector3 goal, bool hasLocalPath,
                                 GameObject self = null, CharacterController cc = null,
                                 int routePoints = -1, Vector3? steer = null)
    {
        if (!Enabled || !LogEvents) return;

        // **한 줄 찍는 값이 싸지 않다** — 이 아래에서 `OverlapSphere`를 쏘고, 칸 열둘의 바위·점유를
        // 되묻고, 스택 트레이스가 붙은 경고를 낸다. 포기는 원래 드문 일이라 그래도 됐는데,
        // **여럿이 같은 벽 앞에서 동시에 포기하면** 그 프레임이 통째로 진단에 먹힌다(막힘의
        // 증상을 재려던 것이 막힘의 원인이 된다). **한 번에 하나만 자세히 찍고 나머지는 센다** —
        // 같은 프레임의 포기 열셋은 서로 다른 사연이 아니다.
        if (Time.unscaledTime - _lastGiveUpLog < GiveUpLogInterval) { _giveUpSuppressed++; return; }
        _lastGiveUpLog = Time.unscaledTime;

        float dist = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(goal.x, 0f, goal.z));

        // **조향 목표를 같이 적는다**(2026-08-31). *"선 자리 통행가능 · 앞을 막은 것 없음 ·
        // 로컬경로 없음"*이라는 **모순처럼 읽히는 세 줄**이 나왔을 때 빠져 있던 조각이
        // 정확히 이것이었다 — 조향 목표가 **로컬 창(40m) 밖**이면 창 가장자리로 접힌
        // 직선 투영을 겨누게 되고, 지하에서 그건 거의 늘 바위 속이다. 최종 목적지와
        // **다른 값**이라 따로 적어야 보인다.
        string route = routePoints < 0 ? "" :
            $" · 라우트 {routePoints}점" +
            (steer.HasValue
                ? $", 조향 ({steer.Value.x:F1}, {steer.Value.z:F1}) " +
                  $"{Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(steer.Value.x, 0f, steer.Value.z)):F1}m " +
                  $"{DescribeTile(steer.Value)}"
                : "");

        Debug.LogWarning($"[이동진단] {pawnName} 포기 @ ({pos.x:F1}, {pos.z:F1}) → " +
                         $"목표 ({goal.x:F1}, {goal.z:F1}) 거리 {dist:F1}m · " +
                         $"로컬경로 {(hasLocalPath ? "있음" : "없음")}{route} · " +
                         $"목표칸 {DescribeTile(goal)}\n" +
                         $"  선 자리: {DescribeTile(pos)}{DescribeFooting(pos, cc)}\n" +
                         $"  앞을 막은 것: {DescribeBlocker(pos, goal, self, cc)}\n" +
                         $"  곧장 그은 선: {DescribePathTiles(pos, goal)}\n" +
                         $"  선 자리 둘레:\n{DescribeNeighborhood(pos)}" +
                         DescribeCorner(pos, goal));
    }

    // **"무언가에 막혔다"를 "무엇에 막혔다"로 바꾼다.** 로컬 경로가 있는데도 못 나아가면
    // 남은 건 물리 충돌뿐인데, 이름과 레이어를 모르면 건물인지 다른 폰인지조차 알 수 없다.
    static string DescribeBlocker(Vector3 pos, Vector3 goal, GameObject self, CharacterController cc)
    {
        Vector3 dir = goal - pos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return "없음";
        dir.Normalize();

        // 진행 방향 바로 앞을 훑는다. 폰 반경(0.5)과 겹치므로 자기 자신도 잡히는데,
        // 그건 `self`로 거른다(이름으로 거르면 같은 이름의 다른 폰까지 지워진다).
        var probe = pos + Vector3.up * 0.8f + dir * 0.8f;
        var hits  = Physics.OverlapSphere(probe, 0.4f, ~0, QueryTriggerInteraction.Ignore);

        // **통과하는 것을 막은 것이라고 적지 않는다**(2026-09-07). `~0`으로 쏘므로 **같은 편
        // 폰까지 걸리는데**, 걸음은 `excludeLayers`로 그들을 그냥 지나간다
        // (`PawnMover.ApplyPawnCollisionRule` — 레이어가 곧 "누구를 통과하는가"다).
        //
        // 그대로 적었더니 습격대가 맵 밖에서 못 움직이는 진짜 원인(세계 밖이 열려 있었다)을
        // 두고 **`앞을 막은 것: Enemy_Goblin`**이 범인처럼 읽혔다. **진단이 거짓말을 하면
        // 없는 사고를 쫓게 된다** — 그래서 지우지 않고 **갈라서** 적는다: 통과하는 것은
        // 통과한다고.
        int exclude = cc != null ? cc.excludeLayers.value : 0;

        var blocks = new System.Text.StringBuilder();
        var passes = new System.Text.StringBuilder();
        foreach (var h in hits)
        {
            if (self != null && h.transform.IsChildOf(self.transform)) continue;

            var sb = (exclude & (1 << h.gameObject.layer)) != 0 ? passes : blocks;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{h.transform.root.name}/{h.name}(레이어 {LayerMask.LayerToName(h.gameObject.layer)})");
        }

        string tail = passes.Length > 0 ? $" · 통과함: {passes}" : "";
        return blocks.Length > 0
            ? blocks + tail
            : "없음(물리적으로 안 막힘 — 조향/경로 문제)" + tail;
    }

    // 폰에서 목표까지 **직선을 그어** 그 위의 칸들을 찍는다. **보이지 않는 점유**(모델보다
    // 큰 건물 footprint)가 길을 끊고 있으면 여기서 `점유됨`으로 바로 드러난다.
    //
    // **경로가 아니다 — 직선이다**(2026-09-05에 이름을 고쳤다). "가는 길 칸"이라고 적어 두었더니
    // 굴에서 **바위가 줄줄이 찍히는 것이 마치 라우트가 벽을 뚫는 것처럼** 읽혔다. 굴은 굽어
    // 있으므로 직선이 바위를 지나는 것은 **정상이다** — 여기서 볼 것은 *어떤 종류의 것이
    // 사이에 있나*이지 *경로가 어디로 가나*가 아니다(그건 `라우트 N점`과 `조향`이 답한다).
    static string DescribePathTiles(Vector3 pos, Vector3 goal)
    {
        var map = MapManager.GetForPosition(goal);
        if (map == null) return "맵없음";

        var from = map.WorldToTile(pos);
        var to   = map.WorldToTile(goal);

        var sb   = new System.Text.StringBuilder();
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        steps = Mathf.Min(steps, 12);   // 콘솔 한 줄을 넘기지 않는다
        if (steps == 0) return "같은 칸";

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            var tile = new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                                      Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)));
            if (sb.Length > 0) sb.Append(' ');

            // **바위를 같이 찍는다**(2026-08-30). `IsPassable`은 점유(건물)만 답하므로
            // 지하에서는 **통짜 바위도 "통행"으로 찍혔다** — 세계의 4분의 3이 바위인데
            // 진단이 그걸 한 번도 말하지 않으니, 벽에 막힌 것이 죄다 *"조향 문제"*로 보였다.
            string state = map.IsOccupied(tile) ? "**점유**"
                         : RockMap.IsRock(tile)                    ? "**바위**"
                         : "통행";
            sb.Append($"[{tile.x},{tile.y}]{state}");
        }
        return sb.ToString();
    }

    // 그 좌표의 타일이 어떤 상태인지 — 통행/바닥/차단기억
    // ── 둘레 3×3을 그림으로 ────────────────────────────────────────────────
    //
    // **칸 하나하나를 글로 적으면 아홉 줄이 되고, 그러면 아무도 안 읽는다.** 눈으로 모양이
    // 보여야 *"주머니에 갇혔나"*가 한눈에 들어온다.
    //
    // `.` 걸을 수 있다 · `#` 바위 · `X` 점유(건물) · `_` 바닥 없음 · `!` 차단기억 · `@` 나
    static string DescribeNeighborhood(Vector3 world)
    {
        var map = MapManager.GetForPosition(world);
        if (map == null) return "    맵없음\n";

        var c  = map.WorldToTile(world);
        var sb = new System.Text.StringBuilder();

        // 북(+z)이 위로 오게 그린다 — 화면과 같은 방향이라야 눈으로 맞춰 볼 수 있다.
        for (int dz = 1; dz >= -1; dz--)
        {
            sb.Append("    ");
            for (int dx = -1; dx <= 1; dx++)
            {
                var t = new Vector2Int(c.x + dx, c.y + dz);
                sb.Append(dx == 0 && dz == 0 ? '@' : Mark(map, t)).Append(' ');
            }
            sb.Append(dz == 1 ? "  (위가 북)" : "").Append('\n');
        }
        return sb.ToString();
    }

    static char Mark(MapManager map, Vector2Int t)
    {
        if (RockMap.IsRock(t))                  return '#';
        if (map.IsOccupied(t))  return 'X';
        if (!map.IsStandable(t))                return '_';
        if (PathBlockMemory.IsBlocked(t))       return '!';
        return '.';
    }

    // ── 대각선 한 칸이면 모서리 둘을 찍는다 ─────────────────────────────────
    //
    // **대각선은 직교 이웃 둘이 다 열려야 지나간다**(`PathGrid.Enterable`) — 안 그러면 몸이
    // 두 면 사이에 낀다. 그런데 진단은 **출발 칸과 목표 칸만** 찍었다: 둘 다 멀쩡한데 경로가
    // 없으면 *"양쪽 다 통행인데 길이 없다"*는 모순처럼 읽히고, **정작 막은 것은 그 사이의
    // 모서리 둘**이다(2026-09-05에 그 그림으로 세 번 포기한 로그를 보고 넣었다).
    static string DescribeCorner(Vector3 pos, Vector3 goal)
    {
        var map = MapManager.GetForPosition(goal);
        if (map == null) return "";

        var a = map.WorldToTile(pos);
        var b = map.WorldToTile(goal);

        int dx = b.x - a.x, dz = b.y - a.y;
        if (Mathf.Abs(dx) != 1 || Mathf.Abs(dz) != 1) return "";

        var side1 = new Vector2Int(a.x + dx, a.y);
        var side2 = new Vector2Int(a.x, a.y + dz);

        bool open1 = map.IsWorkable(side1);
        bool open2 = map.IsWorkable(side2);

        return $"  대각 한 칸 — 모서리 [{side1.x},{side1.y}]{(open1 ? "열림" : "**막힘**")} · " +
               $"[{side2.x},{side2.y}]{(open2 ? "열림" : "**막힘**")}" +
               (open1 && open2 ? "" : " → **대각선은 둘 다 열려야 지나간다**") + "\n";
    }

    static string DescribeTile(Vector3 world)
    {
        var map = MapManager.GetForPosition(world);
        if (map == null) return "맵없음";

        var tile = map.WorldToTile(world);
        return $"[{tile.x},{tile.y}] " +
               $"{(map.IsOccupied(tile) ? "점유됨" : "통행가능")}/" +
               $"{(map.IsStandable(tile) ? "바닥있음" : "바닥없음")}" +
               $"{(RockMap.IsRock(tile) ? "/**바위속**" : "")}" +
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
                  $"베이크 {_bakes}회, 회당 차단 셀: 점유·바위 {_occupiedBlocks * per:F1}, " +
                  $"차단기억 {_blockMemoryHits * per:F1}, 폰 {_pawnBakeBlocks * per:F1} · " +
                  $"폰 {PawnMover.All.Count}" +
                  (_giveUpSuppressed > 0 ? $" · 포기 상세 생략 {_giveUpSuppressed}건" : ""));

        Reset();
    }

    static void Reset()
    {
        _stage1 = _stage2 = _stage3 = _stage4 = 0;
        _blockMemoryHits = _pawnBakeBlocks = _occupiedBlocks = _bakes = 0;
        _giveUpSuppressed = 0;
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

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PawnMover : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 4f;
    public float stopDistance = 0.12f;

    [Header("Ground")]
    public float gravity = -20f;
    public float groundStickForce = -2f;
    [Tooltip("이 높이 아래로 떨어지면 마지막 접지 위치로 복귀 (공허 던전 맵 낙하 안전망)")]
    public float fallRecoveryY = -10f;

    CharacterController _cc;
    float _verticalVel;
    Vector3 _lastGroundedPos;

    // 내비게이션: 코스 라우트(L2 장거리) + 유닛 중심 로컬 정밀 경로
    Vector3       _finalGoal;
    List<Vector3> _coarseRoute;
    int           _coarseIndex;
    List<Vector3> _localPath;
    int           _localIndex;
    Vector3       _replanAnchor;
    float         _replanDriftSqr;

    Vector3 _currentTarget;
    bool _hasTarget;
    float _baseMoveSpeed;
    float _mobilityMult = 1f;   // 절뚝임/기어가기 등 이동 속도 배율
    UnitStats _stats;
    Inventory _inventory;
    StatusEffectSystem _sfx;
    UnitBrain _brain;
    bool _isMinion;

    // 기절 중이면 BT가 정지해 이동 명령을 안 내리지만, MoveController 우클릭 등
    // BT 밖 경로가 SetMoveTarget을 호출할 수 있어 이동 차단이 필요하다.
    bool IsKnockedOut => _brain != null && _brain.Blackboard != null && _brain.Blackboard.IsKnockedOut;

    [Header("내비게이션")]
    [Tooltip("최종 목적지 도착 판정 거리")]
    public float arriveDistance = 0.5f;
    [Tooltip("코스 웨이포인트 통과 판정 거리")]
    public float coarseReachDistance = 4f;
    [Tooltip("이만큼 이동하면 로컬 윈도우를 다시 베이크(재계획)")]
    public float replanDistance = 15f;
    [Tooltip("새 목표가 기존 목표와 이 거리 안이면 재계획 생략 (매 틱 같은 목표 재지정 시 재베이크 방지)")]
    public float goalEpsilon = 1.5f;
    [Tooltip("공허 낭떠러지 가드 전방 확인 거리(m). 던전형 맵에서 이만큼 앞이 바닥 밖이면 정지")]
    public float ledgeLookahead = 0.6f;

    [Header("장애물 회피")]
    [Tooltip("이 거리 안의 다른 폰을 피해 조향한다. 0이면 회피 없음")]
    public float avoidRadius = 1.5f;
    [Tooltip("회피 조향의 세기. 클수록 크게 돌아간다")]
    public float avoidStrength = 1f;
    [Tooltip("이 시간(초)마다 실제 이동량을 검사해 막혔는지 판단")]
    public float blockedCheckTime = 0.35f;
    [Tooltip("막혔을 때 옆으로 비켜 걷는 시간(초)")]
    public float sidestepTime = 0.6f;

    [Tooltip("목적지에 이만큼(초) 동안 한 번도 더 가까워지지 못하면 포기한다. " +
             "**움직이는데 안 가까워지는** 경우를 잡는 그물이라, 정직한 우회를 끊지 않게 넉넉히 둔다.")]
    public float goalStallTime = 8f;

    // 회피/정체 상태
    Vector3 _progressAnchor;
    float   _progressTimer;
    int     _blockedCount;
    Vector3 _sidestepDir;
    float   _sidestepTimer;
    float   _bestGoalDist;      // 지금까지 목적지에 가장 가까웠던 거리
    float   _noGainTimer;       // 그 기록을 못 깬 채로 흐른 시간
    float   _lastDiagLogTime = -999f;   // 계측 로그 폰당 1초 제한

    [Header("Athletics 성장")]
    [SerializeField] float athleticsGrowthRate = 0.005f;

    [Tooltip("짐을 지고 걸을 때 근력 성장 속도. 한도 대비 적재 비율이 곱해진다.")]
    [SerializeField] float strengthGrowthRate = 0.003f;

    // **폰이 걸어 오를 수 있는 수직 턱(m).** `CharacterController.stepOffset`이 그 값인데,
    // 길찾기도 같은 값을 알아야 한다(모르면 못 오를 기단 위로 경로를 낸다). **프리팹이 각자
    // 들면 반드시 갈린다** — 그래서 여기 상수가 이기고 `Awake`가 컨트롤러에 밀어 넣는다.
    //
    // **이 값이 곧 계단의 기울기다**(`BuildModeManager` 계단이 한 칸에 이만큼 내려간다).
    // 컨트롤러 기본값 0.3으로 뒀더니 2m 기단에 **계단이 일곱 칸**이었다 — 오르기는 하는데
    // 마당보다 계단이 길다. 사람 무릎 높이(0.5m)면 한 칸에 한 단이 그럴듯하고 2m가 넉 칸이다.
    // 캡슐 높이(1.8m)에 견주면 여전히 한참 낮아 컨트롤러가 싫어할 값도 아니다.
    public const float StepUp = 0.5f;

    // 자연 지형에서 한 칸(1m) 오를 때 허용되는 높이차(m). 비탈은 턱이 아니라서
    // `slopeLimit`(45도)가 판단한다 — 1m당 1m가 그 각이다.
    public const float SlopeRise = 1.0f;

    // **폰의 몸통 반경(m).** 프리팹에는 0.5로 저작돼 있었는데, 그러면 **지름이 정확히 한 칸**이라
    // **한 칸짜리 틈은 어디도 못 지나간다** — 오두막 문간이 딱 그 자리였다(틈 1.04m, 몸 1.00m).
    // 복도든 건물 사이든 한 칸 틈은 타일 격자에서 가장 흔한 모양이라, 그게 안 되면
    // *"왜 여기로 안 가지"*가 끝없이 나온다.
    //
    // 0.35면 한 칸 틈에 양쪽 0.15씩 남아 조향이 흔들려도 빠져나간다. **길찾기가 이미
    // "폰은 타일 안에 들어간다"를 전제하고 있었다**(`LocalGridManager.BakeGrid`가 반경
    // 부풀림을 타일보다 큰 에이전트에만 쓴다) — 몸만 그 전제를 안 지키고 있었다.
    public const float BodyRadius = 0.35f;

    // **뛰어내릴 수 있는 높이(m).** 오를 수 있는 높이(`StepUp`)와 **다른 축이다** —
    // 사람은 무릎 높이까지밖에 못 올라가도 제 키만큼은 뛰어내린다.
    //
    // 한동안 이 둘을 같은 값으로 뒀다가, *"내려갈 때도 이어진 길로"*로 바꾸면서 아예
    // 막아 봤다. 그러면 **턱 하나 때문에 기단을 통째로 돌아 나간다** — 무릎 높이를
    // 못 내려가서 스무 칸을 도는 것은 보기에도 틀렸다.
    //
    // 비대칭이 답이다: **내려가는 것은 너그럽게, 올라오는 것은 계단으로.** 그러면
    // 자잘한 턱은 그냥 내려가고 진짜 벼랑에서만 계단을 찾는다.
    //
    // **이 값이 계단의 뜻을 정한다** — 크게 잡을수록 계단 없이도 잘 다니고, 작게
    // 잡을수록 어디로 오르내릴지를 플레이어가 짓게 된다.
    public const float DropDown = 1.2f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _cc.stepOffset = StepUp;
        _cc.radius     = BodyRadius;
        _currentTarget = transform.position;
        _lastGroundedPos = transform.position;
        _baseMoveSpeed = moveSpeed;
        _replanDriftSqr = replanDistance * replanDistance;

        ApplyPawnCollisionRule();
    }

    // 같은 레이어(= 같은 편)끼리는 서로 통과한다.
    // 폰 지름이 타일 한 칸과 같아서, 아군이 몸으로 길을 막으면 옆으로 비킬 여유가 없다 —
    // ㄷ자 포위·정면 교착이 여기서 나온다. 통과시키면 그 부류가 원인 채로 사라진다.
    // 적(다른 레이어)과는 그대로 충돌하므로 몸으로 라인을 막는 전술은 살아 있다.
    // ── 어느 레이어에 서야 하나 ───────────────────────────────────────────
    //
    // **레이어가 곧 "누구를 통과하는가"다**(같은 레이어끼리 통과, 다른 레이어와 충돌).
    // 그래서 **세력이 바뀌면 레이어도 같이 옮겨야 한다** — 회유해 온 산적을 그대로 두면
    // 내 폰과 몸이 부딪히고(같은 편인데) 적끼리는 통과한다(남이 됐는데).
    //
    // 갈리는 축은 **내 편인가** 하나다. 세력이 아홉인데 레이어는 둘이라 더 잘게 나눌 수
    // 없고, 나눌 이유도 없다 — 통과 여부를 정하는 물음이 그것 하나다.
    //
    // **자식까지 옮긴다.** 콜라이더가 자식에 달린 프리팹이 있고, 루트만 옮기면 그 콜라이더가
    // 옛 레이어에 남아 **몸의 절반만 규칙을 따른다.**
    public static void ApplyLayer(GameObject go, bool mine)
    {
        int want = LayerMask.NameToLayer(mine ? "Player" : "Enemy");
        if (want < 0 || go == null) return;

        int from = go.layer;
        if (from == want) return;

        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            if (t.gameObject.layer == from) t.gameObject.layer = want;   // 초상화 등 딴 레이어는 둔다

        go.GetComponent<PawnMover>()?.ApplyPawnCollisionRule();
    }

    public void ApplyPawnCollisionRule()
    {
        if (_cc == null) return;

        // Default(0)에는 지면·잡오브젝트가 섞여 있어 제외하면 바닥을 뚫는다.
        // 유닛은 반드시 전용 레이어(Player/Enemy 등)에 둘 것.
        //
        // **말없이 넘어가면 안 된다**(2026-08-28). 여기서 조용히 빠져나가면 그 폰은
        // *"아무와도 안 통과"*가 되고, 더 나쁘게는 `IsBlockedBy`가 레이어 비교라
        // **같은 Default끼리는 그래프에서 서로를 못 본다** — 몸은 부딪히는데 길찾기는
        // 모르는 상태다(폰이 서로를 밀고 선다). 화면에는 "왜 안 비켜"로만 보인다.
        int layer = gameObject.layer;
        if (layer == 0)
        {
            Debug.LogWarning($"[이동] `{name}`이 `Default` 레이어에 있다 — 폰 통과 규칙이 " +
                             "통째로 안 걸린다(같은 레이어끼리 그래프에서 서로를 못 본다). " +
                             "전용 레이어(Player/Enemy 등)로 옮길 것.");
            return;
        }

        _cc.excludeLayers = 1 << layer;
    }

    // other가 나를 물리적으로 막는가 — 길찾기 베이크가 같은 규칙을 쓰도록 공개한다.
    // (물리에서 통과하는 상대를 그래프에서 벽으로 굽으면 헛돌게 된다.)
    public bool IsBlockedBy(PawnMover other)
        => other != null && other != this && other.gameObject.layer != gameObject.layer;

    public void Init(UnitData data)
    {
        moveSpeed      = data.MoveSpeed;
        _baseMoveSpeed = data.MoveSpeed;
    }

    public void SetStats(UnitStats stats, Inventory inventory)
    {
        _stats     = stats;
        _inventory = inventory;
        _sfx       = GetComponent<StatusEffectSystem>();
        _brain     = GetComponent<UnitBrain>();
        _isMinion  = _brain?.Data is MinionData;
    }

    public void SetMoveTarget(Vector3 worldPoint)
    {
        // 기절 중엔 이동 명령 무시 (기상하면 IsKnockedOut이 풀려 복귀 이동은 정상 동작)
        if (IsKnockedOut) return;

        // **갇혀 있거나 업혀 있으면 제 발로 안 간다.**
        //
        // BT는 이미 `UnitBrain.Update`에서 막았지만 **직접 명령은 BT를 안 거친다** —
        // 우클릭 이동은 `MoveController`가 여기로 곧장 들어온다. 그래서 우리 폰을 우리에
        // 가둬 놓고 우클릭하면 갇힌 채로 걸어 나갔다(`Imprisonment.Hold`가 매 프레임 도로
        // 끌어와 부들부들 떨었다). 막는 자리는 기절과 같은 여기다.
        if (_brain?.Blackboard != null &&
            (_brain.Blackboard.IsPrisoner || _brain.Blackboard.CarriedBy != null)) return;

        // 이동 명령 = 일어난다. 누운 자세(침대 위)에서 경로를 잡지 않도록 먼저 내려온다.
        if (IsSleeping) SetSleeping(false);

        worldPoint.y = transform.position.y;

        // **설 수 없는 칸은 목적지가 못 된다.** 스냅은 **여기 한 곳**에서 한다 — 이 함수가
        // 이동으로 들어오는 유일한 문이라, 부르는 쪽마다 챙기게 하면 언젠가 한 경로가 빠뜨린다.
        worldPoint = SnapToStandable(worldPoint);

        // 목표가 거의 동일하고 이미 이동 중이면 재계획 생략 (재베이크 방지).
        // **스냅한 뒤에 비교한다** — 건물을 계속 겨누는 명령이 매 틱 들어와도 같은 칸으로
        // 접히므로 재계획이 안 돈다.
        if (_hasTarget && HorizDistSqr(worldPoint, _finalGoal) < goalEpsilon * goalEpsilon)
            return;

        _finalGoal   = worldPoint;
        _coarseRoute = Pathfinder.ComputeCoarseRoute(transform.position, worldPoint);
        _coarseIndex = 0;
        _hasTarget   = true;
        ResetProgress();
        ReplanLocal();
    }

    // **못 서는 자리를 겨누면 폰은 영영 도착하지 못한다.**
    //
    // 건물 코앞을 우클릭하면 그 칸이 건물 발자국(통행 불가)일 때가 흔하다. 그러면 도착
    // 판정(`arriveDistance`)에 절대 안 걸리고, 벽에 막힐 때마다 `RememberBlockedSpot`이 그
    // 자리를 잠시 막아 플래너가 **건물을 빙 돌아** 반대편으로 보낸다 — 거기서도 막히고,
    // 3초 뒤 옛 기억이 풀려 도로 온다. **폰은 내내 움직이므로** 정체 감시(`TrackProgress`)에
    // 걸리지도 않아, 멈추지도 포기하지도 않고 건물 둘레를 도는 그림이 된다.
    //
    // 답은 `MapManager.FindWorkableNear` — **작업 자리(`WorkSpot.TileFor`)가 쓰는 그것**이다.
    // "폰이 설 수 있는 칸"을 묻는 곳이 둘이면 한쪽만 청사진 발자국이나 공허를 알게 된다.
    //
    // **설 수 있는 칸이면 누른 자리 그대로 둔다** — 칸 중심으로 당기면 작업 자리·집결 자리처럼
    // 칸 안의 특정 지점을 겨눈 명령이 전부 어긋난다.
    Vector3 SnapToStandable(Vector3 point)
    {
        var map = MapManager.GetForPosition(point);
        if (map == null) return point;

        var tile = map.WorldToTile(point);
        if (map.IsWorkable(tile)) return point;

        var near = map.FindWorkableNear(tile);
        if (near == tile) return point;      // 둘레까지 다 막혔다 — 여기서 더 해 줄 것이 없다

        var snapped = map.TileToWorld(near);
        snapped.y = point.y;
        return snapped;
    }

    public void StopMovement()
    {
        _hasTarget   = false;
        _coarseRoute = null;
        _localPath   = null;
    }

    // ── 눕기(침대 휴식) ──────────────────────────────────────────────
    // 누워 있는 동안은 CharacterController를 끄고 위치·회전을 침대에 고정한다.
    // (중력/조향이 계속 돌면 침대 위에서 미끄러지거나 파묻힌다.)
    // 일어날 때는 눕기 직전 서 있던 자리로 되돌린다 — 침대 콜라이더 위에서 깨어나 끼는 것 방지.
    public bool IsSleeping { get; private set; }

    Vector3    _wakePosition;
    Quaternion _wakeRotation;

    // ── 누워 있는 동안의 클릭 판정 ────────────────────────────────────
    //
    // **`CharacterController`를 끄면 폰에게 콜라이더가 하나도 안 남는다.** 그게 유일한
    // 콜라이더라 레이캐스트가 그대로 통과하고, 침대에 누운 사람은 **좌클릭도 우클릭도
    // 안 됐다** — 눌리는 것은 늘 침대였다. 그래서 다친 포로를 살펴볼 수도, [먹인다]·
    // [끌어낸다]를 띄울 수도 없었다.
    //
    // 대신 **트리거 캡슐 하나를 눕는 동안만 켠다**:
    //  · 트리거라 아무것도 막지 않는다 — `CharacterController`를 끈 이유(침대·바닥을 뚫고
    //    되밀린다)는 물리 충돌이지 레이캐스트가 아니다.
    //  · **몸통 크기라 침대보다 작다** — 가장자리를 겨누면 침대가, 몸을 겨누면 사람이 눌린다.
    //    "각도를 맞추면 침대도 눌린다"가 규칙이 아니라 **생김새**에서 나온다.
    //
    // **탐지에는 안 걸린다** — `FactionScan`은 콜라이더가 아니라 `Faction.All`을 훑는다
    // (2026-08-12에 `OverlapSphere`를 버렸다). 자는 폰이 적에게 잡히는지 여부는 예전 그대로다.
    CapsuleCollider _lyingPick;

    void EnsureLyingPick()
    {
        if (_lyingPick != null) return;

        _lyingPick = gameObject.AddComponent<CapsuleCollider>();
        _lyingPick.isTrigger = true;
        _lyingPick.direction = 1;      // 누우면 몸의 긴 축이 로컬 Y다
        _lyingPick.height    = 1.8f;
        _lyingPick.radius    = 0.35f;
        // 피벗이 발밑이므로 몸통 한가운데로 올린다 — 원점에 두면 캡슐이 발끝에 걸쳐
        // **몸의 절반이 클릭 판정 밖**에 남는다.
        _lyingPick.center    = new Vector3(0f, PawnPlacement.CenterOffset(gameObject), 0f);
        _lyingPick.enabled   = false;
    }

    public void SetSleeping(bool sleeping, Vector3 lyingPos = default, Quaternion lyingRot = default)
    {
        if (sleeping == IsSleeping) return;

        EnsureLyingPick();
        _lyingPick.enabled = sleeping;

        if (sleeping)
        {
            StopMovement();
            _wakePosition = transform.position;
            _wakeRotation = transform.rotation;

            _cc.enabled = false;

            // **눕히는 자리는 몸통 기준이다**(`Bed.SleepPosition` = 침대 윗면 + `lieHeight`).
            // 피벗은 발밑에 있으므로(`center.y = height/2`) 그대로 놓으면 **발만 침대에 얹히고
            // 몸은 옆으로 뻗는다** — 누운 방향(`lyingRot`)으로 몸 한가운데만큼 비켜 놓는다.
            transform.SetPositionAndRotation(
                lyingPos - lyingRot * Vector3.up * PawnPlacement.CenterOffset(gameObject),
                lyingRot);
            IsSleeping = true;
        }
        else
        {
            transform.SetPositionAndRotation(_wakePosition, _wakeRotation);
            _cc.enabled  = true;
            _verticalVel = 0f;
            IsSleeping   = false;
        }
    }

    // 유닛 위치 중심 윈도우를 즉석 베이크해 조향 목표까지 로컬 정밀 경로 생성.
    void ReplanLocal()
    {
        Vector3 pos   = transform.position;
        _replanAnchor = pos;

        Vector3 steer = SelectSteeringTarget(pos);
        var local     = Pathfinder.FindLocalPath(pos, steer, this);   // 다른 폰도 장애물로 넣어 우회

        if (local != null && local.Count > 0)
        {
            _localPath  = local;
            _localIndex = 0;
            _currentTarget = WithY(local[0]);
        }
        else
        {
            // **한 발짝도 못 가는 경우다.** 여기까지 오면 `PathGrid`가 부분 경로조차
            // 못 낸 것이라(시작 자리가 곧 가장 가까운 자리), 직선으로 밀어붙일 이유가 없다 —
            // 그 직선이 곧 **벽을 미는 동작**이었다. 제자리에 두고 포기 진단이 처리하게 한다.
            _localPath  = null;
            _localIndex = 0;
            _currentTarget = WithY(pos);
        }
    }

    // 이미 지난 코스 웨이포인트는 통과하고, 다음으로 향할 조향 목표 반환.
    // 근접(reached)했거나, 다음 웨이포인트에 더 가까워졌으면(passed=추월) 인덱스 전진.
    // → 로컬 우회로 웨이포인트를 정확히 밟지 못해도 뒤로 돌아가지 않는다.
    Vector3 SelectSteeringTarget(Vector3 pos)
    {
        if (_coarseRoute == null || _coarseRoute.Count == 0) return _finalGoal;
        while (_coarseIndex < _coarseRoute.Count - 1)
        {
            Vector3 cur  = _coarseRoute[_coarseIndex];
            Vector3 next = _coarseRoute[_coarseIndex + 1];
            bool reached = HorizDistSqr(pos, cur) < coarseReachDistance * coarseReachDistance;
            bool passed  = HorizDistSqr(pos, next) < HorizDistSqr(cur, next);
            if (reached || passed) _coarseIndex++;
            else break;
        }

        // **마지막 구간은 코스 웨이포인트가 아니라 진짜 목적지를 향한다.**
        //
        // 코스 라우트는 메소(4m)·L2(100m) **셀 중심**들의 나열이라, 마지막 점이 목적지와
        // 최대 셀 반쪽만큼 어긋난다. 그걸 그대로 조향 목표로 쓰면 폰은 **셀 중심에 서서 멈추고**,
        // `AdvanceWaypoint`가 다시 계획해도 같은 셀 중심이 나와 제자리를 맴돈다 —
        // 그래서 목적지를 2~3m 앞에 두고 영영 못 좁혔다(연구대에 2.6m 앞에서 멈추던 것,
        // "물리적으로 안 막힘 / 경로 있음 / 가는 길 전부 통행"이 나온 이유가 이것이다).
        //
        // 라우트는 **어느 복도로 갈지**를 정하는 안내지 도착점이 아니다. 마지막 칸은 로컬
        // 정밀 경로(Theta*)가 알아서 잇는다.
        if (_coarseIndex >= _coarseRoute.Count - 1) return _finalGoal;

        return _coarseRoute[_coarseIndex];
    }

    // 현재 웨이포인트 도달 시 다음 단계 진행.
    void AdvanceWaypoint()
    {
        // 로컬 경로의 다음 점
        if (_localPath != null && _localIndex + 1 < _localPath.Count)
        {
            _localIndex++;
            _currentTarget = WithY(_localPath[_localIndex]);
            return;
        }

        Vector3 pos = transform.position;

        // 최종 목적지 도착
        if (HorizDistSqr(pos, _finalGoal) <= arriveDistance * arriveDistance)
        {
            StopMovement();
            return;
        }

        // 아직 멀다 — 다음 윈도우로 재계획
        ReplanLocal();
        if (!IsStuck(pos)) return;

        // 로컬이 막힘 — 전역 재계획(현재 위치 기준 코스 라우트 갱신: 메소 벽 지식 반영)
        _coarseRoute = Pathfinder.ComputeCoarseRoute(pos, _finalGoal);
        _coarseIndex = 0;
        ReplanLocal();

        // 그래도 제자리면 정지 (제자리 떨림/무한 루프 방지)
        if (IsStuck(pos)) StopMovement();
    }

    // 던전형 맵(FloorRequired)에서 다음 스텝이 바닥 밖이면 true.
    // 현재 타일이 바닥일 때만 발동 — 어쩌다 바닥 밖에 놓인 유닛의 복귀 이동은 막지 않는다.
    bool IsVoidAhead(Vector3 pos, Vector3 dir)
    {
        var map = MapManager.Instance;
        if (map == null || !map.FloorRequired) return false;
        return map.IsFloored(pos) && !map.IsFloored(pos + dir * ledgeLookahead);
    }

    bool IsStuck(Vector3 pos)
    {
        Vector3 t = _currentTarget; t.y = pos.y;
        return (t - pos).sqrMagnitude <= stopDistance * stopDistance;
    }

    // ── 지역 회피 & 정체 탈출 ────────────────────────────────────────
    // 길찾기 그래프는 벽(정적)만 안다. 다른 폰·침대·창고처럼 그래프에 없는 장애물은
    // 경로가 그대로 관통하게 나오고, CharacterController가 막혀 제자리에서 밀기만 한다.
    // → ① 앞쪽 폰은 조향 단계에서 비켜 간다 ② 그래도 안 움직이면 옆으로 비켜섰다가 재계획한다.

    static readonly List<PawnMover> _all = new();

    // 길찾기 베이크가 다른 폰을 장애물로 굽는 데 쓴다.
    public static IReadOnlyList<PawnMover> All => _all;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    // 진행 방향 기준 오른쪽(수평)
    static Vector3 RightOf(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x);

    Vector3 Steer(Vector3 pos, Vector3 desired)
    {
        Vector3 steer = desired;

        // 막혀서 비켜서는 중 — 측면 성분을 크게 준다
        if (_sidestepTimer > 0f)
        {
            _sidestepTimer -= Time.deltaTime;
            steer = (desired * 0.35f + _sidestepDir).normalized;
        }

        Vector3 avoid = AvoidNearbyPawns(pos, desired);
        if (avoid != Vector3.zero)
            steer = (steer + avoid * avoidStrength).normalized;

        return steer.sqrMagnitude > 0.001f ? steer : desired;
    }

    // 앞쪽에 있는 다른 폰에서 벗어나는 조향 성분.
    // 측면 회피는 **항상 자기 기준 오른쪽** — 마주 오는 둘이 서로 반대 방향으로 갈라져
    // "서로 맞대고 아무도 못 가는" 교착이 풀린다(우측통행). 근접 시 밀어내는 성분도 섞는다.
    Vector3 AvoidNearbyPawns(Vector3 pos, Vector3 desired)
    {
        if (avoidRadius <= 0f) return Vector3.zero;

        // 목적지 코앞에서는 회피하지 않는다 — 도착 지점에 모여야 하는 경우(창고·작업대·침대 옆)
        // 끝까지 서로 밀어내면 아무도 도착하지 못한다.
        if (HorizDistSqr(pos, _finalGoal) <= arriveDistance * arriveDistance * 4f)
            return Vector3.zero;

        // 교전 상대는 피하지 않는다 — 붙어야 때린다
        var combatTarget = _brain != null && _brain.Blackboard != null ? _brain.Blackboard.Target : null;

        Vector3 right = RightOf(desired);
        Vector3 avoid = Vector3.zero;

        foreach (var other in _all)
        {
            if (other == this || other == null || other.IsSleeping) continue;
            if (combatTarget != null && other.gameObject == combatTarget) continue;

            Vector3 d = other.transform.position - pos;
            d.y = 0f;

            float distSqr = d.sqrMagnitude;
            if (distSqr > avoidRadius * avoidRadius || distSqr < 0.0001f) continue;

            float dist = Mathf.Sqrt(distSqr);
            Vector3 dir = d / dist;

            float ahead = Vector3.Dot(dir, desired);
            if (ahead <= 0.2f) continue;   // 뒤·옆으로 스치는 상대는 무시

            float weight = (1f - dist / avoidRadius) * ahead;
            avoid += (right + -dir * 0.5f) * weight;
        }

        return Vector3.ClampMagnitude(avoid, 1f);
    }

    // 실제로 나아가고 있는지 감시. 밀리기만 하면 단계적으로 탈출을 시도한다.
    void TrackProgress(Vector3 pos, Vector3 desired, float speed)
    {
        _progressTimer += Time.deltaTime;
        if (_progressTimer < blockedCheckTime) return;

        float moved    = Mathf.Sqrt(HorizDistSqr(pos, _progressAnchor));
        float expected = speed * _progressTimer * 0.35f;   // 기대치의 35%도 못 가면 막힌 것

        if (moved >= expected) _blockedCount = 0;
        else                   OnBlocked(pos, desired);

        TrackGoalGain(pos, _progressTimer);

        _progressAnchor = pos;
        _progressTimer  = 0f;
    }

    // **움직이는 것과 가까워지는 것은 다르다.**
    //
    // 위의 감시는 *얼마나 움직였나*만 잰다. 그래서 도달할 수 없는 자리를 겨눈 폰이 건물
    // 둘레를 도는 동안에는 **한 번도 안 걸린다** — 부지런히 걷고 있으니까. 멈추지도
    // 포기하지도 않는 그 왕복이 이 그물이 잡는 것이다.
    //
    // **기록을 깰 때만 시계를 되돌린다**(가장 가까웠던 거리). 지금 거리와 견주면 제자리에서
    // 반 걸음씩 오가는 것도 "가까워졌다"가 되어 영영 안 걸린다.
    //
    // 여유(`GoalGainEpsilon`)를 두는 것은 조향의 미세한 떨림이 기록을 조금씩 갱신하며
    // 시계를 무한정 되돌리는 것을 막기 위해서다. 시간은 넉넉히 준다 — **큰 건물을 빙 도는
    // 정직한 우회도 한동안은 안 가까워진다.**
    const float GoalGainEpsilon = 0.25f;

    void TrackGoalGain(Vector3 pos, float elapsed)
    {
        float dist = Mathf.Sqrt(HorizDistSqr(pos, _finalGoal));

        if (dist < _bestGoalDist - GoalGainEpsilon)
        {
            _bestGoalDist = dist;
            _noGainTimer  = 0f;
            return;
        }

        _noGainTimer += elapsed;
        if (_noGainTimer < goalStallTime) return;

        // 갈 수 없는 곳이다. **BT가 다음 행동을 고르게 놓아 준다** — 막혔을 때의 마지막
        // 단계와 같은 자리이고, 여기서 계속 붙들면 그 폰은 다른 일을 영영 못 한다.
        MovementDiagnostics.LogGiveUp(_brain != null ? _brain.UnitName : name,
                                      pos, _finalGoal, _localPath != null);
        StopMovement();
    }

    // 막혔을 때의 대응. **좌우로 번갈아 비켜서지 않는다** — U자로 둘러싸인 자리에서
    // 그러면 한쪽으로 갔다가 되돌아오는 진동만 반복하고 절대 빠져나가지 못한다.
    // 한 번만 비켜서 보고, 안 되면 그 자리를 막힌 곳으로 기억시켜 **플래너에게 우회를 맡긴다.**
    void OnBlocked(Vector3 pos, Vector3 desired)
    {
        _blockedCount++;

        MovementDiagnostics.RecordStage(_blockedCount,
                                        _brain != null ? _brain.UnitName : name,
                                        pos, ref _lastDiagLogTime);

        switch (_blockedCount)
        {
            case 1:   // 스치는 정체 — 한 번 비켜서 본다
                _sidestepDir   = RightOf(desired);
                _sidestepTimer = sidestepTime;
                break;

            case 2:   // 막힌 자리를 기억시키고 경로를 다시 뽑는다 (그래프가 우회로를 낸다)
                _sidestepTimer = 0f;
                RememberBlockedSpot(pos, desired);
                ReplanLocal();
                break;

            case 3:   // 로컬 윈도우 밖으로 돌아야 하는 규모 — 전역 경로부터 다시
                RememberBlockedSpot(pos, desired);
                _coarseRoute = Pathfinder.ComputeCoarseRoute(pos, _finalGoal);
                _coarseIndex = 0;
                ReplanLocal();
                break;

            default:  // 포기 — BT가 다른 행동을 고르거나 다음 틱에 다시 명령한다
                MovementDiagnostics.LogGiveUp(_brain != null ? _brain.UnitName : name,
                                              pos, _finalGoal, _localPath != null);
                StopMovement();
                break;
        }
    }

    // 앞을 막고 있는 자리를 잠시 통행 불가로 기록. 내 발밑이 아니라 **가려던 쪽**을 막는다.
    // 목적지 코앞은 제외 — 도착 지점 자체를 막아버리면 영영 못 간다.
    void RememberBlockedSpot(Vector3 pos, Vector3 desired)
    {
        Vector3 spot = pos + desired * 0.9f;
        if (HorizDistSqr(spot, _finalGoal) <= arriveDistance * arriveDistance * 4f) return;
        PathBlockMemory.Block(spot);
    }

    void ResetProgress()
    {
        _progressAnchor = transform.position;
        _progressTimer  = 0f;
        _blockedCount   = 0;
        _sidestepTimer  = 0f;

        // 새 목적지다 — 기록은 **지금 거리에서** 시작한다. `MaxValue`로 두면 첫 측정이
        // 무조건 기록을 깨서 시계가 한 번 공짜로 되돌아간다.
        _bestGoalDist = Mathf.Sqrt(HorizDistSqr(transform.position, _finalGoal));
        _noGainTimer  = 0f;
    }

    static float HorizDistSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    void Update()
    {
        // 누워 있는 동안은 중력·조향 모두 정지 (위치는 SetSleeping이 고정해뒀다)
        if (IsSleeping) return;

        // **업혀 있으면 제 발로 안 움직인다.** 업는 쪽이 `CharacterController`를 끄고
        // 어깨에 붙이는데(켠 채로 부모에 붙이면 콜라이더가 제자리로 되민다), 그 상태로
        // `Move`를 부르면 `CharacterController.Move called on inactive controller`가
        // **매 프레임** 쏟아진다. 위치는 부모(업은 폰)가 정한다.
        //
        // 갇힌 폰도 같은 이유로 뺀다 — `Imprisonment.Hold`가 자리를 잡아 주려고 컨트롤러를
        // 잠깐 껐다 켜는데, 그 사이 프레임에 걸리면 같은 경고가 난다.
        if (_cc == null || !_cc.enabled) return;

        if (_cc.isGrounded)
        {
            _verticalVel = groundStickForce;
            _lastGroundedPos = transform.position;
        }
        else
        {
            _verticalVel += gravity * Time.deltaTime;

            // 공허 낙하 안전망: 바닥 밖으로 떨어지면 마지막 접지 위치로 복귀
            if (transform.position.y < fallRecoveryY)
            {
                StopMovement();
                _verticalVel = 0f;
                _cc.enabled = false;
                transform.position = SafeRecoveryPoint();
                _cc.enabled = true;
                return;
            }
        }

        Vector3 move = new Vector3(0f, _verticalVel, 0f);

        // 기절 상태에서 목표가 남아 있으면 (KO 직전 타이밍 등) 정지시켜 조향을 막는다.
        // 중력/낙하 안전망은 위에서 이미 처리됐으므로 아래 조향만 건너뛴다.
        if (IsKnockedOut)
        {
            if (_hasTarget) StopMovement();
        }
        else if (_hasTarget)
        {
            Vector3 pos = transform.position;

            // 윈도우를 충분히 벗어났으면 새 윈도우로 재계획
            if ((pos - _replanAnchor).sqrMagnitude > _replanDriftSqr)
                ReplanLocal();

            Vector3 to = _currentTarget - pos;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist <= stopDistance)
            {
                AdvanceWaypoint();
            }
            else if (_localPath == null && IsVoidAhead(pos, to / dist))
            {
                // 공허 낭떠러지 가드: 경로 실패 직선 폴백이 공허를 향할 때만 발동.
                // 유효한 로컬 경로는 이미 바닥 게이트를 통과했으므로 검사하지 않는다
                // (좁은 다리에서 전방 샘플이 대각 공허 타일을 찍는 오발 방지).
                StopMovement();
            }
            else
            {
                float weightCorrection = CalcWeightCorrection();
                float sfxMult = _sfx != null ? _sfx.MoveSpeedMult : 1f;
                float effectiveSpeed = (_stats != null
                    ? _baseMoveSpeed * (1f + _stats.Athletics / 100f) * weightCorrection * sfxMult
                    : moveSpeed) * _mobilityMult;

                Vector3 desired = to / dist;
                Vector3 steer   = Steer(pos, desired);

                // 회피/비켜서기로 방향이 틀어졌으니 공허 검사도 실제 진행 방향으로 다시 한다
                if (_localPath == null && IsVoidAhead(pos, steer)) steer = desired;

                move += steer * effectiveSpeed;
                TrackProgress(pos, desired, effectiveSpeed);

                // **가는 쪽을 본다 — 다만 싸우는 중이면 표적이 이긴다.** 겨누는 것과 걷는 것이
                // 같은 프레임에 서로 다른 방향을 요구할 수 있고(사수가 비켜서며 쏜다), 그때
                // 몸이 봐야 하는 것은 **표적**이다. `MonoBehaviour` 실행 순서는 정해져 있지
                // 않으므로 둘 다 쓰게 두면 **어느 쪽이 이길지가 프레임마다 달라져 떤다.**
                //
                // 보통은 겹치지 않는다 — `FightNode`가 사거리 안에서는 `StopMovement`,
                // 밖에서는 `ExitCombat`이라 걷는 동안엔 전투가 꺼져 있다. 여기서 막는 것은
                // 그 사이에 낀 프레임뿐이다.
                if (_brain == null || _brain.Blackboard?.Combat == null
                    || !_brain.Blackboard.Combat.IsInCombat)
                    FaceDirection(steer);

                if (_stats != null)
                {
                    _stats.GrowStat(StatName.Athletics, athleticsGrowthRate * Time.deltaTime * CombatXp.Sol(_stats.Athletics));
                    GrowStrengthFromLoad();
                }
            }
        }

        _cc.Move(move * Time.deltaTime);
    }

    public void SetLimping(bool limping)
    {
        _mobilityMult = limping ? 0.5f : 1f;
        // 나중에 절뚝이는 애니메이션 연결
    }

    public void SetCrawling(bool crawling)
    {
        _mobilityMult = crawling ? 0.2f : 1f;
        // 나중에 기는 애니메이션 연결
    }

    // 짐을 지고 걸으면 근력이 자란다 (켄시식). **한도 대비 얼마나 실었나**로 재므로,
    // 근력이 커질수록 같은 짐으로는 덜 자란다 — 성장하려면 더 무겁게 져야 한다.
    // 가벼운 짐에 눈금이 오르면 빈손 배회로도 근력이 크므로 바닥선을 둔다.
    const float LoadGrowthFloor = 0.25f;   // 한도의 이만큼은 져야 눈금이 오른다

    void GrowStrengthFromLoad()
    {
        if (_inventory == null) return;

        float cap = CarryCapacity.For(_stats);
        if (cap <= 0f) return;

        float ratio = _inventory.GetTotalWeight() / cap;
        if (ratio < LoadGrowthFloor) return;

        _stats.GrowStat(StatName.Strength,
                        strengthGrowthRate * Time.deltaTime * ratio * CombatXp.Sol(_stats.Strength));
    }

    // 한도(`CarryCapacity`)를 넘긴 만큼 느려진다. **한도도 적재도 한 곳에서만 답한다** —
    // 배낭 무게를 직접 세면 어깨에 멘 사람이 빠져서, 사람을 업고도 빈손처럼 걷는다.
    float CalcWeightCorrection()
    {
        if (_stats == null || _brain == null) return 1f;
        float maxWeight     = CarryCapacity.For(_stats);
        float currentWeight = CarryCapacity.Load(_brain);
        if (maxWeight <= 0f || currentWeight <= maxWeight) return 1f;
        return maxWeight / currentWeight;
    }

    // **도는 것은 여기 하나가 한다**(2026-08-24).
    //
    // 그전까지 **폰은 아예 회전하지 않았다** — 코드 어디에도 회전이 없어서 태어난 방향을
    // 영영 보고 섰다. 그래서 석궁을 쏴도 **옆을 보고 쏜다**: 확률을 아무리 손봐도 화면에서는
    // 무슨 일이 벌어지는지 안 보인다.
    //
    // **자세를 이미 이 파일이 들고 있어서**(`SetSleeping`이 눕히고, 기절 회복이 `_preKoRotation`
    // 으로 되돌린다) 회전도 여기 있어야 한다. 밖에서 `transform.rotation`을 만지면 **누운 폰을
    // 세우거나 업힌 폰을 어깨 위에서 돌린다** — 이 저장소가 자세 때문에 여러 번 겪은 자리다.
    //
    // **수평만 돈다** — 위아래로 겨누면 몸이 기울어 발이 땅에서 뜬다.
    public void FaceTowards(Vector3 worldPos, float degreesPerSecond = 540f)
        => FaceDirection(worldPos - transform.position, degreesPerSecond);

    // **걸어가는 쪽도 본다**(2026-08-25). 08-24에 붙인 것은 *싸우는 동안*뿐이라, 폰은 여전히
    // **옆걸음·뒷걸음으로 맵을 가로질렀다** — 회전을 넣은 이유가 절반만 선 셈이다.
    //
    // 겨누는 자리(`FaceTowards`)와 같은 함수를 쓴다: 다른 것은 **무엇을 넘기느냐**뿐이고
    // (표적의 자리냐 · 실제로 가는 방향이냐) 자세 가드와 수평 제약은 한 벌이어야 한다.
    public void FaceDirection(Vector3 dir, float degreesPerSecond = 540f)
    {
        if (IsSleeping) return;   // 누운 폰은 안 돈다 — 자세가 곧 상태다

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0004f) return;   // 겹쳐 서 있으면 방향이 없다

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(dir),
            degreesPerSecond * Time.deltaTime);
    }

    public bool HasTarget()  => _hasTarget;

    // 낙하 복구를 **어디로** 할 것인가.
    //
    // 마지막 접지 위치를 그냥 쓰면 **무한 낙하**가 된다: 한 번도 땅을 밟은 적 없는 폰은
    // `_lastGroundedPos`가 태어난 자리(= 바닥 밖)라 되돌려 놔도 다시 떨어지고, 그게 영원히
    // 반복된다("바닥보다 살짝 밖에서 나와서 계속 떨어져", 2026-08-08).
    //
    // 그래서 되돌릴 자리가 **설 수 있는 곳인지 확인**하고, 아니면 가장 가까운 바닥으로 보낸다.
    Vector3 SafeRecoveryPoint()
    {
        var map = MapManager.GetForPosition(_lastGroundedPos);
        if (map == null || !map.FloorRequired) return _lastGroundedPos;

        var tile = map.WorldToTile(_lastGroundedPos);
        if (map.IsStandable(tile)) return _lastGroundedPos;

        var safe = map.FindWorkableNear(tile, 10);
        if (!map.IsWorkable(safe))
        {
            Debug.LogWarning($"[낙하복구] {name}: {tile} 주변에 설 수 있는 칸이 없습니다.");
            return _lastGroundedPos;
        }

        // 표면 **위에** 세운다 — 표면 Y에 그대로 놓으면 캡슐 아래 절반이 파묻혀 또 밀려난다.
        // 띄우는 값은 `PawnPlacement`가 답한다(스폰·밀어내기·업기와 같은 한 벌).
        return PawnPlacement.StandingPos(gameObject, map.TileToGround(safe));
    }
    // 지금 로컬 정밀 경로를 들고 있는가. 없으면 직선 폴백으로 가는 중이라 물체에 처박힌다 —
    // 포기 진단이 "길을 못 찾은 것"과 "길은 있는데 못 간 것"을 가르는 데 쓴다.
    public bool HasLocalPath => _localPath != null && _localPath.Count > 0;

    public bool IsMoving()   => _hasTarget;
    public void SetDestination(Vector3 pos) => SetMoveTarget(pos);
    public Vector3 Target()  => _currentTarget;

    Vector3 WithY(Vector3 v) => new Vector3(v.x, transform.position.y, v.z);

#if UNITY_EDITOR
    [Header("진단")]
    [Tooltip("고르면 **이 폰이 실제로 닿을 수 있는 칸**을 초록으로 그린다. " +
             "문이 막혔는지 · 계단이 이어졌는지가 한눈에 보인다.")]
    [SerializeField] bool showReach = false;
    [Tooltip("그 범위(칸).")]
    [SerializeField] int reachRadius = 20;

    // **닿을 수 있는 칸을 칠한다 — 추측을 그림으로 바꾼다.**
    //
    // *"왜 저기로 안 가지"*는 원인이 넷이다: 칸이 막혔거나(건물) · 몸이 안 들어가거나
    // (콜라이더) · 턱이 높거나(오르막) · 애초에 끊겼거나(문·계단이 없다). 로그로는 넷을
    // 못 가른다. **폰이 선 자리에서 실제 규칙대로 물을 채워 보면** 넷이 한 그림에 나온다 —
    // 초록이 문을 통과하지 못하면 문이 문제고, 기단 위에서만 초록이면 계단이 문제다.
    //
    // 굽기(`LocalGridManager.BakeGrid`)와 **같은 것을 본다**: 점유 · 장애물 상자 · 오르막.
    // 다른 것을 보면 그림이 거짓말을 하고, 그건 진단이 아니라 혼란이다.
    void DrawReach()
    {
        var map = MapManager.Instance;
        if (map == null) return;

        int r = Mathf.Clamp(reachRadius, 2, 40);
        var start = map.WorldToTile(transform.position);

        var seen  = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        seen.Add(start);
        queue.Enqueue(start);

        var half = new Vector3(0.45f, 1f, 0.45f);
        int mask = Pathfinder.ObstacleLayer;

        bool Open(Vector2Int t)
        {
            if (Mathf.Abs(t.x - start.x) > r || Mathf.Abs(t.y - start.y) > r) return false;
            if (!map.IsWorkable(t)) return false;
            var g = map.TileToGround(t);
            return mask == 0 || !Physics.CheckBox(g + Vector3.up, half, Quaternion.identity, mask);
        }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = cur + d;
                if (seen.Contains(n) || !Open(n) || !map.SameLevel(cur, n)) continue;
                seen.Add(n);
                queue.Enqueue(n);
            }
        }

        // 닿는 칸은 초록, 범위 안인데 못 닿는 칸은 붉게 — **못 닿는 쪽이 답을 준다.**
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            var t = new Vector2Int(start.x + dx, start.y + dz);
            bool ok = seen.Contains(t);
            Gizmos.color = ok ? new Color(0.2f, 1f, 0.3f, 0.35f) : new Color(1f, 0.2f, 0.2f, 0.12f);
            Gizmos.DrawCube(map.TileToGround(t) + Vector3.up * 0.06f, new Vector3(0.85f, 0.02f, 0.85f));
        }
    }

    void OnDrawGizmosSelected()
    {
        if (showReach) DrawReach();

        // 코스 라우트(L2) — 노랑
        if (_coarseRoute != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = _coarseIndex; i < _coarseRoute.Count; i++)
            {
                var cur = WithY(_coarseRoute[i]);
                Gizmos.DrawSphere(cur, 0.3f);
            }
        }

        // 로컬 정밀 경로 — 청록
        if (_localPath != null && _localPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = transform.position;
            for (int i = _localIndex; i < _localPath.Count; i++)
            {
                var cur = WithY(_localPath[i]);
                Gizmos.DrawLine(prev, cur);
                Gizmos.DrawSphere(cur, 0.1f);
                prev = cur;
            }
        }
    }
#endif
}
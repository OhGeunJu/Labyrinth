using System;
using System.Collections.Generic;
using UnityEngine;

// 원정대 편성 — **폰이 갑자기 사라지지 않게** 하는 장치.
//
// 편성을 누르면 즉시 없어지는 게 아니라, 고른 폰들이 **핵의 집결 자리를 둘러싸고 모인다**.
// 그 자리는 저작물이다 — 핵의 `WorkSpot`(건설 모드에서 표식이 보인다)이 원의 중심이 된다.
// 전원이 도착했을 때 비로소 한꺼번에 필드맵에서 빠지고 원정대가 된다(림월드의 대상(隊商) 편성과 같다).
// 나가는 자리와 돌아오는 자리가 둘 다 핵이라 문이 하나로 읽힌다.
//
// 왜 한 명씩 도착하는 대로 빼지 않는가: "누구는 갔고 누구는 걸어가는 중"인 중간 상태가 생기면
// 취소했을 때 어디까지 되돌릴지가 애매해진다. 전원 집결 후 일괄 출발이면 취소가 깨끗하다.
// 대신 한 명이 못 오면 출발이 막히므로 **[취소]로 언제든 푼다**.
[DefaultExecutionOrder(-20)]
public class ExpeditionFormation : MonoBehaviour
{
    public static ExpeditionFormation Instance { get; private set; }

    [Tooltip("집결 원의 반지름. **이 안에 들어오면 도착이다.** 인원과 무관하게 고정 — " +
             "사람이 늘면 원이 커지는 게 아니라 촘촘히 선다.")]
    [SerializeField] float gatherRadius = 4f;
    [Tooltip("서라고 보내는 자리를 판정 원보다 이만큼 **안쪽**에 잡는다. 그래야 걸어오다 " +
             "원을 확실히 넘는다 — 판정선 위에 세우면 경계에서 아슬아슬해진다.")]
    [SerializeField] float approachInset = 1.2f;
    [Tooltip("자리가 원 밖으로 밀렸을 때의 폴백 — 제 자리 이만큼 안이면 도착. 이웃 칸(1.0)보다 작아야 한다.")]
    [SerializeField] float arriveTolerance = 0.8f;
    [Tooltip("길이 막혀 못 오는 폰을 이만큼마다 다시 보낸다(초).")]
    [SerializeField] float nudgeInterval = 2f;
    [Tooltip("이 횟수만큼 다시 보내도 못 오면 '갈 수 없음'으로 알린다(계속 시도는 한다).")]
    [SerializeField] int stuckAfterNudges = 3;

    // 편성 상태가 바뀔 때(시작/도착/취소/출발) — UI 갱신용.
    public static event Action OnChanged;

    // 실제로 떠나는 순간. **모드 전환 전에** 쏜다 —
    // `GameModeManager.ApplyFieldUi`가 필드를 떠날 때 "그때 열려 있던 패널"을 기억했다가
    // 돌아올 때 그대로 되살리므로, 전환 뒤에 닫아 봐야 기억에는 '열림'으로 남아 다시 열린다.
    // (게다가 전환이 패널 루트를 꺼버려 `OnDisable`로 구독이 끊기면 알림 자체가 안 간다.)
    public static event Action OnDeparted;

    readonly List<UnitBrain> _gathering = new();
    readonly Dictionary<UnitBrain, Vector3> _spots = new();
    readonly HashSet<UnitBrain> _arrived = new();
    readonly Dictionary<UnitBrain, int> _nudges = new();
    readonly List<UnitBrain> _stuck = new();       // 집결 지점까지 갈 수 없는 폰들
    Vector3 _center;                               // 집결 원의 중심 — 도착 판정의 기준
    float _nudgeTimer;

    public int StuckCount => _stuck.Count;

    // "라핀" / "라핀 외 2명" — UI가 그대로 문장에 넣을 수 있게.
    public string StuckSummary
    {
        get
        {
            if (_stuck.Count == 0) return string.Empty;
            var first = _stuck[0] != null ? _stuck[0].UnitName : "?";
            return _stuck.Count == 1 ? first : $"{first} 외 {_stuck.Count - 1}명";
        }
    }

    // 지금 모이고 있는 폰들 — 패널이 목록을 다시 그릴 때 체크 상태를 되살리는 데 쓴다.
    public IReadOnlyList<UnitBrain> Members => _gathering;

    public bool IsGathering => _gathering.Count > 0;
    public int  TotalCount  => _gathering.Count;
    public int  ArrivedCount => _arrived.Count;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // **목표는 판정선보다 안쪽이어야 한다.** 원 위에 세우면 폰이 경계에서 멈춰 판정이
    // 부동소수점 오차에 걸린다 — 안쪽으로 보내면 걸어오다 원을 확실히 넘는다.
    //
    // 폴백(제 자리 근처) 쪽은 이웃 칸에 닿으면 안 된다. 자리는 타일 중심에 잡히므로 가장
    // 가까운 남의 자리가 1.0 떨어져 있고, 그보다 넓으면 옆 사람 칸에서 도착이 되어 뭉친다
    // (전에 간격 1.2에 도착 반경 1.8이었고 실제로 한 덩어리로 섰다).
    // 아래로는 폰의 정지 오차(`PawnMover.arriveDistance` 0.5)보다 넉넉해야 판정이 걸린다.
    void OnValidate()
    {
        gatherRadius    = Mathf.Max(1.5f, gatherRadius);
        approachInset   = Mathf.Clamp(approachInset, 0.5f, gatherRadius - 1f);
        arriveTolerance = Mathf.Clamp(arriveTolerance, 0.6f, 0.9f);
    }

    // ── 시작 ─────────────────────────────────────────────────────────────────

    // 고른 폰들에게 집결을 명령한다. 이미 편성 중이면 거절(중복 편성 방지).
    public bool Begin(IList<UnitBrain> members)
    {
        if (IsGathering || members == null || members.Count == 0) return false;

        var core = DungeonCore.Instance;
        if (core == null)
        {
            Debug.LogWarning("[편성] 던전 핵이 없어 집결 지점을 정할 수 없습니다.");
            return false;
        }

        _center   = GatherCenter(core);
        var spots = Formation(_center, members.Count);

        for (int i = 0; i < members.Count; i++)
        {
            var brain = members[i];
            if (brain == null) continue;

            var spot = spots[i];
            _gathering.Add(brain);
            _spots[brain] = spot;
            Order(brain, spot);
        }

        OnChanged?.Invoke();
        return _gathering.Count > 0;
    }

    // 플레이어 명령과 **같은 규칙**으로 보낸다(2026-07-21 결정) —
    // 타겟을 남기면 다음 틱에 전투가 목적지를 덮어쓰고, 작업을 남기면 도착하자마자 일하러 간다.
    static void Order(UnitBrain brain, Vector3 spot)
    {
        var bb = brain.Blackboard;
        bb.Target     = null;
        bb.ManualMove = true;
        brain.AbandonWork();

        brain.GetComponent<CombatHandler>()?.ExitCombat();
        brain.GetComponent<PawnMover>()?.SetMoveTarget(spot);
    }

    // ── 진행 ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsGathering) return;

        bool changed = false;

        for (int i = _gathering.Count - 1; i >= 0; i--)
        {
            var brain = _gathering[i];

            // 편성 중에 죽거나 사라진 폰은 조용히 명단에서 뺀다 — 그것 때문에 출발이 막히면 안 된다.
            if (brain == null)
            {
                _gathering.RemoveAt(i);
                _stuck.Remove(null);
                changed = true;
                continue;
            }

            if (_arrived.Contains(brain)) continue;

            if (HasArrived(brain))
            {
                _arrived.Add(brain);
                // 도착한 폰은 **제자리 대기**로 잡아둔다. `ManualMove`만으로는 안 된다 —
                // 목적지가 소진되는 순간 `ManualOrderNode`가 명령을 풀어 일하러 보낸다.
                brain.Blackboard.HoldPosition = true;
                brain.GetComponent<PawnMover>()?.StopMovement();
                changed = true;
            }
        }

        if (NudgeStragglers()) changed = true;
        if (changed) OnChanged?.Invoke();

        if (_gathering.Count > 0 && _arrived.Count >= _gathering.Count) Depart();
    }

    // 못 오고 있는 폰 챙기기.
    //
    // 길찾기가 포기하면 `PawnMover`는 목적지를 잃고 그냥 선다. 그러면 그 폰은 영영 도착하지 않고
    // 출발 조건("전원 도착")이 채워지지 않아 편성이 걸린 채 방치된다.
    //
    // **몰래 두고 출발하지 않는다.** 그러면 "왜 얘가 안 갔지"가 되고, 플레이어가 고른 명단을
    // 시스템이 임의로 줄이는 셈이다. 대신 ① 계속 다시 보내 보고(길이 뚫리면 알아서 온다)
    // ② 몇 번 해도 안 되면 **누가 못 오는지 알리고 판단을 넘긴다**(취소하거나, 길을 뚫어주거나).
    bool NudgeStragglers()
    {
        _nudgeTimer += Time.deltaTime;
        if (_nudgeTimer < nudgeInterval) return false;
        _nudgeTimer = 0f;

        bool changed = false;

        foreach (var brain in _gathering)
        {
            if (brain == null || _arrived.Contains(brain)) continue;

            var mover = brain.GetComponent<PawnMover>();

            // 걸어오는 중이면 내버려 둔다 — 막혔다가 길이 뚫린 경우 여기서 표시를 거둔다.
            if (mover != null && mover.HasTarget())
            {
                if (_stuck.Remove(brain)) changed = true;
                continue;
            }

            // 목적지를 잃고 서 있다 = 길찾기가 포기했다. 계속 다시 보낸다.
            _nudges.TryGetValue(brain, out int count);
            _nudges[brain] = count + 1;
            Order(brain, _spots[brain]);

            if (count + 1 >= stuckAfterNudges && !_stuck.Contains(brain))
            {
                _stuck.Add(brain);
                changed = true;
                Debug.LogWarning($"[편성] '{brain.UnitName}'이(가) 집결 지점까지 갈 수 없습니다 — " +
                                 "길이 막혔거나 닿을 수 없는 자리입니다. 계속 시도하지만, " +
                                 "편성을 취소하거나 길을 확보해 주세요.");
            }
        }

        return changed;
    }

    // **집결 원 안에 들어오면 도착이다** — 각자의 자리에 정확히 서야 하는 게 아니다.
    //
    // 자리는 "어디쯤 서라"는 안내지 조건이 아니다. 조건으로 삼으면 그 칸에 못 서는 폰 하나가
    // 출발을 통째로 막는다 — 다른 폰에 밀려 반 칸 비켜서기만 해도 그렇다. 폰은 원보다 **안쪽**
    // 자리로 걸어오므로, 제 자리로 가는 길에 원을 반드시 넘는다.
    //
    // 자기 자리 판정을 폴백으로 남긴다: 자리 스냅(`Standable`)이 막힌 칸을 피하다 자리를
    // 원 밖으로 밀어낼 수 있고, 그때는 원 판정이 영영 안 걸린다.
    //
    // **수평 거리로만 잰다.** 핵과 그 자식(`WorkSpot`)은 `_hoverOffset`만큼 떠 있어
    // 3D 거리로 재면 폰이 발밑까지 와도 높이 차 때문에 영영 "도착"이 안 된다.
    bool HasArrived(UnitBrain brain)
    {
        var pos = brain.transform.position;
        return FlatDistance(pos, _center) <= gatherRadius
            || FlatDistance(pos, _spots[brain]) <= arriveTolerance;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ── 출발 / 취소 ──────────────────────────────────────────────────────────

    // 전원 집결 → 한꺼번에 필드맵에서 빠지고 원정대에 합류. 그리고 월드맵으로 넘어간다.
    void Depart()
    {
        var view = FindFirstObjectByType<WorldMapView>();
        if (view == null)
        {
            Debug.LogWarning("[편성] WorldMapView가 없어 출발할 수 없습니다. 편성을 취소합니다.");
            Cancel();
            return;
        }

        var members = new List<UnitBrain>(_gathering);
        // 돌아왔을 때 대기 상태로 굳어 있지 않게 여기서 푼다 — 필드에 되살아나면 바로 일해야 한다.
        foreach (var m in members) Release(m);
        Reset();

        // UI가 스스로 정리할 기회 — **모드 전환 전에** 줘야 한다(위 OnDeparted 주석 참고).
        OnDeparted?.Invoke();

        view.AddMembers(members);          // 여기서 필드맵에서 빠진다(SetActive(false))
        GameModeManager.Instance?.SwitchTo(GameMode.World);

        Debug.Log($"[편성] {members.Count}명이 원정을 떠났습니다.");
        OnChanged?.Invoke();
    }

    // 아직 아무도 필드를 떠나지 않았으므로 되돌릴 게 없다 — 명령만 풀면 하던 일로 돌아간다.
    public void Cancel()
    {
        foreach (var brain in _gathering) Release(brain);
        Reset();
        OnChanged?.Invoke();
    }

    // 붙들어 둔 것을 전부 푼다. 안 풀면 그 폰은 영영 대기 상태로 굳는다.
    static void Release(UnitBrain brain)
    {
        if (brain == null) return;
        brain.Blackboard.HoldPosition = false;
        brain.Blackboard.ManualMove   = false;
    }

    void Reset()
    {
        _gathering.Clear();
        _spots.Clear();
        _arrived.Clear();
        _nudges.Clear();
        _stuck.Clear();
        _nudgeTimer = 0f;
    }

    // 집결 지점을 표면에 붙인다. 핵은 떠 있어서(`_hoverOffset`) 그 Y를 그대로 쓰면
    // 목적지가 공중에 뜨고, 길찾기가 닿을 수 없는 자리로 본다.
    //
    // **`SampleGroundHeight`가 아니라 `SurfaceHeight`다** — 던전 씬처럼 Terrain을 끄고 바닥 시스템을
    // 쓰는 구성에서는 `SampleGroundHeight`가 0(평지 가정)을 돌려주고, 그러면 바닥 위에 선 폰과
    // 집결 지점의 높이가 어긋난다. `SurfaceHeight`는 바닥 윗면 ∥ 지형 높이의 단일 출처다.
    static Vector3 GroundSnap(Vector3 p)
    {
        var map = MapManager.Instance;
        if (map != null) p.y = map.SurfaceHeight(map.WorldToTile(p));
        return p;
    }

    // ── 자리 배치 ────────────────────────────────────────────────────────────

    // 집결 기준점 — **저작한 자리**다.
    //
    // 작업대·연구대가 `WorkSpot`으로 "폰이 서는 자리"를 정하는 것과 **같은 방식**을 쓴다.
    // 핵에 `WorkSpot`이 있으면 그 칸이 원의 중심이고, 없으면 핵 주변에서 설 수 있는 칸을 찾는다
    // (`WorkSpot.TileFor`가 둘 다 처리한다). 덕분에 집결 장소가 데이터가 되고, **건설 모드에서
    // 표식이 보이므로** 그 자리를 막는 배치를 눈으로 피할 수 있다.
    static Vector3 GatherCenter(DungeonCore core)
    {
        var map = MapManager.Instance;
        if (map == null) return core.transform.position;

        return GroundSnap(map.TileToWorld(WorkSpot.TileFor(core)));
    }

    // **기준점을 둘러싸는 원 하나.** 반지름은 고정이고, 사람이 늘면 원이 커지는 게 아니라
    // 촘촘히 선다 — 인원에 따라 집결 범위가 달라지면 "저기 모인다"를 예측할 수 없다.
    //
    // 자리는 판정 원보다 `approachInset`만큼 **안쪽**에 잡는다. 판정선 위에 세우면 폰이
    // 경계에 딱 멈춰 도착이 아슬아슬해지지만, 안쪽으로 보내면 걸어오다 원을 확실히 넘는다.
    //
    // 각도가 촘촘해져 두 자리가 같은 칸으로 떨어지면 `Standable`이 옆의 빈 칸으로 밀어낸다.
    // 원이 감당 못 할 만큼 많아졌을 때 알아서 번지는 것과, 인원마다 원을 다시 그리는 것은 다르다.
    Vector3[] Formation(Vector3 center, int count)
    {
        var result = new Vector3[count];
        var used   = new HashSet<Vector2Int>();
        var map    = MapManager.Instance;

        float radius = Mathf.Max(0.5f, gatherRadius - approachInset);

        for (int i = 0; i < count; i++)
        {
            float a = i * (Mathf.PI * 2f / count);
            var   p = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            result[i] = Standable(p, used, map);
        }
        return result;
    }

    // 자리를 **폰이 실제로 설 수 있는 칸**으로 옮긴다. 원 위의 점은 건물·자원 노드·핵 위에
    // 떨어질 수 있는데, 그러면 그 폰은 영영 도착하지 못하고 출발이 통째로 막힌다
    // (`NudgeStragglers`가 몇 번 뒤에 알리긴 하지만, 애초에 안 보내는 게 낫다).
    Vector3 Standable(Vector3 world, HashSet<Vector2Int> used, MapManager map)
    {
        if (map == null) return GroundSnap(world);

        var tile = FreeTileNear(map, map.WorldToTile(world), used);
        used.Add(tile);
        return GroundSnap(map.TileToWorld(tile));
    }

    // `MapManager.FindWorkableNear`와 같은 고리 탐색이지만 **이미 배정한 칸을 뺀다** —
    // 그게 없으면 막힌 자리 여럿이 같은 대체 칸으로 몰려 한 자리에 겹쳐 선다.
    static Vector2Int FreeTileNear(MapManager map, Vector2Int center, HashSet<Vector2Int> used,
                                   int maxRadius = 4)
    {
        if (map.IsWorkable(center) && !used.Contains(center)) return center;

        for (int r = 1; r <= maxRadius; r++)
        {
            Vector2Int best  = center;
            float      bestD = float.MaxValue;
            bool       found = false;

            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                // 이번 껍질만 본다 — 안쪽은 앞선 반경에서 이미 봤다.
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;

                var t = new Vector2Int(center.x + dx, center.y + dy);
                if (used.Contains(t) || !map.IsWorkable(t)) continue;

                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = t; found = true; }
            }

            if (found) return best;
        }

        // 주변이 전부 막혔다 — 원래 자리를 준다. 못 오면 `NudgeStragglers`가 누구인지 알린다.
        return center;
    }
}

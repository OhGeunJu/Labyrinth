using System.Collections.Generic;
using UnityEngine;

// **습격대 한 무리의 뜻 — 폰 밖에 산다**(2026-09-07, 설계는 `docs/design_raid_lord.md`).
//
// 그전에는 무리의 의도가 **스폰할 때 한 번**이었다(`EventManager`가 핵으로 `SetMoveTarget`).
// 그래서 습격이 **줄줄이 와서 한 명씩 썰리고**(굴은 통로가 한 칸이라 더 심하다) **한 명 남아도
// 끝까지 걸어와 죽었다** — 습격이 끝나는 방식이 언제나 몰살이었다.
//
// 림월드의 `Lord`가 놓이는 자리이고, 여기 없던 층이 이것 하나다.
public class RaidLord
{
    public enum Phase { Approach, Assault, Retreat }

    // ── 눈금 ─────────────────────────────────────────────────────────────────

    // 집결지는 **목적지에서 가장자리 쪽으로 물러난 자리**다. 가장자리에 두면 모이는 동안
    // 방어자가 도착해 **집결지가 곧 전장**이 된다.
    const float StagingBack   = 22f;
    const float GatherRadius  = 14f;   // 이 안에 있으면 "모였다"
    const float GatherRatio   = 0.6f;  // 산 멤버의 이만큼이 모이면 돌격
    // **못 오는 놈 때문에 영영 안 시작하면 안 된다.** 다만 이건 *기다리는 시간*이 아니라
    // **천장**이다 — 보통은 6할이 모이는 순간 넘어가고, 이 값은 **길이 막혀 영영 못 오는
    // 놈**이 있을 때만 걸린다. 짧게 잡으면(45초로 두었었다) 가장자리에서 핵까지 150m를
    // 걷는 동안 그냥 만료되어 **접근이 하는 일이 없어진다.**
    const float ApproachCap = 120f;
    const float BreakRatio    = 0.34f; // 서 있는 놈이 처음의 이만큼 이하면 무너진 것이다

    readonly List<UnitBrain> _members = new();
    readonly int     _initial;
    readonly Vector3 _goal;
    readonly Vector3 _staging;

    Phase _phase;
    float _phaseAt;

    public Phase Current  => _phase;
    public bool  Done     { get; private set; }

    // 도구가 들여다보는 창. **서 있는 수는 `_members.Count`가 곧 답이다** — `Tick`이
    // 매번 쓰러진 놈을 걷어내므로 따로 세면 두 벌이 된다.
    //
    // 이름이 `Standing`이 아닌 이유: 아래에 *한 놈이 서 있나*를 묻는 `Standing(brain)`이
    // 이미 있다. **묻는 것이 다르면 이름도 달라야 한다**(하나는 수, 하나는 참/거짓).
    public int   StandingCount => _members.Count;
    public int   Initial       => _initial;

    // 무리를 이끄는 한 명. 죽으면 `null`이 된다 — **다시 안 뽑는다**(아래 참조).
    public UnitBrain Leader { get; private set; }

    public RaidLord(IEnumerable<GameObject> arrivals, Vector3 edge, Vector3 goal)
    {
        foreach (var go in arrivals)
        {
            var brain = go != null ? go.GetComponent<UnitBrain>() : null;
            if (brain != null) _members.Add(brain);
        }

        _initial = _members.Count;
        _goal    = goal;
        _phaseAt = Time.time;

        PromoteLeader();

        // **가까우면 접근을 건너뛴다** — 집결지가 스폰 뒤에 놓이면 **뒤로 걸어갔다 돌아온다.**
        Vector3 toEdge = edge - goal;
        float   dist   = toEdge.magnitude;
        if (dist > StagingBack * 1.5f)
        {
            _staging = goal + toEdge / dist * StagingBack;
            _phase   = Phase.Approach;
        }
        else
        {
            _staging = goal;
            _phase   = Phase.Assault;
        }

        Order(_phase == Phase.Approach ? _staging : _goal);
    }

    // ── 지휘관 ───────────────────────────────────────────────────────────────

    // **무리에 우두머리가 하나 있다**(2026-09-08, 사용자: *"지휘관으로 선정된 적은 출신
    // 배경이나 특성 이런게 좀 더 강했으면"*).
    //
    // 지금은 습격대가 **똑같은 놈 N명**이다(같은 로드아웃에서 굴린 같은 수준). 그래서 무리를
    // 봐도 *무엇을 먼저 쳐야 하나*라는 결정이 없다 — 아무나 치면 된다. 한 명이 눈에 띄게
    // 세면 그 결정이 생긴다.
    //
    // **이미 가장 센 놈을 골라서 더 세게 한다.** 아무나 뽑아 올리면 *약골이 갑자기 두목*이
    // 되어 무리 안에서 앞뒤가 안 맞고, 무엇보다 **굴린 결과를 버리는 것**이라 같은 시드가
    // 다른 무리를 낳는다(`PawnSpec`이 시드 하나로 도는 이유).
    //
    // **죽어도 다시 안 뽑는다.** 두목이 죽으면 그 무리는 목이 잘린 것이다 — 남은 놈 중
    // 하나를 그 자리에서 강하게 만들면 **때려잡을수록 강해지는 무리**가 된다.
    void PromoteLeader()
    {
        UnitBrain best = null;
        float bestScore = float.MinValue;

        foreach (var brain in _members)
        {
            if (!Standing(brain)) continue;
            // 명세가 있으면 그 평균, 없으면 굴려서 태어난 폰이라 실제 스탯이 답이다
            // (`PawnSpec.PriceOf`가 값을 낼 때 쓰는 것과 **같은 갈래**).
            float score = brain.Spec != null ? brain.Spec.GradeAverage
                        : brain.Stats != null ? brain.Stats.Toughness : 0f;
            if (score > bestScore) { bestScore = score; best = brain; }
        }

        if (best == null || best.Data == null) return;
        Leader = best;

        // **이미 정해진 사람은 안 건드린다.** `Spec`이 있으면 밖에서 주입된 것이다 —
        // 지도에서 걸어온 무리(`SpawnArrivingBand`가 `ApplySpec`으로 그 사람을 그대로 세운다)와
        // 저작한 고정 개체가 그렇다. 여기서 다시 굴리면 **지도에서 두들겨 놓은 무리가 필드에서
        // 딴사람이 되어**, 지도 위에서 싸운 것이 아무 뜻도 못 갖는다.
        //
        // **그래도 지휘관이긴 하다** — 뽑는 것과 세게 만드는 것은 다른 일이고, 이쪽은 이미
        // 그 무리에서 가장 센 사람이다. (굴려서 태어난 폰은 `Spec`이 `null`이라는 것이
        // 이 저장소에 이미 적혀 있다 — `PawnSpec.PriceOf`가 그 위에 서 있다.)
        if (best.Spec != null) return;

        // **수준을 올려 다시 굴린다.** 스탯만 손으로 밀어 올리면 **특성도 배경도 그대로**라
        // 사용자가 바란 *"출신 배경이나 특성이 더 강한 놈"*이 안 된다 — `PawnSpec.Roll`이
        // 그 셋을 한 번에 낸다(수준을 먼저 뽑고 스탯을 그 언저리에서 굴린다).
        //
        // **시드는 신원에서 낸다** — `Random`으로 뽑으면 같은 판을 다시 봐도 다른 사람이 되고,
        // `GetHashCode`는 실행마다 달라진다(`PawnSpecData`가 먼저 겪은 함정).
        var data = best.Data;
        int  seed = Mathf.Abs(StableHash(best.name) ^ Mathf.RoundToInt(_goal.x * 31f + _goal.z * 17f));

        var min = Bump(data.DefaultMinGrade);
        var max = Bump(data.DefaultMaxGrade);

        // **세력의 배경 묶음을 넘긴다**(2026-09-08에 빠뜨린 것을 잡았다). 안 넘기면 `civilian`
        // 폴백으로 떨어져 **고블린 두목에게서 사람 이력이 나온다** — 세력마다 이력을 갈라 둔
        // 뜻이 지워지는 그 자리다(`UnitBrain`이 스스로 굴릴 때는 이미 이걸 넘기고 있었다).
        var group = best.Blackboard?.Faction?.Data?.BackstoryGroup;
        best.ApplySpec(PawnSpec.Roll(data, seed, min, max, null, group));
    }

    // 세 칸(=15점) 올린다. **한두 칸으로는 화면에서 티가 안 나고**, 그 이상이면 초반 습격에
    // 혼자 못 잡는 놈이 선다.
    static StatGrade Bump(StatGrade g) =>
        (StatGrade)Mathf.Clamp((int)g + 3, 0, (int)StatGrade.SSSPlus);

    // `string.GetHashCode`는 **실행마다 달라진다** — 시드로 쓰면 같은 판이 다시 안 나온다.
    static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h;
        }
    }

    // ── 상태 ─────────────────────────────────────────────────────────────────

    public void Tick()
    {
        if (Done) return;

        int standing = 0, gathered = 0;
        for (int i = _members.Count - 1; i >= 0; i--)
        {
            var brain = _members[i];
            if (!Standing(brain)) { _members.RemoveAt(i); continue; }

            standing++;
            if (_phase == Phase.Approach &&
                (brain.transform.position - _staging).sqrMagnitude <= GatherRadius * GatherRadius)
                gathered++;
        }

        // 아무도 안 남았으면 이 무리는 끝이다.
        if (standing == 0) { Done = true; return; }

        // **후퇴는 접근 중에도 걸린다** — 문 앞에서 몰살당하는 중인데 *아직 돌격을 안 했으니*
        // 계속 걸어오는 것은 규칙이 아니라 버그로 보인다.
        if (_phase != Phase.Retreat && standing <= Mathf.FloorToInt(_initial * BreakRatio))
        {
            Enter(Phase.Retreat);
            return;
        }

        if (_phase == Phase.Approach)
        {
            bool enough = gathered >= Mathf.CeilToInt(standing * GatherRatio);
            bool late   = Time.time - _phaseAt >= ApproachCap;
            if (enough || late) Enter(Phase.Assault);
            else                Nudge(_staging);
            return;
        }

        if (_phase == Phase.Assault) Nudge(_goal);
    }

    void Enter(Phase next)
    {
        _phase   = next;
        _phaseAt = Time.time;

        if (next == Phase.Retreat)
        {
            // **새 노드를 안 만든다** — `LeavingMap` 하나가 이미 답이고 `LeaveMapNode`가 이미
            // 트리 맨 위에 있다(풀려난 포로·떠나는 떠돌이와 **같은 문**). 딸려 오는 것:
            // 떠나는 놈은 **먼저 안 치지만 맞으면 되받아친다** — 그게 후퇴의 옳은 모양이다.
            foreach (var brain in _members)
            {
                var bb = brain != null ? brain.Blackboard : null;
                if (bb != null) bb.LeavingMap = true;
            }
            Done = true;
            return;
        }

        Order(next == Phase.Approach ? _staging : _goal);
    }

    // ── 명령 ─────────────────────────────────────────────────────────────────

    // **상태가 바뀔 때 한 번**. 매 틱 부르면 로컬 재계획이 계속 돌고(`RouteVersion`이 오르면
    // 진행 눈금이 다시 시작된다) 폰이 제자리에서 떤다.
    //
    // **자리를 여기서 고르지 않는다** — `SetMoveTarget`이 이미 `FindWorkableNear`로 접는다.
    // 여기서 또 고르면 *밖에서 온 것이 설 자리*의 답이 두 벌이 된다.
    void Order(Vector3 to)
    {
        foreach (var brain in _members)
        {
            if (!Standing(brain)) continue;
            var mover = brain.GetComponent<PawnMover>();
            if (mover != null) mover.SetMoveTarget(Spread(to));
        }
    }

    // **목적지를 잃은 놈에게만 다시 준다.** 싸우고 나면 잃는다(`FightNode`가 표적을 쫓느라
    // 덮어쓰고, 끝나면 배회가 받는다) — 안 주면 그 자리에 눌러앉아 무리가 반씩 갈린다.
    void Nudge(Vector3 to)
    {
        foreach (var brain in _members)
        {
            if (!Standing(brain)) continue;

            // 싸우는 중이면 안 건드린다 — 표적을 쫓는 것이 지금의 명령이다.
            var bb = brain.Blackboard;
            if (bb != null && bb.Target != null) continue;

            var mover = brain.GetComponent<PawnMover>();
            if (mover != null && !mover.HasTarget()) mover.SetMoveTarget(Spread(to));
        }
    }

    // 같은 점을 스무 명에게 주면 한 칸을 두고 밀친다. 스폰이 `ScatterNear`를 쓰는 것과 같은 이유.
    static Vector3 Spread(Vector3 p)
    {
        Vector2 o = Random.insideUnitCircle * 4f;
        return new Vector3(p.x + o.x, p.y, p.z + o.y);
    }

    // **`BecomeCorpse`는 `UnitBrain`을 끄기만 한다** — 그래서 이것이 곧 "살아서 서 있나"다.
    // 갇힌 놈과 이미 떠나는 놈도 무리가 아니다.
    static bool Standing(UnitBrain brain)
    {
        if (brain == null || !brain.isActiveAndEnabled) return false;
        var bb = brain.Blackboard;
        if (bb == null) return true;
        return !bb.IsKnockedOut && !bb.IsPrisoner && !bb.LeavingMap;
    }
}

// **Lord들을 돌리는 자리.** 씬 배선이 없다 — 저작할 것이 없고 상태는 Lord가 든다.
public class RaidLords : MonoBehaviour
{
    const float TickInterval = 0.5f;   // 무리 단위 판단이라 폰 틱(0.1초)만큼 자주 볼 이유가 없다

    static RaidLords _instance;
    readonly List<RaidLord> _lords = new();

    // 지금 지휘 중인 무리들 — 도구가 상태를 보여 주는 데 쓴다.
    public static IReadOnlyList<RaidLord> Active =>
        _instance != null ? _instance._lords : System.Array.Empty<RaidLord>();
    float _next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("RaidLords") { hideFlags = HideFlags.DontSave };
        _instance = go.AddComponent<RaidLords>();
        DontDestroyOnLoad(go);
    }

    // 습격을 세운 쪽이 부른다. **아무도 안 보는 습격은 안 부른다** — 그쪽은 세우자마자
    // 추상으로 풀고 파괴하므로(`WorldBattle.ResolveRaid`) 지휘할 무리가 없다.
    public static void Lead(IEnumerable<GameObject> arrivals, Vector3 edge, Vector3 goal)
    {
        if (arrivals == null) return;

        // **없으면 만든다.** 전에는 스폰하는 쪽이 폰마다 직접 `SetMoveTarget`을 걸었으므로
        // 지휘자가 없어도 습격은 굴러갔다. 이제 **어디로 갈지를 여기가 통째로 들고 있어서**,
        // 부트스트랩이 안 돈 상태(에디터에서 씬을 직접 열었을 때 등)에서 조용히 돌아가면
        // **습격대가 가장자리에 서서 아무것도 안 한다** — 화면에는 *"적이 안 온다"*로만 보인다.
        if (_instance == null) Bootstrap();
        if (_instance == null) return;
        var lord = new RaidLord(arrivals, edge, goal);
        if (!lord.Done) _instance._lords.Add(lord);
    }

    void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + TickInterval;

        for (int i = _lords.Count - 1; i >= 0; i--)
        {
            _lords[i].Tick();
            if (_lords[i].Done) _lords.RemoveAt(i);
        }
    }
}

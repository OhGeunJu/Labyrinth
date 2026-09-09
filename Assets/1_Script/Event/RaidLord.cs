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

    // ── 포위 — 여러 갈래로 들어온다 (2026-09-09) ─────────────────────────────
    //
    // **포위는 *어디에 서나*가 아니라 *어느 쪽으로 접근하나*다.** 그래서 진형이 아니라
    // 가장자리를 고르는 자리에서 시작한다(`HierarchicalPathfinder.PickReachableSpread`가
    // 서로 먼 방위를 하나씩 준다). 여기는 **갈래마다 제 집결지와 제 나갈 문**을 든다.
    //
    // **갈래는 무리이지 개인이 아니다** — 스무 명을 여덟 갈래로 쪼개면 두세 명씩 여덟 번
    // 걸어오는 것이라, 이 층을 만든 이유(*줄줄이 와서 한 명씩 썰린다*)가 **갈래 단위로
    // 그대로 되살아난다.** 그래서 한 갈래에 `ProngSize`명은 있어야 한다.
    public class Prong
    {
        public readonly Vector3 Edge;                       // 이 갈래가 들어온 자리 = 나갈 자리
        public readonly List<GameObject> Members = new();
        public Prong(Vector3 edge) { Edge = edge; }
    }

    // 한 갈래에 적어도 **셋**. 둘은 무리가 아니다 — 두 명씩 세 방향에서 오는 것은 이 층을
    // 만든 이유(*줄줄이 와서 한 명씩 썰린다*)를 **갈래 단위로 되살리는 것**이라, 포위처럼
    // 보이는 자살이 된다.
    //
    // **지금 저작된 습격이 4~6명이라 이 값이 곧 *언제 포위가 보이나*를 정한다** — 다섯으로
    // 두었더니 어떤 습격도 안 갈라져서 **만들어 놓고 한 번도 안 나오는 규칙**이 됐다.
    // 셋이면 여섯 명짜리부터 갈라진다. 더 자주 보고 싶으면 손잡이는 여기가 아니라
    // `GameEventData.EnemyCount`다(*습격이 몇 명인가*는 난이도이고 이건 *몇 명이면 나눌
    // 만한가*이다 — 섞으면 갈래를 늘리려고 난이도를 올리게 된다).
    public const int ProngSize = 3;

    // **셋이 넘지 않는다** — 굴에서 서로 먼 방위 넷을 찾는 일이 드물기도 하고, 넷을 넘기면
    // 화면에서 *포위*가 아니라 **사방에서 새어 들어오는 것**으로 보인다(막을 곳을 고르는
    // 결정이 다시 사라진다).
    public const int MaxProngs = 3;

    // **갈래 수는 무리의 크기가 정한다 — 저작하지 않는다.** 이벤트 에셋마다 적게 하면
    // 저작 실수 하나가 *세 명이 세 방향에서 오는 습격*을 만들고, 무엇보다 **묻는 문이 둘**
    // (이벤트 습격·지도에서 걸어온 무리)이라 한쪽만 고치는 날이 온다.
    public static int ProngsFor(int members) => Mathf.Clamp(members / ProngSize, 1, MaxProngs);

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
    // **서 있는 놈이 처음의 이만큼 이하면 무너진 것이다.**
    //
    // 0.34였다(셋 중 둘이 쓰러져야 물러난다). **2026-09-08에 절반으로 올렸다** — 사용자가
    // *절반이 기절했을 때*를 보고 있었고, 그게 맞다: 이 층을 만든 이유가 **습격이 몰살
    // 말고 다른 방식으로 끝나게 하는 것**인데, 3분의 2가 쓰러질 때까지 안 물러나면
    // 남은 셋도 대개 그 자리에서 죽는다(그때는 물러나 봐야 결과가 같다).
    //
    // **쓰러진 놈은 이미 안 센다** — `Standing`이 기절·포로·이탈을 다 빼므로 *죽은 수*가
    // 아니라 **싸울 수 있는 수**로 잰다.
    const float BreakRatio    = 0.5f;

    // ── 대형을 유지한 채 밀고 들어간다 (2026-09-08) ──────────────────────────
    //
    // duty를 얹고도 **돌격이 한 줄로 왔다**(사용자: *"진형을 짜고 돌격한다는 느낌이 안 들어"*).
    // 당연했다 — 목적지에 슬롯을 주는 것은 **도착했을 때의 모양**이고, 오는 동안에는 각자
    // 제 슬롯으로 길을 찾으므로 **통로에서 한 줄로 늘어선다.** 대형은 도착해서야 생긴다.
    //
    // 그래서 **한 번에 끝까지 안 보낸다** — 무리의 지금 한복판에서 목적지 쪽으로 `AdvanceStep`
    // 만큼만 대형을 놓고, 주기마다 다시 놓는다. 앵커가 무리를 따라 앞으로 기어가는 셈이라
    // **오는 내내 모양이 유지되고**, 무엇보다 **앞선 놈이 뒤처진 놈을 기다린다**(앵커를
    // 한복판에서 내므로 뒤가 처지면 앵커도 같이 처진다).
    //
    // **매 틱 덮어쓰지 않는다**(그건 이 파일의 첫 규칙이다) — 틱은 0.5초이고 이건 그보다
    // 훨씬 뜸하다. 그리고 **싸우는 중인 놈은 안 건드린다**: 자리는 적어 두되 명령은 안 준다
    // (싸움이 끝나면 `Nudge`가 그 자리로 돌려보낸다).
    const float ReformSeconds = 2.5f;
    const float AdvanceStep   = 10f;

    float _reformAt;

    readonly List<UnitBrain> _members = new();
    readonly int     _initial;
    readonly Vector3 _goal;

    // **갈래마다 하나씩.** 집결지는 *어디서 기다리나*, 가장자리는 **들어온 자리이자 나갈
    // 자리**다(아래 `Enter(Retreat)` 참조).
    readonly Vector3[] _stagings;
    readonly Vector3[] _edges;

    // 누가 어느 갈래인가. 집결지도 나갈 문도 **폰마다 다르므로** 없으면 답이 안 나온다.
    readonly Dictionary<UnitBrain, int> _prongOf = new();

    // 대형을 다시 낼 때 쓰는 그릇 — 2.5초마다 도는 자리라 매번 새로 만들지 않는다.
    readonly List<UnitBrain> _crew   = new();
    readonly List<PawnMover> _movers = new();

    // 누가 어느 칸인가. `Order`가 채우고 `Nudge`가 읽는다 — 목적지를 잃은 놈을 **한 점이
    // 아니라 제 자리로** 돌려보내야 진형이 첫 접촉에서 안 녹는다.
    readonly Dictionary<UnitBrain, Vector3> _slot = new();

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
    public int   ProngCount    => _edges.Length;

    // 무리를 이끄는 한 명. 죽으면 `null`이 된다 — **다시 안 뽑는다**(아래 참조).
    public UnitBrain Leader { get; private set; }

    // **한 갈래도 갈래다** — 편의로 `(폰 목록, 가장자리)`를 받는 짝을 두지 않는다. 부르는
    // 문이 둘이면 언젠가 한쪽만 고치게 되고, 갈래가 하나뿐인 습격은 그냥 `Prong` 하나다.
    public RaidLord(IReadOnlyList<Prong> prongs, Vector3 goal)
    {
        _goal    = goal;
        _phaseAt = Time.time;

        int np    = prongs != null ? prongs.Count : 0;
        _edges    = new Vector3[Mathf.Max(1, np)];
        _stagings = new Vector3[_edges.Length];

        bool anyFar = false;
        for (int p = 0; p < np; p++)
        {
            var prong = prongs[p];
            _edges[p] = prong.Edge;

            // **집결지는 갈래마다다** — 목적지에서 **제가 들어온 쪽으로** 물러난 자리.
            // 하나로 두면 갈래들이 한 점에 모였다가 같이 나가므로 **포위가 아니라 합류**다.
            Vector3 toEdge = prong.Edge - goal;
            float   dist   = toEdge.magnitude;
            if (dist > StagingBack * 1.5f)
            {
                _stagings[p] = goal + toEdge / dist * StagingBack;
                anyFar       = true;
            }
            else
            {
                // **가까우면 제자리에서 기다린다** — 여기에 목적지를 넣으면 그 갈래만
                // 집결한답시고 **거점 한복판으로 걸어 들어간다**(옛 한 갈래 시절에는
                // 그럴 때 접근을 통째로 건너뛰었으므로 드러나지 않던 자리다).
                _stagings[p] = prong.Edge;
            }

            foreach (var go in prong.Members)
            {
                var brain = go != null ? go.GetComponent<UnitBrain>() : null;
                if (brain == null) continue;
                _members.Add(brain);
                _prongOf[brain] = p;
            }
        }

        _initial = _members.Count;
        PromoteLeader();

        // **가까우면 접근을 건너뛴다** — 집결지가 스폰 뒤에 놓이면 **뒤로 걸어갔다 돌아온다.**
        // 갈래가 여럿이면 **하나라도 멀면 접근한다**: 상태는 무리 하나의 것이라, 먼 갈래가
        // 걸어오는 동안 가까운 갈래만 먼저 들이치면 그건 포위가 아니라 **줄줄이 오는 것**이다.
        _phase = anyFar ? Phase.Approach : Phase.Assault;

        Reform(advance: false);
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

        // **두목은 뽑는 것이 아니라 세우는 것이다**(2026-09-09, 사용자 결정: *"처음 만들때부터
        // 대장하나에 부하넷을 만들면 되는 거 아니야?"*).
        //
        // 잠깐 *"이끈 이력을 가진 사람을 먼저 고른다"*로 만들었다가 되돌렸다 — 그건 **증상을
        // 쫓는 코드**였다: 이력이 아무에게나 붙는 것은 그대로 두고 뽑는 쪽에서 맞춰 주는
        // 것이라, 그 이력을 읽는 자리가 하나 늘 때마다 같은 맞춤이 또 필요해진다.
        //
        // 이제 **이끄는 이력은 굴림에서 아예 안 나온다**(`BackstoryPool.Fits`). 그래서
        // *"이력은 두목인데 두목이 아니다"*라는 상태가 **만들어질 수가 없고**, 여기서는
        // 예전처럼 가장 센 놈을 고른 뒤 **이력을 주기만** 하면 된다.

        // **화면에서도 알아보게 한다**(2026-09-09). 두목을 세게 만든 이유가 *무엇을 먼저
        // 쳐야 하나*인데, 포위가 붙어 갈래가 셋이 되고도 **어느 갈래에 있는지는 눌러 봐야
        // 알았다** — 그러면 그건 판단이 아니라 뽑기다. 표식의 규칙은 전부 `PawnMark`가
        // 든다(여기서 색이나 높이를 정하면 다음 표식이 그 답을 다시 갖는다).
        PawnMark.Show(best.gameObject, PawnMark.Kind.Leader);

        var storyGroup = best.Blackboard?.Faction?.Data?.BackstoryGroup;

        // **이미 정해진 사람은 다시 안 굴린다.** `Spec`이 있으면 밖에서 주입된 것이다 —
        // 지도에서 걸어온 무리(`SpawnArrivingBand`가 `ApplySpec`으로 그 사람을 그대로 세운다)와
        // 저작한 고정 개체가 그렇다. 여기서 다시 굴리면 **지도에서 두들겨 놓은 무리가 필드에서
        // 딴사람이 되어**, 지도 위에서 싸운 것이 아무 뜻도 못 갖는다.
        //
        // **그래도 두목이긴 하다** — 세우는 것과 세게 만드는 것은 다른 일이고, 이쪽은 이미
        // 그 무리에서 가장 센 사람이다. (굴려서 태어난 폰은 `Spec`이 `null`이라는 것이
        // 이 저장소에 이미 적혀 있다 — `PawnSpec.PriceOf`가 그 위에 서 있다.)
        if (best.Spec == null)
        {
            // **수준을 올려 다시 굴린다.** 스탯만 손으로 밀어 올리면 **특성도 배경도 그대로**라
            // 사용자가 바란 *"출신 배경이나 특성이 더 강한 놈"*이 안 된다 — `PawnSpec.Roll`이
            // 그 셋을 한 번에 낸다(수준을 먼저 뽑고 스탯을 그 언저리에서 굴린다).
            //
            // **시드는 신원에서 낸다** — `Random`으로 뽑으면 같은 판을 다시 봐도 다른 사람이
            // 되고, `GetHashCode`는 실행마다 달라진다(`PawnSpecData`가 먼저 겪은 함정).
            var data = best.Data;
            int seed = Mathf.Abs(StableHash(best.name) ^
                                 Mathf.RoundToInt(_goal.x * 31f + _goal.z * 17f));

            // **세력의 배경 묶음을 넘긴다**(2026-09-08에 빠뜨린 것을 잡았다). 안 넘기면
            // `civilian` 폴백으로 떨어져 **고블린 두목에게서 사람 이력이 나온다** — 세력마다
            // 이력을 갈라 둔 뜻이 지워지는 자리다.
            best.ApplySpec(PawnSpec.Roll(data, seed,
                                         Bump(data.DefaultMinGrade), Bump(data.DefaultMaxGrade),
                                         null, storyGroup));
        }

        // **이력은 맨 끝에 준다 — 여기가 유일한 문이다.**
        //
        // 굴림에서는 이끄는 이력이 아예 안 나오므로(`BackstoryPool.Fits`) 두목이 그것을
        // 받는 길은 이 한 줄뿐이다. **`ApplySpec` 뒤여야 한다** — 다시 굴리는 일에는 이력
        // 굴림도 딸려 와서, 앞에서 주면 여기서 날아간다(갓 세운 두목이 `짐꾼`을 달고 선다).
        //
        // **명세가 있는 사람에게도 준다.** 안 그러면 지도에서 걸어온 무리는 영영 두목 이력이
        // 없어 **머리 위 표식과 스탯 창이 서로 다른 말을 한다** — *다시 안 굴린다*가 지키려는
        // 것은 **그 사람의 신원**(이름·등급·특성)이지 이력 딱지가 아니다.
        //
        // **묶음에 이끄는 이력이 없으면 굴린 것을 그대로 둔다**(`LeaderOf`가 `null`) —
        // `civilian`으로 안 떨어뜨린다: 그러면 **고블린 두목이 사람 두목 이력**을 단다.
        best.SetAdulthood(BackstoryPool.LeaderOf(storyGroup));
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
            // **제 갈래의 집결지에서 잰다** — 하나로 재면 멀리서 오는 갈래가 영영 안 모여
            // 접근이 늘 천장(`ApproachCap`)으로만 끝난다.
            if (_phase == Phase.Approach &&
                (brain.transform.position - _stagings[ProngOf(brain)]).sqrMagnitude
                    <= GatherRadius * GatherRadius)
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
            else                Nudge();
            return;
        }

        if (_phase != Phase.Assault) return;

        if (Time.time >= _reformAt)
        {
            _reformAt = Time.time + ReformSeconds;
            Reform(advance: true);
        }
        else Nudge();
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
            // **왔던 길로 나간다**(2026-09-08, 사용자: *"그냥 벽을 뚫고 사라져버려"*).
            //
            // 처음에는 `EventManager.TryFindExit`을 썼는데 **그건 직선으로 걷는다** —
            // 지하에서는 모퉁이를 못 돌아 **대여섯 걸음 앞 벽에서 멈추고**, 폰은 거기에
            // 도착해 그대로 사라진다(도착 판정이 0.9m다). 화면에는 *벽에 처박혀 없어지는
            // 것*으로 보인다. 상인이 기지 코앞의 벽에서 튀어나오던 것과 **같은 함수, 같은
            // 이유**이고, 들어오는 쪽은 이미 고쳤는데 나가는 쪽만 남아 있었다.
            //
            // **들어온 자리는 이미 안다** — 그리고 그 자리는 *걸어 들어온 곳*이라 **길이
            // 이어져 있음이 증명된 유일한 좌표**다. 새로 찾을 이유가 없다.
            //
            // `LeavingMap`만 켜서도 안 된다 — `LeaveTarget`이 (0,0,0)이면 세계 원점으로
            // 걷는다. 노드는 *못 움직일 때만* 다시 잡으므로, 돌격 명령으로 이미 걷는 중인
            // 폰은 **그 자리를 다시 안 묻고 핵으로 계속 간다.**
            //
            // **하던 걸음을 멈춘다** — 안 멈추면 `SetDestination`이 덮이기 전까지 옛 목적지로
            // 몇 걸음 더 간다(그 몇 걸음이 방어선 안쪽이면 그대로 붙잡힌다).
            foreach (var brain in _members)
            {
                var bb = brain != null ? brain.Blackboard : null;
                if (bb == null) continue;

                bb.LeavingMap = true;
                bb.Routed     = true;   // 맞아도 안 돌아선다 — 이게 패주다
                bb.Target     = null;

                // **제가 들어온 문으로 나간다** — 갈래가 셋이면 나가는 문도 셋이다.
                // 하나로 몰면 서쪽으로 들어온 놈이 **거점을 가로질러 동쪽으로 나가고**,
                // 그건 후퇴가 아니라 관통이다(그 길은 걸어 본 적도 없다).
                bb.LeaveTarget = _edges[ProngOf(brain)];

                bb.Mover?.StopMovement();
            }
            Done = true;
            return;
        }

        Reform(advance: false);
    }

    // ── 명령 ─────────────────────────────────────────────────────────────────

    // **상태가 바뀔 때 한 번**. 매 틱 부르면 로컬 재계획이 계속 돌고(`RouteVersion`이 오르면
    // 진행 눈금이 다시 시작된다) 폰이 제자리에서 떤다.
    //
    // ── duty — 무리가 어떤 모양으로 서는가 (2026-09-08) ──────────────────────
    //
    // 상태(`Phase`)가 *어디로 갈지*를 정했다면 duty는 *어떻게 설지*를 정한다. 이 층을 만든
    // 이유가 그것이고, 여기까지 와야 습격이 **똑같은 놈 N명이 줄줄이 오는 것**을 면한다.
    //
    // **내 폰과 같은 규칙을 쓴다**(`Formation`). 갈라 두면 *같은 무기를 든 무리가 내가 이끌 때와
    // 적이 이끌 때 다르게 선다* — 그건 규칙이 아니라 두 벌이다. 딸려 오는 것: 앞줄은 여기서도
    // **사거리가 답한다**(근접이 앞, 활이 뒤). 저작할 것이 없다.
    //
    // 상태마다 모양이 다르다:
    //   · **접근** = 밀집. 집결지에 **뭉치는** 것이 이 상태의 뜻이다.
    //   · **돌격** = 삼각형. 앞이 뾰족해 **근접이 먼저 닿고** 활이 뒤에 남는다.
    //   · **후퇴** = 진형 없음(각자 나간다). 대형을 유지하며 물러나는 것은 패주가 아니다.
    //
    // **바라보는 쪽은 가는 쪽이다** — **그 갈래의** 무게중심에서 목적지로. 여기서 카메라를
    // 보면 아무도 안 보는 습격에서 답이 없어진다(추상으로 푸는 갈래가 그 자리다).
    void Reform(bool advance)
    {
        _slot.Clear();

        // **갈래마다 따로 세운다.** 무리 전체의 한복판에서 대형을 내면 세 갈래가 **한 점을
        // 향해 모여들어**, 서로 먼 방위에서 들어온 뜻이 도착하기도 전에 사라진다.
        for (int p = 0; p < _edges.Length; p++)
        {
            _crew.Clear();
            _movers.Clear();
            Vector3 sum = Vector3.zero;

            foreach (var brain in _members)
            {
                if (!Standing(brain) || ProngOf(brain) != p) continue;
                var mover = brain.GetComponent<PawnMover>();
                if (mover == null) continue;

                _crew.Add(brain);
                _movers.Add(mover);
                sum += brain.transform.position;
            }
            if (_movers.Count == 0) continue;   // 이 갈래는 전멸했다

            var center = sum / _movers.Count;
            var to     = TargetFor(p, center, advance);

            var facing = to - center;
            var shape  = _phase == Phase.Approach ? Formation.Shape.Box : Formation.Shape.Wedge;

            // **자리를 여기서 고르지 않는다** — 접는 것은 `Formation`이 `FindWorkableNear`로 하고,
            // 그건 `SetMoveTarget`이 쓰는 문 그대로다(*밖에서 온 것이 설 자리*의 답은 여전히 한 벌).
            // 다만 **누가 어느 칸인지는 여기서 기억한다** — 아래 `Nudge`가 같은 자리를 다시 주려면
            // 필요하고, 매번 새로 내면 한 명이 목적지를 잃을 때마다 **무리 전체가 자리를 맞바꾼다**.
            var spots = Formation.Place(to, facing, _movers, shape);
            for (int i = 0; i < _movers.Count; i++)
            {
                _slot[_crew[i]] = spots[i];

                // **싸우는 중이면 자리만 적고 명령은 안 준다** — 표적을 쫓는 것이 지금의
                // 명령이고, 여기서 덮으면 붙은 싸움에서 등을 돌린다. 싸움이 끝나면 `Nudge`가
                // 이 자리로 돌려보낸다(그래서 자리는 적어 두어야 한다).
                var bb = _crew[i].Blackboard;
                if (bb != null && bb.Target != null) continue;

                _movers[i].SetMoveTarget(spots[i]);
            }
        }
    }

    // 이 갈래가 지금 어디로 가는가. **한 걸음 앞에 놓는다**(`advance`) — 목적지가 코앞이면
    // 그냥 목적지에 놓는다: 안 그러면 마지막 걸음이 목적지를 지나쳐 무리가 핵을 통과해
    // 걸어 나간다.
    Vector3 TargetFor(int prong, Vector3 center, bool advance)
    {
        if (_phase == Phase.Approach) return _stagings[prong];
        if (!advance)                 return _goal;

        var toGoal = _goal - center;
        toGoal.y   = 0f;

        float d = toGoal.magnitude;
        return d <= AdvanceStep ? _goal : center + toGoal / d * AdvanceStep;
    }

    // **목적지를 잃은 놈에게만 다시 준다.** 싸우고 나면 잃는다(`FightNode`가 표적을 쫓느라
    // 덮어쓰고, 끝나면 배회가 받는다) — 안 주면 그 자리에 눌러앉아 무리가 반씩 갈린다.
    void Nudge()
    {
        foreach (var brain in _members)
        {
            if (!Standing(brain)) continue;

            // 싸우는 중이면 안 건드린다 — 표적을 쫓는 것이 지금의 명령이다.
            var bb = brain.Blackboard;
            if (bb != null && bb.Target != null) continue;

            // **제 자리로 돌려보낸다 — 목적지 한 점이 아니라.** 여기서 목적지를 그냥 주면
            // 싸우고 돌아온 놈들이 **전부 한 점으로 몰려** 진형이 한 번 붙을 때마다 녹는다
            // (`Reform`이 기억해 둔 칸이 그래서 필요하다). 자리를 못 찾으면 **제 갈래의**
            // 목적지로 — 무리 전체의 것으로 떨어뜨리면 갈래가 뒤섞인다.
            var mover = brain.GetComponent<PawnMover>();
            if (mover == null || mover.HasTarget()) continue;

            mover.SetMoveTarget(_slot.TryGetValue(brain, out var spot)
                ? spot
                : _phase == Phase.Approach ? _stagings[ProngOf(brain)] : _goal);
        }
    }

    // 누가 어느 갈래인가. **모르면 첫 갈래로 친다** — 여기서 예외를 던지면 무리 하나가
    // 통째로 멎는다(갈래는 세우는 자리에서 정해지므로 빠질 일이 없지만, 빠졌을 때
    // *아무것도 안 하는 습격*보다 *한 갈래로 오는 습격*이 낫다).
    int ProngOf(UnitBrain brain)
        => brain != null && _prongOf.TryGetValue(brain, out var p) ? p : 0;

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
        _instance = SelfStanding.Create<RaidLords>("RaidLords");
    }

    // 습격을 세운 쪽이 부른다. **아무도 안 보는 습격은 안 부른다** — 그쪽은 세우자마자
    // 추상으로 풀고 파괴하므로(`WorldBattle.ResolveRaid`) 지휘할 무리가 없다.
    // **갈래를 받는 문**(2026-09-09 — 포위). 세우는 쪽이 이미 갈래로 나눠 세우므로
    // 여기서 다시 나누지 않는다(나누면 *어느 자리에서 나왔나*와 *어느 갈래인가*가 갈린다).
    public static void Lead(IReadOnlyList<RaidLord.Prong> prongs, Vector3 goal)
    {
        if (prongs == null || prongs.Count == 0) return;

        // **없으면 만든다.** 전에는 스폰하는 쪽이 폰마다 직접 `SetMoveTarget`을 걸었으므로
        // 지휘자가 없어도 습격은 굴러갔다. 이제 **어디로 갈지를 여기가 통째로 들고 있어서**,
        // 부트스트랩이 안 돈 상태(에디터에서 씬을 직접 열었을 때 등)에서 조용히 돌아가면
        // **습격대가 가장자리에 서서 아무것도 안 한다** — 화면에는 *"적이 안 온다"*로만 보인다.
        if (_instance == null) Bootstrap();
        if (_instance == null) return;
        var lord = new RaidLord(prongs, goal);

        // **숫자를 찍는다**(2026-09-08). 화면에서는 폰이 걷는 것만 보여 *접근인지 돌격인지*를
        // 못 가리고, 무리가 아예 안 섰을 때(`Done`)는 **아무 표시도 안 난다** — 그러면
        // *"접근이 안 뜬다"*가 **① 무리가 없다 ② 처음부터 돌격이다** 중 어느 쪽인지 알 방법이
        // 없다. 한 줄이면 갈린다. 습격은 드물게 오므로 로그가 쌓이지 않는다.
        Debug.Log($"[습격대] {lord.StandingCount}명 · {lord.ProngCount}갈래 · " +
                  $"가장자리→목적지 {Vector3.Distance(prongs[0].Edge, goal):F0}m · 시작 {lord.Current}" +
                  (lord.Done ? " · 곧바로 끝남(설 수 있는 멤버가 없다)" : ""));

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

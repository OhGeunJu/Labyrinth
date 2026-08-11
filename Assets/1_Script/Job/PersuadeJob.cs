using UnityEngine;

// **갇힌 사람을 말로 설득한다.** 회유 게이지를 회차마다 올린다.
//
// 하수인 종족에게는 안 쓴다 — 그쪽은 던전 핵이 지배한다(`Conversion.TickCore`).
// **말이 통하는 상대에게만 사람을 보낸다**는 것이 두 길을 가르는 선이다.
//
// 규약은 `TreatJob`을 그대로 따른다: 우리 옆에 서서 회차로 끊고, 시계는 **도착해서** 켠다
// (걸어가는 동안 흘러가면 멀리 있던 폰이 닿자마자 끝난다).
//
// **일감의 주인은 우리다**(`PrisonCage`) — `Bed`가 `TreatJob`을 내는 것과 같은 자리다.
// 포로 자신이 내면 누가 등록을 지우는지가 애매해지고(넘어오거나 죽으면 스스로 못 지운다),
// 무엇보다 **누르는 사람마다 하나씩 만들면 여럿이 동시에 붙는다**: 세 명이 [설득한다]를
// 누르면 세 회차가 나란히 돌아 게이지가 세 배로 찼다. 자리가 하나면 일감도 하나다.
public class PersuadeJob : JobBase
{
    // **손으로 지목했으면 그 폰만.** 우리가 저 혼자 낸 것이면 `speaker`가 없고, 그때는
    // 간수를 켜 둔 아무 폰이나 집어 간다.
    public PersuadeJob(UnitBrain target, UnitBrain speaker = null)
    {
        _target  = target;
        _speaker = speaker;
        Priority = 4;

        if (target != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(target.transform.position);
    }

    readonly UnitBrain _target;
    readonly UnitBrain _speaker;
    float _endTime;

    public UnitBrain Target => _target;

    // **간수 일이다.** 한때 `Medical`을 빌려 쓰면서 `CanAssignTo`로 지목한 폰만 통과시켰는데,
    // 그러면 손으로 시키는 길밖에 없다 — 붙잡아 둔 사람이 있어도 아무도 저절로 가지 않는다.
    // 제 분류가 생기면서 **켜고 끄는 자리**(P키)도 함께 생겼다.
    public override JobCategory Category => JobCategory.Warden;

    // 말을 붙이는 일이라 **손만 닿으면 된다** — 우리 창살 너머로도 말은 통한다.
    public override float Reach => HandReach;

    // 그새 넘어왔거나 풀려났으면 큐에서 기다리지 않고 끝낸다.
    //
    // **쿨타임은 여기서 안 본다.** `IsReady`가 false면 큐에 남아 **대기**하는데, 그러면
    // 쿨타임이 도는 20초 동안 폰이 우리 앞에 붙박여 기다린다 — 쿨타임을 둔 이유가 바로
    // 그 사이에 다른 일을 하라는 것이다. 우리가 쿨타임이 지난 다음에 일감을 낸다.
    public override bool IsReady => Conversion.CanPersuade(_target);

    // 자기 자신은 못 설득하고, 기절한 폰은 말을 못 한다(치료와 같은 규칙).
    public override bool CanAssignTo(UnitBrain unit)
    {
        if (unit == null || unit == _target) return false;
        if (unit.Blackboard != null && unit.Blackboard.IsKnockedOut) return false;

        // 손으로 지목한 것이면 그 폰만 — 남이 대신 집으면 시킨 사람이 안 간다.
        return _speaker == null || unit == _speaker;
    }

    protected override void OnStart() => _endTime = 0f;

    protected override void Execute(UnitBrain unit)
    {
        if (!Conversion.CanPersuade(_target)) { Status = JobStatus.Done; return; }

        // 갇힌 사람은 안 움직이지만 우리를 옮길 수는 있다 — 매번 자리를 다시 본다.
        if (MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(_target.transform.position);

        // 도착했다 — 이제 시작한다.
        if (_endTime <= 0f)
        {
            _endTime = Time.time + Conversion.PersuadeSeconds(unit);
            return;
        }
        if (Time.time < _endTime) return;

        // **굴림도 알림도 `Conversion`이 한다.** 여기서 계산하면 나중에 다른 설득 경로가
        // 생겼을 때 성공률과 문구가 두 벌이 된다.
        Conversion.PersuadeRound(unit, _target);

        // **실패해도 인지력은 는다.** 해 보는 것 자체가 노동이고, 성공에만 붙이면 인지력이
        // 낮은 폰은 늘지도 않아 영영 못 하게 된다(다른 노동과 같은 축, SOL).
        if (unit.Stats != null)
            unit.Stats.GrowStat(StatName.Cognition, 4f * CombatXp.Sol(unit.Stats.Cognition));

        Status = JobStatus.Done;   // 한 회차 = 한 일감. 쿨타임이 지나면 우리가 다음 것을 건다.
    }
}

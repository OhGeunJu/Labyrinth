using UnityEngine;

// **갇힌 사람에게 밥을 가져다 먹인다.** 창고에서 챙겨(선택) 자리로 가서 한 끼 먹이는
// 2-phase 작업 — 규약은 `TreatJob`을 그대로 따른다.
//
// 갇히면 BT가 안 돌아 `EatNode`도 안 돈다. 그래서 한동안 **갇힌 사람은 허기가 멈춰 있었다** —
// 안 그러면 먹을 방법이 없는 채로 굶어 죽었기 때문이다(그건 규칙이 아니라 사고였다).
// 먹이는 길이 생겼으니 허기를 되살렸고(`Feeding.Simulates`), 이제 **붙잡아 두는 데 값이 든다.**
//
// **일감의 주인은 가두는 자리다**(`PrisonFeeding`) — 포로 자신이 내면 죽거나 넘어왔을 때
// 스스로 등록을 못 지우고, 누르는 사람마다 만들면 여럿이 동시에 붙는다(설득이 실제로 그랬다).
public class FeedJob : JobBase
{
    // 창고에 들를 때 **한 끼만 들고 가지 않는다.** 끼니마다 창고를 왕복하면 빵 하나
    // 먹이는 데 맵을 가로지른다. 남은 것은 다음 끼니에 그대로 쓰이고, 다 쓰고 남으면
    // `UnloadJob`이 창고로 돌려놓는다. (`TreatJob.FetchCount`와 같은 규약, 같은 이유.)
    const int FetchCount = 3;

    enum Phase { GoToStorage, GoToPrisoner }

    readonly UnitBrain _prisoner;
    readonly UnitBrain _feeder;    // 손으로 지목했으면 그 폰만

    Phase    _phase = Phase.GoToPrisoner;
    Storage  _source;
    FoodData _fetching;   // 창고에서 챙겨 올 것
    FoodData _using;      // 이번 끼니에 먹일 것
    float    _endTime;

    public FeedJob(UnitBrain prisoner, UnitBrain feeder = null)
    {
        _prisoner = prisoner;
        _feeder   = feeder;

        // **설득보다 급하다.** 게이지는 도중에 줄지 않지만 굶으면 죽고, 죽으면 회유고
        // 몸값이고 아무것도 남지 않는다(같은 `Warden`이라 가르는 것은 이 값뿐이다).
        Priority = 6;

        if (prisoner != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(prisoner.transform.position);
    }

    public UnitBrain Prisoner => _prisoner;

    public override JobCategory Category => JobCategory.Warden;

    // 그릇을 건네는 일이라 **손만 닿으면 된다** — 우리 창살 너머로도 먹일 수 있다
    // (`PersuadeJob`과 같은 값). 창고에 들를 때도 마찬가지라 단계로 갈리지 않는다.
    public override float Reach => HandReach;

    // 그새 풀려났거나 배가 찼으면 큐에서 기다린다.
    //
    // **먹을 것이 있는지는 여기서 안 본다** — 그걸 물으려면 창고와 폰 배낭을 전부 훑어야
    // 하는데, `IsReady`는 폰이 일감을 찾을 때마다 불린다. 값어치 판단은 자리가 일감을 걸
    // 때 한 번만 한다(`PrisonFeeding.Tick`). 그새 물자가 떨어지면 아래에서 접는다.
    public override bool IsReady => PrisonFeeding.NeedsFood(_prisoner);

    // 자기 자신은 못 먹인다(갇힌 폰은 어차피 일감을 못 집지만, 규칙은 적어 둔다).
    // 기절한 폰은 남을 못 먹인다 — 손이 안 움직인다(치료·설득과 같은 규칙).
    public override bool CanAssignTo(UnitBrain unit)
    {
        if (unit == null || unit == _prisoner) return false;
        if (unit.Blackboard != null && unit.Blackboard.IsKnockedOut) return false;

        return _feeder == null || unit == _feeder;
    }

    protected override void OnStart()
    {
        var feeder = ReservedBy;
        float have = _prisoner != null && _prisoner.Blackboard != null
                   ? _prisoner.Blackboard.Hunger : 0f;

        // **배낭에 있으면 그걸 쓴다.** 창고에 더 알맞은 것이 있어도 가지러 가지 않는다 —
        // 들고 있는 빵을 두고 스튜를 찾아 맵을 가로지르면 그동안 포로는 굶는다.
        _using = Feeding.BestInBackpack(feeder, have);
        if (_using != null) { BeginFeeding(); return; }

        // 예약 없이 시작될 일은 없지만(`Start`는 `Reserve` 뒤다), 자리를 못 물으면
        // 포로 옆에서 재는 편이 낫다 — 원점(0,0,0)을 기준으로 삼으면 늘 같은 창고가 이긴다.
        var origin = feeder != null ? feeder.transform.position : _prisoner.transform.position;
        _fetching  = Feeding.BestInStorage(have, origin, out _source);
        if (_fetching == null) { BeginFeeding(); return; }   // 없으면 가서 확인만 하고 접는다

        _phase    = Phase.GoToStorage;
        TileCoord = WorkSpot.TileFor(_source);   // 창고도 자기 칸은 통행 불가다
    }

    void BeginFeeding()
    {
        _phase = Phase.GoToPrisoner;

        if (_prisoner != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(_prisoner.transform.position);

        // **시계는 도착해서 켠다**(0 = 아직 안 켬). 여기서 켜면 걸어가는 동안 흘러가서
        // 멀리 있던 폰은 닿자마자 끝난다(`TreatJob`·`CraftJob`과 같은 규약).
        _endTime = 0f;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_phase == Phase.GoToStorage)
        {
            // 그새 남이 가져갔거나 배낭에 자리가 없을 수 있다 — 빈손이면 아래에서 접는다.
            // **자리를 먼저 보고 꺼내는 것은 `Storage.FetchInto`가 한다**(안 그러면 짐이 찬
            // 폰이 올 때마다 창고에서 나왔다 들어갔다 한다).
            int took = _source != null && _fetching != null
                     ? _source.FetchInto(unit.Inventory, _fetching, FetchCount)
                     : 0;

            _using = took > 0 ? _fetching : null;
            BeginFeeding();
            return;
        }

        // 풀려났거나 죽었거나 배가 찼다.
        if (!PrisonFeeding.NeedsFood(_prisoner)) { Status = JobStatus.Done; return; }

        // **먹일 것이 없으면 여기서 접는다.** 치료는 맨손이라도 되지만 빈손으로 밥을 먹일
        // 수는 없다 — 자리가 `IsReady`를 보고 물자가 들어왔을 때 다시 건다.
        if (_using == null) { Status = JobStatus.Done; return; }

        // 갇힌 사람은 안 움직이지만 우리를 옮길 수는 있다 — 매번 자리를 다시 본다.
        if (MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(_prisoner.transform.position);

        // 도착했다 — 이제 시작한다.
        if (_endTime <= 0f)
        {
            _endTime = Time.time + Feeding.FeedSeconds(_using);
            return;
        }
        if (Time.time < _endTime) return;

        // **다 먹이고 나서 소모한다.** 먼저 빼면 도중에 끊겼을 때(폰이 죽거나 명령을 받으면)
        // 음식만 사라진다(`TreatJob`의 약과 같은 순서).
        if (!Feeding.ConsumeFromBackpack(unit, _using)) { Status = JobStatus.Done; return; }

        Feeding.Eat(_prisoner, _using);
        Debug.Log($"[Feed] {unit.UnitName} → {_prisoner.UnitName} " +
                  $"({_using.ItemName}, 허기 {_prisoner.Blackboard.Hunger:F0}/100)");

        Status = JobStatus.Done;   // 한 끼 = 한 일감. 아직 배고프면 자리가 다음 것을 건다.
    }
}

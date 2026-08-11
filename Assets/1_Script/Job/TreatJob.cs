using UnityEngine;

// **의사가 다친 폰을 돌본다** — 창고에서 약을 챙겨(선택) 환자에게 가서 치료하는 2-phase 작업.
//
// 부상은 누워 있으면 저절로 낫는다(`RestNode`). 그건 시간이 지나는 것이지 누가 무엇을 한 것이
// 아니라서, 부상이 사건이 되지 못했다 — 다치면 그냥 기다렸다.
//
// **침대에 누워 있을 때만** 일감이 생긴다(`Bed`가 등록한다). 환자가 돌아다니면 의사가
// 쫓아다녀야 하고, 침대 배정은 이 게임에서 이미 "쉬어라"라는 명령이다.
//
// **한 회차 = 약 하나.** 시간을 재서 회복량을 조금씩 얹는 대신 회차로 끊는 이유는, 그래야
// 소모품을 쓰는 자리가 생기기 때문이다. 아직 나을 것이 남았으면 `Bed`가 다음 회차를 다시 건다.
public class TreatJob : JobBase
{
    // 창고에 들를 때 **한 회차분만 들고 가지 않는다.** 회차마다 창고를 왕복하면 붕대 하나
    // 감는 데 맵을 가로지른다. 남은 것은 배낭에 있다가 다음 회차에 그대로 쓰이고(창고행 생략),
    // 다 낫고 남으면 `UnloadJob`이 알아서 창고로 돌려놓는다.
    const int FetchCount = 5;

    enum Phase { GoToStorage, GoToPatient }

    readonly Bed       _bed;       // 침대에서 난 일감이면 그 침대(없어도 된다)
    readonly UnitBrain _patient;   // 침대가 없을 때의 환자
    readonly bool      _manual;    // 우클릭으로 콕 집어 시킨 것인가

    Phase        _phase = Phase.GoToPatient;
    Storage      _source;
    MedicineData _fetching;   // 창고에서 챙겨 올 것
    MedicineData _using;      // 이번 회차에 쓸 것(없으면 맨손)
    float        _endTime;

    public override JobCategory Category => JobCategory.Medical;

    // 침대에서 나는 일감 — 누가 누워 있느냐는 그때그때 침대에 묻는다.
    public TreatJob(Bed bed)
    {
        _bed     = bed;
        Priority = 2;
        SetSite(bed);   // 목표 칸(WorkSpot)도 여기서 정해진다
    }

    // **침대가 없어도 치료한다**(우클릭으로 폰을 직접 지목). 전투 직후 쓰러진 사람을 그 자리에서
    // 돌보는 것이 침대까지 끌고 가는 것보다 급하다 — 침대는 회복을 **얹는** 것이지 조건이 아니다.
    public TreatJob(UnitBrain patient, bool manual)
    {
        _patient = patient;
        _manual  = manual;
        Priority = manual ? 5 : 2;

        // 폰에는 저작된 작업 자리가 없다 — 서 있는(누워 있는) 칸을 그대로 겨눈다.
        if (patient != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(patient.transform.position);
    }

    public UnitBrain      Patient     => _bed != null ? _bed.Occupant : _patient;
    public BodyPartSystem PatientBody => Patient != null ? Patient.GetComponent<BodyPartSystem>() : null;

    // **단계마다 다르다.** 창고에서 약을 챙길 때는 손만 닿으면 되지만, 환자 앞에서는
    // 그 자리에 서야 한다 — 두 칸 떨어져 붕대를 감을 수는 없다.
    public override float Reach => _phase == Phase.GoToStorage ? HandReach : StandReach;

    // 환자가 자리에 없거나 다 나았으면 큐에서 기다린다(취소가 아니라 대기 —
    // 재료 부족과 같은 정책. `Bed`가 필요 없어지면 그때 지운다).
    public override bool IsReady
    {
        get
        {
            var body = PatientBody;
            return body != null && body.NeedsTreatment;
        }
    }

    // **자기 자신은 큐로는 못 고친다** — 누워 있는 폰이 제 일감을 집으면 침대에서 일어나
    // 침대 옆으로 걸어가는 우스운 일이 된다(애초에 BT가 `Rest`에서 멈춰 `Work`까지 안 간다.
    // 누운 채 하는 자가치유는 `RestNode` 안에 있다 — `WorkSettings.SelfTend`).
    //
    // **손으로 시킨 것은 예외다.** 우클릭으로 자기를 눌러 "지금 싸매라"고 한 것은 지시이고,
    // 그때는 침대에 누워 있지도 않으므로 위의 우스운 일이 생기지 않는다.
    public override bool CanAssignTo(UnitBrain unit)
        => unit != null && Patient != null && (_manual || unit != Patient);

    protected override void OnStart()
    {
        var doctor = ReservedBy;

        // **배낭에 있으면 그걸 쓴다.** 창고에 더 좋은 것이 있어도 가지러 가지 않는다 —
        // 들고 있는 붕대를 두고 구급낭을 찾아 맵을 가로지르면, 그동안 환자는 피를 흘린다.
        _using = Treatment.BestInBackpack(doctor);
        if (_using != null) { BeginTreating(doctor); return; }

        // 없으면 창고에 들른다. 그것도 없으면 맨손으로 바로 간다.
        _fetching = Treatment.BestInStorage(out _source);
        if (_fetching == null) { BeginTreating(doctor); return; }

        _phase    = Phase.GoToStorage;
        TileCoord = WorkSpot.TileFor(_source);   // 창고도 자기 칸은 통행 불가다
    }

    // 스스로 싸매는가 — 우클릭으로 자기를 지목한 경우. 회복량과 시간에 `SelfPenalty`가 붙는다.
    bool IsSelf(UnitBrain doctor) => doctor != null && doctor == Patient;

    void BeginTreating(UnitBrain doctor)
    {
        _phase = Phase.GoToPatient;

        // 침대에서 난 일감은 침대 옆에 서고, 그냥 지목한 폰은 그 자리로 간다.
        // **자기 자신이면 갈 곳이 없다** — 있는 자리에서 바로 시작한다.
        if (_bed != null)                 TileCoord = WorkSpot.TileFor(_bed);
        else if (!IsSelf(doctor) && Patient != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(Patient.transform.position);

        // **시계는 도착해서 켠다**(0 = 아직 안 켬). 여기서 켜면 걸어가는 동안 흘러가서,
        // 멀리 있던 의사는 닿자마자 끝난다 — `Treatment.Seconds`도 의료 스탯도 도구 속도도
        // 아무것도 안 하는 죽은 값이 된다. `CraftJob`·`ResearchJob`이 `_timerStarted`로
        // 도착 후에 켜는 것과 같은 규약이다.
        _endTime = 0f;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_phase == Phase.GoToStorage)
        {
            // 그새 남이 가져갔을 수 있다 — 없으면 맨손으로 넘어간다(치료가 멎는 것보다 낫다).
            int took = 0;
            if (_source != null && _fetching != null)
            {
                int have = _source.Inventory.GetUnlockedQuantity(_fetching);
                took = Mathf.Min(FetchCount, have);
                if (took > 0)
                {
                    _source.Inventory.RemoveUnlocked(_fetching, took);
                    int got = unit.Inventory.TryAdd(_fetching, took);
                    // 배낭에 다 못 넣었으면 남은 것은 창고에 돌려놓는다 — 증발시키지 않는다.
                    if (got < took) _source.Inventory.TryAdd(_fetching, took - got);
                    took = got;
                }
            }

            _using = took > 0 ? _fetching : null;
            BeginTreating(unit);
            return;
        }

        var body = PatientBody;
        if (body == null) { Status = JobStatus.Done; return; }   // 환자가 일어났거나 죽었다

        // 도착했다 — 이제 시작한다.
        if (_endTime <= 0f)
        {
            float startSkill = unit.Stats != null ? unit.Stats.Medicine : 0f;
            _endTime = Time.time + Treatment.Seconds(_using, startSkill, IsSelf(unit));
            return;
        }

        if (Time.time < _endTime) return;

        // **약은 다 쓰고 나서 소모한다.** 먼저 빼면 도중에 끊겼을 때(폰이 밥 먹으러 가거나
        // 죽으면) 약만 사라진다. 그새 없어졌으면 맨손 값으로 친다.
        var used = Treatment.ConsumeFromBackpack(unit, _using) ? _using : null;

        float skill = unit.Stats != null ? unit.Stats.Medicine : 0f;
        float power = Treatment.Power(used, skill, IsSelf(unit));
        Treatment.Apply(body, power);

        // 노동 성장 — 다른 노동과 같은 축(SOL). 상대가 얼마나 다쳤는지는 안 본다:
        // 중상을 골라 치료하는 것이 이득이 되면 가벼운 부상을 방치하게 된다.
        if (unit.Stats != null)
            unit.Stats.GrowStat(StatName.Medicine, 6f * CombatXp.Sol(skill));

        Debug.Log($"[Treat] {unit.UnitName} → {Patient?.UnitName} " +
                  $"({(used != null ? used.ItemName : "맨손")}, 배율 {power:F2})");

        Status = JobStatus.Done;
    }
}

using UnityEngine;

// **시체를 갈라 쓸 것을 꺼낸다.**
//
// 시체가 저절로 사라지지 않는 세상에서 이건 **치우는 길 셋 중 하나**다(나머지는 매장·화장) —
// 그래서 자원을 얻는 일인 동시에 뒷정리다.
//
// **일하는 자리는 도축대 하나다** — 갈리는 것은 *누가 시켰나*뿐이다.
//   - **우클릭 `[도축한다]`** — 사람이 콕 집어 시킨다. 작업 설정을 안 본다(`ReserveFor`).
//   - **도축대 규칙**(`ButcherBill`) — 조건에 맞는 시체를 저 혼자 찾아 건다.
//
// 둘 다 **가장 가까운 도축대로 업어 간다**(`ButcherTable.NearestTo`). 도축대가 **아직 없을
// 때만** 그 자리에서 가른다 — 가죽이 첫 방어구의 재료라, 도축대를 짓기 전에도 사냥한 것을
// 손질할 수 있어야 한다.
//
// **규칙은 시체만 고른다.** 쓰러진 것을 자동으로 가르기 시작하면 **붙잡는 일이 성립하지
// 않는다** — 적을 때려눕히는 것은 회유·몸값을 노리는 행동인데, 그 사이에 폰이 알아서 갈라
// 버리면 포로를 만들 길이 사라진다. 산 것을 가르는 것은 늘 **손으로 시킬 때만**이다.
//
// **쓰러진 것도 가른다.** 기절은 회복 가능한 상태라(켄시식) 그대로 두면 일어나는데, 그건
// *"잡았다"*가 아니다 — 짐승을 때려눕히고 그 자리에서 손질하는 것이 사냥의 정상 경로다.
// 그리고 **가장 신선할 때** 가르는 길이기도 하다(죽여 놓고 이틀을 기다릴 이유가 없다).
//
// 대가는 `UnitBrain.Die`가 이미 문다 — 죽이면 그 세력 우호도가 떨어지고, **가둬 둔 자를
// 가르면 처형과 같은 값**이 붙는다(`ExecutePenalty`). 여기서 따로 셈하지 않는다.
public class ButcherJob : JobBase
{
    // **시체든 산 채로 쓰러진 것이든 같은 일감이다.** 규칙을 둘로 나누면 "쓰러진 것은 되는데
    // 시체는 안 되는" 구멍이 생긴다 — 대상은 게임오브젝트 하나로 받고, 산 것이면 마무리에서
    // 죽인다(그때 시체가 되고, 그 뒤는 똑같다).
    readonly GameObject   _target;
    readonly ButcherTable _table;

    // **도축대가 있으면 두 단계다** — 업으러 가서, 업고 와서, 여기서 가른다.
    // 없으면 그 자리에서 가른다(우클릭 `[도축한다]`).
    enum Phase { Work, Fetch }
    Phase _phase = Phase.Work;

    public ButcherJob(GameObject target, ButcherTable table = null)
    {
        _target = target;
        _table  = table;
        _phase  = table != null ? Phase.Fetch : Phase.Work;

        // 어느 쪽이든 **먼저 가는 곳은 시체**다 — 도축대가 있으면 업으러, 없으면 가르러.
        SetSite(target != null ? target.transform : null);

        Priority = 5;
    }

    Corpse    _corpse => _target != null ? _target.GetComponent<Corpse>()    : null;
    UnitBrain _brain  => _target != null ? _target.GetComponent<UnitBrain>() : null;

    public override JobCategory Category => JobCategory.Butcher;
    // **도축대에서는 서서, 바닥에서는 쭈그려 가른다** — 일감이 이미 어느 쪽인지 안다.
    public override WorkAnim    Anim     => _phase != Phase.Work ? WorkAnim.None
                                          : _table != null      ? WorkAnim.Skinning
                                          :                       WorkAnim.SkinningGround;

    // 시체 옆에 붙어서 한다 — 손만 닿으면 되는 일이 아니라 **거기 서서** 하는 일이다.
    // 다만 **업으러 갈 때는 손이면 된다**(집어 드는 일이다 — `ConstructionJob`이 창고에
    // 들를 때와 같은 규약).
    public override float Reach => _phase == Phase.Fetch ? HandReach : StandReach;

    public GameObject Target => _target;

    // 도축대 앞에 닿아 **가르고 있는 중인가** — 업으러 가는 동안은 거짓이다.
    // 도축대가 걸린 고기를 켜고 끄는 기준이 이것 하나다.
    public bool AtTable => _table != null && _phase == Phase.Work;

    // 대상이 그새 없어졌으면(파괴됐거나) 할 일이 없다.
    public override bool IsReady => _target != null;

    // **업혀 있는 것은 못 가른다** — 남의 어깨 위에서 할 일이 아니다.
    // **자기 자신도 못 가른다**(쓰러진 폰에게 시킬 수는 없다 — 그건 일감이 아니라 사고다).
    public override bool CanAssignTo(UnitBrain unit)
    {
        if (_target == null || unit == null) return false;
        if (unit.gameObject == _target) return false;

        var bb = _brain != null ? _brain.Blackboard : null;
        // **내가 업고 있는 것은 예외다** — 도축대로 나르는 중이 바로 그 상태다.
        if (bb != null && bb.CarriedBy != null && bb.CarriedBy != unit) return false;

        // **이미 남을 업은 폰에게는 안 준다**(도축대 몫만) — 어깨는 하나라
        // `PawnCarry.CanCarry`가 거절하는데, 그 사실이 **가서야** 드러난다: 폰은 시체까지
        // 걸어갔다가 못 업고 일감이 죽고, 도축대는 1초 뒤 같은 일감을 다시 걸고, 그 폰이 또
        // 집어 간다 — 자리에서 맴도는 고리가 된다. 걸러야 할 곳은 배정이다.
        if (_table != null && unit.Blackboard != null
            && unit.Blackboard.Carrying != null && unit.Blackboard.Carrying != _brain)
            return false;

        return true;
    }

    // 얼마나 걸리나 — 요리 솜씨가 손을 빠르게 한다(스킬 축을 새로 만들지 않는다).
    //
    // **6초는 너무 빨랐다** — 몸 하나가 눈 깜짝할 사이에 사라져 도축이 *일*로 안 보였다.
    // 제작 시간이 6~22초인 것과 같은 눈금에 둔다.
    const float BaseSeconds = 22f;

    // **죽이는 것도 일이다**(림월드의 도살). 쓰러진 것을 올려놓자마자 죽어 버리면 그건
    // 작업이 아니라 판정이고, 무엇보다 **무를 수 있는 시간이 0**이 된다.
    const float SlaughterSeconds = 7f;

    // 한 구를 마치면 요리가 이만큼 는다(SOL이 곱해진다). 제작 한 번(0.2~0.7)보다 크고
    // 치료 회차(6)보다 작다 — 몸 하나가 그 사이의 일이다.
    const float CookingXp = 3f;

    float _worked;
    bool  _started;   // 자리를 잡았나 — 내려놓기는 한 번만 한다
    bool  _slain;     // 죽이는 일을 마쳤나

    // 지금 무엇을 하고 있나 — 도축대 창의 게이지가 이걸 읽는다.
    public bool  Slaughtering => _started && !_slain;
    public float Progress =>
        !_started ? 0f
        : !_slain ? Mathf.Clamp01(_worked / SlaughterSeconds)
        :           Mathf.Clamp01(_worked / BaseSeconds);

    // 그만둘 때 도축대가 게이지를 붙들고 있지 않게 — 취소는 밖에서도 온다.
    protected override void OnCancelled()
    {
        // 취소는 밖에서도 온다(멈춤 버튼·철거). 붙들어 둔 몸을 풀고 게이지도 놓는다.
        _table?.ReleaseLaid();
        _table?.SetWork(null);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_target == null) { Status = JobStatus.Done; return; }

        // **가는 사이에 깨어났으면 그만둔다.** 쓰러진 것을 가르는 것과 멀쩡한 것을 덮치는
        // 것은 다른 일이다 — 후자는 명령이 아니라 살인이고, 시켜 놓고 자리를 비운 사이에
        // 그렇게 되면 안 된다.
        if (_corpse == null && !Butchering.CanButcherAlive(_brain))
        {
            // **업고 있었으면 내려놓는다.** 도축대까지 메고 가는 길이 생기면서 *깨어나는
            // 순간 손에 사람이 남는* 경우가 실제로 생긴다 — 안 내려놓으면 시킨 적 없는
            // 납치가 되고, 폰은 갈 곳 없는 사람을 어깨에 진 채로 다음 일을 하러 간다.
            var bb0 = _brain != null ? _brain.Blackboard : null;
            if (bb0 != null && bb0.CarriedBy == unit) PawnCarry.PutDown(unit);

            // 상판에 붙들어 뒀으면 풀어 준다 — 안 풀면 산 채로 도마 위에 굳는다.
            _table?.ReleaseLaid();
            _table?.SetWork(null);

            Status = JobStatus.Done;
            return;
        }

        // ── 업으러 왔다 ──────────────────────────────────────────────────────
        //
        // 도착했으니 업고, 목표를 **도축대로 바꾼다**(`ConstructionJob`이 자재를 챙긴 뒤
        // 청사진으로 돌아서는 것과 같은 모양). 못 업으면 그만둔다 — 그 사이에 남이 업었거나
        // 사라진 것이라, 붙들고 서 있어 봐야 다음 일감만 막는다.
        if (_phase == Phase.Fetch)
        {
            var bb = _brain != null ? _brain.Blackboard : null;
            bool mine = bb != null && bb.CarriedBy == unit;

            // **이 일감이 시킨 업기다** — `keepCurrentJob`을 안 넘기면 `TryPickUp`이
            // `AbandonWork`로 **이 일감 자신**을 취소해, 폰이 시체를 든 채 영영 서 있는다.
            if (!mine && !PawnCarry.TryPickUp(unit, _brain, keepCurrentJob: true))
            {
                Status = JobStatus.Done;
                return;
            }

            _phase    = Phase.Work;
            TileCoord = WorkSpot.TileFor(_table);
            return;
        }

        // ── 자리를 잡는다 ───────────────────────────────────────────────────
        //
        // **한 번만 한다** — 업은 것을 내려놓고, 도축대가 있으면 상판에 눕힌다.
        if (!_started)
        {
            _started = true;
            _slain   = _corpse != null;   // 이미 시체면 죽이는 일은 건너뛴다

            var bb = _brain != null ? _brain.Blackboard : null;
            if (bb != null && bb.CarriedBy == unit) PawnCarry.PutDown(unit);

            // **산 것도 여기서 상판에 올린다 — 죽이는 것도 도축대에서 하는 일이다.**
            //
            // 한때 시체만 올렸다("바닥에서 잡고, 상판에 올려 가른다"). 그런데 그러면 **죽이는
            // 7초 동안 몸이 폰 발밑에 누워 있어** 도축대가 옆에 선 가구가 된다 — 같은 일감의
            // 두 단계가 자리를 달리할 이유가 없다.
            //
            // 그때 걱정한 둘은 각자 제자리에서 풀렸다:
            //   ① `PawnMover`가 도로 끌어내린다 → `LayOnTable`이 `SetSleeping`으로 붙든다
            //      (보정을 그쪽이 이미 하므로 밖에서 또 빼지 않는다).
            //   ② 죽을 때 전리품이 도마 위에 쌓인다 → 떨구는 자리를 아래에서 옆 바닥으로 돌린다.
            if (_table != null) _table.LayOnTable(_brain);
            _table?.SetWork(this);
        }

        float skill = unit.Stats != null ? unit.Stats.Cooking : 0f;
        _worked += unit.Blackboard.TickDelta * (0.5f + skill / 100f);

        // ── 죽이는 일 ───────────────────────────────────────────────────────
        //
        // **산 것은 먼저 죽인다 — 그것도 일이다**(림월드의 도살). 올려놓자마자 죽어 버리면
        // 그건 작업이 아니라 판정이고, 무엇보다 **무를 수 있는 시간이 0**이 된다.
        //
        // 이 단계 동안에도 여전히 물릴 수 있다(위 `CanButcherAlive` 검사가 매 틱 돈다) —
        // 다만 깨어나면 그만두는 것이지, 사람이 취소하는 것도 여기까지다.
        if (!_slain)
        {
            if (_worked < SlaughterSeconds) return;

            _slain  = true;
            _worked = 0f;

            // **죽는다고 물건이 떨어지지는 않는다** — 지녔던 것은 태어날 때부터 배낭에 있고
            // (`UnitBrain.FillPockets`), 그대로 시체 배낭이 된다. 꺼내는 것은 손질을 마칠 때
            // `Corpse.SpillBelongings`가 **옆 작업 자리**에 한 번에 한다. 그래서 상판 위에서
            // 죽여도 도마에 동전이 쌓이지 않는다 — 떨구는 시점 자체가 사라졌다.
            _brain?.Die();

            // **자리를 다시 잡는다.** 죽으면서 `PawnMover`가 꺼졌으므로(`BecomeCorpse`)
            // 붙들려 있던 몸이 그냥 놓인 몸이 됐다 — 값은 그대로지만 두 경로가 같은 식으로
            // 모이게 한 번 더 맞춘다.
            if (_table != null) _table.LayOnTable(_brain);
            return;
        }

        if (_worked < BaseSeconds) return;

        Finish(unit);
        Status = JobStatus.Done;
    }

    // **나온 것은 손질한 폰이 든다 — 못 드는 만큼만 발밑에 떨군다.**
    //
    // 한때 전부 바닥에 떨궜다. 이유는 *"배낭에 밀어 넣으면 짐이 찬 폰에게서 조용히 증발한다"*
    // 였는데, 그건 반환값을 안 보고 밀어 넣을 때의 이야기다(`LoadoutData.GiveOrDrop`이 이미
    // 그렇게 푼다). 바닥에 두는 쪽이 오히려 나빴다 — **바닥 물건은 기본으로 아무도 안 나른다**
    // (2026-08-13, `DroppedItem.HaulAllowed`). 그래서 도축할 때마다 H로 찍어 주지 않으면
    // 가죽이 시체가 있던 자리에 그대로 남았다.
    //
    // 배낭에 넣으면 **짐내림(`UnloadJob`)이 저절로 창고로 옮긴다** — 이미 도는 큐 작업이라
    // 새 규칙이 없다. 손질한 폰은 그 자리에 서서 손을 넣고 있는 사람이니 **드는 것이 자연스럽고**,
    // 한도(`CarryCapacity`)를 지키므로 무거운 가죽을 지면 느려지는 것도 그대로 걸린다.
    void Finish(UnitBrain unit)
    {
        var race = _brain != null ? _brain.Data : null;
        var at   = _target.transform.position;

        // 산 것은 **시작할 때 이미 죽였다**(위 `_started`) — 여기 오면 늘 시체다.
        var corpse = _corpse;
        if (corpse == null) { Status = JobStatus.Done; return; }   // 시체가 안 남는 종류였다

        var stage = corpse.Stage;

        int kinds = 0;
        foreach (var (item, count) in Butchering.Roll(race, stage))
        {
            GiveOrDrop(unit, item, count, at);
            kinds++;
        }

        // **요리는 한 구를 마칠 때 한 번 는다** — 틱마다 주면 스물아홉 배가 된다.
        // 제작(`SkillGrowthAmount` 0.2~0.7)과 치료 회차(6)와 같은 눈금이고, 몸 하나가
        // 제작 한 번보다 오래 걸리므로 그 사이에 둔다.
        if (unit != null && unit.Stats != null)
            unit.Stats.GrowStat(StatName.Cooking, CookingXp * CombatXp.Sol(unit.Stats.Cooking));

        // 한 구를 마쳤다 — 규칙의 남은 횟수는 도축대가 센다. 상판도 비운다.
        _table?.SetWork(null);
        _table?.ClearLaid();
        _table?.BodyFinished();

        // **시체는 여기서 없어진다.** 가르고 남은 것을 또 가를 수는 없다 — 무엇보다 이것이
        // 지금 유일한 치우는 길이라, 안 지우면 도축이 자원만 늘리고 자리는 그대로 둔다.
        //
        // 시체가 지녔던 것이 있으면 **같이 떨군다** — 안 뒤지고 도축했다고 유품이
        // 사라지면 그건 조용한 손실이다(`BecomeCorpse`가 막으려던 바로 그것).
        // **배낭뿐 아니라 차고 있던 장비까지** 나온다 — 그 답은 `Corpse`가 든다(한때
        // 여기서 배낭만 훑어 무기·갑옷이 시체와 함께 사라졌다).
        //
        // **유품은 상판이 아니라 옆 바닥에 떨군다.** 몸을 상판에 눕혀 놓았으므로 그 자리에
        // 떨구면 **동전과 장비가 도마 위에 쌓인다** — 도축이 내놓는 것(통에 들어간다)과
        // 죽은 자가 지녔던 것(바닥에 떨어져 운반 축을 탄다)은 다른 축이라, 자리도 갈라야
        // 무엇이 무엇인지 보인다.
        corpse.SpillBelongings(_table != null ? _table.ApproachPosition : at);

        string what = kinds > 0 ? $"{Butchering.StageName(stage)} 시체를 손질했습니다"
                                : $"{Butchering.StageName(stage)} 시체에서는 나올 것이 없었습니다";
        EventNotificationUI.Notify("도축", $"{corpse.UnitName} — {what}.");

        Object.Destroy(corpse.gameObject);
    }

    // **도축대 → 폰 배낭 → 바닥.** 작업대 산출물과 **같은 순서**다(`CraftJob`) — 뒤로 갈수록
    // 나쁜 쪽이고, 도축대가 맨 앞인 것은 거기 쌓이면 `HaulDispatcher`가 알아서 실어 가기
    // 때문이다. 도축대가 없으면(우클릭 도축) 배낭부터다.
    //
    // 배낭 한도는 무게(`CarryCapacity.RoomFor`)와 **격자 자리**(`Inventory.TryAdd`의 반환값)
    // 둘 다 본다 — 어느 하나만 보면 다른 쪽에서 조용히 샌다.
    //
    // 한때 전부 바닥에 떨궜다. 이유는 *"배낭에 밀어 넣으면 짐이 찬 폰에게서 조용히 증발한다"*
    // 였는데, 그건 반환값을 안 보고 밀어 넣을 때의 이야기다. 바닥에 두는 쪽이 오히려 나빴다 —
    // **바닥 물건은 기본으로 아무도 안 나른다**(`DroppedItem.HaulAllowed`).
    void GiveOrDrop(UnitBrain unit, ItemData item, int count, Vector3 at)
    {
        if (item == null || count <= 0) return;

        int left = count;

        // ── 도축대에 쌓는다 ─────────────────────────────────────────────────
        //
        // **못 쌓았으면 그 사실을 적는다.** 산출물이 바닥으로 새는 것은 화면에서 "왜 도축대에
        // 안 쌓이지"로만 보이고, 원인(통이 없다 / 칸이 찼다)은 아무 데도 안 남는다 —
        // 조용한 실패를 만들지 않는 것이 이 저장소의 관례다.
        if (_table != null)
        {
            if (_table.Inventory == null)
            {
                Debug.LogError($"[Butcher] {_table.name}에 산출물 통이 없습니다 — " +
                               $"{item.ItemName} {left}개가 배낭·바닥으로 갑니다.", _table);
            }
            else
            {
                int before = left;
                left -= _table.Inventory.TryAdd(item, left);

                if (left > 0)
                    Debug.LogWarning(
                        $"[Butcher] {_table.name}이(가) {item.ItemName} {before}개 중 " +
                        $"{left}개를 못 받았습니다(격자 {_table.Inventory.Width}x" +
                        $"{_table.Inventory.Height}, 든 뭉치 {_table.Inventory.Items.Count}개). " +
                        "배낭·바닥으로 갑니다.", _table);
            }
        }

        var inv = unit != null ? unit.Inventory : null;
        if (left > 0 && inv != null)
        {
            int room = CarryCapacity.RoomFor(unit, item);
            if (room > 0) left -= inv.TryAdd(item, Mathf.Min(room, left));
        }

        if (left > 0)
            DroppedItem.Spawn(item, left,
                              (_table != null ? _table.ApproachPosition : at) + Vector3.up * 0.15f);
    }

}

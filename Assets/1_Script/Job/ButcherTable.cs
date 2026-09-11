using System.Collections.Generic;
using UnityEngine;

// **도축대 — 규칙을 걸어 두면 시체가 알아서 치워진다.**
//
// 우클릭 `[도축한다]`는 한 구씩 사람이 시키는 길이라, 싸움이 끝나고 시체가 열 구 널브러지면
// 그것만 열 번 눌러야 한다. 도축대는 그 일을 **규칙 하나로** 바꾼다(`ButcherBill`).
//
// **하는 일은 셋이다.**
//   1. 규칙에 맞는 시체를 찾아 일감을 하나 건다(`SiteJobSlot` — 자리가 일감 하나를 든다).
//   2. 폰이 그 시체를 **업어 와서** 여기서 가른다(`ButcherJob`의 두 단계).
//   3. 나온 것은 **여기 쌓인다**(`Inventory`) — 작업대와 같은 규약이라, `allowHaulOut`이
//      켜져 있으면 `HaulDispatcher`가 창고로 실어 간다.
//
// **시체를 담아 두지 않는다.** 도축대는 일하는 자리지 시체를 얹어 두는 통이 아니다 —
// 폰이 업고 와서 내려놓고 그 자리에서 가른다. 담아 두게 만들면 "올려놨는데 아무도 안 가른다"는
// 상태가 하나 더 생기고, 그걸 비우는 길을 또 만들어야 한다.
public class ButcherTable : MonoBehaviour, IWorldClickable, IHaulSource
{
    public static readonly List<ButcherTable> All = new();

    // **좌클릭하면 제 창이 열린다**(`ButcherTableUI`) — 규칙을 손보는 것은 거기서 한다.
    // 살펴보기 창은 지금 어떻게 걸려 있는지만 적는다(창고·작업대와 같은 짝).
    public static event System.Action<ButcherTable> OnSelected;

    [Header("산출물")]
    [SerializeField] int  gridWidth  = 6;
    [SerializeField] int  gridHeight = 3;
    [SerializeField] bool allowHaulOut = true;

    [Header("규칙")]
    [SerializeField] ButcherBill bill = new();

    [Tooltip("시체를 이 거리 안에서만 찾는다. 0이면 맵 전체.")]
    [SerializeField] float searchRadius = 0f;

    // **손질하는 동안에만 고기가 걸린다.** 늘 걸어 두면 그건 장식이고, 안 걸면 도축대가
    // 무엇을 하는 물건인지 서 있는 모습만으로는 안 보인다 — 갈고리는 늘 있고 **거기 걸린
    // 것이 지금 일하고 있다는 표시**다(연구대 게이지·용광로 불꽃과 같은 자리).
    [Tooltip("손질 중에만 켤 묶음. 비우면 아무 일도 안 한다.")]
    [SerializeField] GameObject hangingMeat;

    [Tooltip("손질하는 동안 몸을 눕힐 자리(상판 위). 비우면 도축대 한가운데.")]
    [SerializeField] Transform tableAnchor;

    public ButcherBill Bill      => bill;
    public Inventory   Inventory { get; private set; }

    public bool AllowHaulOut     => allowHaulOut;

    SiteJobSlot     _slot;
    HaulDispatcher  _haul;
    float           _tick;
    float           _haulTimer;

    void Awake()
    {
        // **산출물 통은 여기서 한 번 세운다.** 없으면 나중에 몰래 만들지 않고 **적어 둔다** —
        // 지연 생성은 고장을 고쳐 주는 게 아니라 **가려 준다**: 통이 없다는 것은 배선이나
        // 순서가 어긋났다는 뜻인데, 그때마다 새로 만들어 주면 원인이 영영 안 드러난다.
        Inventory = gameObject.AddComponent<Inventory>();
        if (!Inventory.Resize(gridWidth, gridHeight))
            Debug.LogError($"[ButcherTable] {name}: 산출물 격자를 {gridWidth}x{gridHeight}로 " +
                           "못 잡았습니다(이미 물건이 든 통). 산출물이 바닥으로 샙니다.", this);

        // **만드는 법은 한 번만 묶는다**(`SiteJobSlot` 주석) — 매 틱 클로저가 나지 않게.
        _slot = new SiteJobSlot(MakeJob, () => PickTarget() != null);
        _haul = new HaulDispatcher(this);
    }

    void OnEnable()  => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        _slot?.Drop();

        // **꺼질 때 상판을 비운다**(철거·휴면). `Drop`은 **아직 시작 안 한 일감만** 끊으므로
        // 손질 중이던 몸은 아무도 안 풀어 준다 — 붙들린 채 남으면 `SetSleeping`이 자세를
        // 붙들고 기절 시계까지 멈춰 있어(`Restrained`) **도마 위에서 영영 굳는다.**
        ReleaseLaid();
    }

    JobBase MakeJob(UnitBrain worker)
    {
        var target = PickTarget();
        if (target == null) return null;

        var job = new ButcherJob(target.gameObject, this);
        return job;
    }

    // 규칙에 맞는 시체 중 **가장 가까운 것**. 이미 누가 업고 있거나 묻힌 것은 뺀다.
    //
    // **`Corpse.All` 같은 레지스트리를 새로 만들지 않는다** — 시체는 `Corpse` 컴포넌트로
    // 찾으면 되고(맵에 수십 구지 수천 구가 아니다), 1초에 한 번만 훑는다. 레지스트리를 두면
    // 등록·해제 자리가 늘고 그중 하나가 빠지면 유령 항목이 남는다.
    Corpse PickTarget()
    {
        if (!bill.Wanted) return null;
        if (BuildingDormancy.IsDormantAt(this)) return null;

        var here = transform.position;
        float radiusSq = searchRadius > 0f ? searchRadius * searchRadius : float.MaxValue;

        Corpse best = null;
        float  bestSq = float.MaxValue;

        foreach (var corpse in Corpse.All)
        {
            if (corpse == null || !bill.Matches(corpse)) continue;

            // 나올 것이 없으면 나를 값이 없다 — 폰을 왕복시키고 빈손으로 돌아온다.
            var race = corpse.GetComponent<UnitBrain>()?.Data;
            if (!Butchering.HasYield(race, corpse.Stage)) continue;

            // 남이 업고 가는 중이면 건드리지 않는다.
            var bb = corpse.GetComponent<UnitBrain>()?.Blackboard;
            if (bb != null && bb.CarriedBy != null) continue;

            // **이미 일감이 걸린 몸도 뺀다.** 손으로 시킨 도축(우클릭)은 이 자리를 안 거치므로
            // 규칙이 같은 시체를 한 번 더 고를 수 있고, 그러면 **두 폰이 같은 시체로 걸어간다.**
            if (JobManager.Instance != null && JobManager.Instance.HasButcherJobFor(corpse.gameObject))
                continue;

            float sq = (corpse.transform.position - here).sqrMagnitude;
            if (sq > radiusSq || sq >= bestSq) continue;

            best   = corpse;
            bestSq = sq;
        }
        return best;
    }

    // **상판에 올려 둔 몸.** 손질이 끝나면 사라지지만, 도중에 그만두면(폰이 다른 일을 하러
    // 갔거나 사람이 취소했거나) **그대로 누워 있는다** — 그때 치울 길이 있어야 한다.
    // 도축대 콜라이더가 상판을 감싸고 있어서 그 위의 몸은 **커서로 집을 수가 없다.**
    public UnitBrain Laid { get; private set; }

    // 아직 그 자리에 있나 — 남이 업어 갔거나 사라졌으면 손을 뗀다(화장터와 같은 판정).
    public UnitBrain LaidBody
    {
        get
        {
            if (Laid == null) return null;
            if (Laid.Blackboard != null && Laid.Blackboard.CarriedBy != null) { Laid = null; return null; }
            return Laid;
        }
    }

    // **몸은 산 것이든 시체든 상판에 오른다.**
    //
    // 한때 시체만 올렸다. 이유는 산 것을 올려 두면 **다음 프레임에 도로 떨어지기** 때문인데
    // (`PawnMover.Update`는 `_cc`가 켜져 있는 한 매 프레임 중력을 먹인다 — 기절은
    // `IsSleeping`이 아니라 회전만 눕힌 상태다), 그러면 **죽이는 7초 동안 몸이 폰 발밑
    // 바닥에 누워 있다.** 도축대는 옆에 선 가구가 되고, 화면에서는 *"왜 저기서 잡고 있지"*
    // 로만 보인다 — 같은 일감의 두 단계가 자리를 달리할 이유가 없다.
    //
    // 붙드는 길은 새로 만들 것 없이 이미 있다 — **`PawnMover.SetSleeping`**(침대·우리가
    // 쓰는 그것). 컨트롤러를 끄고 `Update`를 통째로 건너뛰므로 중력이 안 걸리고, 눕는 동안의
    // 클릭 캡슐(`_lyingPick`)까지 같이 켜져 상판 위의 몸을 커서로 집을 수 있다.
    //
    // **눕히는 보정은 그쪽이 제 몫으로 이미 한다**(`lyingPos - lyingRot * up * CenterOffset`) —
    // 밖에서 한 번 더 빼면 **두 번 빠진다.** 그래서 산 몸에는 앵커를 **그대로** 넘긴다.
    public void LayOnTable(UnitBrain body)
    {
        if (body == null) return;
        Laid = body;

        var spot = tableAnchor != null ? tableAnchor.position : transform.position;
        var rot  = Quaternion.Euler(0f, transform.eulerAngles.y, 90f);

        // **묶어 두는 동안은 기절에서 안 깨어난다**(2026-09-11) — 상판은 쉬는 자리가 아니다.
        if (body.Blackboard != null) body.Blackboard.Restrained = true;

        // ── 산 몸 — 붙들어 눕힌다 ───────────────────────────────────────────
        var mover = body.GetComponent<PawnMover>();
        if (mover != null && mover.enabled)
        {
            // 이미 누워 있으면(다른 자리에서 옮겨 왔다) 한 번 일으켜야 다시 눕는다 —
            // `SetSleeping`은 같은 값으로 부르면 **아무 일도 안 하고 돌아간다.**
            if (mover.IsSleeping) mover.SetSleeping(false);

            mover.SetSleeping(true, spot + Vector3.up * PawnPlacement.GroundClearance, rot);
            return;
        }

        // ── 시체 — 그냥 옮기면 그 자리에 있는다(`BecomeCorpse`가 `PawnMover`를 껐다) ──
        //
        // 이쪽은 보정을 직접 하되 **수평 성분만** 뺀다 — 높이는 `PlaceOnGround`가 얹는다.
        var lean = rot * Vector3.up * PawnPlacement.CenterOffset(body.gameObject);
        PawnPlacement.PlaceOnGround(body.gameObject, spot - new Vector3(lean.x, 0f, lean.z), rot);
    }

    // 상판에서 손을 뗀다. **산 몸은 붙들려 있으므로 반드시 풀어야 한다** — 안 풀면
    // 도마 위에 산 채로 굳는다(가는 사이에 깨어났을 때·취소했을 때가 그 자리다).
    // `SetSleeping(false)`가 붙들기 직전 자리로 되돌려 놓는다.
    public void ReleaseLaid()
    {
        var body = Laid;
        Laid = null;
        if (body == null) return;

        // 풀었으면 기절 시계도 다시 간다 — 놓아 준 몸은 깨어나도 된다.
        if (body.Blackboard != null) body.Blackboard.Restrained = false;

        var mover = body.GetComponent<PawnMover>();
        if (mover != null && mover.enabled && mover.IsSleeping) mover.SetSleeping(false);
    }

    // 손질이 끝나 몸이 사라졌을 때 — 풀어 줄 것이 없으므로 잊기만 한다
    // (`ReleaseLaid`는 **산 것을 도로 움직이게** 하는 짝이라 여기서는 안 쓴다).
    public void ClearLaid()
    {
        // 몸이 사라졌으니 풀어 줄 것은 없지만 **표시는 지운다** — 일으켜진 시체
        // (`UnitBrain.Rise`)처럼 같은 오브젝트가 다시 서는 길이 있다.
        if (Laid != null && Laid.Blackboard != null) Laid.Blackboard.Restrained = false;
        Laid = null;
    }

    // 상판에서 내린 몸을 놓을 자리 — **폰이 서는 칸**(`WorkSpot`). 도축대 발밑에 두면
    // 이번엔 건물 발자국 안에 눕는다.
    public Vector3 ApproachPosition
    {
        get
        {
            var spot = GetComponentInChildren<WorkSpot>(true);
            return spot != null ? spot.transform.position : transform.position;
        }
    }

    // 한 구를 마쳤다고 규칙에 알린다 — 횟수를 세는 것은 규칙의 몫이다.
    public void BodyFinished()
    {
        bill.Done();
    }

    // ── 지금 무슨 일이 벌어지고 있나 ────────────────────────────────────────
    //
    // **일감이 스스로 알린다**(`SetWork`). `_slot.Current`만 보면 **손으로 시킨 도축을
    // 못 본다** — 우클릭으로 낸 일감은 규칙의 자리를 안 거치기 때문이다. 그러면 사람이
    // 직접 시켰을 때 갈고리에 고기도 안 걸리고 게이지도 안 돈다.
    ButcherJob _work;

    public void SetWork(ButcherJob job)
    {
        if (job == null && _work != null && _work.Status == JobStatus.InProgress) return;
        _work = job;
    }

    ButcherJob Work =>
        _work != null && _work.Status == JobStatus.InProgress && _work.AtTable ? _work : null;

    // 지금 이 도축대에서 **실제로 손을 대고 있나**(업으러 가는 중은 아니다).
    public bool Cutting => Work != null;

    // 죽이는 중인가 — 게이지 글자가 갈린다.
    public bool Slaughtering => Work != null && Work.Slaughtering;

    // 진행도(0~1). 아무 일도 없으면 0.
    public float Progress => Work != null ? Work.Progress : 0f;

    void Update()
    {
        // 고기는 매 프레임 본다 — `_tick`(1초)에 얹으면 손질이 끝나고도 한참 걸려 있다.
        if (hangingMeat != null && hangingMeat.activeSelf != Cutting)
            hangingMeat.SetActive(Cutting);

        _tick += Time.deltaTime;
        if (_tick >= 1f)
        {
            _tick = 0f;
            _slot.Tick(bill.Wanted);
        }

        // 쌓인 산출물을 창고로 — 작업대와 **같은 발행자**(`HaulDispatcher`)를 쓴다.
        // 완성품은 한 개도 놀려 두지 않으므로 임계값 1.
        _haulTimer += Time.deltaTime;
        if (_haulTimer < 2f) return;
        _haulTimer = 0f;

        if (!allowHaulOut || Inventory == null || Inventory.Items.Count == 0) return;
        if (JobManager.Instance == null || BuildingDormancy.IsDormantAt(this)) return;
        _haul.Dispatch(1);
    }

    // ── 규칙 손보기 (우클릭 창) ──────────────────────────────────────────────
    //
    // **창을 새로 안 짓는다** — 우클릭 창이 이미 목록 그릇이다(흥정 상대를 고르는 창과 같은
    // 판단). 값이 몇 가지 안 되므로 누르면 **다음 값으로 돌아간다**: 목록 안 목록을 열면
    // 두 번 눌러야 하고, 씬에 패널을 새로 그리면 그 배선이 또 하나 는다.
    public void CycleTarget()
    {
        bill.Target = bill.Target == ButcherBill.Whom.Hostile   ? ButcherBill.Whom.NotAllied
                    : bill.Target == ButcherBill.Whom.NotAllied ? ButcherBill.Whom.Everyone
                    :                                             ButcherBill.Whom.Hostile;
    }

    public void CycleStage()
    {
        bill.Stage = bill.Stage == ButcherBill.Ripeness.Any       ? ButcherBill.Ripeness.NotBones
                   : bill.Stage == ButcherBill.Ripeness.NotBones  ? ButcherBill.Ripeness.FreshOnly
                   :                                                ButcherBill.Ripeness.Any;
    }

    // 무한 → 1 → 5 → 10 → 25 → 무한. 한 번에 하나씩 올리게 하면 스물다섯을 걸 때 손이 아프다.
    public void CycleRepeat()
    {
        if (bill.Mode == ButcherBill.Repeat.Forever)
        {
            bill.Mode      = ButcherBill.Repeat.Count;
            bill.Remaining = 1;
        }
        else
        {
            bill.Remaining = bill.Remaining switch { 1 => 5, 5 => 10, 10 => 25, _ => 0 };
            if (bill.Remaining == 0) bill.Mode = ButcherBill.Repeat.Forever;
        }
    }

    // **멈추면 하던 것도 멈춘다.** 규칙만 끄고 진행 중인 손질을 그대로 두면, 눌러도 아무
    // 일이 안 일어나는 것처럼 보인다 — 스위치가 거짓말을 하는 셈이다(운반 지정을 끄면
    // 가던 폰도 돌려세우는 것과 같은 판단: *"지금 그러지 마라"*가 헛걸음을 아끼는 것보다 앞선다).
    public void ToggleSuspended()
    {
        bill.Suspended = !bill.Suspended;
        if (!bill.Suspended) return;

        _slot.Drop();

        // 손으로 시킨 것이든 규칙이 낸 것이든, 지금 이 도축대에서 도는 일감을 거둔다.
        if (_work != null && _work.Status == JobStatus.InProgress)
            JobManager.Instance?.CancelJob(_work);
        _work = null;
    }

    public void SetAllowHaulOut(bool value)
    {
        if (allowHaulOut == value) return;
        allowHaulOut = value;
        _haulTimer   = 2f;      // 켜면 다음 프레임에 바로 발행되게
    }

    public void OnWorldClick() => OnSelected?.Invoke(this);

    // **손으로 시킨 도축이 갈 곳.** 가장 가까운, 쓸 수 있는 도축대.
    //
    // `bill.Suspended`는 **안 본다** — 멈춤은 *규칙을 돌리지 마라*이고 손으로 시킨 일은
    // 규칙의 결과가 아니다(작업 설정을 안 보는 것과 같은 줄). 휴면(핵 영역 밖)은 뺀다:
    // 그건 건물이 아예 작동을 멈춘 것이라 가 봐야 아무 일도 안 일어난다.
    public static ButcherTable NearestTo(Vector3 from)
    {
        ButcherTable best = null;
        float bestSq = float.MaxValue;
        foreach (var t in All)
        {
            if (t == null || BuildingDormancy.IsDormantAt(t)) continue;
            float sq = (t.transform.position - from).sqrMagnitude;
            if (sq >= bestSq) continue;
            best   = t;
            bestSq = sq;
        }
        return best;
    }

    // ── `IHaulSource` ────────────────────────────────────────────────────────
    public Vector2Int TileCoord =>
        MapManager.Instance != null ? MapManager.Instance.WorldToTile(transform.position) : default;
}

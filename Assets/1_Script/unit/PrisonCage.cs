using System.Collections.Generic;
using UnityEngine;

// **붙잡아 온 사람을 가두는 우리.** 한 명이 들어간다.
//
// 이 게임에는 문도 방도 없다 — 벽을 둘러 가둔다는 뜻이 성립하지 않는다. 그래서 켄시처럼
// **자리에 묶어 두는 것**으로 푼다: 우리에 넣으면 그 칸을 못 벗어나고, 깨어나도 마찬가지다.
//
// **죄수 침대와 나란한 물건이다**(`Bed.ForPrisoners`). 우리는 가두는 것이 전부라 싸고,
// 침대는 회복·치료가 붙는 대신 자리를 더 먹는다 — 살려서 회유할 사람은 침대에, 그냥
// 붙잡아 둘 사람은 우리에. 어느 쪽이든 **갇힌 상태는 하나**(`UnitBlackboard.IsPrisoner`)라
// BT 정지·자동 표적 제외 규칙이 둘로 갈리지 않는다.
public class PrisonCage : MonoBehaviour, IWorldClickable
{
    public static readonly List<PrisonCage> All = new();
    public static event System.Action<PrisonCage> OnSelected;

    [Tooltip("갇힌 사람이 서 있을 자리. 비우면 우리 한가운데.")]
    [SerializeField] Transform holdAnchor;

    public UnitBrain Prisoner { get; private set; }
    public bool      IsOccupied => Prisoner != null;

    public Vector3 HoldPosition => holdAnchor != null ? holdAnchor.position : transform.position;

    void OnEnable()  => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        DropPersuadeJob();
        // **우리가 사라지면 갇힌 사람도 풀린다.** 안 풀면 아무 데도 없는 우리에 묶인 채
        // BT가 멎어 영영 안 움직이는 폰이 남는다.
        Release();
    }

    // ── 설득 일감 ────────────────────────────────────────────────────────────
    //
    // **우리가 일감을 낸다** — `Bed`가 `TreatJob`을 내는 것과 같은 자리다. 포로 자신이 내면
    // 넘어오거나 죽었을 때 스스로 등록을 못 지우고, 무엇보다 **누르는 사람마다 하나씩 만들면
    // 여럿이 동시에 붙는다**(세 명이 [설득한다]를 누르면 세 회차가 나란히 돌아 게이지가
    // 세 배로 찼다). 자리가 하나면 일감도 하나다.
    //
    // **한 회차씩** 건다 — 끝나면 아직 남았을 때 다음 것이 걸린다. 그래야 회차 사이에
    // 다른 급한 일(치료)이 끼어들 수 있고, 도중에 넘어오면 거기서 멈춘다.
    const float CheckInterval = 1f;

    PersuadeJob _persuadeJob;
    float       _nextCheck;

    void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + CheckInterval;

        if (_persuadeJob != null && _persuadeJob.Status is JobStatus.Done or JobStatus.Cancelled)
            _persuadeJob = null;

        // 넘어왔거나 풀려났거나, 애초에 말이 안 통하는 상대(하수인 종족 — 핵이 지배한다).
        if (!Conversion.CanPersuade(Prisoner)) { DropPersuadeJob(); return; }
        if (_persuadeJob != null) return;

        // **쿨타임 동안은 일감을 안 낸다.** 내 놓고 `IsReady`로 막으면 폰이 우리 앞에 서서
        // 20초를 기다린다 — 쿨타임을 둔 이유가 그 사이에 다른 일을 하라는 것이다.
        if (!Conversion.PersuadeReady(Prisoner)) return;
        if (JobManager.Instance == null) return;

        _persuadeJob = new PersuadeJob(Prisoner);
        JobManager.Instance.RegisterJob(_persuadeJob);
    }

    // **이 폰에게 맡긴다**(우클릭 [설득한다]). 새로 만들지 않고 **있던 일감을 넘긴다** —
    // 우리가 일감의 주인이라 여기서 하나 더 만들면 같은 포로에게 둘이 붙는다.
    // 이미 누가 붙어 말하고 있으면 안 뺏는다.
    public bool RequestPersuadeBy(UnitBrain speaker)
    {
        if (speaker == null || speaker == Prisoner) return false;
        if (!Conversion.CanPersuade(Prisoner)) return false;
        if (!Conversion.PersuadeReady(Prisoner)) return false;   // 쉬는 중엔 재촉해도 안 된다
        if (JobManager.Instance == null) return false;

        if (_persuadeJob != null && _persuadeJob.Status == JobStatus.InProgress) return false;

        if (_persuadeJob == null || _persuadeJob.Status is JobStatus.Done or JobStatus.Cancelled)
        {
            _persuadeJob = new PersuadeJob(Prisoner, speaker);
            JobManager.Instance.ReserveFor(_persuadeJob, speaker);
            return true;
        }

        // 아직 아무도 안 집었거나 남이 예약만 해 둔 것 — 이 폰 앞으로 다시 잡는다.
        _persuadeJob.ReleaseReservation();
        _persuadeJob.Reserve(speaker);
        return true;
    }

    void DropPersuadeJob()
    {
        if (_persuadeJob == null) return;

        // 하고 있는 것은 그대로 끝내게 둔다 — `Execute`가 첫 줄에서 다시 보고 접는다.
        if (_persuadeJob.Status is JobStatus.Available or JobStatus.Reserved)
        {
            JobManager.Instance?.RemoveJob(_persuadeJob);
            _persuadeJob.Cancel();
        }
        _persuadeJob = null;
    }

    // 빈 우리 중 가장 가까운 것.
    public static PrisonCage FindNearestFree(Vector3 from)
    {
        PrisonCage best = null;
        float bestDist = float.MaxValue;

        foreach (var cage in All)
        {
            if (cage == null || cage.IsOccupied) continue;
            float d = (cage.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = cage; }
        }
        return best;
    }

    public static PrisonCage HolderOf(UnitBrain brain)
    {
        if (brain == null) return null;
        foreach (var cage in All)
            if (cage != null && cage.Prisoner == brain) return cage;
        return null;
    }

    public bool TryPut(UnitBrain brain)
    {
        if (brain == null || IsOccupied) return false;

        Prisoner = brain;
        Imprisonment.Bind(brain, HoldPosition);
        Debug.Log($"[Cage] {brain.UnitName}을(를) 가뒀다");
        return true;
    }

    public void Release()
    {
        if (Prisoner == null) return;
        DropPersuadeJob();
        Imprisonment.Unbind(Prisoner);
        Debug.Log($"[Cage] {Prisoner.UnitName}을(를) 풀어 줬다");
        Prisoner = null;
    }

    // **자리가 비었는지는 `Update`가 안 본다.** 파괴된 것은 유니티의 `== null`이 이미 잡으므로
    // `IsOccupied`가 저절로 거짓이 된다(죽는 쪽에서도 `UnitBrain.Die`가 `HolderOf(this)?.Release()`로
    // 정리한다). 위 `Update`가 도는 것은 **설득 일감을 걸기 위해서**지 자리 청소가 아니다 —
    // 1초에 한 번, 갇힌 사람이 없으면 첫 줄에서 빠진다.

    public void OnWorldClick() => OnSelected?.Invoke(this);
}

// **갇힌 상태는 한 곳에서 정한다.** 우리와 죄수 침대가 각자 BT를 멈추고 각자 자리에 붙들면,
// 한쪽만 고쳐 놓고 다른 쪽에서 포로가 걸어 나가는 일이 반드시 생긴다.
public static class Imprisonment
{
    // `pose`를 안 주면 **서 있는 자세**(우리). 죄수 침대는 `Bed.SleepRotation`을 넘긴다.
    public static void Bind(UnitBrain brain, Vector3 at, Quaternion? pose = null)
    {
        if (brain?.Blackboard == null) return;

        brain.Blackboard.IsPrisoner  = true;
        brain.Blackboard.PrisonSpot  = at;
        brain.Blackboard.PrisonPose  = pose ?? Quaternion.Euler(0f, brain.transform.eulerAngles.y, 0f);

        // 하던 것을 전부 끊는다 — 깨어나서 이어서 하면 안 된다.
        brain.Blackboard.Target      = null;
        brain.Blackboard.FollowTarget = null;
        brain.GetComponent<CombatHandler>()?.ExitCombat();
        brain.GetComponent<PawnMover>()?.StopMovement();
    }

    public static void Unbind(UnitBrain brain)
    {
        if (brain?.Blackboard == null) return;
        brain.Blackboard.IsPrisoner = false;
    }

    // **놓아 준다 — 자리에서 꺼내고, 갈 길을 정해 준다.**
    //
    // `Unbind`만 하면 갇힘 표시만 사라지고 자리는 그대로 물고 있어(우리·침대가 제 `Prisoner`를
    // 안 놓는다) 그 자리를 아무도 못 쓴다. **어디에 갇혀 있었든 한 곳에서 푼다** —
    // 부르는 쪽이 우리인지 침대인지 따져야 하면 반드시 한쪽을 빠뜨린다.
    //
    // **적은 놓아 주면 도망친다**(`CombatStance.Flee`). 그냥 풀면 갇힘이 풀리는 순간 BT가
    // 돌아 그 자리에서 덤비는데, 그건 놓아 준 것이 아니라 우리를 연 것이다 — 살려 보내는
    // 선택이 성립하려면 실제로 가야 한다. 우리 폰은 하던 대로 돌아간다.
    public static void Release(UnitBrain brain)
    {
        if (brain == null) return;

        PrisonCage.HolderOf(brain)?.Release();   // 우리에 있었으면 우리가 푼다
        Bed.ReleaseFrom(brain);         // 죄수 침대였으면 침대가 푼다(갇힘도 같이 풀린다)
        Unbind(brain);                  // 어느 쪽도 아니었을 때의 안전망

        // **남은 맵을 떠난다.** 성향만 도주로 바꾸면 우리 폰을 피해 달아나다 구석에서
        // 서성이고, 결국 그 자리에 적이 하나 남는다 — 그건 놓아 준 것이 아니다.
        // 가는 동안 안 덤비게 도주 성향도 같이 씌운다(맞으면 `LeaveMapNode`가 둘 다 거둔다).
        var faction = brain.GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer)
        {
            brain.SetStanceOverride(CombatStance.Flee);
            if (EventManager.TryFindExit(brain.transform.position, out var exit))
            {
                brain.Blackboard.LeavingMap  = true;
                brain.Blackboard.LeaveTarget = exit;
            }
            else
            {
                // 나갈 길을 못 찾았으면 떠나지는 못해도 덤비지는 않는다.
                Debug.LogWarning($"[Prison] {brain.UnitName}: 맵 밖으로 나갈 길을 못 찾았습니다 — 도주만 켭니다.");
            }
        }

        Debug.Log($"[Prison] {brain.UnitName}을(를) 놓아 줬다");
    }

    // 갇힌 사람은 제자리를 지킨다. 깨어나도 마찬가지다 — 벗어나는 길은 풀려나는 것뿐이다.
    // (탈출은 아직 없다. 월드맵 포로가 하루마다 굴리는 것과 짝을 맞출 자리다.)
    public static void Hold(UnitBrain brain)
    {
        if (brain?.Blackboard == null || !brain.Blackboard.IsPrisoner) return;

        var bb  = brain.Blackboard;

        // **침대에 누운 포로는 `SetSleeping`이 붙들고 있다** — 자리도 자세도 그쪽이 정하므로
        // 여기서 또 만지면 같은 일을 두 곳이 한다(우리 폰을 `RestNode`가 눕히는 것과 같은 길).
        var mover = brain.GetComponent<PawnMover>();
        if (mover != null && mover.IsSleeping) return;

        var pos = brain.transform.position;

        // 우리에 선 포로는 여기가 붙든다. 기절에서 깨어날 때 `BodyPartSystem`이 기절 직전
        // 자세로 되돌리므로(`_preKoRotation`) 자세도 같이 본다.
        if (Quaternion.Angle(brain.transform.rotation, bb.PrisonPose) > 1f)
            brain.transform.rotation = bb.PrisonPose;

        var spot = bb.PrisonSpot;
        if ((new Vector2(pos.x, pos.z) - new Vector2(spot.x, spot.z)).sqrMagnitude < 0.25f) return;

        // 파묻힘·되밀림 보정은 `PawnPlacement`가 한 곳에서 다룬다.
        PawnPlacement.PlaceOnGround(brain.gameObject, spot, bb.PrisonPose);
    }
}

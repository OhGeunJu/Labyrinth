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
public class PrisonCage : MonoBehaviour, IWorldClickable, IPrisonSite, ICarrySite
{
    public static readonly List<PrisonCage> All = new();
    public static event System.Action<PrisonCage> OnSelected;

    [Tooltip("갇힌 사람이 서 있을 자리. 비우면 우리 한가운데.")]
    [SerializeField] Transform holdAnchor;

    public UnitBrain Prisoner { get; private set; }
    public bool      IsOccupied => Prisoner != null;

    // ── `IPrisonSite` — 먹이기 일감의 자리 ───────────────────────────────
    // 우리에 든 사람은 곧 갇힌 사람이다(`TryPut`이 `Imprisonment.Bind`를 같이 한다).
    public UnitBrain   HeldPrisoner => Prisoner;
    public SiteJobSlot FeedSlot { get; private set; }

    // **슬롯은 여기서 묶는다** — 필드 초기화식에서는 `this`를 못 잡고(CS0236), 무엇보다
    // 한 번만 묶어야 매 틱 클로저가 새로 안 난다(`SiteJobSlot` 주석 참고).
    void Awake()
    {
        _persuadeSlot = new SiteJobSlot(w => new PersuadeJob(Prisoner, w),
                                        () => Conversion.PersuadeReady(Prisoner));
        // 짐승은 설득이 아니라 조련이다 — 같은 쉬는 시간을 쓴다(`Conversion.TameRound`).
        _tameSlot     = new SiteJobSlot(w => new TameJob(Prisoner, w),
                                        () => Conversion.PersuadeReady(Prisoner));
        FeedSlot      = PrisonFeeding.NewSlot(this);
    }

    public Vector3 HoldPosition => holdAnchor != null ? holdAnchor.position : transform.position;

    void OnEnable()  => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        DropPersuadeJob();
        PrisonFeeding.Drop(this);
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
    // 걸고·넘기고·거두는 규칙 자체는 `SiteJobSlot`이 든다(침대의 치료, 갇힌 사람의 끼니와
    // 같은 그릇). 여기서 답하는 것은 **언제 필요하고 언제 걸어도 되는가**뿐이다.
    const float CheckInterval = 1f;

    SiteJobSlot _persuadeSlot;   // `Awake`에서 묶는다
    SiteJobSlot _tameSlot;
    float _nextCheck;

    void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + CheckInterval;

        // **갇힌 사람 몫은 설득과 따로 돈다**(먹이기·탈출) — 설득이 안 되는 상대(하수인
        // 종족)도, 다 넘어온 뒤에도 먹고 도망친다. 아래와 엮이면 "말이 안 통하면 굶긴다"가 된다.
        Imprisonment.TickHeld(this);

        // 넘어왔거나 풀려났거나, 애초에 말이 안 통하는 상대(하수인 종족 — 핵이 지배한다).
        //
        // **쿨타임 동안은 일감을 안 낸다.** 내 놓고 `IsReady`로 막으면 폰이 우리 앞에 서서
        // 20초를 기다린다 — 쿨타임을 둔 이유가 그 사이에 다른 일을 하라는 것이다.
        _persuadeSlot?.Tick(Conversion.CanPersuade(Prisoner));
        _tameSlot?.Tick(Conversion.CanTame(Prisoner));
    }

    // **이 폰에게 맡긴다**(우클릭 [길들인다]).
    public bool RequestTameBy(UnitBrain tamer)
    {
        if (tamer == null || tamer == Prisoner) return false;
        if (!Conversion.CanTame(Prisoner)) return false;
        if (!Conversion.PersuadeReady(Prisoner)) return false;

        return _tameSlot != null && _tameSlot.RequestBy(tamer);
    }

    // **이 폰에게 맡긴다**(우클릭 [설득한다]).
    public bool RequestPersuadeBy(UnitBrain speaker)
    {
        if (speaker == null || speaker == Prisoner) return false;
        if (!Conversion.CanPersuade(Prisoner)) return false;
        if (!Conversion.PersuadeReady(Prisoner)) return false;   // 쉬는 중엔 재촉해도 안 된다

        return _persuadeSlot != null && _persuadeSlot.RequestBy(speaker);
    }

    void DropPersuadeJob() { _persuadeSlot?.Drop(); _tameSlot?.Drop(); }

    // 빈 우리 중 가장 가까운 것.
    public static PrisonCage FindNearestFree(Vector3 from) => CarrySites.NearestFree(All, from);

    public static PrisonCage HolderOf(UnitBrain brain)
    {
        if (brain == null) return null;
        foreach (var cage in All)
            if (cage != null && cage.Prisoner == brain) return cage;
        return null;
    }

    // ── `ICarrySite` — 업은 것을 받는 자리 ──────────────────────────────────
    //
    // **죽은 사람은 안 가둔다** — 가둘 이유가 없고, 넣어 두면 그 우리를 산 사람이 못 쓴다
    // (`Bed.Accepts`와 같은 규칙). 시체가 갈 자리는 무덤이다.
    public bool Accepts(UnitBrain brain) => brain != null && !PawnCarry.IsCorpse(brain);

    Vector3     ICarrySite.PutPosition => HoldPosition;
    Quaternion? ICarrySite.PutRotation => null;   // 서 있는 자세 그대로
    bool        ICarrySite.IsFull      => IsOccupied;
    void        ICarrySite.Put(UnitBrain brain) => TryPut(brain);
    string      ICarrySite.SiteName    => "우리";

    public bool TryPut(UnitBrain brain)
    {
        if (brain == null || IsOccupied) return false;
        if (!Accepts(brain)) return false;

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

        // **탈출 게이지는 여기서 안 건드린다.** 자리를 옮겨도(업었다 다른 우리에 넣어도)
        // 쌓인 것은 그대로다 — 되돌리면 옮기기를 되풀이해 영영 안 도망가게 만들 수 있다.
        // 지우는 자리는 `Unbind` 하나뿐이다(풀려나거나 넘어왔을 때).

        // 하던 것을 전부 끊는다 — 깨어나서 이어서 하면 안 된다.
        brain.Blackboard.Target      = null;
        brain.Blackboard.FollowTarget = null;
        if (brain.TryGetComponent(out CombatHandler combat)) combat.ExitCombat();
        if (brain.TryGetComponent(out PawnMover brainMover)) brainMover.StopMovement();
    }

    // **게이지는 여기서 안 지운다.** `Unbind`는 "자리에서 뗀다"이지 "풀려났다"가 아니다 —
    // `PawnCarry.TryPickUp`이 업기 전에 이걸 부르므로, 지우면 **[끌어낸다] → [여기 가둔다]만
    // 되풀이해도 95%짜리 탈출 게이지가 0으로 돌아간다**(`Bind`의 주석이 그러면 안 된다고
    // 적어 둔 바로 그 악용이다). 회유 게이지(`ConvertProgress`)도 여기서 안 지운다 —
    // 둘이 같은 조작에서 다르게 굴면 어느 쪽이 규칙인지 알 수 없다.
    //
    // 지우는 자리는 **`Release` 하나**(풀려났거나 도망쳤을 때).
    public static void Unbind(UnitBrain brain)
    {
        if (brain?.Blackboard == null) return;
        brain.Blackboard.IsPrisoner = false;
    }

    // **갇힌 사람에게 매 초 일어나는 일.** 자리(`PrisonCage`·`Bed`)가 이 한 줄만 부른다 —
    // 축이 늘 때(먹이기 → 탈출) 자리를 둘 다 고치게 하면 언젠가 한쪽을 빠뜨린다.
    public static void TickHeld(IPrisonSite site)
    {
        if (site == null) return;

        PrisonFeeding.Tick(site);
        PrisonBreak.Tick(site.HeldPrisoner);
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
    // `reason`은 **로그 문구일 뿐이다** — 하는 일은 같다(자리를 비우고, 갇힘을 풀고,
    // 도주 성향을 씌워 내보낸다). 손으로 놓아 준 것과 제 발로 나간 것이 로그에서 구별되지
    // 않으면, "왜 없어졌지"를 되짚을 때 그 둘을 가릴 수가 없다.
    // 손으로 놓아 준 것의 기본 문구. **우호도가 이 값을 본다** — 몸값·회유처럼 제 몫을 이미
    // 얹은 경로는 다른 `reason`을 넘기므로, 여기서 또 더해 두 번 값을 받는 일이 없다.
    public const string DefaultReleaseReason = "놓아 줬다";

    public static void Release(UnitBrain brain, string reason = DefaultReleaseReason)
    {
        if (brain == null) return;

        PrisonCage.HolderOf(brain)?.Release();   // 우리에 있었으면 우리가 푼다
        Bed.ReleaseFrom(brain);         // 죄수 침대였으면 침대가 푼다(갇힘도 같이 풀린다)
        Unbind(brain);                  // 어느 쪽도 아니었을 때의 안전망

        // **여기가 게이지를 지우는 유일한 자리다.** 자리를 옮기는 것(업기·다른 우리)과
        // 풀려나는 것은 다른 일이다 — 옮길 때마다 0이 되면 옮기기를 되풀이해 영영 안
        // 도망가게 만들 수 있다. 다시 잡히면 처음부터 차는 것이 맞다.
        PrisonBreak.Clear(brain);

        // **남은 맵을 떠난다.** 성향만 도주로 바꾸면 우리 폰을 피해 달아나다 구석에서
        // 서성이고, 결국 그 자리에 적이 하나 남는다 — 그건 놓아 준 것이 아니다.
        // 가는 동안 안 덤비게 도주 성향도 같이 씌운다(맞으면 `LeaveMapNode`가 둘 다 거둔다).
        var faction = brain.GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer)
        {
            // **값을 안 받고 놓아 준 것은 더 산다**(`RansomBonus`보다 크다) — 몸값은 거래지만
            // 이건 베푼 것이다. 그래야 "돈이냐 관계냐"가 고르는 일이 된다.
            //
            // **몸값으로 넘긴 경우는 여기 안 걸린다** — 그쪽은 `WorldRansom.Sell`이 제 몫을
            // 이미 얹었고, 여기서 또 더하면 한 번 놓아 준 것이 두 번 값을 받는다.
            if (faction != null && faction.Data != null && reason == DefaultReleaseReason)
                FactionRelations.Add(faction.Data, FactionRelations.ReleaseBonus,
                                     $"{brain.UnitName}을(를) 그냥 놓아 줬다");

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

        Debug.Log($"[Prison] {brain.UnitName}: {reason}");
    }

    // 갇힌 사람은 제자리를 지킨다. 깨어나도 마찬가지다 — 벗어나는 길은 풀려나는 것뿐이다.
    // (도망치는 길은 `PrisonBreak`가 든다 — 게이지가 차면 여기서 놓여난다. 그 눈금은
    //  월드맵 포로가 하루마다 굴리는 것과 짝이다.)
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
        //
        // **기절해 있는 동안은 자세를 안 건드린다** — 쓰러진 것을 억지로 세우면 우리 안에서
        // 곰이 벌떡 선다. 깨어나면 그때 `PrisonPose`로 돌아간다.
        if (!bb.IsKnockedOut && Quaternion.Angle(brain.transform.rotation, bb.PrisonPose) > 1f)
            brain.transform.rotation = bb.PrisonPose;

        // **누운 폰은 몸통으로 잰다 — 피벗은 발밑이다.**
        //
        // 서 있을 땐 몸 한가운데가 발 바로 위라 수평으로는 차이가 없지만, 누우면 그 한가운데가
        // **옆으로 뻗는다.** 그대로 놓으면 발만 우리 한가운데에 오고 몸은 밖으로 삐져나온다 —
        // 곰처럼 큰 짐승일수록 눈에 띄었다(`PawnMover.SetSleeping`이 침대에서 하는 보정과
        // 같은 것인데 우리에만 빠져 있었다). 거리 판정도 같은 기준이라야 한 번 어긋난 것이
        // 그대로 굳지 않는다.
        Vector3 lean = brain.transform.rotation * Vector3.up
                     * PawnPlacement.CenterOffset(brain.gameObject);
        var body = new Vector2(pos.x + lean.x, pos.z + lean.z);

        var spot = bb.PrisonSpot;
        if ((body - new Vector2(spot.x, spot.z)).sqrMagnitude < 0.25f) return;

        // 파묻힘·되밀림 보정은 `PawnPlacement`가 한 곳에서 다룬다.
        // 자세는 지금 것을 그대로 둔다(위에서 정했다) — 여기서 또 넘기면 기절한 폰을 세운다.
        PawnPlacement.PlaceOnGround(brain.gameObject,
                                    spot - new Vector3(lean.x, 0f, lean.z));
    }
}

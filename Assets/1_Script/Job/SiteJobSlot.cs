using System;

// **자리가 일감 하나를 든다** — 침대의 치료, 우리의 설득, 갇힌 사람의 끼니가 같은 모양이다.
//
// **왜 자리가 드는가.** 대상(환자·포로)이 들면 누가 등록을 지우는지가 애매해진다 —
// 기절하거나 죽거나 넘어오면 스스로 못 지운다. 그리고 **누르는 사람마다 하나씩 만들면
// 여럿이 동시에 붙는다**: 세 명이 [설득한다]를 누르니 회차가 셋 나란히 돌아 게이지가 세 배로
// 찼다(2026-08-12에 실제로 겪었다). 자리가 하나면 일감도 하나다.
//
// 그 규칙이 세 곳에 각자 적혀 있었다 — `Tick`/`RequestBy`/`Drop`이 세 벌이었고, 하나만
// 고치면 나머지가 조용히 어긋난다. 셋에서 다른 것은 셋뿐이라 인자로 받는다:
// **필요한가**(`wanted`) · **지금 걸어도 되는가**(`canIssue`) · **무엇을 만드나**(`make`).
//
// **주기는 안 든다.** 부르는 쪽이 `Update`에서 이미 재고 있고(침대는 치료와 끼니를 같은
// 간격으로 본다), 여기서 또 재면 자리 하나에 시계가 둘이 된다.
public class SiteJobSlot
{
    // ── 만드는 법은 **한 번만** 묶는다 ────────────────────────────────────────
    //
    // 처음엔 `Tick(wanted, make, canIssue)`로 매번 넘겼다. 그러면 자리마다 **초당 클로저가
    // 둘씩**(람다 하나 + 메서드 그룹 하나) 생긴다 — 이 클로저들은 `this`를 잡으므로 컴파일러가
    // 캐시하지 못한다(캡처 없는 람다만 정적 필드에 캐시된다). 우리 열 개면 초당 스무 개고,
    // 아무 일도 안 하는 동안에도 계속 난다.
    //
    // 자리마다 답은 **처음부터 정해져 있다**("이 침대는 치료 일감을 낸다"). 생성자에서 한 번
    // 묶으면 그 뒤로는 할당이 없고, 부르는 쪽도 `Tick(wanted)` 하나로 짧아진다.
    //
    // **만드는 법은 하나다** — `make(null)`이면 아무나 집어가는 일감, `make(폰)`이면 그 폰
    // 앞으로 잡는 일감. 두 벌로 두면 자동으로 난 것과 손으로 시킨 것이 다른 일감이 되기 쉽다.
    readonly Func<UnitBrain, JobBase> _make;
    readonly Func<bool>               _canIssue;

    public SiteJobSlot(Func<UnitBrain, JobBase> make, Func<bool> canIssue = null)
    {
        _make     = make;
        _canIssue = canIssue;
    }

    public JobBase Current { get; private set; }

    // 이미 누가 붙어 하고 있나 — 우클릭 명령이 "안 뺏는다"를 판단하는 데 쓴다.
    public bool IsRunning => Current != null && Current.Status == JobStatus.InProgress;

    // **한 회차씩** 건다. 끝나면 아직 필요할 때 다음 것이 걸린다 — 그래야 회차 사이에 더
    // 급한 일이 끼어들고, 도중에 조건이 깨지면 거기서 멈춘다.
    public void Tick(bool wanted)
    {
        // 끝났거나 취소된 것을 붙들고 있어 봐야 다음 회차를 막을 뿐이다.
        if (Current != null && Current.Status is JobStatus.Done or JobStatus.Cancelled)
            Current = null;

        if (!wanted) { Drop(); return; }
        if (Current != null) return;

        // **못 걸 상황이면 아예 안 건다.** 걸어 두고 `IsReady`로 막으면 폰이 그 앞까지 갔다가
        // 서서 기다린다 — 쿨타임(설득)이나 물자(먹이기)를 둔 이유가 바로 그 사이에 다른 일을
        // 하라는 것이다. **지연 평가다** — 이미 일감이 있으면 묻지도 않는다(먹이기의
        // `ExistsAnywhere`는 창고를 전부 훑는다).
        if (_canIssue != null && !_canIssue()) return;
        if (_make == null || JobManager.Instance == null) return;

        Current = _make(null);   // 임자 없음 — 조건에 맞는 아무 폰이나 집어 간다
        if (Current != null) JobManager.Instance.RegisterJob(Current);
    }

    // **이 폰에게 맡긴다**(우클릭 명령). 새로 만들지 않고 **있던 것을 넘긴다** — 자리가
    // 주인이라 여기서 하나 더 만들면 같은 대상에게 둘이 붙는다.
    //
    // **이미 누가 하고 있으면 안 뺏는다** — 들고 온 약·밥만 버려진다.
    // 손으로 시킨 일이 작업 설정(on/off)을 건너뛰는 것은 `JobManager.ReserveFor` 쪽 규약이다.
    public bool RequestBy(UnitBrain worker)
    {
        if (worker == null || _make == null || JobManager.Instance == null) return false;
        if (IsRunning) return false;

        if (Current == null || Current.Status is JobStatus.Done or JobStatus.Cancelled)
        {
            Current = _make(worker);
            if (Current == null) return false;

            JobManager.Instance.ReserveFor(Current, worker);
            return true;
        }

        // 아직 아무도 안 집었거나 남이 예약만 해 둔 것 — 이 폰 앞으로 다시 잡는다.
        Current.ReleaseReservation();
        Current.Reserve(worker);
        return true;
    }

    // 자리가 사라지거나 대상이 없어졌을 때. **하고 있는 것은 그대로 끝내게 둔다** —
    // 각 `Execute`가 첫 줄에서 조건을 다시 보고 접는다(환자가 잠깐 일어났다고 손을 놓게
    // 하면 약만 버려진다).
    public void Drop()
    {
        if (Current == null) return;

        if (Current.Status is JobStatus.Available or JobStatus.Reserved)
        {
            JobManager.Instance?.RemoveJob(Current);
            Current.Cancel();
        }
        Current = null;
    }
}

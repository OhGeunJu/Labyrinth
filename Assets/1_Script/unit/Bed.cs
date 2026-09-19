using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Bed : MonoBehaviour, IWorldClickable, IPrisonSite, ICarrySite
{
    static readonly List<Bed> _all = new();

    public string BedName = "침대";

    // ── 죄수용인가 ──────────────────────────────────────────────────────────
    //
    // **같은 침대를 용도만 바꾼다**(림월드식). 죄수 침대를 따로 지으면 포로가 생기기 전에는
    // 쓸모없는 건물을 미리 지어 둬야 하고, 포로가 없어지면 다시 쓸모가 없어진다 —
    // 잡아 왔을 때 방 하나를 죄수용으로 돌리는 편이 자연스럽다.
    //
    // 우리(`PrisonCage`)와의 차이: 우리는 가두는 것이 전부라 싸고, 죄수 침대는 회복·치료가
    // 붙는다(살려서 회유할 사람은 침대에). **갇힌 상태 자체는 `Imprisonment` 하나가 정한다.**
    [SerializeField] bool forPrisoners;

    // **잠자리의 질**(2026-08-25). 피로가 얼마나 빨리 차는지를 이 침대가 답한다 —
    // `Needs.SleepPerSecond`가 곧바로 배율로 쓴다(바닥은 `Needs.FloorQuality` 0.5).
    //
    // **침대마다 다를 수 있어야 가구를 지을 이유가 생긴다.** 전에는 `bool inBed` 하나라
    // 짚더미든 깃털 요든 똑같이 600초였고, 그러면 더 나은 침대라는 것이 표현될 자리가 없다.
    //
    // 1.0이 보통이다 — **아래로 내리는 데는 안 쓴다**(나쁜 침대는 바닥과 구별이 안 가고,
    // 그건 짓지 말라는 뜻이라 선택이 아니다).
    [Tooltip("피로 회복 배율. 보통 침대 1.0 · 좋은 침대 1.4 (바닥은 0.5로 고정)")]
    [SerializeField] float restQuality = 1f;

    public float RestQuality => Mathf.Max(0.1f, restQuality);

    public bool ForPrisoners => forPrisoners;

    public void SetForPrisoners(bool on)
    {
        if (forPrisoners == on) return;

        // 용도를 바꾸면 지금 쓰던 사람은 비켜야 한다 — 죄수 침대에 우리 폰이 배정돼 있거나
        // 그 반대면, 규칙이 뒤바뀐 자리에 누운 채로 남는다.
        if (_occupant != null && _occupant.Blackboard != null && _occupant.Blackboard.IsPrisoner)
            Imprisonment.Unbind(_occupant);
        AssignedUnit = null;
        _occupant    = null;

        forPrisoners = on;
        RefreshTint();
    }

    // ── 용도 표시 — **매트리스를 물들인다** ─────────────────────────────────
    //
    // **눈으로 갈려야 한다.** 죄수용인지 아닌지가 우클릭 메뉴를 열어 봐야만 보이면,
    // 방을 여러 개 두는 순간 어느 침대가 어느 쪽인지 알 수가 없다.
    //
    // 한때 바닥에 작은 표식 quad를 얹었는데 **침대 모델이 그 위를 덮어서 한 번도 안 보였고**,
    // 매트리스 위로 올렸더니 이번엔 침대에 스티커를 붙인 꼴이었다. 매트리스 **자체**가
    // 그 색이면 멀리서도 한눈에 갈리고 따로 붙인 물건이 없다.
    //
    // ── 색은 **저작한다** — 런타임에 만들지 않는다 ─────────────────────────
    //
    // 처음엔 코드가 색을 들고 `rend.material`로 칠했다. 그러면 셋이 딸려 온다:
    //  ① **에디터에서는 둘이 똑같아 보인다** — 색을 게임 돌릴 때만 입히므로, 프리팹을 열어도
    //     씬에 놓아도 죄수 침대인지 알 수가 없다.
    //  ② `.material`은 **침대마다 머티리얼 인스턴스를 하나씩** 만든다(배칭이 깨지고,
    //     `OnDestroy`에서 손수 지워 줘야 한다).
    //  ③ `OnEnable`은 **편집 중에도 불린다**(스크립트 재컴파일·컴포넌트 추가). 그때 `.material`을
    //     만지면 경고와 함께 씬에 이름 없는 머티리얼이 쌓인다 — 그래서 `Application.isPlaying`
    //     가드를 달았는데, 그 가드가 곧 ①이었다.
    //
    // 셋 다 **머티리얼을 에셋으로 저작하면** 사라진다. 프리팹이 제 색을 이미 물고 있으므로
    // 에디터에서 그대로 보이고, 런타임에 하는 일은 **둘 중 하나를 골라 끼우는 것**뿐이다
    // (`sharedMaterial` — 인스턴스를 안 만든다). 색을 고치는 자리도 코드가 아니라 에셋이다.
    //
    // **`MaterialPropertyBlock`은 안 쓴다.** 철거 예정 표시가 MPB로 덮었다가
    // `SetPropertyBlock(null)`로 되돌리는데, 그 되돌림은 **머티리얼 본래 색**으로 간다 —
    // MPB로 칠해 두면 철거 모드에 한 번 스치는 것만으로 색이 사라진다.
    //
    // **쨍하지 않게.** 자기 자신을 봐 달라는 UI가 아니라 침대 천의 색이다 —
    // 형광색을 칠하면 방 안에서 침대만 튀어 보인다(`Bld_5C8057` / `Bld_9E6133`).
    [Tooltip("용도 색을 입힐 부분. 비우면 자식 중 이름에 'Mattress'가 든 것을 찾는다.")]
    [SerializeField] Renderer tintTarget;

    [Tooltip("보통 침대 매트리스 — 바랜 초록(Bld_5C8057).")]
    [SerializeField] Material normalTint;

    [Tooltip("죄수 침대 매트리스 — 바랜 주황(Bld_9E6133).")]
    [SerializeField] Material prisonTint;

    void RefreshTint()
    {
        var mat = forPrisoners ? prisonTint : normalTint;
        if (mat == null) return;   // 안 물렸으면 프리팹이 들고 있는 색 그대로 둔다

        var rend = tintTarget != null ? tintTarget : FindMattress();
        if (rend == null || rend.sharedMaterial == mat) return;

        rend.sharedMaterial = mat;
    }

    // 이름으로 찾는다 — 프리팹마다 필드를 물리게 하면 새 침대 모델에서 한 번은 빠뜨린다.
    // 못 찾으면 **가장 큰 렌더러**(대개 매트리스나 몸통)를 쓴다.
    Renderer FindMattress()
    {
        Renderer biggest = null;
        float biggestSize = -1f;

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null) continue;
            if (r.name.IndexOf("Mattress", StringComparison.OrdinalIgnoreCase) >= 0) return r;

            float size = r.bounds.size.sqrMagnitude;
            if (size > biggestSize) { biggestSize = size; biggest = r; }
        }
        return biggest;
    }

    [Header("눕는 자세")]
    [Tooltip("있으면 이 지점의 위치·회전으로 눕는다. 비우면 침대 윗면 중앙에 자동 배치.")]
    [SerializeField] Transform sleepAnchor;

    [Tooltip("sleepAnchor가 없을 때 침대 윗면에서 폰을 띄우는 높이.")]
    [SerializeField] float lieHeight = 0.35f;

    [Tooltip("**베개가 어느 쪽인가** — 머리를 둘 방향(침대 기준 각도).\n" +
             "0 = 침대 정면(+Z) 쪽에 머리. 모델의 베개가 반대 끝이면 180, 옆이면 ±90.\n" +
             "폰이 다가온 방향과 무관하게 늘 같은 쪽으로 눕는다.")]
    [SerializeField] float pillowYaw;

    [Tooltip("침대 옆 접근 지점을 침대 가장자리에서 얼마나 떨어뜨릴지.")]
    [SerializeField] float approachMargin = 0.8f;

    public void Init(BuildingData data) { if (data != null) BedName = data.BuildingName; }
    public UnitBrain  AssignedUnit { get; private set; }

    // 누울 위치 — 침대 윗면 중앙 위. 렌더러가 없으면 오브젝트 원점.
    public Vector3 SleepPosition
    {
        get
        {
            if (sleepAnchor != null) return sleepAnchor.position;

            var bounds = WorldBounds();
            return new Vector3(bounds.center.x, bounds.max.y + lieHeight, bounds.center.z);
        }
    }

    // 침대 옆에서 설 수 있는 지점 — 이동 목표로 쓴다. 네 면 중 **설 수 있고 가장 가까운** 쪽.
    //
    // 예전 이유는 "침대는 길찾기 그래프에 없는 장애물이라 경로가 관통해서 나온다"였다.
    // 그 모순은 없앴다 — 침대 콜라이더를 **트리거**로 돌려 지도(빈 칸)와 물리(안 막음)가
    // 같은 말을 하게 했다(2026-08-12). 그래도 이 함수는 남는다: 옆에 서는 것이 맞기 때문이다.
    // 중심을 목표로 주면 폰이 침대 한가운데 서고, 거기서 눕히면 제 발밑에서 자는 그림이 된다.
    public Vector3 ApproachPosition(Vector3 from)
    {
        var bounds = WorldBounds();
        float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + approachMargin;

        Vector3 center = bounds.center;
        Vector3[] sides =
        {
            center + transform.right   * reach,
            center - transform.right   * reach,
            center + transform.forward * reach,
            center - transform.forward * reach,
        };

        // 1차: 설 수 있는 면 중 가장 가까운 쪽
        Vector3 best        = center;
        float   bestDistSqr = float.MaxValue;

        foreach (var side in sides)
        {
            if (!IsOpen(side)) continue;
            float distSqr = (side - from).sqrMagnitude;
            if (distSqr >= bestDistSqr) continue;
            bestDistSqr = distSqr;
            best        = side;
        }
        if (bestDistSqr < float.MaxValue) return best;

        // 2차: 네 면이 다 막혔으면 그냥 가장 가까운 면으로 (그 앞에서 멈추더라도 붙는다)
        foreach (var side in sides)
        {
            float distSqr = (side - from).sqrMagnitude;
            if (distSqr >= bestDistSqr) continue;
            bestDistSqr = distSqr;
            best        = side;
        }

        return best;
    }

    // 그 지점에 설 수 있는가 — 바닥이 있고(던전) 건물에 점유되지 않았는가
    static bool IsOpen(Vector3 world)
    {
        var map = MapManager.GetForPosition(world);
        if (map == null) return true;

        var tile = map.WorldToTile(world);
        return map.IsStandable(tile) && !map.IsOccupied(tile);
    }

    // **여기에 침대가 아닌 것을 섞지 말 것.** `SleepPosition`(윗면 + `lieHeight`)도
    // `ApproachPosition`도 전부 이 바운즈에서 나오므로, 장식 하나를 자식으로 붙이면
    // 그만큼 폰이 침대 위에 떠서 자거나 엉뚱한 자리에 선다. (용도 색을 표식 quad로 얹었을
    // 때 실제로 그랬다 — 지금은 매트리스를 직접 물들이므로 자식이 늘지 않는다.)
    //
    // **켜진 렌더러만 센다**(2026-09-20). 모델을 입히면 옛 도형은 렌더러만 **꺼 둔 채** 남는데(`PropDresser`),
    // `GetComponentsInChildren`은 꺼진 렌더러도 돌려줘서 **안 보이는 옛 도형**이 경계를 넓혔다.
    Bounds WorldBounds()
    {
        bool any = false;
        var bounds = new Bounds(transform.position, Vector3.one);
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    // **머리는 베개 쪽이다 — 침대가 정한다.**
    //
    // 전에는 **폰이 다가온 면**을 보고 네 방향 중 하나로 눕혔다. 어느 쪽에서 와도 자연스럽게
    // 보이라는 뜻이었지만, 침대에는 베개가 있다 — 같은 침대인데 걸어온 방향에 따라 머리가
    // 발치에 가고, 두 폰이 번갈아 쓰면 매번 반대로 눕는다.
    //
    // 그래서 **침대의 방향만 본다.** 기본은 침대 정면(+Z) 쪽에 머리를 둔다. 모델의 베개가
    // 다른 끝에 있으면 `pillowYaw`를 180으로(옆이면 ±90) — 프리팹마다 한 번 적으면 끝난다.
    // `sleepAnchor`를 두면 그 회전이 이긴다(가장 세게 저작하는 길).
    public Quaternion SleepRotation
    {
        get
        {
            if (sleepAnchor != null) return sleepAnchor.rotation;
            return Quaternion.Euler(-90f, transform.eulerAngles.y + pillowYaw, 0f);
        }
    }

    UnitBrain _occupant;
    public bool IsOccupied => _occupant != null;

    // 지금 이 침대에 **누워 있는** 폰. 배정(`AssignedUnit`)과 다르다 — 배정은 명령이고
    // 이건 실제로 자리에 있느냐다. 치료는 자리에 있어야 할 수 있다.
    public UnitBrain Occupant => _occupant;

    // ── `IPrisonSite` — 먹이기 일감의 자리 ───────────────────────────────
    //
    // **누워 있는 것만으로는 포로가 아니다.** 죄수 침대이면서 그 사람이 실제로 갇혀
    // 있을 때만이다 — 용도를 되돌리면(`SetForPrisoners`) 갇힘이 먼저 풀리고, 그때
    // 여기가 계속 참이면 우리 폰에게 간수가 밥을 떠먹인다(제 손으로 먹을 수 있는데).
    public UnitBrain HeldPrisoner
        => forPrisoners && _occupant != null && _occupant.Blackboard != null
                        && _occupant.Blackboard.IsPrisoner
         ? _occupant : null;

    public SiteJobSlot FeedSlot { get; private set; }

    // **슬롯은 여기서 묶는다** — 필드 초기화식에서는 `this`를 못 잡고(CS0236), 무엇보다
    // 한 번만 묶어야 매 틱 클로저가 새로 안 난다(`SiteJobSlot` 주석 참고).
    void Awake()
    {
        _treatSlot = new SiteJobSlot(_ => new TreatJob(this), CaresForOccupant);
        FeedSlot   = PrisonFeeding.NewSlot(this);
    }

    void OnEnable()  { _all.Add(this); RefreshTint(); }
    void OnDisable() { _all.Remove(this); DropTreatJob(); PrisonFeeding.Drop(this); }

    // ── 치료 일감 ────────────────────────────────────────────────────────────
    //
    // **침대가 일감을 낸다.** 다친 폰 자신이 내면 "누가 등록을 지우는가"가 애매해진다
    // (기절해 있거나 죽으면 스스로 못 지운다). 침대는 자리를 아는 유일한 물건이고,
    // 작업대가 제작 주문을 내는 것과 같은 자리다.
    //
    // **한 회차씩** 건다 — `TreatJob`이 끝나면 아직 나을 것이 남았을 때 다음 것이 걸린다.
    // 그래야 회차마다 약을 하나씩 쓰고, 도중에 물자가 떨어져도 맨손으로 이어진다.
    // 걸고·넘기고·거두는 규칙 자체는 `SiteJobSlot`이 든다(우리의 설득, 갇힌 사람의 끼니와
    // 같은 그릇). 여기서 답하는 것은 **언제 필요하고 누구를 돌보는가**뿐이다.
    const float CheckInterval = 1f;

    SiteJobSlot _treatSlot;   // `Awake`에서 묶는다
    float _nextCheck;

    void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + CheckInterval;

        // **갇힌 사람 몫은 치료와 별개로 돈다**(먹이기·탈출). 아래는 다 나으면 거기서
        // 끝나는데(`wanted`), 멀쩡한 포로도 배는 고프고 오히려 그때 걸어 나간다 —
        // 관문 뒤에 두면 상처가 아무는 순간 굶기 시작하고 도망도 안 친다.
        Imprisonment.TickHeld(this);

        var body = _occupant != null ? _occupant.GetComponent<BodyPartSystem>() : null;
        bool wanted = body != null && body.NeedsTreatment;

        _treatSlot?.Tick(wanted);
    }

    // **우리 폰이거나, 죄수 침대에 누운 포로거나.**
    //
    // 예전엔 플레이어 세력만 돌봤다("붙잡아 온 적을 눕힐 길이 아직 없다"). 이제 있다 —
    // 그리고 **죄수 침대의 존재 이유가 바로 이것**이다. 우리(`PrisonCage`)는 가두는 것이
    // 전부고, 침대는 회복과 치료가 붙기 때문에 살려서 다룰 사람을 눕히는 자리다.
    // 여기서 안 돌보면 죄수 침대와 우리가 같은 물건이 된다.
    //
    // **지나가던 적은 여전히 안 돌본다** — 갇힌 사람만이다.
    //
    // 이건 `wanted`와 다른 축이라 따로 둔다: 안 돌볼 사람이면 **일감을 안 낼** 뿐이고,
    // 이미 걸린 것을 거두지는 않는다(치료 도중에 세력이 바뀌는 일은 회유뿐인데, 그때
    // 하던 치료를 끊을 이유가 없다).
    bool CaresForOccupant()
    {
        if (_occupant == null) return false;

        var faction = _occupant.GetComponent<Faction>();
        return (faction != null && faction.IsPlayer) || HeldPrisoner == _occupant;
    }

    // 지금 이 침대에 치료가 필요한가 — 우클릭 메뉴가 선택지를 낼지 정할 때 쓴다.
    public bool NeedsTreatment
    {
        get
        {
            var body = _occupant != null ? _occupant.GetComponent<BodyPartSystem>() : null;
            return body != null && body.NeedsTreatment;
        }
    }

    // **이 의사에게 맡긴다**(우클릭 [치료한다]).
    public bool RequestTreatBy(UnitBrain doctor)
    {
        if (doctor == null || doctor == _occupant || !NeedsTreatment) return false;

        // 침대에서 나는 일감은 **누가 누웠는지를 그때그때 침대에 묻는다** — `TreatJob(Bed)`는
        // 의사를 인자로 안 받는다(예약이 곧 배정이다).
        return _treatSlot != null && _treatSlot.RequestBy(doctor);
    }

    void DropTreatJob() => _treatSlot?.Drop();

    // SelectionManager의 단일 레이캐스트가 이 건물을 클릭 판정 시 호출.
    // **전용 패널을 더는 안 연다.**
    //
    // `BedUI`가 하던 일은 [선택 유닛 배정]/[해제] 둘뿐인데 그건 이미 우클릭에 있고
    // (눕는다·일어난다·치료한다·죄수용으로 바꾼다·업은 사람을 여기 눕힌다·풀어 준다),
    // **침대가 무엇이고 누가 누웠는지**는 `UnitHP_Panel`이 건물 정보로 띄운다.
    //
    // 남겨 두면 나쁜 쪽으로 갈린다 — 죄수 침대에 [선택 유닛 배정]을 누르면 우리 폰이
    // 들어앉고 갇힌 사람은 자리에서 밀려나 **갇힘만 남았다**(실제로 그렇게 새어 나갔다).
    // 문이 둘인데 한쪽만 규칙을 아는 상태였다.
    public void OnWorldClick() { }

    // **죄수 침대는 배정으로 쓰는 물건이 아니다.**
    //
    // 배정은 이 게임에서 "쉬어라"라는 **명령**인데(그걸 `RestNode`가 읽는다) 포로에게는
    // 명령이 안 통한다. 그런데 막아 두지 않았더니 죄수 침대에 우리 폰을 배정할 수 있었고,
    // 그러면 갇힌 사람이 자리에서 밀려나면서 **갇힘만 남았다** — 침대 밖에 선 채로 BT가
    // 멎어 영영 안 움직이는 폰이 생긴다.
    //
    // 포로를 눕히는 길은 `PlacePrisoner` 하나뿐이고, 그건 **업어서 데려왔을 때만** 지난다.
    // **누가 배정했나** — 손으로 붙인 것인가, 졸린 폰이 저 혼자 잡은 것인가.
    //
    // 이 한 칸이 *배정이 무슨 뜻인가*를 가른다:
    //   · **손으로 붙였으면 "가서 쉬어라"라는 명령**이다 — 안 졸려도 눕고, 뗄 때까지 눕는다.
    //     (다친 동료를 눕히는 길이 이것이다. 피로만 보게 두면 **멀쩡한데 다친 폰을 눕힐 방법이
    //     사라진다**.)
    //   · **저 혼자 잡았으면 그냥 제 잠자리**다 — 졸리면 가고 가득 채우면 일어난다.
    //
    // 안 가르면 둘 중 하나가 깨진다: 늘 눕게 두면 침대를 준 폰이 하루 종일 누워 있고,
    // 피로만 보게 두면 **배정해도 안 눕는다**(2026-08-20에 실제로 그랬다).
    public bool AutoClaimed { get; private set; }

    public void Assign(UnitBrain unit)
    {
        if (forPrisoners) return;

        AutoClaimed = false;   // 손으로 붙인 것 — 아래 `ClaimFreeFor`만 참으로 되돌린다

        // 기존 배정 유닛의 침대를 빼앗는 경우 해제
        if (unit != null)
            foreach (var bed in _all)
                if (bed != this && bed.AssignedUnit == unit)
                    bed.AssignedUnit = null;

        AssignedUnit = unit;
    }

    // 업어 온 사람을 이 침대에 눕힌다. **점유·배정·갇힘을 한 번에** 세운다 —
    // 셋 중 하나만 빠져도 조용히 어긋난다(패널이 "배정 없음"이거나, 갇힘이 안 걸리거나,
    // 그 침대를 아무도 못 쓰거나).
    // **이 침대가 이 사람을 받나.** 죄수 침대는 누구든, 보통 침대는 **붙잡은 사람은 안 받는다.**
    //
    // 받으면 갇힘이 안 걸린 채 눕기만 해서 다음 틱에 **누운 자세 그대로 걸어 나간다.**
    // 붙들자니 보통 침대가 곧 죄수 침대가 되고, 안 붙들자니 눕힌 것이 아무 뜻이 없다 —
    // 애초에 못 눕히는 것이 맞다. 우리 폰(다친 동료)은 그대로 받는다.
    // **죽은 사람은 어느 침대도 안 받는다.** 시체는 업어서 옮길 수 있지만(`PawnCarry`)
    // 눕히는 것은 회복·치료를 얹는 일이라 뜻이 없고, 눕혀 두면 그 침대를 산 사람이 못 쓴다
    // (`_occupant`가 시체를 물고 있으면 `== null`로 안 걸린다 — `Die`가 자리를 비우는 것도
    // 같은 이유다). 시체는 옮겨서 아무 데나 내려놓는 것이 전부다.
    public bool Accepts(UnitBrain brain)
        => !PawnCarry.IsCorpse(brain) && (forPrisoners || !PrisonEscort.IsCaptive(brain));

    public void PlacePrisoner(UnitBrain brain)
    {
        if (brain == null || !Accepts(brain)) return;

        TryOccupy(brain);
        AssignedUnit = brain;
        if (!forPrisoners) return;

        Imprisonment.Bind(brain, SleepPosition, SleepRotation);

        // **눕히는 것은 `SetSleeping` 하나가 답한다** — 우리 폰이 제 발로 누울 때
        // (`RestNode`) 쓰는 바로 그 길이다. 자리·자세를 붙들고 중력·조향을 멈춘다.
        //
        // 포로용으로 따로 붙들면 답이 두 벌이 된다. 실제로 그랬다: `Imprisonment.Hold`가
        // 자세를 다시 세우게 만들어 놨었는데, 우리 폰은 `RestNode`가 다시 눕히고 포로는
        // `Hold`가 다시 눕히는 꼴이라 같은 일을 두 곳이 했다.
        brain.GetComponent<PawnMover>()?.SetSleeping(true, SleepPosition, SleepRotation);
    }

    // ── `ICarrySite` — 업은 것을 받는 자리 ──────────────────────────────────
    //
    // 침대가 원래 갖고 있던 이름들을 그대로 잇는다. **베개 쪽을 침대가 정하는 것**이 여기
    // `PutRotation`이다 — 안 정하면 발이 베개를 벤다.
    Vector3     ICarrySite.PutPosition => SleepPosition;
    Quaternion? ICarrySite.PutRotation => SleepRotation;
    bool        ICarrySite.IsFull      => IsOccupied;
    void        ICarrySite.Put(UnitBrain brain) => PlacePrisoner(brain);
    string      ICarrySite.SiteName    => forPrisoners ? "죄수 침대" : "침대";

    public void Unassign() => AssignedUnit = null;

    public bool TryOccupy(UnitBrain unit)
    {
        // 같은 유닛의 재점유는 허용 — 전투 등으로 잠시 자리를 뜬 뒤 돌아올 때 막히면 안 된다.
        if (_occupant != null && _occupant != unit) return false;
        _occupant = unit;
        return true;
    }

    public void Vacate(UnitBrain unit)
    {
        if (_occupant == unit) _occupant = null;
    }

    // **명령을 받아 침대에서 일어난다 — 배정을 거둘지는 *배정의 뜻*이 정한다.**
    //
    // 점유와 자세는 **늘 푼다**(안 그러면 그 침대를 아무도 못 쓴다). 갈리는 것은 배정이다:
    //
    //   · **손으로 붙인 배정은 "쉬어라"라는 명령**이라, 딴 데로 가라는 명령이 그것을 덮어쓴다.
    //     안 거두면 시킨 자리에 닿는 순간 `RestNode`가 도로 눕혀 **명령이 안 먹는 것처럼 보인다.**
    //   · **저 혼자 잡은 잠자리**(`AutoClaimed`)는 명령이 아니라 그냥 제 방이다 — 덮어쓸 것이
    //     없으므로 **남긴다.** 거두면 한 번 옮겼다는 이유로 잠자리를 잃는다.
    //   · **소집 중이면 남긴다** — `RestNode`가 이미 게이트에 걸려 있어 *"도착하자마자 도로
    //     눕는다"*가 성립하지 않는다. 진형을 잡으려고 한 번 옮겼을 뿐인데 침대가 떨어져 나가고
    //     소집을 풀어도 안 돌아가면, 그건 자원 노드에서 겪은 것과 **같은 사고**다(2026-08-20).
    public static void RouseFrom(UnitBrain unit)
    {
        if (unit == null) return;

        bool ordered = false;
        var  bed     = FindAssigned(unit);
        if (bed != null) ordered = !bed.AutoClaimed && !unit.IsDrafted;

        if (ordered) { ReleaseFrom(unit); return; }

        // 배정은 남기고 자리만 비운다 — 돌아오면 `RestNode`가 다시 점유한다.
        FindOccupiedBy(unit)?.Vacate(unit);
    }

    // **이 폰을 침대에서 놓아 준다** — 배정과 점유를 함께 거둔다.
    //
    // 명령을 받으면(이동·공격·따라가기·줍기) 침대를 떠나야 하는데, 그때 **배정만 두면 도착하는
    // 순간 `RestNode`가 도로 눕히고**(배정이 곧 "쉬어라"라는 명령이라 그게 맞다),
    // **점유만 두면 그 침대를 아무도 못 쓴다** — `RestNode`가 자기 `_bed`를 붙들고 있는데
    // 그 노드는 명령이 도는 동안 아예 안 돌아 스스로 비울 기회가 없다.
    //
    // 그 사이 `Bed.Update`는 자리에 없는 사람을 치료하겠다고 일감을 계속 걸었다.
    public static void ReleaseFrom(UnitBrain unit)
    {
        if (unit == null) return;

        foreach (var bed in _all)
        {
            // **죄수 침대에서 놓아 주면 갇힘도 함께 푼다.** 자리만 비우면 `IsPrisoner`가 남아
            // **자리 없이 갇힌 폰**이 된다 — BT가 안 돌아 영영 서 있고, 풀어 줄 곳도 사라진다.
            // (일으켜 세우는 쪽은 아예 포로를 건드리지 않는다 — `MoveController.Rouse`.
            //  여기 있는 건 그 길로 안 들어온 호출을 위한 안전망이다.)
            if (bed.forPrisoners && bed._occupant == unit) Imprisonment.Unbind(unit);

            // **눕혀 둔 것은 일으켜 세운다.** 침대에서 놓아 주면서 `SetSleeping`을 안 풀면
            // 침대 밖으로 나가서도 누운 자세로 붙들려 있다(`PawnMover.Update`가 통째로
            // 멈춰 있으므로 중력도 조향도 안 돈다).
            //
            // **죽은 사람은 안 일으킨다.** `UnitBrain.Die`가 자리를 비우려고 여기를 지나는데,
            // 그때 자세를 풀면 시체가 침대에서 벌떡 일어선 채로 굳는다(`BecomeCorpse`는
            // 죽은 그 순간의 자세로 멈춘다). 비우는 것과 일으키는 것은 다른 일이다.
            if (bed._occupant == unit && !unit.IsDead)
                unit.GetComponent<PawnMover>()?.SetSleeping(false);

            if (bed.AssignedUnit == unit) bed.AssignedUnit = null;
            bed.Vacate(unit);
        }
    }

    // 이 폰이 **지금 누워 있는** 침대. 폰을 직접 눌러 치료를 시킬 때, 침대에 있는 사람이면
    // 침대 쪽 일감을 써야 한다(여기서 새로 만들면 같은 사람에게 둘이 붙는다).
    // **빈 죄수 침대 중 가장 가까운 것**(`PrisonCage.FindNearestFree`와 짝). 붙잡은 사람을
    // 데려갈 자리를 고를 때 쓴다 — 자리를 미리 못 박지 않고 매번 다시 고르므로, 걸어가는
    // 사이에 그 침대가 차도 다음 것으로 갈아탄다.
    public static Bed FindNearestFreePrison(Vector3 from)
    {
        Bed best = null;
        float bestDist = float.MaxValue;

        foreach (var bed in _all)
        {
            // 휴면 침대는 물어볼 것도 없다 — `enabled = false`가 `OnDisable`로 이 목록에서
            // 빼 준다(`BuildingDormancy`가 침대를 끄는 이유가 바로 그것이다).
            if (bed == null || !bed.forPrisoners || bed.IsOccupied) continue;

            float d = (bed.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = bed; }
        }
        return best;
    }

    public static Bed FindOccupiedBy(UnitBrain unit)
    {
        if (unit == null) return null;
        foreach (var bed in _all)
            if (bed._occupant == unit) return bed;
        return null;
    }

    // **졸린 폰이 빈 침대를 잡는다**(`RestNode`).
    //
    // 배정을 사람이 일일이 하게 두면 폰이 늘 때마다 잡일이 늘고, 잊으면 **그 폰만 조용히
    // 바닥에서 잔다** — 그건 선택이 아니라 놓친 것이다. 손으로 배정하는 길은 그대로 있고
    // (우클릭 [여기 눕는다]) 그쪽이 이긴다: 이미 배정된 폰은 여기까지 오지 않는다.
    //
    // **죄수 침대는 빼고**(그건 가두는 자리다) 남이 배정받지 않은 빈 것 중 가장 가까운 것.
    // 휴면 침대는 물어볼 것도 없다 — `OnDisable`이 목록에서 빼 준다.
    public static Bed ClaimFreeFor(UnitBrain unit)
    {
        if (unit == null) return null;

        var  from = unit.transform.position;
        Bed  best = null;
        float bestDist = float.MaxValue;

        foreach (var bed in _all)
        {
            if (bed == null || bed.forPrisoners) continue;
            if (bed.AssignedUnit != null || bed.IsOccupied) continue;

            float d = (bed.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = bed; }
        }

        if (best != null)
        {
            best.Assign(unit);
            best.AutoClaimed = true;   // 저 혼자 잡은 잠자리 — 가득 채우면 일어난다
        }
        return best;
    }

    // 해당 유닛에게 배정된 침대 반환
    public static Bed FindAssigned(UnitBrain unit)
    {
        foreach (var bed in _all)
            if (bed.AssignedUnit == unit) return bed;
        return null;
    }
}

using UnityEngine;

public class UnitBlackboard
{
    public GameObject Self;
    public PawnMover Mover;

    // **자주 묻는 컴포넌트는 여기 붙여 둔다.** BT는 폰마다 초당 열 번 도는데, 노드들이
    // `bb.Self.GetComponent<UnitBrain>()`을 매번 다시 물었다 — 한 파일에서만 일곱 군데였다.
    // `Mover`를 캐시해 둔 것과 같은 이유이고, 같은 자리에 있어야 다음 사람이 찾는다.
    //
    // (`Brain.Blackboard`가 이 객체이므로 서로를 가리킨다 — 순환이지만 둘 다 폰과 수명이
    //  같아서 끊어질 일이 없다.)
    public UnitBrain          Brain;
    public BodyPartSystem     Body;
    public CombatHandler      Combat;
    public Faction            Faction;

    // 이번 BT 틱까지 실제로 흐른 시간(초). BT는 tickInterval마다 돌므로 노드 안에서
    // Time.deltaTime(한 프레임)을 누적하면 시간이 tickInterval/frameTime 배만큼 느리게 간다.
    // 시간을 다루는 노드는 반드시 이 값을 쓸 것.
    public float TickDelta;

    // 전투
    public GameObject Target;

    // 마지막으로 나를 때린 상대 — 관계(중립/적대)와 무관하게 반격 대상이 된다.
    // 중립 세력이 선공당했을 때 대응할 수 있게 하는 축. RetaliationMemory 동안만 유효.
    public GameObject LastAttacker;
    public float      LastAttackedTime = -999f;
    public const float RetaliationMemory = 15f;

    public bool HasFreshAttacker =>
        LastAttacker != null && Time.time - LastAttackedTime <= RetaliationMemory;

    // **같은 편의 오사는 원한으로 치지 않는다** — 빗나간 화살에 맞았다고 동료에게 칼을 뽑으면
    // 오사가 곧 아군 오폭이 된다. 중립은 기록한다(*중립을 쏴서 원한을 사는* 길이 그것이다).
    //
    // **게이트를 여기 둔다** — 부르는 곳이 둘(피격 · 공격 시도)이라 각자 적으면 한쪽이 빠진다.
    public void RecordAttacker(GameObject attacker)
    {
        if (attacker == null || attacker == Self) return;

        var their = attacker.GetComponent<Faction>();
        if (Faction != null && their != null && Faction.IsAllyOf(their)) return;

        LastAttacker     = attacker;
        LastAttackedTime = Time.time;
    }

    // **사수는 제가 쏠 수 있는 데까지 본다**(2026-08-26, 사용자 검증에서 드러났다).
    //
    // 그전에는 감지 범위가 종족 값(사람 10m)에 고정이라 **사거리가 그 위로 가면 아무 의미가
    // 없었다** — `DetectNode`가 10m에서 적을 잡고 석궁 사거리는 14m라 폰은 **언제나 10m에서
    // 쐈다.** 인지력으로 21m가 돼도 서는 자리가 한 치도 안 바뀐다. *"D와 SSS+가 차이가
    // 없다"*의 정체가 이것이었다(수동 공격 명령일 때만 보였다 — 그때는 표적이 먼저 정해져
    // `FightNode`가 사거리까지만 다가간다).
    //
    // **근접에는 안 붙인다** — 칼을 들었다고 멀리 보일 이유가 없고, 붙이면 사거리 1.5m라
    // 어차피 아무 일도 안 한다.
    public float BaseDetectRange = 10f;   // 종족(`UnitData.DetectRange`)이 정한 값

    // **한번 붙은 싸움은 주위가 빌 때까지 이어진다**(2026-08-26, 사용자 결정).
    //
    // `HasFreshAttacker`(15초)만으로 훑기를 켜면, 때린 놈을 눕히고 15초가 지나는 순간
    // **옆에 적을 두고도 손을 놓는다.** 15초는 *맞은 기억*의 길이지 **싸움의 길이가 아니다** —
    // 싸움이 끝났는지는 시계가 아니라 **주위에 적이 남았는지**가 정한다.
    //
    // **드는 길은 둘 — 맞았거나, 치고 있거나.** *맞았을 때*만 보면 **내가 먼저 친 싸움이
    // 교전으로 안 쳐진다**(우클릭으로 지목해 한 놈을 눕힌 폰이 한 대도 안 맞았으면 그 자리에서
    // 손을 놓는다). **표적을 들고 있다는 것 자체가 교전이다.**
    //
    // 끄는 것은 **훑어 봤는데 아무도 없을 때** 하나다(둘 다 `DetectNode`).
    // `CombatHandler.IsInCombat`과 **다른 물건이다** — 저쪽은 공격 타이머가 도는 중인지다.
    public bool Engaged;

    public float DetectRange
    {
        get => RangedWeapon ? Mathf.Max(BaseDetectRange, AttackRange) : BaseDetectRange;
        set => BaseDetectRange = value;
    }
    // 밤눈(0~1). 감지 범위에 어떻게 섞이는지는 `Vision`이 답한다 — 여기는 값만 든다.
    public float NightVision  = 0f;
    // **원거리 사거리는 인지력이 늘린다**(2026-08-26). 무기가 정한 값이 바탕이고 거기에
    // 배수가 곱해진다 — 읽는 곳이 여덟이라(`FightNode`·`DetectNode` 목줄·오사 부채꼴…)
    // **여기서 한 번 계산하지 않으면** 어느 하나가 맨 무기 값을 그대로 쓴다.
    //
    // **필드가 아니라 프로퍼티인 것이 요점이다** — 인지력은 자라므로 장착할 때 한 번 구워
    // 두면 **키운 만큼이 영영 안 붙는다**(다시 차야 반영된다). 셈은 곱 한 번이라 매 틱
    // 읽어도 값이 없다.
    public float BaseAttackRange = 1.5f;   // 무기(없으면 종족)가 정한 값 — `Equipment`가 넣는다
    public bool  RangedWeapon;             // 지금 든 것이 원거리인가

    public float AttackRange
    {
        get => RangedWeapon ? BaseAttackRange * CombatHandler.RangeMult(Brain?.Stats)
                            : BaseAttackRange;
        set => BaseAttackRange = value;
    }

    public float AttackDamage = 10f;

    // 작업
    public JobBase CurrentJob;
    public ResourceNode AssignedNode;

    // **누가 붙였느냐가 배정의 뜻을 가른다**(`Bed.AutoClaimed`와 같은 축).
    //
    // 손으로 붙였으면 *"너는 저기를 캐라"*라는 **명령**이라 작업 설정(P패널 on/off)을 안 본다
    // — `[치료한다]`·`[먹인다]`가 설정을 안 보는 것과 같은 규칙. 폰이 저 혼자 잡았으면
    // 그건 *"할 일이 없어서 간 것"*이라, 그 작업을 끄면 놓고 나와야 한다.
    //
    // **침대와 달리 자리가 아니라 폰이 든다** — 노드 하나에 `MaxWorkers`만큼 붙으므로
    // 노드에 두면 누구 것인지를 또 갈라야 하는데, 폰은 배정을 하나만 갖는다.
    public bool NodeAutoClaimed;

    // 이번 틱에 RestNode가 실행됐는가 — 상위 분기(전투/소집 등)가 휴식을 밀어냈을 때
    // UnitBrain이 누워 있는 폰을 일으켜 세우는 데 쓴다. 매 틱 UnitBrain이 리셋한다.
    public bool RestTicked;

    // 플레이어가 직접 내린 이동 명령이 진행 중인가.
    // 켜져 있는 동안은 전투·작업 같은 자동 행동보다 우선한다 — 명령했는데 BT가 덮어쓰면
    // 조작이 안 먹는 것처럼 느껴진다. 도착하거나 이동이 끝나면 자동 해제(ManualOrderNode).
    public bool ManualMove;

    // 명령으로 **제자리에 붙들려 있는가**. 원정 편성 중 집결 지점에 먼저 도착한 폰이
    // 다시 일하러 가버리지 않게 잡아둔다 — `ManualMove`는 목적지가 소진되면 자동 해제되므로
    // "도착했지만 계속 대기"를 표현할 수 없다. 푸는 건 명령을 건 쪽의 몫이다.
    public bool HoldPosition;

    // 따라갈 대상. 우클릭 메뉴의 [따라가기]가 켜고, 다른 명령(이동·공격)이 끄거나
    // 대상이 사라지면 `FollowNode`가 스스로 끈다.
    // **`ManualMove`와 다른 축이다** — 저건 "정해진 지점까지 가고 끝"이라 목적지가 소진되면
    // 자동 해제되므로, 계속 쫓아다니는 상태를 표현할 수 없다(`HoldPosition`을 따로 둔 것과 같은 이유).
    public Transform FollowTarget;

    // 주우러 갈 드롭. 우클릭 메뉴의 [줍는다]가 켜고, 주웠거나 남이 먼저 가져가면
    // `PickUpNode`가 스스로 끈다.
    // **자동 운반(`GroundHaulJob`)과 다른 축이다** — 그건 "창고로 가져다 놓아라"는 큐 작업이고
    // 이건 "저걸 네가 들어라"는 직접 명령이다. 그래서 큐를 안 타고 작업 설정에도 안 걸린다.
    public DroppedItem PickUpTarget;

    // 이번 줍기 명령이 **적재 한도를 무시하는가**. 폰 하나만 골라 시켰을 때 켠다 —
    // 그건 "저걸 네가 들어라"이지 일감 배분이 아니므로, 손으로 배낭에 넣는 것과 같아야 한다
    // (과적은 막는 대신 이동·공격 속도로 갚는다). 여럿을 골랐을 때는 한도를 지키는 폰이 간다.
    public bool PickUpIgnoreLimit;

    // 주운 뒤 바로 착용하는가(우클릭 [착용한다]). 원래 차고 있던 것은 `Equipment`가
    // 배낭으로 돌려준다 — 교체는 이미 그렇게 동작하므로 여기서 따로 벗기지 않는다.
    public bool PickUpThenEquip;

    // 상태
    public bool IsDrafted;
    public bool IsUnarmed  = true;
    public bool IsKnockedOut;

    // ── 업기 ────────────────────────────────────────────────────────────────
    // **어깨에 멘 사람 / 나를 메고 있는 사람.** 둘을 같이 두는 이유는 어느 쪽에서 물어도
    // 답이 나와야 해서다 — 업은 폰은 느려지고 한 손이 막히고(`Carrying`), 업힌 폰은
    // 자동 탐지·일감에서 빠져야 한다(`CarriedBy`).
    public UnitBrain Carrying;
    public UnitBrain CarriedBy;

    // 업으러 가라는 명령(우클릭). 닿으면 업고 지워진다 — `PickUpTarget`과 같은 규약이다.
    public UnitBrain CarryTarget;

    // 내려놓을 곳 — **한 자리를 콕 집은 것**(우리·침대를 우클릭해서 고른 그것).
    // 침대·우리면 그 자리에 눕히고, 맨바닥이면 그냥 내린다.
    //
    // **자리는 한 칸이다**(`ICarrySite`) — 전에는 `PutDownBed`·`PutDownCage`가 나란히 있어서
    // 지울 때마다 둘 다 지워야 했고, 무덤이 늘면 셋이 될 참이었다. 자리 종류가 느는 만큼
    // 필드가 느는 구조는 언젠가 한 칸을 빠뜨린다.
    public Vector3    PutDownAt;
    public bool       HasPutDown;
    public ICarrySite PutDownSite;

    // 데려갈 **자리의 종류**(`PrisonEscort`). 위와 다른 축이다 — 저쪽은 "저 우리"고
    // 이쪽은 "우리". 종류로 두면 걸어가는 사이 그 칸이 차도 다음 칸으로 갈아탄다.
    //
    // **업기를 거쳐도 살아남는다** — `[우리로 옮긴다]`는 업기와 데려가기가 한 동작이라,
    // `PawnCarry.TryPickUp`이 지우는 `PutDown*`(지난 명령의 잔재)와 달리 여기 남아 있어야
    // 업은 다음에 그대로 이어진다.
    public EscortDest EscortTo;

    // ── 갇힘 ────────────────────────────────────────────────────────────────
    // **BT가 안 돈다 + 자동 표적에서 빠진다 + 제자리를 지킨다.** 우리든 죄수 침대든 같은
    // 플래그를 쓴다 — 가두는 물건마다 규칙을 따로 두면 한쪽에서 포로가 걸어 나간다.
    public bool       IsPrisoner;
    public Vector3    PrisonSpot;

    // **자세도 가둔 자리가 정한다.** 죄수 침대는 눕히고(베개 쪽), 우리는 세운다.
    // 안 들고 있으면 기절에서 깨어날 때 `BodyPartSystem`이 **기절 직전 자세**로 되돌려
    // 침대에 누워 있던 포로가 벌떡 일어선다.
    public Quaternion PrisonPose = Quaternion.identity;

    // 회유 진행도(0~1). **길이 둘이어도 게이지는 하나다** — 핵이 채우든(하수인 종족)
    // 폰이 설득해서 채우든(사람 종족) 100%가 되면 같은 문으로 들어온다(`Conversion`).
    public float ConvertProgress;

    // 다음 설득 회차를 걸 수 있는 시각(`Conversion.PersuadeCooldown`). **설득당하는 쪽이
    // 들고 있다** — 누가 말을 붙이든 같은 쿨타임이어야 하고(사람을 바꿔 가며 누르면 쉬지 않고
    // 도는 것이 되면 안 된다), 폰이 사라지면 같이 사라져야 한다.
    public float NextPersuadeTime;

    // **탈출 게이지**(0~1). 다 차면 걸어 나간다 — `ConvertProgress`와 나란한 축이다:
    // 하나는 넘어오는 쪽으로, 하나는 나가는 쪽으로 찬다. **관리가 그 경주를 정한다.**
    //
    // 확률로 굴리지 않는 이유는 이 저장소가 여러 번 적어 둔 그것이다 — 모르게 굴리면
    // 어느 날 포로가 사라지고 그게 관리를 안 해서인지 운인지 알 길이 없다.
    public float EscapeProgress;

    // 게이지를 마지막으로 굴린 시각. 부르는 간격과 무관하게 정확한 델타를 재려고 든다
    // (자리의 점검 주기가 바뀌어도 속도가 안 흔들린다). `Time.time`이라 배속도 따라온다.
    public float LastEscapeTick;

    // 풀려나 맵을 떠나는 중. **맞으면 꺼진다**(`LeaveMapNode`) — 살려 보낸 것을 무르는 길.
    public bool    LeavingMap;
    public Vector3 LeaveTarget;

    // **콕 집어 마무리하라고 지목한 대상.** `BTTargetUtil.IsAttackable`이 기절한 폰을 모든
    // 자동 경로(탐지·반격)에서 빼는 것은 KO가 사형선고가 되지 않게 하려는 것이고(켄시식),
    // 그 규칙은 그대로 둔다 — 대신 **사람이 우클릭으로 지목한 하나만** 예외로 통과시킨다.
    // 자동으로는 절대 붙지 않으므로 "내버려 두면 일어난다"가 그대로 성립한다.
    // ── 도발 ──────────────────────────────────────────────────────────────
    // 누가 나를 붙들어 놨나, 언제까지. **묻는 곳은 `Taunt.HolderOf` 하나** — 만료·죽음·기절을
    // 거기서 한꺼번에 보므로 읽는 쪽마다 시간을 비교하지 않는다.
    //
    // **`Target`과 따로 든다.** 표적에 바로 꽂기만 하면 다음 틱에 `DetectNode`가 *"더 가까운
    // 적이 있다"*며 갈아타 **도발이 한 틱만 산다.**
    public GameObject TauntedBy;
    public float      TauntedUntil;

    public GameObject FinishTarget;

    // 순찰
    public PatrolZone AssignedPatrolZone;

    // ── 몸이 요구하는 것들 (0 = 바닥, 100 = 가득) ────────────────────────
    //
    // **저장은 한 벌이다.** 셋이 기계적으로 같은 물건이라(`Needs`) 값을 배열 하나에 두고,
    // 이름 있는 프로퍼티는 **그 배열을 가리키는 별명**일 뿐이다 — 읽기 좋으라고 둔 것이지
    // 따로 든 값이 아니다. 눈금·임계·바닥 벌칙은 전부 `Needs`가 답한다.
    readonly float[] _needs = Needs.NewFull();

    public float Need(NeedKind kind)              => _needs[(int)kind];
    public void  SetNeed(NeedKind kind, float v)  => _needs[(int)kind] = v;

    public float Hunger { get => _needs[(int)NeedKind.Hunger]; set => _needs[(int)NeedKind.Hunger] = value; }
    public float Rest   { get => _needs[(int)NeedKind.Rest];   set => _needs[(int)NeedKind.Rest]   = value; }

    // 배회/복귀 기준점. NPC는 스폰 시 설정, 그 외 유닛은 처음 배회할 때 그 자리로 잡는다.
    public Vector3 HomePosition;
    public bool    HomeSet;

    // NPC 전용
    public Vector3 NpcDestination; // EventMerchant 목적지
    public float   NpcTimer;       // 범용 타이머
    public int     NpcPhase;       // EventMerchant 단계 (0=이동중 1=대기 2=퇴장)
}

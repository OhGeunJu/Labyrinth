using System.Collections.Generic;
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
        Grudges.Add(attacker);   // 같은 편 오사는 위에서 걸렀다
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
    //
    // **교전은 경계 상태의 한 칸이다**(2026-10-18 — `Alert`). 읽고 쓰는 곳(여덟)은 그대로 이 이름을 쓴다.
    public bool Engaged
    {
        get => Alert == AlertState.Combat;
        set
        {
            if (value) Alert = AlertState.Combat;
            else if (Alert == AlertState.Combat) Alert = AlertState.Calm;
        }
    }

    // ── 지각 기억 (2026-10-18 — `docs/combat.md` "지각 기억") ─────────────────────
    //
    // 전에는 **한번 잡은 표적을 벽 너머로도, 맵 끝까지도 정확히 알았다** — `DetectNode`가 새 후보가 없으면 든 것을
    // 그대로 쥐었고, 놓는 조건은 *칠 수 없게 됐나* 하나였다. 이제 **보이는 동안만 안다**(`Vision.CanSee`).
    // 놓치면 마지막으로 본 자리까지 가서 둘러보고(`SearchNode`), 못 찾으면 잊는다.
    //
    // - **보이는 동안은 끝까지 쫓는다** — 시계는 *안 보일 때만* 돈다(사용자: *"도망치는데 거리가 안 벌어지면 끝까지
    //   따라오는 게 맞다"*). 시간이 지났다고 돌아서지 않는다.
    // - **잊는 것은 둘 중 먼저 오는 것**: 마지막으로 본 뒤 `SearchMemory`(게임 초) · 마지막 자리가 `SearchReachMult`
    //   × 감지 범위보다 멀다(거리 + 시간 — 사용자).
    public AlertState Alert;
    public GameObject LastSeenTarget;
    public Vector3    LastSeenPos;
    public float      LastSeenAt = -999f;   // `GameTimeManager.Elapsed`

    public const float SearchMemory    = 30f;   // 안 보인 뒤 이만큼 지나면 잊는다(게임 초)
    public const float SearchLook      = 6f;    // 마지막 자리에 닿아 둘러보는 시간(게임 초)
    public const float SearchReachMult = 3f;    // 마지막 자리가 감지 범위의 이 배수보다 멀면 안 간다

    public void SawTarget(GameObject target)
    {
        LastSeenTarget = target;
        LastSeenPos    = target.transform.position;
        LastSeenAt     = GameTimeManager.Elapsed;
    }

    public void Forget()
    {
        LastSeenTarget = null;
        if (Alert == AlertState.Search) Alert = AlertState.Calm;
    }

    // ── 원한 — 나를 친 놈은 세력과 상관없이 적이다 (2026-10-18) ──────────────────────
    //
    // 사용자: *"이미 적대 관계가 됐는데 시간이 지났다고 풀리는 경우"*. 전에는 중립이 나를 쳐도 **15초**만 칠 수
    // 있었다(`HasFreshAttacker`) — 그 뒤로는 `FactionScan`이 세력만 보므로 다시 중립이었다. 칸도 하나라 두 번째로
    // 친 놈이 첫 놈을 덮었다.
    //
    // **원한은 *누구인가*만 든다 — *어디 있는가*는 안 든다.** 보여야 친다(먼저 노리지도 않는다 — 가까운 순).
    // 시간으로 안 풀린다. 그놈이 죽거나 · 갇히거나 · 내 세력이 바뀌면(`ClearMemory`) 끝난다.
    public readonly HashSet<GameObject> Grudges = new();

    public bool HoldsGrudge(GameObject other) => other != null && Grudges.Contains(other);

    // 죽음 · 세력 변경 — 들고 있던 기억을 다 버린다(`UnitBrain`의 두 자리).
    public void ClearMemory()
    {
        Alert          = AlertState.Calm;
        LastSeenTarget = null;
        Grudges.Clear();
    }

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
        // 근접은 **팔 길이**를 얹는다(`BodySize.Reach` — 큰 종족). 원거리는 몸집과 무관하다.
        get => RangedWeapon ? BaseAttackRange * CombatHandler.RangeMult(Brain?.Stats)
                            : BaseAttackRange + BodySize.Reach(Brain != null ? Brain.Data : null);
        set => BaseAttackRange = value;
    }

    public float AttackDamage = 10f;

    // 작업
    public JobBase CurrentJob;
    public ResourceNode AssignedNode;

    // **이번 틱에 실제로 하고 있는 동작.** `WorkNode`가 자리에서 일감을 돌릴 때 적고, 뇌가 틱마다
    // 없음으로 되돌린다 — 걷는 동안·싸우는 동안은 저절로 없음이다. 애니메이터(`PawnAnimator`)와
    // 손에 드는 도구(`PawnProps`)가 읽는다.
    public WorkAnim Working;

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

    // 따라갈 대상. 우클릭 메뉴의 [따라가기]가 켜고, 다른 명령(이동·공격)이 끄거나
    // 대상이 사라지면 `FollowNode`가 스스로 끈다.
    // **`ManualMove`와 다른 축이다** — 저건 "정해진 지점까지 가고 끝"이라 목적지가 소진되면
    // 자동 해제되므로, 계속 쫓아다니는 상태를 표현할 수 없다.
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

    // **붙들려 있다** — 도축대 상판에 묶인 몸(2026-09-11). 업힌 것과 **같은 뜻의 다른 자리**라
    // `CarriedBy`로는 못 적는다(어깨에서 내려 상판에 올린 상태다).
    //
    // 쓰는 곳은 **기절 시계 하나**다 — 붙들려 있는 동안은 회복이 아니다(`BodyPartSystem`).
    // 침대와 갈리는 자리가 여기다: 침대는 눕혀 두는 것이 곧 회복이라 시계가 가야 하고,
    // 상판은 가르려고 묶어 둔 것이라 안 가야 한다. 둘 다 `SetSleeping`을 쓰므로 **자세로는
    // 구별할 수 없어** 붙드는 쪽이 적어 준다.
    public bool Restrained;

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
    // **사람이 지목한 표적**(2026-09-08). `Target`과 **다른 물음**이다: 저것은 *지금 누구와
    // 싸우나*이고 이것은 *그게 명령이었나*다.
    //
    // 소집 중에는 `FightNode`가 **자리에서 목줄만큼만** 나가는데(진형 유지), 그 규칙은
    // *저 혼자 문 표적*의 것이다. **사람이 콕 집어 준 표적까지 목줄에 걸면** 20m 밖의 적을
    // 치라고 시켜도 폰이 **몇 걸음 나가다 서서** 명령이 안 먹는 것처럼 보인다
    // (`FinishTarget`이 *마무리는 지목해야만 된다*를 적는 것과 같은 모양의 필드다).
    public GameObject OrderedTarget;

    // **소집한 폰의 자리**(2026-09-08). 진형이 준 슬롯이고, 없으면 소집한 그 자리다.
    //
    // **`FightNode`가 이것을 본다** — 소집 중에는 표적에게 다가가되 **여기서 멀어지지 않는
    // 선까지만** 간다. 없으면 표적을 문 폰이 그것을 쫓아 걸어 나가 **진형이 첫 접촉에서
    // 녹는다**(자리를 나눠 주기만 하고 지키게 하지 않으면 대형은 *출발 직전의 그림*일 뿐이다).
    public Vector3 FormationSpot;

    // 원거리가 거리를 벌리는 중인가(`Kiting` — 물러남 · 쏠 때까지 섬).
    public readonly Kiting.State Kite = new Kiting.State();

    public bool    LeavingMap;

    // **떠나는 이유가 둘이라 맞았을 때가 갈린다**(2026-09-08).
    //
    // `LeavingMap`은 *떠나는 중인가*에만 답한다. 놓아주어서 걸어 나가는 사람(풀려난 포로 ·
    // 계약 끝난 용병 · 떠돌이)은 **맞으면 돌아서는 것이 맞다** — 나가는 길이지 도망이 아니다.
    // 그런데 **무너져서 물러나는 습격대는 맞아도 간다**. 그게 패주다.
    //
    // 안 가르면 `LeaveMapNode`의 *맞으면 돌아선다*가 **후퇴를 첫 틱에 취소한다** —
    // 물러나는 무리는 쫓기며 맞는 중이라 그 조건이 **늘 참**이라서, 습격이 영영 안 물러났다
    // (2026-09-08에 실제로 그랬다. 화면에는 *"적이 안 도망쳐"*로만 보인다).
    public bool    Routed;
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

    // **뚫고 있는 건물**(2026-09-30, `DetectNode.Breach` — 적만). 길을 포기한 뒤 고른 길막이를, 막아선 폰이 사거리에
    // 들어오거나 부서질 때까지 붙든다 — 탐지의 *더 가까운 것으로 갈아타기*가 벽 너머 폰을 다시 물지 않게.
    public GameObject BreachTarget;

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

    // **배회 반경을 밖에서 좁힌다**(2026-09-29) — 0이면 트리가 정한 반경. 무리로 다니는 짐승은 기준점을
    // 같이 쓰고 그 둘레에서만 서성여야 떼로 보인다(`WildSpawner`가 적는다 — 반경 12m면 흩어진다).
    public float   WanderRadius;

    // NPC 전용
    public Vector3 NpcDestination; // EventMerchant 목적지
    public float   NpcTimer;       // 범용 타이머
    public int     NpcPhase;       // EventMerchant 단계 (0=이동중 1=대기 2=퇴장)
}

// 경계 상태. 런타임 전용(직렬화 안 함) — 그래도 꼬리에만 붙인다.
public enum AlertState
{
    Calm,     // 평온
    Search,   // 놓친 표적을 찾는 중(마지막으로 본 자리로)
    Combat,   // 교전(`Engaged`)
}

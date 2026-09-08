using UnityEngine;

// **폰이 폰을 어깨에 멘다.** 붙잡은 적을 끌고 오는 것도, 쓰러진 동료를 침대로 옮기는 것도
// 같은 동작이다 — 규칙을 둘로 나누면 하나가 반드시 어긋난다.
//
// **두 팔로 안는 것이 아니라 한쪽 어깨에 멘다.** 그래서 값이 둘 붙는다:
//  · **무게** — 사람 하나는 무겁다. 근력 10(한도 15kg)이면 기어가고, 근력 130(75kg)이면
//    거의 멀쩡하다. 업고 나르는 것이 **힘센 폰의 일**이 되게 하는 값이다.
//  · **한 손** — 남는 손이 하나뿐이라 양손 무기도 방패도 못 쓴다. 전투 한복판에서 누군가를
//    빼내는 것이 **선택**이 되려면, 업는 동안 약해져야 한다.
//
// **시체도 같은 동작으로 옮긴다.** 한때 "시체는 다른 축(뒤져서 물건만 꺼낸다)"으로 두었는데,
// 그러면 싸움이 끝난 자리에 시체가 영영 쌓이고 치울 길이 없다. 메고 걸어가 내려놓는 것은
// 산 사람을 옮기는 것과 **글자 그대로 같은 일**이라 규칙을 따로 만들 이유가 없다 —
// 무게(짐까지 얹힌다)도 한 손 제약도 그대로 걸린다.
//
// 다만 **자리에는 안 들어간다**: 침대도 우리도 죽은 사람을 받지 않는다(`Bed.Accepts` /
// `PrisonCage.TryPut`). 시체로 할 수 있는 것은 옮겨서 아무 데나 내려놓는 것뿐이다.
public static class PawnCarry
{
    // 시체인가. `BecomeCorpse`는 `UnitBrain`을 **끄기만** 하므로(컴포넌트는 남는다)
    // `== null`로는 산 사람과 구별되지 않는다 — 물어볼 자리가 필요하다.
    public static bool IsCorpse(UnitBrain brain)
        => brain != null && brain.GetComponent<Corpse>() != null;

    // 사람 하나의 몸무게(kg). 몸집(`PawnSpec.BodyScale`)이 곱해진다.
    //
    // 적재 한도가 `10 + 근력 × 0.5`이므로 이 값이 곧 **"몇 근력부터 편히 업나"**를 정한다 —
    // 60이면 근력 100에서 한도와 같아진다. 더 낮추면 업는 것이 공짜가 되고, 더 올리면
    // 어지간한 폰은 아예 못 움직인다.
    public const float BodyWeight = 60f;

    // 어깨 높이 — 업힌 폰을 이만큼 올려 붙인다.
    const float ShoulderHeight = 1.1f;
    const float ShoulderSide   = 0.25f;

    // **머리가 뒤로 간다**(2026-09-08, 사용자: *"머리가 뒤로 내 폰에 등쪽에 있어야지"*).
    //
    // 전에는 로컬 Z로 90도 눕혀서 몸이 **좌우로** 뻗었다 — 어깨를 가로지르는 널빤지라
    // 머리가 옆으로 튀어나왔다. 이제 로컬 X로 −90도라 몸이 **앞뒤로** 눕고, 그 방향에서
    // 머리(피벗 반대쪽)가 **등 뒤**에 온다.
    //
    // 몸 한가운데를 어깨에 딱 맞추면 **발이 앞으로 그만큼 튀어나오므로** 조금 뒤로 민다.
    const float ShoulderBack = 0.35f;

    // ── 무게 ────────────────────────────────────────────────────────────────

    // 이 폰을 업으면 몇 kg인가. **그 폰이 지고 있던 짐도 같이 온다** — 어깨에 메는 것이지
    // 배낭을 벗겨 두고 오는 것이 아니다.
    public static float WeightOf(UnitBrain brain)
    {
        if (brain == null) return 0f;

        float scale = brain.Spec != null ? Mathf.Max(0.1f, brain.Spec.BodyScale) : 1f;
        float gear  = brain.Inventory != null ? brain.Inventory.GetTotalWeight() : 0f;
        return BodyWeight * scale + gear;
    }

    // ── 업기 / 내리기 ────────────────────────────────────────────────────────

    public static bool CanCarry(UnitBrain carrier, UnitBrain target)
    {
        if (carrier == null || target == null || carrier == target) return false;
        if (carrier.Blackboard == null || target.Blackboard == null) return false;

        // 업은 폰은 또 못 업는다(어깨는 하나다), 기절한 폰과 시체는 남을 못 업는다.
        if (carrier.Blackboard.Carrying != null) return false;
        if (carrier.Blackboard.IsKnockedOut)     return false;
        if (IsCorpse(carrier))                   return false;

        // 이미 남이 업고 있는 사람은 못 뺏는다.
        if (IsCarried(target)) return false;

        // **시체는 늘 업는다.** 치우는 것이 유일하게 할 수 있는 일이라 조건을 달 이유가 없다
        // (아래 기절·소속 판정은 죽은 자에게 뜻이 없다 — `Faction`은 지워졌고 기절 여부는
        // 죽은 방식에 따라 참일 수도 거짓일 수도 있어서, 안 가르면 답이 들쭉날쭉해진다).
        if (IsCorpse(target)) return true;

        // **우리 편은 멀쩡해도 업는다.** 낭떠러지 앞에서 빼내거나 급히 옮기는 것은 명령이고,
        // 명령을 받는 쪽이 우리 폰이면 "싫다"고 할 이유가 없다.
        if (IsOurs(target)) return true;

        // **이미 붙잡은 사람은 깨어나도 업는다.** 기절을 요구하는 것은 *붙잡는 순간*의
        // 규칙이지, 붙잡아 둔 사람을 옮기는 데까지 걸 이유가 없다 — 회복해 눈을 뜨는 순간
        // 우리에서 꺼낼 길이 사라져 버린다(자리를 바꾸지도, 침대로 데려가지도 못한다).
        if (target.Blackboard.IsPrisoner) return true;

        // **남은 기절해야 업는다.** 멀쩡한 적을 어깨에 메는 것은 붙잡는 게 아니라 제압이고,
        // 그건 전투로 하는 일이다 — 걸어다니는 적을 클릭 한 번으로 데려올 수 있으면 싸울
        // 이유가 사라진다.
        return target.Blackboard.IsKnockedOut;
    }

    static bool IsOurs(UnitBrain brain)
    {
        var f = brain != null ? brain.GetComponent<Faction>() : null;
        return f != null && f.IsPlayer;
    }

    public static bool IsCarried(UnitBrain brain) =>
        brain != null && brain.Blackboard != null && brain.Blackboard.CarriedBy != null;

    // **`keepCurrentJob` — 업기를 시킨 것이 일감 자신일 때.**
    //
    // 아래에서 하던 일감을 놓는데(`AbandonWork`), **그 일감이 바로 이 업기를 시킨 것이면
    // 스스로를 취소한다.** `ButcherJob`이 정확히 그랬다: 시체를 업으러 가서 업는 순간
    // `bb.CurrentJob`(= 그 `ButcherJob`)이 취소되고, 폰은 **시체를 든 채 영영 서 있었다**
    // (도축대를 지어 두면 도축이 통째로 안 됐다 — 도축대 없이 그 자리에서 가르는 길만 돌았다).
    //
    // 기본은 **놓는다** — 명령으로 업는 길(우클릭·간수 호송)에서는 하던 일과 업기가 남남이다.
    public static bool TryPickUp(UnitBrain carrier, UnitBrain target, bool keepCurrentJob = false)
    {
        if (!CanCarry(carrier, target)) return false;

        // **침대·우리·일감에서 먼저 놓아 준다.** 배정만 남으면 내려놓는 순간 도로 그리로 가고,
        // 점유만 남으면 그 자리를 아무도 못 쓴다. 갇힌 사람을 업어 옮기는 것도 같은 길이다
        // (다른 우리로 옮기거나, 회유하려고 침대로 데려가거나).
        PrisonCage.HolderOf(target)?.Release();
        Imprisonment.Unbind(target);
        Bed.ReleaseFrom(target);

        // **업는 순간 하던 일을 놓는다.** 업고 있으면 일감을 안 집지만(`WorkNode`), 이미 손에
        // 든 것은 그 게이트로 안 풀린다 — 자리로 데려가는 동안 `CarryPawnNode`가 Running을
        // 물어 `WorkNode`가 아예 안 돌기 때문이다. 그러면 그 일감은 **예약된 채 얼어붙어
        // 다른 폰도 못 가져간다**(한 명이 멈추는 게 아니라 일감이 통째로 죽는다).
        //
        // 놓는 자리를 여기 두는 이유는 `PutDownSite`를 지우는 것과 같다 — **업기가 시작되는
        // 문은 이 함수 하나뿐**이라, 명령을 내는 쪽마다 챙기게 하면 언젠가 빠뜨린다.
        if (keepCurrentJob) carrier.Blackboard?.AssignedNode?.Unassign(carrier);
        else                carrier.AbandonWork();

        // 걷던 중이면 멈춘다 — 멀쩡한 우리 폰도 업을 수 있으므로 가던 길이 남아 있을 수 있다.
        target.GetComponent<PawnMover>()?.StopMovement();

        // **싸우던 중이었으면 여기서 끈다**(`BodyPartSystem.KnockOut`과 **같은 이유·같은 자리**).
        // 업히면 BT가 통째로 멎는데(`UnitBrain.Update`) 전투 상태를 끄는 자리는 그 BT의
        // `FightNode`뿐이라, **아무도 안 끄면 `IsInCombat`이 참인 채로 얼어붙는다.**
        //
        // `CombatHandler.Update`는 BT가 아니라 **매 프레임 도는 MonoBehaviour**라 그대로 살아서
        // 둘을 한다: ① `FaceTowards`가 월드 회전을 덮어 **어깨 위에서 벌떡 선다**(업힌 자세는
        // 로컬 90도로 눕혀 둔 것이다) ② 공격 타이머가 계속 차서 **어깨 위에서 휘두른다**
        // (사거리 게이트도 BT에 있어 거리를 안 본다).
        //
        // 기절해서 업히는 쪽은 `KnockOut`이 이미 껐다 — 남는 것은 **멀쩡한 우리 편을 싸움
        // 한복판에서 업어 나르는 길**이고, 그게 업기를 만든 이유다.
        target.GetComponent<CombatHandler>()?.ExitCombat();

        var cc = target.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;   // 켜진 채 부모에 붙이면 콜라이더가 제자리로 되민다

        // **콜라이더도 같이 끈다**(2026-09-08). 컨트롤러만 껐더니 **같은 자리의
        // `CapsuleCollider`가 어깨에 매달린 채 살아 있었다** — 트리거도 아니라서, 업힌 몸이
        // 지름 0.7m짜리 물리 덩어리로 업은 폰을 따라다닌다(화면에서는 몸보다 훨씬 큰
        // 초록 캡슐로 보인다). 업힌 것은 **짐이지 몸이 아니다.**
        //
        // 클릭 판정은 이걸로 안 바뀐다 — `SelectionManager.PickVisible`이 **업힌 폰을 이미
        // 건너뛴다**(어깨 위라 업은 폰과 구별이 안 된다). 여기서 없애는 것은 물리 쪽이다.
        foreach (var col in target.GetComponents<Collider>()) col.enabled = false;

        // **부모의 크기를 상쇄한다.** 폰마다 몸집이 다른데(`PawnSpec.BodyScale`), 그냥 자식으로
        // 붙이면 업힌 사람의 월드 크기가 **업은 사람의 크기만큼 곱해진다** — 큰 폰이 업으면
        // 업힌 사람이 커진다. 게다가 내려놓을 때 `SetParent(null, true)`가 그 부푼 크기를
        // `localScale`로 굳혀서, **업을 때마다 조금씩 더 커진다.**
        var world = target.transform.lossyScale;

        target.transform.SetParent(carrier.transform, worldPositionStays: false);

        var parent = carrier.transform.lossyScale;
        target.transform.localScale = new Vector3(
            parent.x != 0f ? world.x / parent.x : world.x,
            parent.y != 0f ? world.y / parent.y : world.y,
            parent.z != 0f ? world.z / parent.z : world.z);

        // **어깨에 얹는 것은 발이 아니라 몸통이다.** 피벗이 발밑에 있으므로(`center.y = height/2`)
        // 그대로 놓으면 업힌 사람이 어깨 **위로** 통째로 솟는다 — 몸 한가운데가 어깨에 오도록
        // 그만큼 내린다. 값은 콜라이더에게 묻는다(높이가 종족마다 다르다).
        //
        // **앞뒤로 눕혔으므로 그 오프셋은 로컬 Z 방향으로 간다** — `Euler(-90,0,0)`이 로컬
        // Y(머리 쪽)를 **−Z(등 뒤)**로 보낸다. 그래서 피벗(발밑)을 +Z로 밀어야 몸 한가운데가
        // 어깨에 온다.
        //
        // **업힌 폰의 크기를 태운다**(2026-09-08). `CenterOffset`은 그 폰의 **제 단위**로
        // 잰 값인데, 방금 `localScale`을 `world/parent`로 맞춰 놨으므로 부모 좌표에서의
        // 실제 반높이는 그 배율만큼 다르다. 안 태우면 **몸집이 1이 아닌 폰**(고블린처럼
        // 작은 종족)의 몸 한가운데가 어깨를 빗나가 **옆으로 붕 뜬 채 실린다.**
        float mid = PawnPlacement.CenterOffset(target.gameObject) * target.transform.localScale.y;
        target.transform.localPosition = new Vector3(ShoulderSide, ShoulderHeight, mid - ShoulderBack);
        target.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 머리가 등 뒤로

        carrier.Blackboard.Carrying = target;
        target.Blackboard.CarriedBy = carrier;

        // **새로 업으면 갈 곳은 백지다.** 지난 "여기 가둔다" 명령이 남아 있으면, 그냥 업기만
        // 시켰는데 폰이 **저 혼자 예전 우리로 걸어간다** — 시킨 적 없는 일이 일어나고,
        // 자리가 차 있으면 거기서 겹쳐 서기까지 한다.
        //
        // 지우는 자리를 여기 두는 이유: 업기가 시작되는 문은 이 함수 하나뿐이라, 명령을 내는
        // 쪽마다 챙기게 하면 언젠가 한 곳을 빠뜨린다.
        carrier.Blackboard.HasPutDown  = false;
        carrier.Blackboard.PutDownSite = null;

        Debug.Log($"[Carry] {carrier.UnitName}이(가) {target.UnitName}을(를) 업었다 " +
                  $"({WeightOf(target):F0}kg)");
        return true;
    }

    // 내려놓는다. `at`이 없으면 업은 폰의 발밑.
    // `facing`을 주면 그 방향으로 눕힌다 — 침대는 베개 쪽이 정해져 있어서 아무 방향으로
    // 눕히면 발이 베개를 베고 눕는다.
    public static void PutDown(UnitBrain carrier, Vector3? at = null, Quaternion? facing = null)
    {
        if (carrier?.Blackboard?.Carrying == null) return;

        var target = carrier.Blackboard.Carrying;
        carrier.Blackboard.Carrying = null;

        // **내려놓으면 데려가던 일도 끝난다.** 자리에 넣었든, 자리가 없어 발밑에 놓았든,
        // 손으로 내려놓았든 마찬가지다 — 여기가 업기가 끝나는 유일한 문이라 여기서 지우면
        // 다음에 다른 사람을 업었을 때 옛 명령이 따라붙지 않는다.
        carrier.Blackboard.EscortTo = EscortDest.None;
        if (target == null) return;                       // 업은 채로 죽어 파괴됐다
        if (target.Blackboard != null) target.Blackboard.CarriedBy = null;

        target.transform.SetParent(null, worldPositionStays: true);

        // **눕히는 방향은 자리가 정한다.** 침대는 베개 쪽이 있어서(`Bed.SleepRotation`)
        // 아무 방향으로 눕히면 발이 베개를 벤다.
        //
        // 자리가 안 정해 주면 스스로 정한다: **깨어 있으면 세우고, 기절했거나 죽었으면
        // 눕힌다** — 멀쩡한 우리 폰도 업을 수 있으므로 눕는 자세를 그대로 두면 멀쩡한
        // 사람이 바닥에 누운 채로 걸어다니고, 시체를 세우면 서 있는 시체가 된다.
        bool down = IsCorpse(target)
                 || (target.Blackboard != null && target.Blackboard.IsKnockedOut);
        var rot   = facing ?? Quaternion.Euler(0f, target.transform.eulerAngles.y, down ? 90f : 0f);

        // 업을 때 꺼 둔 콜라이더를 되켠다. **컨트롤러는 `PawnPlacement`가 켠다** — 표면 위로
        // 띄우는 보정과 한 묶음이라 그쪽이 순서를 안다.
        foreach (var col in target.GetComponents<Collider>())
            if (!(col is CharacterController)) col.enabled = true;

        // 업을 때 꺼 둔 컨트롤러를 **여기서 되켠다.** 표면 위로 띄우는 보정(파묻힘·되밀림)은
        // `PawnPlacement`가 한 곳에서 다룬다 — 스폰·건물 밀어내기와 같은 함정이다.
        PawnPlacement.PlaceAndEnable(target.gameObject, at ?? carrier.transform.position, rot);

        Debug.Log($"[Carry] {carrier.UnitName}이(가) {target.UnitName}을(를) 내려놓았다");
    }

    // 업은 폰이 죽거나 사라질 때 — 짐이 부모째 사라지면 안 된다.
    public static void DropOnDestroy(UnitBrain carrier)
    {
        if (carrier?.Blackboard?.Carrying == null) return;
        PutDown(carrier, carrier.transform.position);
    }
}

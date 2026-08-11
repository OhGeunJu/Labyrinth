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
// **기절한 폰만 멘다.** 멀쩡한 사람을 업고 다니는 것은 이 게임에 필요한 동작이 아니고,
// 시체는 이미 다른 축이다(`Corpse` — 뒤져서 물건만 꺼낸다).
public static class PawnCarry
{
    // 사람 하나의 몸무게(kg). 몸집(`PawnSpec.BodyScale`)이 곱해진다.
    //
    // 적재 한도가 `10 + 근력 × 0.5`이므로 이 값이 곧 **"몇 근력부터 편히 업나"**를 정한다 —
    // 60이면 근력 100에서 한도와 같아진다. 더 낮추면 업는 것이 공짜가 되고, 더 올리면
    // 어지간한 폰은 아예 못 움직인다.
    public const float BodyWeight = 60f;

    // 어깨 높이 — 업힌 폰을 이만큼 올려 붙인다.
    const float ShoulderHeight = 1.1f;
    const float ShoulderSide   = 0.25f;

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

        // 업은 폰은 또 못 업는다(어깨는 하나다), 기절한 폰은 남을 못 업는다.
        if (carrier.Blackboard.Carrying != null) return false;
        if (carrier.Blackboard.IsKnockedOut)     return false;

        // 이미 남이 업고 있는 사람은 못 뺏는다.
        if (IsCarried(target)) return false;

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

    public static bool TryPickUp(UnitBrain carrier, UnitBrain target)
    {
        if (!CanCarry(carrier, target)) return false;

        // **침대·우리·일감에서 먼저 놓아 준다.** 배정만 남으면 내려놓는 순간 도로 그리로 가고,
        // 점유만 남으면 그 자리를 아무도 못 쓴다. 갇힌 사람을 업어 옮기는 것도 같은 길이다
        // (다른 우리로 옮기거나, 회유하려고 침대로 데려가거나).
        PrisonCage.HolderOf(target)?.Release();
        Imprisonment.Unbind(target);
        Bed.ReleaseFrom(target);

        // 걷던 중이면 멈춘다 — 멀쩡한 우리 폰도 업을 수 있으므로 가던 길이 남아 있을 수 있다.
        target.GetComponent<PawnMover>()?.StopMovement();

        var cc = target.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;   // 켜진 채 부모에 붙이면 콜라이더가 제자리로 되민다

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

        target.transform.localPosition = new Vector3(ShoulderSide, ShoulderHeight, 0f);
        target.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // 기절 자세 그대로

        carrier.Blackboard.Carrying = target;
        target.Blackboard.CarriedBy = carrier;

        // **새로 업으면 갈 곳은 백지다.** 지난 "여기 가둔다" 명령이 남아 있으면, 그냥 업기만
        // 시켰는데 폰이 **저 혼자 예전 우리로 걸어간다** — 시킨 적 없는 일이 일어나고,
        // 자리가 차 있으면 거기서 겹쳐 서기까지 한다.
        //
        // 지우는 자리를 여기 두는 이유: 업기가 시작되는 문은 이 함수 하나뿐이라, 명령을 내는
        // 쪽마다 챙기게 하면 언젠가 한 곳을 빠뜨린다.
        carrier.Blackboard.HasPutDown  = false;
        carrier.Blackboard.PutDownBed  = null;
        carrier.Blackboard.PutDownCage = null;

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
        // 자리가 안 정해 주면 스스로 정한다: **깨어 있으면 세우고, 기절했으면 눕힌다** —
        // 멀쩡한 우리 폰도 업을 수 있으므로 눕는 자세를 그대로 두면 멀쩡한 사람이 바닥에
        // 누운 채로 걸어다닌다.
        bool down = target.Blackboard != null && target.Blackboard.IsKnockedOut;
        var rot   = facing ?? Quaternion.Euler(0f, target.transform.eulerAngles.y, down ? 90f : 0f);

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

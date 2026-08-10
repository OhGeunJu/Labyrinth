using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MoveController : MonoBehaviour
{
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] LayerMask groundMask = ~0;
    Camera _cam;

    void Awake() => _cam = Camera.main;

    void Update()
    {
        HandleMove();
    }

    void HandleMove()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;
        if (CoreRelocator.IsActive) return;   // 이전 모드에선 우클릭 = 취소

        // **UI 위에서 누른 우클릭은 월드로 새지 않는다.** 창을 열어 둔 채 그 위를 우클릭하면
        // 뒤의 땅으로 이동 명령이 나가거나 뒤에 선 것을 겨눴다 — 배낭·상점을 다루는 내내
        // 발밑을 조심해야 했다. 월드맵(`WorldMapView`)은 이미 `OverUI()`로 막고 있었다.
        //
        // 좌클릭(`SelectionManager._pressOverUI`)처럼 누름/놓음을 짝지어 붙들 필요는 없다.
        // 우클릭은 **누르는 그 프레임에 전부 끝나므로**(드래그가 없다) 누른 곳만 보면 된다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 선택지 창이 떠 있으면 이번 우클릭은 **창을 닫는 데만** 쓴다(창 쪽이 닫는다).
        // 여기서도 처리하면 창을 닫으면서 동시에 명령이 나가 버린다.
        if (PawnContextMenuUI.Instance != null && PawnContextMenuUI.Instance.IsOpen) return;
        if (selectionManager.Selected.Count == 0) return;

        // 방랑자만 명령 대상 (하수인은 정보 확인만 가능)
        var wanderers = new List<PawnMover>();
        foreach (var pawn in selectionManager.Selected)
        {
            if (pawn == null) continue; // 선택 후 파괴된 유닛
            var brain = pawn.GetComponent<UnitBrain>();
            if (brain != null && !(brain.Data is MinionData))
                wanderers.Add(pawn);
        }
        if (wanderers.Count == 0) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        // **적을 겨눴을 때 우클릭의 뜻은 상황이 정한다 — 붙기 전에는 여럿, 붙고 나면 하나.**
        //
        // 아직 안 싸우는 중이면 물어볼 것이 있다(칠 것인가 / 쓰러진 놈을 마무리할 것인가 /
        // 따라붙을 것인가). 그때는 시간도 있다. 반대로 이미 붙었으면 뜻이 "저놈을 쳐라"
        // 하나뿐이고, 표적을 바꿀 때마다 창이 뜨면 조작만 느려진다.
        //
        // 쓰러진 놈은 **싸우는 중이어도 창을 띄운다** — 지금 그 자리에서 할 수 있는 건
        // 마무리뿐인데, 그건 지목해야만 되는 일이라(`FinishTarget`) 즉시 공격으로 보내면
        // 아무 일도 안 일어나는 헛클릭이 된다.
        bool hasTarget = PickTarget(ray, wanderers, out RaycastHit first);

        if (hasTarget)
        {
            var go      = first.collider.gameObject;
            var faction = first.collider.GetComponent<Faction>();
            if (faction != null && !faction.IsPlayer && IsHostileToUs(faction, wanderers)
                && Fighting(wanderers) && !IsDown(go))
            {
                AttackWith(wanderers, go);
                return;
            }
        }

        // 그 밖의 모든 것은 **선택지 창**으로 간다 — 바닥까지 포함해서.
        // 바닥도 창을 거치므로 이동이 두 번 누르기가 된다. 대신 "우클릭하면 뭘 할 수 있는지 뜬다"가
        // 예외 없이 성립한다(예외가 있으면 어디서 뜨는지 매번 기억해야 한다).
        OpenMenuAt(ray, wanderers, hasTarget, first);
    }

    // 우클릭이 무엇을 겨눴는가. 가장 가까운 것 하나.
    //
    // **명령 받는 폰도 겨눌 수 있다.** 한때 건너뛰었는데(고른 폰을 누르면 `따라간다`(자기를
    // 따라감)가 떠서), 자기 자신을 치료하는 길이 생기면서 **겨눌 수 있어야** 하게 됐다.
    // 말이 안 되던 것은 겨냥이 아니라 **선택지**였다 — 그건 `AddUnitOptions`가 가른다.
    static bool PickTarget(Ray ray, List<PawnMover> commanders, out RaycastHit hit)
        => Physics.Raycast(ray, out hit, 500f);

    // 실제 이동 명령. 창의 [여기로 이동]과 옛 직접 명령이 같은 코드를 쓴다.
    void MoveTo(List<PawnMover> commanders, Vector3 point)
    {
        Rouse(commanders);   // 딴 데로 가라는 명령 = 침대에서 일어난다
        Vector3[] positions = CalcFormation(point, commanders.Count);
        for (int i = 0; i < commanders.Count; i++)
        {
            // 이동 명령 = 교전 이탈 + 하던 작업 취소.
            // Target을 그대로 두면 FightNode가 다음 틱에 목적지를 적 위치로 덮어쓰고,
            // 작업을 남겨두면 도착하자마자 일하러 돌아간다 — 둘 다 명령이 안 먹는 것처럼 보인다.
            var brain = commanders[i].GetComponent<UnitBrain>();
            var bb    = brain.Blackboard;

            bb.Target       = null;
            bb.FinishTarget = null;   // 딴 데로 가라는 명령 = 마무리 지목도 거둔다
            bb.ManualMove   = true;
            bb.FollowTarget = null;   // 다른 데로 가라는 명령 = 따라가기 그만
            brain.AbandonWork();

            commanders[i].GetComponent<CombatHandler>()?.ExitCombat();
            commanders[i].SetMoveTarget(positions[i]);
        }
    }

    // 고른 폰들 중 **하나라도** 적대로 보면 적대로 친다. 섞여 있을 일은 드물지만,
    // 그때 "일부는 치고 일부는 메뉴"가 되면 무슨 일이 일어났는지 알 수 없다.
    static bool IsHostileToUs(Faction target, List<PawnMover> commanders)
    {
        foreach (var pawn in commanders)
        {
            var mine = pawn.GetComponent<Faction>();
            if (mine != null && mine.IsHostileTo(target)) return true;
        }
        return false;
    }

    // 지금 싸우는 중인가. **이미 붙었으면 우클릭은 표적 전환이지 물어볼 자리가 아니다.**
    //
    // 방랑자 BT에는 `DetectNode`가 없어 `Target`이 저 혼자 붙는 일이 없다 — 여기(우클릭)에서만
    // 붙는다. 그래서 이 검사는 곧 **"내가 공격 명령을 내려 둔 상태인가"** 다.
    // 맞고 있는 중(`HasFreshAttacker`)도 싸우는 것이다 — 두들겨 맞으면서 창을 고를 수는 없다.
    static bool Fighting(List<PawnMover> commanders)
    {
        foreach (var pawn in commanders)
        {
            var bb = pawn.GetComponent<UnitBrain>()?.Blackboard;
            if (bb == null) continue;
            if (bb.HasFreshAttacker) return true;
            if (bb.Target != null && BTTargetUtil.IsAttackable(bb.Target, bb)) return true;
        }
        return false;
    }

    // 창 제목 = **개체 이름 + 종족**(`그르낙 (고블린)`).
    //
    // 종족만 띄우면 **어느 놈을 눌렀는지 알 수 없다** — 고블린이 셋 서 있으면 셋 다 "고블린"이고,
    // 겨냥이 빗나가 엉뚱한 것을 눌러도 그럴듯해 보여서 드러나지 않는다.
    // `UnitHPUI`가 이미 개체 이름을 먼저 쓰므로 규약도 그쪽에 맞춘다(창마다 다른 이름으로
    // 불리면 같은 폰인지 알 수 없다).
    static string PawnTitle(UnitBrain brain, GameObject go)
    {
        string race = brain.Data != null ? brain.Data.RaceName : null;
        string name = brain.UnitName;

        bool hasName = !string.IsNullOrWhiteSpace(name);
        bool hasRace = !string.IsNullOrWhiteSpace(race);

        if (hasName && hasRace && name != race) return $"{name} ({race})";
        if (hasName) return name;
        if (hasRace) return race;
        return go.name;
    }

    // 쓰러져 누운 폰인가. 여기서만 [공격한다]가 [마무리한다]로 바뀐다.
    static bool IsDown(GameObject go)
    {
        var bb = go.GetComponent<UnitBrain>()?.Blackboard;
        return bb != null && bb.IsKnockedOut;
    }

    static void AttackWith(List<PawnMover> commanders, GameObject target)
    {
        Rouse(commanders);
        foreach (var pawn in commanders)
        {
            var bb = pawn.GetComponent<UnitBrain>().Blackboard;
            bb.Target       = target;
            bb.FinishTarget = null;    // 멀쩡한 놈을 치는 명령 — 지난 지목이 남아 있으면 안 된다
            bb.ManualMove   = false;   // 공격 명령이 이동 명령을 대체
            bb.FollowTarget = null;    // 따라가던 중이었으면 그만둔다
        }
    }

    // **마무리는 지목해야만 된다.** 기절한 폰은 모든 자동 경로에서 대상에서 빠지므로
    // (`BTTargetUtil.IsAttackable` — KO가 사형선고가 되지 않게 하는 켄시식 규칙),
    // 그 규칙을 건드리는 대신 **사람이 콕 집은 대상 하나만** 예외로 뚫는다.
    // 적 AI는 이 길을 쓰지 않으므로 **쓰러진 우리 폰은 여전히 안 맞는다** — 예외는 한쪽 방향이다.
    static void FinishWith(List<PawnMover> commanders, GameObject target)
    {
        Rouse(commanders);
        foreach (var pawn in commanders)
        {
            var bb = pawn.GetComponent<UnitBrain>().Blackboard;
            bb.Target       = target;
            bb.FinishTarget = target;
            bb.ManualMove   = false;
            bb.FollowTarget = null;
        }
    }

    // 선택지 구성은 **여기서** 정한다(`PawnContextMenuUI`는 받은 것만 그린다).
    // 대상 종류를 창 안에서 따지기 시작하면 판단이 두 곳으로 갈라진다
    // (월드맵 도착 메뉴를 `WorldMapView.OpenMenu`가 짜는 것과 같은 규칙).
    // `hasHit`/`hit`은 `PickTarget`이 이미 고른 것 — **여기서 다시 쏘지 않는다.**
    // 따로 쏘면 "명령 받는 폰은 건너뛴다"는 규칙이 한쪽에만 걸려, 즉시 공격과 창이 서로 다른
    // 것을 겨누게 된다.
    void OpenMenuAt(Ray ray, List<PawnMover> commanders, bool hasHit, RaycastHit hit)
    {
        var menu = PawnContextMenuUI.Instance;

        // 바닥 지점은 따로 잰다 — 유닛·건물을 맞았어도 [여기로 이동]은 바닥을 가리켜야 한다.
        bool hasGround = Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask);

        if (!hasHit)
        {
            // 아무것도 안 맞았는데 바닥만 잡혔으면 그냥 이동(창에 넣을 게 하나뿐이다).
            if (hasGround) MoveTo(commanders, groundHit.point);
            return;
        }

        // 창이 씬에 없으면(생성기를 아직 안 돌렸으면) 예전처럼 바로 이동한다 —
        // 조작이 통째로 죽는 것보다는 낫다.
        if (menu == null)
        {
            if (hasGround) MoveTo(commanders, groundHit.point);
            return;
        }

        var go      = hit.collider.gameObject;
        var options = new List<(string, System.Action)>();
        string title = "이곳";

        var brain   = go.GetComponent<UnitBrain>();
        var node    = go.GetComponent<ResourceNode>();
        var drop    = go.GetComponent<DroppedItem>();
        var corpse  = go.GetComponent<Corpse>();
        var core    = go.GetComponent<DungeonCore>();
        var clickable = go.GetComponent<IWorldClickable>();

        if (brain != null)
        {
            title = PawnTitle(brain, go);
            bool down = IsDown(go);

            // **명령 받는 폰 자신을 눌렀는가.** 자기에게 시킬 수 있는 일은 치료뿐이다 —
            // 자기를 따라가거나 자기를 칠 수는 없다(그 줄이 뜬 것이 한때의 버그였다).
            bool self = IsCommander(brain, commanders);

            // 거래는 상인일 때만. `Merchant`는 상인 역할이면 자동으로 붙는다.
            // 쓰러진 상인은 선택지에서 뺀다 — 눌러 봐야 아무 일도 안 일어난다.
            var merchant = go.GetComponent<Merchant>();
            if (!self && merchant != null && merchant.CanTrade)
                options.Add(("거래한다", merchant.OnWorldClick));

            // 누운 것을 따라다닐 수는 없다.
            if (!self && !down) options.Add(("따라간다", () => FollowWith(commanders, go.transform)));

            // **치료 — 침대에 눕히지 않아도 된다.** 전투 직후 쓰러진 사람을 그 자리에서 돌보는
            // 것이 침대까지 끌고 가는 것보다 급하다. 자기 자신도 된다(그때는 스스로 싸맨다).
            AddTreatOption(brain, go, commanders, self, options);

            // 아군(플레이어 세력)까지 칠 이유는 없다 — 오사 사고만 난다.
            var faction = go.GetComponent<Faction>();
            if (!self && (faction == null || !faction.IsPlayer))
            {
                // **문구는 하나다 — 쓰러진 놈에게도 [공격한다].** 하는 일이 다르긴 하지만
                // (지목해야만 뚫리는 예외를 켠다) 사람이 뜻하는 바는 같고, 상태마다 다른 낱말을
                // 쓰면 [마무리한다]가 별개의 동작인 줄 알게 된다. 갈림은 안에서 처리한다.
                System.Action attack = down ? () => FinishWith(commanders, go)
                                            : () => AttackWith(commanders, go);
                options.Add(("공격한다", attack));
            }
        }
        else if (node != null)
        {
            bool tree = node.Data != null && node.Data.IsTree;
            title = node.Data?.DropItem != null ? node.Data.DropItem.ItemName : "자원";

            // 남은 자리를 **선택지에 적는다.** 눌러 보고 나서야 자리가 없는 걸 아는 것보다
            // 누르기 전에 보이는 편이 낫다. 꽉 찼으면 그 사실만 적고 아무 일도 하지 않는다.
            string room = node.MaxWorkers > 0
                        ? $" ({node.AssignedCount}/{node.MaxWorkers})"
                        : "";
            string verb = tree ? "여기서 나무를 벤다" : "여기서 캔다";

            if (node.IsFull)
            {
                options.Add(($"{verb}{room} - 자리 없음", () => { }));
            }
            else
            {
                options.Add(($"{verb}{room}", () =>
                {
                    int rejected = 0;
                    foreach (var pawn in commanders)
                    {
                        var b = pawn.GetComponent<UnitBrain>();
                        if (b == null) continue;
                        if (!node.Assign(b)) rejected++;
                    }
                    if (rejected > 0)
                        Debug.Log($"[자원 노드] {node.name}: 자리가 {node.MaxWorkers}명뿐이라 " +
                                  $"{rejected}명은 배정하지 못했습니다.");
                }));
            }
            options.Add(("살펴본다", () => SelectionManager.InspectNode(node)));
        }
        else if (drop != null)
        {
            // **바닥에 떨어진 것을 손으로 집는 길.** 자동 운반(`GroundHaulJob`)은 받아 줄 창고가
            // 있고 폰이 운반 작업을 켰을 때만 도는데, 그 조건이 안 맞으면 물건이 영영 거기 남아
            // 주울 방법이 없었다 — 발견 이벤트가 떨군 물건이 정확히 그랬다.
            title = drop.Item != null ? $"{drop.Item.ItemName} x{drop.Amount}" : "떨어진 물건";

            // 한 명만 골랐으면 한도를 넘겨서라도 든다 — 아래 `PickUpWith` 주석 참고.
            bool ignoreLimit = commanders.Count == 1;

            // 들 수 있는 폰이 있는지 **누르기 전에** 보여 준다(자원 노드의 "자리 없음"과 같은 규칙).
            bool anyRoom = false;
            foreach (var pawn in commanders)
                if (CanTake(pawn.GetComponent<UnitBrain>(), drop, ignoreLimit)) { anyRoom = true; break; }

            // **장비는 [착용한다]가 먼저다.** 무기·갑옷을 주우라고 시키는 대부분의 이유가
            // 그걸 차기 위해서인데, 줍고 나서 배낭 창을 열어 다시 차게 하면 두 조작이 된다.
            // 원래 차고 있던 것은 `Equipment`가 배낭으로 돌려준다.
            if (drop.Item is WeaponData || drop.Item is ArmorData)
                options.Add(("착용한다", () => PickUpWith(commanders, drop, equip: true)));

            if (anyRoom) options.Add(("줍는다", () => PickUpWith(commanders, drop)));
            else         options.Add(("줍는다 - 배낭에 자리가 없음", () => { }));

            // 손으로 내려놓은 것은 아무도 안 건드린다 — 되돌리는 길을 여기 둔다.
            if (drop.NoAutoHaul) options.Add(("창고로 옮긴다", drop.AllowAutoHaul));
        }
        else if (corpse != null)
        {
            title = "시체";
            options.Add(("뒤진다", () => SelectionManager.InspectCorpse(corpse)));
        }
        else if (core != null)
        {
            title = "던전 핵";
            options.Add(("연다", core.Select));
        }
        else if (clickable != null)
        {
            var placed = go.GetComponentInParent<PlacedBuilding>();
            title = placed?.Data != null ? placed.Data.BuildingName : go.name;

            // **"연다"는 안에 물건이 든 것에만 쓴다.** 창고는 열어서 넣고 빼지만,
            // 작업대·연구대·침대·농지는 상태를 들여다보는 것이라 자원 노드와 같은 말이 맞다.
            string verb = go.GetComponent<Storage>() != null ? "연다" : "살펴본다";

            // 휴면(핵 영역 밖) 건물은 열어도 아무것도 못 한다 — 선택지에서 뺀다.
            if (!BuildingDormancy.IsDormantAt(hit.collider))
                options.Add((verb, clickable.OnWorldClick));

            // **침대는 명령을 받는 물건이다.** 눕히는 것도 치료를 시키는 것도 고른 폰에게
            // 내리는 지시라, 패널을 열어 배정하는 것보다 우클릭이 자연스럽다.
            var bed = go.GetComponent<Bed>();
            if (bed != null) AddBedOptions(bed, commanders, options);
        }

        // 여기까지 아무것도 안 붙었으면 **맨바닥**이다. 이때 창을 띄우면 선택지가 하나뿐인
        // 창을 매번 한 번 더 누르게 만드는 것뿐이라, 그냥 옮긴다 — 이동은 제일 잦은 명령이다.
        // 창은 **고를 것이 실제로 있을 때만** 뜬다.
        if (options.Count == 0)
        {
            if (hasGround) MoveTo(commanders, groundHit.point);
            return;
        }

        // **이동은 어디를 우클릭했든 마지막에 붙는다.** 건물 위를 눌렀다고 이동을 못 하면
        // 좁은 데서 폰을 옮길 방법이 없어진다.
        if (hasGround)
        {
            Vector3 dest = groundHit.point;
            options.Add(("여기로 이동", () => MoveTo(commanders, dest)));
        }

        // 창은 **우클릭한 월드 지점**에 붙는다(화면이 아니라) — 카메라를 움직여도 그 자리에 남는다.
        menu.Open(title, options, hit.point);
    }

    // 이 폰이 지금 명령을 받는 무리에 들어 있는가(= 자기 자신을 눌렀는가).
    static bool IsCommander(UnitBrain brain, List<PawnMover> commanders)
    {
        var mover = brain != null ? brain.GetComponent<PawnMover>() : null;
        if (mover == null) return false;

        foreach (var c in commanders) if (c == mover) return true;
        return false;
    }

    // **폰을 직접 눌러 치료를 시킨다** — 침대에 눕히지 않아도 된다.
    //
    // 침대에서 나는 일감(`Bed`)은 그대로 두고, 이건 손으로 콕 집는 길이다. 그래서 **치료를
    // 꺼 둔 폰에게도 간다**(`JobManager.ReserveFor` — 자원 노드 직접 배정과 같은 규칙).
    //
    // 침대에 누워 있는 환자면 **침대 쪽 일감을 쓴다** — 여기서 하나 더 만들면 같은 사람에게
    // 둘이 붙고, 침대가 제 일감을 지울 때 이건 안 지워진다.
    static void AddTreatOption(UnitBrain patient, GameObject go, List<PawnMover> commanders,
                               bool self, List<(string, System.Action)> options)
    {
        var body = go.GetComponent<BodyPartSystem>();
        if (body == null || !body.NeedsTreatment) return;

        // 남을 치료하는 건 우리 편만. 적을 살려 줄 길(포로 치료)은 아직 없다 —
        // 생기면 그때 여기서 갈라야 한다는 것이 드러나야 한다.
        var faction = go.GetComponent<Faction>();
        if (!self && (faction == null || !faction.IsPlayer)) return;

        var doctor = self ? patient
                          : NearestTo(commanders, go.transform.position)?.GetComponent<UnitBrain>();
        if (doctor == null) return;

        // 기절한 폰은 남을 돌볼 수 없다(자기 자신도 마찬가지 — 손이 안 움직인다).
        if (doctor.Blackboard != null && doctor.Blackboard.IsKnockedOut) return;

        string label = self ? "스스로 치료한다" : $"{patient.UnitName}을(를) 치료한다";

        options.Add((label, () =>
        {
            var bed = Bed.FindOccupiedBy(patient);
            if (bed != null) { doctor.AbandonWork(); bed.RequestTreatBy(doctor); return; }

            // **두 번 누르면 두 번 시키는 것이 아니다.** 침대 쪽은 `Bed`가 제 일감을 하나만
            // 들고 있어 저절로 막히는데, 침대 없는 환자는 누를 때마다 새로 만들어 큐에 쌓였다 —
            // 의사는 같은 사람을 연달아 치료하고, 환자가 먼저 나으면 남은 것들이 `IsReady == false`
            // 인 채로 영영 남아 매 `RequestJob`마다 훑힌다.
            if (JobManager.Instance == null) return;
            if (JobManager.Instance.HasTreatJobFor(patient)) return;

            doctor.AbandonWork();
            JobManager.Instance.ReserveFor(new TreatJob(patient, manual: true), doctor);
        }));
    }

    // **명령을 받으면 침대에서 일어난다 — 기절 중이 아니면.**
    //
    // `SetMoveTarget`은 이미 누운 자세를 푼다. 여기서 더 하는 것은 **자리를 비우는 일**이다:
    // 배정을 두면 도착하는 순간 `RestNode`가 도로 눕히고(배정이 곧 "쉬어라"다), 점유를 두면
    // 그 침대를 아무도 못 쓴다. 명령이 도는 동안 `RestNode`는 아예 안 돌아 스스로 못 비운다.
    //
    // **기절한 폰은 건드리지 않는다.** 못 일어나는데 침대만 뺏으면(`SetMoveTarget`도 무시한다)
    // 깨어나서 돌아갈 자리가 없다 — 명령이 안 먹는 것이 아니라 해가 된다.
    static void Rouse(List<PawnMover> commanders)
    {
        foreach (var pawn in commanders)
        {
            var brain = pawn != null ? pawn.GetComponent<UnitBrain>() : null;
            if (brain?.Blackboard == null || brain.Blackboard.IsKnockedOut) continue;

            Bed.ReleaseFrom(brain);
            pawn.SetSleeping(false);
        }
    }

    // 침대 우클릭 — **눕는다 / 치료한다.**
    //
    // 둘 다 "가장 가까운 한 명"에게 간다. 침대는 하나뿐이라 여럿을 눕힐 수 없고, 치료도
    // 한 사람이면 족하다 — 여럿을 보내면 먼저 닿은 하나만 일하고 나머지는 헛걸음한다
    // ([줍는다]와 같은 규칙).
    static void AddBedOptions(Bed bed, List<PawnMover> commanders,
                              List<(string, System.Action)> options)
    {
        var nearest = NearestTo(commanders, bed.transform.position);
        var brain   = nearest != null ? nearest.GetComponent<UnitBrain>() : null;
        if (brain == null) return;

        // 눕기 — 배정이 곧 "쉬어라"라는 명령이다(`RestNode`가 그걸 읽는다).
        if (bed.AssignedUnit != brain)
            options.Add(("여기 눕는다", () => bed.Assign(brain)));
        else
            options.Add(("일어난다", bed.Unassign));

        // 치료 — 누가 누워 있고 손댈 것이 남았을 때만. **자기 자신은 못 고친다**(누운 채로
        // 하는 자가치유는 `WorkSettings.SelfTend`가 따로 있다).
        if (bed.Occupant != null && bed.Occupant != brain && bed.NeedsTreatment)
            options.Add(($"{bed.Occupant.UnitName}을(를) 치료한다", () =>
            {
                brain.AbandonWork();          // 하던 일을 놓아야 새 일감을 집는다
                bed.RequestTreatBy(brain);
            }));
    }

    static PawnMover NearestTo(List<PawnMover> commanders, Vector3 point)
    {
        PawnMover best = null;
        float bestDist = float.MaxValue;

        foreach (var pawn in commanders)
        {
            if (pawn == null) continue;
            float d = Vector3.SqrMagnitude(pawn.transform.position - point);
            if (d < bestDist) { bestDist = d; best = pawn; }
        }
        return best;
    }

    // 이 폰이 이 뭉치를 들 수 있는가.
    // **무게와 격자를 둘 다 본다** — 무게만 보면 배낭 격자가 꽉 찬 폰이 통과해서, 거기까지
    // 걸어가 놓고 한 개도 못 싣는다(`GroundHaulJob.CanAssignTo`와 같은 규칙).
    // `ignoreLimit`이면 무게는 안 본다. 격자는 물리적 한계라 어느 쪽이든 넘을 수 없다.
    static bool CanTake(UnitBrain b, DroppedItem drop, bool ignoreLimit)
    {
        if (b == null || b.Inventory == null || drop == null || drop.Item == null) return false;
        if (!b.Inventory.CanFit(drop.Item, 1, drop.Quality)) return false;
        return ignoreLimit || CarryCapacity.RoomFor(b, drop.Item) > 0;
    }

    // **한 명만 보낸다.** 뭉치 하나를 여럿에게 시키면 먼저 닿은 하나가 집고 나머지는 헛걸음한다.
    // 들 수 있는 폰 중 가장 가까운 하나 — 제일 가까운 폰의 짐이 찼으면 **그다음 폰이 간다.**
    //
    // **혼자 골랐을 때는 적재 한도를 넘겨서라도 든다.** 그건 일감 배분이 아니라 "저걸 네가
    // 들어라"라서, 손으로 배낭에 넣는 것과 같아야 한다 — 넘긴 만큼 이동·공격이 느려지는 것으로
    // 이미 값을 치른다. 여럿을 골랐을 때만 한도가 누가 갈지를 가른다.
    // `equip` — 주운 뒤 바로 착용한다. 그때는 적재 한도를 안 본다: 물건이 배낭을 스쳐
    // 곧바로 몸에 붙으므로 짐으로 남지 않는다(격자 자리 하나만 잠깐 필요하고, 그 자리는
    // 벗겨진 것이 도로 들어올 자리이기도 하다).
    static void PickUpWith(List<PawnMover> commanders, DroppedItem drop, bool equip = false)
    {
        bool ignoreLimit = equip || commanders.Count == 1;

        UnitBrain best = null;
        float bestDist = float.MaxValue;

        foreach (var pawn in commanders)
        {
            var b = pawn.GetComponent<UnitBrain>();
            if (!CanTake(b, drop, ignoreLimit)) continue;

            float d = (pawn.transform.position - drop.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = b; }
        }
        if (best == null) return;

        var bb = best.Blackboard;
        bb.PickUpTarget      = drop;
        bb.PickUpIgnoreLimit = ignoreLimit;
        bb.PickUpThenEquip   = equip;
        bb.Target            = null;    // 줍는 동안 교전으로 새지 않게
        bb.FinishTarget      = null;
        bb.FollowTarget      = null;
        bb.ManualMove        = false;
        best.AbandonWork();             // 하던 일감을 놓지 않으면 도착하자마자 일하러 돌아간다

        // **한 명만 보내므로 그 한 명만 일으킨다.** 위에서 `commanders` 전부를 깨우면
        // 가지도 않을 폰들의 침대가 같이 풀린다.
        Rouse(new List<PawnMover> { best.GetComponent<PawnMover>() });
    }

    static void FollowWith(List<PawnMover> commanders, Transform target)
    {
        Rouse(commanders);
        foreach (var pawn in commanders)
        {
            var bb = pawn.GetComponent<UnitBrain>().Blackboard;
            bb.FollowTarget = target;
            bb.Target       = null;    // 따라가기와 교전은 같이 갈 수 없다
            bb.FinishTarget = null;
            bb.ManualMove   = false;
        }
    }

    static Vector3[] CalcFormation(Vector3 center, int count)
    {
        var positions = new Vector3[count];
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        float spacing = 1.2f;

        for (int i = 0; i < count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            float ox = (col - (cols - 1) * 0.5f) * spacing;
            float oz = (row - (Mathf.CeilToInt((float)count / cols) - 1) * 0.5f) * spacing;
            positions[i] = center + new Vector3(ox, 0f, oz);
        }
        return positions;
    }
}

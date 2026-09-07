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

        // 운반 지정 모드에서 우클릭은 **지정을 지우는 것**이다 — 안 막으면 그 클릭이 월드로
        // 새어 이동 명령까지 나간다(지우려 눌렀는데 폰이 그리로 걸어간다).
        // 지정 모드 중에는 우클릭이 **무르기**다 — 이동 명령으로 새면 찍은 것을 지우려다
        // 폰이 벽으로 걸어간다.
        if (HaulDesignator.IsActive || MineDesignator.IsActive) return;

        // **UI 위에서 누른 우클릭은 월드로 새지 않는다.** 창을 열어 둔 채 그 위를 우클릭하면
        // 뒤의 땅으로 이동 명령이 나가거나 뒤에 선 것을 겨눴다 — 배낭·상점을 다루는 내내
        // 발밑을 조심해야 했다. 월드맵(`WorldMapView`)은 이미 `OverUI()`로 막고 있었다.
        //
        // 좌클릭(`SelectionManager._pressOverUI`)처럼 누름/놓음을 짝지어 붙들 필요는 없다.
        // 우클릭은 **누르는 그 프레임에 전부 끝나므로**(드래그가 없다) 누른 곳만 보면 된다.
        // **손에 물건을 들고 있으면 우클릭은 되돌리기다**(`DraggableItem`) — 월드로 안 샌다.
        if (DraggableItem.CarriedInstance != null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 선택지 창이 떠 있으면 이번 우클릭은 **창을 닫는 데만** 쓴다(창 쪽이 닫는다).
        // 여기서도 처리하면 창을 닫으면서 동시에 명령이 나가 버린다.
        if (PawnContextMenuUI.Instance != null && PawnContextMenuUI.Instance.IsOpen) return;
        if (selectionManager.Selected.Count == 0) return;

        // 방랑자만 명령 대상 (하수인은 정보 확인만 가능)
        var wanderers = new List<PawnMover>();
        bool heldOut  = false;   // 갇히거나 업혀서 빠진 폰이 있었나
        foreach (var pawn in selectionManager.Selected)
        {
            if (pawn == null) continue; // 선택 후 파괴된 유닛
            var brain = pawn.GetComponent<UnitBrain>();
            if (brain == null || !brain.AcceptsOrders) continue;

            // **명령을 수행할 수 없는 폰은 명령 목록에서 뺀다.** 갇혔거나 남의 어깨에 실려
            // 있으면 이동은 `PawnMover`가 막고 나머지는 BT가 안 돌아 조용히 사라진다 —
            // 그러면 "시켰는데 아무 일도 안 일어난다"가 된다. 게다가 [치료한다]·[업는다]는
            // **가장 가까운 한 명**을 고르는데, 그 하나가 갇힌 폰이면 아무도 안 간다.
            if (!brain.CanObeyNow) { heldOut = true; continue; }

            wanderers.Add(pawn);
        }

        // **고른 폰이 전부 갇혀 있어도 창은 연다.** [풀어 준다]처럼 **명령 받는 폰이 필요 없는**
        // 선택지가 있는데, 여기서 막으면 마지막 한 명을 가둔 순간 풀어 줄 길이 사라진다.
        // 명령이 필요한 줄들은 `Nearest`가 null을 돌려줘 저절로 안 붙는다.
        // (하수인만 고른 경우는 예전 그대로 조용히 끝낸다 — 그건 원래 명령 대상이 아니다.)
        if (wanderers.Count == 0 && !heldOut) return;

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
    //
    // **업혀 있는 것은 건너뛴다 — 그건 겨눌 것이 아니라 짐이다.** 어깨 위에 붙어 있어서
    // 업은 폰보다 커서에 먼저 걸리는데, 그러면 업은 폰을 누를 방법이 사라진다: 시체를 메고
    // 있는 폰을 우클릭하면 **[내려놓는다] 대신 그 시체의 [뒤진다]·[도축한다]가 떴다.**
    // 문서가 *"업힌 쪽을 때리면 업은 폰을 노리는 것과 구별이 안 된다 — 어깨 위라 사거리가
    // 같다"*고 적어 둔 그것이 겨냥에서도 그대로 성립한다.
    static bool PickTarget(Ray ray, List<PawnMover> commanders, out RaycastHit hit)
        => SelectionManager.PickVisible(ray, out hit);

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

            // 업은 사람을 어디에 놓으라던 것도 거둔다. 안 거두면 시킨 자리까지 걸어간 다음
            // **저 혼자 예전 우리로 다시 걸어간다** — 명령을 덮어썼는데 옛 명령이 살아남는다.
            bb.HasPutDown   = false;
            bb.PutDownSite  = null;
            bb.EscortTo     = EscortDest.None;   // 데려가던 것도 거둔다(간수가 켜져 있으면 다시 잡는다)

            // **소집 중이면 배정은 남긴다.** 평소 이동 명령이 배정까지 거두는 이유는
            // *"안 거두면 도착하자마자 일하러 돌아가 명령이 안 먹는 것처럼 보인다"*인데,
            // **소집이 이미 `WorkNode`를 막고 있으므로 그 이유가 없다.** 거두면 진형을 잡으려고
            // 한 번 옮겼을 뿐인데 캐던 바위가 떨어져 나가고, 소집을 풀어도 안 돌아간다.
            if (brain.IsDrafted) brain.DropCurrentJob();
            else                 brain.AbandonWork();

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

    // 창 제목 = **`UnitBrain.DisplayTitle`**(`던컨 (떠돌이 · 변방민)`).
    //
    // 이름만 띄우면 고블린이 셋일 때 무엇인지를 모르고, 딸림말만 띄우면 셋 다 "고블린"이라
    // 개체가 구별되지 않는다. 그리고 **대화창과 같은 것을 써야 한다** — 창마다 따로 만들면
    // 같은 폰이 우클릭에선 `(인간)`, 대화에선 `(떠돌이)`로 불린다.
    static string PawnTitle(UnitBrain brain, GameObject go) => brain.DisplayTitle;

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
        var options = new List<PawnMenuOption>();
        string title = "이곳";

        var brain   = go.GetComponent<UnitBrain>();
        var node    = go.GetComponent<ResourceNode>();
        var drop    = go.GetComponent<DroppedItem>();
        var corpse  = go.GetComponent<Corpse>();
        var core    = go.GetComponent<DungeonCore>();
        var clickable = go.GetComponent<IWorldClickable>();

        // **누운 폰은 여기서 그냥 잡힌다** — `PawnMover`가 눕는 동안 트리거 캡슐을 켜 둔다.
        // 몸통 크기라 침대보다 작으므로, **몸을 겨누면 사람이 침대를 겨누면 침대가** 눌린다.
        // 한때 "침대를 누르면 누운 사람"으로 우회했는데 그러면 **침대 자체를 누를 길이 없다.**

        // **시체를 먼저 본다.** 시체도 `UnitBrain`을 그대로 갖고 있어서(`BecomeCorpse`는
        // 컴포넌트를 끄기만 한다) 아래 `brain != null`이 먼저 걸렸고, 그래서 시체 가지는
        // **한 번도 안 도는 죽은 코드**였다 — 우클릭하면 [뒤진다] 대신 `따라간다`·`공격한다`가
        // 떴다(따라갈 수도 칠 수도 없는 것에게). 죽은 것은 무엇보다 먼저 시체다.
        if (corpse != null)
        {
            // 이름을 적는다 — 시체가 여럿 쌓이면 "시체"만으로는 어느 것을 눌렀는지 모른다
            // (산 폰의 창 제목과 같은 규약).
            title = string.IsNullOrWhiteSpace(corpse.UnitName) ? "시체" : $"{corpse.UnitName}의 시체";

            // **얼마나 삭았는지 제목에 적는다** — 무엇이 나올지가 그것으로 갈린다.
            title += $" ({Butchering.StageName(corpse.Stage)})";

            AddCorpseOptions(corpse, go, brain, commanders, options);

            // **업어서 치운다.** 산 사람을 옮기는 것과 같은 동작이라 같은 길을 쓴다
            // (무게도 한 손 제약도 그대로 걸린다).
            AddCarryOption(brain, commanders, options);
        }
        else if (brain != null)
        {
            title = PawnTitle(brain, go);
            bool down = IsDown(go);

            // **명령 받는 폰 자신을 눌렀는가.** 자기에게 시킬 수 있는 일은 치료뿐이다 —
            // 자기를 따라가거나 자기를 칠 수는 없다(그 줄이 뜬 것이 한때의 버그였다).
            bool self = IsCommander(brain, commanders);

            // **갇힌 사람에게 할 수 있는 일은 정해져 있다** — 끌어내거나, 돌보거나, 죽이거나.
            // 붙잡아 둔 사람을 "따라간다"·"거래한다"로 대할 수는 없는데, 회복해 눈을 뜨면
            // 그 줄들이 그대로 떴다(갇힘이 아니라 기절 여부로만 갈랐기 때문이다).
            bool held  = brain.Blackboard != null && brain.Blackboard.IsPrisoner;
            // 풀려나 떠나는 중 — 자동 표적에서 빠져 있으므로 칠 때도 지목해야 한다.
            bool going = brain.Blackboard != null && brain.Blackboard.LeavingMap;

            // **말을 건다** — 상인·중립·붙잡아 둔 자까지 **한 줄로** 받는다.
            //
            // 무슨 말을 할 수 있는지는 `Conversation`이 정한다(상인이면 물건을, 떠돌이면 합류를,
            // 갇힌 자에게는 넘어오라고). 여기서 상대 종류를 따지기 시작하면 판단이 두 곳으로
            // 갈라진다 — 우클릭 창이 늘 그래 왔듯 **구성은 여는 쪽이, 그리기는 창이** 한다.
            //
            // **거래 줄은 그대로 둔다**(아래). 물건을 사는 것은 가장 자주 하는 일이라 한 단계를
            // 더 얹으면 분위기가 아니라 손해다 — 대화는 *다른 길*이지 대체가 아니다.
            var speaker = Nearest(commanders, go.transform.position, b => b != brain);
            if (Conversation.CanTalk(brain, speaker) && DialogueUI.Instance != null)
                options.Add(("말을 건다", () => Conversation.Open(brain, speaker)));

            // 거래는 상인일 때만. `Merchant`는 상인 역할이면 자동으로 붙는다.
            // 쓰러진 상인은 선택지에서 뺀다 — 눌러 봐야 아무 일도 안 일어난다.
            var merchant = go.GetComponent<Merchant>();
            if (!self && !held && merchant != null && merchant.CanTrade)
                options.Add(("거래한다", merchant.OnWorldClick));

            // 누운 것을, 그리고 갇힌 것을 따라다닐 수는 없다.
            if (!self && !down && !held) options.Add(("따라간다", () => FollowWith(commanders, go.transform)));

            // **치료 — 침대에 눕히지 않아도 된다.** 전투 직후 쓰러진 사람을 그 자리에서 돌보는
            // 것이 침대까지 끌고 가는 것보다 급하다. 자기 자신도 된다(그때는 스스로 싸맨다).
            AddTreatOption(brain, go, commanders, self, options);

            // **업고 있는 폰을 우클릭하면 내려놓는다.** 맨바닥에 두면 이동이 두 번 누르기가
            // 되므로, 짐을 진 당사자에게 묻는다. 고른 폰이 아니어도 뜬다 — 내려놓는 것은
            // 그 폰이 하는 일이라 "누구에게 시키나"가 애매할 여지가 없다.
            AddSelfCarryOptions(brain, commanders, options);

            // **업기.** 적이면 붙잡아 오는 것이고 아군이면 옮기는 것인데 동작은 같다.
            // 우리 편은 멀쩡해도 업고, 남은 기절해야 업는다(`PawnCarry.CanCarry`).
            if (!self) AddCarryOption(brain, commanders, options);

            // **업기와 데려가기를 한 동작으로.** 쓰러뜨린 적을 가둘 때 [업는다] → 걸어가서
            // 우리 우클릭 → [여기 가둔다]로 세 번 누르게 하면, 가장 잦은 일이 가장 번거롭다.
            if (!self) AddEscortOptions(brain, commanders, options);

            // **쓰러졌거나 갇혔으면 뒤져 볼 수 있다.** 무엇을 지녔는지가 가둘지·몸값을
            // 부를지·그냥 죽일지를 정하는 재료라, 그 판단보다 **먼저** 볼 수 있어야 한다
            // (예전엔 갇힌 사람에게만 떠서 일단 업어 오는 수밖에 없었다).
            if ((down || held) && !self) AddSearchOption(brain, options);

            // **갇힌 사람에게 직접 묻는다 — 먹이기와 석방.**
            //
            // 우리·침대를 눌러야만 풀어 줄 수 있으면 "누구를 푸는가"가 자리로 표현된다.
            // 사람을 보고 정하는 일이라 사람에게 물어야 한다.
            if (held) AddPrisonerOptions(brain, commanders, options);

            // 아군(플레이어 세력)까지 칠 이유는 없다 — 오사 사고만 난다.
            var faction = go.GetComponent<Faction>();
            if (!self && (faction == null || !faction.IsPlayer))
            {
                // **문구는 하나다 — 쓰러진 놈에게도 [공격한다].** 하는 일이 다르긴 하지만
                // (지목해야만 뚫리는 예외를 켠다) 사람이 뜻하는 바는 같고, 상태마다 다른 낱말을
                // 쓰면 [마무리한다]가 별개의 동작인 줄 알게 된다. 갈림은 안에서 처리한다.
                // **갇힌 사람도 쓰러진 사람과 같은 길**(`FinishWith`) — 자동 표적에서 빠져
                // 있으므로 지목해야만 뚫린다. 처형하는 길이 아예 없으면 붙잡은 뒤에 할 수
                // 있는 일이 줄어들 뿐이고, 자동으로 뚫리면 붙잡아 온 뜻이 사라진다.
                System.Action attack = (down || held || going) ? () => FinishWith(commanders, go)
                                                               : () => AttackWith(commanders, go);
                options.Add(("공격한다", attack));
            }

            // **쓰러진 것은 그 자리에서 가른다** — 때려눕히고 손질하는 것이 사냥의 정상
            // 경로이고, 죽여 놓고 시체가 삭기를 기다릴 이유가 없다. 조건은
            // `Butchering.CanButcherAlive`가 답한다(쓰러졌을 것 · 내 편이 아닐 것).
            AddButcherOption(go, commanders, options);
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

            // **야생은 배정이 아니라 지정이다**(2026-09-06). 그전에는 여기서 `Assign`을
            // 불렀는데 야생이 그것을 거절하게 된 뒤로 **아무 일도 안 일어나면서 "자리가
            // 모자란다"는 거짓말**이 찍혔다(자리는 비어 있었다).
            //
            // **누르는 사람에게는 하는 일이 같다** — *"여기서 캔다"*. 갈리는 것은 그 뒤이고
            // (배정이냐 큐냐) 그건 노드가 안다.
            if (node.CanDesignate)
            {
                // 야생 — 지정이다. **자리 수는 안 적는다**(배정이 아니므로 뜻이 없다).
                options.Add((node.Designated ? $"{verb} - 지정 취소" : verb,
                             () => node.SetDesignated(!node.Designated)));
            }
            else if (node.IsFull)
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

            // **자동 운반은 물건마다 켜고 끈다** — 기본은 꺼짐이고, 켠 것에는 표식이 뜬다.
            // **양방향이어야 한다**: 예전엔 켜는 길만 있어서, 저절로 실려 가는 물건을
            // "여기 둬라"라고 할 방법이 없었다(그게 규칙을 고칠 수 없는 것으로 보이게 했다).
            options.Add(drop.HaulAllowed
                ? ("여기 둔다",     () => drop.SetHaulAllowed(false))
                : ("창고로 옮긴다", () => drop.SetHaulAllowed(true)));
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
            //
            // **침대도 뺀다.** `Bed.OnWorldClick`은 일부러 비어 있다(전용 패널을 없앴고, 침대로
            // 하는 일은 전부 아래 [여기 눕는다]·[치료한다]…에 있다). 눌러도 아무 일이 안
            // 일어나는 줄은 "먹혔나?"를 남길 뿐이고, 침대가 무엇인지는 좌클릭이 답한다.
            // **화톳불도 뺀다** — `Campfire.OnWorldClick`은 침대와 같은 이유로 비어 있다
            // (조작은 아래 [불을 끈다] 한 줄뿐이고, 무엇이 얼마나 남았는지는 좌클릭이 답한다).
            bool opensSomething = go.GetComponent<Bed>() == null
                                  && go.GetComponentInParent<Campfire>() == null;
            if (opensSomething && !BuildingDormancy.IsDormantAt(hit.collider))
                options.Add((verb, clickable.OnWorldClick));

            // **침대는 명령을 받는 물건이다.** 눕히는 것도 치료를 시키는 것도 고른 폰에게
            // 내리는 지시라, 패널을 열어 배정하는 것보다 우클릭이 자연스럽다.
            var bed = go.GetComponent<Bed>();
            if (bed != null) AddBedOptions(bed, commanders, options);

            var cage = go.GetComponentInParent<PrisonCage>();
            if (cage != null) AddCageOptions(cage, commanders, options);

            var butcher = go.GetComponentInParent<ButcherTable>();
            if (butcher != null) AddButcherTableOptions(butcher, commanders, options);

            var grave = go.GetComponentInParent<Grave>();
            if (grave != null) AddGraveOptions(grave, commanders, options);

            var pyre = go.GetComponentInParent<Pyre>();
            if (pyre != null) AddPyreOptions(pyre, commanders, options);

            var fire = go.GetComponentInParent<Campfire>();
            if (fire != null) AddCampfireOptions(fire, options);
        }

        // **청사진은 콜라이더가 없어 여기까지 걸리지 않는다.**
        //
        // 고스트는 콜라이더를 일부러 다 꺼 두었다 — 청사진은 통행을 안 막아야 하고, 켜면
        // 선택·이동 명령의 레이캐스트에 걸려 그 위로 폰을 보낼 수가 없다. 그래서 지금까지
        // **건설 모드에 들어가지 않으면 잘못 놓은 청사진을 물릴 길이 없었다.**
        //
        // 바닥 지점을 타일로 바꿔 그 칸의 청사진을 찾는다(철거 모드의 픽킹과 같은 방식).
        // **아무것도 안 걸렸을 때만 본다** — 그 위에 폰이 서 있으면 폰이 이긴다(폰에게 시킬
        // 일을 물으려고 누른 것이고, 청사진은 폰이 비켜난 뒤에 물려도 된다).
        if (options.Count == 0 && hasGround && MapManager.Instance != null)
        {
            var bp = BuildingBlueprint.PickAt(MapManager.Instance.WorldToTile(groundHit.point));
            if (bp != null)
            {
                title = BlueprintTitle(bp);
                options.Add(("건설을 취소한다", bp.Cancel));
            }
        }

        // **맨바닥에서는 내려놓기를 안 묻는다.** 사람을 업었다고 이동이 두 번 누르기가 되면
        // 안 된다 — 업고 다니는 동안 가장 잦은 명령은 여전히 이동이다.
        // 내려놓기는 **업고 있는 그 폰을 우클릭**하면 나온다(`AddSelfCarryOptions`) —
        // 어깨 위의 것으로 할 수 있는 일도 **같은 창에** 딸려 온다(뒤진다·도축·매장…).
        // 업힌 몸은 겨냥에서 빠지므로(`SelectionManager.PickVisible`) **어느 쪽을 눌러도
        // 같은 창**이고, 업고 있다는 이유로 할 수 있는 일이 줄지 않는다.
        //
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

    // 창 제목에 **진행을 같이 적는다** — 취소할지 말지는 얼마나 들어갔는지가 정한다.
    // 다 들어간 것을 물리는 것과 하나도 안 들어간 것을 물리는 것은 다른 결정이다.
    public static string BlueprintTitle(BuildingBlueprint bp)
    {
        string name = bp.Data != null ? bp.Data.BuildingName : "청사진";
        return bp.CostItem != null
            ? $"{name} (청사진 {bp.Delivered}/{bp.CostRequired})"
            : $"{name} (청사진)";
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
                               bool self, List<PawnMenuOption> options)
    {
        var body = go.GetComponent<BodyPartSystem>();
        if (body == null || !body.NeedsTreatment) return;

        // **우리 편이거나, 붙잡아 둔 사람이거나.**
        //
        // 예전 주석은 "적을 살려 줄 길(포로 치료)은 아직 없다 — 생기면 그때 여기서 갈라야
        // 한다"였다. 이제 있다: 죄수 침대에 눕히면 `Bed.Update`가 치료 일감을 낸다.
        // 그런데 그쪽만 갈라 놓고 여기는 안 갈라서, **침대에 눕힌 포로는 저절로 치료받는데
        // 우리에 가둔 포로는 우클릭으로도 손을 못 대는** 상태였다.
        //
        // 지나가던 적은 여전히 안 돌본다 — 갇힌 사람만이다(`Bed.Update`와 같은 기준).
        var faction = go.GetComponent<Faction>();
        bool ours   = faction != null && faction.IsPlayer;
        bool held   = patient.Blackboard != null && patient.Blackboard.IsPrisoner;
        if (!self && !ours && !held) return;

        // **돌볼 수 있는 폰 중 가장 가까운 하나.** 그냥 가장 가까운 폰을 고르고 나서 기절을
        // 확인하면, 제일 가까운 폰이 쓰러져 있을 때 선택지가 통째로 사라진다 — 뒤에 멀쩡한
        // 폰이 서 있어도([줍는다]·[업는다]와 같은 규칙).
        var doctor = self ? patient
                          : Nearest(commanders, go.transform.position,
                                    b => b.Blackboard == null || !b.Blackboard.IsKnockedOut);
        if (doctor == null) return;

        // 기절한 폰은 자기도 못 돌본다 — 손이 안 움직인다.
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

            // **갇힌 사람은 안 일으킨다.** 침대에서 놓아 주기만 하면 `IsPrisoner`가 남아
            // 자리 없이 갇힌 폰이 된다 — 죄수 침대에 [배정]을 누를 수 있던 것과 같은 누수다.
            // 풀어 주는 길은 [풀어 준다]와 [끌어낸다] 둘뿐이다.
            if (brain.Blackboard.IsPrisoner) continue;

            // **무엇을 거둘지는 침대가 답한다**(`Bed.RouseFrom`) — 손으로 붙인 배정만
            // 거두고, 저 혼자 잡은 잠자리와 소집 중인 폰의 배정은 남긴다. 여기서 그냥
            // `ReleaseFrom`을 부르면 **한 번 옮겼다는 이유로 잠자리를 잃는다.**
            Bed.RouseFrom(brain);
            pawn.SetSleeping(false);
        }
    }

    // 침대 우클릭 — **눕는다 / 치료한다.**
    //
    // 둘 다 "가장 가까운 한 명"에게 간다. 침대는 하나뿐이라 여럿을 눕힐 수 없고, 치료도
    // 한 사람이면 족하다 — 여럿을 보내면 먼저 닿은 하나만 일하고 나머지는 헛걸음한다
    // ([줍는다]와 같은 규칙).
    // **업으라고 시킨다.** 한 명만 보낸다 — 사람 하나를 여럿에게 시키면 먼저 닿은 하나가
    // 업고 나머지는 헛걸음한다(`PickUpWith`와 같은 규칙).
    //
    // **누구를 보낼지는 거리로 정한다.** 무게로 거르지 않는다: 사람은 누구든 업을 수 있고,
    // 힘이 모자라면 못 드는 게 아니라 **느려질 뿐**이다 — 그건 플레이어가 보고 판단할 일이지
    // 명령이 조용히 거절할 일이 아니다(손으로 배낭에 넣는 것과 같은 축).
    // 업고 있는 폰 자신에게 묻는 것 — 그 자리에 내려놓는다. **걸어갈 필요가 없으므로
    // 일감을 걸지 않고 즉시 처리한다**(BT를 거치면 다음 틱까지 아무 일도 안 일어난 것처럼 보인다).
    // **시체 하나로 할 수 있는 일.** 바닥에 있든 어깨 위에 있든 **같은 목록**이다 —
    // 그래서 함수가 하나다. 갈라 두면 업고 있는 동안에는 뒤지지도 못하게 된다.
    static void AddCorpseOptions(Corpse corpse, GameObject go, UnitBrain brain,
                                 List<PawnMover> commanders, List<PawnMenuOption> options)
    {
        options.Add(("뒤진다", () => SelectionManager.InspectCorpse(corpse)));

        AddButcherOption(go, commanders, options);

        // **묻거나 태운다 — 업기와 데려가기가 한 동작.** [업는다] → 걸어가서 무덤
        // 우클릭 → [여기 묻는다]로 세 번 누르게 하면 가장 잦은 뒷정리가 가장 번거롭다
        // (붙잡은 사람의 [우리로 옮긴다]와 같은 판단).
        AddFuneralOptions(brain, commanders, options);
    }

    // 업고 있는 폰을 우클릭했을 때 — **어깨 위의 것까지 같이 묻는다.**
    //
    // 업힌 몸은 업은 폰의 자식이라 어깨 높이에 붙어 커서에 먼저 걸리는데, 그래서 겨냥에서는
    // 뺐다(`SelectionManager.PickVisible`). 그렇다고 **할 수 있는 일까지 사라지면 안 된다** —
    // 업고 있는 동안 뒤지지도 묻지도 못하게 되고, 그러려고 일부러 내려놓았다 다시 업게 된다.
    //
    // **어느 쪽을 눌러도 같은 창이 뜬다**: 업은 폰을 누르든 어깨의 몸을 누르든 목록이 하나다.
    static void AddSelfCarryOptions(UnitBrain brain, List<PawnMover> commanders,
                                    List<PawnMenuOption> options)
    {
        var carried = brain?.Blackboard?.Carrying;
        if (carried == null) return;

        var faction = brain.GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer) return;

        options.Add(($"{carried.UnitName}을(를) 내려놓는다", () =>
        {
            // 걸어가서 놓으려던 명령이 남아 있으면 지운다 — 여기서 이미 내려놓았다.
            brain.Blackboard.HasPutDown  = false;
            brain.Blackboard.PutDownSite = null;
            PawnCarry.PutDown(brain);
        }));

        // 어깨 위의 것으로 할 수 있는 일 — **바닥에 있을 때와 같은 목록**이다.
        var carriedCorpse = carried.GetComponent<Corpse>();
        if (carriedCorpse != null)
        {
            AddCorpseOptions(carriedCorpse, carried.gameObject, carried, commanders, options);
            return;
        }

        // 산 사람을 업고 있다 — 어깨 위의 사람도 뒤져 볼 수 있다(업고 걸어가는 동안
        // 무엇을 지녔는지 보고 어디로 데려갈지 정하는 것이 실제 흐름이다).
        AddSearchOption(carried, options);

        // 붙잡아 온 사람이면 먹이고 놓아 줄 수도 있다.
        if (carried.Blackboard != null && PrisonEscort.IsCaptive(carried))
            AddPrisonerOptions(carried, commanders, options);

        AddEscortOptions(carried, commanders, options);

        // **쓰러진 것은 업고 있어도 가른다.** 바닥에 두면 `[도축한다]`가 뜨는데
        // 어깨에 얹었다고 사라지면, 사냥한 짐승을 도축대까지 메고 와 놓고 **다시 내려놓아야**
        // 그 줄이 돌아온다. 판정은 `Butchering.CanButcherAlive` 하나라 여기서도 같은 답이다.
        AddButcherOption(carried.gameObject, commanders, options);
    }

    // **쓰러져 있으면 뒤질 수 있다 — 갇혀 있지 않아도.**
    //
    // 한때 이 줄은 갇힌 사람에게만 떴다. 그래서 **때려눕힌 적이 무엇을 지녔는지 보려면 일단
    // 업어야 했고**(업고 있는 폰의 창에는 이 줄이 있다), 우리에 넣기 전에는 알 수가 없었다 —
    // 그런데 *무엇을 지녔는가*는 대개 **가둘지 몸값을 부를지 그냥 죽일지**를 정하는 재료다.
    // 판단에 쓰는 정보를 판단한 뒤에야 볼 수 있으면 순서가 뒤집힌 것이다.
    //
    // **뒤지는 것이 아니라 보는 것이다.** 시체는 [뒤진다]로 꺼내 오지만 산 사람의 배낭은
    // 그 사람 것이고, 창을 열면 옮길 수 있다 — 말을 다르게 두는 편이 무슨 일이 일어나는지
    // 정확하다. 창은 원정 나간 폰의 배낭을 여는 것과 **같은 통로**(`BindTo`)를 쓴다.
    //
    // **멀쩡히 서 있는 사람은 안 된다** — 그건 뒤지는 것이 아니라 소매치기이고, 저쪽이
    // 가만히 있을 이유가 없다. 쓰러졌거나(`down`) 갇혔을 때(`held`)만이다.
    static void AddSearchOption(UnitBrain pawn, List<PawnMenuOption> options)
    {
        var inv = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (inv == null || pawn.Inventory == null) return;

        options.Add(("소지품을 본다", () =>
        {
            inv.BindTo(pawn);
            inv.ForceOpen();   // 묶기만 하면 창이 닫힌 채로 바뀌어 아무 일도 안 한 것처럼 보인다
        }));
    }

    // **명령 받을 폰이 필요 없다.** 놓아 주는 것은 우리·침대가 하는 일이라, 마지막 한 명을
    // 가둔 뒤에도 뜬다.
    static void AddPrisonerOptions(UnitBrain prisoner, List<PawnMover> commanders,
                                   List<PawnMenuOption> options)
    {
        // **먹인다** — 갇힌 사람은 제 발로 못 먹으러 간다.
        //
        // 간수를 켜 뒀으면 자리(`PrisonFeeding.Tick`)가 저절로 일감을 내지만, 꺼 뒀거나
        // 지금 당장 먹여야 할 때의 길이 있어야 한다(자원 노드 직접 배정·[치료한다]와 같은
        // 규칙 — 손으로 시킨 일은 작업 설정을 안 본다). **일감은 자리가 들고 있는 것을
        // 넘긴다**(`RequestBy`) — 누를 때마다 만들면 한 사람에게 여럿이 붙는다.
        if (PrisonFeeding.NeedsFood(prisoner))
        {
            float hunger = prisoner.Blackboard.Hunger;

            // **먹일 것이 없으면 그렇게 적는다.** 눌러도 안 되는 줄을 그대로 두면 "왜 안
            // 가지"가 되고, 아예 빼면 허기를 볼 자리가 사라진다(설득의 쿨타임 표시와 같은 꼴).
            options.Add(!Feeding.ExistsAnywhere()
                ? ($"먹인다 (허기 {hunger:F0}) - 음식 없음", (System.Action)null)
                : ($"먹인다 (허기 {hunger:F0})", (System.Action)(() =>
                {
                    var feeder = Nearest(commanders, prisoner.transform.position,
                                         b => b != prisoner
                                           && (b.Blackboard == null || !b.Blackboard.IsKnockedOut));
                    if (feeder == null) return;

                    feeder.AbandonWork();   // 하던 일을 놓아야 이 일감을 집는다
                    PrisonFeeding.RequestBy(prisoner, feeder);
                })));
        }

        // **지배는 도박이라 사람이 눌러야 한다.** 게이지가 다 차도 저절로 넘어오지 않는다 —
        // 확률을 적어 두고 누를지 말지를 맡긴다(실패하면 그 자리에서 죽는다).
        if (Conversion.CanDominateNow(prisoner))
        {
            int pct = Mathf.RoundToInt(Conversion.DominateChance() * 100f);
            options.Add(($"지배를 시도한다 ({pct}% — 실패하면 죽는다)",
                         () => Conversion.TryDominate(prisoner)));
        }

        // **말이 통하는 상대에게만 사람을 보낸다.** 하수인 종족은 던전 핵이 지배하므로
        // (`Conversion.TickCore`) 폰이 갈 일이 없다 — 그쪽엔 이 줄이 안 뜬다.
        //
        // **일감을 새로 만들지 않고 우리에게 맡긴다**(`RequestPersuadeBy` — 침대의
        // `RequestTreatBy`와 같은 규약). 누를 때마다 만들면 여럿이 동시에 붙어 게이지가
        // 사람 수만큼 빨리 찼다 — 자리가 하나면 일감도 하나다.
        if (Conversion.CanPersuade(prisoner))
        {
            int pct = Mathf.RoundToInt(prisoner.Blackboard.ConvertProgress * 100f);

            // **쉬는 중이면 그렇게 적는다.** 눌러도 안 되는 줄을 그대로 두면 "왜 안 가지"가
            // 되고, 아예 빼면 진행도(%)를 볼 자리가 사라진다.
            float wait = Conversion.PersuadeCooldownLeft(prisoner);

            options.Add(wait > 0f
                ? ($"설득한다 ({pct}% — {Mathf.CeilToInt(wait)}초 뒤)", (System.Action)null)
                : ($"설득한다 ({pct}%)", (System.Action)(() =>
                {
                    var cage = PrisonCage.HolderOf(prisoner);
                    if (cage == null) return;

                    var speaker = Nearest(commanders, prisoner.transform.position,
                                          b => b != prisoner
                                            && (b.Blackboard == null || !b.Blackboard.IsKnockedOut));
                    if (speaker == null) return;

                    speaker.AbandonWork();   // 하던 일을 놓아야 이 일감을 집는다
                    cage.RequestPersuadeBy(speaker);
                })));
        }

        // **돈을 받고 넘긴다.** 붙잡은 사람으로 할 수 있는 일이 회유·처형·풀어 주기뿐이면
        // 말이 통하는 상대를 붙잡는 것과 안 통하는 상대를 붙잡는 것이 같은 값이 된다.
        //
        // **안 되는 상대에게도 줄은 남긴다 — 흐리게.** 빼 버리면 "이 세력은 원래 몸값이라는
        // 게 없나" 와 구별이 안 되고, 그렇다고 창에 사유를 적으면 작은 팝업이 부푼다.
        // 그래서 **못 누르는 색으로 두고 이유는 올려 봤을 때** 뜬다(`PawnMenuOption.Blocked`).
        if (PrisonEscort.IsCaptive(prisoner))
        {
            // **얼마인지 적는다** — 넘길지는 액수가 정한다([업는다]에 무게를 적는 것과 같은 규약).
            //
            // **흥정은 그 일을 하러 간 폰이 한다**(`Trading.HagglerFor`).
            //
            // **대화창과 같은 사람을 고른다** — `Nearest(commanders, 대상)`는 [말을 건다]가
            // `speaker`를 뽑는 것과 같은 식이다. 여기만 "맵 최고"로 두면 **맵 최고 ≥ 말을 건
            // 폰**이 항상 참이라, 대화창의 `[네 쪽에 돌려보내 주지]`가 영영 고를 이유가 없는
            // 줄이 된다(값을 받으려면 창을 닫고 우클릭하는 것이 언제나 낫다).
            //
            // 고른 폰이 하나도 없으면(마지막 한 명까지 가둔 경우) `HagglerFor`가 맵 최고로
            // 떨어뜨린다. 액수를 띄울 때와 치를 때 **같은 사람을 넘긴다**.
            // 쓰러진 폰은 뺀다 — 설득 `speaker`·`Trading.BestOf`와 같은 규칙이다.
            var haggler = Trading.HagglerFor(
                Nearest(commanders, prisoner.transform.position,
                        b => b != prisoner && (b.Blackboard == null || !b.Blackboard.IsKnockedOut)));
            int payout  = WorldRansom.PayoutFor(prisoner, haggler);

            if (WorldRansom.CanSell(prisoner))
                options.Add(($"몸값을 받고 넘긴다 ({payout:N0} G)",
                             () => WorldRansom.Sell(prisoner, haggler)));
            else
            {
                var fd  = prisoner.GetComponent<Faction>()?.Data;
                string who = fd != null ? fd.FactionName : "이쪽";
                options.Add(PawnMenuOption.Blocked("몸값을 받고 넘긴다",
                    $"{who}과는 말이 통하지 않는다.\n돈을 주고 데려가지 않으므로 회유하거나, " +
                    "풀어 주거나, 처형하는 수밖에 없다."));
            }
        }

        options.Add(("풀어 준다", () => Imprisonment.Release(prisoner)));
    }

    // **[도축한다]** — 시체를 갈라 쓸 것을 꺼낸다(그리고 그 자리를 치운다).
    //
    // 큐를 안 타는 일감이라 여기서 **그 폰 앞으로 예약해** 보낸다(`ReserveFor` — `[치료한다]`와
    // 같은 길). 손으로 시킨 일은 작업 설정을 안 본다.
    // 산 채로 쓰러진 것에도 붙는다 — 판정은 `Butchering.CanButcherAlive` 하나가 한다.
    static void AddButcherOption(GameObject target, List<PawnMover> commanders,
                                 List<PawnMenuOption> options)
    {
        if (target == null) return;

        var corpse = target.GetComponent<Corpse>();
        var brain  = target.GetComponent<UnitBrain>();

        // 시체가 아니면 **쓰러진 것**이어야 한다(그리고 내 편이 아니어야 한다).
        if (corpse == null && !Butchering.CanButcherAlive(brain)) return;

        var race  = brain != null ? brain.Data : null;
        var stage = corpse != null ? corpse.Stage : CorpseStage.Fresh;

        // **나올 것이 없으면 줄을 흐리게 남긴다** — 빼 버리면 "이 시체는 원래 도축이 안 되나"와
        // "지금은 안 되나"가 구별이 안 된다(몸값 줄과 같은 규약).
        if (!Butchering.HasYield(race, stage))
        {
            options.Add(PawnMenuOption.Blocked("도축한다", "여기서 나올 것이 없다"));
            return;
        }

        // **자기 자신은 못 시킨다** — 쓰러진 폰을 고른 채 그 폰을 누르는 경우.
        var worker = Nearest(commanders, target.transform.position, b => b.gameObject != target);
        if (worker == null) return;

        var doer = worker.GetComponent<UnitBrain>();
        if (doer == null) return;

        // **문구는 하나다 — 산 것에도 `[도축한다]`.** 상태마다 낱말을 나누면 그것이 별개의
        // 동작인 줄 알게 된다(`[공격한다]`가 쓰러진 놈에게도 같은 낱말인 것과 같은 규칙).
        // 되돌릴 수 없다는 것은 **일이 시작되는 순간**이 말해 준다 — 산 것은 칼을 대는
        // 그때 죽는다.
        const string label = "도축한다";

        // **도축대가 있으면 거기로 데려간다.** 손으로 시킨 것도 마찬가지다 — 도축대를 지어
        // 놓고도 폰이 시체 옆에 쭈그려 앉아 가르면 그 건물을 지은 뜻이 없고, 산출물도
        // 도축대에 쌓이지 않아 창고로 실려 가지 않는다(바닥·배낭으로 흩어진다).
        //
        // **없으면 그 자리에서 가른다** — 도축대를 짓기 전에도 사냥한 것을 손질할 수 있어야
        // 한다(가죽이 첫 방어구의 재료다).
        var table = ButcherTable.NearestTo(target.transform.position);

        options.Add((label, () =>
        {
            // **있던 일감을 넘긴다 — 새로 만들지 않는다.** 도축대 규칙이 이미 같은 몸에
            // 하나를 걸어 뒀을 수 있고, 그때 하나 더 만들면 **두 폰이 같은 시체로 걸어간다**
            // (`[치료한다]`가 `RequestTreatBy`로 넘기는 것과 같은 규약). **하고 있으면 안
            // 뺏는다** — 가서 보니 이미 갈라져 있는 헛걸음이 된다.
            var mgr = JobManager.Instance;
            if (mgr == null) return;

            var existing = mgr.FindButcherJobFor(target);
            if (existing != null)
            {
                if (existing.Status == JobStatus.InProgress) return;
                existing.ReleaseReservation();
                existing.Reserve(doer);
                return;
            }

            mgr.ReserveFor(new ButcherJob(target, table), doer);
        }));
    }

    static void AddCarryOption(UnitBrain target, List<PawnMover> commanders,
                               List<PawnMenuOption> options)
    {
        if (target == null || target.Blackboard == null) return;
        if (PawnCarry.IsCarried(target)) return;

        // **업을 수 있는 폰 중 가장 가까운 하나.** 전에는 그냥 가장 가까운 폰을 고르고 나서
        // 업을 수 있는지 물었는데, 그러면 **제일 가까운 폰이 이미 누굴 업고 있을 때 선택지가
        // 통째로 사라진다** — 뒤에 멀쩡한 폰이 서 있어도. [줍는다]는 처음부터 "들 수 있는 폰 중
        // 가장 가까운 하나"였으니 여기만 규칙이 달랐던 셈이다.
        var carrier = Nearest(commanders, target.transform.position,
                              b => PawnCarry.CanCarry(b, target));
        if (carrier == null) return;

        // 몇 kg인지 적는다 — 업을지 말지는 그 폰이 얼마나 느려지느냐가 정한다.
        float w   = PawnCarry.WeightOf(target);
        float cap = CarryCapacity.For(carrier);
        string tail = cap > 0f && w > cap ? $" ({w:F0}kg — 버겁다)" : $" ({w:F0}kg)";

        // **갇힌 사람도 업는다** — 다른 우리로 옮기거나, 회유하려고 침대로 데려가거나,
        // 그냥 끌어내거나. 가둔 것을 되돌리는 길이 [풀어 준다] 하나뿐이면 자리를 못 바꾼다.
        // 우리·침대에서 놓아 주는 것은 `PawnCarry.TryPickUp`이 한다.
        string verb = target.Blackboard.IsPrisoner ? "끌어낸다" : "업는다";

        options.Add(($"{verb}{tail}", () =>
        {
            foreach (var pawn in commanders)
            {
                var b = pawn.GetComponent<UnitBrain>();
                if (b == null || b.Blackboard == null) continue;
                b.Blackboard.CarryTarget = b == carrier ? target : null;
            }

            // **[업는다]는 업기만이다.** 아직 안 이룬 [우리로 옮긴다]가 남아 있으면 업자마자
            // 저 혼자 우리로 걸어간다 — 시킨 것과 다른 일이 일어난다.
            // (간수가 켜져 있으면 그건 그것대로 데려간다. 그건 설정이 시킨 일이라 다른 축이다.)
            carrier.Blackboard.EscortTo = EscortDest.None;

            carrier.AbandonWork();   // 하던 일을 놓아야 이 명령이 돈다
        }));
    }

    // **[우리로 옮긴다] · [죄수 침대로 옮긴다]** — 업기와 데려가기가 한 동작이다.
    //
    // 자리를 **콕 집지 않는다**(`EscortDest`는 종류다). 지금 가장 가까운 빈 칸을 미리 정해
    // 두면 걸어가는 사이에 그 칸이 차도 계속 그리로 가고, 여럿을 동시에 나를 때 서로 같은
    // 칸을 노린다. 어디에 넣을지는 `PrisonEscort.Resolve`가 매 틱 다시 정한다.
    //
    // **간수 설정을 안 본다** — 손으로 시킨 일은 "이 작업을 맡길까"의 결과가 아니라 지금
    // 이 순간의 지시다(자원 노드 직접 배정·[치료한다]와 같은 규칙).
    static void AddEscortOptions(UnitBrain target, List<PawnMover> commanders,
                                 List<PawnMenuOption> options)
    {
        Add("우리로 옮긴다",      EscortDest.Cage);
        Add("죄수 침대로 옮긴다", EscortDest.Bed);

        void Add(string label, EscortDest dest)
        {
            if (!PrisonEscort.CanOrder(commanders, target, dest)) return;
            options.Add((label, () => PrisonEscort.Order(commanders, target, dest)));
        }
    }

    // **[묻는다] · [태운다]** — 시체판 `AddEscortOptions`다.
    //
    // 자리를 콕 집지 않는 것도, 걸어가는 사이 자리가 차면 갈아타는 것도, 자리가 없으면
    // 선택지를 아예 안 띄우는 것도 전부 위와 **같은 규칙**이다. 시체를 옮기는 것과 붙잡은
    // 사람을 옮기는 것은 같은 동작이라 새 규칙을 만들 이유가 없다.
    static void AddFuneralOptions(UnitBrain target, List<PawnMover> commanders,
                                  List<PawnMenuOption> options)
    {
        Add("묻는다", EscortDest.Grave);
        Add("태운다", EscortDest.Pyre);

        void Add(string label, EscortDest dest)
        {
            if (!PrisonEscort.CanOrder(commanders, target, dest)) return;
            options.Add((label, () => PrisonEscort.Order(commanders, target, dest)));
        }
    }

    // 업고 있는 폰에게 "여기 내려놔"를 시킨다. 자리가 정해져 있으면 그 자리가 받아 가고
    // (침대면 눕히고, 우리면 가두고, 무덤이면 묻는다), 맨바닥이면 그냥 내린다.
    static void AddPutDownOption(List<PawnMover> commanders, List<PawnMenuOption> options,
                                 string label, Vector3 at, ICarrySite site = null)
    {
        // 받는 자리가 정해져 있으면 **그 자리가 받아 줄 사람**을 업은 폰만 고른다 — 자리를
        // 못 받는 사람을 업고 있으면 선택지를 아예 안 띄운다(눌러도 아무 일이 없으면 명령이
        // 먹혔는지 알 길이 없다).
        var carrier = Nearest(commanders, at,
                              b => b.Blackboard?.Carrying != null
                                && (site == null || site.Accepts(b.Blackboard.Carrying)));
        if (carrier == null) return;

        options.Add((label, () =>
        {
            var bbc = carrier.Blackboard;
            bbc.HasPutDown  = true;
            bbc.PutDownAt   = at;
            bbc.PutDownSite = site;
            carrier.AbandonWork();
        }));
    }

    static void AddCageOptions(PrisonCage cage, List<PawnMover> commanders,
                               List<PawnMenuOption> options)
    {
        // **놓아 주는 길은 하나다**(`Imprisonment.Release`) — 우리가 제 것만 풀면 도망 성향이
        // 안 붙어, 우리에서 푼 적은 그 자리에서 덤비고 사람에게서 푼 적은 도망친다.
        if (cage.IsOccupied)
        {
            var held = cage.Prisoner;
            options.Add(($"{held.UnitName}을(를) 풀어 준다", () => Imprisonment.Release(held)));
        }
        else
            // **시체를 업고 있으면 안 띄운다**(`PrisonCage.TryPut`이 거절한다 — 침대와 같은
            // 규칙). 눌러도 아무 일이 안 일어나면 명령이 먹혔는지 알 길이 없다.
            AddPutDownOption(commanders, options, "업은 사람을 여기 가둔다",
                             cage.HoldPosition, cage);
    }

    // **도축대 — 몸을 올리고 내리는 줄만.**
    //
    // 규칙을 손보는 것은 제 창(`ButcherTableUI`)이 한다. 여기 남는 것은 침대의
    // `[업은 사람을 여기 눕힌다]`와 **같은 축**이다 — 업고 있는 폰에게 내리는 지시고,
    // 조작 화면에 넣을 성질이 아니다(창을 열려고 짐을 내려놓을 수는 없다).
    //
    // **내리는 줄이 없으면 상판 위의 몸을 치울 길이 아예 없다** — 도축대 콜라이더가 상판을
    // 감싸고 있어서 그 위에 누운 몸은 커서로 집히지 않는다(화장터의 `[내린다]`와 같은 자리).
    static void AddButcherTableOptions(ButcherTable table, List<PawnMover> commanders,
                                       List<PawnMenuOption> options)
    {
        var laid = table.LaidBody;
        if (laid != null)
        {
            options.Add(($"{laid.UnitName}을(를) 내린다", () =>
            {
                // 일하러 오던 폰이 있으면 그 일감도 거둔다 — 안 그러면 내려놓자마자
                // 도로 올려놓는다.
                var job = JobManager.Instance?.FindButcherJobFor(laid.gameObject);
                if (job != null) JobManager.Instance.CancelJob(job);

                // **붙들어 둔 것을 먼저 푼다** — 산 채로 상판에 눕혀 뒀으면
                // `SetSleeping`이 걸려 있어 그냥 옮기면 자리만 바뀐 채 굳는다.
                table.ReleaseLaid();
                PawnPlacement.PlaceOnGround(laid.gameObject, table.ApproachPosition);
            }));
            return;
        }

        // 업고 있으면 **여기서 손질하라**고 시킨다. 시체를 메고 도축대까지 걸어와 놓고
        // 다시 그 시체를 우클릭해야 하는 것은 한 동작이 두 동작이 되는 일이다.
        var carrier = Nearest(commanders, table.transform.position,
                              b => b.Blackboard?.Carrying != null
                                && Butchering.CanButcher(b.Blackboard.Carrying));
        if (carrier == null) return;

        var body = carrier.Blackboard.Carrying;
        options.Add(($"{body.UnitName}을(를) 여기서 손질한다", () =>
        {
            var mgr = JobManager.Instance;
            if (mgr == null) return;

            var existing = mgr.FindButcherJobFor(body.gameObject);
            if (existing != null) { if (existing.Status != JobStatus.InProgress) { existing.ReleaseReservation(); existing.Reserve(carrier); } return; }

            mgr.ReserveFor(new ButcherJob(body.gameObject, table), carrier);
        }));
    }

    // **무덤 — 업은 시체를 여기 묻는다.**
    //
    // 파내는 것은 **여기 없다** — 그건 무덤을 조작하는 일이라 제 창(`GraveUI`)이 답한다.
    // 우클릭에 남는 것은 *업고 있는 폰에게 내리는 지시* 하나뿐이고, 그건 침대의
    // [여기 눕힌다]와 정확히 같은 자리다.
    static void AddGraveOptions(Grave grave, List<PawnMover> commanders,
                                List<PawnMenuOption> options)
    {
        if (grave.IsOccupied) return;

        AddPutDownOption(commanders, options, "업은 시체를 여기 묻는다",
                         grave.GravePosition, grave);
    }

    // **화장터 — 업은 시체를 여기 올린다.**
    //
    // 내리는 것도 진행도 **여기 없다**(`PyreUI`) — 무덤과 같은 이유다.
    static void AddPyreOptions(Pyre pyre, List<PawnMover> commanders,
                               List<PawnMenuOption> options)
    {
        if (pyre.IsOccupied) return;

        AddPutDownOption(commanders, options, "업은 시체를 여기서 태운다",
                         pyre.PyrePosition, pyre);
    }

    // **화톳불 — 켜고 끄는 줄 하나.**
    //
    // 어두워지면 저절로 붙으므로 평소에는 누를 일이 없다. 이 줄이 있는 이유는 **목재가
    // 아쉬운 며칠**이다 — 그때 어느 불을 죽일지가 실제 결정이 되고, 그 결정을 내릴 자리가
    // 없으면 화톳불은 지어 두고 잊는 물건이 된다.
    //
    // **명령 받을 폰이 필요 없다** — 폰이 장작을 나르지 않으므로(`MaterialPool.Consume`)
    // 이건 건물에 거는 스위치지 누구에게 시키는 일이 아니다.
    static void AddCampfireOptions(Campfire fire, List<PawnMenuOption> options)
    {
        options.Add(fire.WantsFire
            ? ("불을 끈다",   () => fire.SetWanted(false))
            : ("불을 지핀다", () => fire.SetWanted(true)));
    }

    static void AddBedOptions(Bed bed, List<PawnMover> commanders,
                              List<PawnMenuOption> options)
    {
        // **업고 있으면 그 사람을 여기 눕히는 것이 먼저다.** 사람을 진 채로 침대 앞에 서서
        // "내가 눕는다"가 맨 위에 뜨면 순서가 뒤집힌다.
        //
        // **보통 침대는 붙잡은 사람을 안 받는다**(`Bed.Accepts`). 받으면 갇힘이 안 걸린 채로
        // 눕기만 해서 다음 틱에 **누운 자세 그대로 걸어 나간다** — 침대가 감옥이 아니니 붙들
        // 근거가 없고, 그렇다고 붙들면 보통 침대가 곧 죄수 침대가 된다.
        string verb = bed.ForPrisoners ? "여기 가둔다" : "여기 눕힌다";
        if (!bed.IsOccupied)
            AddPutDownOption(commanders, options, $"업은 사람을 {verb}", bed.SleepPosition, bed);

        // 죄수 침대는 우리 폰이 자러 오는 자리가 아니다.
        //
        // **여기까지는 명령 받을 폰이 없어도 뜬다** — 풀어 주는 것은 침대가 하는 일이지
        // 폰에게 시키는 일이 아니다. 마지막 한 명을 가둔 순간 풀어 줄 길이 사라지면 안 된다.
        if (bed.ForPrisoners)
        {
            if (bed.Occupant != null)
            {
                var held = bed.Occupant;
                options.Add(($"{held.UnitName}을(를) 풀어 준다", () => Imprisonment.Release(held)));
            }
            else
                options.Add(("보통 침대로 되돌린다", () => bed.SetForPrisoners(false)));
            return;
        }

        // 아래는 **고른 폰에게 내리는 명령**이라 받을 사람이 있어야 한다.
        var brain = Nearest(commanders, bed.transform.position, null);
        if (brain == null) return;

        // **용도 전환은 우클릭에 둔다**(림월드식 "죄수용으로 지정"). 패널에 버튼을 세우려면
        // 씬을 고쳐야 하는데, 침대는 이미 눕기·치료를 우클릭으로 시키는 물건이라 여기가 맞다.
        // 누가 쓰고 있으면 안 바꾼다 — 바꾸는 순간 규칙이 뒤집힌 자리에 누운 사람이 남는다.
        if (bed.AssignedUnit == null && bed.Occupant == null)
            options.Add(("죄수용으로 바꾼다", () => bed.SetForPrisoners(true)));

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

    // **"조건에 맞는 폰 중 가장 가까운 하나"는 이 창의 기본 규약이다.**
    //
    // 우클릭 명령 대부분이 한 명에게만 간다 — 침대에 눕는 것도, 물건을 줍는 것도, 사람을
    // 업는 것도 여럿을 보내면 먼저 닿은 하나만 일하고 나머지는 헛걸음한다. 그래서 같은 고르기가
    // 세 벌로 적혀 있었고, 조건만 조금씩 달랐다(들 수 있나 / 업고 있나 / 무조건).
    //
    // `can`이 null이면 아무나. 못 고르면 null — 부르는 쪽이 그때 선택지를 안 내놓으면 된다.
    static UnitBrain Nearest(List<PawnMover> commanders, Vector3 point, System.Func<UnitBrain, bool> can)
    {
        UnitBrain best = null;
        float bestDist = float.MaxValue;

        foreach (var pawn in commanders)
        {
            if (pawn == null) continue;
            var b = pawn.GetComponent<UnitBrain>();
            if (b == null || (can != null && !can(b))) continue;

            float d = Vector3.SqrMagnitude(pawn.transform.position - point);
            if (d < bestDist) { bestDist = d; best = b; }
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

        var best = Nearest(commanders, drop.transform.position, b => CanTake(b, drop, ignoreLimit));
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

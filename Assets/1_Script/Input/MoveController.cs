using System.Collections.Generic;
using UnityEngine;
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

        // **적대 세력만 묻지 않고 친다.** 전투 중에 창이 뜨면 조작이 느려지고,
        // 그 경우엔 우클릭의 뜻이 하나뿐이라 물을 것도 없다.
        if (Physics.Raycast(ray, out RaycastHit first, 500f))
        {
            var faction = first.collider.GetComponent<Faction>();
            if (faction != null && !faction.IsPlayer && IsHostileToUs(faction, wanderers))
            {
                AttackWith(wanderers, first.collider.gameObject);
                return;
            }
        }

        // 그 밖의 모든 것은 **선택지 창**으로 간다 — 바닥까지 포함해서.
        // 바닥도 창을 거치므로 이동이 두 번 누르기가 된다. 대신 "우클릭하면 뭘 할 수 있는지 뜬다"가
        // 예외 없이 성립한다(예외가 있으면 어디서 뜨는지 매번 기억해야 한다).
        OpenMenuAt(ray, wanderers);
    }

    // 실제 이동 명령. 창의 [여기로 이동]과 옛 직접 명령이 같은 코드를 쓴다.
    void MoveTo(List<PawnMover> commanders, Vector3 point)
    {
        Vector3[] positions = CalcFormation(point, commanders.Count);
        for (int i = 0; i < commanders.Count; i++)
        {
            // 이동 명령 = 교전 이탈 + 하던 작업 취소.
            // Target을 그대로 두면 FightNode가 다음 틱에 목적지를 적 위치로 덮어쓰고,
            // 작업을 남겨두면 도착하자마자 일하러 돌아간다 — 둘 다 명령이 안 먹는 것처럼 보인다.
            var brain = commanders[i].GetComponent<UnitBrain>();
            var bb    = brain.Blackboard;

            bb.Target       = null;
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

    static void AttackWith(List<PawnMover> commanders, GameObject target)
    {
        foreach (var pawn in commanders)
        {
            var bb = pawn.GetComponent<UnitBrain>().Blackboard;
            bb.Target       = target;
            bb.ManualMove   = false;   // 공격 명령이 이동 명령을 대체
            bb.FollowTarget = null;    // 따라가던 중이었으면 그만둔다
        }
    }

    // 선택지 구성은 **여기서** 정한다(`PawnContextMenuUI`는 받은 것만 그린다).
    // 대상 종류를 창 안에서 따지기 시작하면 판단이 두 곳으로 갈라진다
    // (월드맵 도착 메뉴를 `WorldMapView.OpenMenu`가 짜는 것과 같은 규칙).
    void OpenMenuAt(Ray ray, List<PawnMover> commanders)
    {
        var menu = PawnContextMenuUI.Instance;

        // 바닥 지점은 따로 잰다 — 유닛·건물을 맞았어도 [여기로 이동]은 바닥을 가리켜야 한다.
        bool hasGround = Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
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
            title = !string.IsNullOrWhiteSpace(brain.Data?.RaceName) ? brain.Data.RaceName : go.name;

            // 거래는 상인일 때만. `Merchant`는 상인 역할이면 자동으로 붙는다.
            // 쓰러진 상인은 선택지에서 뺀다 — 눌러 봐야 아무 일도 안 일어난다.
            var merchant = go.GetComponent<Merchant>();
            if (merchant != null && merchant.CanTrade) options.Add(("거래한다", merchant.OnWorldClick));

            options.Add(("따라간다", () => FollowWith(commanders, go.transform)));

            // 아군(플레이어 세력)까지 칠 이유는 없다 — 오사 사고만 난다.
            var faction = go.GetComponent<Faction>();
            if (faction == null || !faction.IsPlayer)
                options.Add(("공격한다", () => AttackWith(commanders, go)));
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
        bb.FollowTarget      = null;
        bb.ManualMove        = false;
        best.AbandonWork();             // 하던 일감을 놓지 않으면 도착하자마자 일하러 돌아간다
    }

    static void FollowWith(List<PawnMover> commanders, Transform target)
    {
        foreach (var pawn in commanders)
        {
            var bb = pawn.GetComponent<UnitBrain>().Blackboard;
            bb.FollowTarget = target;
            bb.Target       = null;    // 따라가기와 교전은 같이 갈 수 없다
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

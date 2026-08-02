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
        if (PawnContextMenu.Instance != null && PawnContextMenu.Instance.IsOpen) return;
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

    // 선택지 구성은 **여기서** 정한다(`PawnContextMenu`는 받은 것만 그린다).
    // 대상 종류를 창 안에서 따지기 시작하면 판단이 두 곳으로 갈라진다
    // (월드맵 도착 메뉴를 `WorldMapView.OpenMenu`가 짜는 것과 같은 규칙).
    void OpenMenuAt(Ray ray, List<PawnMover> commanders)
    {
        var menu = PawnContextMenu.Instance;

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
        var corpse  = go.GetComponent<Corpse>();
        var core    = go.GetComponent<DungeonCore>();
        var clickable = go.GetComponent<IWorldClickable>();

        if (brain != null)
        {
            title = !string.IsNullOrWhiteSpace(brain.Data?.RaceName) ? brain.Data.RaceName : go.name;

            // 거래는 상인일 때만. `Merchant`는 NpcType이 ShopMerchant일 때 자동으로 붙는다.
            var merchant = go.GetComponent<Merchant>();
            if (merchant != null) options.Add(("거래한다", merchant.OnWorldClick));

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

            options.Add((tree ? "여기서 나무를 벤다" : "여기서 캔다", () =>
            {
                foreach (var pawn in commanders)
                {
                    var b = pawn.GetComponent<UnitBrain>();
                    if (b != null) node.Assign(b);
                }
            }));
            options.Add(("살펴본다", () => SelectionManager.InspectNode(node)));
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

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

        // 적 클릭 → 타겟 지정. 플레이어 세력이 아니면 명령으로 공격 가능(중립도 조준 허용 — 선공은 플레이어 몫).
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var faction = hit.collider.GetComponent<Faction>();
            if (faction != null && !faction.IsPlayer)
            {
                foreach (var pawn in wanderers)
                {
                    var bb = pawn.GetComponent<UnitBrain>().Blackboard;
                    bb.Target     = hit.collider.gameObject;
                    bb.ManualMove = false;   // 공격 명령이 이동 명령을 대체
                }
                return;
            }
        }

        // 바닥 클릭 → 이동 명령
        if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
        {
            Vector3[] positions = CalcFormation(groundHit.point, wanderers.Count);
            for (int i = 0; i < wanderers.Count; i++)
            {
                // 이동 명령 = 교전 이탈 + 하던 작업 취소.
                // Target을 그대로 두면 FightNode가 다음 틱에 목적지를 적 위치로 덮어쓰고,
                // 작업을 남겨두면 도착하자마자 일하러 돌아간다 — 둘 다 명령이 안 먹는 것처럼 보인다.
                var brain = wanderers[i].GetComponent<UnitBrain>();
                var bb    = brain.Blackboard;

                bb.Target     = null;
                bb.ManualMove = true;
                brain.AbandonWork();

                wanderers[i].GetComponent<CombatHandler>()?.ExitCombat();
                wanderers[i].SetMoveTarget(positions[i]);
            }
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

using System.Collections.Generic;
using UnityEngine;

// PawnMover에서 사용하는 길찾기 진입점.
// 실제 계산은 HierarchicalPathfinder에 위임.
public static class Pathfinder
{
    // 코스 라우트 (코스/L2 경로). 없으면 직선 1점.
    // 길찾기가 장애물로 치는 레이어. **진단이 실제 굽기와 같은 것을 봐야** 그림이
    // 거짓말을 안 한다.
    public static LayerMask ObstacleLayer =>
        HierarchicalPathfinder.Instance != null ? HierarchicalPathfinder.Instance.ObstacleLayer : 0;

    public static List<Vector3> ComputeCoarseRoute(Vector3 from, Vector3 to)
        => HierarchicalPathfinder.Instance != null
            ? HierarchicalPathfinder.Instance.ComputeCoarseRoute(from, to)
            : new List<Vector3> { to };

    // **광역이 낸 라우트를 로컬이 못 뚫었다** — 그 셀을 잠시 닫고 다음 라우트가 피하게 한다.
    // 이 문이 없으면 폰이 같은 벽 앞으로 몇 번이고 다시 걸어간다.
    public static void ReportUnreachable(Vector3 world)
        => HierarchicalPathfinder.Instance?.ReportUnreachable(world);

    // 유닛 중심 로컬 정밀 경로. Instance 없으면 null (호출측에서 직선 폴백).
    // self를 주면 그 폰을 제외한 다른 폰들을 장애물로 넣어 경로를 뽑는다(폰이 만든 막힘 우회).
    public static List<Vector3> FindLocalPath(Vector3 from, Vector3 target, PawnMover self = null)
        => HierarchicalPathfinder.Instance != null
            ? HierarchicalPathfinder.Instance.FindLocalPath(from, target, self)
            : null;
}

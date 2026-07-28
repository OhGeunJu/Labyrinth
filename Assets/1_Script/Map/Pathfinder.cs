using System.Collections.Generic;
using UnityEngine;

// PawnMover에서 사용하는 길찾기 진입점.
// 실제 계산은 HierarchicalPathfinder에 위임.
public static class Pathfinder
{
    // 코스 라우트 (메소/L2 경로). 없으면 직선 1점.
    public static List<Vector3> ComputeCoarseRoute(Vector3 from, Vector3 to)
        => HierarchicalPathfinder.Instance != null
            ? HierarchicalPathfinder.Instance.ComputeCoarseRoute(from, to)
            : new List<Vector3> { to };

    // 유닛 중심 로컬 정밀 경로. Instance 없으면 null (호출측에서 직선 폴백).
    // self를 주면 그 폰을 제외한 다른 폰들을 장애물로 넣어 경로를 뽑는다(폰이 만든 막힘 우회).
    public static List<Vector3> FindLocalPath(Vector3 from, Vector3 target, PawnMover self = null)
        => HierarchicalPathfinder.Instance != null
            ? HierarchicalPathfinder.Instance.FindLocalPath(from, target, self)
            : null;
}

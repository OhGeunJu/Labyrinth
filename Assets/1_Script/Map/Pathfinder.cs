using System.Collections.Generic;
using UnityEngine;

// PawnMover에서 사용하는 길찾기 진입점.
// 실제 계산은 HierarchicalPathfinder에 위임.
public static class Pathfinder
{
    public static List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        if (HierarchicalPathfinder.Instance != null)
            return HierarchicalPathfinder.Instance.FindPath(from, to);

        // HierarchicalPathfinder 없으면 직선 폴백
        return new List<Vector3> { to };
    }
}

using System.Collections.Generic;
using UnityEngine;

// 월드맵을 이동하는 원정 파티 (논리 데이터). 실시간 이동/월드 위치는 WorldMapView가 관리.
// 멤버 = 방랑자 + 동행 하수인 (편성 시 출발 거점 필드맵에서 제거된 상태). 여러 파티 동시 가능.
public class ExpeditionParty
{
    public readonly List<UnitBrain> Members = new();

    public Vector2Int CurrentNode;   // 현재 위치한 노드 (이동 중엔 마지막으로 지난 노드)

    public ExpeditionParty(Vector2Int startNode)
    {
        CurrentNode = startNode;
    }
}

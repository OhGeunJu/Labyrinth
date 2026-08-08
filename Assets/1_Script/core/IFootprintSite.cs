using System.Collections.Generic;
using UnityEngine;

// **이 건물이 깔고 앉는 칸들.** 다 지어진 건물(`PlacedBuilding`)과 아직 청사진인 것
// (`BuildingBlueprint`)이 같은 답을 내놓게 하려고 인터페이스로 뺐다.
//
// 다 지어진 건물은 그 칸이 이미 통행 불가라 따로 물어볼 일이 거의 없다. **문제는 청사진이다** —
// 청사진은 일부러 칸을 안 막는다(짓는 동안 폰이 지나다녀야 하고, 지으러 오는 폰도 통과한다).
// 그래서 "설 수 있는 칸"만 보고 작업 자리를 고르면 3×2 건물의 **한복판**이 뽑히고, 완성되는
// 순간 그 칸이 통행 불가가 되면서 방금 지은 폰이 벽 안에 갇힌다.
//
// 자리를 발자국 밖으로 잡는 것이 먼저고, 밀어내기는 그래도 남는 경우를 위한 안전망이다
// (`PlacedBuilding.OccupyTiles`).
public interface IFootprintSite
{
    IEnumerable<Vector2Int> Footprint { get; }
}

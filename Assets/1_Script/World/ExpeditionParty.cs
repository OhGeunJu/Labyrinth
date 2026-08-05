using System.Collections.Generic;
using UnityEngine;

// 월드맵을 이동하는 원정 파티 (논리 데이터). 실시간 이동/월드 위치는 WorldMapView가 관리.
// 멤버 = 방랑자 + 동행 하수인 (편성 시 출발 거점 필드맵에서 제거된 상태). 여러 파티 동시 가능.
//
// **등짐(Cargo)** — 밖에서 얻은 것은 거점에 돌아가야 창고에 들어간다.
// 지고 갈 수 있는 양은 동행한 폰들의 근력이 정한다 → 누구를 데려갈지가 선택이 된다.
public class ExpeditionParty
{
    public readonly List<UnitBrain> Members = new();

    public Vector2Int CurrentNode;   // 현재 위치한 노드 (이동 중엔 마지막으로 지난 노드)

    // 아이템 → 개수. 거점 진입 시 창고로 비운다.
    public readonly Dictionary<ItemData, int> Cargo = new();

    public ExpeditionParty(Vector2Int startNode)
    {
        CurrentNode = startNode;
    }

    // 적재 한도(kg) = 멤버들의 `CarryCapacity` 합. 필드의 폰 적재와 **같은 한 곳**에서 나온다 —
    // 원정대라고 다른 규칙을 쓰면 "이 폰을 데려가면 얼마나 더 지나"가 계산되지 않는다.
    public float Capacity => CarryCapacity.TotalFor(Members);

    // **등짐은 무게로 센다.** 예전엔 kg으로 만든 `Capacity`를 개수 한도로 썼다 —
    // 씨앗(0.05)이나 철갑옷(8)이나 똑같이 한 자리를 먹었고, 무엇을 담아 오느냐가
    // 아무 선택도 되지 못했다. `ItemData.Weight`는 이미 다 저작돼 있었다.
    public float CarriedWeight
    {
        get
        {
            float w = 0f;
            foreach (var kv in Cargo)
                if (kv.Key != null) w += kv.Key.Weight * kv.Value;
            return w;
        }
    }

    // 개수는 표시용으로만 남긴다(한도 판정에는 안 쓴다).
    public int CarriedCount
    {
        get
        {
            int n = 0;
            foreach (var kv in Cargo) n += kv.Value;
            return n;
        }
    }

    public float FreeWeight => Mathf.Max(0f, Capacity - CarriedWeight);

    public bool HasRoom => FreeWeight > 0f;

    // 한도까지만 받고 **실제로 받은 개수를 돌려준다** — 넘치는 만큼 조용히 사라지면 안 된다.
    public int TryAdd(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return 0;

        // 무게 없는 물건은 무게로 막지 않는다(문서·열쇠 같은 것). 격자 제약은 등짐엔 없다.
        int room = item.Weight <= 0f
                 ? amount
                 : Mathf.FloorToInt(FreeWeight / item.Weight);
        if (room <= 0) return 0;

        int take = Mathf.Min(room, amount);
        Cargo.TryGetValue(item, out int cur);
        Cargo[item] = cur + take;
        return take;
    }

    public void ClearCargo() => Cargo.Clear();
}

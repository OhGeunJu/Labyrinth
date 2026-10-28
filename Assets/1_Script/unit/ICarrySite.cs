using System.Collections.Generic;
using UnityEngine;

// **업은 것을 받는 자리.**
//
// 폰을 어깨에 메고 걸어가 어딘가에 내려놓는 일은 하나인데(`PawnCarry`), 받는 자리는 여럿이다 —
// 우리 · 죄수 침대 · 보통 침대, 그리고 **무덤**. 자리마다 다른 것은 넷뿐이다: 어디에 놓나 ·
// 어느 쪽을 보게 놓나 · 이 사람을 받나 · 받고 나서 무엇이 되나.
//
// **자리가 셋이 되는 순간 필드도 셋이 된다** — `UnitBlackboard`에 `PutDownBed`·`PutDownCage`가
// 나란히 있었고, `BTLeafNodes`는 그 둘을 삼항으로 골랐고, `PawnCarry`·`MoveController`는
// 지울 때마다 **둘 다** 지워야 했다. 하나 더 붙이면 그 사다리가 넷이 되고, 언젠가 한 칸을
// 빠뜨린다(이 저장소가 `CarryCapacity`·`Storage.Deliver`에서 이미 겪은 모양이다).
//
// 그래서 자리를 **하나의 물음**으로 접는다. 밖에서는 "자리에 놓는다"만 알면 된다.
public interface ICarrySite
{
    // 놓을 자리. 침대는 베개 쪽, 우리는 서 있을 칸, 무덤은 구덩이.
    Vector3 PutPosition { get; }

    // 어느 쪽을 보게 놓나. `null`이면 놓는 쪽이 알아서(= 업은 폰이 보던 방향).
    // 침대는 발이 베개를 베지 않게 자리가 정한다.
    Quaternion? PutRotation { get; }

    // 이미 찼나. **도착해서 다시 묻는다** — 고를 때 비어 있어도 걸어오는 사이에 찰 수 있다.
    bool IsFull { get; }

    // 이 사람을 받나. 보통 침대는 붙잡은 사람을 안 받고, 우리·침대는 시체를 안 받고,
    // 무덤은 **시체만** 받는다.
    bool Accepts(UnitBrain brain);

    // 받고 나서 무엇이 되나. 우리·죄수 침대면 갇히고, 보통 침대면 배정되고, 무덤이면 묻힌다.
    // **점유·배정·갇힘을 자리가 한 번에 세운다** — 밖에서 셋을 따로 부르면 하나가 빠진다.
    void Put(UnitBrain brain);

    // 알림 문구에 쓸 이름("우리에 이미 다른 사람이 있어…").
    string SiteName { get; }
}

// **빈 자리 중 가장 가까운 것** — 무덤·우리·화장터가 같은 열 줄을 세 벌 들고 있었다(2026-09-09).
// 침대는 여기 안 온다(죄수용인지를 같이 물어서 물음이 다르다 — `Bed.FindNearestFreePrison`).
public static class CarrySites
{
    public static T NearestFree<T>(IReadOnlyList<T> all, Vector3 from) where T : Component, ICarrySite
    {
        T best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (s == null || s.IsFull) continue;
            float sq = (s.transform.position - from).sqrMagnitude;
            if (sq >= bestSq) continue;
            best   = s;
            bestSq = sq;
        }
        return best;
    }
}

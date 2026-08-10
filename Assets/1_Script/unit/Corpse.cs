using UnityEngine;

// 시체. **살아 있을 때의 것 중 남겨야 하는 것만 든다.**
//
// `UnitBrain.BecomeCorpse`가 `Faction` 컴포넌트를 **지운다** — `DetectNode` 같은 자동 탐지가
// `OverlapSphere`로 `Faction`을 훑기 때문에, 컴포넌트가 남아 있으면 시체가 계속 적으로 잡힌다
// (`enabled = false`로는 못 막는다 — `GetComponent`는 꺼진 것도 돌려준다).
//
// 그래서 **소속은 여기서 기억한다.** 지우기만 하면 "누구의 시체인가"가 통째로 사라져,
// 화면이 죽은 자를 소속 없는 무엇으로 취급하게 된다(허기 바가 뜬 것이 그 증상이었다).
// 지우는 것은 자동 판정을 막기 위한 것이지 그 사실을 잊자는 뜻이 아니다.
public class Corpse : MonoBehaviour
{
    public Inventory   Inventory { get; private set; }
    public string      UnitName  { get; private set; }
    public FactionData Faction   { get; private set; }

    public void Init(string unitName, Inventory inventory, FactionData faction)
    {
        UnitName  = unitName;
        Inventory = inventory;
        Faction   = faction;
    }

    // 이 시체가 플레이어 세력이었나. 죽은 폰을 살아 있는 폰과 같은 잣대로 물어야 하는 곳
    // (허기·라벨)이 `Faction.IsPlayer` 대신 쓴다.
    public bool WasPlayer => Faction != null && Faction.PlayerControlled;
}

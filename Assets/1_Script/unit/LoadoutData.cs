using System.Collections.Generic;
using UnityEngine;

// **이 사람이 무엇을 지니고 다니는가.**
//
// 종족(`UnitData`)에 두면 틀린다. `Human` 하나를 산적·마을 사람·경비·상인이 같이 쓰는데,
// 그들이 지니는 물건은 전혀 다르다. 종족에 적으면 **인간이면 누구나 도끼를 차고 나온다.**
//
// 이 저장소의 네 축(종족·세력·AI 성향·역할)이 서로를 모르는 것과 같은 이유로 다섯째로 뗀다.
// **조합은 프리팹이 한다** — 인간 산적 = (`Human` + `Loadout_Bandit`),
// 고블린 도적 = (`Goblin` + `Loadout_GoblinRaider`). 같은 `Goblin`을 쓰는 `Minion_Goblin`은
// 내 하수인이라 다른 것을 지녀야 하는데, 종족에 적으면 그 둘을 가를 방법이 아예 없다.
//
// **여기가 유일한 출처다 — `UnitData`에는 아이템이 없다.**
//
// 한때 "몸에서 나오는 것(늑대 가죽)은 종족, 지니는 것(무기)은 역할"로 갈라 두 벌을 굴렸다.
// 서사로는 맞는 구분인데 **기계적으로는 둘 다 "죽으면 나오는 아이템"**이라, "이 적이 뭘
// 떨구나"에 답하려면 에셋 둘을 열어 더해야 했다. 늑대 가죽도 `Loadout_Wolf`에 적는다.
[CreateAssetMenu(fileName = "Loadout_", menuName = "DungeonColony/LoadoutData")]
public class LoadoutData : ScriptableObject
{
    [Header("차고 나오는 것")]
    [Tooltip("태어날 때 입힌다. 무기가 공격을, 갑옷이 피해 감소를 바꾸므로 **살아 있는 동안 일한다** " +
             "— 죽을 때 만들어 내면 맨몸으로 싸운 적이 철갑옷을 떨군다.\n" +
             "**같은 부위를 여럿 적으면 나중 것이 이긴다**(밀려난 것은 배낭에 남아 전리품이 된다).")]
    public List<LoadoutEntry> Loadout = new();

    [Header("주머니")]
    [Tooltip("죽으면 시체에 남는 물건. 살아 있는 동안 아무 차이를 안 만들어서 죽을 때 굴린다 " +
             "— 미리 지고 다니게 하면 적재 무게·짐내림 같은 살아 있는 폰의 규칙에 공짜로 걸린다.")]
    public List<LootEntry> Loot = new();

    [Tooltip("죽으면 나오는 동전. **종족이 든 것에 더해진다**(덮어쓰지 않는다) — " +
             "고블린 산적은 고블린이라서 지닌 것과 산적이라서 지닌 것을 둘 다 갖는다.")]
    [Min(0)] public int CoinDropMin;
    [Min(0)] public int CoinDropMax;
}

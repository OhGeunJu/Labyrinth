using System;
using System.Collections.Generic;
using UnityEngine;

// **무엇을 들면 무엇이 손에 보이나 — 답은 이 에셋 하나다**(`Resources/Data/HandPropSet.asset`).
//
// 무기는 `WeaponType`으로, 도구는 `WorkAnim`으로 고른다. 장비 에셋(`WeaponData`)마다 모델을 적게
// 하지 않는 이유: 아이템은 JSON에서도 오고(모드) 그쪽은 프리팹을 못 가리킨다. 종류로 고르면
// 철검이든 강철검이든 같은 검을 들고, 새 검을 데이터에 넣을 때 모델을 잊을 수가 없다.
//
// 만드는 도구는 `DungeonColony/Pawn/6. Create Hand Props`. 씬 배열이 아니라 `Resources`다 —
// 배열로 두면 런타임에 태어난 폰이 못 본다(이 저장소가 네 번 겪은 함정).
[CreateAssetMenu(fileName = "HandPropSet", menuName = "DungeonColony/HandPropSet")]
public class HandPropSet : ScriptableObject
{
    [Serializable] public class WeaponEntry { public WeaponType Type; public GameObject Prefab; }
    [Serializable] public class ToolEntry   { public WorkAnim   Work; public GameObject Prefab; }

    public List<WeaponEntry> Weapons = new();
    public List<ToolEntry>   Tools   = new();

    const string Path = "Data/HandPropSet";
    static HandPropSet _instance;
    static bool _tried;

    public static HandPropSet Instance
    {
        get
        {
            if (_instance == null && !_tried)
            {
                _tried    = true;
                _instance = Resources.Load<HandPropSet>(Path);
                if (_instance == null)
                    Debug.LogWarning("[HandPropSet] `Resources/" + Path + "`이 없다 — 손에 아무것도 안 보인다. " +
                                     "`DungeonColony/Pawn/6. Create Hand Props`를 돌릴 것.");
            }
            return _instance;
        }
    }

    public GameObject WeaponPrefab(WeaponType type)
    {
        foreach (var e in Weapons) if (e != null && e.Type == type) return e.Prefab;
        return null;
    }

    public GameObject ToolPrefab(WorkAnim work)
    {
        foreach (var e in Tools) if (e != null && e.Work == work) return e.Prefab;
        return null;
    }
}

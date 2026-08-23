using UnityEditor;
using UnityEngine;

// 도발 능력 에셋을 만든다(2026-08-23). **일회성** — 산출물(`Ability_Taunt.asset`)이 자리를
// 잡으면 지운다. 이후 조정은 인스펙터에서 한다.
//
// 값의 근거는 `docs/design_combat_rework.md` 5절:
//   인원 = 1 + 강인함/25   (0 → 1명, 50 → 3명, 110(SS) → 5명, 130 → 6명)
//   범위 = 4 + 강인함/24 m (0 → 4m,  130 → 9.4m)
//
// **처음엔 40·30이었다** — 그러면 강인함 SS(110)에서 **세 명**이라, 스탯을 거기까지 올려 놓고도
// 습격 한 무리(3~6)를 못 붙든다. 탱커의 값이 *"몇을 붙드느냐"*인데 그 수가 안 늘면 강인함을
// 올릴 이유가 도발 쪽에는 없다.
public static class TauntAbilityCreator
{
    const string Dir  = "Assets/Resources/Data/Abilities";
    const string Path = Dir + "/Ability_Taunt.asset";

    [MenuItem("DungeonColony/Create Taunt Ability")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Data"))
            {
                Debug.LogError("[도발] Assets/Resources/Data 가 없습니다."); return;
            }
            AssetDatabase.CreateFolder("Assets/Resources/Data", "Abilities");
        }

        // **있으면 값만 맞춘다** — 다시 만들면 이미 장비에 물려 둔 참조가 끊긴다
        // (에셋을 지우고 새로 만들면 guid가 바뀐다).
        var taunt = AssetDatabase.LoadAssetAtPath<AbilityData>(Path);
        bool isNew = taunt == null;
        if (isNew) taunt = ScriptableObject.CreateInstance<AbilityData>();

        taunt.AbilityName = "도발";
        taunt.Description = "방패를 두드려 주위의 적이 나를 노리게 한다.";
        taunt.Kind        = AbilityKind.Taunt;
        taunt.Cooldown    = 25f;
        taunt.Duration    = 8f;
        taunt.BaseRange     = 4f;
        taunt.StatPerRange  = 24f;
        taunt.BaseTargets   = 1;
        taunt.StatPerTarget = 25f;

        if (isNew) AssetDatabase.CreateAsset(taunt, Path);
        else       EditorUtility.SetDirty(taunt);

        AssetDatabase.SaveAssets();
        Debug.Log($"[도발] {(isNew ? "만들었습니다" : "값을 맞췄습니다")} — {Path}. " +
                  "방패 JSON의 `abilityId`가 `Ability_Taunt`를 가리킵니다.");
    }
}

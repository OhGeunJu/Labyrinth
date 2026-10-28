using UnityEditor;
using UnityEngine;

// 테스트용 — **죽은 자를 일으키는 것만** 따로 부른다(`Necromancy`).
//
// 원래는 습격이 도착할 때 딸려 오는 일이라, 확인하려면 교단·망자 습격이 뽑힐 때까지
// 이벤트를 굴려야 한다(`EventManager`의 `Force Roll Event`는 **아무거나** 뽑는다).
// 시체를 남겨 놓고 여기를 누르면 그 부분만 그 자리에서 볼 수 있다.
//
// `Assets/Editor/`에 있으므로 **빌드에 아예 안 들어간다**(`DebugGiveItems`와 같은 자리).
public static class DebugNecromancy
{
    [MenuItem("DungeonColony/Debug/Raise the Dead (검은 교단)")]
    static void RaiseAsCult() => Raise("Faction_Cult");

    [MenuItem("DungeonColony/Debug/Raise the Dead (망자)")]
    static void RaiseAsUndead() => Raise("Faction_Undead");

    // ── 삭히기 ───────────────────────────────────────────────────────────────
    //
    // 뼈가 되기까지 **게임 내 나흘**(1배속 실기 두 시간)이라, 그냥 두고는 *뼈에서 해골이
    // 선다*를 눈으로 볼 수가 없다. 단계만 앞당긴다 — 규칙은 그대로다.
    [MenuItem("DungeonColony/Debug/Age Corpses → 썩음")]
    static void AgeToRotten() => Age(Corpse.FreshDays + 0.1f, "썩음");

    [MenuItem("DungeonColony/Debug/Age Corpses → 뼈")]
    static void AgeToBones() => Age(Corpse.RottenDays + 0.1f, "뼈");

    static void Age(float days, string what)
    {
        if (!EnsurePlaying()) return;

        int n = 0;
        foreach (var corpse in Object.FindObjectsByType<Corpse>(FindObjectsSortMode.None))
        {
            if (corpse == null) continue;
            corpse.SetAge(days);
            n++;
        }

        Debug.Log($"[Debug] 시체 {n}구를 {what} 단계로 삭혔습니다.");
    }

    static bool EnsurePlaying()
    {
        if (Application.isPlaying) return true;

        Debug.LogWarning("[Debug] Play 중에만 동작합니다 — 시체는 런타임 상태입니다.");
        return false;
    }

    static void Raise(string assetName)
    {
        if (!EnsurePlaying()) return;

        // 세력 에셋은 레지스트리가 훑는 자리에 있다(`Resources/Data/Factions/`).
        var faction = Resources.Load<FactionData>($"Data/Factions/{assetName}");
        if (faction == null)
        {
            Debug.LogWarning($"[Debug] {assetName}을(를) 못 찾았습니다.");
            return;
        }

        int risen = Necromancy.RaiseAll(faction);
        if (risen == 0)
            Debug.Log($"[Debug] 일어난 것이 없습니다 — 맵에 시체가 없거나, " +
                      $"{faction.FactionName}이(가) 죽은 자를 일으키지 않습니다(FactionData.RaisesDead).");
    }
}

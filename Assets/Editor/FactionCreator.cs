using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 팩션 스타터 에셋 생성 — 세력 SO 4종 + 관계를 미리 채운다.
// 세계관: 고블린 = 몬스터(모두와 적대), 산적 = 사람(플레이어·마을사람을 털지만 몬스터와도 싸움).
// 방향별 관계의 대표 케이스인 "산적 → 마을사람 적대 / 마을사람 → 산적 중립"(한쪽만 선공)이 들어 있다.
// 이미 있는 에셋은 건드리지 않는다(관계도 새로 만든 것만 채움 — 사용자 수정 보존).
public static class FactionCreator
{
    const string Dir = "Assets/5_Data/Faction";

    const string Player   = "Faction_Player";
    const string Goblin   = "Faction_Goblin";
    const string Bandit   = "Faction_Bandit";
    const string Villager = "Faction_Villager";

    [MenuItem("DungeonColony/Create Starter Factions")]
    static void Create()
    {
        EnsureFolder(Dir);

        // 1단계: 에셋 생성 (관계는 서로를 참조하므로 전부 만든 뒤 2단계에서 연결)
        var made = new List<string>();
        var all  = new Dictionary<string, FactionData>();

        foreach (var (assetName, display, isPlayer, def) in new[]
        {
            (Player,   "플레이어",   true,  FactionStance.Neutral),
            (Goblin,   "고블린",     false, FactionStance.Hostile),   // 목록에 없으면 전부 적
            (Bandit,   "산적",       false, FactionStance.Neutral),
            (Villager, "마을 사람",  false, FactionStance.Neutral),
        })
        {
            string path = $"{Dir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FactionData>(path);
            if (existing != null) { all[assetName] = existing; continue; }

            var so = ScriptableObject.CreateInstance<FactionData>();
            AssetDatabase.CreateAsset(so, path);
            SetBasics(so, display, isPlayer, def);

            all[assetName] = so;
            made.Add(assetName);
        }

        // 2단계: 관계 — 이번에 새로 만든 에셋만 채운다.
        if (made.Contains(Player))
            SetRelations(all[Player], (all[Goblin],   FactionStance.Hostile),
                                      (all[Bandit],   FactionStance.Hostile),
                                      (all[Villager], FactionStance.Neutral));

        // 고블린(몬스터): 기본 적대 — 사람이든 뭐든 다 문다. 예외 없음.
        if (made.Contains(Goblin))
            SetRelations(all[Goblin]);

        // 산적(사람): 기본 중립이되 플레이어·마을사람은 털고, 몬스터인 고블린과도 싸운다.
        if (made.Contains(Bandit))
            SetRelations(all[Bandit], (all[Player],   FactionStance.Hostile),
                                      (all[Villager], FactionStance.Hostile),
                                      (all[Goblin],   FactionStance.Hostile));

        // 마을 사람: 아무도 선공하지 않음(맞으면 도망/반격). 플레이어와는 아군.
        if (made.Contains(Villager))
            SetRelations(all[Villager], (all[Player], FactionStance.Ally));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (made.Count == 0)
            Debug.Log($"[FactionCreator] 이미 전부 있습니다 (경로: {Dir}). 기존 에셋은 건드리지 않았습니다.");
        else
            Debug.Log($"[FactionCreator] {made.Count}개 생성 — {string.Join(", ", made)} (경로: {Dir}).\n" +
                      "각 유닛 프리팹의 Faction 컴포넌트 → Data에 연결하세요. " +
                      "관계는 양쪽 다 Data가 연결돼야 적용됩니다(한쪽이라도 비면 레거시 Player/Enemy 규칙).");
    }

    // 비공개 [SerializeField]라 SerializedObject로 채운다(런타임 API에 세터를 뚫지 않기 위함).
    static void SetBasics(FactionData so, string display, bool isPlayer, FactionStance def)
    {
        var sobj = new SerializedObject(so);
        sobj.FindProperty("factionName").stringValue    = display;
        sobj.FindProperty("playerControlled").boolValue = isPlayer;
        sobj.FindProperty("defaultStance").enumValueIndex = (int)def;
        sobj.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRelations(FactionData so, params (FactionData other, FactionStance stance)[] entries)
    {
        var sobj = new SerializedObject(so);
        var list = sobj.FindProperty("relations");
        list.ClearArray();

        for (int i = 0; i < entries.Length; i++)
        {
            list.InsertArrayElementAtIndex(i);
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Other").objectReferenceValue = entries[i].other;
            e.FindPropertyRelative("Stance").enumValueIndex      = (int)entries[i].stance;
        }

        sobj.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}

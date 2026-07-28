using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// AI 프로필 스타터 에셋 생성 — 자주 쓰는 역할 4종.
// 유닛 프리팹의 UnitBrain → Ai Profile 칸에 연결해서 쓴다(프로필 하나를 여러 프리팹이 공유).
// 이미 있는 에셋은 건드리지 않는다.
public static class AiProfileCreator
{
    const string Dir = "Assets/5_Data/AiProfile";

    [MenuItem("DungeonColony/Create Starter AI Profiles")]
    static void Create()
    {
        EnsureFolder(Dir);

        var made = new List<string>();

        // (파일명, 표시명, 성향, 혈액임계, 도주거리, 감지범위, 배회, 배회반경)
        var presets = new (string file, string display, CombatStance stance,
                           float bloodRatio, float fleeDist, float fleeRange,
                           bool wander, float wanderRadius)[]
        {
            // 겁쟁이 민간인 — 적대 세력이 보이면 무조건 도망. 마을사람·아이·잡상인.
            ("AiProfile_Civilian", "겁쟁이 민간인", CombatStance.Flee,             0.4f, 18f, 12f, true,  8f),
            // 일반 전투 — 보이면 싸운다. 몬스터·산적 기본.
            ("AiProfile_Fighter",  "전투원",       CombatStance.Fight,            0.4f, 15f,  0f, true, 12f),
            // 신중한 전투 — 피가 40% 아래로 떨어지면 물러난다. 용병·경비대장.
            ("AiProfile_Cautious", "신중한 전투원", CombatStance.FightWhileHealthy, 0.4f, 15f,  0f, true, 10f),
            // 보초 — 싸우되 자리를 뜨지 않는다. 문지기·상점 경비.
            ("AiProfile_Sentry",   "보초",         CombatStance.Fight,            0.4f, 15f,  0f, false, 0f),
        };

        foreach (var p in presets)
        {
            string path = $"{Dir}/{p.file}.asset";
            if (AssetDatabase.LoadAssetAtPath<AiProfileData>(path) != null) continue;

            var so = ScriptableObject.CreateInstance<AiProfileData>();
            AssetDatabase.CreateAsset(so, path);

            // 비공개 [SerializeField]라 SerializedObject로 채운다(런타임 API에 세터를 뚫지 않기 위함).
            var sobj = new SerializedObject(so);
            sobj.FindProperty("profileName").stringValue      = p.display;
            sobj.FindProperty("stance").enumValueIndex        = (int)p.stance;
            sobj.FindProperty("fleeBloodRatio").floatValue    = p.bloodRatio;
            sobj.FindProperty("fleeDistance").floatValue      = p.fleeDist;
            sobj.FindProperty("fleeDetectRange").floatValue   = p.fleeRange;
            sobj.FindProperty("wander").boolValue             = p.wander;
            sobj.FindProperty("wanderRadius").floatValue      = p.wanderRadius;
            sobj.ApplyModifiedPropertiesWithoutUndo();

            made.Add(p.file);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (made.Count == 0)
            Debug.Log($"[AiProfileCreator] 이미 전부 있습니다 (경로: {Dir}).");
        else
            Debug.Log($"[AiProfileCreator] {made.Count}개 생성 — {string.Join(", ", made)} (경로: {Dir}).\n" +
                      "유닛 프리팹의 UnitBrain → Ai Profile 칸에 연결하세요. 비워두면 타입별 기본값으로 동작합니다.");
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

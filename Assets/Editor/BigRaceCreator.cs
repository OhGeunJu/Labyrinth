using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **큰 종족 셋 — 오크 · 오우거 · 트롤**(2026-10-28, 사용자: *"오크 1.25배 큰 크기? 오우거 트롤 2배 큰 크기"*).
//
// 종족(`UnitData`)과 적 프리팹 한 벌씩을 세운다. 오우거 · 트롤의 몸은 사람 몸이 아니라 따로 빚는다(`ChibiPawnCreator.Brutes` —
// 끝에 부르는 `Pawn/3`이 굽는다). **몸집 · 휘두르기 · 체질은 종족 칸**이다(`BodySize` · `Regeneration`) —
// 프리팹은 사람 몸(`Enemy_Bandit`)을 베끼고 `Model`을 종족 크기로 구워 둔다(에디터에서도 큰 몸으로 보이게 — 10-31).
// 판에서는 `BodySize.Apply`가 종족 값을 다시 넣는다(곱이 아니라 두 번 걸리지 않는다).
// 세력 · 성향 · 지닐 것은 고블린 프리팹의 것을 물린다(녹색 피부 무리) — 다르게 하려면 프리팹에서 바꿀 것.
//
// **어디에 나오나는 여기서 정하지 않는다** — 바이옴 표(`BiomeContentData.WildSpawns`)와 소굴은 사용자가 저작한다.
//
// 이미 있으면 **값만 다시 적는다**(guid를 지킨다 — 소굴 · 표가 guid로 가리킨다). 끝에 `Pawn/3`을 불러 살을 입힌다
// (옷 색 · 체형은 `ChibiPawnCreator.Outfits`에 있다).
public static class BigRaceCreator
{
    const string UnitDir   = "Assets/Resources/Data/Units";
    const string PrefabDir = "Assets/3_Prefab/Pawn";
    const string HumanData = UnitDir + "/Human.asset";
    const string BodyFrom  = PrefabDir + "/Enemy_Bandit.prefab";    // 사람 크기의 치비 몸
    const string GangFrom  = PrefabDir + "/Enemy_Goblin.prefab";    // 세력 · 성향 · 지닐 것

    readonly struct Race
    {
        public readonly string Key, Name;
        public readonly float Scale, Hp, Speed, Damage;
        public readonly int Cleave;
        public readonly float Regen, SlowChance;
        public readonly StatGrade Grade, Strong;
        public readonly string[] Names;
        public readonly WeaponType[] Forbidden;
        public readonly Color32[] Tones;

        public Race(string key, string name, float scale, float hp, float speed, float damage, int cleave,
                    float regen, float slowChance, StatGrade grade, StatGrade strong, WeaponType[] forbidden, string[] names,
                    Color32[] tones = null)
        {
            Tones = tones ?? new Color32[0];
            Key = key; Name = name; Scale = scale; Hp = hp; Speed = speed; Damage = damage; Cleave = cleave;
            Regen = regen; SlowChance = slowChance; Grade = grade; Strong = strong; Forbidden = forbidden; Names = names;
        }
    }

    // 큰 손으로는 활시위 · 석궁 · 지팡이를 못 다룬다. 오크는 사람만 한 손이라 다 쥔다.
    static readonly WeaponType[] BigHands = { WeaponType.Bow, WeaponType.Crossbow, WeaponType.Staff };

    static readonly Race[] Races =
    {
        //        키        이름     크기  체력  속도  피해 휩쓸기 아묾  경직  등급            힘·맷집
        new Race("Orc",   "오크",   1.25f, 130f, 4.0f, 13f, 0, 0f,   0f,    StatGrade.D,    StatGrade.C,
                 new WeaponType[0],
                 new[] { "그롬마쉬", "우르각", "볼그", "가즈낙", "두르크", "모르곤", "라그낙", "스노가", "타르쉬", "우그락" }),
        new Race("Ogre",  "오우거", 2.0f,  260f, 3.4f, 22f, 2, 0f,   0.35f, StatGrade.DMinus, StatGrade.B,
                 BigHands,
                 new[] { "굼보", "두그", "모로", "부르가", "오그", "툼브", "흐룸", "고르바", "바룸", "둠두" },
                 // 오우거는 개체마다 살색이 크게 다르다 — 푸른 · 초록 · 회갈 · 적갈 · 올리브 · 보랏빛 회색
                 new Color32[] { new(0x6E, 0x8F, 0xC8, 0xFF), new(0x7A, 0xA0, 0x62, 0xFF), new(0xA8, 0x94, 0x78, 0xFF),
                                 new(0xA0, 0x64, 0x4E, 0xFF), new(0x8C, 0x96, 0x58, 0xFF), new(0x8A, 0x78, 0x98, 0xFF) }),
        new Race("Troll", "트롤",   2.0f,  220f, 3.8f, 18f, 2, 0.6f, 0f,    StatGrade.DMinus, StatGrade.CPlus,
                 BigHands,
                 new[] { "스크라그", "우르스", "그림", "호르크", "자브", "트룩", "브락", "스날", "무르그", "크로그" },
                 // 트롤은 푸른빛과 회색 안에서만 — 푸른 회색 · 슬레이트 · 찬 회색 · 어두운 청색
                 new Color32[] { new(0x5E, 0x6A, 0x7A, 0xFF), new(0x58, 0x60, 0x6A, 0xFF), new(0x7A, 0x80, 0x86, 0xFF), new(0x4E, 0x5E, 0x80, 0xFF) }),
    };

    [MenuItem("DungeonColony/Pawn/9. Create Big Races (오크 · 오우거 · 트롤)")]
    public static void Create()
    {
        var human = AssetDatabase.LoadAssetAtPath<UnitData>(HumanData);
        if (human == null) { Debug.LogError($"[큰 종족] 바탕 종족이 없다 — {HumanData}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BodyFrom) == null) { Debug.LogError($"[큰 종족] 바탕 몸이 없다 — {BodyFrom}"); return; }

        var gang = AssetDatabase.LoadAssetAtPath<GameObject>(GangFrom);
        var made = new List<string>();

        foreach (var r in Races)
        {
            var data = EnsureData(r, human);
            if (EnsurePrefab(r, data, gang)) made.Add($"{r.Name}({r.Scale}배)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[큰 종족] {string.Join(" · ", made)} — 이어서 `Pawn/3`으로 살을 입힌다. **어디에 나오나는 바이옴 표 · 소굴에서 정할 것.**");

        ChibiPawnCreator.Create();
    }

    static UnitData EnsureData(Race r, UnitData human)
    {
        string path = $"{UnitDir}/{r.Key}.asset";
        var d = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<UnitData>();
            EditorUtility.CopySerialized(human, d);   // 사람의 나머지 값(도축 · 성장 · 배경)을 바탕으로
            AssetDatabase.CreateAsset(d, path);
        }

        d.name       = r.Key;
        d.Id         = r.Key;
        d.RaceName   = r.Name;
        d.NamePool   = new List<string>(r.Names);
        d.MaleNames  = new List<string>();
        d.FemaleNames = new List<string>();
        d.MaxHP        = r.Hp;
        d.MoveSpeed    = r.Speed;
        d.AttackDamage = r.Damage;

        d.DefaultMinGrade = r.Grade;
        d.DefaultMaxGrade = r.Grade;
        d.StatGrades = new List<StatGradeEntry>
        {
            new() { Stat = StatName.Strength,  Min = r.Strong, Max = r.Strong },
            new() { Stat = StatName.Toughness, Min = r.Strong, Max = r.Strong },
        };

        d.BodyScale         = r.Scale;
        d.CleaveTargets     = r.Cleave;
        d.CleaveDamage      = 0.5f;
        d.ForbiddenWeapons  = new List<WeaponType>(r.Forbidden);
        d.RegenPerSecond    = r.Regen;
        d.HitEffect         = StatusEffectType.Slowed;
        d.HitEffectChance   = r.SlowChance;
        d.HitEffectDuration = 2f;
        d.HitEffectStrength = 0.6f;
        d.SkinTones = new List<Color>();
        foreach (var c in r.Tones) d.SkinTones.Add(c);

        EditorUtility.SetDirty(d);
        return d;
    }

    static bool EnsurePrefab(Race r, UnitData data, GameObject gang)
    {
        string path = $"{PrefabDir}/Enemy_{r.Key}.prefab";
        if (!File.Exists(path) && !AssetDatabase.CopyAsset(BodyFrom, path))
        {
            Debug.LogError($"[큰 종족] {path}를 못 만들었다.");
            return false;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!root.TryGetComponent(out UnitBrain brain)) { Debug.LogError($"[큰 종족] {path}에 `UnitBrain`이 없다."); return false; }

            var so = new SerializedObject(brain);
            so.FindProperty("unitData").objectReferenceValue = data;
            if (gang != null && gang.TryGetComponent(out UnitBrain gb))
            {
                var gso = new SerializedObject(gb);
                so.FindProperty("aiProfile").objectReferenceValue = gso.FindProperty("aiProfile").objectReferenceValue;
                so.FindProperty("loadout").objectReferenceValue   = gso.FindProperty("loadout").objectReferenceValue;
            }
            so.FindProperty("pawnSpec").objectReferenceValue = null;   // 산적의 명세를 물려받지 않는다
            so.ApplyModifiedPropertiesWithoutUndo();

            if (gang != null && gang.TryGetComponent(out Faction gf) && root.TryGetComponent(out Faction f))
            {
                var fso = new SerializedObject(f);
                fso.FindProperty("data").objectReferenceValue = new SerializedObject(gf).FindProperty("data").objectReferenceValue;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            // **크기는 종족이 준다** — 프리팹에도 같은 값을 구워 둔다(판에서는 `BodySize.Apply`가 넣는다 — 곱하지 않는다).
            var model = root.transform.Find("Model");
            if (model != null) model.localScale = Vector3.one * r.Scale;

            root.name = $"Enemy_{r.Key}";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

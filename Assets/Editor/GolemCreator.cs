using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **골렘 하수인 셋**(2026-08-28).
//
// `Stone_Golem` 팩은 **같은 메시에 머티리얼만 다른 셋**이다 — 재질의 플래그가 그 셋을
// 가른다(`_lava: 1` · `_Snow: 1` · `_web: 0.06`). 그래서 종류를 이름이 아니라 **성격**으로
// 붙였다: 용암 · 서리 · 돌.
//
// ── 왜 베껴서 고치나 ───────────────────────────────────────────────────────
//
// 폰 프리팹에는 **눈에 안 보이는 저작이 잔뜩 있다** — `CharacterController`(높이·반경·발밑
// 피벗), `UnitBrain`·`Faction`·`BodyPartSystem`·`CombatHandler`·`Equipment`·`Inventory`,
// 레이어, 그리고 그것들이 서로 물린 자리. 새로 지으면 그걸 다 다시 맞춰야 하고 **하나만
// 빠뜨려도 조용히 틀린다**(폰이 땅에 박히거나, 안 맞거나, 아무도 안 때린다).
//
// 그래서 `Minion_Skeleton`을 **통째로 베끼고 `Model` 자식의 속만 갈아 끼운다.** 오늘
// 건물에 얼굴을 입힐 때와 같은 규칙이다: **저작된 것은 안 건드린다.**
//
// ── 발을 어디에 붙이나 ─────────────────────────────────────────────────────
//
// 폰의 피벗 규약(발밑이 0이냐 −1이냐)은 문서 두 곳이 다르게 적혀 있다. **그래서 안 믿고
// 잰다** — 베낀 해골의 몸이 실제로 서 있던 높이를 재서, 골렘의 발을 **정확히 그 자리**에
// 놓는다. 규약이 무엇이든 해골과 똑같이 선다.
public static class GolemCreator
{
    const string Src      = "Assets/3_Prefab/Pawn/Minion_Skeleton.prefab";
    const string PawnDir  = "Assets/3_Prefab/Pawn";
    const string UnitDir  = "Assets/Resources/Data/Units";
    const string Pack     = "Assets/assetsss/Stone_Golem/prefab";
    const string StonePath = "Assets/Resources/Data/Items/Res_Stone.asset";

    struct Golem
    {
        public string Id, Name, Art;
        public string[] Names;              // 이름 풀
        public float Height;                // 서 있을 때의 키(m)
        public int   HP, Damage, Cost;
        public float Speed, Range;
        public (int type, float amount)[] Resist;
        public (StatName stat, int grade)[] Grades;
        public string Desc;
    }

    // 피해 종류(`DamageType`): 0 베기 · 1 찌르기 · 2 둔기 · 3 불 · 4 물 · 5 대지 ·
    // 6 번개 · 7 비전 · 8 암흑 · 9 신성.
    static readonly Golem[] Golems =
    {
        new Golem
        {
            Id = "Golem_Stone", Name = "돌 골렘", Art = "SKM_Golem (1)",   // `_web` — 이끼 낀 돌
            Names = new[]{ "무른 바위", "이끼 낀 것", "오래 선 것", "말 없는 벽", "굳은 흙" },
            Height = 2.6f, HP = 180, Damage = 17, Speed = 2.6f, Range = 1.8f, Cost = 60,
            Grades = new (StatName, int)[]
            {
                // **있는 것만 적는다** — 기본 등급이 0이라 안 적은 것은 전부 0이다.
                // 골렘에게 요리·의술·연구·사교가 F−인 것은 결함이 아니라 **정의**다.
                (StatName.Strength,    20),   // 짐꾼이자 벽 — 적재량이 근력에서 나온다
                (StatName.Toughness,   22),
                (StatName.MeleeAttack, 12),
                (StatName.MartialArts, 14),   // 장비를 못 끼니 **맨몸 숙련이 곧 무기 숙련**이다
                (StatName.Defense,      6),   // 막지 않는다 — 맞고 버틴다
                (StatName.Evasion,     1),   // 크고 느리다
                (StatName.Athletics,    8),
                (StatName.Labor,       14),
                (StatName.Mining,      16),
                (StatName.Stonecutting,12),
                (StatName.Cutting,      8),
                (StatName.Dexterity,    2),
                (StatName.Cognition,    2),
            },
            // 칼날은 튕기고 창은 안 박히지만 **둔기에는 깨진다** — 돌의 성격이 곧 약점이다.
            Resist = new[]{ (0, 0.6f), (1, 0.5f), (2, -0.3f), (5, 0.5f) },
            Desc = "가장 단단하고 가장 느리다.",
        },
        new Golem
        {
            Id = "Golem_Lava", Name = "용암 골렘", Art = "SKM_Golem (2)",  // `_lava: 1`
            Names = new[]{ "식지 않는 것", "붉은 속", "타는 어깨", "재를 흘리는 것", "불씨" },
            Height = 2.5f, HP = 130, Damage = 24, Speed = 3.1f, Range = 1.8f, Cost = 80,
            Grades = new (StatName, int)[]
            {
                // **있는 것만 적는다** — 기본 등급이 0이라 안 적은 것은 전부 0이다.
                // 골렘에게 요리·의술·연구·사교가 F−인 것은 결함이 아니라 **정의**다.
                (StatName.Strength,    18),   // 짐꾼이자 벽 — 적재량이 근력에서 나온다
                (StatName.Toughness,   14),
                (StatName.MeleeAttack, 16),
                (StatName.MartialArts, 16),   // 장비를 못 끼니 **맨몸 숙련이 곧 무기 숙련**이다
                (StatName.Defense,      6),   // 막지 않는다 — 맞고 버틴다
                (StatName.Evasion,     3),   // 크고 느리다
                (StatName.Athletics,    8),
                (StatName.Labor,       14),
                (StatName.Mining,      10),
                (StatName.Stonecutting,8),
                (StatName.Cutting,      8),
                (StatName.Dexterity,    2),
                (StatName.Cognition,    2),
            },
            // 불에는 거의 안 다치고 **물에는 크게 다친다** — 그 한 줄이 이 골렘의 쓰임을 정한다.
            Resist = new[]{ (3, 0.9f), (4, -0.6f), (0, 0.4f), (1, 0.4f) },
            Desc = "가장 아프게 치고 물에 약하다.",
        },
        new Golem
        {
            Id = "Golem_Frost", Name = "서리 골렘", Art = "SKM_Golem",      // `_Snow: 1`
            Names = new[]{ "언 손", "흰 등", "숨 없는 것", "겨울 조각", "서릿발" },
            Height = 2.5f, HP = 150, Damage = 15, Speed = 2.9f, Range = 1.8f, Cost = 70,
            Grades = new (StatName, int)[]
            {
                // **있는 것만 적는다** — 기본 등급이 0이라 안 적은 것은 전부 0이다.
                // 골렘에게 요리·의술·연구·사교가 F−인 것은 결함이 아니라 **정의**다.
                (StatName.Strength,    18),   // 짐꾼이자 벽 — 적재량이 근력에서 나온다
                (StatName.Toughness,   18),
                (StatName.MeleeAttack, 12),
                (StatName.MartialArts, 13),   // 장비를 못 끼니 **맨몸 숙련이 곧 무기 숙련**이다
                (StatName.Defense,      6),   // 막지 않는다 — 맞고 버틴다
                (StatName.Evasion,     2),   // 크고 느리다
                (StatName.Athletics,    8),
                (StatName.Labor,       14),
                (StatName.Mining,      10),
                (StatName.Stonecutting,8),
                (StatName.Cutting,      8),
                (StatName.Dexterity,    2),
                (StatName.Cognition,    2),
            },
            Resist = new[]{ (4, 0.7f), (3, -0.5f), (0, 0.4f), (1, 0.4f) },
            Desc = "물을 흘려보내고 불에 약하다.",
        },
    };

    [MenuItem("DungeonColony/Building/4. Create Golem Minions (골렘 셋)")]
    static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Src) == null)
        {
            Debug.LogError($"[골렘] 본보기 프리팹이 없다 — `{Src}`. 하수인 프리팹이 옮겨졌으면 " +
                           "`GolemCreator.Src`를 고칠 것.");
            return;
        }

        var made = new List<string>();
        foreach (var g in Golems)
        {
            var art = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/{g.Art}.prefab");
            if (art == null) { Debug.LogError($"[골렘] 모델이 없다 — `{Pack}/{g.Art}.prefab`"); continue; }

            var prefab = MakePrefab(g, art);
            if (prefab == null) continue;

            var data = MakeData(g, prefab);
            Link(prefab, data);
            made.Add($"{g.Name} — HP {g.HP} · 피해 {g.Damage} · 속도 {g.Speed} · 석재 {g.Cost}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[골렘] {made.Count}종을 만들었다.\n  " + string.Join("\n  ", made) +
                  "\n**장비를 안 끼고 스탯도 안 굴린다** — 만든 것이라 소환할 때마다 같은 놈이 나온다.\n**핵의 소환 목록에 뜨려면** `MinionData`가 `Resources/Data/Units/`에 있으면 된다 — " +
                  "거기 뒀다. 값을 다듬는 것은 그 에셋의 인스펙터에서.");
    }

    // ── 프리팹 ──────────────────────────────────────────────────────────────

    static GameObject MakePrefab(Golem g, GameObject art)
    {
        string path = $"{PawnDir}/Minion_{g.Id}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // **이미 있으면 그 루트를 다시 쓴다** — 통째로 새로 만들면 루트 fileID가 바뀌어
        // 씬에 놓인 인스턴스가 오버라이드를 잃는다.
        GameObject root = existing != null
            ? PrefabUtility.LoadPrefabContents(path)
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Src));

        try
        {
            root.name = $"Minion_{g.Id}";

            // 베낀 것이 프리팹 인스턴스면 끊는다 — 안 끊으면 해골을 고칠 때 골렘이 따라 바뀐다.
            if (existing == null && PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var model = root.transform.Find("Model");
            if (model == null) { Debug.LogError($"[골렘] `{Src}`에 `Model` 자식이 없다."); return null; }

            // **바꾸기 전에 잰다** — 해골의 몸이 실제로 서 있던 자리가 골렘이 설 자리다.
            bool hadStand = Bounds(model.gameObject, out var stand);

            for (int i = model.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(model.GetChild(i).gameObject);
            model.localPosition = Vector3.zero;
            model.localScale    = Vector3.one;

            var body = (GameObject)PrefabUtility.InstantiatePrefab(art, model);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale    = Vector3.one;

            // 몸에 딸려 온 콜라이더는 뗀다 — 폰의 몸은 루트의 `CharacterController` 하나다.
            // 둘이면 서로 밀어내고, 길찾기는 그중 아무것도 안 본다.
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // **레이어를 루트에 맞춘다** — 다르면 `PawnMover.ApplyLayer`(세력이 바뀔 때
            // 옮기는 것)가 이 몸만 옛 레이어에 두고 가서 **몸의 절반만 규칙을 따른다.**
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = root.layer;

            Stand(body, g.Height, hadStand ? stand : new Bounds());

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) Debug.LogError($"[골렘] 프리팹을 못 만들었다 — `{path}`.");
            return saved;
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else                  Object.DestroyImmediate(root);
        }
    }

    // 키를 맞추고 **발을 해골이 서 있던 높이에** 붙인다.
    static void Stand(GameObject body, float height, Bounds stand)
    {
        if (!Bounds(body, out var b) || b.size.y <= 0.0001f) return;

        body.transform.localScale *= height / b.size.y;

        if (!Bounds(body, out b)) return;
        float footY = stand.size.y > 0.0001f ? stand.min.y : 0f;   // 잰 것이 없으면 발밑을 0으로
        body.transform.localPosition += new Vector3(-b.center.x, footY - b.min.y, -b.center.z);
    }

    static bool Bounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    // ── 하수인 데이터 ───────────────────────────────────────────────────────

    static MinionData MakeData(Golem g, GameObject prefab)
    {
        string path = $"{UnitDir}/{g.Id}.asset";

        var d = AssetDatabase.LoadAssetAtPath<MinionData>(path);
        bool fresh = d == null;
        if (fresh) d = ScriptableObject.CreateInstance<MinionData>();

        d.RaceName = g.Name;
        d.NamePool = new List<string>(g.Names);

        d.MaxHP        = g.HP;
        d.MoveSpeed    = g.Speed;
        d.AttackDamage = g.Damage;
        d.AttackRange  = g.Range;
        d.DetectRange  = 12f;

        // **언데드가 아니다.** 골렘은 일으켜 세운 시체가 아니라 만든 것이라, 신성 ×2도
        // 기절 면역도 붙으면 안 된다(`UnitBrain.IsUndead`가 그 둘을 같이 답한다).
        d.IsUndead   = false;
        d.NightVision = 0.6f;

        d.Resistances = new List<DamageResistance>();
        foreach (var (type, amount) in g.Resist)
            d.Resistances.Add(new DamageResistance { Type = (DamageType)type, Amount = amount });

        // **기본은 바닥이고 잘하는 것만 적는다**(2026-08-28). 반대로 하면(기본을 높이고
        // 못하는 것을 적으면) 스탯이 쉰셋이라 **적다가 빠뜨린 것이 곧 재능**이 된다 —
        // 골렘이 조용히 요리를 잘하게 된다. 없는 쪽이 기본이어야 안 적은 것이 안전하다.
        d.DefaultMinGrade = StatGrade.FMinus;
        d.DefaultMaxGrade = StatGrade.FMinus;

        d.StatGrades = new List<StatGradeEntry>();
        foreach (var (stat, grade) in g.Grades)
            d.StatGrades.Add(new StatGradeEntry
            {
                Stat = stat,
                Min  = (StatGrade)grade,
                Max  = (StatGrade)grade,   // 굴림을 껐어도 **Min==Max로 적어 둔다** — 값이 하나라는 것이 에셋에서 보인다
            });

        // **굴리지 않는다** — 소환할 때마다 값이 달라지면 *"좋은 놈이 나올 때까지 다시
        // 소환한다"*가 되고, 하수인은 성장도 안 하니 나쁘게 나온 것은 재료만 버린 것이다.
        // 범위는 그대로 두되 **한가운데**로 고정된다(`UnitData.RollGrade`).
        d.RollStats = false;

        // **장비를 안 낀다** — 만든 것에게 검을 쥐여 주는 것은 이상하고, 낄 수 있으면
        // 화면에 칸이 뜨고 칸이 뜨면 언젠가 무엇이 들어간다. 맨몸 피해는 `AttackDamage`가 든다.
        d.CanEquip = false;

        d.Prefab           = prefab;
        d.SummonCostItem   = AssetDatabase.LoadAssetAtPath<ItemData>(StonePath);
        d.SummonCostAmount = g.Cost;
        d.SortOrder        = 20;

        // **작업 목록은 비운다 = 전부 허용.** 켜고 끌 수 있는 작업이 하나도 없는 목록은
        // 빈 목록과 같게 보고 경고까지 하므로, 적을 것이 없으면 비우는 것이 맞다.
        d.AllowedJobs = new List<JobCategory>();

        // **못 하는 일은 켤 수 있는 칸으로 두지 않는다.** 등급을 F−로 깔아 둔 것만으로는
        // *"아주 못한다"*여서 다른 폰이 없으면 골렘이 요리를 하러 간다 — 손도 눈도 없는데.
        // 성장 배경의 무능력과 같은 자리로 흘려보낸다(칸에 `×`로 뜬다).
        //
        // **남기는 것은 몸으로 하는 일뿐이다** — 나르고, 캐고, 베고, 짓고, 내려놓는다.
        // 그게 골렘의 쓸모이고, 안 남긴 것이 곧 *"그럼 사람은 왜 필요한가"*의 답이다.
        d.DisabledJobs = new List<JobCategory>
        {
            JobCategory.Crafting,   // 손재주
            JobCategory.Research,   // 읽고 쓴다
            JobCategory.Medical,    // 남의 몸을 다룬다
            JobCategory.Farming,    // 살아 있는 것을 돌본다
            JobCategory.Butcher,
            JobCategory.Warden,     // 말로 설득한다 — 말을 못 한다
        };

        if (fresh) AssetDatabase.CreateAsset(d, path);
        EditorUtility.SetDirty(d);

        if (d.SummonCostItem == null)
            Debug.LogWarning($"[골렘] `{g.Name}`이 `Res_Stone`을 못 찾아 소환 재료가 비었다 — 에셋에서 채울 것.");

        return d;
    }

    // 프리팹의 `UnitBrain`이 이 데이터를 가리켜야 한다 — 안 가리키면 **해골로 태어난다**
    // (베낀 것이라 해골 데이터를 그대로 들고 있다). `unitData`는 비공개라 `SerializedObject`.
    static void Link(GameObject prefab, MinionData data)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var brain = root.GetComponent<UnitBrain>();
            if (brain == null) { Debug.LogError($"[골렘] `{path}`에 `UnitBrain`이 없다."); return; }

            var so = new SerializedObject(brain);
            so.FindProperty("unitData").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

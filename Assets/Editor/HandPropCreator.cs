using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **손에 드는 것을 짓는다**(2026-09-09) — 도구(곡괭이·도끼·망치·칼·물뿌리개)와 무기(검·방패·활·
// 창·지팡이). 받은 애니메이션이 요구하는 것이 딱 그것들이다: 채굴 클립은 곡괭이를, 벌목은 도끼를,
// 망치질은 망치를, 가죽 벗기기는 칼을 쥔 손으로 만들어져 있다.
//
// **짓는 길이 둘이다:**
//   **팩 메시** — `Kevin Iglesias` 데모의 도구들(그 클립에 맞춰 만든 것이라 손에 맞는다)과
//   `DoubleL`의 검·방패·활.
//   **블록** — 팩에 없는 창·지팡이(2026-09-11). 폰이 블록 인형이라 기본 프리미티브로 짓는 편이
//   오히려 어울린다. **내장 메시는 프리팹이 가리킬 수 있고 빌드에도 들어간다**(폰의 큐브가 이미
//   그것이다 — `Shader.Find`로 찾는 셰이더가 빌드에서 잘려 나가는 것과 **다른 물음**이다:
//   저쪽은 아무도 가리키지 않아서 잘리고 이쪽은 프리팹이 가리킨다).
//
// 만드는 것 셋:
//   `3_Prefab/Prop/<이름>.prefab`  — 루트에 `HandProp`(쥐는 자리), 자식에 메시. 콜라이더 없음.
//   `2_Material/Prop/<이름>.mat`   — 팩 재질은 빌트인 `Standard`라 URP에서 분홍이다. URP Lit로
//                                    새로 짓고 텍스처·색만 옮긴다. 블록 프롭은 **부위 재질을
//                                    나눠 쓴다**(`Wood`·`Metal`·`Crystal`).
//   `Resources/Data/HandPropSet.asset` — 무기 종류·일 동작 → 프리팹.
//
// **다시 돌려도 된다** — 프리팹·재질·에셋 전부 제자리에서 갱신한다(guid 유지). 다만 프리팹의
// `HandProp` 값(쥐는 자리)은 **있으면 그대로 둔다** — 그건 손으로 맞추는 값이라 도구가 덮으면 안 된다.
public static class HandPropCreator
{
    const string PrefabDir = "Assets/3_Prefab/Prop";
    const string MatDir    = "Assets/2_Material/Prop";
    const string SetPath   = "Assets/Resources/Data/HandPropSet.asset";

    const string KI      = "Assets/assetsss/Kevin Iglesias/Human Animations";
    const string KITools = KI + "/Unity Demo Scenes/Human Crafting Animations/Models/Tools";
    const string KITex   = KI + "/Textures/HumanAnimations_ColorPalette.png";
    const string DL      = "Assets/assetsss/DoubleL/Model";

    // 이름 · 메시 · 왼손인가 · 두 손인가 · 기본 쥐는 회전(한 손). 회전은 **눈으로 맞출 값**이다 —
    // 프리팹 인스펙터에서 `HandProp.GripEuler`를 돌리면 그 도구를 든 모든 폰이 따라온다.
    // 두 손 도구(곡괭이·도끼)는 회전을 안 쓴다 — 양손에서 매 프레임 낸다(`HandProp` 참고).
    static readonly (string name, string fbx, bool left, bool twoHanded, Vector3 euler)[] Packs =
    {
        ("Pickaxe",     KITools + "/Human_Pickaxe.fbx",     false, true,  new Vector3(90f, 0f, 0f)),
        ("Axe",         KITools + "/Human_Axe.fbx",         false, true,  new Vector3(90f, 0f, 0f)),
        ("Hammer",      KITools + "/Human_Hammer.fbx",      false, false, new Vector3(90f, 0f, 0f)),
        ("Knife",       KITools + "/Human_Knife.fbx",       false, false, new Vector3(90f, 0f, 0f)),
        ("WateringCan", KITools + "/Human_WateringCan.fbx", false, false, new Vector3(0f, 0f, 0f)),
        ("Sword",       DL + "/SM_Wep_Sword_03.fbx",        false, false, new Vector3(90f, 0f, 0f)),
        ("Bow",         DL + "/SM_Prop_Bow_02.fbx",         false, false, new Vector3(0f, 0f, 90f)),
        ("Shield",      DL + "/SM_Wep_Shield_01.fbx",       true,  false, new Vector3(0f, 90f, 0f)),
    };

    // 블록으로 짓는 것 — 이름 · 기본 쥐는 자리 · 기본 쥐는 회전 · 짓는 법.
    //
    // **자루는 +Y로 서고 날은 +Z를 본다** — `HandProp`의 기본값(`HeadAxis` +Y · `EdgeAxis` +Z)이
    // 그것이라 맞춰 짓는다(팩 메시도 같은 약속이라 기본 회전이 X 90도다).
    //
    // **둘 다 한 손으로 고정한다 — 두 손 자세는 도구의 것이다.** 긴 자루라 곡괭이처럼
    // `TwoHanded`로 두고 싶어지는데, 그 자세는 자루를 **팔뚝의 연장**으로 내므로 팔이 내려간
    // 동안에는 자루도 같이 내려간다. 도구는 **일하는 중에만 보여서**(`Working`) 늘 팔이 들린
    // 클립 안에 있지만, **무기는 대기·걷기에도 보인다** — 가만히 선 창병의 팔은 내려가 있어
    // 2m 창이 바닥에 박힌다. 고정 그립이면 `GripEuler`·`GripOffset`이라는 **눈으로 맞출
    // 손잡이**가 생기고, 그게 검·활·방패가 이미 선 자리다.
    static readonly (string name, Vector3 grip, Vector3 euler, System.Action<Transform> build)[] Blocks =
    {
        // 쥐는 자리는 **손 뼈 로컬**이고 +Z가 머리 쪽이다(`HandProp`) — 자루 한가운데가 아니라
        // **밑동에서 3분의 1쯤**을 쥐게 +Z로 밀어 둔다(창은 70cm 뒤·140cm 앞).
        ("Spear", new Vector3(0f, 0f, 0.35f), new Vector3(90f, 0f, 0f), BuildSpear),
        ("Staff", new Vector3(0f, 0f, 0.15f), new Vector3(90f, 0f, 0f), BuildStaff),
    };

    // 무기 종류 → 프롭.
    //
    // **투창은 창을 쓴다** — 던지는 창이라 같은 집안이고(*없는 것을 엉뚱한 것으로 보여 주는 것*에
    // 걸리지 않는다), 지금 `Javelin` 아이템은 **하나도 없다**(JSON에서 누가 넣으면 그때 보인다).
    static readonly (WeaponType type, string prop)[] Weapons =
    {
        (WeaponType.OneHandSword, "Sword"), (WeaponType.TwoHandSword, "Sword"),
        (WeaponType.OneHandAxe,   "Axe"),   (WeaponType.TwoHandAxe,   "Axe"),
        (WeaponType.OneHandMace,  "Hammer"),(WeaponType.TwoHandMace,  "Hammer"),
        (WeaponType.Bow,          "Bow"),   (WeaponType.Crossbow,     "Bow"),
        (WeaponType.Polearm,      "Spear"), (WeaponType.Javelin,      "Spear"),
        (WeaponType.Staff,        "Staff"),
        (WeaponType.Shield,       "Shield"),
    };

    // 일 동작 → 도구. 빈손으로 하는 동작(씨 뿌리기·줍기)은 없다.
    static readonly (WorkAnim work, string prop)[] Tools =
    {
        (WorkAnim.Mining,         "Pickaxe"),
        (WorkAnim.Chopping,       "Axe"),
        (WorkAnim.HammerWall,     "Hammer"),
        (WorkAnim.HammerTable,    "Hammer"),
        (WorkAnim.HammerGround,   "Hammer"),
        (WorkAnim.Watering,       "WateringCan"),
        (WorkAnim.Skinning,       "Knife"),
        (WorkAnim.SkinningGround, "Knife"),
    };

    [MenuItem("DungeonColony/Pawn/3. Create Hand Props (도구 · 무기)")]
    public static void Create()
    {
        EnsureDir(PrefabDir);
        EnsureDir(MatDir);
        EnsureDir("Assets/Resources/Data");

        var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(KITex);
        var made = new Dictionary<string, GameObject>();

        foreach (var (name, fbx, left, twoHanded, euler) in Packs)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogWarning($"[HandProp] 메시를 못 찾았다 — {fbx}"); continue; }

            made[name] = SaveProp(name, left, twoHanded, euler, Vector3.zero, root =>
            {
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                mesh.name = "Mesh";
                mesh.transform.localPosition = Vector3.zero;
                mesh.transform.localRotation = Quaternion.identity;
                mesh.transform.localScale    = Vector3.one;
                foreach (var c in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                var mat = UrpMaterial(name, mesh, palette);
                foreach (var r in mesh.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            });
        }

        foreach (var (name, grip, euler, build) in Blocks)
            made[name] = SaveProp(name, left: false, twoHanded: false, euler, grip, build);

        // ── 표 ──────────────────────────────────────────────────────────
        var set = AssetDatabase.LoadAssetAtPath<HandPropSet>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<HandPropSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }
        set.Weapons.Clear();
        set.Tools.Clear();
        foreach (var (type, prop) in Weapons)
            if (made.TryGetValue(prop, out var p)) set.Weapons.Add(new HandPropSet.WeaponEntry { Type = type, Prefab = p });
        foreach (var (work, prop) in Tools)
            if (made.TryGetValue(prop, out var p)) set.Tools.Add(new HandPropSet.ToolEntry { Work = work, Prefab = p });
        EditorUtility.SetDirty(set);

        AssetDatabase.SaveAssets();
        Debug.Log($"[HandProp] 프롭 {made.Count}개 · 무기 {set.Weapons.Count} · 도구 {set.Tools.Count} — {PrefabDir}");
    }

    // 프리팹 하나를 제자리에서 짓는다 — 자식은 통째로 다시 만들고(`fill`), `HandProp`의
    // **사실**(어느 손·몇 손)은 표가 이기며 **눈으로 맞춘 값**(회전·비킴)은 새로 만들 때만 쓴다.
    static GameObject SaveProp(string name, bool left, bool twoHanded, Vector3 euler, Vector3 grip,
                               System.Action<Transform> fill)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        GameObject root;
        if (existing != null)
        {
            root = PrefabUtility.LoadPrefabContents(path);
            var kids = new List<GameObject>();
            foreach (Transform c in root.transform) kids.Add(c.gameObject);
            foreach (var k in kids) Object.DestroyImmediate(k);

            var hp = root.GetComponent<HandProp>();
            if (hp == null) hp = root.AddComponent<HandProp>();
            hp.LeftHand  = left;
            hp.TwoHanded = twoHanded;
        }
        else
        {
            root = new GameObject(name);
            var hp = root.AddComponent<HandProp>();
            hp.LeftHand   = left;
            hp.TwoHanded  = twoHanded;
            hp.GripEuler  = euler;
            hp.GripOffset = grip;
        }

        fill(root.transform);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (existing != null) PrefabUtility.UnloadPrefabContents(root);
        else                  Object.DestroyImmediate(root);
        return saved;
    }

    // ── 블록 프롭 ────────────────────────────────────────────────────────────
    //
    // **팩에 창도 지팡이도 없다.** 그런데 그 둘을 드는 로드아웃이 둘이고(`Loadout_GoblinSpear` ·
    // `Loadout_CultMagus`) 둘 다 습격으로 오므로, 빈손으로 두면 **맨손으로 싸우는 것처럼 보인다.**
    //
    // 눈금은 폰의 키(`PawnMover.BodyHeight` 1.8m)에 맞춘다 — 창 2.1m · 지팡이 1.9m.
    // 유니티 실린더는 높이 2·지름 1이라 **스케일이 곧 굵기와 절반 길이**다.

    static void BuildSpear(Transform root)
    {
        Block(root, "Shaft",   PrimitiveType.Cylinder, new Vector3(0f, 0f,    0f), new Vector3(0.07f,  1.05f, 0.07f),  Wood);
        Block(root, "Butt",    PrimitiveType.Cylinder, new Vector3(0f, -1.0f, 0f), new Vector3(0.085f, 0.05f, 0.085f), Metal);
        Block(root, "Ferrule", PrimitiveType.Cylinder, new Vector3(0f, 1.02f, 0f), new Vector3(0.095f, 0.06f, 0.095f), Metal);
        // 촉은 납작한 잎이다 — 모로 보면 얇고(2cm) 날이 `+Z`를 본다(`EdgeAxis` 기본값).
        Block(root, "Head",    PrimitiveType.Cube,     new Vector3(0f, 1.22f, 0f), new Vector3(0.075f, 0.30f, 0.022f), Metal);
    }

    static void BuildStaff(Transform root)
    {
        Block(root, "Shaft",  PrimitiveType.Cylinder, new Vector3(0f, 0f,    0f), new Vector3(0.06f, 0.95f, 0.06f), Wood);
        Block(root, "Collar", PrimitiveType.Cylinder, new Vector3(0f, 0.93f, 0f), new Vector3(0.09f, 0.05f, 0.09f), Metal);
        // **원소로 색을 안 가른다** — 프롭은 무기 종류당 하나인데 지팡이는 여덟이라(화염·수류·
        // 암흑…) 하나를 골라 칠하면 **일곱은 거짓말**이 된다. 맑은 수정 하나로 둔다.
        Block(root, "Orb",    PrimitiveType.Sphere,   new Vector3(0f, 1.04f, 0f), new Vector3(0.16f, 0.16f, 0.16f), Crystal);
    }

    static void Block(Transform parent, string name, PrimitiveType shape, Vector3 pos, Vector3 scale,
                      System.Func<Material> mat)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;

        // **콜라이더를 뗀다** — 커서 레이캐스트가 마스크 없이 전부를 훑어서, 남기면 폰을
        // 고르려다 손에 든 창이 잡힌다(`PawnProps`가 런타임에 한 번 더 떼지만 에셋에 남겨
        // 두면 프리팹을 씬에 끌어다 놓는 자리에서 그대로 걸린다).
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial    = mat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    // 부위 재질 — **프롭마다 나무를 한 벌씩 만들지 않는다**(여러 벌을 손으로 맞추게 되고 그중
    // 하나는 반드시 뒤처진다 — 바이옴마다 재질을 만들지 않는 것과 같은 규칙).
    static Material Wood()    => FlatMaterial("Wood",    new Color(0.42f, 0.30f, 0.19f), 0.15f);
    static Material Metal()   => FlatMaterial("Metal",   new Color(0.62f, 0.64f, 0.68f), 0.55f);
    static Material Crystal() => FlatMaterial("Crystal", new Color(0.58f, 0.72f, 0.82f), 0.80f);

    static Material FlatMaterial(string name, Color color, float smoothness)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(UrpLit());
            AssetDatabase.CreateAsset(mat, path);
        }
        // **텍스처를 비운다** — 프리미티브의 기본 재질에서 갈아 끼우는 것이라, 전에 무엇이
        // 물려 있었든 색만으로 칠한다.
        if (mat.HasProperty("_BaseMap"))    mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 팩 재질(빌트인 `Standard`)의 텍스처·색을 URP Lit로 옮긴다. 재질이 없는 메시(KI 도구)는
    // 팩의 팔레트 텍스처를 물린다 — 데모 씬이 그렇게 쓴다.
    static Material UrpMaterial(string name, GameObject mesh, Texture2D palette)
    {
        Texture tex = palette; Color color = Color.white;
        var r0 = mesh.GetComponentInChildren<Renderer>(true);
        var src = r0 != null ? r0.sharedMaterial : null;
        if (src != null)
        {
            if (src.HasProperty("_MainTex") && src.mainTexture != null) tex = src.mainTexture;
            if (src.HasProperty("_Color")) color = src.color;
        }

        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(UrpLit());
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseMap"))    mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Shader UrpLit()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        return sh != null ? sh : Shader.Find("Standard");
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}

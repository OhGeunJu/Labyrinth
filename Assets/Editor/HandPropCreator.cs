using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **손에 드는 것을 팩 메시로 짓는다**(2026-09-09) — 도구(곡괭이·도끼·망치·칼·물뿌리개)와 무기(검·
// 방패·활). 받은 애니메이션이 요구하는 것이 딱 그것들이다: 채굴 클립은 곡괭이를, 벌목은 도끼를,
// 망치질은 망치를, 가죽 벗기기는 칼을 쥔 손으로 만들어져 있다.
//
// 메시는 애니메이션 팩이 같이 준 것을 쓴다 — `Kevin Iglesias` 데모의 도구들(그 클립에 맞춰 만든
// 것이라 손에 맞는다)과 `DoubleL`의 검·방패·활. **새로 모델링하지 않는다**: 폰이 블록 인형이라
// 도구도 단순한 편이 어울리고, 팩의 것이 이미 그렇다.
//
// 만드는 것 셋:
//   `3_Prefab/Prop/<이름>.prefab`  — 루트에 `HandProp`(쥐는 자리), 자식에 메시. 콜라이더 없음.
//   `2_Material/Prop/<이름>.mat`   — 팩 재질은 빌트인 `Standard`라 URP에서 분홍이다. URP Lit로
//                                    새로 짓고 텍스처·색만 옮긴다.
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
    static readonly (string name, string fbx, bool left, bool twoHanded, Vector3 euler)[] Props =
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

    // 무기 종류 → 프롭. 모델이 없는 종류(창·투창·지팡이)는 빈손이다 — 없는 것을 엉뚱한 것으로
    // 보여 주는 것보다 낫다.
    static readonly (WeaponType type, string prop)[] Weapons =
    {
        (WeaponType.OneHandSword, "Sword"), (WeaponType.TwoHandSword, "Sword"),
        (WeaponType.OneHandAxe,   "Axe"),   (WeaponType.TwoHandAxe,   "Axe"),
        (WeaponType.OneHandMace,  "Hammer"),(WeaponType.TwoHandMace,  "Hammer"),
        (WeaponType.Bow,          "Bow"),   (WeaponType.Crossbow,     "Bow"),
        (WeaponType.Shield,       "Shield"),
    };

    // 일 동작 → 도구. 빈손으로 하는 동작(씨 뿌리기·줍기)은 없다.
    static readonly (WorkAnim work, string prop)[] Tools =
    {
        (WorkAnim.Mining,      "Pickaxe"),
        (WorkAnim.Chopping,    "Axe"),
        (WorkAnim.HammerWall,  "Hammer"),
        (WorkAnim.HammerTable, "Hammer"),
        (WorkAnim.Watering,    "WateringCan"),
        (WorkAnim.Skinning,    "Knife"),
    };

    [MenuItem("DungeonColony/Pawn/3. Create Hand Props (도구 · 무기)")]
    public static void Create()
    {
        EnsureDir(PrefabDir);
        EnsureDir(MatDir);
        EnsureDir("Assets/Resources/Data");

        var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(KITex);
        var made = new Dictionary<string, GameObject>();

        foreach (var (name, fbx, left, twoHanded, euler) in Props)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogWarning($"[HandProp] 메시를 못 찾았다 — {fbx}"); continue; }

            string path = PrefabDir + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            GameObject root;
            if (existing != null)
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var kids = new List<GameObject>();
                foreach (Transform c in root.transform) kids.Add(c.gameObject);
                foreach (var k in kids) Object.DestroyImmediate(k);

                // 어느 손·몇 손인가는 **도구의 사실**이라 표가 이긴다. 회전·비킴은 눈으로 맞춘 값이라 둔다.
                var hp = root.GetComponent<HandProp>();
                if (hp == null) hp = root.AddComponent<HandProp>();
                hp.LeftHand  = left;
                hp.TwoHanded = twoHanded;
            }
            else
            {
                root = new GameObject(name);
                var hp = root.AddComponent<HandProp>();
                hp.LeftHand  = left;
                hp.TwoHanded = twoHanded;
                hp.GripEuler = euler;
            }

            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
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

            GameObject saved;
            if (existing != null)
            {
                saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            made[name] = saved;
        }

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
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseMap"))    mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
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

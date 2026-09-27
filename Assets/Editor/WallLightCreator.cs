using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **벽에 박힌 조명을 짓고 바이옴에 물린다**(2026-09-28) — 야광석 · 폐광 횃불.
//
// 런타임 규칙은 `WallLightData` 머리에 있다. 여기가 하는 일은 다섯이다:
//
//   1. 아이템 `야광석`(`Res_Glowstone`) — 벽을 파면 덤으로 나온다
//   2. 재질 `Glowstone.mat` — 팩 결정 재질을 떠서 **스스로 빛나게** 한다
//   3. 프리팹 둘(`3_Prefab/Prop/`) — 피벗 = 벽면, 앞(+Z) = 벽 바깥
//   4. 데이터 둘(`5_Data/World/WallLight_*.asset`)
//   5. 바이옴 배선 — 야광석은 **모든 바이옴**, 횃불은 **폐광만**(사용자 결정)
//
// ## 다시 돌려도 손질을 안 지운다
//
// **프리팹이 이미 있으면 그대로 둔다** — 자리·크기를 눈으로 맞춘 것은 저작이다(무덤 뚜껑에서
// 배운 자리 — `WallTorchDresser.DressGrave`). 새로 짓고 싶으면 프리팹을 지우고 돌리면 된다.
// **바이옴의 확률도 이미 적혀 있으면 안 건드린다** — 없는 줄만 더한다(얼마나 나올지는 사용자가
// 정하는 값이다).
public static class WallLightCreator
{
    const string Cave     = "Assets/assetsss/3DForge/Cave Adventure kit/Prefabs";
    const string Crystal  = Cave + "/Special/Crystals/mana/mana_crystal2.prefab";
    const string CrystalDir = Cave + "/Special/Crystals/mana";

    // **모양 후보**(2026-09-28, 사용자: *"너무 똑같은게 배치되어 있는 느낌"*) — 결정 덩이 넷과 외톨이
    // 조각 셋. 덩이만 있으면 벽마다 같은 무더기가 박혀 보이고, 조각이 섞여야 *자라난 것*으로 읽힌다.
    static readonly (string src, float size)[] Shapes =
    {
        ("mana_crystal1", 0.75f), ("mana_crystal2", 0.7f), ("mana_crystal3", 0.8f), ("mana_crystal4", 0.65f),
        ("mana_shard1",   0.5f),  ("mana_shard2",   0.45f), ("mana_shard3",  0.55f),
    };
    const string TorchSrc = "Assets/3_Prefab/Building/WallTorch.prefab";

    const string ItemPath   = "Assets/Resources/Data/Items/Res_Glowstone.asset";
    const string MatPath    = "Assets/2_Material/Glowstone.mat";
    const string PrefabDir  = "Assets/3_Prefab/Prop";
    const string GlowPrefab = PrefabDir + "/WallLight_Glowstone.prefab";
    const string TorchPrefab= PrefabDir + "/WallLight_MineTorch.prefab";
    const string DataDir    = "Assets/5_Data/World";
    const string GlowData   = DataDir + "/WallLight_Glowstone.asset";
    const string TorchData  = DataDir + "/WallLight_MineTorch.asset";
    const string BiomeDir   = "Assets/Resources/Data/Biomes";

    // 야광석의 빛 — 푸른 기가 도는 흰빛. 횃불(주황)과 한눈에 갈려야 *무엇이 밝히나*가 읽힌다.
    static readonly Color GlowColor = new(0.55f, 0.85f, 1f);

    const float GlowChance  = 0.03f;   // 모든 바이옴 — 벽 서른세 칸에 하나꼴
    const float TorchChance = 0.05f;   // 폐광 — 스무 칸에 하나꼴

    [MenuItem("DungeonColony/World/K. Create Wall Lights (야광석 · 폐광 횃불)")]
    static void Run()
    {
        EnsureFolder(PrefabDir);

        var item  = MakeItem();
        var mat   = MakeMaterial();
        var glowP = AssetDatabase.LoadAssetAtPath<GameObject>(GlowPrefab)  ?? MakeGlowPrefab(mat, Crystal, GlowPrefab, CrystalSize);
        var torch = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefab) ?? MakeTorchPrefab();

        // 모양 후보 — 첫 프리팹(`mana_crystal2`)은 위의 것을 그대로 쓴다(이미 손본 것일 수 있다).
        var variants = new List<GameObject>();
        foreach (var (src, size) in Shapes)
        {
            if (src == "mana_crystal2") { if (glowP != null) variants.Add(glowP); continue; }
            string outPath = PrefabDir + "/WallLight_Glowstone_" + src.Replace("mana_", "") + ".prefab";
            var v = AssetDatabase.LoadAssetAtPath<GameObject>(outPath)
                 ?? MakeGlowPrefab(mat, CrystalDir + "/" + src + ".prefab", outPath, size);
            if (v != null) variants.Add(v);
        }

        if (glowP == null || torch == null)
        {
            Debug.LogError("[벽 조명] 프리팹을 못 지었습니다 — 위 오류를 보세요.");
            return;
        }

        var glow = Data(GlowData, "야광석", glowP, item, 1, 2, 0.6f, 2.0f, 0.3f);
        foreach (var v in variants) Relean(v);
        Vary(glow, variants);
        var mine = Data(TorchData, "횃불", torch, null, 0, 0, 1.1f, 1.1f, 0.2f);

        int glowN = 0, torchN = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b == null) continue;

            bool dirty = false;
            if (AddEntry(b, glow, GlowChance)) { glowN++; dirty = true; }
            if (b.Biome == Biome.Mine && AddEntry(b, mine, TorchChance)) { torchN++; dirty = true; }
            if (dirty) EditorUtility.SetDirty(b);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[벽 조명] 야광석을 바이옴 {glowN}곳에, 횃불을 폐광 {torchN}곳에 새로 물렸습니다.\n" +
                  $"얼마나 나올지는 `{BiomeDir}`의 각 바이옴 → **벽에 박힌 조명** 목록의 Chance로 정합니다 " +
                  $"(야광석 {GlowChance} · 횃불 {TorchChance}로 시작). **이미 적힌 값은 안 건드렸습니다.**\n" +
                  "불빛은 판에서만 섭니다(씬 뷰에는 안 뜹니다).");
    }

    // ── 아이템 ───────────────────────────────────────────────────────────────

    static ResourceItemData MakeItem()
    {
        var r = AssetDatabase.LoadAssetAtPath<ResourceItemData>(ItemPath);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<ResourceItemData>();
            AssetDatabase.CreateAsset(r, ItemPath);
        }

        r.Id          = "res_glowstone";
        r.ItemName    = "야광석";
        r.Description = "어둠 속에서 스스로 빛나는 돌. 굴의 벽에 박혀 있다.";
        r.Weight      = 0.5f;
        r.Width       = 1;
        r.Height      = 1;
        r.BaseValue   = 40;
        r.Group       = ResourceGroup.Gem;
        r.Stack       = 200;
        EditorUtility.SetDirty(r);
        return r;
    }

    // ── 재질 ─────────────────────────────────────────────────────────────────

    // **팩 재질을 뜬다 — 고치지 않는다.** 같은 재질을 다른 결정 소품이 쓰고 있으면 그것까지 빛난다.
    static Material MakeMaterial()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Crystal);
        var srcMat = src != null ? src.GetComponentInChildren<Renderer>(true)?.sharedMaterial : null;

        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null)
        {
            m = srcMat != null ? new Material(srcMat)
                               : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, MatPath);
        }

        m.name = "Glowstone";
        m.SetColor("_BaseColor", Color.Lerp(Color.white, GlowColor, 0.5f));
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", GlowColor * 1.6f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ── 프리팹: 야광석 ───────────────────────────────────────────────────────

    const float CrystalSize = 0.7f;   // 가장 긴 변(m)

    // **위로 자란다**(2026-09-28, 사용자: *"이렇게 달랑 내려와있는건 좀 별로"* — 스크린샷: 결정 하나가 벽에서 수평으로
    // 튀어나와 아래를 향했다). 처음엔 65°로 눕혔고(거의 수평 막대) 벽 바깥 축으로 180°까지 돌게 둬서 **그 막대가
    // 아래를 볼 수 있었다.** 결정은 벽에서 **위로 비스듬히** 솟는 것이 자연스럽다 — 45°로 세우고, 도는 폭은
    // `Vary`가 ±50°로 묶는다(옆으로는 기울되 아래로는 안 간다). 밑동은 더 묻는다 — 벽에서 떠 보였다.
    const float ArtLean = 45f;
    const float ArtSink = -0.14f;

    static GameObject MakeGlowPrefab(Material mat, string srcPath, string outPath, float size)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
        if (src == null) { Debug.LogError($"[벽 조명] 결정 소품을 못 찾았습니다 — {srcPath}"); return null; }

        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(outPath));
        try
        {
            var art = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            art.name = "[Art]";
            art.transform.SetParent(root.transform, false);
            StripColliders(art);

            // **벽에서 비스듬히 솟게 한다** — 모형의 위(+Y)를 벽 바깥(+Z) 쪽으로 `ArtLean`만큼 눕힌다.
            // 90°면 벽에 수직으로 박힌 막대, 0°면 벽을 따라 선 기둥이 된다.
            art.transform.localRotation = Quaternion.Euler(ArtLean, 0f, 0f);
            art.transform.localScale    = Vector3.one * FitScale(art, size);
            art.transform.localPosition = new Vector3(0f, 0f, ArtSink);

            foreach (var r in art.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var lampGo = new GameObject("Light");
            lampGo.transform.SetParent(root.transform, false);
            lampGo.transform.localPosition = new Vector3(0f, 0.1f, 0.5f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type      = LightType.Point;
            lamp.color     = GlowColor;
            lamp.intensity = 1.6f;
            lamp.range     = 6f;
            lamp.shadows   = LightShadows.None;

            // **흔들지 않는다** — 불이 아니라 돌이다. 깜빡이면 전구가 나가는 것처럼 보인다.
            AddLightSource(root, lamp, null, radius: 5f, strength: 0.7f, flicker: 0f);

            return PrefabUtility.SaveAsPrefabAsset(root, outPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ── 프리팹: 폐광 횃불 ─────────────────────────────────────────────────────
    //
    // **벽걸이 횃불(`WallTorch`)의 벽 모양을 그대로 떠 온다** — 모형·불꽃·등을 새로 맞추면
    // 같은 횃불이 지은 것과 자연 것에서 다르게 생긴다. 건물 부품(`PlacedBuilding`·`Campfire`·
    // `WallTorch`)은 안 가져온다: 이것은 **짓지 않은 불**이고 밤낮 없이 탄다.
    static GameObject MakeTorchPrefab()
    {
        var contents = PrefabUtility.LoadPrefabContents(TorchSrc);
        var root = new GameObject("WallLight_MineTorch");
        try
        {
            var wt = contents.GetComponent<WallTorch>();
            var ls = contents.GetComponent<LightSource>();
            if (wt == null || ls == null)
            {
                Debug.LogError("[벽 조명] `WallTorch`/`LightSource`가 없습니다 — " + TorchSrc);
                return null;
            }

            var wso  = new SerializedObject(wt);
            var wall = wso.FindProperty("wall")?.objectReferenceValue as GameObject;
            var head = wso.FindProperty("head")?.objectReferenceValue as Transform;
            float wallHead   = wso.FindProperty("wallHead").floatValue;
            float wallOffset = wso.FindProperty("wallOffset").floatValue;
            float wallYaw    = wso.FindProperty("wallYaw").floatValue;

            var lso    = new SerializedObject(ls);
            var flames = lso.FindProperty("flames")?.objectReferenceValue as GameObject;
            float radius   = lso.FindProperty("radius").floatValue;
            float strength = lso.FindProperty("strength").floatValue;
            float flicker  = lso.FindProperty("flicker").floatValue;

            if (wall == null || head == null)
            {
                Debug.LogError("[벽 조명] 횃불의 `wall`/`head`가 비어 있습니다 — 프리팹 배선을 확인하세요.");
                return null;
            }

            // **자리는 `WallTorch.PoseAt`의 벽 모양과 같게** — 거기서는 칸 한가운데에서
            // `wallOffset`만큼 벽 쪽으로 밀고 벽을 등지게 돌린다. 여기 피벗은 벽면이라 그 나머지
            // (반 칸 − wallOffset)만큼 벽 밖에 두고, 등지는 것은 180°로 돈다.
            var mount = new GameObject("Mount").transform;
            mount.SetParent(root.transform, false);
            mount.localPosition = new Vector3(0f, 0f, Mathf.Max(0f, 0.5f - wallOffset));
            mount.localRotation = Quaternion.Euler(0f, 180f + wallYaw, 0f);

            var wallCopy = Object.Instantiate(wall, mount, false);
            wallCopy.name = "Wall";
            wallCopy.SetActive(true);
            StripColliders(wallCopy);

            var headCopy = Object.Instantiate(head.gameObject, mount, false);
            headCopy.name = "Head";
            headCopy.transform.localPosition = new Vector3(0f, wallHead, 0f);
            StripColliders(headCopy);

            // 불꽃이 머리 아래에 있으면 사본에서 같은 자리를 찾는다.
            GameObject flamesCopy = null;
            if (flames != null && flames.transform.IsChildOf(head))
            {
                string path = AnimationUtility.CalculateTransformPath(flames.transform, head);
                var t = string.IsNullOrEmpty(path) ? headCopy.transform : headCopy.transform.Find(path);
                if (t != null) flamesCopy = t.gameObject;
            }
            if (flamesCopy != null) flamesCopy.SetActive(true);

            var lamp = headCopy.GetComponentInChildren<Light>(true);
            if (lamp != null) lamp.enabled = true;

            AddLightSource(root, lamp, flamesCopy, radius, strength, flicker);

            return PrefabUtility.SaveAsPrefabAsset(root, TorchPrefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ── 거들이 ───────────────────────────────────────────────────────────────

    static void AddLightSource(GameObject root, Light lamp, GameObject flames,
                               float radius, float strength, float flicker)
    {
        var ls = root.AddComponent<LightSource>();
        var so = new SerializedObject(ls);
        so.FindProperty("radius").floatValue             = radius;
        so.FindProperty("strength").floatValue           = strength;
        so.FindProperty("flicker").floatValue            = flicker;
        so.FindProperty("alwaysLit").boolValue           = true;
        so.FindProperty("lamp").objectReferenceValue     = lamp;
        so.FindProperty("flames").objectReferenceValue   = flames;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // **콜라이더를 뗀다** — 벽 조명은 짚을 것도 막을 것도 아니다. 남겨 두면 커서가 벽 대신
    // 결정을 짚어 캘 칸을 못 고른다(`VisiblePick`).
    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    // 모형의 가장 긴 변이 `size`가 되는 배율.
    static float FitScale(GameObject go, float size)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return 1f;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        float m = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        return m > 0.0001f ? size / m : 1f;
    }

    static WallLightData Data(string path, string name, GameObject prefab, ItemData drop,
                              int min, int max, float hMin, float hMax, float jitter)
    {
        var d = AssetDatabase.LoadAssetAtPath<WallLightData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<WallLightData>();
            d.DisplayName = name;
            d.DropItem    = drop;
            d.DropMin     = min;
            d.DropMax     = max;
            d.HeightMin   = hMin;
            d.HeightMax   = hMax;
            d.Jitter      = jitter;
            AssetDatabase.CreateAsset(d, path);
        }

        // **프리팹만은 늘 다시 물린다** — 프리팹을 지우고 새로 지으면 guid가 바뀐다.
        d.Prefab = prefab;
        EditorUtility.SetDirty(d);
        return d;
    }

    // **예전 각(65°)으로 지은 결정만 고쳐 세운다** — 사람이 다른 각으로 맞춘 것은 그대로 둔다(손질은 저작이다).
    static void Relean(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path)) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var art = root.transform.Find("[Art]");
            if (art == null || Mathf.Abs(Mathf.DeltaAngle(art.localEulerAngles.x, 65f)) > 0.5f) return;

            art.localRotation = Quaternion.Euler(ArtLean, 0f, 0f);
            art.localPosition = new Vector3(art.localPosition.x, art.localPosition.y, ArtSink);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[벽 조명] {prefab.name}: 결정을 {ArtLean}°로 세우고 밑동을 더 묻었습니다(예전 65°).");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // **변형 값은 손대지 않은 것만 채운다** — 배율이 1·각이 0이면 아직 아무도 안 정한 것이다.
    static void Vary(WallLightData d, List<GameObject> variants)
    {
        if (d.Variants == null || d.Variants.Length == 0) d.Variants = variants.ToArray();

        // **폭을 넉넉히**(2026-09-28, 사용자: 변형 폭 키우기 · 무리 짓기) — 처음 잡은 0.7~1.3배 · 15° · 0.06은
        // 한 벽에 여럿이 서면 여전히 같은 물건으로 읽혔다.
        bool untouched = d.ScaleRange == Vector2.one && d.Roll == 0f && d.Tilt == 0f && d.HueJitter == 0f;
        if (untouched)
        {
            d.ScaleRange = new Vector2(0.55f, 1.5f);
            d.Roll       = 50f;     // 옆으로만 — 180이면 아래를 향해 **늘어진다**(위 `ArtLean` 참고)
            d.Tilt       = 20f;
            d.HueJitter  = 0.1f;    // 하늘색 ~ 보랏빛 남색
        }

        // 예전 기본값(180)이 그대로면 옆으로만 돌게 좁힌다 — 180은 사람이 고른 값이 아니라 내가 처음 넣은 값이다.
        if (d.Roll == 180f) d.Roll = 50f;

        if (d.ClusterMin == 1 && d.ClusterMax == 1)
        {
            d.ClusterMin    = 1;
            d.ClusterMax    = 4;      // 하나 ~ 넷 — 외톨이도 섞여야 무더기가 무더기로 읽힌다
            d.ClusterSpread = 0.35f;
            d.MemberScale   = new Vector2(0.45f, 0.85f);
        }
        EditorUtility.SetDirty(d);
    }

    static bool AddEntry(BiomeContentData b, WallLightData light, float chance)
    {
        b.WallLights ??= new List<BiomeContentData.WallLightEntry>();
        foreach (var e in b.WallLights)
            if (e != null && e.Light == light) return false;

        b.WallLights.Add(new BiomeContentData.WallLightEntry { Light = light, Chance = chance });
        return true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
